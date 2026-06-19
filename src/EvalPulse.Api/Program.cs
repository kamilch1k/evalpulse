using System.Text.Json;
using System.Text.Json.Serialization;
using EvalPulse.Core;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

var app = builder.Build();
var reports = new Dictionary<string, EvalReport>(StringComparer.OrdinalIgnoreCase);
var evaluator = new Evaluator();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "evalpulse" }));

app.MapGet("/api/sample", () => Results.Ok(SampleData.Request()));

app.MapGet("/api/reports", () =>
{
    var summaries = reports.Values
        .OrderByDescending(report => report.StartedAt)
        .Select(report => new
        {
            report.RunId,
            report.Model,
            report.Dataset,
            report.StartedAt,
            report.Summary.WeightedScore,
            report.Summary.FailureRate,
            report.Summary.AverageLatencyMs,
            report.Summary.TotalCostUsd,
            gatePassed = report.Gate.Passed,
            report.Drift.DriftScore
        });
    return Results.Ok(summaries);
});

app.MapGet("/api/reports/{runId}", (string runId) =>
    reports.TryGetValue(runId, out var report)
        ? Results.Ok(report)
        : Results.NotFound(new { error = "report not found" }));

app.MapPost("/api/evaluate", (EvaluateRequest request) =>
{
    var validation = Validate(request);
    if (validation.Count > 0)
    {
        return Results.BadRequest(new { errors = validation });
    }

    var report = evaluator.Evaluate(request.Dataset, request.Candidate, request.Baseline);
    reports[report.RunId] = report;

    return Results.Json(
        report,
        statusCode: report.Gate.Passed ? StatusCodes.Status200OK : StatusCodes.Status422UnprocessableEntity,
        options: new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        });
});

app.Run();

static List<string> Validate(EvaluateRequest request)
{
    var errors = new List<string>();
    if (string.IsNullOrWhiteSpace(request.Dataset.Name))
    {
        errors.Add("dataset.name is required");
    }
    if (request.Dataset.Cases.Count == 0)
    {
        errors.Add("dataset.cases must not be empty");
    }
    if (string.IsNullOrWhiteSpace(request.Candidate.RunId))
    {
        errors.Add("candidate.runId is required");
    }
    if (request.Candidate.Answers.Count == 0)
    {
        errors.Add("candidate.answers must not be empty");
    }
    return errors;
}

public sealed record EvaluateRequest(
    EvalDataset Dataset,
    ModelRun Candidate,
    ModelRun? Baseline);

internal static class SampleData
{
    public static EvaluateRequest Request()
    {
        var dataset = new EvalDataset(
            "support-bot-golden-set",
            new[]
            {
                new EvalCase(
                    "refund-policy",
                    "A customer asks how to get a refund for a duplicate charge.",
                    "Ask for the order id, verify the payment status, then create a refund ticket.",
                    new[] { "order id", "payment status", "refund ticket" },
                    new[] { "guarantee", "instant cash" },
                    1.2),
                new EvalCase(
                    "privacy-delete",
                    "A user wants all personal data deleted.",
                    "Confirm identity, start the data deletion workflow, and mention the retention window.",
                    new[] { "confirm identity", "data deletion", "retention window" },
                    new[] { "ignore policy", "sell data" },
                    1.0),
                new EvalCase(
                    "incident-summary",
                    "Summarize a failed checkout incident for support.",
                    "Explain checkout failure impact, affected users, mitigation, and next update time.",
                    new[] { "checkout failure", "affected users", "mitigation", "next update" },
                    new[] { "root cause is certain" },
                    1.0)
            },
            new RegressionGate(
                MinWeightedScore: 0.82,
                MaxFailureRate: 0.2,
                MaxAverageLatencyMs: 1_500,
                MaxTotalCostUsd: 0.03m,
                MaxDriftScore: 0.08));

        return new EvaluateRequest(dataset, Candidate(), Baseline());
    }

    private static ModelRun Candidate() => new(
        "run-candidate-001",
        "fixture-model-v2",
        DateTimeOffset.Parse("2026-06-19T20:00:00Z"),
        new[]
        {
            Answer("refund-policy", "Ask for the order id, verify payment status, then open a refund ticket for review.", 720, 0.004m),
            Answer("privacy-delete", "Confirm identity first, start the data deletion workflow, and explain the retention window.", 840, 0.005m),
            Answer("incident-summary", "Checkout failure affected users during payment. Mitigation is deployed and the next update is at 14:00 UTC.", 910, 0.006m)
        });

    private static ModelRun Baseline() => new(
        "run-baseline-001",
        "fixture-model-v1",
        DateTimeOffset.Parse("2026-06-18T20:00:00Z"),
        new[]
        {
            Answer("refund-policy", "Ask for the order id, verify payment status, then create a refund ticket.", 760, 0.004m),
            Answer("privacy-delete", "Confirm identity, start data deletion, and mention the retention window.", 860, 0.005m),
            Answer("incident-summary", "Checkout failure affected users. Mitigation is active and the next update time is 14:00 UTC.", 930, 0.006m)
        });

    private static ModelAnswer Answer(string caseId, string output, int latencyMs, decimal cost) => new(
        caseId,
        output,
        latencyMs,
        PromptTokens: 200,
        CompletionTokens: 120,
        CostUsd: cost);
}
