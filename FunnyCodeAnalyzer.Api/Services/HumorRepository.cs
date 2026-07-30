using FunnyCodeAnalyzer.Api.Data;
using FunnyCodeAnalyzer.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FunnyCodeAnalyzer.Api.Services;

internal sealed class HumorRepository
{
    private const string IssueTrackerSource = "issue-tracker";

    private readonly HumorDbContext _db;

    public HumorRepository(HumorDbContext db)
    {
        _db = db;
    }

    public async Task SaveHumorRecordAsync(HumorRecord record)
    {
        _db.HumorRecords.Add(record);
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<HumorRecord>> GetByUserIdentifierAsync(string userIdentifier, int take)
    {
        var safeTake = Math.Clamp(take, 1, 200);
        var records = await _db.HumorRecords
            .AsNoTracking()
            .Where(record => record.UserIdentifier == userIdentifier)
            .Where(record => record.Source != IssueTrackerSource)
            .ToListAsync();

        return records
            .OrderByDescending(record => record.CreatedUtc)
            .Take(safeTake)
            .ToList();
    }

    public Task<int> GetIssueEncounterCountAsync(string userIdentifier, string issueKind)
    {
        var normalizedIssue = IssueKeyNormalizer.Normalize(issueKind);
        return _db.HumorRecords
            .AsNoTracking()
            .Where(record => record.UserIdentifier == userIdentifier && record.IssueTopic == normalizedIssue)
            .CountAsync();
    }

    public async Task<IReadOnlyList<UserIssueSummary>> GetIssueSummariesByUserIdentifierAsync(string userIdentifier, string? issueKind, int take)
    {
        var safeTake = Math.Clamp(take, 1, 500);
        var normalizedFilter = string.IsNullOrWhiteSpace(issueKind)
            ? null
            : IssueKeyNormalizer.Normalize(issueKind);

        var query = _db.HumorRecords
            .AsNoTracking()
            .Where(record => record.UserIdentifier == userIdentifier);

        if (!string.IsNullOrWhiteSpace(normalizedFilter))
        {
            query = query.Where(record => record.IssueTopic == normalizedFilter);
        }

        return await query
            .GroupBy(record => record.IssueTopic)
            .Select(group => new UserIssueSummary
            {
                IssueKind = group.Key,
                Count = group.Count(),
                FirstSeenUtc = group.Min(item => item.CreatedUtc),
                LastSeenUtc = group.Max(item => item.CreatedUtc)
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.IssueKind)
            .Take(safeTake)
            .ToListAsync();
    }

    public async Task<UserStatisticsResponse> GetUserStatisticsAsync(string userIdentifier, string? issueKind)
    {
        var normalizedFilter = string.IsNullOrWhiteSpace(issueKind)
            ? null
            : IssueKeyNormalizer.Normalize(issueKind);

        var query = _db.HumorRecords
            .AsNoTracking()
            .Where(record => record.UserIdentifier == userIdentifier);

        if (!string.IsNullOrWhiteSpace(normalizedFilter))
        {
            query = query.Where(record => record.IssueTopic == normalizedFilter);
        }

        var totalOccurrences = await query.CountAsync();
        var distinctIssueKinds = await query
            .Select(record => record.IssueTopic)
            .Distinct()
            .CountAsync();

        var issues = await GetIssueSummariesByUserIdentifierAsync(userIdentifier, normalizedFilter, 500);

        return new UserStatisticsResponse
        {
            UserIdentifier = userIdentifier,
            FilterIssueKind = normalizedFilter,
            TotalIssueOccurrences = totalOccurrences,
            DistinctIssueKinds = distinctIssueKinds,
            Issues = issues
        };
    }

    public async Task TrackIssueOccurrencesAsync(
        string userIdentifier,
        IEnumerable<string> issueKinds,
        string channel,
        string recipient,
        string humorMode)
    {
        var normalizedIssueKinds = issueKinds
            .Select(IssueKeyNormalizer.Normalize)
            .Where(kind => !string.IsNullOrWhiteSpace(kind))
            .ToArray();

        if (normalizedIssueKinds.Length == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var issueKind in normalizedIssueKinds)
        {
            _db.HumorRecords.Add(new HumorRecord
            {
                UserIdentifier = userIdentifier,
                IssueTopic = issueKind,
                HumorMode = string.IsNullOrWhiteSpace(humorMode) ? "light" : humorMode,
                HumorText = "Issue occurrence tracked for user statistics.",
                Source = IssueTrackerSource,
                Channel = string.IsNullOrWhiteSpace(channel) ? "analysis" : channel,
                Recipient = string.IsNullOrWhiteSpace(recipient) ? userIdentifier : recipient,
                CreatedUtc = now
            });
        }

        await _db.SaveChangesAsync();
    }
}