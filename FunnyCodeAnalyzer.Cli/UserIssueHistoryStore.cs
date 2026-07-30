using System.Text.Json;

namespace FunnyCodeAnalyzer;

internal sealed class UserIssueHistoryStore
{
    private readonly string _path;

    public UserIssueHistoryStore(string path)
    {
        _path = path;
    }

    public IReadOnlyDictionary<string, int> IncrementAndGetTotals(string userKey, IReadOnlyDictionary<string, int> issueIncrements)
    {
        var state = Load();

        if (!state.Users.TryGetValue(userKey, out var issueCounts))
        {
            issueCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            state.Users[userKey] = issueCounts;
        }

        foreach (var pair in issueIncrements)
        {
            issueCounts.TryGetValue(pair.Key, out var existing);
            issueCounts[pair.Key] = existing + Math.Max(0, pair.Value);
        }

        Save(state);
        return new Dictionary<string, int>(issueCounts, StringComparer.OrdinalIgnoreCase);
    }

    private UserIssueHistoryState Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new UserIssueHistoryState();
            }

            var json = File.ReadAllText(_path);
            var state = JsonSerializer.Deserialize<UserIssueHistoryState>(json, JsonOptions.Default);
            return state ?? new UserIssueHistoryState();
        }
        catch
        {
            return new UserIssueHistoryState();
        }
    }

    private void Save(UserIssueHistoryState state)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(state, JsonOptions.Default);
        File.WriteAllText(_path, json);
    }
}

internal sealed class UserIssueHistoryState
{
    public Dictionary<string, Dictionary<string, int>> Users { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
