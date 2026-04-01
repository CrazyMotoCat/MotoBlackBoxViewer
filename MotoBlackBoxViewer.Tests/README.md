# MotoBlackBoxViewer.Tests

Тестовый проект для проверки core-логики и ключевых app-layer сценариев MotoBlackBoxViewer.

## Что покрыто

Сейчас автотесты покрывают, в том числе:

- CSV parsing, quoted values, encoding fallback, partial-import reports и malformed-row diagnostics
- расчёт дистанции и telemetry statistics
- time-weighted average speed и базовые regression-сценарии аналитики
- session persistence, debounce/flush и error surfacing
- playback coordinator и playback-related workspace flows
- app-layer loading, filtering, restore/reset/clear сценарии
- workspace coordinator и scenario services
- map script building, route simplification и tile-cache helpers
- chart downsampling, chart profiling diagnostics и large-log smoke coverage

## Как запускать

Полный solution run:

```bash
dotnet test MotoBlackBoxViewer.sln
```

Только тестовый проект:

```bash
dotnet test MotoBlackBoxViewer.Tests/MotoBlackBoxViewer.Tests.csproj
```

Если desktop app уже запущен и блокирует обычный app output, используйте:

```bash
dotnet test MotoBlackBoxViewer.Tests/MotoBlackBoxViewer.Tests.csproj /p:UseAppHost=false
```

## Текущий baseline

- текущий checked baseline: `90 / 90`
- test project: `xUnit + Microsoft.NET.Test.Sdk + coverlet.collector`

## Заметки

- часть chart/map и dirty-CSV тестов использует checked-in sample datasets
- часть app-layer тестов intentionally проверяет не только happy-path, но и failure/recovery сценарии
- при заметных изменениях в покрытии этот файл должен обновляться вместе с `README.md`, `ROADMAP.md` и `CHANGELOG.md`
