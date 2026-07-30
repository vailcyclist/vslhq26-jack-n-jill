using System.Text.Json;
using FunnyCodeAnalyzer.Api.Models;

namespace FunnyCodeAnalyzer.Api.Services;

internal sealed class UserProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath;

    public UserProfileStore(IHostEnvironment environment, IConfiguration configuration)
    {
        var configuredPath = configuration["UserProfiles:File"];
        var relativePath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine("Profiles", "user-profiles.json")
            : configuredPath.Trim();

        _filePath = Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.Combine(environment.ContentRootPath, relativePath);
    }

    public async Task<IReadOnlyList<UserProfile>> GetProfilesAsync()
    {
        var document = await LoadAsync();
        return document.Profiles
            .Select(NormalizeProfile)
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(profile => profile.UserIdentifier, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<UserProfile?> GetByIdentifierAsync(string userIdentifier)
    {
        var normalized = (userIdentifier ?? string.Empty).Trim();
        if (normalized.Length == 0)
        {
            return null;
        }

        var document = await LoadAsync();
        var match = document.Profiles.FirstOrDefault(profile =>
            profile.UserIdentifier.Equals(normalized, StringComparison.OrdinalIgnoreCase));

        return match is null ? null : NormalizeProfile(match);
    }

    public async Task<IReadOnlyList<UserProfile>> SearchAsync(string? userIdentifier, string? email, string? name)
    {
        var profiles = await GetProfilesAsync();

        return profiles
            .Where(profile => string.IsNullOrWhiteSpace(userIdentifier) || profile.UserIdentifier.Contains(userIdentifier, StringComparison.OrdinalIgnoreCase))
            .Where(profile => string.IsNullOrWhiteSpace(email) || profile.Email.Contains(email, StringComparison.OrdinalIgnoreCase))
            .Where(profile => string.IsNullOrWhiteSpace(name) || profile.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public async Task<UserProfile> UpsertAsync(UpsertUserProfileRequest request)
    {
        await _gate.WaitAsync();
        try
        {
            var document = await LoadUnlockedAsync();
            var normalizedIdentifier = request.UserIdentifier.Trim();

            var existing = document.Profiles.FirstOrDefault(profile =>
                profile.UserIdentifier.Equals(normalizedIdentifier, StringComparison.OrdinalIgnoreCase));

            var commonIssues = (request.CommonIssues ?? Array.Empty<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var preferredTopics = (request.PreferredTopics ?? Array.Empty<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var preferredHumorModes = (request.PreferredHumorModes ?? Array.Empty<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (existing is null)
            {
                existing = new UserProfile
                {
                    UserIdentifier = normalizedIdentifier
                };
                document.Profiles.Add(existing);
            }

            existing.Name = request.Name.Trim();
            existing.Email = request.Email.Trim();
            existing.PreferredTopics = preferredTopics.Length == 0
                ? new[] { "general" }
                : preferredTopics;
            existing.PreferredHumorModes = preferredHumorModes.Length == 0
                ? new[] { "lightHearted" }
                : preferredHumorModes;
            existing.PreferredTopic = null;
            existing.PreferredHumorMode = null;
            existing.CommonIssues = commonIssues;
            existing.UpdatedUtc = DateTimeOffset.UtcNow;

            await SaveUnlockedAsync(document);
            return NormalizeProfile(existing);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<UserProfileStoreDocument> LoadAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return await LoadUnlockedAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<UserProfileStoreDocument> LoadUnlockedAsync()
    {
        EnsureStoreFileExists();

        var json = await File.ReadAllTextAsync(_filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new UserProfileStoreDocument();
        }

        try
        {
            return JsonSerializer.Deserialize<UserProfileStoreDocument>(json, JsonOptions)
                ?? new UserProfileStoreDocument();
        }
        catch
        {
            return new UserProfileStoreDocument();
        }
    }

    private async Task SaveUnlockedAsync(UserProfileStoreDocument document)
    {
        EnsureStoreFileExists();
        var json = JsonSerializer.Serialize(document, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }

    private void EnsureStoreFileExists()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(_filePath))
        {
            File.WriteAllText(_filePath, JsonSerializer.Serialize(new UserProfileStoreDocument(), JsonOptions));
        }
    }

    private static UserProfile NormalizeProfile(UserProfile profile)
    {
        var preferredTopics = profile.PreferredTopics
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (preferredTopics.Count == 0 && !string.IsNullOrWhiteSpace(profile.PreferredTopic))
        {
            preferredTopics.Add(profile.PreferredTopic.Trim());
        }

        if (preferredTopics.Count == 0)
        {
            preferredTopics.Add("general");
        }

        var preferredHumorModes = profile.PreferredHumorModes
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (preferredHumorModes.Count == 0 && !string.IsNullOrWhiteSpace(profile.PreferredHumorMode))
        {
            preferredHumorModes.Add(profile.PreferredHumorMode.Trim());
        }

        if (preferredHumorModes.Count == 0)
        {
            preferredHumorModes.Add("lightHearted");
        }

        profile.PreferredTopics = preferredTopics;
        profile.PreferredHumorModes = preferredHumorModes;
        profile.PreferredTopic = null;
        profile.PreferredHumorMode = null;

        return profile;
    }
}
