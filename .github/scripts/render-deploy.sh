#!/usr/bin/env bash
# Deployar en image till Render, väntar tills den är live och kontrollerar att rätt version svarar.
# Körs av deploy-jobbet i cd.yml. Misslyckas en deploy behåller Render den gamla versionen live.
#
# Appens inställningar kommer från GitHub-miljön production: varje variabel (APP_VARS) och hemlighet (APP_SECRETS)
# med __ i namnet är en nyckel i appen (AUTH__JWT__KEY blir Auth:Jwt:Key, .NET bryr sig inte om versaler). Alla
# skickas till Render, så en ny inställning behöver bara läggas i miljön. Övriga namn (RENDER_API_KEY, APP_URL) är
# deployens egna och skickas inte. GitHub tillåter inga namn som börjar på GITHUB_, så ett inledande LARKA_ tas bort:
# LARKA_GITHUB__APPID blir GITHUB__APPID (GitHub:AppId).
set -euo pipefail

: "${RENDER_API_KEY:?saknas}" "${RENDER_SERVICE_ID:?saknas, kör infra/render-setup.sh}" "${APP_URL:?saknas}"
: "${IMAGE:?saknas}" "${EXPECTED_VERSION:?saknas}" "${EXPECTED_COMMIT:?saknas}"

API="https://api.render.com/v1/services/$RENDER_SERVICE_ID"

api() {
  curl -fsS -H "Authorization: Bearer $RENDER_API_KEY" -H "Content-Type: application/json" "$@"
}

empty_json='{}'
settings=$(jq -n --argjson vars "${APP_VARS:-$empty_json}" --argjson secrets "${APP_SECRETS:-$empty_json}" \
  '$vars + $secrets | with_entries(select(.key | contains("__")) | .key |= ltrimstr("LARKA_"))')

# Utan dessa startar appen inte, så stoppa innan något ändras.
for required in CONNECTIONSTRINGS__DEFAULT AUTH__JWT__KEY; do
  if [ "$(jq -r --arg key "$required" 'with_entries(.key |= ascii_upcase) | .[$key] // empty' <<<"$settings")" = "" ]; then
    echo "::error::$required saknas i GitHub-miljön production"
    exit 1
  fi
done

# Samma nyckel med andra versaler (Auth__Jwt__Key från förr, AUTH__JWT__KEY från GitHub) skulle ge två variabler som
# .NET läser i okänd ordning. Den gamla stavningen tas bort, och resten av tjänstens variabler lämnas orörda.
existing=$(api "$API/env-vars?limit=100" | jq -r '.[].envVar.key')
while IFS= read -r key; do
  for old in $existing; do
    if [ "$old" != "$key" ] && [ "${old^^}" = "${key^^}" ]; then
      api -X DELETE "$API/env-vars/$old" >/dev/null
      echo "Tog bort $old (ersätts av $key)"
    fi
  done
  api -X PUT "$API/env-vars/$key" -d "$(jq --arg key "$key" '{value: .[$key]}' <<<"$settings")" >/dev/null
done < <(jq -r 'keys[]' <<<"$settings")
echo "Skickade $(jq 'length' <<<"$settings") inställningar till Render"

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
