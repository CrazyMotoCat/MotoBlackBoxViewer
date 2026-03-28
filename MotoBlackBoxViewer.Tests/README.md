# MotoBlackBoxViewer.Tests

Тестовый проект для проверки базовой телеметрической логики.

## Что покрыто

- парсинг CSV с русскими заголовками;
- парсинг чисел с `,` и `.`;
- чтение файлов в UTF-8 и CP1251;
- расчет накопленной дистанции по GPS-точкам;
- расчет сводной статистики по логу.

## Как запустить

```bash
dotnet test MotoBlackBoxViewer.sln
```

или только тестовый проект:

```bash
dotnet test MotoBlackBoxViewer.Tests/MotoBlackBoxViewer.Tests.csproj
```
