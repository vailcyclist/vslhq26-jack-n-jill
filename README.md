# Static Punalyzer

A .NET static code analysis tool that scans local directories and source control repositories, detects common coding issues, and sends personalized pun-powered notifications through Microsoft Teams or email.

## Team

- **Team name (or "Solo"):** Solo
- **Members:**
  - @vailcyclist

## Category

- **Primary:** .NET business app
- **Secondary (optional):** Creative application

## What it does

Static Punalyzer analyzes a local directory or working repository using static code analysis to detect code quality issues such as TODO comments, commented-out code, monster classes, long methods, empty catch blocks, large files, and issue suppressions.

When issues are found, Static Punalyzer calls an API that generates puns based on the detected issue type, selected humor mode, and optional topic preferences. If the scanned directory is a Git or Subversion working repository, the console application attempts to identify the responsible user from commit history and sends a personalized Microsoft Teams message or email with the findings and generated puns.

The API also tracks issue tallies per user and provides metrics so teams can monitor recurring code quality patterns over time.

## Architecture

The solution consists of three primary components:

1. **Console Application**
   - Scans a local directory or source control working copy.
   - Detects whether the directory is Git or Subversion.
   - Maps detected issues to users based on commit metadata when available.
   - Calls the API to retrieve generated puns.
   - Sends notifications through Microsoft Teams or email using stored user profiles.

2. **Static Analysis Analyzer**
   - Performs code analysis against files in the target directory.
   - Detects supported issue types:
     - `TodoComment`
     - `CommentedOutCode`
     - `MonsterClass`
     - `LongMethod`
     - `EmptyCatch`
     - `LargeFile`
     - `IssueSuppression`

3. **API**
   - Generates puns based on issue type, humor mode, and optional topics.
   - Supports multiple humor modes:
     - `average`
     - `dirty`
     - `kidFriendly`
     - `lightHearted`
     - `sarcastic`
     - `spicy`
   - Stores user profiles, including email address, Teams username, and unique user token.
   - Tracks issue counts per user and exposes metrics.

[Local Directory or Repo]
          |
          v
[Console Application]
          |
          v
[Detect Git or SVN Working Copy]
          |
          +--> If Git/SVN:
          |       - Read commit metadata
          |       - Attempt to map detected issues to users
          |
          +--> If not Git/SVN:
                  - Analyze files without commit attribution

[Console Application]
          |
          v
[Static Analysis Analyzer]
          |
          v
[Detected Issues]
          |
          v
[API]
          |
          +--> [Pun Catalog]
          |       - Current implementation: JSON configuration file
          |       - Future implementation: LLM-based pun generation
          |
          +--> [User Profiles]
          |       - Email address
          |       - Teams username
          |       - User token or unique identifier
          |
          +--> [Metrics]
                  - Issue tallies per user
                  - Issue counts by type

[API]
          |
          v
[Console Application]
          |
          +--> [Prepared Microsoft Teams Notification]
          |
          +--> [Prepared Email Notification]


## Tech stack

- Languages:
    - C#
    - JSON (catalog/profile/config files)

- Frameworks/libraries:
    - .NET SDK (solution includes net8.0 CLI/static-analysis and net9.0 API)
    - ASP.NET Core Minimal API
    - Entity Framework Core with SQLite
    - Swashbuckle (Swagger/OpenAPI)

- AI models/services:
    - No LLM dependency in the current implementation
    - Humor generation is catalog/rules based
    - Optional external delivery services: SMTP server and Microsoft Graph (Teams)

- Hosting:
    - Local development: Kestrel (dotnet run)
    - Data store: local SQLite file

## Getting started

### Prerequisites

- .NET 9 SDK installed
    - Needed to build/run the API target (net9.0)
    - Also builds/runs net8.0 projects in this repo
- Git installed (required if you will clone and use git-based attribution)
- Optional: SVN client installed if you use svn source-control mode
- Optional accounts/integrations:
    - SMTP account for email sending
    - Microsoft Entra app + Graph permissions for Teams sending

### Setup

# Clone the repo
git clone https://github.com/<owner>/<repo>.git
cd <repo>

# Restore and build
dotnet restore [FunnyCodeAnalyzer.sln](http://_vscodecontentref_/0)
dotnet build [FunnyCodeAnalyzer.sln](http://_vscodecontentref_/1)

# Terminal 1: run the API
dotnet run --project [FunnyCodeAnalyzer.Api.csproj](http://_vscodecontentref_/2)

# Terminal 2: run the CLI (interactive details generation, no auto-send)
dotnet run --project [FunnyCodeAnalyzer.Cli.csproj](http://_vscodecontentref_/3) -- --repo [demo-repo](http://_vscodecontentref_/4) --source-control directory --channel both --self-user dev-a-token --run-mode interactive --humor-api-config [humor-api.client.json](http://_vscodecontentref_/5)

### Configuration

Core config files:

- API settings: appsettings.json
- User profiles: user-profiles.json
- CLI to API endpoint map: humor-api.client.json
- CLI defaults: run-funny-code-analyzer.defaults.json

Important API config keys:

- ConnectionStrings:DefaultConnection (SQLite connection string)
- Smtp:Host, Smtp:Port, Smtp:EnableSsl, Smtp:Username, Smtp:Password, Smtp:DefaultFromEmail, Smtp:DefaultFromName
- Teams:GraphBaseUrl
- HumorCatalog:File
- UserProfiles:File

## Demo (required)

- Video file in this repo (preferred): `./demo/static-punalyzer.demo.mp4`

## Known limitations

- Microsoft Teams integration is not yet fully functional and requires additional configuration.
- Email integration is not yet fully functional and requires additional configuration.
- The pun catalog is currently implemented as a configuration JSON file.
- LLM-based pun generation is planned for a future version of the API.
- Repository attribution depends on available Git or Subversion commit metadata.
- User notifications require matching commit identities to configured user profiles.
- Static analysis rules may produce false positives or miss certain language-specific patterns.

## License

MIT
