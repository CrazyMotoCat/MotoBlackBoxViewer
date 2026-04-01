# MotoBlackBoxViewer

MotoBlackBoxViewer — desktop-приложение на **C# / WPF / .NET 10** для просмотра и анализа телеметрии мотоцикла, экспортированной в CSV.

Проект ориентирован на сценарий "открыть лог → посмотреть маршрут → проанализировать точки, графики и выбранный диапазон → сохранить контекст работы".

## Возможности

Сейчас приложение поддерживает:

- импорт CSV с разделителем `;`
- поддержку русских и английских заголовков колонок
- чтение UTF-8 с fallback на CP1251 при ошибке декодирования
- потоковую загрузку CSV без полного чтения файла в память
- таблицу телеметрических точек
- графики скорости, угла наклона и ускорений `X / Y / Z`
- встроенную карту маршрута через WebView2
- связанную синхронизацию выбора между таблицей, графиками и картой
- playback с шагами, autoplay и скоростями `0.25x / 0.5x / 1x / 2x / 4x / 8x / 16x`
- фильтрацию по диапазону точек
- окно графика вокруг текущей точки: `50 / 100 / 200 / 500 / 1000 / 5000 / full range`
- расчёт статистики и дистанции для выбранного диапазона
- first-pass event analytics для резких торможений, разгонов, пиков наклона и stop/start паттернов
- экспорт маршрута в HTML-карту с открытием во внешнем браузере
- сохранение и автоматическое восстановление последней сессии
- user-visible diagnostics для части ошибок импорта, экспорта карты и runtime-карты

## Структура решения

- `MotoBlackBoxViewer.App` — WPF UI, ViewModels, контролы, WebView2-карта, app-layer сервисы
- `MotoBlackBoxViewer.Core` — модели, CSV-парсер, расчёты и анализ телеметрии
- `MotoBlackBoxViewer.Tests` — unit-тесты для parsing, analytics и части app-layer сценариев

## Формат CSV

Поддерживаются как русские, так и английские alias-имена колонок. Базово ожидаются поля:

- `широта`
- `долгота`
- `скорость`
- `ускорение по X` / `Y` / `Z` (опционально)
- `угол наклона` (опционально)

Также поддерживаются алиасы вроде `lat`, `lon`, `speed` и другие нормализованные варианты заголовков.

### Что важно знать

- CSV читается построчно
- quoted-поля поддерживаются
- `;` внутри quoted-значений поддерживается
- malformed rows не ломают весь импорт, если в файле остаются валидные строки
- при partial import UI показывает, сколько строк удалось импортировать из числа прочитанных
- если все строки данных невалидны, импорт завершается явной ошибкой
- partial logs поддерживаются в first-pass варианте: если в файле нет `lean` или `accelX/Y/Z`, доступные каналы всё равно загружаются, а UI показывает диагностику о нехватающих данных

## Карта

Карта работает через **WebView2** и HTML/JS runtime.

Используется:

- OpenStreetMap как tile source
- `MapLibre GL JS` как browser-side map engine

Особенности текущей реализации:

- runtime-карта использует безопасную передачу route JSON в JS
- для больших маршрутов применяется downsampling перед передачей в WebView2
- карта различает playback-follow и ручной scrub по slider
- runtime HTML открывается через локальный `https` host mapping вместо `file://`
- tile requests внутри WebView2 проходят через локальный disk-backed cache/proxy

Экспортируемая HTML-карта по-прежнему зависит от доступа браузера к сети для загрузки внешних tile-данных.

## Архитектура

Проект уже разделён на app/core/tests и движется в сторону более явного разделения сценарных обязанностей.

Ключевые app-layer сущности:

- `MainViewModel` — верхнеуровневые команды окна
- `TelemetryWorkspace` — composition root экранного состояния
- `TelemetryDataViewModel` — данные, фильтрация, статистика, chart/table inputs
- `TelemetrySelectionViewModel` — выбранная точка и синхронизация selection/playback
- `TelemetryPlaybackViewModel` — состояние воспроизведения
- `TelemetryMapViewModel` — map JSON, marker, export, refresh

Ключевые сервисы orchestration:

- `TelemetryWorkspaceCoordinator`
- `TelemetryWorkspaceSynchronizationService`
- `TelemetryWorkspacePersistenceService`
- `TelemetryWorkspaceLoadService`
- `TelemetryWorkspaceSessionRestoreService`
- `TelemetryWorkspaceInteractionService`

Подробный план развития вынесен в [ROADMAP.md](./ROADMAP.md), а история заметных изменений — в [CHANGELOG.md](./CHANGELOG.md).

## Как открыть и запустить

### Требования

- Windows
- .NET SDK 10
- Visual Studio 2022 / Rider / `dotnet` CLI
- WebView2 Runtime

### Через Visual Studio

1. Откройте `MotoBlackBoxViewer.sln`
2. Дождитесь восстановления NuGet-пакетов
3. Запустите `MotoBlackBoxViewer.App`

### Через CLI

```bash
dotnet restore
dotnet build MotoBlackBoxViewer.sln
dotnet run --project MotoBlackBoxViewer.App
```

## Тесты

Автотесты покрывают, в том числе:

- CSV parsing
- encoding handling
- numeric parsing
- distance/statistics calculation
- session persistence coordination
- app-layer load/restore/reset/clear/playback сценарии
- map script escaping
- async command reentrancy/error recovery
- часть large-log smoke coverage

Запуск тестов:

```bash
dotnet test MotoBlackBoxViewer.sln
```

## Текущие ограничения

На текущем этапе стоит учитывать следующие ограничения:

- GPS outlier filtering сейчас покрывает first-pass spike-return сценарии, но более глубокое smoothing ещё остаётся в roadmap
- event detection сейчас покрывает first-pass счётчики резких торможений, разгонов, пиков наклона и stop/start паттернов, но richer analytics ещё остаётся в roadmap
- часть chart/map performance work для очень больших логов ещё остаётся в roadmap
- export HTML-карты пока не использует тот же offline/cache path, что runtime-карта
- partial repair/import workflow для проблемных CSV ещё не реализован

## Документация

- [ROADMAP.md](./ROADMAP.md) — приоритеты, фазы и ближайшие направления развития
- [CHANGELOG.md](./CHANGELOG.md) — история заметных изменений
