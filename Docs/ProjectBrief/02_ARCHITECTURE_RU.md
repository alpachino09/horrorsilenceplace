# Архитектура реализации, v0.1

## Принципы
- Unity 6 project with URP; C# modular, low coupling, serialized configurations via ScriptableObject. English identifiers in code, Russian documentation. Use official packages after inspecting actual `Packages/manifest.json` and installed Editor version. Avoid hand-editing Unity YAML if can create scenes/prefabs via Editor C# scripts with idempotent menu actions.
- Network server/host authoritative: combat, enemy AI, inventory mutation, story flags, door world state, item creation, noise decisions, player health, objective completion. Clients own locally predicted/look input and requested interactions; interpolation for networked entities. Host and offline solo share same gameplay core.
- Separate visual/audio playback from simulation. Have a single authoritative simulation of noise events (numbers), not derive enemy behaviour from AudioSource playback.
- Do not assume API package methods without checking installed versions; always compile after adding each dependency.

## Target file structure
```
Assets/_Silence/
  Scripts/
    Core/                 GameBootstrap, GameMode, IDependencyLocator, TimeProvider
    Config/               GameBalanceSO, SurfaceNoiseSO, DifficultySO, ChapterDefinitionSO
    Input/                PlayerInputReader, RebindSettings
    Player/               FirstPersonMotor, PlayerLook, PlayerStance, Stamina, PlayerAnimationFacade
    Interactions/         IInteractable, InteractionScanner, GrabHandle, PhysicalDoor, ItemPickup
    SoundSimulation/      NoiseEvent, NoiseSystem, NoiseSource, SurfaceResolver, AcousticOcclusion
    Audio/                SoundPlayback, AmbientSoundController, MicrophoneNoiseMeter
    Creatures/            CreatureBrain, HearingSensor, NoiseMemory, CreatureMovement, CreatureAttack
    Health/               PlayerVitals, WoundState, Treatment, RescueKit
    Inventory/            InventoryModel, ItemDefinition, InventoryUI, ItemPersistence
    Networking/           NetworkBootstrap, NetworkPlayer, SessionFacade, NetStateSync
    Missions/             ObjectiveController, ChapterController, MissionTrigger
    Story/                StoryFlagStore, VoteController, EndingResolver
    UI/                   HUDController, NoiseMeterUI, MainMenu, SettingsMenu
    Accessibility/        LocalizationFacade
    Save/                 SaveSystem, SaveSchema, SaveMigration
  Editor/                 SilenceSceneGenerator, SilenceProjectValidator
  Scenes/                 Bootstrap, Farm_Prototype, Town, Tunnels, Factory, Lake, RadioStation, Laboratory
  Tests/EditMode/        noise attenuation, inventory, story flags, difficulty
  Tests/PlayMode/        spawn, interaction, health, chapter smoke
  Prefabs/ Materials/ Audio/ Art/ UI/ Settings/
Docs/                       status/decisions/licenses
```
Only generate scenes as functional work reaches them; placeholder scenes are acceptable as clearly labelled placeholders, not complete levels.

## Runtime data contracts
```
NoiseEvent {
  Vector3 WorldPosition;
  float BaseIntensity; // configurable 0..1
  float MaxRangeMeters;
  NoiseKind Kind;
  int SourceNetworkId;
  double ServerTimestamp;
}

interface INoiseEmitter { void EmitNoise(NoiseKind kind, float intensity); }
interface INoiseListener { void OnAudibleNoise(NoiseEvent evt, float perceivedIntensity); }
interface IInteractable { bool CanInteract(PlayerContext player); void RequestInteract(PlayerContext player); }
interface IChapterObjective { string Id; bool IsComplete; }
```
Implement signatures in idiomatic compiling C#, not blindly copy pseudocode. `NoiseSystem` receives host-validated events; uses distance attenuation, sampled raycasts and configurable occluder values; `HearingSensor` chooses loudest recent valid sound, retains last known location and time. Radio voice is a separate service from AI `NoiseEvent` and must be permission-gated.

## Gameplay authority & events
1. Locally pressed interact -> `InteractionScanner` raycast -> client sends request (item entity ID, interaction type) -> host checks distance, ownership and object state -> host applies change -> state replicated -> visual/audio effects on each client.
2. Step/throw/door -> validated `NoiseEvent` server -> `HearingSensor` -> `CreatureBrain` Investigate/Chase -> replicated creature pose/animations.
3. Microphone capture local only after explicit settings opt-in -> low-bandwidth metric samples quantized/throttled -> host clamps/validates noise envelope -> AI noise event. **Do not transmit raw mic data to the AI system.** Voice chat, if implemented, requires a separate authenticated voice system with clear muting consent.
4. Attack: host validates range/cooldown/target alive -> damage state -> hit/animation replication -> treatment/rescue interactions.
5. Chapter transition: host validates objectives & votes, snapshot inventory/story state with save version, then loads chapter for all connected clients.

## Network MVP
- Stage A: host + local client on two launched processes using NGO and Unity Transport, connect by local or LAN address without external online dependency; verify authority.
- Stage B: Sessions/Relay join code via the documented SDK; Unity Authentication (anonymous where appropriate), service configuration and quota notes. Not necessary to block offline Stage A.
- Stage C: session browser, Steam invite, cross-platform compatibility, host disconnect/reconnect handling. These are NOT trivial features, must be independently tested.
- Late join: supported at explicit safe checkpoints only; snapshot/ownership sync required. Host migration out-of-scope v1 unless explicitly commissioned.
- Simulate two clients, not just two player objects in the same local Scene, to test network behavior.

## Performance & macOS
- URP, baked/static lighting where possible, shadow culling, GPU instancing, pooling, occlusion culling and sensible LOD. Profiling on M1 Pro actual hardware mandatory. Set quality presets, resolution scaling; never promise 40–60 FPS merely because Unity supports Apple silicon.
- Prefer simple capsule/placeholder test assets until mechanics are stable. Organic creatures with high-quality rigged animations require separate art pipeline (e.g. Blender) and art iteration; Codex can generate procedural placeholders and asset import helpers, cannot guarantee AAA sculpt solely by writing C#.

## Testing layers
- Pure EditMode tests for inventory capacity, noise perception vs distance and wall occlusion, stamina recovery, branching flags.
- PlayMode tests for prefab spawn, controller, interaction, wound states, ChapterController.
- Manual 1/2/4-player tests on host-client builds: late join; same item simultaneously grabbed; network disconnect; creature attack while players move; conflicting vote; dropped item ownership; no duplication across chapter transitions.
- For failures: include repro steps, expected/actual, console stack trace; correct root cause before increasing scope.
