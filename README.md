# SILENCE — Unity 6

Написан игровой блок **0.3.0**: слуховой ИИ, шум поверхностей, переноска/броски и ручные двери,
ранения/лечение, семь глав с заданиями, голосования и checkpoint. **92 теста чистого ядра прошли.**
Новые компоненты и генератор ещё не запускались в Unity; готовая художественная игра не заявляется.
Ранее владелец подтвердил открывание двери и проход через неё в сетевом прототипе 0.2.4.

## Обновить и открыть новую кампанию

1. Скачай **Code → Download ZIP** на [странице рабочей ветки](https://github.com/alpachino09/horrorsilenceplace/tree/codex/silence-sprint-00-01) и распакуй.
2. В Unity останови Play. Нажми **SILENCE → 00 Update Sources From Folder**, выбери распакованную
   папку с `Assets` и `LocalPackages`. Дождись компиляции. Если меню 00 старое/отсутствует,
   сначала скопируй из ZIP только `Assets/_Silence/Editor/SilenceSourceUpdater.cs` в тот же путь проекта.
3. Нажми **03 Install Multiplayer Package** и дождись компиляции. Оставь NGO **2.13.3** и свой Transport **6.6.0**.
4. **09 Generate Campaign Gameplay → 11 Validate Campaign**.
5. Открой `Assets/_Silence/Generated/Gameplay/Campaign.unity` → **Play → Создать сессию → Начать раунд**.

Для проверки останови Play и нажми **12 Run Solo Gameplay Checks**. Она запускает реальную
Unity-проверку, которой здесь ещё не было; результат появится в Console и `Library/Silence/GameplayChecks.json`.
Для двух игроков: **10 Build Campaign Host and Client → 07 Launch Host and Client**.

- [Управление, механики, ограничения и проверки нового блока](Docs/GAMEPLAY_RU.md)
- [Фактическое состояние разработки](Docs/IMPLEMENTATION_STATUS.md)
- [Первая установка и старый прототип](README_RU.md)
- [Проверка старой сетевой фермы и 1/2/4 процессов](Docs/SPRINT02_RU.md)
- [Перечень файлов](Docs/CREATED_FILES.md)

Меню 09 использует уже созданную сетевую ферму. Для первой установки сначала выполнить 01, 03 и 04.
Меню 05/06 сохраняют прежнее назначение: сборка/проверка старой сетевой фермы.
Обновление сохраняет `.meta`, пользовательские Generated-сцены и резервирует код в
`Library/Silence/SourceBackups`. Не заменяй всю папку `_Silence` через Finder.

Нужны Unity 6 / Universal 3D (URP), официальный Input System и Test Framework.
Локальный сетевой пакет требует официальный NGO 2.13.3; минимальная зависимость Transport 2.6.0,
уже установленный Transport 6.6.0 допускается. Старый совет ставить NGO 2.7.0 отменён из-за EntityId
в Unity 6.6; [восстановление](Docs/SPRINT02_RU.md#если-после-установки-270-появилась-ошибка-entityid--cs0619).
Платные ассеты и аккаунты внешних игровых сервисов для текущего LAN-режима не нужны.
