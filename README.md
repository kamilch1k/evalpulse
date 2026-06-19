# EvalPulse

EvalPulse is a .NET backend for fixture-driven AI evaluation and observability.

It evaluates model responses against a golden dataset, calculates answer quality metrics, checks cost and latency budgets, compares a candidate run against a baseline, and returns a gate result that can block a risky model or prompt change.

The project uses synthetic fixtures only. It does not call an AI provider and does not require API keys.

## Why This Project Exists

Teams are adopting AI features faster than they can measure them. A useful backend portfolio project in this space should show more than "call an LLM API." EvalPulse focuses on the production problem around AI systems: measuring whether a prompt/model change is better, worse, slower, more expensive, or drifting away from expected behavior.

## What It Demonstrates

- ASP.NET Core Minimal API
- deterministic eval scoring without external services
- lexical F1 and required-term coverage metrics
- forbidden-term safety checks
- latency and cost budgets
- baseline-vs-candidate drift reports
- regression gates that return `422` for failed runs
- dependency-free console tests, Dockerfile, CI, and sample payloads

## Quick Start

```powershell
dotnet build .\EvalPulse.slnx --configuration Release
dotnet run --project .\tests\EvalPulse.Tests --configuration Release --no-build
dotnet run --project .\src\EvalPulse.Api --urls http://127.0.0.1:18120
```

In another terminal:

```powershell
Invoke-RestMethod -Uri http://127.0.0.1:18120/health
Invoke-RestMethod -Uri http://127.0.0.1:18120/api/sample
Invoke-RestMethod -Method Post -Uri http://127.0.0.1:18120/api/evaluate -ContentType application/json -InFile samples/strong-run.json
```

Try a regression:

```powershell
Invoke-RestMethod -Method Post -Uri http://127.0.0.1:18120/api/evaluate -ContentType application/json -InFile samples/regression-run.json
```

The regression fixture should return `422` with failed gate reasons and drift warnings.

## API

```text
GET  /health
GET  /api/sample
GET  /api/reports
GET  /api/reports/{runId}
POST /api/evaluate
```

`POST /api/evaluate` accepts:

```json
{
  "dataset": {
    "name": "support-bot-golden-set",
    "cases": [],
    "gate": {}
  },
  "candidate": {
    "runId": "run-candidate-001",
    "model": "fixture-model-v2",
    "answers": []
  },
  "baseline": null
}
```

## Scoring Model

Each case score combines lexical F1 against the expected answer and coverage of required terms, with penalties for forbidden terms. The run summary then applies case weights and checks:

- minimum weighted score
- maximum failure rate
- maximum average latency
- maximum total cost
- maximum drift from baseline

This is not meant to replace human evals. It is a production-style backend gate that catches obvious regressions before a prompt or model change ships.

## Tech

.NET 10, ASP.NET Core Minimal APIs, deterministic scoring, no external packages, Docker, GitHub Actions.
