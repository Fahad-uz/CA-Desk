# Changelog

## 1.1.2

Fixed startup window bounds on smaller and scaled displays so Minimize, Maximize/Restore and Close are fully on screen immediately. Minimum sizes adapt to the current monitor, maximized content accounts for clipped frame edges, and work-area/DPI changes recheck the window. Added actual screen-coordinate and input checks before any minimize/restore action, directly maximized startup coverage and display-scale geometry tests.

## 1.1.1

Added an optional saved profile name in Settings, a personal welcome at startup and the name in the left sidebar. Names are retained in the existing database and complete backups. Changed Windows packaging to a self-contained single executable with native dependencies embedded, so copying only the EXE to Desktop works. The package gate launches an EXE-only copy from another working directory with a fresh runtime cache. Releases now include a direct EXE download, optional ZIP and both checksums.

## 1.1.0

Added independent personal Study Notes with heading/body/date editing, instant search, purple calendar entries, event display filters, right-side navigation, keyboard shortcuts and unsaved-change protection. Added client due dates and remaining-day indicators. Introduced permanent profile storage, automatic legacy detection, verified transactional migrations, daily/local exported backups, confirmed validated restores and persistent view preferences. Release gates test upgrades using the published v1.0.1 package and 150-client/3,000-work fixtures.

## 1.0.1

Added a visible application title bar with Minimize, Maximize / Restore and Close buttons, accessible names, tooltips and keyboard focus. Native window movement, resizing and system menu behavior are retained. Native and packaged-executable checks exercise every caption action.

## 1.0.0

Initial Windows release: offline client/work management, SQLite persistence, live dashboard and progress, sortable and filtered master table, month/week/day calendars, native editing, notes and semantic-versioned portable releases.
