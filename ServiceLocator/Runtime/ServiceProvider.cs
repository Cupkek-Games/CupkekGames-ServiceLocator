using UnityEngine;


namespace CupkekGames.Systems
{
  public abstract class ServiceProvider : MonoBehaviour, IServiceProvider
  {
    [SerializeField] private bool _autoRegister = true;

    protected virtual void Awake()
    {
      if (_autoRegister)
        RegisterServices();
    }

    protected virtual void OnDestroy()
    {
      if (_autoRegister)
        UnregisterServices();
    }

    public abstract void RegisterServices();

    public abstract void UnregisterServices();
  }
}