using EvalPulse.Core;

var tests = new (string Name, Action Run)[]
{
    ("Token F1 rewards overlapping answers", TokenF1RewardsOverlap),
    ("Evaluator passes strong run", EvaluatorPassesStrongRun),
    ("Evaluator catches regressions and drift", EvaluatorCatchesRegression),
    ("Gate catches cost and latency", GateCatchesCostAndLatency)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
    }
}

return failed == 0 ? 0 : 1;

static void TokenF1RewardsOverlap()
{
    var f1 = TextMetrics.TokenF1(
        "refund requests require order id and payment status",
        "the refund workflow checks payment status and the order id");

    Assert.True(f1 > 0.55, $"expected useful overlap, got {f1}");
}

static void EvaluatorPassesStrongRun()
{
    var dataset = Fixtures.Dataset();
    var report = new Evaluator().Evaluate(dataset, Fixtures.StrongRun());

    Assert.True(report.Gate.Passed, string.Join("; ", report.Gate.Reasons));
    Assert.Equal(3, report.Summary.PassedCases);
    Assert.True(report.Summary.WeightedScore >= 0.85, $"score too low: {report.Summary.WeightedScore}");
}

static void EvaluatorCatchesRegression()
{
    var dataset = Fixtures.Dataset();
    var report = new Evaluator().Evaluate(dataset, Fixtures.WeakRun(), Fixtures.StrongRun());

    Assert.False(report.Gate.Passed, "weak run should fail gate");
    Assert.True(report.Drift.DriftScore > 0.05, $"expected drift, got {report.Drift.DriftScore}");
    Assert.True(report.Drift.Warnings.Count > 0, "expected drift warnings");
}

static void GateCatchesCostAndLatency()
{
    var dataset = Fixtures.Dataset() with
    {
        Gate = new RegressionGate(
            MinWeightedScore: 0.2,
            MaxFailureRate: 1,
            MaxAverageLatencyMs: 100,
            MaxTotalCostUsd: 0.001m,
            MaxDriftScore: 1)
    };
    var report = new Evaluator().Evaluate(dataset, Fixtures.ExpensiveRun());

    Assert.False(report.Gate.Passed, "cost and latency should fail gate");
    Assert.True(
        report.Gate.Reasons.Any(reason => reason.Contains("latency", StringComparison.OrdinalIgnoreCase)),
        "expected latency gate reason");
    Assert.True(
        report.Gate.Reasons.Any(reason => reason.Contains("cost", StringComparison.OrdinalIgnoreCase)),
        "expected cost gate reason");
}

static class Fixtures
{
    public static EvalDataset Dataset() => new(
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

    public static ModelRun StrongRun() => new(
        "run-strong",
        "fixture-model-v2",
        DateTimeOffset.Parse("2026-06-19T20:00:00Z"),
        new[]
        {
            Answer("refund-policy", "Ask for the order id, verify payment status, then open a refund ticket for review.", 720, 0.004m),
            Answer("privacy-delete", "Confirm identity first, start the data deletion workflow, and explain the retention window.", 840, 0.005m),
            Answer("incident-summary", "Checkout failure affected users during payment. Mitigation is deployed and the next update is at 14:00 UTC.", 910, 0.006m)
        });

    public static ModelRun WeakRun() => new(
        "run-weak",
        "fixture-model-v3",
        DateTimeOffset.Parse("2026-06-19T21:00:00Z"),
        new[]
        {
            Answer("refund-policy", "Tell them refunds are guaranteed and instant cash is available.", 700, 0.004m),
            Answer("privacy-delete", "Open a ticket.", 820, 0.005m),
            Answer("incident-summary", "The root cause is certain and everything is fixed.", 880, 0.006m)
        });

    public static ModelRun ExpensiveRun() => StrongRun() with
    {
        RunId = "run-expensive",
        Answers = StrongRun().Answers
            .Select(answer => answer with { LatencyMs = 2_500, CostUsd = 0.05m })
            .ToArray()
    };

    private static ModelAnswer Answer(string caseId, string output, int latencyMs, decimal cost) => new(
        caseId,
        output,
        latencyMs,
        PromptTokens: 200,
        CompletionTokens: 120,
        CostUsd: cost);
}

static class Assert
{
    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void False(bool condition, string message) => True(!condition, message);

    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"expected {expected}, got {actual}");
        }
    }
}
