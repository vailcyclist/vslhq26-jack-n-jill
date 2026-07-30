using System.Text.Json.Serialization;

namespace FunnyCodeAnalyzer.Api.Models;

internal sealed class UserProfile
{
    public string UserIdentifier { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public IReadOnlyList<string> PreferredTopics { get; set; } = new[] { "general" };

    public IReadOnlyList<string> PreferredHumorModes { get; set; } = new[] { "lightHearted" };

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreferredTopic { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreferredHumorMode { get; set; }

    public IReadOnlyList<string> CommonIssues { get; set; } = Array.Empty<string>();

    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class UpsertUserProfileRequest
{
    public string UserIdentifier { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public IReadOnlyList<string>? PreferredTopics { get; set; }

    public IReadOnlyList<string>? PreferredHumorModes { get; set; }

    public IReadOnlyList<string>? CommonIssues { get; set; }
}

internal sealed class UserProfileStoreDocument
{
    public List<UserProfile> Profiles { get; set; } = new();
}
