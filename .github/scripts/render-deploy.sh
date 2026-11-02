#!/usr/bin/env bash
# Deployar en image till Render, väntar tills den är live och kontrollerar att rätt version svarar.
# Körs av deploy-jobbet i cd.yml. Misslyckas en deploy behåller Render den gamla versionen live.
#
# Inställningarna kommer från Infisical (projektet Lärka, miljön prod). Jobbet loggar in med GitHubs OIDC-token, så
# ingen hemlighet ligger i GitHub. Mappen /deploy har deployens egna nycklar. Roten är appens inställningar, och
# Render får exakt dem: allt som inte finns i Infisical tas bort från tjänsten.
set -euo pipefail

: "${INFISICAL_IDENTITY_ID:?saknas}" "${INFISICAL_PROJECT_ID:?saknas}" "${APP_URL:?saknas}"
: "${IMAGE:?saknas}" "${EXPECTED_VERSION:?saknas}" "${EXPECTED_COMMIT:?saknas}"
INFISICAL=https://eu.infisical.com/api

github_token=$(curl -fsS -H "Authorization: bearer $ACTIONS_ID_TOKEN_REQUEST_TOKEN" "$ACTIONS_ID_TOKEN_REQUEST_URL" |
  jq -r .value)
infisical_token=$(curl -fsS -X POST "$INFISICAL/v1/auth/oidc-auth/login" -H "Content-Type: application/json" \
  -d "$(jq -n --arg id "$INFISICAL_IDENTITY_ID" --arg jwt "$github_token" '{identityId: $id, jwt: $jwt}')" |
  jq -r .accessToken)

# Hemligheterna under en sökväg som {nyckel: värde} i en fil. Längre värden maskas i loggen.
secrets() {
  curl -fsS -H "Authorization: Bearer $infisical_token" \
    "$INFISICAL/v4/secrets?projectId=$INFISICAL_PROJECT_ID&environment=prod&secretPath=$1" |
    jq '.secrets | map({(.secretKey): .secretValue}) | add // {}' >"$2"
  jq -r '.[] | select(length > 8)' "$2" | while IFS= read -r value; do echo "::add-mask::$value"; done
}

workdir=$(mktemp -d)
trap 'rm -rf "$workdir"' EXIT
secrets /deploy "$workdir/deploy.json"
secrets / "$workdir/settings.json"
deploy=$(cat "$workdir/deploy.json")
settings=$(cat "$workdir/settings.json")
RENDER_API_KEY=$(jq -r '.RENDER_API_KEY // empty' <<<"$deploy")
RENDER_SERVICE_ID=$(jq -r '.RENDER_SERVICE_ID // empty' <<<"$deploy")
: "${RENDER_API_KEY:?saknas i Infisical prod /deploy}" "${RENDER_SERVICE_ID:?saknas i Infisical prod /deploy}"

# Utan dessa startar appen inte, så stoppa innan något ändras.
for required in ConnectionStrings__Default Auth__Jwt__Key; do
  if [ -z "$(jq -r --arg key "$required" '.[$key] // empty' <<<"$settings")" ]; then
    echo "::error::$required saknas i Infisical prod"
    exit 1
  fi
done

API="https://api.render.com/v1/services/$RENDER_SERVICE_ID"

api() {
  curl -fsS -H "Authorization: Bearer $RENDER_API_KEY" -H "Content-Type: application/json" "$@"
}

# Ersätter alla variabler på tjänsten. PORT och miljön hör till Render, inte till appens inställningar.
api -X PUT "$API/env-vars" -d "$(jq '. + {ASPNETCORE_ENVIRONMENT: "Production", PORT: "8080"} |
  to_entries | map({key, value})' <<<"$settings")" >/dev/null
echo "Render har nu $(jq 'length' <<<"$settings") inställningar från Infisical"

echo "Deployar $IMAGE"
response=$(api -X POST "$API/deploys" -d "$(jq -n --arg image "$IMAGE" '{imageUrl: $image}')")
deploy_id=$(jq -r '.id // empty' <<<"$response")
# Render svarar 202 utan body om deployen hamnar i kö. Då är den senaste deployen vår.
if [ -z "$deploy_id" ]; then
  deploy_id=$(api "$API/deploys?limit=1" | jq -r '.[0].deploy.id')
fi
echo "Deploy $deploy_id"

# Upp till 15 minuter.
for _ in $(seq 1 90); do
  status=$(api "$API/deploys/$deploy_id" | jq -r '.status')
  echo "  status: $status"
  case "$status" in
    live) break ;;
    build_failed | update_failed | canceled | deactivated | pre_deploy_failed)
      echo "::error::Render-deployen misslyckades med status $status"
      exit 1
      ;;
  esac
  sleep 10
done
if [ "$status" != live ]; then
  echo "::error::Deployen blev inte live inom 15 minuter (status $status)"
  exit 1
fi

# Free-instansen kan behöva vakna, så försök ett par minuter innan vi ger upp.
echo "Kontrollerar $APP_URL/version"
for _ in $(seq 1 18); do
  if body=$(curl -fsS --max-time 30 "$APP_URL/version") \
    && [ "$(jq -r '.commit' <<<"$body")" = "$EXPECTED_COMMIT" ] \
    && curl -fsS --max-time 30 -o /dev/null "$APP_URL/health"; then
    echo "Live: $body"
    {
      echo "### Deployad till Render"
      echo "- Version: \`$EXPECTED_VERSION\`"
      echo "- Commit: \`$EXPECTED_COMMIT\`"
      echo "- URL: $APP_URL"
    } >>"${GITHUB_STEP_SUMMARY:-/dev/null}"
    exit 0
  fi
  sleep 10
done

echo "::error::$APP_URL svarar inte med commit $EXPECTED_COMMIT och frisk /health"
exit 1
