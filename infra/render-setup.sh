#!/usr/bin/env bash
# Skapar produktionstjänsten på Render, eller uppdaterar den om den redan finns.
#
# Tjänsten kör Docker-imagen som cd.yml bygger och pushar till GitHub Container Registry.
# Render behöver alltså ingen åtkomst till repot, bara en token som får läsa imagen.
#
# Kör lokalt när tjänsten skapas eller dess inställningar ändras:
#   RENDER_API_KEY     Render → Account Settings → API Keys
#   GHCR_USERNAME      GitHub-användaren som äger GHCR_TOKEN
#   GHCR_TOKEN         GitHub classic token med bara read:packages
#
# Appens inställningar sätts inte här. De ligger i Infisical (prod), och deployen skickar dem till Render.
set -euo pipefail

: "${RENDER_API_KEY:?saknas}" "${GHCR_USERNAME:?saknas}" "${GHCR_TOKEN:?saknas}"

NAME=net25-devops-grupp-4
IMAGE=ghcr.io/eduedugrade/net25-devops-grupp-4:latest
API=https://api.render.com/v1

api() {
  curl -fsS -H "Authorization: Bearer $RENDER_API_KEY" -H "Content-Type: application/json" "$@"
}

owner=$(api "$API/owners?limit=1" | jq -r '.[0].owner.id')

# Inloggning mot GHCR så att Render kan hämta den privata imagen.
cred_body=$(jq -n --arg name "ghcr-$NAME" --arg owner "$owner" --arg user "$GHCR_USERNAME" --arg token "$GHCR_TOKEN" \
  '{name: $name, registry: "GITHUB", username: $user, authToken: $token, ownerId: $owner}')
cred=$(api "$API/registrycredentials?name=ghcr-$NAME&ownerId=$owner" | jq -r '.[0].id // empty')
if [ -z "$cred" ]; then
  cred=$(api -X POST "$API/registrycredentials" -d "$cred_body" | jq -r '.id')
  echo "Skapade registry credential $cred"
else
  api -X PATCH "$API/registrycredentials/$cred" -d "$(jq 'del(.ownerId)' <<<"$cred_body")" >/dev/null
  echo "Uppdaterade registry credential $cred"
fi

# PORT: aspnet-imagen lyssnar på 8080. APP_VERSION och GIT_COMMIT bakas in i imagen vid bygget.
env_vars='[{"key": "ASPNETCORE_ENVIRONMENT", "value": "Production"}, {"key": "PORT", "value": "8080"}]'

# Frankfurt = samma region som Supabase. Deployer triggas av GitHub Actions, inte av Render.
service=$(api "$API/services?name=$NAME&ownerId=$owner" | jq -r '.[0].service.id // empty')
if [ -z "$service" ]; then
  body=$(jq -n --arg name "$NAME" --arg owner "$owner" --arg image "$IMAGE" --arg cred "$cred" --argjson env "$env_vars" '{
    type: "web_service",
    name: $name,
    ownerId: $owner,
    autoDeployTrigger: "off",
    image: {ownerId: $owner, imagePath: $image, registryCredentialId: $cred},
    envVars: $env,
    serviceDetails: {runtime: "image", plan: "free", region: "frankfurt", healthCheckPath: "/health"}
  }')
  service=$(api -X POST "$API/services" -d "$body" | jq -r '.service.id')
  echo "Skapade tjänsten $service"
else
  body=$(jq -n --arg owner "$owner" --arg image "$IMAGE" --arg cred "$cred" '{
    autoDeployTrigger: "off",
    image: {ownerId: $owner, imagePath: $image, registryCredentialId: $cred},
    serviceDetails: {plan: "free", healthCheckPath: "/health"}
  }')
  api -X PATCH "$API/services/$service" -d "$body" >/dev/null
  # En variabel i taget, så att appens inställningar från deployen finns kvar.
  count=$(jq 'length' <<<"$env_vars")
  for i in $(seq 0 $((count - 1))); do
    key=$(jq -r ".[$i].key" <<<"$env_vars")
    api -X PUT "$API/services/$service/env-vars/$key" \
      -d "$(jq ".[$i] | {value}" <<<"$env_vars")" >/dev/null
  done
  echo "Uppdaterade tjänsten $service"
fi

url=$(api "$API/services/$service" | jq -r '.serviceDetails.url')
echo
echo "RENDER_SERVICE_ID=$service"
echo "URL=$url"
echo "Lägg RENDER_SERVICE_ID i Infisical (prod, /deploy) och kör en deploy."
