# Produktionsimage: backend + den byggda Vue-klienten i wwwroot. Byggs och pushas av cd.yml.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# dotnet publish kör npm ci + npm run build för klienten (PublishClient i backend.csproj).
COPY --from=node:24-bookworm-slim /usr/local /usr/local
WORKDIR /src

# Restore i ett eget lager så att det cachas tills paketen ändras.
COPY Directory.Build.props ./
COPY backend.Analyzers/backend.Analyzers.csproj backend.Analyzers/
COPY backend/backend.csproj backend/
RUN dotnet restore backend/backend.csproj

COPY . .
RUN dotnet publish backend -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# Sätts av cd.yml och visas på /version.
ARG APP_VERSION=dev
ARG GIT_COMMIT=local
ENV APP_VERSION=$APP_VERSION GIT_COMMIT=$GIT_COMMIT

# Mappen för filer när de sparas på disk (Storage__Disk__Path=/data/files, se docs/drift.md). Den skapas här och ägs
# av appens användare, så att en Docker-volym som monteras där går att skriva i.
RUN mkdir -p /data/files && chown "$APP_UID" /data/files

# aspnet-imagen lyssnar på 8080 och har en användare utan root-rättigheter.
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "backend.dll"]
