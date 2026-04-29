using UnityEngine;

namespace CupkekGames.Services
{
  public abstract class ServiceProviderSO : ScriptableObject, IServiceProvider
  {
    public abstract void RegisterServices();

    public abstract void UnregisterServices();
  }
}
