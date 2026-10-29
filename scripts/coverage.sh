#!/usr/bin/env bash
set -euo pipefail

# Kör backend- och frontendtesterna med täckning och slår ihop dem till en rapport i coverage/report.
# Samma ReportGenerator-anrop körs i CI (jobbet coverage i .github/workflows/ci.yml).
# Backendtesterna startar Postgres med Testcontainers, så Docker måste vara igång.

cd "$(dirname "$0")/.."

rm -rf coverage client/coverage

dotnet tool restore
dotnet test RebornLMS.sln --settings coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory coverage/backend
npm --prefix client run test:coverage

# Sökvägarna i frontendens Cobertura är relativa till client/, därför -sourcedirs.
dotnet reportgenerator \
  "-reports:coverage/backend/**/coverage.cobertura.xml;client/coverage/cobertura-coverage.xml" \
  "-targetdir:coverage/report" \
  "-sourcedirs:.;client" \
  "-reporttypes:Html;MarkdownSummaryGithub;Badges;TextSummary"

sed -n 1,20p coverage/report/Summary.txt
echo
echo "Rapport: coverage/report/index.html"
