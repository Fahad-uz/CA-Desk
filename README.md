# CA Desk

Native Windows 10/11 (64-bit) work management for Chartered Accountants. C#, WPF, .NET 10 and SQLite. Single user and entirely offline.

Development is underway in meaningful, verified commits. Downloadable releases will be published after the Windows checks pass.

## Build

Install the .NET 10 SDK on Windows, then run `dotnet build CaDesk.slnx -c Release`. The finished portable package will bundle its runtime.

## Architecture

`src/CaDesk.Core` contains domain models and local SQLite storage. `src/CaDesk.Desktop` is the Windows WPF interface. Tests and release workflows will be added during development.
