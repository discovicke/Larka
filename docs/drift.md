# Drift: kör Lärka själv eller i molnet

Lärka är en container. Den kan köras var som helst där det går att köra en container med miljövariabler: på skolans egen server, i Azure, på Render eller hos någon annan. Vår demo på Render med Supabase är ett av exemplen ([deploy.md](deploy.md)).

## Vad Lärka behöver

| Behov                   | Konkret                                                                                                                                                                                                                         |
| ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| En container            | Imagen `ghcr.io/eduedugrade/net25-devops-grupp-4:<version>` (en tagg som `vX.Y.Z`, se [releaserna](https://github.com/EduEdugrade/net25-devops-grupp-4/releases)). Lyssnar på port 8080. Lås versionen, använd aldrig `latest`. |
| Postgres 15 eller nyare | Vilken leverantör som helst. Migrationerna körs när appen startar.                                                                                                                                                              |
| Filer                   | En mapp på disk (`Storage__Disk__Path`) eller Supabase Storage. Se [fillagring.md](fillagring.md).                                                                                                                              |
| HTTPS                   | Krävs av Microsoft-inloggningen (utom på localhost). En reverse proxy eller plattformens inbyggda TLS.                                                                                                                          |
| Inställningar           | Miljövariabler. [`deploy/.env.example`](../deploy/.env.example) listar dem: `ConnectionStrings__Default`, `Auth__Jwt__Key`, fillagring och `Auth__Entra__*`.                                                                    |
| **En instans**          | Bakgrundsjobben (notiser, GitHub) körs i appen. Två instanser kör varje jobb två gånger.                                                                                                                                        |
| Hälsokontroll           | `GET /health` svarar `OK`, `GET /version` visar version och commit.                                                                                                                                                             |

## Välj sätt

- **Molnet, rekommenderat.** En containertjänst och en hanterad Postgres. Leverantören sköter säkerhetskopior, uppdateringar och drift, så skolan behöver ingen egen driftpersonal.
- **Egen server.** När data måste ligga på skolans egen hårdvara, eller skolan redan har drift. Allt i en Docker Compose-fil, men skolan ansvarar själv för säkerhetskopior och uppdateringar.

## Imagen

Imagen ligger i ett privat register. Antingen får skolan en token som bara får läsa paket (`docker login ghcr.io`), eller så bygger skolan imagen själv från en tagg i repot:

```bash
git checkout vX.Y.Z
docker build -t larka:vX.Y.Z .
```

Med en egen image sätts `LARKA_IMAGE=larka` i `.env` (compose) eller imagens namn i molntjänsten.

## Egen server med Docker Compose

Det som behövs: en Linuxserver med Docker, ett DNS-namn som pekar på servern och portarna 80 och 443 öppna. [`deploy/compose.yaml`](../deploy/compose.yaml) startar tre containrar: appen, Postgres 17 och Caddy, som hämtar och förnyar HTTPS-certifikatet själv. Databasen och filerna ligger i varsin Docker-volym (`db` och `files`).

```bash
cd deploy
cp .env.example .env
# Fyll i LARKA_DOMAIN, LARKA_VERSION, POSTGRES_PASSWORD och Auth__Jwt__Key (openssl rand -base64 48)
docker compose up -d
docker compose logs -f app
```

Sedan inloggningen: Microsoft (se [nedan](#microsoft-inloggning)), eller en första admin med lösenord (se [Utan Microsoft](#utan-microsoft)). Lägg raderna i `.env` och starta om appen med `docker compose up -d`.

`.env` innehåller hemligheter. Den checkas aldrig in, och bara den som driftar servern ska kunna läsa den (`chmod 600 .env`).

### Säkerhetskopior

Ta en kopia av både databasen och filerna, till exempel varje natt, och spara dem utanför servern:

```bash
docker compose exec -T db pg_dump -U larka -Fc larka > larka-$(date +%F).dump
docker run --rm -v larka_files:/files -v "$PWD":/backup alpine tar czf /backup/files-$(date +%F).tar.gz -C /files .
```

Återställ databasen med `pg_restore` (`docker compose exec -T db pg_restore -U larka -d larka --clean < larka-<datum>.dump`) och filerna genom att packa upp arkivet i volymen på samma sätt.

### Uppgradera

1. Ta en säkerhetskopia.
2. Byt `LARKA_VERSION` i `.env`.
3. `docker compose pull && docker compose up -d`. Nya migrationer körs när appen startar.

## I molnet

Samma krav, med molntjänstens egna delar:

1. **Databas:** en hanterad Postgres. Connection string i .NET-format med `SSL Mode=Require`.
2. **Appen:** en containertjänst med imagen, port 8080, hälsokontroll på `/health` och **en** instans.
3. **Filer:** en beständig disk som monteras på `/data/files` med `Storage__Disk__Path=/data/files`, eller Supabase Storage.
4. **Inställningar:** nycklarna från `.env.example` i tjänstens miljövariabler. Hemligheter i tjänstens hemlighetslager, som appen ändå läser som vanliga miljövariabler.
5. **HTTPS:** ingår i tjänsten. Lägg till en egen domän om skolan vill ha en.

### Exempel: Azure

- **Databas:** Azure Database for PostgreSQL, Flexible Server. Den minsta nivån (Burstable) räcker för en skola. Tillåt åtkomst från appen i nätverksinställningarna.
- **Appen:** Azure App Service (Linux, container) eller Azure Container Apps. I App Service sätts `WEBSITES_PORT=8080` och hälsokontrollen till `/health`. I Container Apps sätts ingress-porten till 8080 och antalet repliker till 1, både minst och högst.
- **Filer:** en filresurs i Azure Files som monteras på `/data/files`.
- **Hemligheter:** i Azure Key Vault. En inställning i App Service kan peka på en hemlighet med `@Microsoft.KeyVault(SecretUri=...)`, och appen ser bara värdet.

Kör inte Postgres i samma container som appen. App Service raderar containerns disk vid omstart, och Postgres på en nätverksdisk riskerar att förstöra databasen.

### Exempel: vår demo

Render kör imagen och Supabase har databasen och filerna. Inställningarna ligger i Infisical och skickas till Render vid varje deploy. Se [deploy.md](deploy.md).

## Microsoft-inloggning

Registrera Lärka i skolans Entra ID med [`infra/entra-setup.sh`](../infra/entra-setup.sh), inloggad med Azure CLI i skolans tenant. Kontot du är inloggad med blir skolans första admin, så kolla med `az account show` att det är rätt konto innan du kör skriptet. Skriptet visar kontot och frågar innan det ändrar något.

```bash
az login
az account show
APP_URL=https://larka.skolan.se infra/entra-setup.sh
```

Lägg de fyra raderna `Auth__Entra__…` i `.env` eller i molntjänstens inställningar och starta om appen. Första gången du som körde skriptet loggar in med Microsoft kopplas skolan till tenanten och du blir admin. Alla andra blir elever. Mer i [deploy.md](deploy.md#microsoft-inloggning).

## Utan Microsoft

En skola som inte har Microsoft Entra ID loggar in med lösenord. Den första admin kommer från två inställningar:

```bash
Auth__FirstAdmin__Email=rektor@skolan.se
Auth__FirstAdmin__Password=<minst 8 tecken, till exempel openssl rand -base64 18>
```

När appen startar och skolan inte har någon admin som kan logga in skapas en admin med adressen och lösenordet (eller så blir en användare med adressen admin). Logga in med dem. Skolan heter först _Placeholder-skolan_: byt namnet under _Inställningar → Skola_. Sedan skapar admin klasser och användare och ger dem tillfälliga lösenord under _Lägg till medlem_. Har någon glömt sitt lösenord, eller skapades utan, sätter admin ett nytt med _Sätt lösenord_ i menyn `⋯` på _Användare_. Raderna kan ligga kvar, eftersom de inte gör något när skolan redan har en admin, men ta gärna bort lösenordet. En ogiltig adress eller ett för kort lösenord stoppar starten med ett tydligt fel.

## Gör inte

- Kör inte Postgres i samma container som appen.
- Kör inte mer än en instans.
- Använd inte `latest`. Lås versionen så att en uppgradering är ett beslut.
- Sätt inte `Database__SeedDemoData` för en riktig skola. Den skapar konton med kända lösenord.

`docker-compose.yml` i repots rot är bara för utveckling ([utveckling.md](utveckling.md)).
