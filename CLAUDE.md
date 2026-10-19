# Toy Car Bumper Arena

Аренная игра для **Poki**: игрушечные машинки толкают друг друга с круглой арены, последний оставшийся побеждает. Игрок против 5–7 ботов, без онлайна. Полный бриф, объём прототипа и порядок работы описаны в [prototype.md](prototype.md). Перед любой задачей сверяйся с ним, а особенно с разделом «Чего в прототипе нет».

Техническая основа и архитектура повторяют проект-образец `D:\Projects\Poki\poki-smup` (SMUP). Если не знаешь, как что-то устроить (состояния, экраны, аудио, сборка, выгрузка на Poki), сначала посмотри, как это сделано там, и делай так же.

## Стек

- **Unity 6.6 (`6000.6.6f1`)**, URP, сборка **Web (WebGL)**. Бриф предлагал Three.js + Rapier, но пользователь выбрал Unity, решение окончательное.
- **bugord-kit**: git-сабмодуль `External/bugord-kit`, подключён в `Packages/manifest.json` через `file:`.
  - `com.bugord.core`: `ILogger<T>`, пулы (`ComponentPool<T>`), `IKeyValueStorage`, `IApplicationFocus`, `AddBugordCore()`, `SceneInjector` (`Bugord.Core`, интеграция с VContainer).
  - `com.bugord.app`: `AppLifetimeScope`, загрузочные задачи (`IBootTask`), машина состояний приложения, `IGamePlatform` (Poki или локальная заглушка), `PillShape` (`Bugord.App.UI`), WebGL-шаблон Poki.
- **VContainer** (DI), **UniState** (состояния приложения), **UniTask** (async).
- **UIKit** на UI Toolkit (`ViewModel` + `View`, `builder.RegisterScreen<VM, V>()`). uGUI нет.
- **Super Console** (v0.6.1, Pro через `SUPERCONSOLE_PRO`): дев-консоль. Есть только в development-сборках.
- Физика: встроенный PhysX (Rigidbody + колайдеры). Ввод: **Input System** (`Assets/Input/InputSystem_Actions.inputactions`).
- Ассеты: Kenney (CC0). Импортируем **FBX**, потому что Unity читает его без дополнительных пакетов. Модели наборов используют общую текстуру-атлас `colormap`, её не дублируем.

## Структура

```
Assets/
  Scripts/                ToyCarArena.asmdef, пространство имён ToyCarArena.*
    App/                  GameScope, состояния RoundState и ResultState, LevelScene, BuildInfo
    Gameplay/             GameplayScope, GameTuning
      Cars/               Car (машинка в раунде), CarMovement (физика), PlayerDriver (ввод игрока → машинка), CarLook (цвет кузова и маркер игрока)
      Arena/              ArenaCamera, ArenaFloor (диск арены строится из Arena.Radius)
      Input/              InputService, сгенерированный InputSystemActions
      Round/              ArenaRound (спавн, выбывание, итог), RoundResult, StandaloneRound
      Bots/               BotDriver (выбирает цели и рулит машинками ботов), Bot (состояние и руление одного бота)
    UI/                   экраны UIKit: CurtainView + ScreenCurtain, HudView, RoundOverView; общий FadingView
    Audio/                появится вместе со звуком
    Editor/               ToyCarArena.Editor.asmdef: WebGLBuild, BuildNumber, SceneMenu, SuperConsoleLinker
  Scenes/                 Boot.unity (GameScope), Game.unity (GameplayScope в корне)
  Data/<Kind>/            ScriptableObject-данные: Data/Gameplay/data_game_tuning.asset, Data/Physics/phys_car
  Art/Models|Textures|Materials/   модели Kenney (FBX), атлас colormap, материалы
  Prefabs/<Kind>/         Prefabs/Cars/Car.prefab
  UI/                     ui_theme.tss (палитра, шрифт), ui_screens.uss, Screens/*.uxml, Fonts/ (Lilita One),
                          data_screen_catalog и ui_panel_settings (заданы у GameScope в Boot)
  Input/
  Settings/               UniversalRP + UniversalRP_Renderer, глобальные настройки URP
  WebGLTemplates/poki-template/   шаблон из кита (Tools > Bugord Kit > Install Poki WebGL Template)
  link.xml                сборки, которые не трогает stripping
External/bugord-kit/      сабмодуль кита
Tools/build-webgl.sh      сборка релиза в batch mode
Builds/  Library/  Temp/  Logs/  UserSettings/   не в git
```

Имена ассетов в snake_case с префиксом типа (`data_`, `mat_`, `mdl_`, `tex_`, `phys_`, `sfx_`, `music_`), префабы в PascalCase. Физматериал в Unity 6.6 имеет расширение `.physicMaterial`: с `.physicsMaterial` ассет уходит в DefaultImporter.

## Архитектура

- **`GameScope`** (`Scripts/App`, в сцене `Boot`) наследует `AppLifetimeScope` кита. Кит регистрирует хранилище, логирование, платформу, UIKit (если задан `ScreenCatalog`) и машину состояний. Игра в `ConfigureGame` регистрирует свои состояния, экраны и сервисы и задаёт корневое состояние через `RegisterAppEntryPoint<T>()`.
- **`LevelScene`** загружает `Game` аддитивно внутри `LifetimeScope.EnqueueParent(parentScope)`, поэтому `GameplayScope` становится дочерним к `GameScope`. Состояния получают сервисы уровня через `levelScene.Resolve<T>()`. Геймплейный код о состояниях не знает.
- **`GameplayScope`** (корень `Game`) регистрирует всё, что нужно геймплею. Без родителя (сцена `Game` открыта напрямую) он сам вызывает `AddBugordCore()`, и сцена играется отдельно.
- **Внедрение в сцену**: компоненты получают зависимости методом `private void Construct(...)` с `[Inject]`. Вызывает его `SceneInjector` из кита по имени, потому что в WebGL-сборке линкер вырезает атрибут `[Inject]`. Префабы создаются через `resolver.InstantiateInjected(...)`.
- **Состояния** (`Scripts/App`, `StateBase` / `StateBase<TPayload>`, `Execute` возвращает `Transition.GoTo<...>()`):
  - `RoundState` (корневое): грузит арену, если её нет, начинает раунд, зовёт `GameplayStart`, открывает HUD и убирает шторку. Ждёт `ArenaRound.Finished`, зовёт `GameplayStop`, выжидает `Round.ResultDelay` и переходит в `ResultState` с `RoundResult`.
  - `ResultState`: экран RoundOver поверх арены, какой она закончилась. После PLAY AGAIN опускает шторку, грузит сцену заново и возвращается в `RoundState`. Реклама между раундами (шаг 7) встанет сюда же, под шторку.
- **Раунд** (`ArenaRound`, entry point в `GameplayScope`): `Begin()` ставит машинки на круг `Arena.SpawnRadius` носом к центру, на высоте `Arena.SpawnHeight`. Машинка ниже `Round.FallHeight` выбывает (`Car.TakeOut`), ниже `Round.RemoveHeight` её убирают. Раунд кончается, когда выбыл игрок, когда он остался один или когда вышло время. Раунд одноразовый: следующий идёт в заново загруженной сцене. Кто рулит машинками, раунд не знает: `PlayerDriver` (ITickable) рулит `round.Player`, пока раунд идёт, а `BotDriver` рулит остальными. Машинок `1 + Bots.Count`, игрок первая, ближе всех к камере. После `InstantiateInjected` машинку ставит `CarMovement.Place`: интерполируемый Rigidbody просыпается там, где префаб создан (в начале координат), и возвращает transform туда же. Нынешний радиус арены берётся из `ArenaRound.ArenaRadius`; пока это `Arena.Radius`, осыпание (шаг 5) будет менять только его. Сцена `Game` без Boot играет раунды через `StandaloneRound`: он перезагружает сцену после каждого.
- **UI** (UIKit на UI Toolkit, `Scripts/UI` + `Assets/UI`): стиль «Sunny Sticker», как в SMUP: белые и яркие наклейки с толстой navy-обводкой и жёсткой тенью снизу, весь текст Lilita One. Тень рисуется толстым нижним бордером, потому что box-shadow в UI Toolkit нет; `PillShape` из кита (`Bugord.App.UI`, вынесен из SMUP) перерисовывает такие элементы с круглыми нижними углами заливки и даёт `border-radius: 999px` круглые концы. Обводка снаружи букв (класс `stroke`): текст дважды, второй лейбл (`stroke__face`) без обводки поверх первого. Иконки (секундомер, машинки, конфетти) собраны из элементов, картинок нет. Размеры заданы в референсных пикселях 1080×1920, панель масштабируется по Expand. Экраны: `Curtain` (слой попапов, шторка между сценами, `ScreenCurtain`), `Hud` (сверху по центру пилюля с секундомером и временем, под ней плашка с машинкой на каждую машину раунда, выбывшие тусклые, и «N LEFT»; при 10 секундах и меньше пилюля краснеет и пульсирует раз в секунду, плашка подпрыгивает, когда машина выбывает), `RoundOver` (наклонный заголовок YOU WIN! с конфетти / KNOCKED OUT / TIME UP! и подзаголовок; карточка с местом на пилюле золото/серебро/бронза, «OF N CARS» и временем на арене, место скрыто, пока машина одна; большая зелёная PLAY AGAIN внизу, под ней на десктопе подсказка «OR PRESS SPACE»; Space и Enter тоже жмут; заголовок, карточка и кнопка въезжают при показе). `FadingView` пропускает ввод только после того, как экран проявился. Макеты экранов (телефон и десктоп) лежат в Claude Design: https://claude.ai/artifact/TK9sXmFcdo281rei8sAv75
- **Платформа**: только через `IGamePlatform` из кита (`GameplayStart/Stop`, `CommercialBreak`, `RewardedBreak`, `Measure`). При `POKI_ENABLED` это `PokiGamePlatform`, иначе `LocalGamePlatform`. Её инициализирует загрузочная задача кита. `gameLoadingFinished` вызывает сам загрузчик Poki из шаблона, когда Unity загрузилась; из игры его не вызываем.
- **`GameTuning`** (`data_game_tuning.asset`): все числа физики и геймплея в одном ассете, разделы Car, Controls, Boost, Bots, Round, Arena. Регистрируется в `GameplayScope`.
- **Машинка** (`CarMovement`) аркадная поверх Rigidbody. Её зовут `Drive(heading, amount)`: куда ехать и насколько сильно. Игрок и боты рулят одним API. Поворот через `angularVelocity.y` без перелёта, скорость вдоль носа тянется к `MaxSpeed`, боковой занос гасится `Grip`. Рулит только колёсами на арене: луч вниз по слою `Arena`. Удары и падение с края остаются за физикой.
- **Боты** (`BotDriver`, entry point в `GameplayScope`, + `Bot` на каждую машинку бота) рулят тем же `Drive`, что и игрок, всегда на полном газу. Цель выбирает `BotDriver` раз в `Bots.RetargetInterval` (± `RetargetJitter`) или когда она выбыла: очки за близость (`DistanceWeight`), за близость к краю, откуда легко столкнуть (`EdgeWeight`), поправка за игрока (`PlayerWeight`, чуть ниже нуля, чтобы новичка не травили; первые `PlayerGrace` секунд раунда игрока не выбирают вовсе, чтобы новичок успел найти свою машинку и управление), штраф за каждого бота, который уже гонится за той же машиной (`CrowdPenalty`), и случайная добавка (`PickNoise`). Бот целится в точку на `AimBehind` позади цели со стороны центра, с упреждением `AimLead`, чтобы удар выталкивал её к краю. Возврат к центру важнее атаки: если бот дальше `радиус − EdgeMargin` или окажется там через `LookAhead × caution` секунд (caution у каждого бота свой, из `Caution`), он едет к центру. Когда цель прямо перед носом (`PushDistance`, `PushAngle`), бот её толкает и вперёд не заглядывает, иначе он отворачивал бы раньше, чем цель доедет до края. Подобрано headless-симуляцией: при игроке на уровне бота он выигрывает около 12% раундов, около 88% раундов решаются до конца таймера, боты сталкивают друг друга примерно 8 раз в минуту; машинка без управления вылетает примерно за 3,5 с.
- **Управление** (`PlayerDriver` + `InputService`):
  - клавиши и стик задают направление относительно камеры;
  - мышь с зажатой кнопкой ведёт машинку к курсору;
  - палец по умолчанию работает как плавающий джойстик (`Controls.TouchSteering`, вариант FollowFinger ведёт к пальцу как мышь).
  - Рулит только первый палец. Нажатия на экранные кнопки (`IOnScreenControls`) не рулят. Клик мыши в течение секунды после касания считается эхом тача и игнорируется.
- **Вид машинок** (`CarLook` на `Car.prefab`, `Apply(index)` зовёт `ArenaRound.Spawn`; 0 игрок, 1..N боты): кузов красится сдвигом UV модели на другой квадрат атласа `colormap` (атлас это палитра плоских квадратов; множитель `_BaseColor` затемнил бы окна, бамперы и колёса), поэтому `mdl_car_racer.fbx` читаемая (Read/Write) и у каждой машинки свой клон меша. Квадрат по индексу, цвета и размеры маркера лежат на компоненте в префабе, не в `GameTuning`: это внешний вид, геймплей их не читает. Игрока выделяет маркер: белое кольцо на полу под машинкой и стрелка над ней, обе с navy-обводкой (мэши строятся в коде, материалы `mat_player_marker*` Unlit). Маркер живёт в мире, а не на машинке, поэтому не кренится вместе с ней, стрелка смотрит в камеру, у выбывшей машинки маркер скрыт. Без коллайдеров, слой по умолчанию.
- **`ArenaCamera`** сохраняет угол из сцены и отъезжает вдоль взгляда так, чтобы арена (`Arena.Radius + ViewMargin`) целиком влезла и в портрет, и в альбом.
- **Ссылки на ассеты в сценах проверяем в сборке.** В WebGL-плеере обращение к null не падает сразу: `null.GetType()` читает мусор, и ошибка всплывает дальше (`Class::FromIl2CppType`, `VContainerException:  is not assignable from …` с пустым именем типа). Поэтому scope проверяет свои serialized-ссылки сам и бросает исключение с понятным текстом. Тот же текст ошибки в релизной сборке (не в development) даёт и дженерик `builder.RegisterInstance<T>(obj)`, когда ссылка жива. Поэтому ScriptableObject регистрируем недженерик-перегрузкой `builder.RegisterInstance(obj, typeof(T))`.

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
- Управление работает с клавиатуры (WASD/стрелки + пробел), с мыши и с тача (джойстик + кнопка). Большинство игроков Poki играют мышью или пальцем, поэтому без клавиатуры игра должна оставаться полноценной. UI проверяем в портретной и в альбомной ориентации.
- Проверка управления в браузерной панели: эмуляция телефона шлёт мышь, а не касания. Тач проверяем синтетическими `TouchEvent` на canvas, долгое нажатие мыши проверяем синтетическими `mousedown`/`mousemove`, потому что `left_click_drag` слишком короткий.
- Одноразовые правки ассетов при закрытом редакторе делаем временным editor-скриптом через `Unity.exe -batchmode -quit -nographics -projectPath . -executeMethod …`, потом удаляем его. Editor-сборка не может называть типы, унаследованные от VContainer (CS0012), поэтому такие компоненты берём через `GetComponent(typeof(GameplayScope))` в переменную типа `Component`.
- Размер сборки держим маленьким: только нужные пакеты и модули движка, mono-аудио, сжатые текстуры. После каждой сборки смотрим `Builds/WebGL-report.txt`.
- В `Assets/` кладём только используемые модели Kenney, архивы паков целиком не кладём.
- Каждый ассет и скрипт коммитим вместе с его `.meta`.
- Если редактор подключён (`unity status`), сцены, префабы и `.asset` меняем через него (`unity command …`), а не правкой YAML вручную. Если редактор открыт без фокуса, в режиме Play ставь `Application.runInBackground = true`, иначе игра встаёт на паузу.
- Кит меняем в его собственном репозитории (сабмодуль), а не копиями здесь. Код, который понадобился второй игре, переносим в кит. Пример: `SceneInjector` и `PillShape`, они начинались в SMUP.
- Стиль кода как в образце:
  - фигурные скобки типов и членов на новой строке, у `if`/`foreach`/`try` на той же;
  - приватные поля без подчёркивания, присваиваются через `this.`;
  - `[SerializeField] private` с `[Tooltip]` и `[Header]`;
  - XML-комментарии на английском объясняют *зачем*.
- Коммит после каждого рабочего шага из «Порядка работы». Без строк об авторстве ИИ.
