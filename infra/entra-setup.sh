#!/usr/bin/env bash
# Registrerar Lärka i skolans Microsoft Entra ID, eller uppdaterar registreringen om den redan finns.
#
# Körs en gång av skolans Entra-admin:
#   az login            (som admin i skolans tenant)
#   gh auth login       (med rätt att ändra repots miljö production)
#   infra/entra-setup.sh
#
# Skriptet skapar en app som bara skolans egna konton kan logga in med (single-tenant), med plattformen
# Single-page application och bara inloggningsbehörigheter (openid, profile, email, offline_access). Ingen
# hemlighet behövs: klienten loggar in med MSAL och backend byter Microsofts ID-token mot Lärkas eget token.
# Sist sparas client id, tenant id, ditt object id och organisationens namn som variabler i GitHub-miljön
# production, och nästa deploy (.github/scripts/render-deploy.sh) sätter dem som Auth__Entra__* på Render.
#
# Den som kör skriptet blir skolans första admin: första gången du loggar in med Microsoft kopplas skolan till
# tenanten, får organisationens namn, och du blir admin. Ingen behöver logga in med lösenord.
#
# Valfritt:
#   APP_URL          appens adress i prod. Standard: variabeln APP_URL i GitHub-miljön production
#   ENTRA_APP_NAME   appens namn i Entra. Standard: Lärka
#   SCHOOL_NAME      skolans namn i Lärka. Standard: organisationens namn i Entra ID
#   SKIP_GITHUB=1    skriv bara ut värdena, ändra inget i GitHub
set -euo pipefail

command -v az >/dev/null || { echo "Azure CLI (az) saknas." >&2; exit 1; }
command -v jq >/dev/null || { echo "jq saknas." >&2; exit 1; }
az account show >/dev/null 2>&1 || { echo "Logga in först: az login" >&2; exit 1; }

NAME=${ENTRA_APP_NAME:-Lärka}
# Taggen hittar vår app även om någon döper om den i Entra.
TAG=larka-sign-in
LOCAL_URL=http://localhost:5173
# Microsoft skickar tillbaka till inloggningssidan, där klienten tar emot svaret.
REDIRECT_PATH=/login

if [ -z "${APP_URL:-}" ] && [ -z "${SKIP_GITHUB:-}" ]; then
  APP_URL=$(gh variable get APP_URL --env production)
fi
: "${APP_URL:?saknas, sätt APP_URL=https://... eller variabeln APP_URL i GitHub-miljön production}"
APP_URL=${APP_URL%/}

tenant=$(az account show --query tenantId -o tsv)
echo "Tenant: $tenant ($(az account show --query user.name -o tsv))"
# Du som kör skriptet blir första admin. Object id (oid) är samma som Microsoft skickar i ID-tokenet.
admin_object_id=$(az ad signed-in-user show --query id -o tsv)
school_name=${SCHOOL_NAME:-$(az rest --method GET --url https://graph.microsoft.com/v1.0/organization \
  --query 'value[0].displayName' -o tsv 2>/dev/null || true)}
echo "Första admin: du ($admin_object_id). Skolans namn: ${school_name:-(inget, sätt SCHOOL_NAME)}"

app_id=$(az ad app list --filter "tags/any(t:t eq '$TAG')" --query '[0].appId' -o tsv)
if [ -z "$app_id" ]; then
  app_id=$(az ad app create --display-name "$NAME" --sign-in-audience AzureADMyOrg --query appId -o tsv)
  echo "Skapade appen $NAME ($app_id)"
else
  echo "Uppdaterar appen $app_id"
fi
object_id=$(az ad app show --id "$app_id" --query id -o tsv)

# Microsoft Graph med de delegerade behörigheterna för inloggning. Id:na är desamma i alla tenanter.
graph=00000003-0000-0000-c000-000000000000
body=$(jq -n --arg name "$NAME" --arg tag "$TAG" --arg app "$APP_URL" --arg local "$LOCAL_URL" --arg path "$REDIRECT_PATH" --arg graph "$graph" '{
  displayName: $name,
  signInAudience: "AzureADMyOrg",
  tags: [$tag],
  spa: {redirectUris: [$app + $path, $local + $path]},
  web: {redirectUris: [], implicitGrantSettings: {enableIdTokenIssuance: false, enableAccessTokenIssuance: false}},
  optionalClaims: {idToken: [{name: "email", essential: false}]},
  requiredResourceAccess: [{
    resourceAppId: $graph,
    resourceAccess: [
      {id: "37f7f235-527c-4136-accd-4a02d197296e", type: "Scope"},
      {id: "14dad69e-099b-42c9-810b-d002981feec1", type: "Scope"},
      {id: "64a6cdd6-aab1-4aaf-94b8-3cc8405e90d0", type: "Scope"},
      {id: "7427e0e9-2fba-42fe-b0c0-848c9e6a8182", type: "Scope"}
    ]
  }]
}')
az rest --method PATCH --url "https://graph.microsoft.com/v1.0/applications/$object_id" \
  --headers Content-Type=application/json --body "$body" >/dev/null
echo "Redirect URI:er: $APP_URL$REDIRECT_PATH, $LOCAL_URL$REDIRECT_PATH"

# Tjänstens huvudnamn (Enterprise application) behövs för att någon ska kunna logga in och för samtycket.
if ! az ad sp show --id "$app_id" >/dev/null 2>&1; then
  az ad sp create --id "$app_id" >/dev/null
  echo "Skapade Enterprise application"
fi

# Samtycke för hela skolan, så att ingen får en fråga vid första inloggningen. Kräver en admin. Utan det
# godkänner varje användare själv, vilket skolans standardregler brukar tillåta för inloggningsbehörigheter.
# Görs direkt mot Microsoft Graph: az ad app permission admin-consent misslyckas när Enterprise application finns.
scopes="openid profile email offline_access"
sp_id=$(az ad sp show --id "$app_id" --query id -o tsv)
graph_sp_id=$(az ad sp show --id "$graph" --query id -o tsv)
grants="https://graph.microsoft.com/v1.0/oauth2PermissionGrants"
filter="clientId eq '$sp_id' and consentType eq 'AllPrincipals' and resourceId eq '$graph_sp_id'"
grant_id=$(az rest --method GET --url "$grants?\$filter=$filter" --query 'value[0].id' -o tsv 2>/dev/null || true)
if [ -n "$grant_id" ]; then
  consent=(--method PATCH --url "$grants/$grant_id" --body "$(jq -n --arg scope "$scopes" '{scope: $scope}')")
else
  consent=(--method POST --url "$grants" --body "$(jq -n --arg client "$sp_id" --arg resource "$graph_sp_id" \
    --arg scope "$scopes" '{clientId: $client, consentType: "AllPrincipals", resourceId: $resource, scope: $scope}')")
fi
if az rest "${consent[@]}" --headers Content-Type=application/json >/dev/null 2>&1; then
  echo "Samtycke givet för hela organisationen"
else
  echo "Kunde inte ge samtycke för hela organisationen (kräver admin). Användarna godkänner själva vid första inloggningen."
fi

echo
echo "ENTRA_CLIENT_ID=$app_id"
echo "ENTRA_TENANT_ID=$tenant"
echo "ENTRA_ADMIN_OBJECT_ID=$admin_object_id"
echo "ENTRA_SCHOOL_NAME=$school_name"

if [ -n "${SKIP_GITHUB:-}" ]; then
  echo "Lägg värdena som variabler i GitHub-miljön production."
  exit 0
fi

gh variable set ENTRA_CLIENT_ID --env production --body "$app_id"
gh variable set ENTRA_TENANT_ID --env production --body "$tenant"
gh variable set ENTRA_ADMIN_OBJECT_ID --env production --body "$admin_object_id"
if [ -n "$school_name" ]; then
  gh variable set ENTRA_SCHOOL_NAME --env production --body "$school_name"
fi
echo "Sparade i GitHub-miljön production. Nästa deploy sätter dem på Render."
echo "Logga sedan in på $APP_URL med Microsoft. Du blir admin och skolan kopplas till tenanten."
