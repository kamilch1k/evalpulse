namespace EvalPulse.Core;

public sealed class Evaluator
{
    public EvalReport Evaluate(EvalDataset dataset, ModelRun candidate, ModelRun? baseline = null)
    {
        var answersByCase = candidate.Answers.ToDictionary(answer => answer.CaseId, StringComparer.OrdinalIgnoreCase);
        var scores = new List<CaseScore>();

        foreach (var evalCase in dataset.Cases)
        {
            if (!answersByCase.TryGetValue(evalCase.Id, out var answer))
            {
                scores.Add(new CaseScore(
                    evalCase.Id,
                    Passed: false,
                    Score: 0,
                    LexicalF1: 0,
                    RequiredTermCoverage: 0,
                    ForbiddenTermHits: 0,
                    LatencyMs: 0,
                    CostUsd: 0,
                    MissingRequiredTerms: evalCase.RequiredTerms,
                    MatchedForbiddenTerms: Array.Empty<string>()));
                continue;
            }

            var lexicalF1 = TextMetrics.TokenF1(evalCase.ExpectedAnswer, answer.Output);
            var coverage = TextMetrics.Coverage(evalCase.RequiredTerms, answer.Output, out var missing);
            var forbiddenHits = TextMetrics.ForbiddenHits(evalCase.ForbiddenTerms, answer.Output, out var matched);
            var penalty = forbiddenHits > 0 ? 0.25 * forbiddenHits : 0;
            var score = Math.Clamp((lexicalF1 * 0.55) + (coverage * 0.45) - penalty, 0, 1);

            scores.Add(new CaseScore(
                evalCase.Id,
                Passed: score >= 0.7 && forbiddenHits == 0,
                Score: Math.Round(score, 4),
                LexicalF1: Math.Round(lexicalF1, 4),
                RequiredTermCoverage: Math.Round(coverage, 4),
                ForbiddenTermHits: forbiddenHits,
                LatencyMs: answer.LatencyMs,
                CostUsd: answer.CostUsd,
                MissingRequiredTerms: missing,
                MatchedForbiddenTerms: matched));
        }

        var summary = Summarize(dataset, candidate, scores);
        var drift = baseline is null
            ? new DriftReport(0, 0, summary.WeightedScore, Array.Empty<string>())
            : DriftAnalyzer.Compare(dataset, baseline, candidate, scores);
        var gate = EvaluateGate(dataset.Gate, summary, drift);

        return new EvalReport(
            candidate.RunId,
            candidate.Model,
            dataset.Name,
            candidate.StartedAt,
            summary,
            scores,
            gate,
            drift);
    }

    private static SummaryMetrics Summarize(EvalDataset dataset, ModelRun run, IReadOnlyList<CaseScore> scores)
    {
        var weightsById = dataset.Cases.ToDictionary(item => item.Id, item => item.Weight, StringComparer.OrdinalIgnoreCase);
        var totalWeight = weightsById.Values.Sum();
        var weightedScore = scores.Sum(score => score.Score * weightsById.GetValueOrDefault(score.CaseId, 1)) / totalWeight;
        var answersByCase = run.Answers.ToDictionary(answer => answer.CaseId, StringComparer.OrdinalIgnoreCase);
        var answered = scores.Select(score => answersByCase.GetValueOrDefault(score.CaseId)).OfType<ModelAnswer>().ToList();

        return new SummaryMetrics(
            TotalCases: scores.Count,
            PassedCases: scores.Count(score => score.Passed),
            FailedCases: scores.Count(score => !score.Passed),
            WeightedScore: Math.Round(weightedScore, 4),
            FailureRate: Math.Round(scores.Count(score => !score.Passed) / (double)scores.Count, 4),
            AverageLatencyMs: Math.Round(answered.Count == 0 ? 0 : answered.Average(answer => answer.LatencyMs), 2),
            TotalTokens: answered.Sum(answer => answer.PromptTokens + answer.CompletionTokens),
            TotalCostUsd: answered.Sum(answer => answer.CostUsd));
    }

    private static GateResult EvaluateGate(RegressionGate gate, SummaryMetrics summary, DriftReport drift)
    {
        var reasons = new List<string>();
        if (summary.WeightedScore < gate.MinWeightedScore)
        {
            reasons.Add($"weighted score {summary.WeightedScore:0.000} below {gate.MinWeightedScore:0.000}");
        }
        if (summary.FailureRate > gate.MaxFailureRate)
        {
            reasons.Add($"failure rate {summary.FailureRate:0.000} above {gate.MaxFailureRate:0.000}");
        }
        if (summary.AverageLatencyMs > gate.MaxAverageLatencyMs)
        {
            reasons.Add($"average latency {summary.AverageLatencyMs:0.0}ms above {gate.MaxAverageLatencyMs:0.0}ms");
        }
        if (summary.TotalCostUsd > gate.MaxTotalCostUsd)
        {
            reasons.Add($"total cost ${summary.TotalCostUsd:0.0000} above ${gate.MaxTotalCostUsd:0.0000}");
        }
        if (drift.DriftScore > gate.MaxDriftScore)
        {
            reasons.Add($"drift score {drift.DriftScore:0.000} above {gate.MaxDriftScore:0.000}");
        }

        return new GateResult(reasons.Count == 0, reasons);
    }
}
