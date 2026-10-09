# Executed testing and scope

## Confirmed Windows checkpoint: 9 October 2026

[Windows workflow 37910326738](https://github.com/Fahad-uz/CA-Desk/actions/runs/37910326738) passed against application commit `c692f1c20228402b605e3f3cc184e3fb67ef0e6f`. The v1.1.0 release workflow repeats the checks against its release source and publishes only after they pass.

- Release solution build: zero warnings and zero errors.
- 46 xUnit core tests passed: original CRUD/filter/sort/progress coverage plus client due dates, note CRUD and independent progress, restart persistence, schemas 1 and 2 upgrades, failed-migration rollback/recovery, future/corrupt database refusal, WAL-aware export, complete restore, daily retention and serialized writes. A 100-client/2,000-work-area case remains covered.
- 83 native WPF checks passed: existing window controls, work editing/filtering/progress, native date/status controls and popups, plus due-date forms, note editor focus/shortcuts/validation/duplicate-save protection, empty-body and same-date notes, heading/body search, unsaved-close protection, note CRUD, date moves, calendar opening, all modes/event/client filters, purple notes and all work colours, backup Settings confirmation/validation, preferences and restart. No WPF binding errors occurred. Crowded month cells also keep both event types visible before overflow.
- 10 upgrade checks passed. The workflow downloaded the actual v1.0.1 release, successfully launched its executable, and used its published Core DLL to create 150 fictional clients and 3,000 work areas. It compared every field, ID and relationship after automatic folder detection/migration; checked dashboard progress, master table and calendar; verified recovery copies and complete backup restoration; and reopened the canonical database without importing old data again. The v1.0.1 library also refused the newer schema without changing it.
- The actual self-contained Windows executable launched, rendered WPF, saved/reopened SQLite, retained study notes and due dates, exported/restored a backup, updated calendar/progress and operated Minimize, Maximize, Restore and Close.
- Actual WPF content was rendered at 1920×1080, 1420×900 and 1000×680, including Notes, mixed calendars, client workspaces, backup Settings and editors. The master table scrolled through 1,505 work areas with row virtualization.

The release-verification artifact contains native/upgrade reports, screenshots, xUnit results, executable verification and the source Git bundle. Only fictional isolated databases are used; no production data enters source or release distributions.

## v1.1.1 release gates

The Windows workflows also exercise the editable profile field and Save name button, Unicode and whitespace handling, invalid-name protection, long-name layout at 1000×680, clearing a name, complete backup restoration and the personal greeting in a reopened window. Packaged verification copies only the published EXE into a separate folder with spaces, starts it from an unrelated working directory with a fresh extraction cache and exercises WPF, native SQLite, profile persistence, backups and all window controls. Publication requires these gates to pass. Reports are included in the release verification artifact.

## Test boundaries

The developer host is macOS; core SQLite tests and Windows cross-compilation run locally. Native execution uses a GitHub-hosted Windows Server runner. Physical Windows 10/11 machines, every DPI setting and an exhaustive accessibility audit have not been tested. Targeted OS compatibility and the test OS are distinct.

## Reproduce

```powershell
dotnet build CaDesk.slnx -c Release
dotnet test tests/CaDesk.Tests -c Release --no-build
./scripts/Verify-Upgrade.ps1 # GitHub CLI with access to the private repository
dotnet run --project tests/CaDesk.WindowsTests -c Release --no-build -- artifacts/windows-checks
./scripts/Publish.ps1 -Version 1.1.1
```

All checks return a nonzero exit code on failure. The release workflow stops before publication if any upgrade, core, native or packaged-executable check fails.
