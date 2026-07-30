namespace FunnyCodeAnalyzer.Api.Models;

internal sealed class HumorCatalogOptions
{
    public string Version { get; set; } = "2.0";

    public string DefaultCulture { get; set; } = "en-US";

    public HumorCatalogDefaults Defaults { get; set; } = new();

    public Dictionary<string, HumorLevelEntry> HumorLevels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, HumorTopicEntry> Topics { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, HumorChannelEntry> Channels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, HumorIssueEntry> Issues { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public HumorFallbackEntry Fallbacks { get; set; } = new();
}

internal sealed class HumorCatalogDefaults
{
    public string HumorLevel { get; set; } = "lightHearted";

    public string Topic { get; set; } = "general";

    public string Channel { get; set; } = "teams";

    public string FallbackTopic { get; set; } = "general";

    public string FallbackHumorLevel { get; set; } = "lightHearted";

    public bool AllowAdultHumor { get; set; }

    public bool AllowSarcasm { get; set; } = true;

    public bool AllowInsults { get; set; }
}

internal sealed class HumorLevelEntry
{
    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool AllowSarcasm { get; set; }

    public bool AllowMildInsults { get; set; }

    public bool AllowProfanity { get; set; }

    public int MaxSpice { get; set; }
}

internal sealed class HumorTopicEntry
{
    public string DisplayName { get; set; } = string.Empty;

    public string[] Aliases { get; set; } = Array.Empty<string>();
}

internal sealed class HumorChannelEntry
{
    public List<HumorTemplateEntry> SubjectTemplates { get; set; } = new();

    public List<HumorTemplateEntry> OpeningTemplates { get; set; } = new();

    public List<HumorTemplateEntry> BodyLeadTemplates { get; set; } = new();

    public List<HumorTemplateEntry> ClosingTemplates { get; set; } = new();
}

internal sealed class HumorIssueEntry
{
    public string DisplayName { get; set; } = string.Empty;

    public string Severity { get; set; } = "info";

    public string DefaultTopic { get; set; } = "general";

    public string[] Tags { get; set; } = Array.Empty<string>();

    public List<HumorTemplateEntry> Messages { get; set; } = new();
}

internal sealed class HumorTemplateEntry
{
    public string Id { get; set; } = string.Empty;

    public string Template { get; set; } = string.Empty;

    public string[] HumorLevels { get; set; } = Array.Empty<string>();

    public string[] Topics { get; set; } = Array.Empty<string>();

    public string[] Channels { get; set; } = Array.Empty<string>();

    public int Spice { get; set; }

    public int Weight { get; set; } = 1;

    public bool Enabled { get; set; } = true;
}

internal sealed class HumorFallbackEntry
{
    public List<HumorTemplateEntry> NoTopicMatch { get; set; } = new();

    public List<HumorTemplateEntry> NoHumorLevelMatch { get; set; } = new();
}