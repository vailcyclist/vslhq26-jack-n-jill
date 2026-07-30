using System.Net;
using System.Net.Mail;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FunnyCodeAnalyzer.Api.Models;
using Microsoft.Extensions.Options;

namespace FunnyCodeAnalyzer.Api.Services;

internal sealed class NotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SmtpOptions _smtpOptions;
    private readonly TeamsOptions _teamsOptions;

    public NotificationService(
        IHttpClientFactory httpClientFactory,
        IOptions<SmtpOptions> smtpOptions,
        IOptions<TeamsOptions> teamsOptions)
    {
        _httpClientFactory = httpClientFactory;
        _smtpOptions = smtpOptions.Value;
        _teamsOptions = teamsOptions.Value;
    }

    public async Task SendEmailAsync(OutgoingEmailMessage message)
    {
        if (string.IsNullOrWhiteSpace(_smtpOptions.Host))
        {
            throw new InvalidOperationException("SMTP host is not configured.");
        }

        using var smtpClient = new SmtpClient(_smtpOptions.Host, _smtpOptions.Port)
        {
            EnableSsl = _smtpOptions.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_smtpOptions.Username))
        {
            smtpClient.Credentials = new NetworkCredential(_smtpOptions.Username, _smtpOptions.Password);
        }

        var fromEmail = string.IsNullOrWhiteSpace(message.FromEmail)
            ? _smtpOptions.DefaultFromEmail
            : message.FromEmail;
        var fromName = string.IsNullOrWhiteSpace(message.FromDisplayName)
            ? _smtpOptions.DefaultFromName
            : message.FromDisplayName;

        if (string.IsNullOrWhiteSpace(fromEmail))
        {
            throw new InvalidOperationException("From email is missing. Configure Smtp:DefaultFromEmail or provide fromEmail in request.");
        }

        using var mail = new MailMessage
        {
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = message.IsHtml,
            From = new MailAddress(fromEmail, fromName)
        };

        mail.To.Add(message.ToEmail);
        AddOptionalAddresses(mail.CC, message.Cc);
        AddOptionalAddresses(mail.Bcc, message.Bcc);

        await smtpClient.SendMailAsync(mail);
    }

    public async Task SendTeamsMessageAsync(OutgoingTeamsMessage message)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", message.GraphAccessToken);

        var chatId = message.TeamsChatId;
        if (string.IsNullOrWhiteSpace(chatId))
        {
            if (string.IsNullOrWhiteSpace(message.RecipientUserId) || string.IsNullOrWhiteSpace(message.SenderUserId))
            {
                throw new InvalidOperationException("recipientUserId and senderUserId are required when teamsChatId is not provided.");
            }

            chatId = await CreateOneOnOneChatAsync(httpClient, message.SenderUserId, message.RecipientUserId);
        }

        var url = BuildGraphUrl($"/chats/{chatId}/messages");
        var payload = new
        {
            body = new
            {
                contentType = "text",
                content = message.Message
            }
        };

        using var response = await httpClient.PostAsJsonAsync(url, payload);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Teams send failed: {(int)response.StatusCode} {response.ReasonPhrase} {error}");
        }
    }

    private async Task<string> CreateOneOnOneChatAsync(HttpClient httpClient, string senderUserId, string recipientUserId)
    {
        var url = BuildGraphUrl("/chats");
        var payload = new Dictionary<string, object?>
        {
            ["chatType"] = "oneOnOne",
            ["members"] = new object?[]
            {
                new Dictionary<string, object?>
                {
                    ["@odata.type"] = "#microsoft.graph.aadUserConversationMember",
                    ["roles"] = Array.Empty<string>(),
                    ["user@odata.bind"] = $"https://graph.microsoft.com/v1.0/users('{senderUserId}')"
                },
                new Dictionary<string, object?>
                {
                    ["@odata.type"] = "#microsoft.graph.aadUserConversationMember",
                    ["roles"] = Array.Empty<string>(),
                    ["user@odata.bind"] = $"https://graph.microsoft.com/v1.0/users('{recipientUserId}')"
                }
            }
        };

        using var response = await httpClient.PostAsJsonAsync(url, payload);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Teams chat creation failed: {(int)response.StatusCode} {response.ReasonPhrase} {error}");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("id", out var idElement))
        {
            throw new InvalidOperationException("Teams chat creation returned no chat id.");
        }

        return idElement.GetString() ?? throw new InvalidOperationException("Teams chat id was empty.");
    }

    private string BuildGraphUrl(string path)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_teamsOptions.GraphBaseUrl)
            ? "https://graph.microsoft.com/v1.0"
            : _teamsOptions.GraphBaseUrl.TrimEnd('/');
        return $"{baseUrl}{path}";
    }

    private static void AddOptionalAddresses(MailAddressCollection collection, string? csvAddresses)
    {
        if (string.IsNullOrWhiteSpace(csvAddresses))
        {
            return;
        }

        foreach (var address in csvAddresses.Split(';', ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            collection.Add(address);
        }
    }
}