# Executed testing and scope

## Confirmed checkpoint: 9 October 2026

[Windows workflow run 37886005660](https://github.com/Fahad-uz/CA-Desk/actions/runs/37886005660) completed successfully for commit `65bf32f8ab1cda4c2e760cb6252b16761c834c05`.

- Release solution build: zero warnings and zero errors.
- 31 xUnit core tests passed. Includes creation, rename, deletion and cascading foreign keys, duplicate names, blank input, all three statuses, deadline add/edit/removal, multiline and long notes, restart persistence, parameterised SQL, all deadline filters, combined filters, sorting, progress and a 100-client/2,000-work-area dataset.
- 32 native WPF checks passed. Includes the actual empty dashboard, editor commands, client search control, inline status dropdown/date picker, synchronized counts and progress, filter preservation, month overflow/day drill-down, entry opening, calendar edits/client filter/view memory, cancellation and confirmation of deletes, rendered pages/forms, reopening the WPF app and no binding errors.
- A self-contained Windows x64 package was produced. Independent launch of that published package is added to the final release workflow.

The first native run found a read-only progress binding error that was not detectable by compilation. It was corrected, and the above rerun passed.

The first screenshot run was limited by the hosted desktop resolution. Final checks render the actual WPF content at explicit 1920×1080 and 1000×680 sizes independent of that limit. New checks also exercise the styled selector popups and 1,505-row native table virtualisation. Final release evidence is uploaded by the release workflow, including package launch verification.

## Test boundaries

The developer host is macOS. Core tests run there against actual SQLite; WPF source is cross-compiled there. Native UI execution is on a GitHub-hosted Windows Server runner. No physical Windows 10 or Windows 11 device test, exhaustive accessibility audit or every DPI setting is claimed. Targeted OS compatibility and actual test OS are distinct.

Test clients are fictional and generated in isolated temporary directories. Production databases are excluded from source control and packages. No real client/accounting information was used.

## Reproduce

```powershell
dotnet build CaDesk.slnx -c Release
dotnet test tests/CaDesk.Tests -c Release --no-build
dotnet run --project tests/CaDesk.WindowsTests -c Release --no-build -- artifacts/windows-checks
./scripts/Publish.ps1 -Version 1.0.0
```

The Windows console check returns a nonzero exit code on a failed workflow, invalid viewport render or binding error. `Publish.ps1` additionally fails if the packaged executable cannot launch, write/read SQLite or render its dashboard.
