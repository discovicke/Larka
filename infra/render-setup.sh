#!/usr/bin/env bash
# Skapar produktionstjänsten på Render, eller uppdaterar den om den redan finns.
#
# Tjänsten kör Docker-imagen som cd.yml bygger och pushar till GitHub Container Registry.
# Render behöver alltså ingen åtkomst till repot, bara en token som får läsa imagen.
#
# Kör lokalt när inställningarna ändras. Hemligheter skickas som miljövariabler, aldrig i repot:
#   RENDER_API_KEY     Render → Account Settings → API Keys
#   GHCR_USERNAME      GitHub-användaren som äger GHCR_TOKEN
#   GHCR_TOKEN         GitHub classic token med bara read:packages
#   CONNECTION_STRING  Supabase session pooler (.NET-format)
#   SUPABASE_URL                 https://<projekt>.supabase.co
#   SUPABASE_SERVICE_ROLE_KEY    service role-nyckeln för Storage, går förbi row level security
set -euo pipefail

: "${RENDER_API_KEY:?saknas}" "${GHCR_USERNAME:?saknas}" "${GHCR_TOKEN:?saknas}" "${CONNECTION_STRING:?saknas}"
: "${SUPABASE_URL:?saknas}" "${SUPABASE_SERVICE_ROLE_KEY:?saknas}"

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
env_vars=$(jq -n --arg cs "$CONNECTION_STRING" --arg sbUrl "$SUPABASE_URL" --arg sbKey "$SUPABASE_SERVICE_ROLE_KEY" '[
  {key: "ASPNETCORE_ENVIRONMENT", value: "Production"},
  {key: "Database__Provider", value: "Postgres"},
  {key: "PORT", value: "8080"},
  {key: "ConnectionStrings__Default", value: $cs},
  {key: "Storage__Supabase__Url", value: $sbUrl},
  {key: "Storage__Supabase__ServiceRoleKey", value: $sbKey}
]')

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
  api -X PUT "$API/services/$service/env-vars" -d "$env_vars" >/dev/null
  echo "Uppdaterade tjänsten $service"
fi

url=$(api "$API/services/$service" | jq -r '.serviceDetails.url')
echo
echo "RENDER_SERVICE_ID=$service"
echo "URL=$url"
echo "Lägg RENDER_SERVICE_ID som secret och URL som variabeln APP_URL i GitHub-miljön production."
