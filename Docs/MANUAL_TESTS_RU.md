# Ручная приёмка Sprint 00–01

Все пункты ниже — **план проверки в Unity**, а не отчёт о пройденных тестах.
Сначала установить пакет по README_RU.md. При красной ошибке сохранить текст и остановить приёмку.

1. Открыть Console, очистить старые сообщения. Выполнить SILENCE/01 Generate Farm Prototype.
   Ожидается две сцены, Player.prefab, настройки и материалы в Generated/Sprint01, без ошибок.
   Исходная открытая сцена и её объекты должны остаться на месте. Выполнить команду повторно:
   ожидается сообщение о сохранении существующего результата, без новых дублей и перезаписи.
2. Выполнить SILENCE/02 Validate Prototype. Ожидается `Failures: 0`; сохранить
   `Library/Silence/Validation.txt`. Этот отчёт содержит настоящие версии Editor и пакетов.
3. Открыть Bootstrap, нажать Play, затем Продолжить. Ожидается Farm_Prototype, один Player,
   одна активная камера и один AudioListener, без предупреждений Missing Script.
4. Проверить WASD и обзор; зажать W+D: диагональ не быстрее прямого движения. Shift ускоряет,
   отпускание возвращает ходьбу. Персонаж сталкивается со стенами и оградой.
5. Прыгнуть Space, нажать Space в воздухе: второго прыжка нет. Приземлиться. Проверить,
   что при ударе головой об потолок вертикальная скорость сбрасывается.
6. Найти низкий навес справа от спавна. Стоя пройти под центр крыши не должно получаться.
   Удерживать Ctrl: пройти. Под крышей отпустить Ctrl: оставаться присевшим.
   Выйти из-под крыши: встать. Камера меняет высоту без проваливания ног в землю.
7. Зайти в дом через дверь, подняться по лестнице, спуститься. Нет провалов, застревания
   на ступенях и лишнего CapsuleCollider на дочернем Body_Graybox.
8. Навести взгляд на дверь/предмет с расстояния >2.5 м: подсказки нет. Подойти — подсказка
   появляется снизу, постоянного прицела нет. Встать за стеной: E не действует через неё.
9. E открывает/закрывает двери и ворота. Двери здесь кнопочные, не физическое ручное открывание.
   E на радио переключает индикатор. Аудио радио в этом спринте отсутствует.
10. Подобрать 3 из 4 предметов, открыть Tab: три занятых слота. Четвёртый предмет не исчезает,
    показывается сообщение о заполнении. Повторный E на месте взятого предмета не дублирует его.
    Предметы здесь ещё нельзя выбрасывать, использовать для лечения или физически носить в руках.
11. F выключает/включает фонарь. Esc открывает меню и освобождает курсор. Изменить чувствительность,
    язык и настройки теней/VSync мышью: меню не должно самопроизвольно закрываться.
    Продолжить/Esc возвращает управление. Переключиться в другое приложение: курсор свободен.
12. Открыть выходные ворота напротив спавна, пройти наружу к табличке и нажать E.
    Ожидается сообщение о завершении маршрута. Никакого перехода в несуществующую главу нет.
13. При наличии геймпада проверить стики, нажатия стиков, нижнюю/левую/верхнюю кнопку,
    D-pad вверх и Start. Навигация по настройкам с геймпада пока не реализована.
14. Остановить и повторно запустить Play: предметы, двери и инвентарь сброшены.
    Постоянного сохранения игрового прогресса пока нет.

## Автоматические проверки в Unity

Открыть Window → General → Test Runner (или найти Test Runner через поиск меню).
Во вкладке EditMode выполнить Run All: ожидается 41 тестовый случай в Silence.EditModeTests.
Затем во вкладке PlayMode выполнить Run All: ожидаются 7 тестов в Silence.PlayModeTests.
Перед PlayMode сцены должны быть сгенерированы и доступны через активный список сцен.
Не считать проверку успешной, если выполнено 0 тестов или есть skipped/failed.

Для командной строки Mac закрыть Editor, открытый на том же проекте, и использовать **его точную версию**:

```sh
UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.x.yf1/Unity.app/Contents/MacOS/Unity"
UNITY_PROJECT="/absolute/path/to/your/unity-project"
mkdir -p "$UNITY_PROJECT/TestResults"
"$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$UNITY_PROJECT" -executeMethod Silence.Editor.SilenceSceneGenerator.Generate -logFile "$UNITY_PROJECT/TestResults/generate.log"
"$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$UNITY_PROJECT" -executeMethod Silence.Editor.SilenceProjectValidator.ValidateBatch -logFile "$UNITY_PROJECT/TestResults/validate.log"
"$UNITY_EDITOR" -batchmode -nographics -projectPath "$UNITY_PROJECT" -runTests -testPlatform EditMode -assemblyNames Silence.EditModeTests -testResults "$UNITY_PROJECT/TestResults/editmode.xml" -logFile "$UNITY_PROJECT/TestResults/editmode.log"
"$UNITY_EDITOR" -batchmode -nographics -projectPath "$UNITY_PROJECT" -runTests -testPlatform PlayMode -assemblyNames Silence.PlayModeTests -testResults "$UNITY_PROJECT/TestResults/playmode.xml" -logFile "$UNITY_PROJECT/TestResults/playmode.log"
```

`6000.x.yf1` и путь к проекту — явные заполнители, их нужно заменить, а не копировать буквально.
Проверить exit status каждой команды и свежесть/содержание XML. При необходимости эти проверки можно
выполнить через интерфейс Editor. Сами команды Unity в облаке не выполнялись.
