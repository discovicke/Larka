# Kodstil i C#

Hur reglerna för C#-koden sätts upp, kontrolleras och rättas.

## Var allt finns

| Fil                        | Vad                                                                                  |
| -------------------------- | ------------------------------------------------------------------------------------ |
| `.editorconfig`            | Reglerna och hur strikta de är. `dotnet format` har ingen egen konfigfil             |
| `Directory.Build.props`    | Lägger till analyzers i alla projekt: Roslynator och våra egna i `backend.Analyzers` |
| `backend.Analyzers/`       | Våra egna regler (`RLMS*`), för sådant som varken .NET eller Roslynator har          |
| `backend.Analyzers.Tests/` | Tester för våra regler                                                               |

En regel kommer från en analyzer (inbyggd i .NET, ett paket eller vår egen) och slås på i `.editorconfig`:

```ini
dotnet_diagnostic.RLMS001.severity = warning   # warning = bygget och CI failar, suggestion = bara i editorn
```

## Reglerna utöver .NET:s egna

```csharp
var content = await service.GetAsync(id, ct);   // inte "var c", och inte "is { } c"
return content is null
    ? Results.NotFound()
    : Results.Ok(content);
```

| Regel     | Vad                                                         | Från                | Rättas av `npm run format`  |
| --------- | ----------------------------------------------------------- | ------------------- | --------------------------- |
| `RCS0056` | max 120 tecken per rad                                      | Roslynator          | ja                          |
| `RLMS001` | `?` och `:` i en ternary på egna rader                      | `backend.Analyzers` | ja                          |
| `RLMS002` | inga namn på en bokstav (`_` och `i` i en `for`-loop är ok) | `backend.Analyzers` | nej, döp om själv           |
| `RLMS003` | inga property patterns (`is { } x`, `is { Length: > 0 }`)   | `backend.Analyzers` | nej, skriv en null-kontroll |

## Var reglerna slår till

1. **Editorn** (VS Code med C#-tillägget, Rider m.fl.) visar dem medan du skriver, eftersom de kommer från bygget. Quick fix på en ternary (Ctrl+. i VS Code) wrappar den.
2. **`dotnet build`** failar på dem, eftersom varningar räknas som fel (`TreatWarningsAsErrors`).
3. **`npm run format`** rättar det som går. `npm run format:check` kontrollerar bara.
4. **CI** kör samma kontroll som `format:check`, och sedan `dotnet build -warnaserror`.

`dotnet format` hoppar tyst över `RLMS*` om `backend.Analyzers` inte är byggd. Därför bygger både npm-skripten och CI den först.

## Lägga till en egen regel

1. Ny klass i `backend.Analyzers` som ärver `DiagnosticAnalyzer`, med nästa lediga id (`RLMS004`). Se `NameLengthAnalyzer.cs`.
2. Går den att rätta automatiskt: en `CodeFixProvider` med samma id, se `WrapTernaryCodeFix.cs`.
3. Tester i `backend.Analyzers.Tests` med `AnalyzerRunner`.
4. `dotnet_diagnostic.RLMS004.severity = warning` i `.editorconfig`, och en rad i tabellen ovan.
