using System;
using System.Collections.Generic;
using System.Linq;

namespace ZedRecentProjects;

/// <summary>
/// Self-contained fuzzy matcher (case-insensitive). No dependency on the Wox host or pinyin
/// runtime: exact match &gt; substring &gt; subsequence. Multiple whitespace-separated terms
/// must all match (AND semantics).
/// </summary>
public static class TextMatcher
{
    public static bool TryScore(string query, string text, out int score, out List<int> highlight)
    {
        score = 0;
        highlight = new List<int>();

        if (string.IsNullOrEmpty(query))
        {
            return true; // empty query matches everything
        }

        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0)
        {
            return true;
        }

        var total = 0;
        var hits = new List<int>();
        foreach (var term in terms)
        {
            if (!ScoreTerm(term, text, out var termScore, out var termHighlight))
            {
                score = 0;
                highlight = new List<int>();
                return false;
            }

            total += termScore;
            hits.AddRange(termHighlight);
        }

        score = total;
        highlight = hits;
        return true;
    }

    private static bool ScoreTerm(string term, string text, out int score, out List<int> highlight)
    {
        score = 0;
        highlight = new List<int>();

        var index = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            score = 7000 - Math.Min(index, 500);
            highlight = Enumerable.Range(index, term.Length).ToList();
            return true;
        }

        if (TrySubsequence(term, text, out var indices))
        {
            score = 4000 + indices.Count * 3 - Math.Min(indices[0], 100);
            highlight = indices;
            return true;
        }

        return false;
    }

    private static bool TrySubsequence(string term, string text, out List<int> indices)
    {
        indices = new List<int>();
        var query = term.ToLowerInvariant();
        var target = text.ToLowerInvariant();

        int targetIndex = 0;
        for (int queryIndex = 0; queryIndex < query.Length; queryIndex++)
        {
            var found = -1;
            for (; targetIndex < target.Length; targetIndex++)
            {
                if (target[targetIndex] == query[queryIndex])
                {
                    found = targetIndex;
                    break;
                }
            }

            if (found < 0)
            {
                return false;
            }

            indices.Add(found);
            targetIndex = found + 1;
        }

        return true;
    }
}