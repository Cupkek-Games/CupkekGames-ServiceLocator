using CupkekGames.EditorInspector;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CupkekGames.Services
{
  [CreateAssetMenu(menuName = "CupkekGames/ServiceLocator/Service Registry")]
  public class ServiceRegistrySO : ScriptableObject
  {
    [Tooltip(
      "When enabled, registers into ServiceLocator while editing only. Locator is cleared when entering and when leaving Play Mode; then editor registrations are re-applied.")]
    [SerializeField]
    private bool _registerInEditor;

    [MultiLineHeader(
      "Service Providers with custom registration logic.\nEach provider controls how it registers/unregisters.")]
    [SerializeField]
    private List<ServiceProviderSO> _providers = new();

    [MultiLineHeader(
      "ScriptableObject services with optional register-as interface.\nChoose concrete or interface per entry in the inspector.")]
    [SerializeField]
    private List<ServiceEntry> _serviceEntries = new();

    public bool RegisterInEditor => _registerInEditor;
    public IReadOnlyList<ServiceProviderSO> Providers => _providers;
    public IReadOnlyList<ServiceEntry> ServiceEntries => _serviceEntries;

    public void RegisterAll()
    {
      foreach (var provider in _providers)
      {
        if (provider != null)
          provider.RegisterServices();
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
      foreach (var provider in _providers)
      {
        if (provider != null)
          provider.UnregisterServices();
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
