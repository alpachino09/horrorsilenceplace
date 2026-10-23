# Сторонние материалы и зависимости

| Материал | Источник | Лицензия/условия | Использование |
|---|---|---|---|
| Unity Editor, встроенные primitives | Unity Hub / установленный Editor | Unity Software Terms, действующая лицензия владельца | Генерация геометрии и запуск |
| URP | `com.unity.render-pipelines.universal`, Unity Registry | Лицензия установленного пакета Unity | Шейдер URP Lit, освещение |
| Input System | `com.unity.inputsystem`, Unity Registry | Лицензия установленного пакета Unity | Клавиатура, мышь, геймпад |
| Unity Test Framework | `com.unity.test-framework`, Unity Registry | Лицензия установленного пакета Unity | EditMode/PlayMode runner |
| NUnit 3.14 | https://www.nuget.org/packages/NUnit/3.14.0 | MIT | Проверка чистого ядра вне Unity |
| NUnit3TestAdapter 4.6 | https://www.nuget.org/packages/NUnit3TestAdapter/4.6.0 | MIT | Запуск NUnit вне Unity |
| Microsoft.NET.Test.Sdk 17.11.1 | https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/17.11.1 | MIT, см. лицензии зависимостей | Внешний тестовый runner |
| .NET SDK/Roslyn | https://dotnet.microsoft.com/ | MIT и уведомления комплекта SDK | Внешняя компиляция ядра, проверка синтаксиса |

Версии Unity-пакетов намеренно не выдуманы: после открытия настоящего проекта смотри
`Packages/manifest.json`, `Packages/packages-lock.json` и `Library/Silence/Validation.txt`.
Unity-пакеты и SDK не включены в распространяемый ZIP; устанавливаются из официальных источников.
Материалы, Input Actions и C#-исходники этого прототипа созданы для проекта.
Исходные документы владельца сохранены без изменений в `Docs/ProjectBrief`.
Звуковые записи, сторонние персонажи, модели, шрифты и изображения не включались.
Перед добавлением внешнего ассета фиксировать автора, URL, точную лицензию и разрешённые способы использования.

## Новые выбранные зависимости Sprint 02

- Netcode for GameObjects 2.7.0, Unity Technologies:
  https://github.com/Unity-Technologies/com.unity.netcode.gameobjects/tree/ngo/2.7.0/com.unity.netcode.gameobjects
  Лицензия официального пакета — Unity Companion License (LICENSE.md), Unity-dependent projects.
  Для изучения использован commit b1ca57132eb8fde7f25eb287df93021e68a57087, без копирования пакета в репозиторий игры.
- Unity Transport 2.6.0: официальный com.unity.transport, зависимость NGO из его package.json.
  Устанавливается UPM; точную лицензию/уведомления установленного пакета сохраняет Unity Package Manager.
  Это выбранная зависимость, а не утверждение о её наличии в проекте владельца или о публичном домене.
- Python 3 standard library используется тестовым launcher; внешние Python-библиотеки не добавлены.

Новые модели/аудио/картинки и внешние asset-store-пакеты не включались. Платные ресурсы не требуются.
