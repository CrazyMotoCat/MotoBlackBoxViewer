# MotoBlackBoxViewer Roadmap

## Purpose

This document is a compact project brief for future chats and work sessions.
It captures what the project already does, how it is structured today, and
which next steps are most valuable.

## Project Snapshot

- Stack: C# / .NET 8 / WPF
- Solution: `MotoBlackBoxViewer.sln`
- Main app: `MotoBlackBoxViewer.App`
- Domain/core logic: `MotoBlackBoxViewer.Core`
- Tests: `MotoBlackBoxViewer.Tests`
- Primary goal: view and analyze motorcycle telemetry logs exported as CSV

## Current Product Scope

The app already supports:

- CSV import with `;` separator
- Russian and English column aliases
- UTF-8 with CP1251 fallback
- telemetry table
- speed, lean angle, and acceleration charts
- route display on embedded map via WebView2
- linked selection between table, charts, and map
- playback controls with multiple speed presets
- point range filtering
- route export to HTML map
- session restore for last file, filter, speed, and playback position

## Architecture Map

### Core

`MotoBlackBoxViewer.Core` contains the reusable telemetry logic:

- `Services/CsvTelemetryReader.cs`
- `Services/TelemetryAnalyzer.cs`
- `Models/TelemetryPoint.cs`
- `Models/TelemetryStatistics.cs`

This is the cleanest part of the codebase and the best place for additional
domain rules, parsers, calculations, and deterministic tests.

### App

`MotoBlackBoxViewer.App` is a WPF shell around the core logic.

Key areas:

- `ViewModels/MainViewModel.cs`
  Thin command layer for the window.
- `ViewModels/TelemetryWorkspace.cs`
  Root screen-level composition object.
- `ViewModels/TelemetryDataViewModel.cs`
  Loading, filtering, chart/table-facing data, statistics.
- `ViewModels/TelemetrySelectionViewModel.cs`
  Selected point and playback-position synchronization.
- `ViewModels/TelemetryPlaybackViewModel.cs`
  Playback state and interaction with the playback coordinator.
- `ViewModels/TelemetryMapViewModel.cs`
  Map JSON, selected marker, map export and refresh.
- `Services/TelemetryWorkspaceCoordinator.cs`
  Main cross-cutting orchestration layer between data, selection, playback,
  map, and session persistence.

Supporting services and abstractions already exist for:

- playback
- file picking
- map export
- app settings persistence
- session persistence

### Tests

Current automated coverage focuses on the most stable logic:

- CSV parsing
- encoding handling
- numeric parsing
- distance calculation
- telemetry statistics
- session persistence coordination
- app-layer loading/filtering consistency
- selection/map disposal behavior
- coordinator load/restore/reset/clear/playback flows

At the time of writing there are 21 unit tests across 5 test files.

## Current Technical Shape

The codebase is in a good intermediate state:

- `MainWindow` is thin
- screen responsibilities are split into several view models
- infrastructure is abstracted behind interfaces
- session restore exists
- `LoadCsvAsync()` now rebuilds visible data immediately, so the data view model
  does not sit in a partially loaded state after file import
- range filtering now slices contiguous point ranges instead of scanning the
  whole log with a linear `Where(...)`
- `TelemetrySelectionViewModel` and `TelemetryMapViewModel` now release their
  event subscriptions during workspace disposal
- tests cover core parsing, analytics, session persistence, and a small but
  growing part of the app-layer behavior

The main remaining complexity is not in the window anymore, but in screen
coordination and UI workflow orchestration.

## Highest-Value Hotspots

These are the places most likely to matter in future work:

1. `MotoBlackBoxViewer.App/ViewModels/TelemetryDataViewModel.cs`
   This is currently the largest view model and owns several responsibilities:
   loading results, filter state, visible data, statistics, and chart inputs.
2. `MotoBlackBoxViewer.App/Services/TelemetryWorkspaceCoordinator.cs`
   This is now the main scenario coordinator and a likely growth point for
   future complexity.
3. `MotoBlackBoxViewer.App/ViewModels/TelemetrySelectionViewModel.cs`
   Selection sync is central to table, charts, map, and playback behavior.
4. `MotoBlackBoxViewer.App/Controls/ChartRenderHelper.cs`
   Custom chart rendering may become harder to evolve once more visual features
   or interactions are added.

## Review Backlog

These review notes still look valid and should stay visible for future work:

- `TelemetrySessionState` is still a mixed bag of domain state, UI state, and
  transient flags; it likely wants to be split over time.
- `TelemetrySeriesSnapshot` removed repeated getter work, but filter changes
  still rebuild all chart arrays and chart definitions each time.
- the WPF to WebView2 bridge still sends full serialized route payloads into
  `ExecuteScriptAsync`, which may become expensive for long routes.
- the CSV reader is still optimized for controlled exports rather than dirty
  real-world CSV with quoting, malformed rows, or very large files.
- the core telemetry model is still intentionally narrow and may need to grow
  before richer ride analysis, event detection, or multi-session comparison.

## Recommended Roadmap

### Phase 1: Stabilize Screen Coordination

Goal: make UI behavior easier to reason about and safer to extend.

Suggested work:

- reduce responsibility inside `TelemetryWorkspaceCoordinator`
- extract explicit user-scenario flows where useful
- make filter/apply/reset/restore flows easier to trace
- continue splitting `TelemetrySessionState` into narrower state holders once
  the next round of coordination refactors begins
- add more tests around synchronization behavior between:
  data, selection, playback, map, and session restore
  Note: a first coordinator-level safety net now exists, but it is still far
  from exhaustive.

Success signal:

- fewer side effects hidden in property-change handlers
- easier to change one screen area without breaking another

### Phase 2: Strengthen Test Coverage Around App Layer

Goal: protect refactors in the UI orchestration layer.

Suggested work:

- add tests for filter range behavior
- add tests for selected-point restoration
- add tests for playback start/stop/speed changes
- add tests for clear/reset flows
- add tests for map refresh/export triggers where possible through interfaces
- add tests around workspace disposal and cross-viewmodel event wiring
- extend coordinator tests down to edge cases and failure paths, not only happy
  path scenarios

Success signal:

- refactors in `App` become much safer
- regressions are caught without manual UI testing every time

### Phase 3: Expand Filtering and Analysis Features

Goal: improve usefulness for real telemetry review sessions.

Suggested work:

- filtering not only by point index but also by:
  speed, lean angle, acceleration ranges
- derived metrics and richer statistics
- comparison of multiple rides/logs
- better summaries for selected ranges
- revisit chart-series recalculation cost if filters become more interactive

Success signal:

- the app becomes useful not just for viewing a log, but for investigating it

### Phase 4: Improve Map and Export Story

Goal: make route review and sharing more practical.

Suggested work:

- export screenshots or richer report artifacts
- improve map interactions and selected-point feedback
- consider offline map mode
- consider saved report bundles for sharing a session
- reduce route payload size or move to incremental map updates if long-route
  performance becomes visible

Success signal:

- map output is useful both inside the app and outside it

### Phase 5: Polish Product UX

Goal: move from capable prototype to smoother daily-use desktop tool.

Suggested work:

- explicit settings screen
- clearer empty/loading/error states
- better large-file responsiveness
- import history or recent files
- clearer status messaging and recovery guidance
- improve save/load error surfacing so persistence failures are user-visible,
  not only traced internally

Success signal:

- fewer confusing states during normal use
- less friction for returning users

## Suggested Immediate Priorities

If continuing development soon, the best order is:

1. Add more app-layer tests around selection, playback, restore, and clear
   flows on top of the new baseline coverage.
2. Refactor `TelemetryWorkspaceCoordinator` into smaller scenario-focused
   pieces if its responsibilities keep growing.
3. Split `TelemetrySessionState` if new coordination or persistence fields keep
   accumulating.
4. Only then expand user-facing features like advanced filters or multi-file
   comparison.

## Good First Refactors

These are relatively high leverage:

- introduce small scenario services for:
  load session, apply filter, synchronize selection, persist session
- separate chart data preparation from generic data-state handling
- make session restore flow independently testable without broad UI wiring
- consider a more robust CSV parsing path before adding multi-file import or
  less-controlled telemetry sources

## Known Environment Notes

- The solution targets `.NET 8`
- The WPF app targets `net8.0-windows`
- Running or testing locally requires a .NET SDK in the environment
- Embedded map uses WebView2
- Map tiles rely on internet access because they come from OpenStreetMap

## Quick Context For Next Chat

If you need to rehydrate context quickly in a future conversation:

- This is a WPF telemetry viewer for motorcycle CSV logs.
- The repository is already refactored away from a fat `MainWindow`.
- The current architectural center is `TelemetryWorkspace` plus
  `TelemetryWorkspaceCoordinator`.
- The most likely next engineering task is to keep improving
  orchestration/testability and gradually split mixed state containers.
- The most likely next product task is richer filtering and analysis, but only
  after the app layer gets a bit more regression safety.
