# Testing Guide

Build and run all tests:

```powershell
dotnet build .\EvalPulse.slnx --configuration Release
dotnet run --project .\tests\EvalPulse.Tests --configuration Release --no-build
```

Run the API:

```powershell
dotnet run --project .\src\EvalPulse.Api --urls http://127.0.0.1:18120
```

Strong fixture:

```powershell
Invoke-RestMethod -Method Post -Uri http://127.0.0.1:18120/api/evaluate -ContentType application/json -InFile samples/strong-run.json
```

Regression fixture:

```powershell
try {
  Invoke-RestMethod -Method Post -Uri http://127.0.0.1:18120/api/evaluate -ContentType application/json -InFile samples/regression-run.json
} catch {
  $_.Exception.Response.StatusCode
}
```

The strong fixture should pass. The regression fixture should return `422` because the candidate introduces forbidden terms, misses required terms, and drifts from the baseline.
