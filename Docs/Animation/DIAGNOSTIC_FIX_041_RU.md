# Жёлтые UAC0009 в Unity 6000.6.7f1

UAC0009 сообщает об устаревшем символе `DEVELOPMENT_BUILD`, а не о поломке
анимации, двери или модели. Такие предупреждения сами по себе не являются
ошибками компиляции. На скриншоте пользователя видны только эти предупреждения.

Исправлены 11 употреблений в пяти файлах:

- `Assets/_Silence/Scripts/Interactions/InteractionDiagnostics.cs`;
- `LocalPackages/com.silence.multiplayer/Runtime/NetworkSmokeProbe.cs`;
- `LocalPackages/com.silence.multiplayer/Runtime/GameplaySmokeProbe.cs`;
- `LocalPackages/com.silence.multiplayer/Runtime/NetworkPlayer.cs`;
- `LocalPackages/com.silence.multiplayer/Runtime/NetworkSession.cs`.

Используется `UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION`; Conditional-атрибуты
также переведены на новый символ. В Editor диагностика остаётся включена;
в Debug/Checked/Instrumented player она доступна через managed code variant.
В Release, где инструментирование выключено, не компилируются automation/warp API
и тела диагностического вывода. Для автоматических smoke-сборок выбирайте вариант
с инструментированием; один флажок Development Build больше не задаёт managed variant.

Замена сверена с UnityCsReference **6000.6.7f1**, commit
`d0e5e3e00a4f454bc2853234708c3d7c67ebca9a`:

- [ManagedCodeVariant и его символы](https://github.com/Unity-Technologies/UnityCsReference/blob/d0e5e3e00a4f454bc2853234708c3d7c67ebca9a/Editor/Mono/PlayerSettings.bindings.cs);
- [формирование флагов компиляции](https://github.com/Unity-Technologies/UnityCsReference/blob/d0e5e3e00a4f454bc2853234708c3d7c67ebca9a/Editor/Mono/Scripting/ScriptCompilation/EditorCompilationInterface.cs).

Roslyn-проверка синтаксиса расширена до семи конфигураций. Дополнительные проверки
активного C# дерева подтверждают одинаковые guards у caller/callee и исключение
автоматизированных тестовых API из Release. Это не запуск Unity и не выполнение
нативного анализатора UAC0009.

Установка: скачать и распаковать **SILENCE_HumanMotion_0.4.1.zip** → остановить Play →
**SILENCE → 00 Update Sources From Folder** → выбрать корень распакованного архива
с Assets и LocalPackages → дождаться компиляции → **Console → Clear**.
Продолжить пункт **25** с того места, где остановились. Уже импортированные
анимации и созданную сцену заново генерировать не требуется.
