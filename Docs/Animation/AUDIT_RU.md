# Аудит перед внедрением Human Motion

## Проект

- Версия на компьютере пользователя: **Unity 6000.6.7f1**. Это source pack,
  а не полный локальный Unity-проект: `Packages/manifest.json` и ProjectSettings
  владельца не заменяются. Облачного Unity Editor нет.
- Графика: URP. Код `Assets/_Silence/Graphics`; импорт героини создаёт URP-материалы.
- Единственный готовившийся человеческий вариант: `Assets/Characters/HoodedGirl`,
  три FBX LOD. Прежняя подготовка 22 создаёт Humanoid Avatar, материалы,
  body/facial clips, LOD prefab и Player Avatar Definition. Геометрия не меняется.
- Четыре участника используют общий сетевой prefab. Меню 21 копирует текущий
  мир и подключает героиню. Конкретные созданные на Mac пути и GUID облаку
  недоступны, поэтому новое меню читает prefab из **текущего NetworkManager**.
- Других отдельных готовых человеческих NPC в доступном source pack нет.
  Vesper/SilencePredator — другой риг существа; человеческие клипы ему не назначаются.
- Animator и Avatar берутся из `PlayerAvatarPresentation.Model`. Старый controller
  содержит направления ходьбы, бег, присед, перенос, ранение, прыжок/падение,
  посадку, смерть и breathing layer. Все исходные controllers сохраняются.
- Движение: `FirstPersonMotor`, один CharacterController. В кооперативе его
  `Simulate` вызывает **серверный сетевой tick** `NetworkPlayer`. На клиентах
  позиции интерполируются существующим `ApplyPose`; Animator не двигает капсулу.
- Камера: `PlayerLook`, `NetworkPlayer`, `PlayerAvatarVisibility`. Сетевая
  проверка взаимодействий использует тот же authoritative eye transform.
- Предметы/двери: `InteractionScanner` → IWorldAuthority → server validation →
  `Interactable.Apply`. Анимационный сигнал добавлен только **после успеха**;
  анимация не подбирает предметы и не открывает дверь самостоятельно.
- Свет: старый `PlayerFlashlight` и подготовленный `FlashlightController`/
  `FlashlightVisuals`/`FlashlightAnimationController`. Новых батарейных моделей
  состояния и новых правил расхода заряда не добавлено.
- Сеть: существующие Netcode for GameObjects **2.13.3**, Unity Transport **2.6.0**.
  `PlayerAvatarNetwork` расширен, второй NetworkAnimator/NetworkTransform не добавлен.
- Шум: `GameplayDirector.Emit`, NoiseSystem, StepCycle/StepProfile, WorldAudio.
  Новый bridge передаёт контакт существующему motor; GameplayWorldNetwork
  продолжает рассылать шум/звук. Прежние звуки шагов синтезированные, не новые записи mocap.
- Animation Rigging отсутствует в закреплённых зависимостях. Для четырёх игроков
  выбраны Animator IK и существующий flashlight IK, без установки ещё одного пакета.

## Реальный Basic Motions пакет

SHA-256 исходного `animation_pack_basic_motions.unitypackage`:

`20b52be48a73cd5a288d6c405b17d27e0a0541fdfa777a1fa36db7d3c51e5b7d`

Архив содержит **41 запись ресурсов**, включая папки, **23 отдельных `.anim`**,
один демонстрационный FBX, prefab, controller, сцену и timeline. Скриптов нет.
Файлы действительно распакованы в рабочую папку `Assets/PolyOne`, но Unity
AssetDatabase в облаке не запускался. Демонстрационная модель не используется
как игровой персонаж, сцена поставщика не открывается.

В каждом `.anim` — **130 Humanoid float bindings**, из них 95 мышечных кривых.
Всего проверено **87 234 конечных ключа**, sample rate **60 Hz**.
Продолжительность — от 0,5167 до 19,1667 секунд. У клипов исходно выключен Loop;
новый генератор меняет настройки только в отдельных копиях.

В настоящем FBX, импортированном доступным Blender 5.2.3 LTS:
**одна арматура, 65 костей, 52 Humanoid mapping в Unity meta**; все назначенные
кости действительно присутствуют. Это подтверждает исходный риг и наличие
данных, **не native-валидность Unity Avatar**. Клипам с Humanoid muscle bindings
не нужны совпадающие имена костей с HoodedGirl: используется её собственный Avatar.

У `Run Right` установлен `mirror`, у `Run Left` — нет. Этот флаг сохраняется.
Некоторые клипы содержат значительное planar travel: root motion извлекается,
но не применяется к объекту. Если запечь это перемещение в позу, бёдра могут
уйти от капсулы. Генератор проверяет и этот случай на реальном Avatar в Unity.

## Quantum Stage

Файлов коллекции/бесплатного sample в репозитории и приложениях нет.
Попытки прочитать обе Google Docs и таблицу из задания получили network HTTP 403.
Видео в этой среде не просмотрены. Коллекция не импортирована, её 139 записей
не входят в количество используемых клипов. Покупка или скачивание платных
ресурсов не выполнялись. Motion Matching не реализован под видом Blend Trees.

## Совместимость API

Новые вызовы сверены с публичным UnityCsReference именно для **6000.6.7f1**,
commit `d0e5e3e00a4f454bc2853234708c3d7c67ebca9a`, и имеющимся исходным NGO.
Например, для AnimationClipSettings используются реальные поля
`loopBlendOrientation`, `loopBlendPositionY`, `loopBlendPositionXZ`.
Это inspection открытого C# API; native-компиляцию оно не заменяет.

Машиночитаемые результаты находятся в `Docs/Validation/Animation/`:
`basic-motions-audit.json`, `source-skeleton.json`, `unity-api-source-review.json`.
