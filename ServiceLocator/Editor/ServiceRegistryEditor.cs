using CupkekGames.EditorUI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace CupkekGames.Services.Editor
{
  [CustomEditor(typeof(ServiceRegistry))]
  public class ServiceRegistryEditor : UnityEditor.Editor
  {
    public override VisualElement CreateInspectorGUI()
    {
      VisualElement root = new VisualElement();

      AddHeader(root, "SO-based registries to load.\nUseful for project-wide services defined in assets.");
      root.Add(new PropertyField(serializedObject.FindProperty("_registries")));

      AddHeader(root, "Service Providers with custom registration logic.\nEach provider controls how it registers/unregisters.");
      root.Add(new PropertyField(serializedObject.FindProperty("_providers")));

      AddHeader(root, "Plain components registered as their concrete type.\nNo custom logic - just drag and drop.");
      root.Add(new PropertyField(serializedObject.FindProperty("_components")));

      AddHeader(root, "ScriptableObject services with optional register-as interface.\nUse Service Entries; choose concrete or interface per entry.");
      root.Add(new PropertyField(serializedObject.FindProperty("_serviceEntries")));

      AddHeader(root, "Deprecated — use Service Entries above. Will be removed after migration.");
      root.Add(new PropertyField(serializedObject.FindProperty("_services")));

      root.Add(new PropertyField(serializedObject.FindProperty("_dontDestroyOnLoad")));

      return root;
    }

    private void AddHeader(VisualElement root, string text)
    {
      var label = new Label(text);
      label.style.whiteSpace = WhiteSpace.Normal;
      label.style.backgroundColor = EditorColorPalette.SurfaceWeak;
      label.style.borderTopColor = EditorColorPalette.BorderMedium;
      label.style.borderBottomColor = EditorColorPalette.BorderMedium;
      label.style.borderLeftColor = EditorColorPalette.BorderMedium;
      label.style.borderRightColor = EditorColorPalette.BorderMedium;
      label.style.color = EditorColorPalette.TextSecondary;
      label.style.borderTopWidth = 1f;
      label.style.borderBottomWidth = 1f;
      label.style.borderLeftWidth = 1f;
      label.style.borderRightWidth = 1f;
      label.style.borderTopLeftRadius = 4f;
      label.style.borderTopRightRadius = 4f;
      label.style.borderBottomLeftRadius = 4f;
      label.style.borderBottomRightRadius = 4f;
      label.style.paddingTop = 5f;
      label.style.paddingBottom = 5f;
      label.style.paddingLeft = 10f;
      label.style.paddingRight = 10f;
      label.style.marginTop = 8f;
      label.style.marginBottom = 4f;
      root.Add(label);
    }
  }
}
