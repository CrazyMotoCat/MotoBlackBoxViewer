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
