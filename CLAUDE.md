# Toy Car Bumper Arena

Аренная игра для **Poki**: игрушечные машинки толкают друг друга с круглой арены, последний оставшийся побеждает. Игрок против 5–7 ботов, без онлайна. Полный бриф, объём прототипа и порядок работы описаны в [prototype.md](prototype.md). Перед любой задачей сверяйся с ним, а особенно с разделом «Чего в прототипе нет».

Техническая основа и архитектура повторяют проект-образец `D:\Projects\Poki\poki-smup` (SMUP). Если не знаешь, как что-то устроить (состояния, экраны, аудио, сборка, выгрузка на Poki), сначала посмотри, как это сделано там, и делай так же.

## Стек

- **Unity 6.6 (`6000.6.6f1`)**, URP, сборка **Web (WebGL)**. Бриф предлагал Three.js + Rapier, но пользователь выбрал Unity, решение окончательное.
- **bugord-kit**: git-сабмодуль `External/bugord-kit`, подключён в `Packages/manifest.json` через `file:`.
  - `com.bugord.core`: `ILogger<T>`, пулы (`ComponentPool<T>`), `IKeyValueStorage`, `IApplicationFocus`, `AddBugordCore()`.
  - `com.bugord.app`: `AppLifetimeScope`, загрузочные задачи (`IBootTask`), машина состояний приложения, `IGamePlatform` (Poki или локальная заглушка), WebGL-шаблон Poki.
- **VContainer** (DI), **UniState** (состояния приложения), **UniTask** (async).
- **UIKit** на UI Toolkit (`ViewModel` + `View`, `builder.RegisterScreen<VM, V>()`). uGUI нет.
- **Super Console** (v0.6.1, Pro через `SUPERCONSOLE_PRO`): дев-консоль. Есть только в development-сборках.
- Физика: встроенный PhysX (Rigidbody + колайдеры). Ввод: **Input System** (`Assets/Input/InputSystem_Actions.inputactions`).
- Ассеты: Kenney (CC0). Импортируем **FBX**, потому что Unity читает его без дополнительных пакетов. Модели наборов используют общую текстуру-атлас `colormap`, её не дублируем.

## Структура

```
Assets/
  Scripts/                ToyCarArena.asmdef, пространство имён ToyCarArena.*
    App/                  GameScope, состояния приложения, LevelScene, BuildInfo
    Gameplay/             GameplayScope, SceneInjector, GameTuning, затем Cars/ Bots/ Arena/ Round/ Input/
    UI/  Audio/           появятся вместе с экранами и звуком
    Editor/               ToyCarArena.Editor.asmdef: WebGLBuild, BuildNumber, SceneMenu, SuperConsoleLinker
  Scenes/                 Boot.unity (GameScope), Game.unity (GameplayScope в корне)
  Data/<Kind>/            ScriptableObject-данные: Data/Gameplay/data_game_tuning.asset
  Art/  Prefabs/<Kind>/  UI/  Input/
  Settings/               UniversalRP + UniversalRP_Renderer, глобальные настройки URP
  WebGLTemplates/poki-template/   шаблон из кита (Tools > Bugord Kit > Install Poki WebGL Template)
  link.xml                сборки, которые не трогает stripping
External/bugord-kit/      сабмодуль кита
Tools/build-webgl.sh      сборка релиза в batch mode
Builds/  Library/  Temp/  Logs/  UserSettings/   не в git
```

Имена ассетов в snake_case с префиксом типа (`data_`, `mat_`, `sfx_`, `music_`, `tex_`), префабы в PascalCase.

## Архитектура

- **`GameScope`** (`Scripts/App`, в сцене `Boot`) наследует `AppLifetimeScope` кита. Кит регистрирует хранилище, логирование, платформу, UIKit (если задан `ScreenCatalog`) и машину состояний. Игра в `ConfigureGame` регистрирует свои состояния, экраны и сервисы и задаёт корневое состояние через `RegisterAppEntryPoint<T>()`.
- **`LevelScene`** загружает `Game` аддитивно внутри `LifetimeScope.EnqueueParent(parentScope)`, поэтому `GameplayScope` становится дочерним к `GameScope`. Состояния получают сервисы уровня через `levelScene.Resolve<T>()`. Геймплейный код о состояниях не знает.
- **`GameplayScope`** (корень `Game`) регистрирует всё, что нужно геймплею. Без родителя (сцена `Game` открыта напрямую) он сам вызывает `AddBugordCore()`, и сцена играется отдельно.
- **Внедрение в сцену**: компоненты получают зависимости методом `private void Construct(...)` с `[Inject]`. Вызывает его `SceneInjector` по имени, потому что в WebGL-сборке линкер вырезает атрибут `[Inject]`. Префабы создаются через `resolver.InstantiateInjected(...)`.
- **Состояния** (`Scripts/App`, `StateBase` / `StateBase<TPayload>`, `Execute` возвращает `Transition.GoTo<...>()`). Сейчас есть только `ArenaState`: грузит арену и ждёт. Раунд, результат и рестарт появятся на шаге 3.
- **Платформа**: только через `IGamePlatform` из кита (`GameplayStart/Stop`, `CommercialBreak`, `RewardedBreak`, `Measure`). При `POKI_ENABLED` это `PokiGamePlatform`, иначе `LocalGamePlatform`. Её инициализирует загрузочная задача кита. `gameLoadingFinished` вызывает сам загрузчик Poki из шаблона, когда Unity загрузилась; из игры его не вызываем.
- **`GameTuning`** (`data_game_tuning.asset`): все числа физики и геймплея в одном ассете, разделы Car, Boost, Bots, Round, Arena. Регистрируется в `GameplayScope`.
- **Ссылки на ассеты в сценах проверяем в сборке.** В WebGL-плеере обращение к null не падает сразу: `null.GetType()` читает мусор, и ошибка всплывает дальше (`Class::FromIl2CppType`, `VContainerException:  is not assignable from …` с пустым именем типа). Поэтому scope проверяет свои serialized-ссылки сам и бросает исключение с понятным текстом.

## Команды

```bash
# Обновить сабмодуль кита после клонирования
git submodule update --init

# Сборка релиза для Poki (редактор с этим проектом должен быть закрыт), отчёт в Builds/WebGL-report.txt
Tools/build-webgl.sh
Tools/build-webgl.sh --development

# Проверить сборку локально: именно localhost, а не 127.0.0.1 (его Poki SDK считает чужим хостингом)
cd Builds/WebGL && python -m http.server 8642   # затем http://localhost:8642/index.html (?inspector=1 для режима Poki Inspector)

# Подключён ли редактор (для управления сценами из CLI)
unity status
```

В редакторе: Tools > Build > WebGL (Poki) / WebGL Development (Poki); Shift+1 открывает Boot, Shift+2 открывает Game.

## Сборка для Poki

Настройки повторяют образец (рекомендации Poki для Unity), не меняй их без причины:
- Compression Disabled (Poki сжимает сам), Name Files As Hashes, Data Caching.
- Strip Engine Code, Managed Stripping High (`link.xml` сохраняет сборки для VContainer и SceneInjector).
- IL2CPP Code Generation: Faster (smaller) builds. Exceptions: Explicitly Thrown Only (UniTask нужны исключения отмены).
- Code Optimization Disk Size with LTO, его ставит `WebGLBuild` (`-noLTO` даёт просто Disk Size).
- Без сплэша Unity, без постобработки URP (у рендерера нет Post Process Data), без видео-шейдеров, без HDR, Run In Background выключен (релизная сборка без него не соберётся).
- `POKI_ENABLED` и `SUPERCONSOLE_PRO` в WebGL-дефайнах. Номер сборки берётся из `git rev-list --count HEAD`; на мелком клоне сборка падает, либо передай `-buildNumber`.

## Правила

- **Все числа геймплея хранятся в `GameTuning`**: скорость, масса, сила толчка, ускорение и его перезарядка, осыпание, длина раунда, число ботов. В коде их не хардкодим.
- **Poki только через `IGamePlatform`.** `gameplayStart` в начале раунда, `gameplayStop` при выбывании игрока, на экране результата и на паузе. `commercialBreak` между раундами, `rewardedBreak` за открытие машинки. Пока идёт реклама, игра и звук на паузе.
- Никаких внешних ссылок, сторонней аналитики и рекламы.
- Управление работает одинаково с клавиатуры (WASD/стрелки + пробел) и с тача (джойстик + кнопка). UI проверяем в портретной и в альбомной ориентации.
- Размер сборки держим маленьким: только нужные пакеты и модули движка, mono-аудио, сжатые текстуры. После каждой сборки смотрим `Builds/WebGL-report.txt`.
- В `Assets/` кладём только используемые модели Kenney, архивы паков целиком не кладём.
- Каждый ассет и скрипт коммитим вместе с его `.meta`.
- Если редактор подключён (`unity status`), сцены, префабы и `.asset` меняем через него (`unity command …`), а не правкой YAML вручную. Если редактор открыт без фокуса, в режиме Play ставь `Application.runInBackground = true`, иначе игра встаёт на паузу.
- Кит меняем в его собственном репозитории (сабмодуль), а не копиями здесь. Код, который понадобился второй игре, переносим в кит. Пример: `SceneInjector`, он скопирован из SMUP.
- Стиль кода как в образце:
  - фигурные скобки типов и членов на новой строке, у `if`/`foreach`/`try` на той же;
  - приватные поля без подчёркивания, присваиваются через `this.`;
  - `[SerializeField] private` с `[Tooltip]` и `[Header]`;
  - XML-комментарии на английском объясняют *зачем*.
- Коммит после каждого рабочего шага из «Порядка работы». Без строк об авторстве ИИ.
