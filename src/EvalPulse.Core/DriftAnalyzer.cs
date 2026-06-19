namespace EvalPulse.Core;

public static class DriftAnalyzer
{
    public static DriftReport Compare(
        EvalDataset dataset,
        ModelRun baseline,
        ModelRun candidate,
        IReadOnlyList<CaseScore> candidateScores)
    {
        var evaluator = new Evaluator();
        var baselineReport = evaluator.Evaluate(dataset, baseline);
        var baselineScores = baselineReport.Cases.ToDictionary(score => score.CaseId, StringComparer.OrdinalIgnoreCase);

        var deltas = new List<double>();
        var warnings = new List<string>();
        foreach (var candidateScore in candidateScores)
        {
            if (!baselineScores.TryGetValue(candidateScore.CaseId, out var baselineScore))
            {
                continue;
            }
            var delta = baselineScore.Score - candidateScore.Score;
            deltas.Add(Math.Max(0, delta));
            if (delta >= 0.2)
            {
                warnings.Add($"{candidateScore.CaseId} dropped by {delta:0.000}");
            }
        }

        var driftScore = deltas.Count == 0 ? 0 : deltas.Average();
        return new DriftReport(
            DriftScore: Math.Round(driftScore, 4),
            BaselineMeanScore: baselineReport.Summary.WeightedScore,
            CandidateMeanScore: candidateScores.Count == 0 ? 0 : Math.Round(candidateScores.Average(score => score.Score), 4),
            Warnings: warnings);
    }
}
