# MotoBlackBoxViewer README (Annotated)

## Структура решения

* `MotoBlackBoxViewer.App` — WPF UI, встроенная карта, окно, ViewModel
  ⚠️ Review note: UI-слой хорошо декомпозирован, но orchestration уже смещается в coordinator и может стать новым узким местом.
* `MotoBlackBoxViewer.Core` — модели, CSV-парсер, анализ телеметрии
  ⚠️ Review note: core логика чистая, но CSV-парсер всё ещё не рассчитан на очень большие или грязные логи.
* `MotoBlackBoxViewer.Tests` — unit-тесты для parsing, analytics и части app-layer поведения
  ⚠️ Review note: покрытие уже заметно лучше, но до end-to-end сценариев и performance-paths ещё есть запас.

---

# MotoBlackBoxViewer

Стартовый каркас Windows-приложения на **C# + WPF + .NET 10** для просмотра логов ESP32 с мотоцикла.

## Что уже есть

* загрузка CSV с разделителем `;`
  ⚠️ Review note: parser уже поддерживает quoted CSV и `;` внутри значений, но всё ещё silently skip’ает malformed rows.
* поддержка русских и английских заголовков
* чтение UTF-8 с fallback на CP1251
  ⚠️ Review note: fallback теперь основан на decode failure, а не на broad `catch`, но чтение всё ещё идёт целиком в память.
* таблица точек
* графики скорости и угла наклона
* график ускорений `X / Y / Z`
* встроенная карта маршрута через WebView2
* связанная подсветка выбранной точки в таблице, на графиках и на карте
* ползунок воспроизведения маршрута
* пошаговое перемещение и autoplay точек
* выбор скорости воспроизведения: `0.25x / 0.5x / 1x / 2x / 4x`
* фильтр диапазона точек с обновлением карты, графиков, таблицы и статистики
* корректный расчёт дистанции для выбранного диапазона
  ⚠️ Review note: расчёт всё ещё не фильтрует GPS outliers.
* экспорт маршрута в HTML-карту и открытие во внешнем браузере
  ⚠️ Review note: export-path уже использует safe JSON bootstrap, но long-route payload size остаётся отдельным perf-вопросом.
* сохранение и автоматическое восстановление последней сессии
  ⚠️ Review note: persistence теперь debounce-based и умеет `Flush(...)`, что снижает лишние записи на диск.

---

## What Improved Recently

* `CsvTelemetryReader` теперь использует strict UTF-8 decode и fallback на CP1251 только при `DecoderFallbackException`.
* CSV parsing теперь поддерживает quoted поля и embedded separators.
* range filtering теперь режет contiguous slice через `SlicePoints(...)`, а не сканирует весь лог через линейный `Where(...)`.
* `SessionPersistenceCoordinator` теперь debounce’ит save, умеет `Flush(...)` и репортит ошибки через trace/error handler.
* `TelemetryWorkspaceCoordinator` теперь подавляет внутренние reactive save-циклы во время load / clear / reset / filter-sync сценариев и дополнительно дедуплицирует одинаковые session-save snapshot'ы.
* из `TelemetryWorkspaceCoordinator` уже начато выносить сценарные обязанности:
  * `TelemetryWorkspaceSynchronizationService` держит data/selection/map synchronization steps
  * `TelemetryWorkspacePersistenceService` держит deduped save / flush session logic
  * `TelemetryWorkspaceLoadService` держит CSV load scenario
  * `TelemetryWorkspaceSessionRestoreService` держит last-session restore scenario
* `TelemetrySelectionViewModel` теперь реагирует на реальные изменения visible data (`VisibleDataVersion`), а не на промежуточный `FilterSummary`, что уменьшает лишние selection/property-change эхо.
* `CsvTelemetryReader` теперь устойчивее к вариантам русских accel-заголовков (`по X` / `поY` family после normalize).
* solution переведён на `.NET 10` (`net10.0` / `net10.0-windows`) и зафиксирован через `global.json` на SDK `10.0.201`.
* test project выровнен под `net10.0-windows`, поэтому solution теперь корректно собирается и тестируется вместе с WPF app-layer.
* после ретаргета удалён лишний `System.Text.Encoding.CodePages` package reference: на `.NET 10` проект и тесты проходят без него.
* export карты и runtime WebView2 updates используют `MapScriptBuilder` для безопасной передачи route JSON через сериализованную строку и `JSON.parse(...)`.
* `MapViewControl` уже избегает redundant refresh pushes через cached applied state:
  * `_appliedRouteJson`
  * `_appliedRefreshVersion`
  * `_appliedSelectedPointIndex`
* live map sync теперь coalesced: параллельные route/selection updates не разрастаются в россыпь независимых fire-and-forget задач.
* базовая визуальная тема уже начала уходить от старого default-WPF вида к более целостной кастомной desktop-палитре.
* второй UI-pass уже проведён:
  * graphite-first palette для больших surface-областей
  * burgundy оставлен как accent
  * toolbar / playback / range-filter стали плотнее
  * map overlay cards стали легче и компактнее
  * contrast states для tabs / combobox / selected grid rows дополнительно выровнены
* следующий UI cleanup pass уже тоже частично внедрён:
  * selected accent выровнен между grid / charts / playback readouts / point card
  * points table получила более спокойные линии, numeric alignment и left accent rail для selected row
  * selected-point card на карте получила более явную иерархию: id, speed/lean, затем coordinates / distance / detail
  * playback strip сильнее центрирован вокруг slider, а speed/meta стали вторичнее
  * chart tabs получили info-rows над графиками, а statistics tab теперь заполнен не только верхним metric grid
  * session summary / status bar стали более product-like: filename сильнее, metadata слабее, статусы менее debug-oriented
* app-layer test safety net тоже расширен:
  * restore missing-file path
  * reset / clear persistence contracts
  * restart playback from the end
  * selected playback-position persistence
* встроенная карта больше не грузится как `file://` page:
  * `MapViewControl` теперь открывает map HTML через локальный `https` host mapping WebView2
  * это нужно для совместимости с текущей tile policy OpenStreetMap, где tile requests ожидают корректный `Referer`

---

## UI Direction

Текущий следующий визуальный фокус после `.NET 10` migration и app-layer stabilization:

* уйти от тяжелой бордовой заливки больших поверхностей к более нейтральной графитовой базе
* оставить бордовый как accent, а не как основной surface color
* сделать toolbar визуально современнее:
  * компактнее
  * с более четким разделением primary / secondary actions
* собрать playback в одну аккуратную control line
* ужать block range filtering по высоте и сделать его более информационно плотным
* облегчить map overlays:
  * меньше тени
  * компактнее карточки
  * сильнее типографическая иерархия

⚠️ Review note:

* текущая красная тема уже лучше исходной темно-синей, но still считается промежуточной и будет дорабатываться в сторону более professional / less heavy dark UI

---

## Формат CSV

Ожидаются колонки:

* `широта`
* `долгота`
* `скорость`
* `ускорение по Z`
* `ускорение по X`
* `ускорение по Y`
* `угол наклона`

Также поддерживаются alias-имена (`lat`, `lon`, `speed`, и т.д.).

Регистр не критичен.

⚠️ Review note:

* все поля фактически обязательны
* строки с ошибками пока silently игнорируются

TODO:

* поддержка частичных логов
* явная диагностика ошибок строк

---

## Как открыть в Visual Studio

1. Откройте `MotoBlackBoxViewer.sln`
2. Дождитесь восстановления NuGet-пакетов
3. Запустите `MotoBlackBoxViewer.App`

---

## NuGet-пакеты

* `Microsoft.Web.WebView2`

---

## Как работает карта

* WebView2 + HTML-шаблон
* маршрут передаётся через JS
* используется OpenStreetMap

⚠️ Review note:

* exported HTML уже использует safe JSON bootstrap
* runtime WebView2 sync тоже уже не шлёт raw JSON напрямую, но всё ещё требует дальнейшей оптимизации по payload size и long-route responsiveness
* `MapViewControl` уже кеширует применённое состояние и coalesced async updates, чтобы не слать одинаковые обновления повторно
* embedded map runtime теперь использует local `https` host mapping вместо `file://` navigation, чтобы не упираться в OSM tile blocking из-за missing `Referer`

TODO:

* оптимизация передачи данных для длинных маршрутов
* incremental updates / chunking при необходимости

---

## Архитектурный рефакторинг v8

* `MainWindow` уменьшен до 13 строк
* карта и графики вынесены в отдельные контролы
* введены сервисы и команды

⚠️ Review note:

* хороший шаг, но сложность переместилась в orchestration

---

## Архитектурный шаг v9

* dependency injection через `App.xaml.cs`
* введены интерфейсы сервисов
* добавлен session persistence

⚠️ Review note:

* архитектура стала чище и тестируемее

---

## Архитектурный шаг v10

* введён `TelemetryWorkspace`
* разделение UI-состояния

⚠️ Review note:

* начало разбиения ответственности, но workspace начал разрастаться

---

## Архитектурный шаг v11

* введены:
  * `TelemetrySessionState`
  * `PlaybackCoordinator`
  * `SessionPersistenceCoordinator`

⚠️ Review note:

* правильное направление: выделение технических подсистем

---

## Архитектурный шаг v12

* разделение на:
  * Data
  * Selection
  * Playback
  * Map

⚠️ Review note:

* структура хорошо отражает UI-сценарии

Но:

* coordination сосредоточен в `TelemetryWorkspaceCoordinator`
* возможен рост сложности в одном месте

---

## Архитектурный шаг v13

* `CsvTelemetryReader` усилен:
  * strict UTF-8 decode + fallback на CP1251 только при `DecoderFallbackException`
  * поддержка quoted CSV-полей и `;` внутри значений
* `TelemetryWorkspaceCoordinator` теперь:
  * не проглатывает `OperationCanceledException` при restore/load
  * выставляет явный `StatusText` при ошибках загрузки
  * suppress’ит внутренние reactive `Save(...)`-цепочки, когда сам перестраивает filter/selection state
  * не шлёт повторный identical session snapshot в persistence layer
* `TelemetrySelectionViewModel` теперь обновляет derived selection/playback properties по `VisibleDataVersion`, а не по промежуточному `FilterSummary`
* solution переведён на:
  * `net10.0` для core
  * `net10.0-windows` для app/tests
* добавлен `global.json` c SDK `10.0.201`
* test project переведён на `net10.0-windows`, чтобы быть совместимым с ссылкой на `MotoBlackBoxViewer.App`
* карта и export переведены на более безопасную передачу route JSON:
  * добавлен `MapScriptBuilder`
  * JSON передаётся через сериализованную строку + `JSON.parse(...)`
* `AsyncRelayCommand`, startup и map sync получили trace/log-friendly обработку async-ошибок
* range filtering теперь использует contiguous slice вместо линейного сканирования
* session persistence теперь debounce-based и умеет `Flush(...)`
* добавлены unit-тесты на:
  * quoted CSV / encoding fallback
  * coordinator failure/cancellation paths
  * map script escaping
  * async command reentrancy/error recovery
  * workspace-level защиту от лишних session save при internal selection/filter synchronization
* `dotnet test MotoBlackBoxViewer.sln` сейчас проходит полностью на `.NET 10`: `31 / 31`

⚠️ Review note:

* устойчивость data/map/async слоёв выросла, но async-тема улучшена частично, а не закрыта полностью: дальше всё ещё нужны streaming CSV, malformed-row diagnostics, chart perf work и дальнейшее дробление coordinator

---

## Что можно делать дальше

* фильтрация по значениям (`speed` / `lean` / `accel`)
* экспорт скриншотов
* офлайн-карта
* мульти-файлы

➕ Добавить:

* streaming CSV
* malformed-row diagnostics
* оптимизацию графиков
* второй проход по UI polish:
  * graphite-first palette
  * denser toolbar/playback/filter layout
  * lighter map overlays

---

## Тесты

Покрывают:

* parsing CSV
* кодировки
* дистанцию
* статистику
* coordinator restore/load/reset/clear/playback flows
* session persistence debounce/flush
* map script escaping
* async command reentrancy/error recovery
* workspace-level suppression лишних session save при internal coordinator sync
* full solution test run (`31 / 31`) на `dotnet test MotoBlackBoxViewer.sln` под `.NET 10`

⚠️ Review note:

* всё ещё не покрыты большие файлы, full export content и performance-sensitive paths

TODO:

* добавить edge-case и large-dataset тесты

---

## Архитектурные риски

⚠️ Основные слабые места:

* CSV parsing:
  * файл всё ещё читается целиком в память
  * malformed rows всё ещё silently skip’аются
* аналитика:
  * средние всё ещё не time-weighted
  * GPS outlier filtering пока нет
* производительность:
  * графики всё ещё делают full redraw
  * массивы данных всё ещё часто materialize’ятся заново
* async:
  * стало безопаснее в части путей, но UI-слой ещё не доведён до полностью прозрачного error surfacing
* масштабируемость:
  * `TelemetryWorkspaceCoordinator` всё ещё может стать bottleneck

---

## GitHub / CI

CI:

* restore
* build
* test

✔️ Базовая настройка уже есть

---

## Итог

Текущее состояние:

* ✔️ Хорошая архитектурная база
* ✔️ Core стал устойчивее к реальным CSV
* ✔️ UI orchestration стал заметно безопаснее в нескольких важных точках

Но:

* ⚠️ parsing больших и грязных данных всё ещё главный риск
* ⚠️ chart performance — следующий большой риск
* ⚠️ orchestration всё ещё требует дальнейшего дробления

Главный фокус:

👉 **устойчивость данных + контроль сложности orchestration + подготовка к большим логам**
