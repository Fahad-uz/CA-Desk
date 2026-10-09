# CA Desk

A native, offline Windows desktop application for Chartered Accountants who manage clients, work areas and deadlines. Built with C#, WPF, .NET 10 and SQLite. Single user; no login, cloud service, subscription, browser or development server.

## Download and use

[Latest Windows release](https://github.com/Fahad-uz/CA-Desk/releases/latest) · [All releases](https://github.com/Fahad-uz/CA-Desk/releases)

1. Sign in to GitHub with access to this private repository.
2. Download `CA-Desk-v1.0.1-win-x64.zip` from the release's **Assets** section.
3. Extract **all** files into a folder and double-click `CA-Desk.exe`.
4. Choose **Add Client**, open the client's workspace and choose **Add Work Area**.

The portable package includes .NET and SQLite; end users need no developer tools, runtime installation or administrator access. Keep the extracted files together. The workspace starts empty.

## Features

- Dashboard with live summary cards, status donut chart, client progress and unfinished deadlines. Click cards to open the corresponding table filter.
- Client cards and compact table, partial name search, friendly duplicate-name validation, editing and confirmed deletion.
- Custom work areas with **Not Started**, **In Progress** and **Completed** statuses, optional editable/removable deadlines and multiline notes.
- Master Work Table across all clients, combined search/status/deadline/client filters, result count, sortable headers and inline status/date editing. Rows are virtualised.
- Month, week and day calendars, client filter, today highlighting, previous/next/today navigation and overflow drill-down. Entries open the same work editor.
- Automatic completed/total progress. Zero-work clients show “No work areas.” Overdue is a deadline condition; it never replaces the work status.
- Consistent forms, visible Minimize / Maximize / Restore / Close title-bar buttons, native Windows resizing and keyboard access.

`Ctrl+F` focuses page search; `Ctrl+N` adds a client; `Ctrl+R` refreshes. `Enter` saves an editor; `Escape` cancels. Deleting a client or work area requires confirmation.

## Windows support and data

Targets Windows 10/11 **64-bit**, with a WPF `win-x64` distribution. .NET 10's [current Windows support policy](https://learn.microsoft.com/dotnet/core/install/windows) applies; Microsoft lists supported Windows 10 LTSC/Enterprise releases. Windows 10 consumer editions have not been verified on physical devices.

Saved data lives in `%LOCALAPPDATA%\CA Desk\ca-desk.db`, separate from the application folder. SQLite uses foreign keys, transactions, parameterised statements, WAL and versioned schema initialization. Deadlines are ISO date-only values and do not shift with time zones. No personal data or production database is committed.

To update, close CA Desk and extract the next release into a new folder. Launch its executable; the user-profile database is retained. A database from a newer schema version is refused with a clear message rather than reset.

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

Core tests use isolated temporary SQLite databases. Windows checks open the actual WPF shell, save through editor commands, operate status/date controls, test connected views and filters, verify row virtualisation, reopen the app and render screenshots at explicit desktop/laptop sizes. They fail on WPF binding errors. See [test evidence](docs/testing.md) for executed results and limits.

Core tests and cross-compilation can also run on a non-Windows developer host with `EnableWindowsTargeting`; the application itself remains Windows only. WPF execution and packaging validation require Windows.

## Create a portable package

After a successful build and test run, run on Windows:

```powershell
./scripts/Publish.ps1 -Version 1.0.1
```

This publishes a self-contained x64 app, launches its executable against an isolated test database, creates `artifacts/CA-Desk-v1.0.1-win-x64.zip` and writes its SHA-256 checksum. No test database enters the distribution. There is no installer or code-signing certificate in this version.

## Automated builds and future releases

[GitHub Actions](https://github.com/Fahad-uz/CA-Desk/actions) builds and tests every main-branch push and pull request on `windows-latest`, then uploads a Windows package and test evidence.

For an update:

1. Make, test, commit and push the change.
2. Update `Directory.Build.props` to the semantic version and add `docs/releases/vX.Y.Z.md`.
3. Push the matching `vX.Y.Z` tag, or run **Publish Windows release** in Actions with that version.
4. The workflow builds, runs core/native tests, verifies the packaged executable and publishes a GitHub Release with the ZIP and checksum. Existing releases cannot be overwritten.

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

See [architecture](docs/architecture.md) for the data flow and [release notes](docs/releases/v1.0.1.md) for limitations. Automated Windows-runner testing does not substitute for physical Windows 10/11 acceptance or every display-scaling configuration.
