using System.Text.RegularExpressions;

namespace EvalPulse.Core;

public static partial class TextMetrics
{
    public static double TokenF1(string expected, string actual)
    {
        var expectedCounts = TokenCounts(expected);
        var actualCounts = TokenCounts(actual);
        if (expectedCounts.Count == 0 && actualCounts.Count == 0)
        {
            return 1;
        }
        if (expectedCounts.Count == 0 || actualCounts.Count == 0)
        {
            return 0;
        }

        var overlap = 0;
        foreach (var (token, expectedCount) in expectedCounts)
        {
            if (actualCounts.TryGetValue(token, out var actualCount))
            {
                overlap += Math.Min(expectedCount, actualCount);
            }
        }

        var expectedTotal = expectedCounts.Values.Sum();
        var actualTotal = actualCounts.Values.Sum();
        if (overlap == 0)
        {
            return 0;
        }

        var precision = overlap / (double)actualTotal;
        var recall = overlap / (double)expectedTotal;
        return 2 * precision * recall / (precision + recall);
    }

    public static double Coverage(IEnumerable<string> requiredTerms, string actual, out IReadOnlyList<string> missing)
    {
        var missingTerms = new List<string>();
        var normalized = NormalizeForSearch(actual);
        var total = 0;
        var hits = 0;

        foreach (var term in requiredTerms)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                continue;
            }
            total++;
            if (normalized.Contains(NormalizeForSearch(term), StringComparison.Ordinal))
            {
                hits++;
            }
            else
            {
                missingTerms.Add(term);
            }
        }

        missing = missingTerms;
        return total == 0 ? 1 : hits / (double)total;
    }

    public static int ForbiddenHits(IEnumerable<string> forbiddenTerms, string actual, out IReadOnlyList<string> matched)
    {
        var matchedTerms = new List<string>();
        var normalized = NormalizeForSearch(actual);
        foreach (var term in forbiddenTerms)
        {
            if (!string.IsNullOrWhiteSpace(term) && normalized.Contains(NormalizeForSearch(term), StringComparison.Ordinal))
            {
                matchedTerms.Add(term);
            }
        }

        matched = matchedTerms;
        return matchedTerms.Count;
    }

    private static Dictionary<string, int> TokenCounts(string text)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Match match in WordRegex().Matches(text.ToLowerInvariant()))
        {
            var token = match.Value;
            counts[token] = counts.TryGetValue(token, out var current) ? current + 1 : 1;
        }
        return counts;
    }

    private static string NormalizeForSearch(string value)
    {
        return string.Join(' ', WordRegex().Matches(value.ToLowerInvariant()).Select(match => match.Value));
    }

    [GeneratedRegex("[a-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex WordRegex();
}
