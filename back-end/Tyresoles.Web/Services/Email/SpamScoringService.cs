using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Tyresoles.Web.Services.Email;

public class SpamScoreResult
{
    public int Score { get; set; } = 100;
    public string Rating { get; set; } = "Excellent"; // Excellent, Good, Warning, HighRisk
    public List<string> Warnings { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public interface ISpamScoringService
{
    SpamScoreResult EvaluateEmail(string subject, string? bodyHtml, string? bodyText);
}

public class SpamScoringService : ISpamScoringService
{
    private static readonly string[] SpamTriggerWords = new[]
    {
        "100% free", "guaranteed", "act now", "risk free", "no risk",
        "make money", "winner", "congratulations", "urgent", "cash bonus",
        "million dollars", "credit card", "order now", "apply now", "buy direct",
        "exclusive deal", "unlimited", "instant", "miracle", "satisfaction guaranteed"
    };

    public SpamScoreResult EvaluateEmail(string subject, string? bodyHtml, string? bodyText)
    {
        var result = new SpamScoreResult();
        int score = 100;

        var combinedContent = $"{subject} {bodyHtml ?? ""} {bodyText ?? ""}".ToLowerInvariant();

        // 1. Subject Line Checks
        if (string.IsNullOrWhiteSpace(subject))
        {
            score -= 30;
            result.Warnings.Add("Subject line is empty.");
        }
        else
        {
            // All-caps subject check
            var letters = Regex.Matches(subject, @"[a-zA-Z]");
            if (letters.Count > 4)
            {
                var upperCount = 0;
                foreach (Match m in letters)
                {
                    if (char.IsUpper(m.Value[0])) upperCount++;
                }
                if ((double)upperCount / letters.Count > 0.6)
                {
                    score -= 15;
                    result.Warnings.Add("Subject line contains excessive CAPITAL letters (triggers spam filters).");
                }
            }

            // Exclamation marks in subject
            var exclamations = Regex.Matches(subject, @"!").Count;
            if (exclamations > 1)
            {
                score -= 10;
                result.Warnings.Add("Subject line contains multiple exclamation marks (!).");
            }

            // Currency signs in subject
            if (Regex.IsMatch(subject, @"[\$₹€£]"))
            {
                score -= 10;
                result.Warnings.Add("Subject line contains currency symbols ($/₹/€).");
            }
        }

        // 2. Spam Trigger Keyword Scanning
        int triggerCount = 0;
        foreach (var word in SpamTriggerWords)
        {
            if (combinedContent.Contains(word))
            {
                triggerCount++;
                if (triggerCount <= 3)
                {
                    result.Warnings.Add($"Contains spam trigger phrase: \"{word}\".");
                }
            }
        }
        if (triggerCount > 0)
        {
            score -= Math.Min(30, triggerCount * 8);
        }

        // 3. Unsubscribe Link Check
        var hasUnsub = combinedContent.Contains("unsubscribe") || combinedContent.Contains("opt-out") || combinedContent.Contains("opt out");
        if (!hasUnsub)
        {
            score -= 20;
            result.Warnings.Add("Missing visible 'Unsubscribe' link or instructions in email body (Required by Google/Yahoo 2024).");
        }

        // 4. Physical Postal Address Check (CAN-SPAM / Legal Requirement)
        var hasAddress = combinedContent.Contains("address") || combinedContent.Contains("pvt") || combinedContent.Contains("ltd") || combinedContent.Contains("road") || combinedContent.Contains("nagar") || combinedContent.Contains("india") || combinedContent.Contains("mumbai");
        if (!hasAddress)
        {
            score -= 10;
            result.Recommendations.Add("Add company physical registered address in the email footer for CAN-SPAM compliance.");
        }

        // 5. HTML to Text / Image-Only Check
        if (!string.IsNullOrEmpty(bodyHtml))
        {
            var hasImages = bodyHtml.Contains("<img", StringComparison.OrdinalIgnoreCase);
            var textOnly = Regex.Replace(bodyHtml, @"<[^>]*>", " ").Trim();
            if (hasImages && textOnly.Length < 100)
            {
                score -= 20;
                result.Warnings.Add("Image-heavy email with very little text. Spam filters frequently block image-only emails.");
            }

            // Excessive link count check
            var linkCount = Regex.Matches(bodyHtml, @"<a\s+[^>]*href=", RegexOptions.IgnoreCase).Count;
            if (linkCount > 10)
            {
                score -= 10;
                result.Warnings.Add($"High number of links ({linkCount}). Keep links under 5 for highest deliverability.");
            }
        }

        result.Score = Math.Clamp(score, 0, 100);

        if (result.Score >= 85)
            result.Rating = "Excellent";
        else if (result.Score >= 70)
            result.Rating = "Good";
        else if (result.Score >= 50)
            result.Rating = "Warning";
        else
            result.Rating = "HighRisk";

        if (result.Recommendations.Count == 0 && result.Score == 100)
        {
            result.Recommendations.Add("Email content looks clean, balanced, and compliant with 2024 deliverability standards.");
        }

        return result;
    }
}
