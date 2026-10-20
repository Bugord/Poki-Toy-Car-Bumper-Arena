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
    App/                  GameScope, состояния RoundState и ResultState, LevelScene, RoundEvents (аналитика), BuildInfo
    Gameplay/             GameplayScope, GameTuning
      Cars/               Car (машинка в раунде), CarMovement (физика), PlayerDriver (ввод игрока → машинка), CarLook (цвет кузова и маркер игрока)
      Arena/              ArenaCamera, ArenaFloor (пол из кольцевых блоков строится из настроек Arena), ArenaCrumble (осыпание краёв)
      Input/              InputService, сгенерированный InputSystemActions
      Round/              ArenaRound (спавн, выбывание, KO, итог), RoundResult, StandaloneRound,
                          SessionProgress (ISessionProgress), PersonalBests + RoundRecord (рекорды)
      Bots/               BotDriver (выбирает цели и рулит машинками ботов), Bot (состояние и руление одного бота),
                          BotDifficulty (сложность раунда)
      Effects/            ArenaFeedback (что показать и сыграть на удар, ускорение, выбывание, осыпание), EffectPlayer
                          (мультяшные эффекты), EffectShapes (их мэши), HitStop
    UI/                   экраны UIKit: CurtainView + ScreenCurtain, HudView + HudControls (кнопка BOOST на таче),
                          RoundOverView; общий FadingView, CarColor (класс цвета машинки по индексу)
    Audio/                IAudioService + AudioService (пул AudioSource и музыка), AudioLibrary (звуки и уровни), Sound
    Editor/               ToyCarArena.Editor.asmdef: WebGLBuild, BuildNumber, SceneMenu, SuperConsoleLinker
  Scenes/                 Boot.unity (GameScope), Game.unity (GameplayScope в корне)
  Data/<Kind>/            ScriptableObject-данные: Data/Gameplay/data_game_tuning.asset, Data/Audio/data_audio_library.asset,
                          Data/Physics/phys_car
  Art/Models|Textures|Materials/   модели Kenney (FBX), атлас colormap, материалы
  Audio/Sfx|Music/        sfx_*_NN.ogg из Kenney Impact Sounds, Interface Sounds и Music Jingles (импорт в моно);
                          music_arena_loop.wav пока синтезированная заглушка: музыкальных петель у Kenney нет
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
- **Внедрение в сцену**: компоненты получают зависимости методом `private void Construct(...)` с `[Inject]`. Вызывает его `SceneInjector` из кита по имени, потому что в WebGL-сборке линкер вырезает атрибут `[Inject]`. Префабы создаются через `resolver.InstantiateInjected(...)`. В редакторе VContainer ещё и сам зовёт `Construct` у компонентов из `RegisterComponent` (`ArenaCamera`, `ArenaFloor`), поэтому `Construct` только сохраняет зависимости и может вызываться дважды. В плеере его зовёт только `SceneInjector`, уже после `Initialize` entry point'ов, поэтому их конструкторы и `Initialize` состояние сценовых компонентов не читают.
- **Состояния** (`Scripts/App`, `StateBase` / `StateBase<TPayload>`, `Execute` возвращает `Transition.GoTo<...>()`):
  - `RoundState` (корневое): грузит арену, если её нет, начинает раунд, зовёт `GameplayStart`, открывает HUD и убирает шторку. Ждёт `ArenaRound.Finished`, зовёт `GameplayStop`, записывает раунд в `SessionProgress` и `PersonalBests`, выжидает `Round.ResultDelay` (после выбывания игрока короче, `Round.OutResultDelay`) и переходит в `ResultState` с `RoundRecord` (итог и рекорды до раунда).
  - `ResultState`: экран RoundOver поверх арены, какой она закончилась. После PLAY AGAIN опускает шторку и грузит сцену заново, пока экран гаснет под шторкой, и возвращается в `RoundState`. От нажатия до управления около 0,7 с плюс загрузка сцены. `commercialBreak` между раундами встанет сюда же, под шторку, но пока его нет: пользователь отложил рекламу, чтобы она не влияла на первые метрики.
- **Раунд** (`ArenaRound`, entry point в `GameplayScope`): `Begin()` ставит машинки на круг `Arena.SpawnRadius` носом к центру, на высоте `Arena.SpawnHeight`. Машинка ниже `Round.FallHeight` выбывает (`Car.TakeOut`), ниже `Round.RemoveHeight` её убирают. Раунд кончается, когда выбыл игрок, когда он остался один или когда вышло время. Раунд одноразовый: следующий идёт в заново загруженной сцене. Кто рулит машинками, раунд не знает: `PlayerDriver` (ITickable) рулит `round.Player`, пока раунд идёт, а `BotDriver` рулит остальными. Машинок `1 + Bots.Count`, игрок первая, ближе всех к камере. После `InstantiateInjected` машинку ставит `CarMovement.Place`: интерполируемый Rigidbody просыпается там, где префаб создан (в начале координат), и возвращает transform туда же. Нынешний радиус арены берётся из `ArenaRound.ArenaRadius` (это `ArenaFloor.Radius`). Сцена `Game` без Boot играет раунды через `StandaloneRound`: он перезагружает сцену после каждого.
  - **KO**: машинка, выбывшая не позже `Round.KnockoutCreditWindow` секунд после удара другой (`Car.LastHitBy`, `LastHitTime`), выбита ею: `ArenaRound.KnockedOut(car, by)` сразу после `CarOut`, для всех машинок, не только игрока. Раунд считает `PlayerKnockouts` и `PlayerKnockedOutBy`, в `RoundResult` идут `Knockouts` и `KnockedOutBy` (индекс машинки, 1..N, он же её цвет; `RoundResult.Nobody`, если никто). Машинка из `RoundResult` не передаётся: она уходит вместе со сценой.
  - **Прогресс визита** (`ISessionProgress`: номер раунда и серия поражений, их читает сложность ботов). `SessionProgress` регистрирует `GameScope`, поэтому он переживает перезагрузки `Game`. Сдвигается, когда раунд решён, а не когда начат следующий: боты читают его при сборке сцены, до `Begin`. Сцена без Boot берёт статический `StandaloneRound.Session` (сбрасывается при старте Play Mode).
  - **Рекорды** (`PersonalBests`, в `GameScope`): лучшее место и больше всего KO за раунд, в `IKeyValueStorage` кита (`best_place`, `best_knockouts`). Первый раунд ставит место без «NEW BEST!».
- **UI** (UIKit на UI Toolkit, `Scripts/UI` + `Assets/UI`): стиль «Sunny Sticker», как в SMUP: белые и яркие наклейки с толстой navy-обводкой и жёсткой тенью снизу, весь текст Lilita One. Тень рисуется толстым нижним бордером, потому что box-shadow в UI Toolkit нет; `PillShape` из кита (`Bugord.App.UI`, вынесен из SMUP) перерисовывает такие элементы с круглыми нижними углами заливки и даёт `border-radius: 999px` круглые концы. Обводка снаружи букв (класс `stroke`): текст дважды, второй лейбл (`stroke__face`) без обводки поверх первого. Иконки (секундомер, машинки, конфетти) собраны из элементов, картинок нет. Размеры заданы в референсных пикселях 1080×1920, панель масштабируется по Expand. Экраны: `Curtain` (слой попапов, шторка между сценами, `ScreenCurtain`), `Hud` (сверху по центру пилюля с секундомером и временем, под ней пилюля пониже со стопкой круглых фишек внахлёст, по фишке на каждую машину, которая ещё в игре (цвет её кузова и белая машинка сверху; боты по порядку, фишка игрока последней, спереди справа, с белым кольцом, как маркер на арене), и «N LEFT»; при 10 секундах и меньше пилюля с временем краснеет и пульсирует раз в секунду; когда машина выбывает, её фишка подскакивает, крутится и исчезает сжатием (0,4 с), остальные съезжаются, цифра бьёт, пилюля подпрыгивает; под пилюлей с фишками с первого KO пилюля со звездой «KO N», подпрыгивает на каждом; на KO игрока наклейка KNOCKOUT! со звездой над точкой, где машинка ушла с края, в пределах экрана и не на HUD: впрыгивает с перелётом, держится 0,8 с и улетает вверх; на тач-экране внизу справа круглая кнопка BOOST: оранжевый круг в ней растёт с перезарядкой, готовая кнопка становится оранжевой и подпрыгивает), `RoundOver` (наклонный заголовок YOU WIN! с конфетти / KNOCKED OUT / TIME UP! и подзаголовок, после KO игрока вместо него «PUSHED OFF BY» и машинка цвета того бота на белом значке; карточка с местом на пилюле золото/серебро/бронза, «OF N CARS» с плашкой рекорда «BEST #2», временем на арене и строкой KNOCKOUTS с рекордом; побитый рекорд это золотая наклейка «NEW BEST!», она впрыгивает после карточки; место скрыто, пока машина одна; цвета машинок `car-color--N` в `ui_screens` (переменные `--car-*` в `ui_theme`, класс по индексу даёт `CarColor`) это цвета атласа там, куда `CarLook` кладёт UV кузова, их же носят фишки HUD; большая зелёная PLAY AGAIN внизу, под ней на десктопе подсказка «OR PRESS SPACE»; Space и Enter тоже жмут; заголовок, карточка и кнопка въезжают при показе). `FadingView` пропускает ввод только после того, как экран проявился. Макеты экранов (телефон и десктоп) лежат в Claude Design: https://claude.ai/artifact/TK9sXmFcdo281rei8sAv75
- **Платформа**: только через `IGamePlatform` из кита (`GameplayStart/Stop`, `CommercialBreak`, `RewardedBreak`, `Measure`). При `POKI_ENABLED` это `PokiGamePlatform`, иначе `LocalGamePlatform`. Её инициализирует загрузочная задача кита. `gameLoadingFinished` вызывает сам загрузчик Poki из шаблона, когда Unity загрузилась; из игры его не вызываем. Аналитику (`Measure`) шлют состояния через `RoundEvents`: `round / 1..10, 10plus / start`, затем `complete` (победа или дожил) или `fail`; `result / won|out|survived / outcome`, `result / place-N-rR / place` и `result / knockouts-N-rR / knockouts` (R это раунд 1, 2, 3 или 4plus), `result / 0-15…60-75 / time`, `result / pushed|fell / out-cause` (выбыв: столкнули или съехал сам), `result / boosts-0|boosts-1-2|boosts-3plus / boosts`; только в первом раунде `controls / keyboard|mouse|touch|gamepad|none / input` (чем впервые рулил) и `controls / move-0-3|move-3-10|move-10plus|never / first-move` (через сколько секунд); `result / play-again / visible`, затем `interact`. Чем рулит игрок и сколько ускоряется, считает `PlayerDriver` (`FirstInput`, `FirstMoveTime`, `Boosts`). Имена не меняем, по ним строятся отчёты; в событии не больше двух чисел (`GameEvent.MaxNumbers`), иначе Poki его выбросит.
- **`GameTuning`** (`data_game_tuning.asset`): все числа физики и геймплея в одном ассете, разделы Car, Controls, Boost, Bots, Round, Arena. Регистрируется в `GameplayScope`.
- **Машинка** (`CarMovement`) аркадная поверх Rigidbody. Её зовут `Drive(heading, amount)`: куда ехать и насколько сильно. Игрок и боты рулят одним API. Поворот через `angularVelocity.y` без перелёта, скорость вдоль носа тянется к `MaxSpeed`, боковой занос гасится `Grip`. Рулит только колёсами на арене: луч вниз по слою `Arena`. Удары и падение с края остаются за физикой.
- **Ускорение и удары**: `CarMovement.TryBoost()` срабатывает только на арене и после перезарядки `Boost.Cooldown`. Он добавляет `Boost.Impulse` туда, куда машинку ведут (без руления вдоль носа), а скорость против рывка сначала гасит, поэтому рывок от края так же силён, как атака. Ещё `Boost.Duration` секунд машинка держит эту линию на поднятой максимальной скорости. Удар (`Car.OnCollisionEnter`) добавляет поверх физики толчок `Car.BumpForce` × скорость, с которой въехала другая машинка (не больше скорости сближения), × отношение масс, а если она ускоряется, ещё × `Boost.BumpMultiplier`, а если это игрок, ещё × `Car.PlayerBumpMultiplier`. Толчок не разгоняет машинку быстрее `Car.MaxBumpSpeed`. Ударенная машинка теряет равновесие (`CarMovement.Stagger`) до `Car.HitStagger` секунд, меньше при слабом ударе: не газует, не рулит и держит только `Car.StaggerGrip` сцепления, поэтому толчок её действительно уносит, а не гаснет в сцеплении, пока она рулит обратно. Игрок теряет равновесие на долю `Car.PlayerStaggerScale` этого времени. Ускорение снимает потерю равновесия, это и есть спасение у края. Каждая машинка толкает только себя, поэтому выигрывает та, что таранит. `Car.Hit` (`CarHit`: другая машинка, точка, сила, `IsHitter`, ускорения) приходит обеим машинкам, его слушают эффекты, а `LastHitBy` нужен для KO. Пока машинка давит в другую (`OnCollisionStay`), `LastHitTime` обновляется, поэтому долгий толчок до края засчитывается.
- **Боты** (`BotDriver`, entry point в `GameplayScope`, + `Bot` на каждую машинку бота) рулят тем же `Drive` и `TryBoost`, что и игрок. Цель выбирает `BotDriver` раз в `Bots.RetargetInterval` (± `RetargetJitter`) или когда она выбыла: очки за близость (`DistanceWeight`), за близость к краю, откуда легко столкнуть (`EdgeWeight`), поправка за игрока (`PlayerWeight` сложности плюс личности, не выше `MaxPlayerWeight`), штраф за каждого бота, который уже гонится за той же машиной (`CrowdPenalty`), бонус нынешней цели, чтобы намерение читалось и от него можно было увернуться (`StickWeight`), бонус машине, которую ударили в последние `OpportunityTime` секунд и которая скользит наружу (`OpportunityWeight`), обида на того, кто последним ударил бота (`GrudgeWeight` в течение `GrudgeTime`; на игрока обижен не больше одного бота сразу), и случайная добавка (`PickNoise`). Бот целится в точку на `AimBehind` позади цели со стороны центра, с упреждением `AimLead`, чтобы удар выталкивал её к краю. Цель он видит с задержкой: между взглядами (`ReactionTime`) ведёт её по старой скорости, и курс чуть сбит (`AimError`), поэтому резкий поворот уводит от бота. Возврат к центру важнее атаки и сложностью не замедляется, иначе боты сами валятся с края и раунды скачут: если бот дальше `радиус − EdgeMargin` или окажется там через `LookAhead × caution` секунд, он едет к центру на полном газу. Отступы в метрах, поэтому на последнем диске 5 м осторожный бот держится середины. Когда цель прямо перед носом (`PushDistance`, `PushAngle`), бот её толкает и вперёд не заглядывает, иначе он отворачивал бы раньше, чем цель доедет до края; через `BoostDelay` ускоряется в неё, если сам не вылетит. Сползая с края, с шансом `EscapeBoostChance` ускоряется обратно.
  - **Сложность** (`BotDifficulty` из `ISessionProgress`, считается при загрузке сцены): блоки `Easy` и `Normal` смешиваются по уровню от 0 до 1. Первый раунд визита Easy, раунд `1 + RampRounds` (5-й) Normal. С `StreakStart` (2) поражений подряд уровень отступает к Easy на `StreakEase` за каждое, не больше `MaxStreakEase`. Рычаги: газ на машинку игрока `PlayerPace` (друг с другом боты дерутся на полном), `PlayerWeight`, `PlayerGrace` (столько секунд от начала раунда игрока не выбирают вовсе), `MaxPlayerHunters`, `ReactionTime`, `AimError`, `BoostDelay`, `EscapeBoostChance`. Цель брифа: новичок выигрывает первый раунд в 50% случаев и чаще, к пятому в 15–20%.
  - **Личности** это множители поверх общих чисел (`Bully`, `Careful`, `Opportunist`, `Clumsy`). Bully чаще выбирает игрока, рано ускоряется и смел у края. Careful держится середины и чаще спасается ускорением. Opportunist добивает ударенные машинки и пересматривает цель при каждом ударе. Clumsy мажет и изредка съезжает сам, но его `caution` и `escapeBoost` сильно не опускаем: самовылеты отнимают у игрока «это я его скинул». Набор задаёт `Bots.Mix` (берутся первые `Count`: на 6 ботов 2 Bully, 2 Careful, Opportunist и Clumsy), каждый раунд он перемешивается.
  - Вся случайность ботов идёт от `BotDriver.Seed`, у каждого бота свой `System.Random`, чтобы headless-симуляция могла повторить раунд.
  - Цифры прежней headless-симуляции (при игроке на уровне бота он выигрывает около 12% раундов, около 88% раундов решаются до конца таймера, боты сталкивают друг друга примерно 8 раз в минуту, машинка без управления вылетает примерно за 3,5 с) сняты до ускорения, осыпания, сложности и личностей. Их надо снять заново, нынешние числа сложности и личностей подобраны рассуждением.
- **Управление** (`PlayerDriver` + `InputService`):
  - клавиши и стик задают направление относительно камеры;
  - мышь с зажатой кнопкой ведёт машинку к курсору;
  - палец по умолчанию работает как плавающий джойстик (`Controls.TouchSteering`, вариант FollowFinger ведёт к пальцу как мышь).
  - Рулит только первый палец. Нажатия на экранные кнопки (`IOnScreenControls`) не рулят. Клик мыши в течение секунды после касания считается эхом тача и игнорируется.
  - Ускоряют пробел, правая кнопка мыши (левая рулит), south на геймпаде и кнопка BOOST на HUD. Кнопку HUD показывает только на тач-экране (мобильная платформа или уже было касание). `HudControls` (в `GameScope`) передаёт `InputService` нажатия кнопки и её место как `IOnScreenControls`. Сцена без Boot берёт `NoOnScreenControls`.
- **Вид машинок** (`CarLook` на `Car.prefab`, `Apply(index)` зовёт `ArenaRound.Spawn`; 0 игрок, 1..N боты): кузов красится сдвигом UV модели на другой квадрат атласа `colormap` (атлас это палитра плоских квадратов; множитель `_BaseColor` затемнил бы окна, бамперы и колёса), поэтому `mdl_car_racer.fbx` читаемая (Read/Write) и у каждой машинки свой клон меша. Квадрат по индексу (после последнего боты начинают заново с первого бота, цвет игрока только у игрока), цвета и размеры маркера лежат на компоненте в префабе, не в `GameTuning`: это внешний вид, геймплей их не читает. Игрока выделяет маркер: белое кольцо на полу под машинкой и стрелка над ней, обе с navy-обводкой (мэши строятся в коде, материалы `mat_player_marker*` Unlit). Маркер живёт в мире, а не на машинке, поэтому не кренится вместе с ней, стрелка смотрит в камеру, у выбывшей машинки маркер скрыт. Без коллайдеров, слой по умолчанию.
- **Осыпание краёв** (`ArenaFloor` в сцене + `ArenaCrumble`, entry point в `GameplayScope`): пол это сплошной диск `Arena.MinRadius` и кольца примерно по `Arena.CrumbleStep` до `Arena.Radius` (полоса делится на целое число колец), каждое кольцо порезано на блоки (длина блока, тряска и падение лежат на компоненте `ArenaFloor`: это внешний вид). Блоки только видны: коллайдер один, диск по внешнему краю колец, которые ещё не упали; при падении кольца он меняется на меньший, поэтому машинки не цепляются за швы, а машинка на падающем кольце падает. Rigidbody машинок не засыпает (`sleepThreshold = 0`), иначе стоящая машинка повисла бы над упавшим кольцом. `ArenaCrumble` идёт по часам раунда: через `CrumbleDelay` и дальше раз в `CrumbleInterval` крайнее кольцо трясётся `CrumbleWarning` секунд и падает. Трясущееся кольцо уже не входит в `ArenaFloor.Radius`, поэтому боты с него уезжают. После конца раунда новые кольца не начинают трястись, а уже трясущееся всё равно падает. События `RingWarning` и `RingFell` (внутренний и внешний радиус кольца) для эффектов и звука. Камера не двигается.
- **`ArenaCamera`** сохраняет угол из сцены и отъезжает вдоль взгляда так, чтобы арена (`Arena.Radius + ViewMargin`) целиком влезла и в портрет, и в альбом. `Shake(amount)` трясёт камеру поверх подогнанной позиции (по unscaled-времени); `TryScreenToGround` тряску вычитает, чтобы руление не дёргалось.
- **Эффекты** (`Gameplay/Effects`, объект `Effects` в сцене `Game`): `ArenaFeedback` слушает события раунда, машинок и осыпания и решает, что показать и сыграть: на удар (один раз, со стороны `CarHit.IsHitter`) звёзды, кольцо на полу и пуфы по силе удара, тяжёлый удар со вспышкой и тряской, а с игроком ещё и hit-stop (`Time.timeScale` на 60–80 мс, не накладывается, всегда восстанавливается); на ускорение пуфы сзади, полосы скорости и пыль; на выбывание всплеск у края, на KO игрока (`ArenaRound.KnockedOut`) золотые искры; на осыпание комья пыли по кольцу и облака с обломками при падении. Ещё он запускает музыку, тикает последние 10 секунд в такт HUD и играет джингл победы или поражения. `EffectPlayer` рисует плоские unlit-фигуры с толстой navy-обводкой (`mat_fx_*`, общий размер всех фигур задаёт `sizeScale`) через `Graphics.DrawMesh` из буфера структур: без ParticleSystem, без объектов и аллокаций, исчезают сжатием, а не прозрачностью; время unscaled, поэтому во время hit-stop звёзды летят. Числа эффектов и ощущений лежат на компонентах (это вид, не геймплей).
- **Звук** (`Scripts/Audio`): `IAudioService` живёт в `GameScope` (объект `Audio` в DontDestroyOnLoad с единственным AudioListener; у камеры в `Game` его нет), поэтому музыка не прерывается при перезагрузке `Game`; сцена `Game` без Boot регистрирует свой. Звуки и уровни в `data_audio_library` (`Sound`: дубли клипа, громкость, разброс высоты). `Play(sound, volume)`, `PlayJingle` (приглушает музыку), `PlayMusic`, `PlayUiClick` для кнопок экранов, `IsPaused` (это `AudioListener.pause`, его же ставит `PokiGamePlatform` на время рекламы). Все звуки 2D.
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
- **Poki только через `IGamePlatform`.** `gameplayStart` в начале раунда, `gameplayStop` при выбывании игрока, на экране результата и на паузе. `commercialBreak` между раундами (на первом тесте Poki он намеренно выключен, см. prototype.md), `rewardedBreak` за открытие машинки. Пока идёт реклама, игра и звук на паузе.
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
- **Ветки по gitflow:**
  - `main` хранит только выпущенные версии: то, что загружено на Poki. Напрямую в неё не коммитим.
  - `develop` это текущая разработка, от неё начинается любая работа.
  - Задача ведётся в `feature/<коротко-что>` (например, `feature/car-abilities`) от `develop` и вливается обратно в `develop` через PR. Мелкая правка документации может идти прямо в `develop`.
  - Сборка для Poki готовится в `release/<версия>` (например, `release/0.1`) от `develop`: там только правки к выпуску. Готовый релиз вливается в `main` с тегом `v<версия>` и обратно в `develop`.
  - Срочное исправление выпущенной версии делается в `hotfix/<версия>` от `main` и вливается в `main` (с тегом) и в `develop`.
  - Чужую историю не переписываем: в общих ветках без rebase и force-push, вливаем merge-коммитом.
  - Влитые ветки `feature/*`, `release/*` и `hotfix/*` удаляем.
