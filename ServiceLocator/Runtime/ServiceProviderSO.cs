using UnityEngine;

namespace CupkekGames.Systems
{
  public abstract class ServiceProviderSO : ScriptableObject, IServiceProvider
  {
    public abstract void RegisterServices();

    public abstract void UnregisterServices();
  }
}
