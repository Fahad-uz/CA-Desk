# Architecture

WPF desktop only. A SQLite repository owns persistent data; a shared observable application store reloads committed snapshots and publishes changes. MVVM view models derive dashboard, client progress, filtered tables and calendar from that one store. Views provide native date pickers, direct status editing, keyboard access and confirmation for deletes.

Clients have many work areas with cascading foreign keys. Status is an enum with exactly three values; overdue is derived separately from local calendar dates. Deadlines use ISO date-only strings. Schema versions use transactional SQLite user_version migrations. The database lives under %LOCALAPPDATA%\CA Desk, separate from the installation.

No account, network client, sample production data, reminder service or backup system.
