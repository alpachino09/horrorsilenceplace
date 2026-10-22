# Roadmap для Codex: маленькие законченные шаги

## Definition of Done для КАЖДОГО спринта
C# compiles in actual Unity 6 project (or explicitly mark not verified if editor unavailable); tested basic behaviour, no broken refs/scene missing scripts, documented changed files, manual click-by-click test in Russian, Git checkpoint optional, precise TODO. Only proceed after essential previous systems work. Do not claim success on unexecuted tests.

### Sprint 00: Проверка окружения и структура
Inspect Unity project, Git, URP, compatible packages, create folders, docs and editor validators; preserve existing scene data. Deliver runnable blank Unity editor opening without errors.

### Sprint 01: Player + Farm graybox
Programmatic Editor scene generator and primitive-only graybox: fenced farm, barn, home, floors, stairs; player fps motor with run/crouch/basic jump, look; Input System; simple interaction range overlay only when near interactable, no permanent crosshair; graphics settings baseline; solo fallback. Dedicated code modules for multiplayer extension. Complete walkthrough from spawn to exit.

### Sprint 02: Authoritative multiplayer fundamentals
Two executable builds Host and Client with NGO; ownership-controlled player movement, replicated position, simultaneous interactions, disconnect/reconnect safety basics; visual player representation; local own camera only. Tests 1,2,4 simulated players (4 subject to hardware). Remote invite/Relay deferred.

### Sprint 03: Sound simulation + creature prototype
Surface dependent footsteps; dropped object impacts; movable metal/wood door; noise meter HUD; distance falloff + occlusion raycasts + ambient masking; AI FSM hearing only; chase/investigate/search; host authority; regression tests including sound produced by remote player. Solo/host+client acceptance.

### Sprint 04: Physical interactions
Pick up, carry, drop, controlled throw; slow/fast door opens with variable sound; reliable ownership transfer, physics stability and noise event de-duplication; hiding spots and stealth test corridor.

### Sprint 05: Health/inventory/difficulty
3 slot inventory; rare backpack expansion config; batteries + medicine; wound moans, initial injury and lethal second attack, aid/carry; rare RescueKit revives killed teammate with clear conditions; full-party wipe chapter restart; Easy/Normal/Hard/Nightmare tunable SO, test balancing.

### Sprint 06: Mic and voice plumbing
Permission, device selection, enable/mute, calibration, thresholds, smoothing, privacy labels, noisy-mic detection; convert mic RMS to network noise events without raw audio; prevent spam and host abuse. Separate voice provider interface, proximity/chat/radio/muting; real provider only after SDK/account details are available and tested, otherwise clearly mark voice not implemented.

### Sprint 07: Farm vertical slice complete
Farm objective chain: scavenge batteries, recover radio message, retrieve document, exit while creature hunts. Cinematic entry/exit, animals or simple ambient animal loops, RU/EN text, accessible optional captions settings, mission start/wipe/restart, chapter save, network tests. No overly technical puzzles.

### Sprint 08: Campaign scaffolding
Seven scene assets (Farm/Town/Tunnels/Factory/Lake/RadioStation/Laboratory) + ChapterDefinition ScriptableObjects + ChapterLoader + StoryFlagStore + EndingResolver + VoteController. Use clearly marked temporary grayboxes, only Farm complete. Hook persistent inventory between chapters.

### Sprint 09: Town and Tunnels
Hand-authored layouts using Editor builders, NPC dialogue stub and real animal ambience, mission events, acoustics in tunnel, radio communications, solo-valid temporary teammate separation. Tests.

### Sprint 10: Factory and Lake
Machine noise masking, planned NPC betrayal, weather, day/sunset controlled timeline; boat as cinematic transition (not drivable sim), moral votes. Tests.

### Sprint 11: RadioStation and Laboratory
Origin reveal, scripted chase and destructible authored obstacles, final evidence and branching, three ending sequences, replay support. Tests.

### Sprint 12: Visual and audio production
Replace prototypes with license-audited free/owned character and creature models, rigging, unique animations, sounds, atmosphere, PBR, lights, blood/VFX, no copyrighted film assets. Real-world recordings only with permission. Optimization + render scale on M1 Pro.

### Sprint 13: Steam & shipping
Steamworks integration with permitted SDK, invite flow, achievements, persistent saves/versioning, localization RU/EN, robust error reporting, graphics/controller rebinding, end-to-end 1/2/4 player QA, platform builds and store rating compliance. Publishing requires accounts and user involvement.

## Sprint sequencing rule
User requested all chapters in project from early stage: implement ChapterDefinition assets and names/scenes as **scaffold** once base tech works, do NOT claim all chapter gameplays exist. This preserves their whole-campaign goal while keeping each change testable.

## For every subsequent Codex request
`Прочитай Docs/IMPLEMENTATION_STATUS.md и файлы 00–04 из пакета. Реализуй ровно следующий незавершённый sprint. Сначала план из 3–7 пунктов, потом реальные изменения проекта, затем тесты и отчёт. Не ломай уже рабочее. Если требуются аккаунты, внешние ассеты или Unity Editor, опиши конкретное ручное действие и продолжай всё, что возможно без него.`
