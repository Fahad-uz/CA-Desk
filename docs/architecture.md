# Architecture

CA Desk is a native Windows WPF application. SQLite owns persistent records; ApplicationStore serializes writes, exports and restores, reloads committed snapshots and notifies connected views. Client progress and dashboard statistics derive only from work areas. Study notes remain independent of clients and work statuses.

## Stable storage and upgrade

Production uses `%LOCALAPPDATA%\CADesk\Data\cadesk.db`, with backups under `%LOCALAPPDATA%\CADesk\Backups`. The path and single-user application mutex are independent of release version. If the canonical database is absent, startup detects the v1.0.x path `%LOCALAPPDATA%\CA Desk\ca-desk.db`, validates it and transfers it using SQLite's backup API. It retains the original and a protected legacy copy. An existing canonical file always wins and is never silently initialized or reset.

Schema 1 contains clients and work areas. Migration 2 adds nullable client `due_date`. Migration 3 adds `study_notes` and `preferences`. A verified pre-migration copy is created before schema changes. Ordered additive changes run in one transaction; integrity, foreign keys, column contracts, dates, timestamps and statuses are checked before setting `user_version` and committing. Failure rolls back, reports the recovery path and stops startup. The recovery prompt can restore the previous schema for use with a compatible executable. Newer unsupported schemas are refused.

Every release must retain these ordered migrations and add forward steps instead of recreating existing tables. Never delete user data as part of an update. Tests include schema 1/2 failure rollback, corrupt/future file rejection and a published-old-release fixture with 150 clients and 3,000 work areas.

## Backups and restoration

[Microsoft.Data.Sqlite BackupDatabase](https://learn.microsoft.com/dotnet/standard/data/sqlite/backup) creates consistent copies including WAL writes. Export stages a copy, verifies it and moves it to a new destination filename. Startup and local midnight schedule a background daily backup under the store gate. Only old automatic copies rotate, after the new copy has passed verification; the newest 14 remain. Recovery copies are retained.

Restore validates compatibility/integrity before confirmation. It prepares and migrates an isolated candidate before changing the live database. A verified current-data backup is then created, the candidate restored through SQLite, and the result validated and loaded. Failure after replacement attempts recovery from that current-data backup. The UI blocks edits during replacement. Original selected backups are never modified. Preferences are stored in the database and included in every copy.

## Dates, notes and calendar

Deadlines, client due dates and note dates use ISO date-only strings; no timezone conversion can shift them. Remaining days use local calendar-day difference and refresh when the local date changes. Work status has exactly Not Started, In Progress and Completed; overdue remains a derived condition.

Study notes contain a unique ID, required heading, plain-text body, required editable date and creation/update timestamps. Notes have no client foreign key and no status. Deleting a client cannot delete personal notes. Month/week/day calendars merge work deadlines and study notes, retain client filtering for work only, and apply a separate event-type filter. Purple marks notes, while grey/blue/green/red retain work meaning. Calendar entries contain headings, never study-note bodies.

## Native window behavior

Visible Minimize, Maximize / Restore and Close buttons route through WPF SystemCommands. [WindowChrome](https://learn.microsoft.com/en-us/dotnet/api/system.windows.shell.windowchrome) retains caption dragging, double-click maximize/restore, resizing and the system menu. The note editor supports Ctrl+S and guards against duplicate save commands and unsaved cancellation.

No account, network client, cloud sync, AI service or production sample records. The portable distribution contains no user database and removing it leaves the profile database and backups intact.
