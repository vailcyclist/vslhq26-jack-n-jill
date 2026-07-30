using System.Text;
using System.Text.Json;

namespace FunnyCodeAnalyzer;

internal sealed class HumorService
{
    private static readonly HttpClient HttpClient = new();

    public string GetPunchline(AnalysisReport report, CliOptions options, FunnyMessageConfig config)
    {
        var issueKinds = report.Issues.Select(issue => issue.Kind).Distinct().ToArray();
        var localPunchline = GetLocalPunchline(issueKinds, options, config);

        return options.HumorSource switch
        {
            HumorSourceKind.Local => localPunchline,
            HumorSourceKind.Web => GetWebPunchline(issueKinds, options, config) ?? localPunchline,
            HumorSourceKind.Mixed => GetWebPunchline(issueKinds, options, config) ?? localPunchline,
            _ => localPunchline
        };
    }

    private static string GetLocalPunchline(IReadOnlyList<IssueKind> issueKinds, CliOptions options, FunnyMessageConfig config)
    {
        var candidates = new List<string>();

        if (config.Humor.ModePunchlines.TryGetValue(options.HumorMode, out var modePunchlines))
        {
            candidates.AddRange(modePunchlines);
        }

        foreach (var kind in issueKinds)
        {
            if (config.Humor.IssuePuns.TryGetValue(kind, out var puns))
            {
                candidates.AddRange(puns);
            }
        }

        candidates.AddRange(config.Humor.GeneralPunchlines);

        if (candidates.Count == 0)
        {
            return "The analyzer is standing by with a dry sense of humor and a clipboard.";
        }

        return candidates[Random.Shared.Next(candidates.Count)];
    }

    private static string? GetWebPunchline(IReadOnlyList<IssueKind> issueKinds, CliOptions options, FunnyMessageConfig config)
    {
        var clientApiConfig = HumorApiClientConfig.Load(options.HumorApiConfigPath);
        var apiTemplate = ResolveApiTemplate(options, config, clientApiConfig);
        if (string.IsNullOrWhiteSpace(apiTemplate))
        {
            return null;
        }

        var textProperty = options.HumorApiTextProperty ?? clientApiConfig.TextProperty ?? config.HumorApi.TextProperty;

        foreach (var issueKind in issueKinds.DefaultIfEmpty(IssueKind.TodoComment))
        {
            var url = apiTemplate
                .Replace("{issueKind}", issueKind.ToString().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)
                .Replace("{humorMode}", options.HumorMode.ToString().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)
                .Replace("{sourceControl}", options.SourceControl.ToString().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)
                .Replace("{userIdentifier}", Uri.EscapeDataString(options.SelfUser), StringComparison.OrdinalIgnoreCase);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                using var response = HttpClient.Send(request);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var payload = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var joke = ExtractText(payload, textProperty);
                if (!string.IsNullOrWhiteSpace(joke))
                {
                    return joke.Trim();
                }
            }
            catch
            {
                // Fall back to local humor.
            }
        }

        return null;
    }

    private static string? ResolveApiTemplate(CliOptions options, FunnyMessageConfig config, HumorApiClientConfig clientApiConfig)
    {
        if (options.HumorSource == HumorSourceKind.Local)
        {
            return null;
        }

        return options.HumorApiUrlTemplate
            ?? clientApiConfig.UrlTemplate
            ?? config.HumorApi.UrlTemplate
            ?? (config.HumorApi.EnabledByDefault ? config.HumorApi.UrlTemplate : null);
    }

    private static string? ExtractText(string payload, string? configuredProperty)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        payload = payload.Trim();
        if (!payload.StartsWith('{') && !payload.StartsWith('['))
        {
            return payload;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            return ExtractTextFromElement(document.RootElement, configuredProperty);
        }
        catch
        {
            return payload;
        }
    }

    private static string? ExtractTextFromElement(JsonElement element, string? configuredProperty)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            if (!string.IsNullOrWhiteSpace(configuredProperty) &&
                element.TryGetProperty(configuredProperty, out var configuredValue))
            {
                return ExtractTextFromElement(configuredValue, configuredProperty);
            }

            foreach (var propertyName in new[] { "text", "joke", "pun", "value", "message", "setup", "delivery" })
            {
                if (element.TryGetProperty(propertyName, out var value))
                {
                    var extracted = ExtractTextFromElement(value, configuredProperty);
                    if (!string.IsNullOrWhiteSpace(extracted))
                    {
                        return extracted;
                    }
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var extracted = ExtractTextFromElement(item, configuredProperty);
                if (!string.IsNullOrWhiteSpace(extracted))
                {
                    return extracted;
                }
            }
        }

        return null;
    }
}