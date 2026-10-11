# CA Desk

A native, offline Windows desktop application for Chartered Accountants who manage clients, work areas and deadlines. Built with C#, WPF, .NET 10 and SQLite. Single user; no login, cloud service, subscription, browser or development server.

## Download and use

[Latest Windows release](https://github.com/Fahad-uz/CA-Desk/releases/latest) · [All releases](https://github.com/Fahad-uz/CA-Desk/releases)

1. Sign in to GitHub with access to this private repository.
2. Download `CA-Desk-v1.1.2-win-x64.exe` from the release's **Assets** section.
3. Place the executable on your Desktop or in any folder and double-click it. The ZIP is also available with instructions and notices.
4. Choose **Add Client**, open the client's workspace and choose **Add Work Area**.

The portable package includes .NET and SQLite; end users need no developer tools, runtime installation or administrator access. The executable includes all required dependencies; copying just the EXE is supported. The workspace starts empty unless existing CA Desk data is found.

## Features

- Personal profile name in Settings, a “Welcome back, Name” greeting when opening the app and your name at the top of the left sidebar. Saved locally and included in backups.

- Dashboard with live summary cards, status donut chart, client progress and unfinished deadlines. Click cards to open the corresponding table filter.
- Optional client due dates, calendar days remaining, due-today and overdue indicators on cards, the compact table and client workspaces.
- Client cards and compact table, partial name search, friendly duplicate-name validation, editing and confirmed deletion.
- Custom work areas with **Not Started**, **In Progress** and **Completed** statuses, optional editable/removable deadlines and multiline notes.
- Master Work Table across all clients, combined search/status/deadline/client filters, result count, sortable headers and inline status/date editing. Rows are virtualised.
- Independent personal Study Notes: heading, multiline plain-text body, editable date, instant heading/body search, newest-date-first cards and confirmed deletion. The purple Notes navigation is on the right.
- Month, week and day calendars with purple study-note entries, Show Everything / Client Deadlines Only / Study Notes Only, client filter, today highlighting, previous/next/today navigation and overflow drill-down. Entries open the corresponding work or study-note editor. Personal notes stay visible when filtering by client.
- Automatic completed/total progress. Zero-work clients show “No work areas.” Overdue is a deadline condition; it never replaces the work status.
- Consistent forms, Minimize / Maximize / Restore / Close title-bar buttons visible from first launch, automatic sizing for the current usable screen area, native Windows resizing and keyboard access.

`Ctrl+F` focuses page search; `Ctrl+N` creates a note when Notes is active, otherwise adds a client; `Ctrl+R` refreshes. `Ctrl+S` saves an editor; `Enter` saves outside multiline text fields; `Escape` cancels. Deleting a client, work area or study note requires confirmation. The note editor warns before discarding unsaved changes.

## Windows support and data

Targets Windows 10/11 **64-bit**, with a WPF `win-x64` distribution. .NET 10's [current Windows support policy](https://learn.microsoft.com/dotnet/core/install/windows) applies; Microsoft lists supported Windows 10 LTSC/Enterprise releases. Windows 10 consumer editions have not been verified on physical devices.

Saved data lives in `%LOCALAPPDATA%\CADesk\Data\cadesk.db`, separate from the application folder. SQLite uses foreign keys, transactions, parameterised statements, WAL and versioned, transactional forward migrations. Deadlines are ISO date-only values and do not shift with time zones. No personal data or production database is committed.

To update, close CA Desk and replace the executable with the next release. Launch its executable; the user-profile database is retained. v1.1.0 automatically detects v1.0.x data at `%LOCALAPPDATA%\CA Desk\ca-desk.db`, verifies it and transfers it to the permanent path. The original file and verified recovery backups are retained. A database from a newer schema version is refused with a clear message rather than reset.

## Backup and recovery

Settings provides **Export Backup**, **Restore Backup** and **Open backups folder**. An export includes every client/work field, client due dates, personal study notes and profile name and saved calendar/client-view preferences. Choose a new `.db` filename. Restore checks the file first, asks before replacing current data, backs up current data, migrates an isolated candidate when needed, verifies the restored database and refreshes the workspace.

Automatic backups run at startup and local date changes, at most once daily, under `%LOCALAPPDATA%\CADesk\Backups`. The newest 14 daily copies are kept. Pre-migration, legacy-transfer and pre-restore recovery copies are retained separately. All copies use SQLite's backup API, including WAL writes. A failed migration rolls back and offers restoration of its verified pre-update backup; startup stops rather than opening partial or empty data.

Keep exported copies on another drive. Removing/replacing the portable application folder does not remove user data. There is no uninstaller. After upgrading, keep using the newer executable; the untouched legacy folder is a recovery source, not a second active workspace. A previous version may refuse a newer schema; use a compatible pre-upgrade backup for recovery rather than attempting an arbitrary downgrade.

## Build from source

Developers need the **.NET 10 SDK** on Windows. Visual Studio is optional; a current version with the .NET desktop workload can open `CaDesk.slnx`.

```powershell
dotnet restore CaDesk.slnx
dotnet build CaDesk.slnx -c Release
dotnet run --project src/CaDesk.Desktop
```

The SDK downloads development dependencies on restore. The resulting portable app runs offline.

## Test

```powershell
dotnet test tests/CaDesk.Tests -c Release
dotnet run --project tests/CaDesk.WindowsTests -c Release -- artifacts/windows-checks
```

Core tests use isolated temporary SQLite databases. The Windows workflow also downloads the published v1.0.1 package, launches its executable and uses its Core DLL to create 150 fictional clients and 3,000 work areas for full-field upgrade/restore checks. Run `./scripts/Verify-Upgrade.ps1` with repository access via the GitHub CLI to reproduce this gate. Windows checks open the actual WPF shell, save through editor commands, operate status/date controls, test connected views and filters, verify row virtualisation, reopen the app and render screenshots at explicit desktop/laptop sizes. They fail on WPF binding errors. See [test evidence](docs/testing.md) for executed results and limits.

Core tests and cross-compilation can also run on a non-Windows developer host with `EnableWindowsTargeting`; the application itself remains Windows only. WPF execution and packaging validation require Windows.

## Create a portable package

After a successful build and test run, run on Windows:

```powershell
./scripts/Publish.ps1 -Version 1.1.2
```

This publishes a self-contained single-file x64 app with embedded native libraries. It copies only the EXE into an isolated Desktop-style folder, launches it from a different working directory with a fresh extraction cache, and verifies WPF, SQLite, profile, backup and window controls. It creates `artifacts/CA-Desk-v1.1.2-win-x64.exe`, the optional ZIP and SHA-256 checksums. No test database enters the distribution. There is no installer or code-signing certificate in this version.

## Automated builds and future releases

[GitHub Actions](https://github.com/Fahad-uz/CA-Desk/actions) builds and tests every main-branch push and pull request on `windows-latest`, then uploads a Windows package and test evidence.

For an update:

1. Make, test, commit and push the change.
2. Update `Directory.Build.props` to the semantic version and add `docs/releases/vX.Y.Z.md`.
3. Push the matching `vX.Y.Z` tag, or run **Publish Windows release** in Actions with that version.
4. The workflow builds, runs core/native and previous-release upgrade tests, verifies the packaged executable and publishes a GitHub Release with the standalone EXE, optional ZIP and their checksums. Existing releases cannot be overwritten.

Add transactional schema migrations in `Repository` when data structure changes; preserve existing records. Never put real client information in source, test fixtures or release assets.

## Project structure

| Location | Purpose |
| --- | --- |
| `src/CaDesk.Core` | Domain records, SQLite repository, shared store and search/progress rules |
| `src/CaDesk.Desktop/ViewModels` | MVVM navigation, client/table/dashboard/calendar state and editor commands |
| `src/CaDesk.Desktop/Views` | WPF pages, modal editors and deletion confirmation |
| `src/CaDesk.Desktop/Themes` | Shared typography, colours, controls and tables |
| `src/CaDesk.Desktop/Controls` | Status converters, drawn donut and progress animation |
| `tests/CaDesk.Tests` | SQLite, persistence, filtering, dates and progress tests |
| `tests/CaDesk.WindowsTests` | Native WPF integration checks and rendered screenshots |
| `.github/workflows` | Windows builds and versioned GitHub Releases |
| `scripts/Publish.ps1` | Portable packaging and executable verification |
| `docs` | Architecture, usage, test evidence and release notes |

See [architecture](docs/architecture.md) for the data flow and [release notes](docs/releases/v1.1.2.md) for limitations. Automated Windows-runner testing does not substitute for physical Windows 10/11 acceptance or every display-scaling configuration.
