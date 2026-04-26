using System;
using System.Collections.Generic;
using System.Linq;
using CupkekGames.Core.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CupkekGames.Systems.Editor
{
  /// <summary>
  /// UI Toolkit property drawer for <see cref="ServiceEntry"/> — single-row layout (asset + register-as type).
  /// </summary>
  [CustomPropertyDrawer(typeof(ServiceEntry))]
  public class ServiceEntryDrawer : PropertyDrawer
  {
    private const string ConcreteLabel = "(Concrete Type)";

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
      return EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
      SerializedProperty instanceProp = property.FindPropertyRelative(nameof(ServiceEntry.Instance));
      SerializedProperty registerAsProp = property.FindPropertyRelative(nameof(ServiceEntry.RegisterAsType));
      if (instanceProp == null || registerAsProp == null)
      {
        EditorGUI.LabelField(position, label.text, "Malformed ServiceEntry");
        return;
      }

      EditorGUI.BeginProperty(position, label, property);
      float line = EditorGUIUtility.singleLineHeight;
      float gap = 6f;
      Rect row = EditorGUI.PrefixLabel(position, label);
      float asW = 22f;
      float avail = row.width - asW - gap * 2f;
      float instW = Mathf.Max(80f, avail * 0.5f);
      Rect instRect = new Rect(row.x, row.y, instW, line);
      Rect asRect = new Rect(instRect.xMax + gap, row.y, asW, line);
      Rect dropRect = new Rect(asRect.xMax + gap, row.y, Mathf.Max(40f, row.xMax - asRect.xMax - gap), line);

      EditorGUI.PropertyField(instRect, instanceProp, GUIContent.none);
      EditorGUI.LabelField(asRect, new GUIContent("as", "Registered as (ServiceLocator key type)"));

      var so = instanceProp.objectReferenceValue as ScriptableObject;
      BuildRegisterAsTypeOptionLists(so, out List<string> labels, out List<string> qualified);
      string current = registerAsProp.stringValue ?? "";
      int index = 0;
      if (!string.IsNullOrEmpty(current))
      {
        int found = qualified.FindIndex(q => q == current);
        if (found >= 0)
          index = found;
      }

      EditorGUI.BeginChangeCheck();
      int newIndex = EditorGUI.Popup(dropRect, index, labels.ToArray());
      if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < qualified.Count)
      {
        registerAsProp.stringValue = qualified[newIndex];
        registerAsProp.serializedObject.ApplyModifiedProperties();
      }

      EditorGUI.EndProperty();
    }

    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
      SerializedProperty instanceProp = property.FindPropertyRelative(nameof(ServiceEntry.Instance));
      SerializedProperty registerAsProp = property.FindPropertyRelative(nameof(ServiceEntry.RegisterAsType));

      // One row: [ScriptableObject picker]  as  [Register-as dropdown]
      // List "Element N" stays on the left from the parent list; no second label row on the ObjectField.
      var row = new VisualElement
      {
        tooltip =
          "ScriptableObject instance and the type key used with ServiceLocator.Register(instance, type). " +
          "Pick an interface for register-as, or (Concrete Type) for the asset’s runtime class.",
        style =
        {
          flexDirection = FlexDirection.Row,
          alignItems = Align.Center,
          flexGrow = 1,
          width = new Length(100, LengthUnit.Percent),
          minHeight = 20,
        }
      };

      var instanceField = new ObjectField
      {
        objectType = typeof(ScriptableObject),
        allowSceneObjects = false,
        label = string.Empty,
      };
      // Equal share of row: basis 0 + grow 1 so asset name / type string length does not change column widths.
      instanceField.style.flexGrow = 1f;
      instanceField.style.flexShrink = 1f;
      instanceField.style.flexBasis = 0;
      instanceField.style.minWidth = 0;
      instanceField.BindProperty(instanceProp);
      row.Add(instanceField);

      var asLabel = new Label("as")
      {
        style =
        {
          flexGrow = 0,
          flexShrink = 0,
          marginLeft = 6f,
          marginRight = 4f,
          minWidth = 18f,
          unityTextAlign = TextAnchor.MiddleCenter,
          color = EditorColorPalette.TextMuted,
          fontSize = 11,
        }
      };
      asLabel.tooltip = "Registered as (ServiceLocator key type)";
      row.Add(asLabel);

      var dropdown = new DropdownField { label = string.Empty };
      dropdown.style.flexGrow = 1f;
      dropdown.style.flexShrink = 1f;
      dropdown.style.flexBasis = 0;
      dropdown.style.minWidth = 0;
      dropdown.tooltip = "Interface or concrete type name stored for registration.";
      row.Add(dropdown);

      void RefreshDropdown(bool preserveSelection)
      {
        var so = instanceProp.objectReferenceValue as ScriptableObject;
        BuildRegisterAsTypeOptionLists(so, out List<string> labels, out List<string> qualified);

        string current = registerAsProp.stringValue ?? "";
        int index = 0;
        if (preserveSelection && !string.IsNullOrEmpty(current))
        {
          int found = qualified.FindIndex(q => q == current);
          if (found >= 0)
            index = found;
        }
        else if (preserveSelection && string.IsNullOrEmpty(current))
          index = 0;
        else if (!preserveSelection)
        {
          int found = qualified.FindIndex(q => q == current);
          index = found >= 0 ? found : 0;
        }

        dropdown.choices = labels;
        dropdown.index = Mathf.Clamp(index, 0, Mathf.Max(0, labels.Count - 1));

        string next = qualified[dropdown.index];
        if (registerAsProp.stringValue != next)
        {
          registerAsProp.stringValue = next;
          property.serializedObject.ApplyModifiedProperties();
        }
      }

      RefreshDropdown(true);

      instanceField.RegisterValueChangedCallback(_ =>
      {
        property.serializedObject.ApplyModifiedProperties();
        RefreshDropdown(false);
      });

      dropdown.RegisterValueChangedCallback(_ =>
      {
        var so = instanceProp.objectReferenceValue as ScriptableObject;
        BuildRegisterAsTypeOptionLists(so, out List<string> _, out List<string> qualified);
        int i = dropdown.index;
        if (i >= 0 && i < qualified.Count)
        {
          registerAsProp.stringValue = qualified[i];
          property.serializedObject.ApplyModifiedProperties();
        }
      });

      return row;
    }

    /// <summary>
    /// Named to avoid clashing with UI Toolkit internals that use <c>BuildChoices</c> (wrong overload resolution with <c>out</c> args).
    /// </summary>
    private static void BuildRegisterAsTypeOptionLists(ScriptableObject instance, out List<string> labels, out List<string> qualifiedNames)
    {
      labels = new List<string> { ConcreteLabel };
      qualifiedNames = new List<string> { "" };

      if (instance == null)
        return;

      IEnumerable<Type> interfaces = instance.GetType().GetInterfaces()
        .Where(IsUserFacingInterface)
        .OrderBy(t => t.FullName, StringComparer.Ordinal);

      foreach (Type i in interfaces)
      {
        labels.Add(i.FullName ?? i.Name);
        qualifiedNames.Add(i.AssemblyQualifiedName);
      }
    }

    private static bool IsUserFacingInterface(Type i)
    {
      if (!i.IsPublic && !i.IsNestedPublic)
        return false;
      string ns = i.Namespace;
      if (ns != null)
      {
        if (ns.StartsWith("UnityEngine", StringComparison.Ordinal) ||
            ns.StartsWith("UnityEditor", StringComparison.Ordinal))
          return false;
      }
      return true;
    }
  }
}
