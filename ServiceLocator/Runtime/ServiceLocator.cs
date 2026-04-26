using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace CupkekGames.Systems
{
  public class ServiceLocator : MonoBehaviour
  {
    private const string DefaultKey = "";

    private static readonly Dictionary<Type, Dictionary<string, List<ServiceDescriptor>>> services = new();

    public static event Action OnChanged;

    public static IReadOnlyDictionary<Type, Dictionary<string, List<ServiceDescriptor>>> RegisteredServices => services;

    /// <summary>
    /// Removes every registration. Used when entering Play Mode from the Editor so edit-time
    /// <c>RegisterInEditor</c> services do not stack with runtime <see cref="ServiceRegistry"/> registration.
    /// </summary>
    public static void ClearAll()
    {
      if (services.Count == 0)
        return;

      services.Clear();
      OnChanged?.Invoke();
    }

    // ── Register ─────────────────────────────────────────────────

    public static void Register(object implementation, bool append = false)
    {
      Register(new ServiceDescriptor(implementation), DefaultKey, append);
    }

    public static void Register(object implementation, Type serviceType, bool append = false)
    {
      Register(new ServiceDescriptor(implementation, serviceType), DefaultKey, append);
    }

    public static void Register(object implementation, Type serviceType, string key, bool append = false)
    {
      Register(new ServiceDescriptor(implementation, serviceType), key ?? DefaultKey, append);
    }

    public static void Register(ServiceDescriptor descriptor, bool append = false)
    {
      Register(descriptor, DefaultKey, append);
    }

    /// <summary>
    /// Register a service descriptor under a key.
    /// append=false replaces all entries for the same key (not the whole type).
    /// </summary>
    public static void Register(ServiceDescriptor descriptor, string key, bool append = false)
    {
      var type = descriptor.ServiceType;

      if (!services.TryGetValue(type, out var keyMap))
      {
        keyMap = new Dictionary<string, List<ServiceDescriptor>>();
        services[type] = keyMap;
      }

      if (!keyMap.TryGetValue(key, out var list))
      {
        list = new List<ServiceDescriptor>();
        keyMap[key] = list;
      }

      if (append)
      {
        list.Add(descriptor);
      }
      else
      {
        list.Clear();
        list.Add(descriptor);
      }

      OnChanged?.Invoke();
    }

    // ── Remove ───────────────────────────────────────────────────

    /// <summary>
    /// Removes every registration whose <see cref="ServiceDescriptor.Implementation"/> equals <paramref name="implementation"/>.
    /// Matches the registered service type(s), unlike removing by <c>implementation.GetType()</c>.
    /// </summary>
    public static void Remove(object implementation)
    {
      if (implementation == null)
        return;

      var emptyServiceTypes = new List<Type>();
      foreach (var typeKvp in services)
      {
        var keyMap = typeKvp.Value;
        var emptyKeys = new List<string>();
        foreach (var keyKvp in keyMap)
        {
          keyKvp.Value.RemoveAll(d => d.Implementation == implementation);
          if (keyKvp.Value.Count == 0)
            emptyKeys.Add(keyKvp.Key);
        }

        foreach (var k in emptyKeys)
          keyMap.Remove(k);

        if (keyMap.Count == 0)
          emptyServiceTypes.Add(typeKvp.Key);
      }

      foreach (var t in emptyServiceTypes)
        services.Remove(t);

      OnChanged?.Invoke();
    }

    public static void Remove<T>()
    {
      Remove(typeof(T));
    }

    public static void Remove(Type serviceType)
    {
      services.Remove(serviceType);
      OnChanged?.Invoke();
    }

    /// <summary>
    /// Remove all entries of a type with the specified key.
    /// </summary>
    public static void Remove(Type serviceType, string key)
    {
      if (!services.TryGetValue(serviceType, out var keyMap)) return;

      keyMap.Remove(key ?? DefaultKey);
      if (keyMap.Count == 0)
        services.Remove(serviceType);

      OnChanged?.Invoke();
    }

    /// <summary>
    /// Remove a specific instance registered for a type (any key).
    /// </summary>
    public static void RemoveInstance(object implementation, Type serviceType)
    {
      if (!services.TryGetValue(serviceType, out var keyMap)) return;

      List<string> emptyKeys = null;
      foreach (var kvp in keyMap)
      {
        kvp.Value.RemoveAll(d => d.Implementation == implementation);
        if (kvp.Value.Count == 0)
        {
          emptyKeys ??= new List<string>();
          emptyKeys.Add(kvp.Key);
        }
      }

      if (emptyKeys != null)
        foreach (var k in emptyKeys)
          keyMap.Remove(k);

      if (keyMap.Count == 0)
        services.Remove(serviceType);

      OnChanged?.Invoke();
    }

    // ── Has ──────────────────────────────────────────────────────

    public static bool Has<T>()
    {
      return services.ContainsKey(typeof(T));
    }

    public static bool Has<T>(string key)
    {
      if (!services.TryGetValue(typeof(T), out var keyMap))
        return false;
      return keyMap.ContainsKey(key ?? DefaultKey);
    }

    // ── Get ──────────────────────────────────────────────────────

    /// <summary>
    /// Get the first registered instance for a type (any key).
    /// </summary>
    public static object Get(Type serviceType, bool silent = false)
    {
      if (!services.TryGetValue(serviceType, out var keyMap))
      {
        if (!silent)
          throw new Exception($"Service of type {serviceType.Name} is not registered");
        return null;
      }

      foreach (var list in keyMap.Values)
      {
        for (int i = 0; i < list.Count; i++)
        {
          var desc = list[i];
          if (desc.Implementation != null)
            return desc.Implementation;

          // Lazy instantiation
          Type actualType = desc.ImplementationType ?? desc.ServiceType;
          if (actualType.IsAbstract || actualType.IsInterface)
            continue;

          var ctor = actualType.GetConstructors()[0];
          var parameters = ctor.GetParameters()
            .Select(p => Get(p.ParameterType, silent)).ToArray();
          var impl = Activator.CreateInstance(actualType, parameters);
          desc.Implementation = impl;
          return impl;
        }
      }

      if (!silent)
        throw new Exception($"Service of type {serviceType.Name} is not registered");
      return null;
    }

    public static T Get<T>(bool silent = false)
    {
      var result = Get(typeof(T), silent);
      if (result == null) return default;
      return (T)result;
    }

    /// <summary>
    /// Get the first registered instance for a type with the specified key.
    /// </summary>
    public static object Get(Type serviceType, string key, bool silent = false)
    {
      if (services.TryGetValue(serviceType, out var keyMap)
          && keyMap.TryGetValue(key ?? DefaultKey, out var list))
      {
        for (int i = 0; i < list.Count; i++)
        {
          if (list[i].Implementation != null)
            return list[i].Implementation;
        }
      }

      if (!silent)
        throw new Exception($"Service of type {serviceType.Name} with key \"{key}\" is not registered");
      return null;
    }

    public static T Get<T>(string key, bool silent = false)
    {
      var result = Get(typeof(T), key, silent);
      if (result == null) return default;
      return (T)result;
    }

    // ── GetAll ───────────────────────────────────────────────────

    /// <summary>
    /// Get all instances registered for a type (all keys).
    /// </summary>
    public static IReadOnlyList<T> GetAll<T>()
    {
      if (!services.TryGetValue(typeof(T), out var keyMap))
        return Array.Empty<T>();

      var result = new List<T>();
      foreach (var list in keyMap.Values)
      {
        for (int i = 0; i < list.Count; i++)
        {
          if (list[i].Implementation is T typed)
            result.Add(typed);
        }
      }
      return result;
    }

    /// <summary>
    /// Get all instances registered for a type with the specified key. O(1) key lookup.
    /// </summary>
    public static IReadOnlyList<T> GetAll<T>(string key)
    {
      if (!services.TryGetValue(typeof(T), out var keyMap)
          || !keyMap.TryGetValue(key ?? DefaultKey, out var list))
        return Array.Empty<T>();

      var result = new List<T>(list.Count);
      for (int i = 0; i < list.Count; i++)
      {
        if (list[i].Implementation is T typed)
          result.Add(typed);
      }
      return result;
    }

    public static IReadOnlyList<object> GetAll(Type serviceType)
    {
      if (!services.TryGetValue(serviceType, out var keyMap))
        return Array.Empty<object>();

      var result = new List<object>();
      foreach (var list in keyMap.Values)
      {
        for (int i = 0; i < list.Count; i++)
        {
          if (list[i].Implementation != null)
            result.Add(list[i].Implementation);
        }
      }
      return result;
    }

    public static IReadOnlyList<object> GetAll(Type serviceType, string key)
    {
      if (!services.TryGetValue(serviceType, out var keyMap)
          || !keyMap.TryGetValue(key ?? DefaultKey, out var list))
        return Array.Empty<object>();

      var result = new List<object>(list.Count);
      for (int i = 0; i < list.Count; i++)
      {
        if (list[i].Implementation != null)
          result.Add(list[i].Implementation);
      }
      return result;
    }
  }
}