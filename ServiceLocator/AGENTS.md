# ServiceLocator — AI Agent Instructions

## Overview

A lightweight service locator for Unity. Provides static `Register`/`Get` access to services at both runtime and editor time. Lives in `CupkekGames.Systems` namespace.

Assembly: `CupkekGames.Systems.ServiceLocator` (Runtime), `CupkekGames.Systems.ServiceLocator.Editor` (Editor)

## Architecture

```
ServiceLocator (static)
  ├── Register / Get              → single instance per Type (default)
  ├── Register(append:true) / GetAll → multiple instances per Type
  └── RemoveInstance              → remove a specific instance

ServiceRegistrySO (SO)
  ├── List<ServiceProviderSO>  → providers with custom registration logic
  └── List<ScriptableObject>   → plain SOs registered as their concrete type

ServiceRegistry (MonoBehaviour)
  ├── List<ServiceRegistrySO>  → SO-based registries to load
  ├── List<ServiceProvider>    → scene providers with custom logic
  └── List<Component>          → plain components registered as concrete type

ServiceRegistrySOEditor [InitializeOnLoad]
  └── Auto-discovers all ServiceRegistrySO assets
  └── If config.RegisterInEditor == true → calls RegisterAll() at editor time
  └── Has "Find All Providers" button → auto-populates the list via FindAssets("t:ServiceProviderSO")
```

## Key Classes

### `ServiceLocator` (Runtime/ServiceLocator.cs)
Static service container. **Single unified store:** `Dictionary<Type, List<ServiceDescriptor>>`.

- `Register()` replaces by default — sets list to exactly one entry
- `Register(append: true)` appends — adds to existing list
- `Get()` returns first entry in the list
- `GetAll()` returns all entries in the list

```csharp
// Single instance (replaces previous)
ServiceLocator.Register(myService);
ServiceLocator.Register(myService, typeof(IMyInterface));
var svc = ServiceLocator.Get<IMyInterface>();

// Multiple instances (accumulates)
ServiceLocator.Register(provider1, typeof(IMyProvider), append: true);
ServiceLocator.Register(provider2, typeof(IMyProvider), append: true);
IReadOnlyList<IMyProvider> all = ServiceLocator.GetAll<IMyProvider>();

// Removal
ServiceLocator.Remove<IMyInterface>();              // removes all entries for that type
ServiceLocator.RemoveInstance(provider1, typeof(IMyProvider)); // removes one specific entry
```

`RegisteredServices` returns `IReadOnlyDictionary<Type, List<ServiceDescriptor>>` — the full store.

### `ServiceDescriptor` (Runtime/ServiceDescriptor.cs)
Holds `ServiceType`, `ImplementationType`, and `Implementation` (the actual object). Supports lazy instantiation via constructor injection.

### `IServiceProvider` (Runtime/IServiceProvider.cs)
Interface: `RegisterServices()` + `UnregisterServices()`. Implemented by both SO and MonoBehaviour base classes.

### `ServiceProviderSO` (Runtime/ServiceProviderSO.cs)
Abstract `ScriptableObject` base. Extend this to create SO-based services that can be dragged into `ServiceRegistrySO`.

```csharp
public class MyProviderSO : ServiceProviderSO
{
    public override void RegisterServices()
    {
        ServiceLocator.Register(this, typeof(IMyProvider), append: true);
    }
    public override void UnregisterServices()
    {
        ServiceLocator.RemoveInstance(this, typeof(IMyProvider));
    }
}
```

### `ServiceProvider` (Runtime/ServiceProvider.cs)
Abstract `MonoBehaviour` base. Auto-calls `RegisterServices()` on `Awake()`, `UnregisterServices()` on `OnDestroy()`.
- `bool _autoRegister = true` → set to false if managed by a `ServiceRegistry`

### `ServiceRegistrySO` (Runtime/ServiceRegistrySO.cs)
ScriptableObject with:
- `bool _registerInEditor` → if true, editor auto-registers on domain reload
- `List<ServiceProviderSO> _providers` → providers with custom registration logic
- `List<ScriptableObject> _services` → plain SOs registered as their concrete type (no custom logic)

### `ServiceRegistry` (Runtime/ServiceRegistry.cs)
Unified MonoBehaviour for all runtime service registration. Place in scene, optionally DontDestroyOnLoad.
- `List<ServiceRegistrySO> _registries` → SO-based registries to load (project-wide services)
- `List<ServiceProvider> _providers` → scene providers with custom logic
- `List<Component> _components` → plain components registered as concrete type
- `bool _dontDestroyOnLoad` → optionally persist across scenes

### `ServiceRegistrySOEditor` (Editor/ServiceRegistrySOEditor.cs)
- `[InitializeOnLoad]`: On domain reload, finds all `ServiceRegistrySO` assets with `RegisterInEditor == true` and calls `RegisterAll()`. This is how editor-time service registration works.
- Inspector: "Find All Providers" button auto-discovers all `ServiceProviderSO` assets via `AssetDatabase.FindAssets`.

### `ServiceLocatorDebugWindow` (Editor/ServiceLocatorDebugWindow.cs)
Tools → CupkekGames → Service Locator Debug. Shows all registered services with their types and implementations.

## Patterns

### Single-instance service (typical)
Use `Register` + `Get`. One instance per interface type. Last registration wins.

### Multi-instance service (e.g., multiple providers)
Use `Register(append: true)` + `GetAll`. All instances coexist. Use when multiple SOs of the same type each contribute data (e.g., named key providers).

### Editor-time registration
Set `RegisterInEditor = true` on the `ServiceRegistrySO` asset. The `ServiceRegistrySOEditor` static constructor handles auto-registration on domain reload.

### Runtime registration
Place a `ServiceRegistry` MonoBehaviour in a scene. Add `ServiceRegistrySO` assets to its `_registries` list, or drag providers/components directly.

## Rules

- One `ServiceRegistrySO` SO per project is typical, but multiple are supported
- `Register()` replaces the previous instance for that type — no duplicates
- `Register(append: true)` accumulates — use `RemoveInstance()` to remove specific ones
- `Remove(Type)` clears both single and multi stores for that type
- MonoBehaviour services (`ServiceProvider`) self-register on Awake by default — set `_autoRegister = false` if managed by `ServiceRegistry`
- SO services (`ServiceProviderSO`) must be added to a `ServiceRegistrySO` list
- Scene services can use `ServiceRegistry` (Mono) for centralized control, or self-register individually
- `ServiceRegistry` is the unified entry point for runtime — handles SO registries, providers, and plain components
- Static dictionary — survives scene loads but clears on domain reload (editor) or app restart (build)
