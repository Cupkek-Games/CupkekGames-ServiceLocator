# CupkekGames Services

Lightweight service-locator pattern. ScriptableObject-driven service registration — no manual wiring in MonoBehaviour boilerplate. Used by `gamesave`, `inventory`, `rpgstats`, `sequencer`, `settings`, and most game-side code.

## What's inside

**Runtime** (`CupkekGames.Services.asmdef`)

- `ServiceLocator` — static facade: `ServiceLocator.Get<T>()`, `ServiceLocator.GetAll<T>(catalogId)`, `ServiceLocator.Register(...)`, `ServiceLocator.Remove(...)`
- `ServiceDescriptor` — describes a single registered service
- `ServiceProvider` / `ServiceProviderSO` — base classes for ScriptableObject-driven registration bundles
- `ServiceRegistry` / `ServiceRegistrySO` / `ServiceRegistrySORunner` — registry orchestration loaded by a scene
- `IServiceProvider` — abstract registration contract

**Editor** (`CupkekGames.Services.Editor.asmdef`)

- `ServiceLocatorDebugWindow` — inspect registered services at runtime
- `ServiceRegistryEditor` / `ServiceRegistrySOEditor` / `ServiceEntryDrawer` — inspector tooling
- `ServiceLocatorDebugTypeDisplay` — friendly type display in the debug window

## Dependencies

None.

## Usage pattern

Cache `ServiceLocator.Get<T>()` in `Awake()` / `Start()` — never call per-frame. Register services via `ServiceProviderSO` / `ServiceRegistrySO` assets in scenes, not via `ServiceLocator.Register(...)` directly in MonoBehaviours.
