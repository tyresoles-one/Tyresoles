using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace Tyresoles.SqlServer.Cli.Services;

public class EmailService
{
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUser;
    private readonly string _smtpPass;
    private readonly string _fromEmail;
    private readonly string _toEmail;

    public EmailService(string smtpHost, int smtpPort, string smtpUser, string smtpPass, string fromEmail, string toEmail)
    {
        _smtpHost = smtpHost;
        _smtpPort = smtpPort;
        _smtpUser = smtpUser;
        _smtpPass = smtpPass;
        _fromEmail = fromEmail;
        _toEmail = toEmail;
    }

    public async Task SendNotificationAsync(string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(_smtpHost) || string.IsNullOrWhiteSpace(_toEmail))
        {
            Console.WriteLine("Email notifications are not configured properly. Skipping email.");
            return;
        }

        try
        {
            using (var client = new SmtpClient(_smtpHost, _smtpPort))
            {
                client.Credentials = new NetworkCredential(_smtpUser, _smtpPass);
                client.EnableSsl = true;

                using (var message = new MailMessage(_fromEmail, _toEmail))
                {
                    message.Subject = subject;
                    message.Body = body;
                    message.IsBodyHtml = false;

                    await client.SendMailAsync(message);
                    Console.WriteLine($"Email notification sent: {subject}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send email notification: {ex.Message}");
        }
    }
}
