using System;
using UnityEngine;

namespace CupkekGames.Services
{
  /// <summary>
  /// Registers a <see cref="ScriptableObject"/> instance under a chosen service type
  /// (concrete type or interface). Empty <see cref="RegisterAsType"/> means concrete type.
  /// </summary>
  [Serializable]
  public class ServiceEntry
  {
    public ScriptableObject Instance;
    /// <summary>Assembly-qualified type name, or empty for <see cref="Instance"/>.GetType().</summary>
    public string RegisterAsType;

    /// <summary>
    /// Resolves the key type used with <see cref="ServiceLocator.Register(object, Type)"/>.
    /// Returns null if <see cref="Instance"/> is null or type string is invalid.
    /// </summary>
    public Type ResolveServiceType() => ResolveServiceType(Instance, RegisterAsType);

    /// <summary>Resolves locator key type for unregister / editor cache without a <see cref="ServiceEntry"/> instance.</summary>
    public static Type ResolveServiceType(ScriptableObject instance, string registerAsType)
    {
      if (instance == null)
        return null;

      if (string.IsNullOrWhiteSpace(registerAsType))
        return instance.GetType();

      Type t = Type.GetType(registerAsType.Trim(), throwOnError: false);
      return t ?? instance.GetType();
    }
  }
}
