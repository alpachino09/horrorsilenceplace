# Human Motion 0.4.1 — результат и границы проверки

Версия 0.4.1 заменяет устаревший `DEVELOPMENT_BUILD` в пяти файлах диагностики
на `UNITY_INCLUDE_INSTRUMENTATION`, включая Conditional-атрибуты.
Согласованность вызывающего кода и диагностических API проверена в семи
конфигурациях компиляции: Editor, Debug, Checked, Instrumented и Release.
Сцены, модели, механики взаимодействия и сетевой формат анимаций этим исправлением
не меняются. Повторная генерация мира для устранения UAC0009 не требуется.

## A. Реализовано

В существующий проект добавлены генерируемый Animator Controller с реальными
клипами, отдельные idle/gait states, directional Blend Tree, переходы приседа,
carry/wounded, jump/fall/land/death, повороты и 180° pivot. Длинная Idle-запись
не растягивает цикл ходьбы: она вынесена из движущегося Blend Tree. Скорость
клипов калибруется по движению стоп именно на целевом Humanoid Avatar.

Добавлены общий профиль, отложенное направление **графического** тела, умеренная
коррекция наклона, ограниченный head look, фазовый foot IK, коррекция таза,
краткая фиксация стопы относительно поверхности и восстановление целей IK.
Камера не получает анимационного вращения. Управление расширяет старый motor,
не создаёт второй Player Controller; плавный разгон/остановка и камера приседа
включаются только на новом скопированном player prefab. Старый CanStand сохранён.

Успешные действия E публикуют presentation cue через существующую authority.
Рука проверяет фактическую досягаемость. Чужие сильные Impact в пределах слышимости
могут запустить взгляд на источник с cooldown; случайных невидимых угроз нет.
Trip доступен через событийный API и тестовую кнопку, без добавления несуществующих
ловушек/случайных спотыканий и без произвольного отъёма управления.

Единый источник gameplay-шума шагов остаётся в FirstPersonMotor/Game.Emit.
Контактный bridge временно заменяет distance-based выбор момента, автоматически
возвращая fallback при неработающем Animator. Общая защита не даёт двум вариантам
emit шаг подряд при переключении. Добавлен Gravel, прежние SurfaceKind ID сохранены;
аудиомассив теперь учитывает все enum значения и проверяет границы.

## B. Какие клипы подключает генератор

В рабочей папке действительно извлечены все 23 исходных `.anim`. Подключение
Unity-ссылок и создание копий выполняет меню 25, поскольку облачного Editor нет.

| Basic Motions — 14 клипов игрового controller | Использование |
|---|---|
| Idle | одна из двух idle-вариаций, разная фаза для участников |
| Walk | ходьба и осторожная ходьба с согласованием скорости |
| Run, Run Fast | jog/run и быстрый бег |
| Run Backward, Run Left, Run Right | направления бега; right сохраняет mirror |
| Turn Left, Turn Right | поворот из idle |
| Walking Turn 180, Run Turn 180 | pivot; направление может зеркалиться |
| Jumping Up | положительная вертикальная скорость прыжка |
| Tripping | событийная визуальная реакция Trip |
| Dying | смерть, без повторного запуска на каждом кадре |

13 собственных fallback-клипов из подготовленного Avatar: **Idle, WalkBack,
StrafeLeft, StrafeRight, CrouchIdle, CrouchWalk, CarryIdle, CarryWalk, WoundedIdle,
WoundedWalk, Fall, Land, UseItem**. Все копируются отдельно; исходники сохраняются.

Остальные 9 Basic Motions доступны **только отдельному просмотру**, не
автоматическому геймплею: Angry, Climbing Down Wall, Happy, Jumping Down,
Pointing Forward, Run Up Stairs, Sad, Walk Sad, Yelling. Их нет оснований выдавать
за crouch, pickup, hit или полноценную механику climbing. Файлов Quantum Stage нет.

## C. Персонажи

Цель — нынешняя HoodedGirl, используемая всеми четырьмя участниками. Обновление
обходит человеческие PlayerContext prefab из текущего реестра и готовит общую
архитектуру для каждого. Несовместимый Avatar/отсутствующий собственный fallback
вызывает понятный отказ до замены сцены. Сетевые props и монстр сохраняются.
Демонстрационный манекен PolyOne в игру не устанавливается.

Облачный код **не менял файлы на Mac сам**. Уже созданные на Mac prefab/scene
недоступны этой среде, поэтому нельзя честно утверждать, что пользовательский
мир уже получил новую систему до выполнения меню 25.

## D. Фонарик

Используются те же FlashlightController, батарейный state machine, snapshot,
FlashlightAnimationController и калибровочные маркеры. Новая locomotion не
пересоздаёт батарейки и не меняет момент их расхода. UpperBody mask исключает
правую руку; последовательности failure/reload получают приоритет над reach
левой руки. IK выполняется на последнем активном слое, а не до override-позы.

Новая модель фонарика, новая записанная mocap-последовательность для его
разрядки/батареек **не созданы**. Ранее запрошенный настоящий USDZ не поступил;
контакт ладоней, cap/cells и визуальная реалистичность требуют реальной модели
и калибровки в Unity. Заглушка-примитив не добавлена.

## E. Кооператив

Расширен существующий PlayerAvatarNetwork: server-confirmed planar velocity,
vertical speed и grounded/crouched/carry flags, до 20 Hz. Равные snapshots не
становятся новыми NetworkVariable updates. Надёжные server-only RPC передают
значимые animation cues; Host не проигрывает их вторично. Health/death и
flashlight по-прежнему приходят своими существующими snapshots. Bone transforms
по сети не передаются. Новая сцена получает NGO protocol >= 7 и отдельный реестр.
Общий admission token обновлён до `SILENCE-GAME-4`: старые сборки отказываются
ещё до появления игрока, даже при запуске сохранённой старой сцены.

Это реализованный код обмена; фактическое испытание четырёх экземпляров Unity
или сборок здесь не выполнялось.

## F. Выполненные проверки

- **170** настоящих NUnit/.NET тестов Core — без ошибок, включая 19 новых случаев
  приоритета реакций, углов/поворотов, контактов, переходного idle blend и гравия.
- **9** Python-тестов безопасности распаковки — без ошибок.
- Фактический разбор 23 файлов: 2 990 Humanoid curves, 87 234 конечных ключа,
  сохранённый mirror, исходные hashes/GUID.
- Фактический импорт демонстрационного исходного FBX в Blender 5.2.3:
  65 костей, 52 mapping, отсутствующих назначенных костей нет.
- C# 9 syntax в пяти конфигурациях и структурные/asmdef проверки — см. актуальные
  `csharp-syntax.txt`/`structure.json`; это **не Unity compilation**.
- Новые API проверены по открытому UnityCsReference **6000.6.7f1**.
- Архив обновления проверяется на состав, hashes, отсутствие сторонних assets,
  совместимость со старым updater и сохранение `.meta`/Generated.

Четыре NativeHumanMotionTests подготовлены для Unity, но **не запускались**.
Отдельно не проверены: native compilation/ILPP, состояние Animator в Play,
плавность/биомеханика, посадка стоп и рук на видимой модели, FPS, камера,
физическая проходимость двери после обновления и реальный кооператив.

## G. Что остаётся

Нет специальных mocap Crouch Enter/Exit, crouch backward/strafe/turn, start/stop,
hit/startle и отдельных поворотов 45/135°. Сейчас используются существующая
пара crouch и blending; для бокового/заднего приседа **пока остаётся прежний
общий CrouchWalk, это не полноценная направленная анимация**. Нужны подходящие
исходные клипы и их визуальная приёмка. Hit меняет существующее wounded state
и взгляд; отдельный новый flinch-клип не выдуман. Раненый быстрый бег также
требует отдельной визуальной проверки/подходящего клипа.

Не реализованы полноценный Motion Matching, новые gameplay-падения/ловушки,
случайные реакции на монстра без сигнала, несуществующие рычаги/ящики.
Не заявляется финальная AAA-реалистичность, устранение всего foot sliding
или прохождение всей приёмки задания. Дальнейшие native-проверки перечислены
в [NATIVE_CHECKS_RU.md](NATIVE_CHECKS_RU.md).

## H. Файлы

Новые основные исходники:

- `Assets/_Silence/Scripts/Core/CharacterAnimationModel.cs`
- `Assets/_Silence/Scripts/Player/PlayerAnimationCue.cs`
- `Assets/_Silence/Scripts/Player/ILeftHandIKClaim.cs`
- `Assets/_Silence/Scripts/Player/PlayerAnimatorIK.cs`
- `Assets/_Silence/Graphics/Runtime/Players/PlayerAnimationProfile.cs`
- `Assets/_Silence/Graphics/Runtime/Players/PlayerFootContacts.cs`
- `Assets/_Silence/Graphics/Runtime/Players/CharacterIKController.cs`
- `Assets/_Silence/Graphics/Runtime/Players/CharacterAnimationActions.cs`
- `Assets/_Silence/Graphics/Runtime/Players/FootstepAnimationBridge.cs`
- `Assets/_Silence/Graphics/Runtime/Players/HumanMotionLab.cs`
- `LocalPackages/com.silence.multiplayer/Editor/Visual/BasicMotionsCatalog.cs`
- `LocalPackages/com.silence.multiplayer/Editor/Visual/AnimationClipPreparation.cs`
- `LocalPackages/com.silence.multiplayer/Editor/Visual/PlayerAnimationControllerBuilder.cs`
- `LocalPackages/com.silence.multiplayer/Editor/Visual/PlayerAnimationWorkshop.cs`
- `LocalPackages/com.silence.multiplayer/Editor/Visual/AnimationLabBuilder.cs`
- `Assets/_Silence/Tests/EditMode/CharacterAnimationTests.cs`
- `Assets/_Silence/Tests/AnimationEditor/NativeHumanMotionTests.cs` и тестовая asmdef
- `Tools/Animation/` — аудит, проверка архива и сборка обновления
- `Docs/Animation/`, `Docs/Validation/Animation/` — задание, инструкции, результаты

Изменены: `PlayerAvatarPresentation.cs`, `PlayerAvatarState.cs`, `PlayerContext.cs`,
`FirstPersonMotor.cs`, `PlayerInputReader.cs`, `SilenceControls.inputactions`,
`AcousticModels.cs`, `SessionAdmission.cs`, `WorldAudio.cs`, `GameplayDirector.cs`,
`FlashlightAnimationController.cs`, `OfflineWorldAuthority.cs`,
`NetworkWorldAuthority.cs`, `NetworkPlayer.cs`, `PlayerAvatarNetwork.cs`,
`SilenceSourceUpdater.cs`, `.gitignore`, `Tools/CoreTests/Silence.CoreTests.csproj`,
`Tools/verify_structure.py`.

Фактические Unity prefab/controller/scenes создаются **в Editor по кнопке 25**;
их точные имена и paths приведены в [инструкции установки](README_RU.md).
Их нельзя считать созданными/проверенными в облаке, где нет Unity.
