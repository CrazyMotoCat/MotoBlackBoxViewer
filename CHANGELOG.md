# Changelog

Все заметные изменения проекта фиксируются в этом файле.

Формат вдохновлён принципами Keep a Changelog, но адаптирован под текущий стиль проекта.

Правило ведения:

- новые заметные изменения добавляем сюда продолжением текущей истории
- существующие блоки радикально не переписываем без отдельной причины
- README и ROADMAP обновляются по своему назначению, а CHANGELOG остаётся журналом уже сделанных заметных изменений

## [Unreleased]

### Added

- chart profiling mode с агрегированием времени для `CreateVisibleData`, chart slicing, downsampling и redraw
- дополнительные chart viewport preset'ы: `50 / 100 / 200 / 500 / 1000 / 5000 / full range`
- runtime map tile cache/proxy через локальный disk-backed path внутри WebView2
- user-visible surfacing для части ошибок session save, map export/browser open и embedded map runtime failures
- large-log smoke coverage на основе `example_log_35000dots.csv`

### Changed

- CSV import переведён на streaming read path
- CSV parser поддерживает quoted values и embedded separators
- malformed rows теперь учитываются в diagnostics и не ломают импорт, если в файле остаются валидные строки
- partial logs теперь поддерживаются в first-pass варианте: missing `lean`/`accelX`/`accelY`/`accelZ` channels больше не ломают весь импорт
- partial import report теперь показывает, сколько строк удалось импортировать из числа прочитанных
- analytics теперь делает first-pass GPS outlier filtering для spike-return сценариев, чтобы distance и summary меньше искажались шумными точками
- statistics теперь включают first-pass event detection для резких торможений и разгонов
- statistics теперь дополнительно показывают peak lean magnitude и transition-based peak lean event count
- statistics теперь включают first-pass stop/start pattern counters с гистерезисом по скорости
- average speed теперь считается как time-weighted metric, а не как простое среднее по sample points
- chart pipeline использует first-pass downsampling и slice-based reuse full-series buffers
- range filtering использует contiguous slice вместо полного линейного сканирования
- session persistence стал debounce-based и поддерживает `Flush(...)`
- map/runtime JSON bridge усилен через более безопасную JSON-to-JS передачу
- runtime map follow behavior теперь различает playback и ручной scrub
- `TelemetrySelectionViewModel` обновляет derived selection state по реальным visible-data changes
- solution переведён на `.NET 10`, SDK закреплён через `global.json`
- test project выровнен под `net10.0-windows`

### Fixed

- import теперь не схлопывается в пустое состояние, если все data rows невалидны: показывается явная ошибка
- partial import diagnostics теперь явно показывают отсутствующие каналы, а не только malformed rows
- import diagnostics теперь дают recovery hints для malformed CSV и честнее объясняют ограничения partial-import каналов
- checked-in dirty CSV fixture закрепляет partial-import behavior regression-тестом
- open/import command path больше не глотает load exceptions локально в `MainViewModel`; отмена и ошибки проходят через централизованную async-command обработку
- import/open failure теперь также поднимается в явный верхнеуровневый user-facing dialog через notification service
- session save failures больше не остаются только в trace-path
- map export/open failures теперь отображаются в status text
- embedded map больше не зависит от `file://` navigation path и использует local `https` host mapping
- повторные map refresh/update pushes сокращены через cached applied state и coalesced update flow

### UI

- baseline-палитра смещена к graphite-first surface design с burgundy как accent
- toolbar, playback и range-filter стали плотнее
- selected-state accent выровнен между grid / charts / playback / point readouts
- points table получила calmer styling, numeric alignment и left accent rail для selected row
- selected-point card и statistics tab получили более выраженную информационную иерархию
- из map area убраны дублирующие summary overlays

## [v13] - Architecture and reliability pass

### Added

- `MapScriptBuilder` для безопасной передачи route JSON
- новые unit tests для encoding fallback, map script escaping, async command recovery и coordinator failure paths
- `global.json` с SDK `10.0.201`

### Changed

- `CsvTelemetryReader` усилен: strict UTF-8 decode + CP1251 fallback only on decode failure
- `TelemetryWorkspaceCoordinator` начал suppress/deduplicate internal save chains
- `TelemetrySelectionViewModel` переведён на `VisibleDataVersion` вместо промежуточных summary-trigger'ов
- solution ретаргетирован на `.NET 10`

## [v12] - Workspace decomposition

### Added

- разделение screen-level состояния на Data / Selection / Playback / Map
- явный `TelemetryWorkspace` как root object для экранного состояния

### Changed

- app-слой стал лучше отражать UI-сценарии, но coordination сместился в `TelemetryWorkspaceCoordinator`

## [v11] - Technical subsystems

### Added

- `TelemetrySessionState`
- `PlaybackCoordinator`
- `SessionPersistenceCoordinator`

### Changed

- playback и persistence стали более явными техническими подсистемами

## [v10] - Screen state shaping

### Added

- базовое разделение UI-состояния вокруг telemetry workspace-подхода

### Changed

- проект ушёл от overly fat window-level state

## [v9] - DI and session persistence

### Added

- dependency injection через `App.xaml.cs`
- интерфейсы сервисов
- session persistence

### Changed

- архитектура стала чище и тестируемее

## [v8] - Early app refactor

### Added

- выделенные контролы для карты и графиков
- сервисы и команды вместо логики в окне

### Changed

- `MainWindow` был радикально уменьшен и перестал быть центральным местом всей логики
