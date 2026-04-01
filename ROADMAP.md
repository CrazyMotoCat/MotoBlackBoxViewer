# MotoBlackBoxViewer Roadmap

## Назначение

Этот документ фиксирует **куда проект идёт дальше**: ключевые приоритеты, технические долги, последовательность работ и критерии успеха.

README описывает текущее состояние проекта.
ROADMAP описывает будущие изменения и приоритеты.
CHANGELOG фиксирует уже сделанные заметные изменения.

## Правило обновления документации

При заметных изменениях в проекте придерживаемся такого правила:

- `CHANGELOG.md` дополняем новыми изменениями, сохраняя текущую структуру и стиль, без радикальной переработки истории
- `README.md` обновляем только для значимых изменений проекта, которые действительно важны для внешнего описания, запуска, ограничений или возможностей
- `ROADMAP.md` используем для сверки с будущими планами, новыми долгами и приоритетами; уже реализованные пункты из него убираем или переводим в другие документы при необходимости

## Продуктовая цель

Развивать MotoBlackBoxViewer как удобный desktop-инструмент для анализа мото-телеметрии, в котором:

- реальные CSV-логи открываются надёжно
- аналитике можно доверять
- большие сессии остаются отзывчивыми
- карта, графики и playback работают как единое связанное пространство
- архитектура остаётся сопровождаемой при росте функциональности

## Текущие приоритеты

### P0 — Надёжность данных и честность аналитики

- добить устойчивость CSV import для грязных и частично неполных логов
- улучшить import diagnostics и recovery flows
- добавить outlier filtering для GPS/сенсорных данных
- расширить набор корректных derived metrics и event detection
- сделать поведение аналитики понятным и воспроизводимым

### P1 — Производительность и контроль сложности

- продолжить дробление `TelemetryWorkspaceCoordinator`
- сократить лишние materialization/copy operations в chart/data pipeline
- развить perf-budget подход для import / charts / map refresh
- закрыть наиболее тяжёлые сценарии на больших логах

### P2 — Практическая ценность продукта

- расширить фильтрацию
- улучшить map/export story
- добавить multi-session comparison
- сделать UX более продуктовым и менее “debug-oriented”

## Принципы

1. **Correctness before cosmetics**
   Красивый график бесполезен, если метрика считается неверно.

2. **Graceful degradation**
   Частично проблемный лог должен открываться настолько полно, насколько это возможно.

3. **Performance is a feature**
   Большие поездки не должны превращать UI в тяжёлую отладочную оболочку.

4. **Explicit orchestration beats hidden side effects**
   Чем меньше неявной реактивной магии между screen-level объектами, тем проще сопровождение.

5. **Docs must stay aligned with code**
   README, ROADMAP и CHANGELOG не должны спорить между собой.

## Ключевые зоны долга

### Data reliability

- partial logs пока поддерживаются не полностью
- richer CSV recovery flow пока не реализован
- нужна более прозрачная import diagnostics story

### Analytics

- нужен GPS/sensor outlier filtering
- нужны richer derived metrics
- нужны event-oriented сценарии анализа: hard braking, peak lean, acceleration events

### Performance

- chart pipeline всё ещё имеет запас для больших файлов
- full redraw path ещё остаётся дорогим при части обновлений
- map/export payload strategy ещё требует разделения preview vs full-fidelity

### Architecture

- `TelemetryWorkspaceCoordinator` остаётся главной complexity hotspot
- `TelemetrySessionState` со временем может потребовать дополнительного разбиения
- app-layer сценарии ещё не полностью разведены по небольшим ответственностям

### UX

- recovery/error states ещё можно сделать понятнее
- статусные тексты должны быть более user-facing
- advanced analysis workflows пока уступают базовому linked-selection сценарию

## Фазы развития

## Phase 1 — Data Reliability

### Goal
Сделать импорт устойчивым, диагностируемым и удобным для реальных логов.

### Planned work

- продолжить усиление streaming CSV path
- добавить полноценный import report:
  - сколько строк прочитано
  - сколько пропущено
  - почему они пропущены
  - какие каналы отсутствуют
- поддержать graceful degradation для partial logs
- расширить user-visible diagnostics в UI
- добавить golden-sample regression fixtures для грязных CSV и больших файлов

### Success signal

- import не превращается в «тишину» при проблемных данных
- пользователь получает понятный результат: success / partial success / explicit failure
- поведение импорта проверяется репликабельными тестами

## Phase 2 — Correct Analytics

### Goal
Сделать агрегаты и summary-метрики надёжнее и полезнее.

### Planned work

- развить новую baseline-модель time-weighted метрик
- добавить GPS outlier filtering / smoothing
- добавить event detection:
  - hard braking
  - sharp acceleration
  - peak lean
  - stop/start patterns
- разделить raw vs cleaned interpretation там, где это важно
- расширить тесты на корректность метрик

### Success signal

- summary metrics объяснимы и воспроизводимы
- искажения от шумных данных уменьшаются
- аналитика становится полезнее для реального разбора поездки

## Phase 3 — Performance & Architecture

### Goal
Удержать UI отзывчивым и не дать orchestration-союзу снова стать bottleneck.

### Planned work

- продолжить вынос сценариев из `TelemetryWorkspaceCoordinator`
- сделать workspace facade тоньше
- определить perf budgets для:
  - import time
  - chart redraw time
  - map refresh latency
  - session restore time
- использовать profiling results для следующего глубокого chart/perf cut
- уменьшить лишние копирования и полные пересборки данных
- добавить perf regression coverage в CI

### Success signal

- большие логи остаются usable
- app-layer refactors легче делать без страха сломать полэкрана
- профилирование помогает выбирать следующую optimisation target, а не гадать

## Phase 4 — Map & Export

### Goal
Сделать карту сильной рабочей частью продукта, а не просто визуализацией маршрута.

### Planned work

- разделить runtime preview path и full-fidelity export path
- довести tile cache/proxy до более законченной capability
- добавить дополнительные export formats:
  - GPX
  - KML
  - GeoJSON
- добавить route heatmaps:
  - by speed
  - by lean
  - by acceleration
- добавить bookmarks / incidents на карте
- улучшить selected-point feedback и long-route responsiveness

### Success signal

- карта полезна и внутри приложения, и для внешнего шеринга
- export качество не ограничивается runtime shortcut’ами
- repeated scrubbing/panning меньше зависит от сети и tile churn

## Phase 5 — Advanced Analysis Features

### Goal
Сделать приложение инструментом исследования, а не только просмотра.

### Planned work

- фильтрация не только по индексу, но и по:
  - speed
  - lean
  - acceleration
  - distance
  - time interval
- сравнение двух заездов
- overlay нескольких трасс
- segment / lap analysis
- richer selected-range summaries
- scenario presets:
  - city
  - highway
  - mountain road
  - track day

### Success signal

- пользователь может быстро изолировать интересный участок поездки
- появляется реальная comparative-analysis value
- базовый workflow становится сильнее, а не сложнее ради сложности

## Phase 6 — Release Engineering & Product Polish

### Goal
Сделать проект проще для выпуска, повторяемой сборки и повседневного использования.

### Planned work

- явный CI/status story в репозитории
- portable build / installer artifacts
- релизы с changelog
- crash-log bundle для bug reports
- sample datasets для demo и regression
- clearer empty/loading/error states
- более чистые и user-facing status messages
- consistency cleanup по UI и interaction flows

### Success signal

- проект проще запускать, проверять и распространять
- пользователю легче понимать состояние приложения
- качество релизного процесса догоняет качество кода

## Версионированный backlog

### v14 — Data Reliability

- продолжить hardening streaming CSV reader path
- расширить import report и malformed-row diagnostics
- поддержать partial logs с graceful degradation
- добавить golden-sample dirty CSV tests
- улучшить recovery messaging в UI

### v15 — Correct Analytics

- развить time-weighted metrics baseline
- добавить GPS/sensor outlier handling
- добавить event detection
- расширить metric correctness tests
- отделить raw vs cleaned data interpretation там, где это полезно

### v16 — Performance & Architecture

- продолжить splitting orchestration into scenario services
- сделать workspace facade тоньше
- уменьшить rebuild/materialization cost
- использовать profiling aggregates для следующего perf-cut
- добавить perf regression automation

### v17 — Map & Export

- разделить preview/export fidelity
- улучшить tile cache/proxy story
- добавить GPX/KML/GeoJSON export
- добавить heatmaps и bookmarks
- улучшить export/share сценарии

### v18 — Product Features

- advanced filters
- multi-ride comparison
- route overlays
- segment/lap workflows
- presets and richer summaries

### v19 — Release Engineering

- release pipeline
- packaging artifacts
- changelog discipline
- sample datasets
- crash-report bundle
- clearer repo-level release story

## Immediate next steps

На ближайшую итерацию я бы держал такой порядок:

1. Доработать import diagnostics и recovery states вокруг CSV/restore flows.
2. Закрыть следующий слой корректности аналитики: outlier filtering и derived metrics.
3. По profiling output выбрать следующий глубокий performance cut в chart/data pipeline.
4. Продолжить дробление `TelemetryWorkspaceCoordinator`, только там, где это реально уменьшает hidden coupling.
5. После этого расширять advanced filters и multi-session features.

## Критерии успеха

Roadmap можно считать успешно реализуемым, если проект дойдёт до состояния, где:

- реальные CSV открываются заметно надёжнее
- ошибки и partial success объясняются пользователю
- summary metrics вызывают больше доверия
- большие логи не ломают UX
- карта и графики остаются отзывчивыми на длинных поездках
- архитектура не деградирует обратно в central-god-object design
- README, ROADMAP и CHANGELOG остаются согласованными
