# Сборка

## Что нужно

| Инструмент | Зачем | Примечание |
| --- | --- | --- |
| [.NET SDK 10](https://dotnet.microsoft.com/download) | сам лаунчер | `dotnet --version` должен показывать 10.x |
| Python 3.9+ | скрипт сборки релиза | обычный `python` из PATH |
| Visual Studio Build Tools с рабочей нагрузкой C++ | сборка запускающего exe | только для Windows-релиза, см. ниже |
| Git | подмодуль `Robust.LoaderApi` | |

Клонировать нужно вместе с подмодулем:

```bash
git clone --recurse-submodules <адрес репозитория>
```

Если уже склонировали без него:

```bash
git submodule update --init --recursive
```

## Быстрая проверка

Собрать и прогнать тесты — этого достаточно, чтобы убедиться, что всё на месте:

```bash
dotnet build SS14.Launcher.sln -c Debug
```

```bash
dotnet test SS14.Launcher.Tests/SS14.Launcher.Tests.csproj
```

Запустить лаунчер из отладочной сборки:

```bash
dotnet run --project SS14.Launcher/SS14.Launcher.csproj
```

В отладочной сборке лаунчер берёт данные из той же папки, что и релизный. Чтобы не трогать свои
настройки и не качать контент заново, задайте другую папку:

```bash
SS14_LAUNCHER_DATADIR="Airlock Launcher Dev" dotnet run --project SS14.Launcher/SS14.Launcher.csproj
```

На Windows в PowerShell то же самое пишется так: `$env:SS14_LAUNCHER_DATADIR = "Airlock Launcher Dev"`.

## Релиз под Windows

```bash
python publish.py windows --x64-only
```

Результат:

- `bin/publish/Windows/` — готовая папка: `Airlock Launcher.exe`, `bin_x64/`, `dotnet_x64/`, `zapret/`;
- `SS14.Launcher_Windows.zip` в корне — то, что выкладывается в релизы.

Без `--x64-only` собирается ещё и ARM64.

### Две вещи, на которых легко споткнуться

**`vswhere.exe` не в PATH.** Запускающий `Airlock Launcher.exe` собирается через NativeAOT, а тот ищет
компоновщик MSVC с помощью `vswhere`. Он лежит не в PATH, и сборка падает с `MSB3073 ... vswhere.exe не
является внутренней или внешней командой`. Лечится добавлением папки установщика Visual Studio в PATH
на время сборки (PowerShell):

```powershell
$env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\Installer;" + $env:PATH
```

**Git Bash и ключи `/p:`.** Вызовы вида `dotnet publish ... /p:Foo=Bar` из Git Bash падают с
`MSB1008: можно указать только один проект` — MSYS превращает `/p:` в путь. Любые команды `dotnet`
с такими ключами (и сам `publish.py`) запускайте из PowerShell или cmd.

### Про zapret в релизе

`publish.py` во время сборки скачивает последний релиз
[zapret-discord-youtube](https://github.com/Flowseal/zapret-discord-youtube) и кладёт его в
`bin/publish/Windows/zapret/`. В репозитории этих файлов нет.

- Если GitHub недоступен или упёрся в лимит запросов, сборка **не падает** — просто выходит без
  zapret, а лаунчер предложит скачать его сам.
- Если zapret в этот момент запущен, его файлы заняты; скрипт оставит их как есть и перезапишет
  остальное. Чтобы собрать начисто, остановите его кнопкой в лаунчере.

## Linux и macOS

```bash
python publish.py linux
```

```bash
python publish.py osx
```

Под Linux есть и nix-сборка: `nix build` (см. `flake.nix`).

## Структура

| Папка | Что это |
| --- | --- |
| `SS14.Launcher` | сам лаунчер: модели, вью-модели, представления, локали, темы |
| `SS14.Launcher.Bootstrap` | маленький нативный exe, который запускает лаунчер |
| `SS14.Launcher.Tests` | тесты (NUnit) |
| `SS14.Loader` | загрузчик, стартующий клиент игры |
| `Robust.LoaderApi` | подмодуль с интерфейсом загрузчика |
| `PublishFiles` | файлы, попадающие в релиз как есть (`.desktop`, бандл для macOS) |
| `publish.py` | сборка релизов |

## Куда смотреть в коде

- Пути и перенос данных при переименовании — `SS14.Launcher/LauncherPaths.cs`.
- Список серверов и работа с хабами — `SS14.Launcher/Models/ServerStatus/`.
- Загрузка движка и контента — `SS14.Launcher/Models/EngineManager/`, `Models/Updater*.cs`.
- Редактор персонажа — `SS14.Launcher/Models/Character/`.
- Обход блокировок — `SS14.Launcher/Models/Zapret/`.
- Темы — `SS14.Launcher/Theme/Palettes/`, список в `SS14.Launcher/ThemeManager.cs`.

Новая тема: скопируйте любой файл палитры, поменяйте цвета и добавьте имя файла в
`ThemeManager.Themes` и строку `theme-name-<имя в нижнем регистре>` в три файла локалей. Тест
`ThemePaletteTest` проверит, что палитра определяет тот же набор ключей, что и остальные.
