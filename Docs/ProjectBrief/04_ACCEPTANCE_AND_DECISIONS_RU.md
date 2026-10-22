# Контроль качества и спорные решения

## Fixed user requirements
- Exactly 1–4 players, first-person; Windows/macOS first; planned Steam; Unity 6 on Mac; Codex writes most code and generates scenes via Editor tools.
- 7 story chapters, 4–6 hours target, semi-open spaces, no returning to previous chapters, scripted moments, multiple endings and voting.
- No shooting/weapons, no survival hunger/sleep/thirst/temp, no crafting/building/cars, no conventional circuit puzzles or random gameplay-changing events.
- 3 inventory slots baseline; rare backpacks; retained items at transitions; medications, batteries, rare revive item; all dead -> restart chapter.
- Blind fast monsters, 1–2 variants, sound driven, first attack injuries then possible death; break selected world objects, climb authored zones, no learning, no sonic-stun weapon, no bosses, no mutual monster communication.
- Microphone presence in-world, proximity voice and radio, gestures; noise meter, weather/occlusion/material effect.
- RU/EN and rebind, controller, graphics controls. AAA art goal, M1 Pro target 40–60 medium is **an empirical target, not guarantee**. Budget 10k RUB for assets.

## Interpretations / deferred approvals (never misstate as user-approved)
- Project name `SILENCE`, organisation code `_Silence`, scenario labels THAW-9, six years after outbreak, specific names/character dialogue, 3 endings — proposals.
- URP chosen as performance-first recommendation; user asked AAA look, not specific pipeline.
- Backpack adds 2 slots (example), RescueKit as item, little post-hit grace window, mission-based solo adaptation, optional captions OFF by default (user said no subtitles); all adjustable.
- `No random events` interpreted as fixed campaign beats, atmospheric sounds and minor collectible placements may vary only if user agrees.
- Death revival: a dead player returns via rare item wielded by living teammate at valid point; no free automatic revival. This is a design proposal.
- 16+ is intended label, not guaranteed age rating given explicit dismemberment and heavy gore. Verify store/legal guidance before shipping.
- Steam invites & all variants of connectivity staged; online service costs and accounts not assumed free forever.
- All code development via Codex ≠ all art can be created by C# alone; final asset modeling/rigging/audio needs legitimate inputs/tools.

## Criteria for first playable demo
1. Project opens in the actual installed Unity version without compile errors.
2. Unity editor menu generates scene reproducibly without clearing unrelated data.
3. Local character walks, runs, crouches, looks, interacts; no double active cameras.
4. At least three footstep surface types produce deterministic `NoiseEvent` intensities.
5. Loud metal object drop causes creature to Investigate at position; wall reduces perceived volume, stronger sources trigger Chase; no line-of-sight detection.
6. Creature reliably returns to Search and then Patrol when sound loses relevance; never knows current player's location magically.
7. Two separate network processes can interact with the same item without duplication, and creature reacts to remote noise.
8. Injury state and clear game-over/restart route are present in playable slice.
9. Scene UI remains minimal, RU/EN strings for implemented UI, NO permanent crosshair.
10. `Docs/IMPLEMENTATION_STATUS.md` tracks actual outcomes and manual test commands.

## Risks and explicit mitigations
- Scope explosion: fixed sprint boundaries, compilation each sprint, MVP first.
- Netcode physics: server owned interactive entities, conservative sync rates, tests for contention.
- Mic abuse/ambient clipping: opt-in, noise gate, clamped amplitude, rate limit, no raw PCM for AI.
- Horror balance: tune difficulty and patrol states; avoid constant instant deaths.
- Mac performance: actual profiler traces on M1 Pro; visual options not guesses.
- Realtime voice provider availability: service abstraction, never claim functional without provider.
- Asset/IP issues: log URL/creator/license in `THIRD_PARTY_LICENSES`, don't extract movie/game models or textures.

## Instructions to owner
1. Create empty **Unity 6 Universal 3D / URP** project (if not already present) and open project root in Codex.
2. Copy the package files to the project's `Docs/ProjectBrief/` (Codex will read them there).
3. Give Codex prompt inside `00_START_HERE_RU.md`.
4. Open Unity after each small implementation, read Console errors, test scene. Provide exact stack traces to Codex.
5. Do not pay for assets/services automatically. Owner approves every purchase and accounts.
