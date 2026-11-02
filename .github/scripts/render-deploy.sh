#!/usr/bin/env bash
# Deployar en image till Render, väntar tills den är live och kontrollerar att rätt version svarar.
# Körs av deploy-jobbet i cd.yml. Misslyckas en deploy behåller Render den gamla versionen live.
set -euo pipefail

: "${RENDER_API_KEY:?saknas}" "${RENDER_SERVICE_ID:?saknas, kör infra/render-setup.sh}" "${APP_URL:?saknas}"
: "${SUPABASE_URL:?saknas}" "${SUPABASE_SERVICE_ROLE_KEY:?saknas}"
: "${AUTH_JWT_KEY:?saknas, lägg den som secret i GitHub-miljön production}"
: "${IMAGE:?saknas}""${EXPECTED_VERSION:?saknas}" "${EXPECTED_COMMIT:?saknas}"

API="https://api.render.com/v1/services/$RENDER_SERVICE_ID"

api() {
  curl -fsS -H "Authorization: Bearer $RENDER_API_KEY" -H "Content-Type: application/json" "$@"
}

# Filagringen (Supabase Storage) och JWT-nyckeln ställs in här så att hemligheterna bara behöver finnas i GitHub-miljön
# production. Ett anrop per variabel, så att övriga miljövariabler på tjänsten lämnas orörda.
for pair in "Storage__Supabase__Url=$SUPABASE_URL" "Storage__Supabase__ServiceRoleKey=$SUPABASE_SERVICE_ROLE_KEY" \
  "Auth__Jwt__Key=$AUTH_JWT_KEY"; do
  api -X PUT "$API/env-vars/${pair%%=*}" -d "$(jq -n --arg value "${pair#*=}" '{value: $value}')" >/dev/null
done

# Demokontona (admin/admin m.fl.) och demodatan finns bara när SEED_DEMO_DATA är true, som i Edugrades demo. En ny
# skola sätter den inte och får en tom skola utan konton med kända lösenord.
api -X PUT "$API/env-vars/Database__SeedDemoData" \
  -d "$(jq -n --arg value "${SEED_DEMO_DATA:-false}" '{value: $value}')" >/dev/null

# Microsoft-inloggningen är valfri. infra/entra-setup.sh lägger variablerna i GitHub-miljön production.
if [ -n "${ENTRA_CLIENT_ID:-}" ] && [ -n "${ENTRA_TENANT_ID:-}" ]; then
  for pair in "Auth__Entra__ClientId=$ENTRA_CLIENT_ID" "Auth__Entra__TenantId=$ENTRA_TENANT_ID" \
    "Auth__Entra__AdminObjectId=${ENTRA_ADMIN_OBJECT_ID:-}" "Auth__Entra__SchoolName=${ENTRA_SCHOOL_NAME:-}"; do
    if [ -n "${pair#*=}" ]; then
      api -X PUT "$API/env-vars/${pair%%=*}" -d "$(jq -n --arg value "${pair#*=}" '{value: $value}')" >/dev/null
    fi
  done
fi

# GitHub är valfritt. Tomma värden hoppas över, så en deploy utan GitHub fungerar som förut.
for pair in \
  "GitHub__AppId=${GH_APP_ID:-}" \
  "GitHub__AppSlug=${GH_APP_SLUG:-}" \
  "GitHub__ClientId=${GH_CLIENT_ID:-}" \
  "GitHub__Org=${GH_ORG:-}" \
  "GitHub__InstallationId=${GH_INSTALLATION_ID:-}" \
  "GitHub__PrivateKey=${GH_APP_PRIVATE_KEY:-}" \
  "GitHub__ClientSecret=${GH_CLIENT_SECRET:-}" \
  "GitHub__WebhookSecret=${GH_WEBHOOK_SECRET:-}"; do
  key="${pair%%=*}"
  value="${pair#*=}"
  if [ -n "$value" ]; then
    api -X PUT "$API/env-vars/$key" -d "$(jq -n --arg value "$value" '{value: $value}')" >/dev/null
  fi
done

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
