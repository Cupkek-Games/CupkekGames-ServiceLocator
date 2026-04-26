using System;
using System.Collections.Generic;
using CupkekGames.Core;
using UnityEngine;

namespace CupkekGames.Systems
{
  /// <summary>
  /// MonoBehaviour registry that centralizes service registration.
  /// Can hold SO-based registries, scene providers, and plain components.
  /// </summary>
  public class ServiceRegistry : MonoBehaviour
  {
    [MultiLineHeader("SO-based registries to load.\nUseful for project-wide services defined in assets.")]
    [SerializeField]
    private List<ServiceRegistrySO> _registries = new();

    [MultiLineHeader(
      "Service Providers with custom registration logic.\nEach provider controls how it registers/unregisters.")]
    [SerializeField]
    private List<ServiceProvider> _providers = new();

    [MultiLineHeader("Plain components registered as their concrete type.\nNo custom logic - just drag and drop.")]
    [SerializeField]
    private List<Component> _components = new();

    [MultiLineHeader(
      "ScriptableObject services with optional register-as interface.\nChoose concrete or interface per entry in the inspector.")]
    [SerializeField]
    private List<ServiceEntry> _serviceEntries = new();

    [SerializeField] private bool _dontDestroyOnLoad;

    public IReadOnlyList<ServiceRegistrySO> Registries => _registries;
    public IReadOnlyList<ServiceProvider> Providers => _providers;
    public IReadOnlyList<Component> Components => _components;
    public IReadOnlyList<ServiceEntry> ServiceEntries => _serviceEntries;

    private void Awake()
    {
      if (_dontDestroyOnLoad)
        DontDestroyOnLoad(gameObject);

      RegisterAll();
    }

    private void OnDestroy()
    {
      UnregisterAll();
    }

    public void RegisterAll()
    {
      foreach (var registry in _registries)
      {
        if (registry != null)
          registry.RegisterAll();
      }

      foreach (var provider in _providers)
      {
        if (provider != null)
          provider.RegisterServices();
      }

      foreach (var component in _components)
      {
        if (component != null)
          ServiceLocator.Register(component, component.GetType());
      }

      foreach (var entry in _serviceEntries)
      {
        if (entry?.Instance == null)
          continue;
        Type serviceType = entry.ResolveServiceType();
        if (serviceType != null)
          ServiceLocator.Register(entry.Instance, serviceType);
      }
    }

    public void UnregisterAll()
    {
      foreach (var registry in _registries)
      {
        if (registry != null)
          registry.UnregisterAll();
      }

      foreach (var provider in _providers)
      {
        if (provider != null)
          provider.UnregisterServices();
      }

      foreach (var component in _components)
      {
        if (component != null)
          ServiceLocator.Remove(component.GetType());
      }

      foreach (var entry in _serviceEntries)
      {
        if (entry?.Instance == null)
          continue;
        Type serviceType = entry.ResolveServiceType();
        if (serviceType != null)
          ServiceLocator.Remove(serviceType);
      }
    }
  }
}
