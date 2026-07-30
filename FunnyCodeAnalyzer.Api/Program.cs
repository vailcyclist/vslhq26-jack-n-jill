using FunnyCodeAnalyzer.Api.Data;
using FunnyCodeAnalyzer.Api.Models;
using FunnyCodeAnalyzer.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var catalogFileSetting = builder.Configuration["HumorCatalog:File"];
var catalogFileName = string.IsNullOrWhiteSpace(catalogFileSetting)
    ? "humor-config-issuekind-complete.json"
    : catalogFileSetting.Trim();

var catalogsDirectory = Path.Combine(builder.Environment.ContentRootPath, "Catalogs");
var catalogPath = Path.IsPathRooted(catalogFileName)
    ? catalogFileName
    : Path.Combine(catalogsDirectory, catalogFileName);

if (!File.Exists(catalogPath))
{
    var availableCatalogs = Directory.Exists(catalogsDirectory)
        ? string.Join(", ", Directory.EnumerateFiles(catalogsDirectory, "*.json").Select(Path.GetFileName).OrderBy(name => name))
        : "(Catalogs directory not found)";

    throw new FileNotFoundException(
        $"Humor catalog file not found: {catalogPath}. Available catalogs: {availableCatalogs}",
        catalogPath);
}

builder.Configuration.AddJsonFile(catalogPath, optional: false, reloadOnChange: true);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<TeamsOptions>(builder.Configuration.GetSection("Teams"));
builder.Services.Configure<HumorCatalogOptions>(builder.Configuration);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=funny-humor.db";

builder.Services.AddDbContext<HumorDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddScoped<HumorRepository>();
builder.Services.AddScoped<HumorEngine>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<ReportMessageComposer>();
builder.Services.AddSingleton<UserProfileStore>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HumorDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/api/humor/history/{userIdentifier}", async (string userIdentifier, HumorRepository repository, int take = 25) =>
{
    if (string.IsNullOrWhiteSpace(userIdentifier))
    {
        return Results.BadRequest("userIdentifier is required.");
    }

    var items = await repository.GetByUserIdentifierAsync(userIdentifier.Trim(), take);
    return Results.Ok(items);
});

app.MapGet("/api/users/profiles", async (
    UserProfileStore store,
    string? userIdentifier,
    string? email,
    string? name) =>
{
    var profiles = await store.SearchAsync(userIdentifier, email, name);
    return Results.Ok(profiles);
});

app.MapGet("/api/users/profiles/{userIdentifier}", async (string userIdentifier, UserProfileStore store) =>
{
    if (string.IsNullOrWhiteSpace(userIdentifier))
    {
        return Results.BadRequest("userIdentifier is required.");
    }

    var profile = await store.GetByIdentifierAsync(userIdentifier.Trim());
    return profile is null ? Results.NotFound() : Results.Ok(profile);
});

app.MapPost("/api/users/profiles", async (UpsertUserProfileRequest request, UserProfileStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.UserIdentifier))
    {
        return Results.BadRequest("userIdentifier is required.");
    }

    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest("name is required.");
    }

    if (string.IsNullOrWhiteSpace(request.Email))
    {
        return Results.BadRequest("email is required.");
    }

    var saved = await store.UpsertAsync(request);
    return Results.Ok(saved);
});

app.MapGet("/api/users/{userIdentifier}/issues", async (
    string userIdentifier,
    HumorRepository repository,
    string? issueKind,
    int take = 100) =>
{
    if (string.IsNullOrWhiteSpace(userIdentifier))
    {
        return Results.BadRequest("userIdentifier is required.");
    }

    var issues = await repository.GetIssueSummariesByUserIdentifierAsync(userIdentifier.Trim(), issueKind, take);
    return Results.Ok(issues);
});

app.MapGet("/api/users/{userIdentifier}/statistics", async (
    string userIdentifier,
    HumorRepository repository,
    string? issueKind) =>
{
    if (string.IsNullOrWhiteSpace(userIdentifier))
    {
        return Results.BadRequest("userIdentifier is required.");
    }

    var stats = await repository.GetUserStatisticsAsync(userIdentifier.Trim(), issueKind);
    return Results.Ok(stats);
});

app.MapGet("/api/humor/modes", (IOptions<HumorCatalogOptions> catalogOptions) =>
{
    var modes = catalogOptions.Value.HumorLevels
        .Keys
        .OrderBy(mode => mode, StringComparer.OrdinalIgnoreCase)
        .Select(mode => new { name = mode })
        .ToArray();

    return Results.Ok(modes);
});

app.MapGet("/api/humor/issue-types", (IOptions<HumorCatalogOptions> catalogOptions) =>
{
    var issueTypes = catalogOptions.Value.Issues
        .Keys
        .OrderBy(topic => topic, StringComparer.OrdinalIgnoreCase)
        .Select(topic => new { name = topic })
        .ToArray();

    return Results.Ok(issueTypes);
});

app.MapGet("/api/humor/policy", (
    string issueTopic,
    string? humorMode,
    string? topic,
    string? channel,
    HumorEngine humorEngine) =>
{
    if (string.IsNullOrWhiteSpace(issueTopic))
    {
        return Results.BadRequest("issueTopic is required.");
    }

    var resolvedMode = string.IsNullOrWhiteSpace(humorMode)
        ? "lightHearted"
        : humorMode;

    var policy = humorEngine.ResolvePolicy(issueTopic, resolvedMode, topic, channel);
    return Results.Ok(policy);
});

app.MapGet("/api/humor/generate", async (
    string userIdentifier,
    string issueTopic,
    string? humorMode,
    string? topic,
    HumorEngine humorEngine,
    HumorRepository repository,
    UserProfileStore profileStore) =>
{
    if (string.IsNullOrWhiteSpace(userIdentifier))
    {
        return Results.BadRequest("userIdentifier is required.");
    }

    if (string.IsNullOrWhiteSpace(issueTopic))
    {
        return Results.BadRequest("issueTopic is required.");
    }

    userIdentifier = userIdentifier.Trim();
    var profile = await profileStore.GetByIdentifierAsync(userIdentifier);
    var normalizedIssueKind = IssueKeyNormalizer.Normalize(issueTopic);
    var priorCount = await repository.GetIssueEncounterCountAsync(userIdentifier, normalizedIssueKind);
    var encounterCount = priorCount + 1;

    var resolvedMode = string.IsNullOrWhiteSpace(humorMode)
        ? ChooseProfilePreference(profile?.PreferredHumorModes, "lightHearted", encounterCount)
        : humorMode;
    var resolvedTopic = string.IsNullOrWhiteSpace(topic)
        ? ChooseProfilePreference(profile?.PreferredTopics, "general", encounterCount)
        : topic;

    var response = humorEngine.Generate(issueTopic, resolvedMode, resolvedTopic);
    response.IssueEncounterCount = encounterCount;
    response.Text = HumorPersistenceText.Apply(response.Text, encounterCount);

    await repository.SaveHumorRecordAsync(new HumorRecord
    {
        UserIdentifier = userIdentifier,
        IssueTopic = response.IssueTopic,
        HumorMode = response.HumorMode,
        HumorText = response.Text,
        Source = "api-local",
        Channel = "humor",
        Recipient = userIdentifier,
        CreatedUtc = DateTimeOffset.UtcNow
    });

    return Results.Ok(response);
});

app.MapPost("/api/humor/generate", async (
    HumorRequest request,
    HumorEngine humorEngine,
    HumorRepository repository,
    UserProfileStore profileStore) =>
{
    if (string.IsNullOrWhiteSpace(request.UserIdentifier))
    {
        return Results.BadRequest("userIdentifier is required.");
    }

    if (string.IsNullOrWhiteSpace(request.IssueTopic))
    {
        return Results.BadRequest("issueTopic is required.");
    }

    request.UserIdentifier = request.UserIdentifier.Trim();
    var profile = await profileStore.GetByIdentifierAsync(request.UserIdentifier);
    var normalizedIssueKind = IssueKeyNormalizer.Normalize(request.IssueTopic);
    var priorCount = await repository.GetIssueEncounterCountAsync(request.UserIdentifier, normalizedIssueKind);
    var encounterCount = priorCount + 1;

    var resolvedMode = string.IsNullOrWhiteSpace(request.HumorMode)
        ? ChooseProfilePreference(profile?.PreferredHumorModes, "lightHearted", encounterCount)
        : request.HumorMode;
    var resolvedTopic = string.IsNullOrWhiteSpace(request.Topic)
        ? ChooseProfilePreference(profile?.PreferredTopics, "general", encounterCount)
        : request.Topic;

    var response = humorEngine.Generate(request.IssueTopic, resolvedMode, resolvedTopic);
    response.IssueEncounterCount = encounterCount;
    response.Text = HumorPersistenceText.Apply(response.Text, encounterCount);

    await repository.SaveHumorRecordAsync(new HumorRecord
    {
        UserIdentifier = request.UserIdentifier,
        IssueTopic = response.IssueTopic,
        HumorMode = response.HumorMode,
        HumorText = response.Text,
        Source = "api-local",
        Channel = "humor",
        Recipient = request.UserIdentifier,
        CreatedUtc = DateTimeOffset.UtcNow
    });

    return Results.Ok(response);
});

app.MapPost("/api/notifications/email/details", async (
    NotificationDetailsRequest request,
    HumorEngine humorEngine,
    ReportMessageComposer composer,
    UserProfileStore profileStore) =>
{
    if (string.IsNullOrWhiteSpace(request.UserIdentifier))
    {
        return Results.BadRequest("userIdentifier is required.");
    }

    var userIdentifier = request.UserIdentifier.Trim();
    var profile = await profileStore.GetByIdentifierAsync(userIdentifier);
    var issueSeed = (request.Issues?.Count ?? 0) + 1;
    var effectiveHumorMode = ChooseProfilePreference(profile?.PreferredHumorModes, "lightHearted", issueSeed);
    var effectiveHumorTopic = ChooseProfilePreference(profile?.PreferredTopics, "general", issueSeed);

    var dispatchRequest = BuildDispatchRequest(request, userIdentifier, profile?.Name, effectiveHumorMode, effectiveHumorTopic, "email");
    var body = composer.BuildBaseMessage(dispatchRequest, humorEngine);

    var response = new EmailMessageDetailsResponse
    {
        UserIdentifier = userIdentifier,
        RecipientName = dispatchRequest.RecipientName,
        IssueCount = dispatchRequest.Report.IssueCount,
        Subject = $"Funny Code Analyzer: {dispatchRequest.Report.IssueCount} issue(s)",
        Body = body,
        HumorAddon = null
    };

    return Results.Ok(response);
});

app.MapPost("/api/notifications/teams/details", async (
    NotificationDetailsRequest request,
    HumorEngine humorEngine,
    ReportMessageComposer composer,
    UserProfileStore profileStore) =>
{
    if (string.IsNullOrWhiteSpace(request.UserIdentifier))
    {
        return Results.BadRequest("userIdentifier is required.");
    }

    var userIdentifier = request.UserIdentifier.Trim();
    var profile = await profileStore.GetByIdentifierAsync(userIdentifier);
    var issueSeed = (request.Issues?.Count ?? 0) + 1;
    var effectiveHumorMode = ChooseProfilePreference(profile?.PreferredHumorModes, "lightHearted", issueSeed);
    var effectiveHumorTopic = ChooseProfilePreference(profile?.PreferredTopics, "general", issueSeed);

    var dispatchRequest = BuildDispatchRequest(request, userIdentifier, profile?.Name, effectiveHumorMode, effectiveHumorTopic, "teams");
    var message = composer.BuildBaseMessage(dispatchRequest, humorEngine);

    var response = new TeamsMessageDetailsResponse
    {
        UserIdentifier = userIdentifier,
        RecipientName = dispatchRequest.RecipientName,
        IssueCount = dispatchRequest.Report.IssueCount,
        Title = $"Funny Code Analyzer Report ({dispatchRequest.Report.IssueCount} issue(s))",
        Message = message,
        HumorAddon = null
    };

    return Results.Ok(response);
});

app.MapPost("/api/notifications/email", async (EmailNotificationRequest request, HumorEngine humorEngine, NotificationService notifications, HumorRepository repository) =>
{
    if (string.IsNullOrWhiteSpace(request.UserIdentifier))
    {
        return Results.BadRequest("userIdentifier is required.");
    }

    if (string.IsNullOrWhiteSpace(request.ToEmail))
    {
        return Results.BadRequest("toEmail is required.");
    }

    var humor = request.IncludeHumor
        ? humorEngine.Generate(request.IssueTopic ?? "general", request.HumorMode)
        : null;

    var body = request.Body;
    if (humor is not null)
    {
        body = string.Join(Environment.NewLine, new[]
        {
            request.Body,
            string.Empty,
            "Humor addon:",
            humor.Text
        });

        await repository.SaveHumorRecordAsync(new HumorRecord
        {
            UserIdentifier = request.UserIdentifier,
            IssueTopic = request.IssueTopic ?? "general",
            HumorMode = request.HumorMode,
            HumorText = humor.Text,
            Source = "api-local",
            Channel = "email",
            Recipient = request.ToEmail,
            CreatedUtc = DateTimeOffset.UtcNow
        });
    }

    var message = new OutgoingEmailMessage
    {
        UserIdentifier = request.UserIdentifier,
        ToEmail = request.ToEmail,
        Cc = request.Cc,
        Bcc = request.Bcc,
        Subject = request.Subject,
        Body = body,
        IsHtml = request.IsHtml,
        FromEmail = request.FromEmail,
        FromDisplayName = request.FromDisplayName
    };

    await notifications.SendEmailAsync(message);

    return Results.Ok(new
    {
        status = "sent",
        request.ToEmail,
        humor = humor?.Text
    });
});

app.MapPost("/api/notifications/teams", async (TeamsNotificationRequest request, HumorEngine humorEngine, NotificationService notifications, HumorRepository repository) =>
{
    if (string.IsNullOrWhiteSpace(request.UserIdentifier))
    {
        return Results.BadRequest("userIdentifier is required.");
    }

    if (string.IsNullOrWhiteSpace(request.GraphAccessToken))
    {
        return Results.BadRequest("graphAccessToken is required.");
    }

    if (string.IsNullOrWhiteSpace(request.TeamsChatId) &&
        (string.IsNullOrWhiteSpace(request.RecipientUserId) || string.IsNullOrWhiteSpace(request.SenderUserId)))
    {
        return Results.BadRequest("Provide teamsChatId or both recipientUserId and senderUserId.");
    }

    var humor = request.IncludeHumor
        ? humorEngine.Generate(request.IssueTopic ?? "general", request.HumorMode)
        : null;

    var finalMessage = humor is null
        ? request.Message
        : string.Join(Environment.NewLine, new[] { request.Message, string.Empty, humor.Text });

    await notifications.SendTeamsMessageAsync(new OutgoingTeamsMessage
    {
        GraphAccessToken = request.GraphAccessToken,
        TeamsChatId = request.TeamsChatId,
        RecipientUserId = request.RecipientUserId,
        SenderUserId = request.SenderUserId,
        Message = finalMessage
    });

    if (humor is not null)
    {
        await repository.SaveHumorRecordAsync(new HumorRecord
        {
            UserIdentifier = request.UserIdentifier,
            IssueTopic = request.IssueTopic ?? "general",
            HumorMode = request.HumorMode,
            HumorText = humor.Text,
            Source = "api-local",
            Channel = "teams",
            Recipient = request.RecipientUserId ?? request.TeamsChatId ?? "unknown",
            CreatedUtc = DateTimeOffset.UtcNow
        });
    }

    return Results.Ok(new
    {
        status = "sent",
        recipient = request.RecipientUserId ?? request.TeamsChatId,
        humor = humor?.Text
    });
});

app.MapPost("/api/notifications/dispatch-report", async (
    ReportDispatchRequest request,
    HumorEngine humorEngine,
    ReportMessageComposer composer,
    NotificationService notifications,
    HumorRepository repository,
    UserProfileStore profileStore) =>
{
    if (string.IsNullOrWhiteSpace(request.UserIdentifier))
    {
        return Results.BadRequest("userIdentifier is required.");
    }

    if (request.Report is null)
    {
        return Results.BadRequest("report is required.");
    }

    var channel = request.Channel.Trim().ToLowerInvariant();
    if (channel is not ("email" or "teams" or "both"))
    {
        return Results.BadRequest("channel must be email, teams, or both.");
    }

    request.UserIdentifier = request.UserIdentifier.Trim();
    var profile = await profileStore.GetByIdentifierAsync(request.UserIdentifier);
    if (string.IsNullOrWhiteSpace(request.RecipientName))
    {
        request.RecipientName = ResolveRecipientName(request, profile?.Name);
    }

    var effectiveHumorMode = string.IsNullOrWhiteSpace(request.HumorMode)
        ? ChooseProfilePreference(profile?.PreferredHumorModes, "lightHearted", request.Report.IssueCount + 1)
        : request.HumorMode;
    request.HumorMode = effectiveHumorMode;

    var effectiveHumorTopic = string.IsNullOrWhiteSpace(request.HumorTopic)
        ? ChooseProfilePreference(profile?.PreferredTopics, "general", request.Report.IssueCount + 1)
        : request.HumorTopic;
    request.HumorTopic = effectiveHumorTopic;

    await repository.TrackIssueOccurrencesAsync(
        request.UserIdentifier,
        request.Report.Issues.Select(issue => issue.Kind),
        channel,
        request.RecipientName,
        request.HumorMode);

    var defaultBaseMessage = composer.BuildBaseMessage(request, humorEngine);
    var emailBaseBody = request.Email?.Body;
    var teamsBaseMessage = request.Teams?.Message;

    var finalEmailBody = string.IsNullOrWhiteSpace(emailBaseBody) ? defaultBaseMessage : emailBaseBody;
    var finalTeamsBody = string.IsNullOrWhiteSpace(teamsBaseMessage) ? defaultBaseMessage : teamsBaseMessage;

    if (channel is "email" or "both")
    {
        if (request.Email is null || string.IsNullOrWhiteSpace(request.Email.ToEmail))
        {
            return Results.BadRequest("email.toEmail is required for email or both channels.");
        }

        await notifications.SendEmailAsync(new OutgoingEmailMessage
        {
            UserIdentifier = request.UserIdentifier,
            ToEmail = request.Email.ToEmail,
            Cc = request.Email.Cc,
            Bcc = request.Email.Bcc,
            Subject = request.Email.Subject,
            Body = finalEmailBody,
            IsHtml = request.Email.IsHtml,
            FromEmail = request.Email.FromEmail,
            FromDisplayName = request.Email.FromDisplayName
        });
    }

    if (channel is "teams" or "both")
    {
        if (request.Teams is null || string.IsNullOrWhiteSpace(request.Teams.GraphAccessToken))
        {
            return Results.BadRequest("teams.graphAccessToken is required for teams or both channels.");
        }

        if (string.IsNullOrWhiteSpace(request.Teams.TeamsChatId) &&
            (string.IsNullOrWhiteSpace(request.Teams.RecipientUserId) || string.IsNullOrWhiteSpace(request.Teams.SenderUserId)))
        {
            return Results.BadRequest("Provide teams.teamsChatId or both teams.recipientUserId and teams.senderUserId.");
        }

        await notifications.SendTeamsMessageAsync(new OutgoingTeamsMessage
        {
            GraphAccessToken = request.Teams.GraphAccessToken,
            TeamsChatId = request.Teams.TeamsChatId,
            RecipientUserId = request.Teams.RecipientUserId,
            SenderUserId = request.Teams.SenderUserId,
            Message = finalTeamsBody
        });
    }

    if (request.IncludeHumor && request.Report.Issues.Count > 0)
    {
        var firstIssue = request.Report.Issues[0];
        var humor = humorEngine.Generate(firstIssue.Kind, request.HumorMode, request.HumorTopic);

        await repository.SaveHumorRecordAsync(new HumorRecord
        {
            UserIdentifier = request.UserIdentifier,
            IssueTopic = firstIssue.Kind,
            HumorMode = request.HumorMode,
            HumorText = humor.Text,
            Source = "api-local",
            Channel = channel,
            Recipient = request.RecipientName,
            CreatedUtc = DateTimeOffset.UtcNow
        });
    }

    return Results.Ok(new
    {
        status = "sent",
        channel,
        issueCount = request.Report.IssueCount,
        includeHumor = request.IncludeHumor
    });
});

app.Run();

static string ChooseProfilePreference(IReadOnlyList<string>? values, string fallback, int seed)
{
    var candidates = (values ?? Array.Empty<string>())
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (candidates.Length == 0)
    {
        return fallback;
    }

    var index = Math.Abs(seed - 1) % candidates.Length;
    return candidates[index];
}

static ReportDispatchRequest BuildDispatchRequest(
    NotificationDetailsRequest request,
    string userIdentifier,
    string? profileName,
    string humorMode,
    string humorTopic,
    string channel)
{
    var issues = request.Issues ?? Array.Empty<ReportIssueSnapshot>();
    var workingFileCount = issues
        .Select(issue => issue.FilePath)
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    var recipientName = !string.IsNullOrWhiteSpace(profileName)
        ? profileName
        : ResolveDominantIssueUserIdentifier(issues, userIdentifier);

    return new ReportDispatchRequest
    {
        UserIdentifier = userIdentifier,
        Channel = channel,
        RecipientName = recipientName,
        HumorMode = humorMode,
        HumorTopic = humorTopic,
        IncludeHumor = true,
        Report = new ReportSnapshot
        {
            RepositoryPath = string.IsNullOrWhiteSpace(request.RepositoryPath) ? "(unknown)" : request.RepositoryPath,
            SourceControl = string.IsNullOrWhiteSpace(request.SourceControl) ? "directory" : request.SourceControl,
            WorkingFileCount = workingFileCount,
            IssueCount = issues.Count,
            Issues = issues
        }
    };
}

static string ResolveRecipientName(ReportDispatchRequest request, string? profileName)
{
    if (!string.IsNullOrWhiteSpace(request.RecipientName))
    {
        return request.RecipientName.Trim();
    }

    if (!string.IsNullOrWhiteSpace(profileName))
    {
        return profileName;
    }

    return ResolveDominantIssueUserIdentifier(request.Report.Issues, request.UserIdentifier);
}

static string ResolveDominantIssueUserIdentifier(IEnumerable<ReportIssueSnapshot> issues, string fallback)
{
    var dominant = issues
        .Select(issue => issue.UserIdentifier)
        .Where(identifier => !string.IsNullOrWhiteSpace(identifier))
        .GroupBy(identifier => identifier.Trim(), StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(group => group.Count())
        .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.Key)
        .FirstOrDefault();

    if (!string.IsNullOrWhiteSpace(dominant))
    {
        return dominant;
    }

    return string.IsNullOrWhiteSpace(fallback) ? "there" : fallback.Trim();
}