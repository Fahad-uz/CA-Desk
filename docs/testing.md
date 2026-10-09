# Executed testing and scope

## Confirmed checkpoint: 9 October 2026

[Windows workflow run 37892612108](https://github.com/Fahad-uz/CA-Desk/actions/runs/37892612108) passed the native and packaged-executable checks for application commit `e53453fc380a56d0a088885bf536a04a01b0207c`. The v1.0.1 release workflow repeats these checks against its tagged source.

- Release solution build: zero warnings and zero errors.
- 31 xUnit core tests passed. Includes creation, rename, deletion and cascading foreign keys, duplicate names, blank input, all three statuses, deadline add/edit/removal, multiline and long notes, restart persistence, parameterised SQL, all deadline filters, combined filters, sorting, progress and a 100-client/2,000-work-area dataset.
- 43 native WPF checks passed. Includes visible caption controls; Minimize, Maximize, Restore and Close actions; the actual empty dashboard, editor commands, client search control, inline status dropdown/date picker and native popups, invalid typed-date validation and correction, synchronized counts and progress, combined filters and preservation, visible reset selections, all header sorts, month overflow/day drill-down, entry opening, calendar edits/client filter/view memory, cancellation and confirmation of deletes, reopening the WPF app and no binding errors.
- Actual WPF content was rendered at 1920×1080, 1420×900 and 1000×680, plus modal editor sizes. Dashboard, clients, master table, settings, calendar modes and forms were visually reviewed. The native table scrolled through 1,505 work areas with row virtualisation enabled.
- The self-contained Windows x64 executable was independently launched from its published folder. It rendered WPF, wrote and reopened an isolated SQLite database, updated dashboard progress and operated its Minimize, Maximize, Restore and Close buttons successfully. The portable ZIP and checksum were produced after that verification.

The first native run found a read-only progress binding error that was not detectable by compilation. It was corrected, and subsequent runs passed. Screenshot review also led to wider table controls and a visible All Statuses selection after reset; the final native checks include those fixes.

The first screenshot run was limited by the hosted desktop resolution. Final checks render the actual WPF content at explicit sizes independent of that limit. Final release evidence is uploaded as the release-verification artifact, including screenshots, native results, xUnit results and packaged executable verification.

## Test boundaries

The developer host is macOS. Core tests run there against actual SQLite; WPF source is cross-compiled there. Native UI execution is on a GitHub-hosted Windows Server runner. No physical Windows 10 or Windows 11 device test, exhaustive accessibility audit or every DPI setting is claimed. Targeted OS compatibility and actual test OS are distinct.

Test clients are fictional and generated in isolated temporary directories. Production databases are excluded from source control and packages. No real client/accounting information was used.

## Reproduce

```powershell
dotnet build CaDesk.slnx -c Release
dotnet test tests/CaDesk.Tests -c Release --no-build
dotnet run --project tests/CaDesk.WindowsTests -c Release --no-build -- artifacts/windows-checks
./scripts/Publish.ps1 -Version 1.0.1
```

The Windows console check returns a nonzero exit code on a failed workflow, invalid viewport render or binding error. `Publish.ps1` additionally fails if the packaged executable cannot launch, write/read SQLite or render its dashboard.
