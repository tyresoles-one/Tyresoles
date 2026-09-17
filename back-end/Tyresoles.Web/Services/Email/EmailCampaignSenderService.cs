using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Tyresoles.Data.Features.Crm.Entities;

namespace Tyresoles.Web.Services.Email;

public interface IEmailCampaignSenderService
{
    Task<bool> SendSingleCampaignEmailAsync(
        CrmEmailCampaign campaign,
        CrmEmailCampaignRecipient recipient,
        CrmContact? contact,
        CancellationToken cancellationToken = default);

    Task<bool> SendDirectTestEmailAsync(
        string toEmail,
        string subject,
        string bodyHtml,
        string? bodyText,
        string fromName,
        string fromEmail,
        CancellationToken cancellationToken = default);
}

public class EmailCampaignSenderService : IEmailCampaignSenderService
{
    private readonly AmazonSesSmtpSettings _settings;
    private readonly ILogger<EmailCampaignSenderService> _logger;

    public EmailCampaignSenderService(
        IOptions<AmazonSesSmtpSettings> settings,
        ILogger<EmailCampaignSenderService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> SendSingleCampaignEmailAsync(
        CrmEmailCampaign campaign,
        CrmEmailCampaignRecipient recipient,
        CrmContact? contact,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new MimeMessage();

            var fromName = !string.IsNullOrWhiteSpace(campaign.FromName) ? campaign.FromName : _settings.DefaultFromName;
            var fromEmail = !string.IsNullOrWhiteSpace(campaign.FromEmail) ? campaign.FromEmail : _settings.DefaultFromEmail;
            message.From.Add(new MailboxAddress(fromName, fromEmail));

            message.To.Add(new MailboxAddress(recipient.FullName, recipient.EmailAddress));

            var replyTo = !string.IsNullOrWhiteSpace(campaign.ReplyToEmail) ? campaign.ReplyToEmail : _settings.DefaultReplyToEmail;
            if (!string.IsNullOrWhiteSpace(replyTo))
            {
                message.ReplyTo.Add(MailboxAddress.Parse(replyTo));
            }

            // Merge Token Values
            var fullName = !string.IsNullOrWhiteSpace(recipient.FullName) ? recipient.FullName : (contact?.FullName ?? "Valued Partner");
            var companyName = !string.IsNullOrWhiteSpace(recipient.CompanyName) ? recipient.CompanyName : (contact?.CompanyName ?? "Your Company");
            var city = contact?.City ?? "";
            var products = contact?.Products ?? "";

            var trackingBaseUrl = _settings.TrackingBaseUrl.TrimEnd('/');
            var unsubUrl = $"{trackingBaseUrl}/api/campaigns/unsubscribe?token={recipient.TrackingToken}";

            // Replace in Subject
            var subject = ReplaceMergeTokens(campaign.Subject, fullName, companyName, city, products, unsubUrl);
            message.Subject = subject;

            // MANDATORY RFC 8058 ONE-CLICK LIST-UNSUBSCRIBE HEADERS (Google & Yahoo 2024 compliance)
            message.Headers.Add("List-Unsubscribe", $"<{unsubUrl}>, <mailto:unsub+{recipient.TrackingToken}@tyresoles.in>");
            message.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
            message.Headers.Add("X-Campaign-Id", campaign.Id.ToString());
            message.Headers.Add("X-Recipient-Id", recipient.Id.ToString());

            // Build Body
            var builder = new BodyBuilder();

            // Plain Text Version
            var rawText = !string.IsNullOrWhiteSpace(campaign.BodyText)
                ? campaign.BodyText
                : StripHtml(campaign.BodyHtml ?? "");
            var mergedText = ReplaceMergeTokens(rawText, fullName, companyName, city, products, unsubUrl);
            builder.TextBody = mergedText;

            // HTML Version
            if (!string.IsNullOrWhiteSpace(campaign.BodyHtml))
            {
                var mergedHtml = ReplaceMergeTokens(campaign.BodyHtml, fullName, companyName, city, products, unsubUrl);

                // 1. Rewrite <a href="..."> links for click tracking
                mergedHtml = RewriteLinksForTracking(mergedHtml, recipient.TrackingToken, trackingBaseUrl);

                // 2. Inject 1x1 zero-cache open tracking pixel
                var trackingPixel = $"<img src=\"{trackingBaseUrl}/api/campaigns/track/open/{recipient.TrackingToken}.png\" width=\"1\" height=\"1\" alt=\"\" style=\"display:none!important;\" />";
                if (mergedHtml.Contains("</body>", StringComparison.OrdinalIgnoreCase))
                {
                    mergedHtml = Regex.Replace(mergedHtml, @"(</body>)", $"{trackingPixel}$1", RegexOptions.IgnoreCase);
                }
                else
                {
                    mergedHtml += trackingPixel;
                }

                builder.HtmlBody = mergedHtml;
            }

            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            client.Timeout = 15000;

            await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls, cancellationToken);
            if (!string.IsNullOrWhiteSpace(_settings.Username) && !string.IsNullOrWhiteSpace(_settings.Password))
            {
                await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            recipient.Status = "Sent";
            recipient.SentAt = DateTime.UtcNow;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch email for recipient {RecipientId} ({Email}) in campaign {CampaignId}",
                recipient.Id, recipient.EmailAddress, campaign.Id);
            recipient.Status = "Failed";
            recipient.ErrorMessage = ex.Message;
            return false;
        }
    }

    public async Task<bool> SendDirectTestEmailAsync(
        string toEmail,
        string subject,
        string bodyHtml,
        string? bodyText,
        string fromName,
        string fromEmail,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new MimeMessage();
            var effectiveFromName = !string.IsNullOrWhiteSpace(fromName) ? fromName : _settings.DefaultFromName;
            var effectiveFromEmail = !string.IsNullOrWhiteSpace(fromEmail) ? fromEmail : _settings.DefaultFromEmail;

            message.From.Add(new MailboxAddress(effectiveFromName, effectiveFromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = $"[TEST] {subject}";

            var builder = new BodyBuilder
            {
                HtmlBody = bodyHtml,
                TextBody = bodyText ?? StripHtml(bodyHtml)
            };
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            client.Timeout = 15000;
            await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls, cancellationToken);
            if (!string.IsNullOrWhiteSpace(_settings.Username) && !string.IsNullOrWhiteSpace(_settings.Password))
            {
                await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
            }
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send direct test email to {ToEmail}", toEmail);
            throw;
        }
    }

    private static string ReplaceMergeTokens(string template, string fullName, string companyName, string city, string products, string unsubUrl)
    {
        if (string.IsNullOrEmpty(template)) return "";

        return template
            .Replace("{{FullName}}", fullName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{contact.FullName}}", fullName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{FirstName}}", fullName.Split(' ')[0], StringComparison.OrdinalIgnoreCase)
            .Replace("{{CompanyName}}", companyName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{contact.CompanyName}}", companyName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{City}}", city, StringComparison.OrdinalIgnoreCase)
            .Replace("{{contact.City}}", city, StringComparison.OrdinalIgnoreCase)
            .Replace("{{Products}}", products, StringComparison.OrdinalIgnoreCase)
            .Replace("{{UnsubscribeLink}}", unsubUrl, StringComparison.OrdinalIgnoreCase);
    }

    private static string RewriteLinksForTracking(string html, string token, string trackingBaseUrl)
    {
        if (string.IsNullOrEmpty(html)) return "";

        return Regex.Replace(
            html,
            @"<a\s+(?<pre>[^>]*?)href=[""'](?<url>https?://[^""']+)[""'](?<post>[^>]*?)>",
            m =>
            {
                var originalUrl = m.Groups["url"].Value;
                // Don't rewrite unsubscribe endpoints
                if (originalUrl.Contains("/api/campaigns/unsubscribe", StringComparison.OrdinalIgnoreCase))
                {
                    return m.Value;
                }

                var encodedOriginal = UrlEncoder.Default.Encode(originalUrl);
                var trackUrl = $"{trackingBaseUrl}/api/campaigns/track/click/{token}?target={encodedOriginal}";
                return $"<a {m.Groups["pre"].Value}href=\"{trackUrl}\"{m.Groups["post"].Value}>";
            },
            RegexOptions.IgnoreCase);
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        return Regex.Replace(html, @"<[^>]*>", " ").Trim();
    }
}
