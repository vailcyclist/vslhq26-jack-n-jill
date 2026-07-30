using FunnyCodeAnalyzer.Api.Models;
using Microsoft.Extensions.Options;

namespace FunnyCodeAnalyzer.Api.Services;

internal sealed class HumorEngine
{
    private readonly HumorCatalogOptions _catalog;

    public HumorEngine(IOptions<HumorCatalogOptions> catalog)
    {
        _catalog = catalog.Value;
    }

    public HumorResponse Generate(string issueTopic, string humorMode, string? topic = null)
    {
        var normalizedIssueType = NormalizeKey(issueTopic);
        var requestedHumorLevel = MapModeToLevel(humorMode);
        var resolvedHumorLevel = ResolveHumorLevel(requestedHumorLevel);

        var issueEntry = ResolveIssueEntry(normalizedIssueType);
        var resolvedTopic = ResolveTopic(topic ?? issueEntry?.DefaultTopic ?? _catalog.Defaults.Topic);
        var issueMessages = issueEntry?.Messages ?? new List<HumorTemplateEntry>();

        var line = SelectIssueMessage(issueMessages, resolvedTopic, resolvedHumorLevel)
            ?? SelectFallback(_catalog.Fallbacks.NoTopicMatch, resolvedTopic, resolvedHumorLevel)
            ?? SelectFallback(_catalog.Fallbacks.NoHumorLevelMatch, _catalog.Defaults.FallbackTopic, _catalog.Defaults.FallbackHumorLevel)
            ?? "The analyzer spotted something odd and brought a polite warning.";

        return new HumorResponse
        {
            IssueTopic = normalizedIssueType,
            HumorMode = resolvedHumorLevel,
            Topic = resolvedTopic,
            Text = line
        };
    }

    public HumorPolicyResponse ResolvePolicy(string issueTopic, string humorMode, string? topic, string? channel)
    {
        var normalizedIssueType = NormalizeKey(issueTopic);
        var requestedHumorLevel = MapModeToLevel(humorMode);
        var resolvedHumorLevel = ResolveHumorLevel(requestedHumorLevel);
        var issueEntry = ResolveIssueEntry(normalizedIssueType);

        var resolvedTopic = ResolveTopic(topic ?? issueEntry?.DefaultTopic ?? _catalog.Defaults.Topic);
        var resolvedChannel = ResolveChannel(channel ?? _catalog.Defaults.Channel);

        var levelEntry = _catalog.HumorLevels.TryGetValue(resolvedHumorLevel, out var level)
            ? level
            : new HumorLevelEntry();

        var candidates = (issueEntry?.Messages ?? new List<HumorTemplateEntry>())
            .Where(item => item.Enabled)
            .Where(item => Matches(item.HumorLevels, resolvedHumorLevel))
            .Where(item => Matches(item.Topics, resolvedTopic))
            .Where(item => Matches(item.Channels, resolvedChannel))
            .Where(item => item.Spice <= levelEntry.MaxSpice)
            .Select(item => item.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new HumorPolicyResponse
        {
            RequestedIssueTopic = issueTopic,
            RequestedHumorMode = humorMode,
            RequestedTopic = topic ?? string.Empty,
            RequestedChannel = channel ?? string.Empty,
            ResolvedIssueType = normalizedIssueType,
            ResolvedHumorLevel = resolvedHumorLevel,
            ResolvedTopic = resolvedTopic,
            ResolvedChannel = resolvedChannel,
            MaxSpice = levelEntry.MaxSpice,
            AllowSarcasm = _catalog.Defaults.AllowSarcasm && levelEntry.AllowSarcasm,
            AllowMildInsults = _catalog.Defaults.AllowInsults && levelEntry.AllowMildInsults,
            AllowProfanity = _catalog.Defaults.AllowAdultHumor && levelEntry.AllowProfanity,
            IssueFound = issueEntry is not null,
            CandidateTemplateIds = candidates,
            CandidateTemplateCount = candidates.Length
        };
    }

    private string? SelectIssueMessage(
        IReadOnlyList<HumorTemplateEntry> messages,
        string topic,
        string humorLevel)
    {
        if (messages.Count == 0)
        {
            return null;
        }

        var withTopic = SelectTemplate(messages, topic, humorLevel);
        if (withTopic is not null)
        {
            return withTopic.Template;
        }

        var fallbackTopic = ResolveTopic(_catalog.Defaults.FallbackTopic);
        var withFallbackTopic = SelectTemplate(messages, fallbackTopic, humorLevel);
        if (withFallbackTopic is not null)
        {
            return withFallbackTopic.Template;
        }

        return SelectTemplate(messages, null, humorLevel, ignoreTopic: true)?.Template;
    }

    private string? SelectFallback(
        IReadOnlyList<HumorTemplateEntry> templates,
        string topic,
        string humorLevel)
    {
        return SelectTemplate(templates, topic, humorLevel)?.Template
            ?? SelectTemplate(templates, null, humorLevel, ignoreTopic: true)?.Template;
    }

    private HumorTemplateEntry? SelectTemplate(
        IReadOnlyList<HumorTemplateEntry> templates,
        string? topic,
        string humorLevel,
        bool ignoreTopic = false)
    {
        var level = _catalog.HumorLevels.TryGetValue(humorLevel, out var levelEntry)
            ? levelEntry
            : null;

        var allowProfanity = _catalog.Defaults.AllowAdultHumor && level?.AllowProfanity == true;
        var effectiveSpice = level?.MaxSpice ?? 0;

        var filtered = templates
            .Where(item => item.Enabled)
            .Where(item => !string.IsNullOrWhiteSpace(item.Template))
            .Where(item => item.Spice <= effectiveSpice)
            .Where(item => TemplateContainsProfanity(item.Template) ? allowProfanity : true)
            .Where(item => Matches(item.HumorLevels, humorLevel))
            .Where(item => ignoreTopic || Matches(item.Topics, topic ?? _catalog.Defaults.Topic))
            .ToArray();

        if (filtered.Length == 0)
        {
            return null;
        }

        var totalWeight = filtered.Sum(item => Math.Max(1, item.Weight));
        var roll = Random.Shared.Next(totalWeight);
        var running = 0;
        foreach (var item in filtered)
        {
            running += Math.Max(1, item.Weight);
            if (roll < running)
            {
                return item;
            }
        }

        return filtered[^1];
    }

    private HumorIssueEntry? ResolveIssueEntry(string issueType)
    {
        if (_catalog.Issues.TryGetValue(issueType, out var directMatch))
        {
            return directMatch;
        }

        return _catalog.Issues.FirstOrDefault(pair =>
            NormalizeKey(pair.Key).Equals(issueType, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private string ResolveTopic(string? topic)
    {
        var normalizedRequested = NormalizeKey(topic);
        if (_catalog.Topics.ContainsKey(normalizedRequested))
        {
            return normalizedRequested;
        }

        foreach (var entry in _catalog.Topics)
        {
            if (entry.Value.Aliases.Any(alias => NormalizeKey(alias).Equals(normalizedRequested, StringComparison.OrdinalIgnoreCase)))
            {
                return entry.Key;
            }
        }

        return NormalizeKey(_catalog.Defaults.FallbackTopic);
    }

    private string ResolveHumorLevel(string requestedLevel)
    {
        if (_catalog.HumorLevels.ContainsKey(requestedLevel))
        {
            return requestedLevel;
        }

        var fallback = NormalizeKey(_catalog.Defaults.FallbackHumorLevel);
        if (_catalog.HumorLevels.ContainsKey(fallback))
        {
            return fallback;
        }

        return _catalog.HumorLevels.Keys.FirstOrDefault() ?? "lightHearted";
    }

    private string ResolveChannel(string channel)
    {
        var normalized = NormalizeKey(channel);
        if (_catalog.Channels.ContainsKey(normalized))
        {
            return normalized;
        }

        var fallback = NormalizeKey(_catalog.Defaults.Channel);
        if (_catalog.Channels.ContainsKey(fallback))
        {
            return fallback;
        }

        return _catalog.Channels.Keys.FirstOrDefault() ?? "teams";
    }

    private static bool Matches(IReadOnlyList<string> values, string requested)
    {
        if (values.Count == 0)
        {
            return true;
        }

        return values.Any(value => NormalizeKey(value).Equals(NormalizeKey(requested), StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "general";
        }

        return value.Trim().Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
    }

    private static string MapModeToLevel(string mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return "lighthearted";
        }

        var normalized = NormalizeKey(mode);
        return normalized switch
        {
            "light" => "lighthearted",
            "kidfriendly" => "kidfriendly",
            "dad" => "average",
            "dirty" => "dirty",
            _ => normalized
        };
    }

    private static bool TemplateContainsProfanity(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return false;
        }

        var profanitySignals = new[] { " damn", " hell", " crap", " shit", " fuck" };
        var padded = " " + template.ToLowerInvariant();
        return profanitySignals.Any(signal => padded.Contains(signal, StringComparison.Ordinal));
    }
}