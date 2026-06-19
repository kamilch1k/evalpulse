namespace EvalPulse.Core;

public sealed record EvalDataset(
    string Name,
    IReadOnlyList<EvalCase> Cases,
    RegressionGate Gate);

public sealed record EvalCase(
    string Id,
    string Prompt,
    string ExpectedAnswer,
    IReadOnlyList<string> RequiredTerms,
    IReadOnlyList<string> ForbiddenTerms,
    double Weight = 1.0);

public sealed record ModelRun(
    string RunId,
    string Model,
    DateTimeOffset StartedAt,
    IReadOnlyList<ModelAnswer> Answers);

public sealed record ModelAnswer(
    string CaseId,
    string Output,
    int LatencyMs,
    int PromptTokens,
    int CompletionTokens,
    decimal CostUsd);

public sealed record RegressionGate(
    double MinWeightedScore,
    double MaxFailureRate,
    double MaxAverageLatencyMs,
    decimal MaxTotalCostUsd,
    double MaxDriftScore);

public sealed record EvalReport(
    string RunId,
    string Model,
    string Dataset,
    DateTimeOffset StartedAt,
    SummaryMetrics Summary,
    IReadOnlyList<CaseScore> Cases,
    GateResult Gate,
    DriftReport Drift);

public sealed record SummaryMetrics(
    int TotalCases,
    int PassedCases,
    int FailedCases,
    double WeightedScore,
    double FailureRate,
    double AverageLatencyMs,
    int TotalTokens,
    decimal TotalCostUsd);

public sealed record CaseScore(
    string CaseId,
    bool Passed,
    double Score,
    double LexicalF1,
    double RequiredTermCoverage,
    int ForbiddenTermHits,
    int LatencyMs,
    decimal CostUsd,
    IReadOnlyList<string> MissingRequiredTerms,
    IReadOnlyList<string> MatchedForbiddenTerms);

public sealed record GateResult(
    bool Passed,
    IReadOnlyList<string> Reasons);

public sealed record DriftReport(
    double DriftScore,
    double BaselineMeanScore,
    double CandidateMeanScore,
    IReadOnlyList<string> Warnings);
