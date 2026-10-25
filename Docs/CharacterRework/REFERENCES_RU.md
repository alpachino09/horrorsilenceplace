# Изученные источники и применение

Страницы docs.unity3d.com в этой среде вернули HTTP 403; их содержание не
выдаётся за прочитанное. Использованы доступные исходники и пакетная
документация. Hashes/URL и результаты запросов записаны в
`Docs/Validation/CharacterRework044/references.json`.

- [URP BaseShaderGUI](https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.universal/Editor/ShaderGUI/BaseShaderGUI.cs)
  и [Lit.shader](https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader):
  Opaque выбирает One/Zero, запись depth, очередь Geometry и отключение
  transparent/premultiply keywords. Это применено к копиям твёрдых материалов.
- [Two Bone IK — документация пакета](https://github.com/needle-mirror/com.unity.animation.rigging/blob/1bd2bf88886c18e9012b90f9c3c331bc85c2f513/Documentation~/constraints/TwoBoneIKConstraint.md):
  разделение позиции цели, её вращения и Hint для направления локтя.
- [AnimationRuntimeUtils](https://github.com/needle-mirror/com.unity.animation.rigging/blob/1bd2bf88886c18e9012b90f9c3c331bc85c2f513/Runtime/Utils/AnimationRuntimeUtils.cs)
  и [TwoBoneIKConstraintJob](https://github.com/needle-mirror/com.unity.animation.rigging/blob/1bd2bf88886c18e9012b90f9c3c331bc85c2f513/Runtime/AnimationJobs/TwoBoneIKConstraintJob.cs):
  реальные длины звеньев, устойчивый plane/hint при вырожденном положении,
  раздельные веса. В проекте написана собственная ограниченная геометрическая
  модель; существующий Mecanim IK применяет её цели. Второй RigBuilder поверх
  тех же рук не добавлялся, чтобы не получить двух независимых владельцев.
- [UnityCsReference 6000.6.7f1](https://github.com/Unity-Technologies/UnityCsReference/tree/d0e5e3e00a4f454bc2853234708c3d7c67ebca9a):
  Animator IK API, BakeMesh/useScale, localBounds, BoneWeight и Playables.
  Эти исходники использованы для проверки доступности вызовов.

Файлы Unity/Animation Rigging не включены в обновление как собственный код.
Платные модели/анимации не скачивались и не публиковались. Пользовательский
Basic Motions импортируется только из исходного лицензионного пакета на Mac.
