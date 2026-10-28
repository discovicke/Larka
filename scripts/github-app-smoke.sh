#!/usr/bin/env bash
set -euo pipefail

# Smoke-test a GitHub App installation without starting the backend.
#
# Credentials are read from .NET user-secrets or environment variables.
#
# Required:
#   GitHub:AppId              or GITHUB_APP_ID
#   GitHub:PrivateKeyPath     or GITHUB_APP_PRIVATE_KEY_PATH
#   GitHub:InstallationId     or GITHUB_INSTALLATION_ID
#   GitHub:Org                or GITHUB_ORG
#
# Optional:
#   GitHub:TemplateRepo       or GITHUB_TEMPLATE_REPO   default: Template
#   GITHUB_SMOKE_REPO         create this repo from the template
#   GITHUB_TEST_USER          add this user as collaborator to the smoke repo
#   GITHUB_DELETE_SMOKE=1     delete the smoke repo afterwards
#
# The private key must stay outside the repository. Do not commit it.

# Values come from environment variables or .NET user-secrets:
#   dotnet user-secrets set "GitHub:AppId" "..." --project backend
#   dotnet user-secrets set "GitHub:PrivateKeyPath" "..." --project backend
#   dotnet user-secrets set "GitHub:InstallationId" "..." --project backend
#   dotnet user-secrets set "GitHub:Org" "..." --project backend
# Environment variables win, which makes CI and one-off runs easy.
secrets_json=""
if command -v dotnet >/dev/null 2>&1; then
  secrets_json=$(dotnet user-secrets list --json --project backend 2>/dev/null \
    | sed '/^\/\/BEGIN$/d;/^\/\/END$/d' || true)
fi

secret_value() {
  if [[ -z "$secrets_json" ]]; then
    return 0
  fi
  jq -r --arg key "$1" '.[$key] // empty' <<<"$secrets_json"
}

GITHUB_APP_ID="${GITHUB_APP_ID:-$(secret_value 'GitHub:AppId')}"
GITHUB_APP_PRIVATE_KEY_PATH="${GITHUB_APP_PRIVATE_KEY_PATH:-$(secret_value 'GitHub:PrivateKeyPath')}"
GITHUB_INSTALLATION_ID="${GITHUB_INSTALLATION_ID:-$(secret_value 'GitHub:InstallationId')}"
GITHUB_ORG="${GITHUB_ORG:-$(secret_value 'GitHub:Org')}"
GITHUB_TEMPLATE_REPO="${GITHUB_TEMPLATE_REPO:-$(secret_value 'GitHub:TemplateRepo')}"
GITHUB_TEMPLATE_REPO="${GITHUB_TEMPLATE_REPO:-Template}"
GITHUB_APP_PRIVATE_KEY_PATH="${GITHUB_APP_PRIVATE_KEY_PATH:-$HOME/.config/rebornlms/github-app.pem}"
GITHUB_API="${GITHUB_API:-https://api.github.com}"

: "${GITHUB_APP_ID:?Set GitHub:AppId in user-secrets or GITHUB_APP_ID}"
: "${GITHUB_INSTALLATION_ID:?Set GitHub:InstallationId in user-secrets or GITHUB_INSTALLATION_ID}"
: "${GITHUB_ORG:?Set GitHub:Org in user-secrets or GITHUB_ORG}"

if [[ ! -r "$GITHUB_APP_PRIVATE_KEY_PATH" ]]; then
  echo "Private key is not readable: $GITHUB_APP_PRIVATE_KEY_PATH" >&2
  exit 1
fi

b64url() {
  openssl base64 -e -A | tr '+/' '-_' | tr -d '='
}

# GitHub requires iat/exp in the JWT. Keep exp below the 10-minute maximum.
now=$(date +%s)
header=$(printf '%s' '{"alg":"RS256","typ":"JWT"}' | b64url)
payload=$(printf '{"iat":%s,"exp":%s,"iss":"%s"}' "$((now - 60))" "$((now + 540))" "$GITHUB_APP_ID" | b64url)
signature=$(printf '%s.%s' "$header" "$payload" \
  | openssl dgst -sha256 -sign "$GITHUB_APP_PRIVATE_KEY_PATH" \
  | b64url)
jwt="$header.$payload.$signature"

api() {
  local method="$1"
  local path="$2"
  local token="$3"
  local data="${4:-}"
  local args=(
    -sS
    -X "$method"
    "$GITHUB_API$path"
    -H "Accept: application/vnd.github+json"
    -H "X-GitHub-Api-Version: 2022-11-28"
    -H "Authorization: Bearer $token"
  )

  if [[ -n "$data" ]]; then
    args+=(-H "Content-Type: application/json" -d "$data")
  fi

  curl "${args[@]}"
}

echo "== App =="
app=$(api GET /app "$jwt")
if ! jq -e '.id' >/dev/null 2>&1 <<<"$app"; then
  echo "$app" >&2
  exit 1
fi
jq '{id, slug, name, owner: .owner.login}' <<<"$app"

echo
echo "== Installation token =="
token_response=$(api POST "/app/installations/$GITHUB_INSTALLATION_ID/access_tokens" "$jwt" '{}')
if ! jq -e '.token' >/dev/null 2>&1 <<<"$token_response"; then
  echo "$token_response" >&2
  exit 1
fi
token=$(jq -r '.token' <<<"$token_response")
jq '{expires_at, repository_selection, permissions}' <<<"$token_response"

echo
echo "== Repositories visible to the installation =="
repos=$(api GET '/installation/repositories?per_page=100' "$token")
jq -r '.repositories[] | "\(.full_name) private=\(.private) template=\(.is_template)"' <<<"$repos"

template_full_name="$GITHUB_ORG/$GITHUB_TEMPLATE_REPO"
if ! jq -e --arg name "$template_full_name" \
  '.repositories[] | select(.full_name == $name)' >/dev/null <<<"$repos"; then
  echo "Template repo is not visible to the installation: $template_full_name" >&2
  exit 1
fi

echo
echo "== Latest commit in $template_full_name =="
latest=$(api GET "/repos/$template_full_name/commits?per_page=1" "$token")
jq -r '.[0] | "\(.sha) \(.commit.message | split("\n")[0])"' <<<"$latest"

if [[ -n "${GITHUB_SMOKE_REPO:-}" ]]; then
  echo
  echo "== Create $GITHUB_ORG/$GITHUB_SMOKE_REPO from template =="
  create_body=$(jq -nc \
    --arg owner "$GITHUB_ORG" \
    --arg name "$GITHUB_SMOKE_REPO" \
    '{owner: $owner, name: $name, private: true}')
  created=$(api POST "/repos/$template_full_name/generate" "$token" "$create_body")
  if ! jq -e '.full_name' >/dev/null 2>&1 <<<"$created"; then
    echo "$created" >&2
    exit 1
  fi
  jq '{full_name, private, html_url}' <<<"$created"

  if [[ -n "${GITHUB_TEST_USER:-}" ]]; then
    echo
    echo "== Add $GITHUB_TEST_USER as collaborator =="
    api PUT "/repos/$GITHUB_ORG/$GITHUB_SMOKE_REPO/collaborators/$GITHUB_TEST_USER" \
      "$token" '{"permission":"push"}' | jq .
  fi

  if [[ "${GITHUB_DELETE_SMOKE:-}" == "1" ]]; then
    echo
    echo "== Delete $GITHUB_ORG/$GITHUB_SMOKE_REPO =="
    api DELETE "/repos/$GITHUB_ORG/$GITHUB_SMOKE_REPO" "$token" | jq .
  fi
fi
