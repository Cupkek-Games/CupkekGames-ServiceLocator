using System;
using System.Collections.Generic;
using System.Linq;
using CupkekGames.Systems;
using CupkekGames.Core;
using CupkekGames.Core.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CupkekGames.Systems.Editor
{
  public class ServiceLocatorDebugWindow : EditorWindow
  {
    // ── State ──
    private TextField _searchField;
    private string _searchFilter = "";
    private ScrollView _typeListScroll;
    private ScrollView _middleScroll;
    private VisualElement _inspectorColumn;
    private VisualElement _inspectorPanel;

    private Label _summaryLabel;
    private ServiceDescriptor _selectedDescriptor;
    private readonly List<VisualElement> _typeRows = new();
    private readonly HashSet<string> _expandedKeys = new();
    private readonly List<VisualElement> _instanceRows = new();
    private readonly HashSet<string> _collapsedNamespaces = new();

    // ── Instance-first view ──
    private object _selectedInstance;

    private class InstanceEntry
    {
      public object Implementation;
      public string DisplayName;
      public Type ImplementationType;
      public List<RegistrationInfo> Registrations = new();
    }

    private struct RegistrationInfo
    {
      public Type ServiceType;
      public string Key;
    }

    private readonly List<InstanceEntry> _filteredInstances = new();
    private readonly List<VisualElement> _instanceListRows = new();

    private AssetFinderToolbar _registryFilterToolbar;
    private Toggle _autoRegisterToggle;
    private VisualElement _mainHost;
    private VisualElement _registryOverlayRoot;
    private Button _registryToolsButton;
    private bool _registryPanelOpen;

    // ── Colors ──
    private static readonly Color RowBg = new(0, 0, 0, 0.06f);
    private static readonly Color RowHoverBg = new(1f, 1f, 1f, 0.06f);
    private static readonly Color InstanceSelectedBg = new(0.25f, 0.55f, 0.85f, 0.28f);
    private static readonly Color GreenDot = new(0.4f, 0.8f, 0.4f);
    private static readonly Color YellowDot = new(0.85f, 0.75f, 0.3f);
    private static readonly Color RedDot = new(0.85f, 0.35f, 0.35f);
    private static readonly Color MutedText = new(0.6f, 0.6f, 0.6f);
    private static readonly Color BadgeBg = new(0.3f, 0.5f, 0.8f, 0.3f);
    private static readonly Color KeyBadgeBg = new(0.5f, 0.5f, 0.5f, 0.25f);
    private static readonly Color SeparatorColor = new(0, 0, 0, 0.15f);
    private static readonly Color DetailHeaderBg = new(0, 0, 0, 0.08f);
    private static readonly Color FoldoutHeaderBg = new(0, 0, 0, 0.06f);
    private static readonly Color NsHeaderBg = new(0, 0, 0, 0.04f);

    [MenuItem("Tools/CupkekGames/Service Locator Debug")]
    public static void ShowWindow()
    {
      var window = GetWindow<ServiceLocatorDebugWindow>("Service Locator");
      window.minSize = new Vector2(780, 320);
    }

    private void OnDestroy()
    {
      ServiceLocator.OnChanged -= Refresh;
    }

    public void CreateGUI()
    {
      var root = rootVisualElement;
      root.style.flexDirection = FlexDirection.Column;
      root.AddToClassList("sl-root");

      StyleSheet paletteUss = AssetDatabase.LoadAssetAtPath<StyleSheet>(
        "Packages/com.cupkekgames.luna/Editor/EditorColorPalette.uss");
      StyleSheet windowUss = AssetDatabase.LoadAssetAtPath<StyleSheet>(
        "Packages/com.cupkekgames.data/ServiceLocator/Editor/ServiceLocatorDebugWindow.uss");
      if (paletteUss != null)
        root.styleSheets.Add(paletteUss);
      if (windowUss != null)
        root.styleSheets.Add(windowUss);

      // ── Toolbar: search, refresh, editor registries (retrigger + auto-register on load), registry tools ──
      var toolbar = new VisualElement();
      toolbar.AddToClassList("sl-toolbar");
      toolbar.style.flexDirection = FlexDirection.Row;
      toolbar.style.flexWrap = Wrap.Wrap;
      toolbar.style.alignItems = Align.Center;
      toolbar.style.paddingTop = 6;
      toolbar.style.paddingBottom = 6;
      toolbar.style.paddingLeft = 10;
      toolbar.style.paddingRight = 10;
      toolbar.style.borderBottomWidth = 1;
      toolbar.style.borderBottomColor = SeparatorColor;

      static VisualElement ToolbarSep()
      {
        var sep = new VisualElement();
        sep.AddToClassList("sl-toolbar-sep");
        return sep;
      }

      var searchContainer = new VisualElement();
      searchContainer.style.flexGrow = 1;
      searchContainer.style.minWidth = 140;
      searchContainer.style.marginRight = 4;
      searchContainer.style.flexDirection = FlexDirection.Row;
      searchContainer.style.alignItems = Align.Center;

      _searchField = new TextField();
      _searchField.AddToClassList("sl-toolbar-search");
      _searchField.style.flexGrow = 1;
      _searchField.textEdition.placeholder = "Search services\u2026";

      var searchClearBtn = new Button(() =>
      {
        _searchField.value = "";
      })
      {
        text = "\u00d7"
      };
      searchClearBtn.style.display = DisplayStyle.None;
      searchClearBtn.style.position = Position.Absolute;
      searchClearBtn.style.right = 2;
      searchClearBtn.style.top = 1;
      searchClearBtn.style.width = 18;
      searchClearBtn.style.height = 16;
      searchClearBtn.style.fontSize = 13;
      searchClearBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
      searchClearBtn.style.backgroundColor = Color.clear;
      searchClearBtn.style.borderTopWidth = 0;
      searchClearBtn.style.borderBottomWidth = 0;
      searchClearBtn.style.borderLeftWidth = 0;
      searchClearBtn.style.borderRightWidth = 0;
      searchClearBtn.style.color = MutedText;
      searchClearBtn.style.paddingLeft = 0;
      searchClearBtn.style.paddingRight = 0;
      searchClearBtn.style.paddingTop = 0;
      searchClearBtn.style.paddingBottom = 0;
      searchClearBtn.tooltip = "Clear search";

      _searchField.RegisterValueChangedCallback(evt =>
      {
        _searchFilter = evt.newValue?.ToLowerInvariant() ?? "";
        searchClearBtn.style.display = string.IsNullOrEmpty(evt.newValue)
          ? DisplayStyle.None
          : DisplayStyle.Flex;
        Refresh();
      });

      searchContainer.Add(_searchField);
      searchContainer.Add(searchClearBtn);
      toolbar.Add(searchContainer);

      var refreshButton = new Button(Refresh) { text = "\u21bb" };
      refreshButton.AddToClassList("sl-toolbar-icon-btn");
      refreshButton.tooltip = "Refresh";
      toolbar.Add(refreshButton);

      var copyJsonBtn = new Button(CopyStateAsJson) { text = "{}" };
      copyJsonBtn.AddToClassList("sl-toolbar-icon-btn");
      copyJsonBtn.tooltip = "Copy current ServiceLocator state as JSON to clipboard";
      toolbar.Add(copyJsonBtn);

      var clearLocatorToolbarBtn = new Button(ClearLocatorAndRefresh) { text = "C" };
      clearLocatorToolbarBtn.AddToClassList("sl-toolbar-icon-btn");
      clearLocatorToolbarBtn.AddToClassList("sl-toolbar-clear-locator-btn");
      clearLocatorToolbarBtn.tooltip =
        "Clear locator — ServiceLocator.ClearAll(); removes every registration (does not change ServiceRegistrySO assets).";
      toolbar.Add(clearLocatorToolbarBtn);

      var retriggerEditorRegistriesBtn = new Button(RetriggerEditorRegistrationsAll)
      {
        text = "Retrigger editor registries"
      };
      retriggerEditorRegistriesBtn.style.marginLeft = 8;
      retriggerEditorRegistriesBtn.tooltip =
        "Runs the editor registration pipeline for every ServiceRegistrySO in the project: UnregisterAll, then RegisterAll when Register In Editor is enabled on each asset.";
      toolbar.Add(retriggerEditorRegistriesBtn);

      _autoRegisterToggle = new Toggle("Auto-register on load")
      {
        value = ServiceRegistryEditorBootstrap.AutoRegisterOnEditorLoad
      };
      _autoRegisterToggle.AddToClassList("sl-toolbar-auto-register");
      _autoRegisterToggle.tooltip =
        "When enabled, reapplies Register In Editor after domain reload and when returning from Play Mode.";
      _autoRegisterToggle.RegisterValueChangedCallback(evt =>
      {
        ServiceRegistryEditorBootstrap.AutoRegisterOnEditorLoad = evt.newValue;
      });
      toolbar.Add(_autoRegisterToggle);

      toolbar.Add(ToolbarSep());

      _registryToolsButton = new Button(() => SetRegistryPanelOpen(!_registryPanelOpen));
      _registryToolsButton.AddToClassList("sl-toolbar-registry-btn");
      _registryToolsButton.tooltip =
        "Open or close the panel: filtered retrigger, Asset Finder filters, bulk Register In Editor.";
      toolbar.Add(_registryToolsButton);
      UpdateRegistryToolsButtonLabel();

      root.Add(toolbar);

      // ── Summary strip (always visible) ──
      _summaryLabel = new Label();
      _summaryLabel.AddToClassList("sl-summary");
      _summaryLabel.style.fontSize = 11;
      _summaryLabel.style.paddingTop = 5;
      _summaryLabel.style.paddingBottom = 5;
      _summaryLabel.style.paddingLeft = 12;
      _summaryLabel.style.paddingRight = 12;
      _summaryLabel.style.borderBottomWidth = 1;
      _summaryLabel.style.borderBottomColor = SeparatorColor;
      root.Add(_summaryLabel);

      // ── Main area: split + registry overlay (overlay does not use layout height when closed) ──
      _mainHost = new VisualElement();
      _mainHost.AddToClassList("sl-main-host");
      _mainHost.style.flexGrow = 1;
      _mainHost.style.minHeight = 0;
      _mainHost.style.position = Position.Relative;
      _mainHost.RegisterCallback<GeometryChangedEvent>(_ => UpdateRegistryPanelLayout());
      root.Add(_mainHost);

      // ── Outer split: types | (middle + inspector) ──
      var outerSplit = new TwoPaneSplitView(0, 220f, TwoPaneSplitViewOrientation.Horizontal);
      outerSplit.style.flexGrow = 1;
      _mainHost.Add(outerSplit);

      var leftPane = new VisualElement();
      leftPane.AddToClassList("sl-left-pane");
      leftPane.style.minWidth = 160;
      outerSplit.Add(leftPane);

      _typeListScroll = new ScrollView(ScrollViewMode.Vertical);
      _typeListScroll.style.flexGrow = 1;
      leftPane.Add(_typeListScroll);

      // ── Middle column | inspector column (inspector only when an instance row is selected) ──
      var innerRow = new VisualElement();
      innerRow.AddToClassList("sl-inner-row");
      innerRow.style.flexDirection = FlexDirection.Row;
      innerRow.style.flexGrow = 1;
      innerRow.style.minWidth = 400;
      innerRow.style.minHeight = 0;
      outerSplit.Add(innerRow);

      var middlePane = new VisualElement();
      middlePane.AddToClassList("sl-middle-pane");
      middlePane.style.flexGrow = 1;
      middlePane.style.minWidth = 200;
      middlePane.style.minHeight = 0;
      innerRow.Add(middlePane);

      _middleScroll = new ScrollView(ScrollViewMode.Vertical);
      _middleScroll.style.flexGrow = 1;
      middlePane.Add(_middleScroll);

      _inspectorColumn = new VisualElement();
      _inspectorColumn.AddToClassList("sl-inspector-column");
      _inspectorColumn.style.display = DisplayStyle.None;
      _inspectorColumn.style.flexDirection = FlexDirection.Column;
      _inspectorColumn.style.flexShrink = 0;
      _inspectorColumn.style.width = 300;
      _inspectorColumn.style.minWidth = 240;
      _inspectorColumn.style.maxWidth = 520;
      _inspectorColumn.style.borderLeftWidth = 1;
      _inspectorColumn.style.borderLeftColor = SeparatorColor;
      innerRow.Add(_inspectorColumn);

      var inspectorActionsBar = new VisualElement();
      inspectorActionsBar.AddToClassList("sl-inspector-actions");
      inspectorActionsBar.style.flexDirection = FlexDirection.Row;
      inspectorActionsBar.style.flexWrap = Wrap.Wrap;
      inspectorActionsBar.style.alignItems = Align.Center;
      inspectorActionsBar.style.flexShrink = 0;
      _inspectorColumn.Add(inspectorActionsBar);

      var copyTypeBtn = new Button(CopySelectedTypeName) { text = "Copy" };
      copyTypeBtn.tooltip = "Copy selected service type name (with namespace)";
      inspectorActionsBar.Add(copyTypeBtn);

      var pingBtn = new Button(PingSelected) { text = "Ping" };
      pingBtn.style.marginLeft = 4;
      pingBtn.tooltip = "Ping implementation in Project/Hierarchy (or script for plain C# services)";
      inspectorActionsBar.Add(pingBtn);

      var openBtn = new Button(OpenSelectedInInspector) { text = "Open" };
      openBtn.style.marginLeft = 4;
      openBtn.tooltip = "Select implementation in the main Inspector";
      inspectorActionsBar.Add(openBtn);

      _inspectorPanel = new VisualElement();
      _inspectorPanel.AddToClassList("sl-inspector-pane");
      _inspectorPanel.style.flexGrow = 1;
      _inspectorPanel.style.minHeight = 0;
      _inspectorPanel.style.overflow = Overflow.Hidden;
      _inspectorColumn.Add(_inspectorPanel);

      // ── Registry tools: overlay on main split (no layout height when closed) ──
      _registryOverlayRoot = new VisualElement();
      _registryOverlayRoot.AddToClassList("sl-registry-overlay");
      _registryOverlayRoot.style.display = DisplayStyle.None;
      _registryOverlayRoot.style.position = Position.Absolute;
      _registryOverlayRoot.style.left = 8;
      _registryOverlayRoot.style.right = 8;
      _registryOverlayRoot.style.top = 8;
      _registryOverlayRoot.style.flexDirection = FlexDirection.Column;
      _mainHost.Add(_registryOverlayRoot);

      var registryChrome = new VisualElement();
      registryChrome.style.flexDirection = FlexDirection.Column;
      registryChrome.style.flexGrow = 1;
      registryChrome.style.minHeight = 0;
      _registryOverlayRoot.Add(registryChrome);

      var registryCard = new VisualElement();
      registryCard.AddToClassList("sl-registry-card");
      registryCard.style.flexDirection = FlexDirection.Column;
      registryCard.style.flexGrow = 1;
      registryCard.style.minHeight = 0;
      registryCard.style.overflow = Overflow.Hidden;
      registryChrome.Add(registryCard);

      var registryHeader = new VisualElement();
      registryHeader.AddToClassList("sl-registry-card-header");
      registryHeader.style.flexDirection = FlexDirection.Row;
      registryHeader.style.alignItems = Align.Center;
      registryHeader.style.flexShrink = 0;
      registryCard.Add(registryHeader);

      var registryTitle = new Label("Service registry");
      registryTitle.style.flexGrow = 1;
      registryTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
      registryTitle.style.fontSize = 12;
      registryHeader.Add(registryTitle);

      var closeRegistryBtn = new Button(() => SetRegistryPanelOpen(false)) { text = "\u00d7" };
      closeRegistryBtn.AddToClassList("sl-registry-close");
      closeRegistryBtn.style.width = 24;
      closeRegistryBtn.style.height = 22;
      closeRegistryBtn.tooltip = "Close registry tools";
      registryHeader.Add(closeRegistryBtn);

      var registryScroll = new ScrollView(ScrollViewMode.Vertical);
      registryScroll.style.flexGrow = 1;
      registryScroll.style.minHeight = 0;
      registryCard.Add(registryScroll);

      var bootstrap = new VisualElement();
      bootstrap.AddToClassList("sl-registry-body");
      bootstrap.style.flexDirection = FlexDirection.Column;
      registryScroll.Add(bootstrap);

      var sectionEditor = new Label("Filtered actions");
      sectionEditor.AddToClassList("sl-registry-section-title");
      bootstrap.Add(sectionEditor);

      var bootRow = new VisualElement();
      bootRow.style.flexDirection = FlexDirection.Row;
      bootRow.style.flexWrap = Wrap.Wrap;
      bootRow.style.alignItems = Align.Center;
      bootRow.style.marginBottom = 6;

      var retriggerFiltered = new Button(RetriggerEditorRegistrationsFiltered) { text = "Retrigger (filtered)" };
      retriggerFiltered.tooltip =
        "For each ServiceRegistrySO matching the filters below: UnregisterAll, then RegisterAll if Register In Editor is checked. Use the toolbar for retrigger on all assets.";
      bootRow.Add(retriggerFiltered);

      bootstrap.Add(bootRow);

      var filterFoldout = new FoldoutSection("Asset Finder filters",
        "ServiceLocatorDebugWindow_RegistryFilters",
        false);
      var toolbarConfig = new AssetFinderToolbarConfig
      {
        AssetType = typeof(ServiceRegistrySO),
        PersistenceKey = "ServiceLocatorDebugWindow_ServiceRegistrySO"
      };
      _registryFilterToolbar = new AssetFinderToolbar(toolbarConfig);
      filterFoldout.Content.Add(_registryFilterToolbar);
      bootstrap.Add(filterFoldout);

      var bulkFoldout = new FoldoutSection("Bulk: Register In Editor on assets",
        "ServiceLocatorDebugWindow_BulkRegistry",
        false);
      var bulkHint = new Label(
        "Writes the Register In Editor flag on ServiceRegistrySO assets. Filtered actions use the Asset Finder filters above.")
      {
        style =
        {
          color = MutedText,
          fontSize = 11,
          whiteSpace = WhiteSpace.Normal,
          marginBottom = 6
        }
      };
      bulkFoldout.Content.Add(bulkHint);
      var bulkRow = new VisualElement();
      bulkRow.style.flexDirection = FlexDirection.Row;
      bulkRow.style.flexWrap = Wrap.Wrap;
      bulkRow.style.alignItems = Align.Center;

      var tickFiltered = new Button(() => BulkSetRegisterInEditor(true, true)) { text = "Tick (filtered)" };
      tickFiltered.tooltip = "Set Register In Editor on registries matching filters.";
      var tickAll = new Button(() => BulkSetRegisterInEditor(true, false)) { text = "Tick (all)" };
      tickAll.tooltip = "Set Register In Editor on every ServiceRegistrySO.";
      var untickFiltered = new Button(() => BulkSetRegisterInEditor(false, true)) { text = "Untick (filtered)" };
      untickFiltered.tooltip = "Clear Register In Editor and UnregisterAll on matching registries.";
      var untickAll = new Button(() => BulkSetRegisterInEditor(false, false)) { text = "Untick (all)" };
      untickAll.tooltip = "Clear Register In Editor on every ServiceRegistrySO; UnregisterAll each.";

      bulkRow.Add(tickFiltered);
      bulkRow.Add(tickAll);
      bulkRow.Add(untickFiltered);
      bulkRow.Add(untickAll);
      bulkFoldout.Content.Add(bulkRow);
      bootstrap.Add(bulkFoldout);

      ServiceLocator.OnChanged += Refresh;
      Refresh();
    }

    private void UpdateRegistryToolsButtonLabel()
    {
      if (_registryToolsButton == null)
        return;
      _registryToolsButton.text = _registryPanelOpen ? "Hide registry tools" : "Show registry tools";
    }

    private void SetRegistryPanelOpen(bool open)
    {
      _registryPanelOpen = open;
      if (_registryOverlayRoot != null)
        _registryOverlayRoot.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
      UpdateRegistryToolsButtonLabel();
      UpdateRegistryPanelLayout();
      if (open)
        EditorApplication.delayCall += () =>
        {
          if (_registryPanelOpen)
            UpdateRegistryPanelLayout();
        };
    }

    private void UpdateRegistryPanelLayout()
    {
      if (_mainHost == null || _registryOverlayRoot == null || !_registryPanelOpen)
        return;
      float h = _mainHost.layout.height;
      if (h < 2f)
        return;
      float panelH = Mathf.Clamp(h * 0.7f, 200f, 560f);
      _registryOverlayRoot.style.height = panelH;
    }

    private void CopySelectedTypeName()
    {
      if (_selectedDescriptor?.Implementation != null)
      {
        EditorGUIUtility.systemCopyBuffer =
          ServiceLocatorDebugTypeDisplay.FormatWithNamespace(_selectedDescriptor.Implementation.GetType());
        return;
      }
      if (_selectedInstance != null)
      {
        EditorGUIUtility.systemCopyBuffer =
          ServiceLocatorDebugTypeDisplay.FormatWithNamespace(_selectedInstance.GetType());
      }
    }

    private void PingSelected()
    {
      if (_selectedDescriptor?.Implementation is UnityEngine.Object uo && uo != null)
      {
        EditorGUIUtility.PingObject(uo);
        return;
      }

      MonoScript script = FindScriptForDescriptor(_selectedDescriptor);
      if (script != null)
        EditorGUIUtility.PingObject(script);
    }

    private void OpenSelectedInInspector()
    {
      if (_selectedDescriptor?.Implementation is UnityEngine.Object uo && uo != null)
      {
        Selection.activeObject = uo;
        return;
      }

      MonoScript script = FindScriptForDescriptor(_selectedDescriptor);
      if (script != null)
        Selection.activeObject = script;
    }

    private void RetriggerEditorRegistrationsFiltered()
    {
      if (_registryFilterToolbar == null)
        return;
      ServiceRegistryEditorBootstrap.RetriggerEditorRegistrationsFiltered(_registryFilterToolbar.GetFiltersCopy());
      Refresh();
    }

    private void RetriggerEditorRegistrationsAll()
    {
      ServiceRegistryEditorBootstrap.RetriggerEditorRegistrationsAll();
      Refresh();
    }

    private void ClearLocatorAndRefresh()
    {
      ServiceRegistryEditorBootstrap.ClearAllServices();
      Refresh();
    }

    private void CopyStateAsJson()
    {
      var all = ServiceLocator.RegisteredServices;
      var sb = new System.Text.StringBuilder(2048);
      sb.AppendLine("{");

      var sortedTypes = all.Keys
        .Where(t => CountInstances(all[t]) > 0)
        .OrderBy(t => t.FullName ?? t.Name)
        .ToList();

      for (int ti = 0; ti < sortedTypes.Count; ti++)
      {
        Type type = sortedTypes[ti];
        var keyMap = all[type];
        string typeName = ServiceLocatorDebugTypeDisplay.FormatWithNamespace(type);
        sb.AppendLine($"  \"{EscapeJson(typeName)}\": {{");

        var sortedKeys = keyMap.Keys.OrderBy(k => k).ToList();
        for (int ki = 0; ki < sortedKeys.Count; ki++)
        {
          string key = sortedKeys[ki];
          string keyDisplay = string.IsNullOrEmpty(key) ? "(default)" : key;
          var descriptors = keyMap[key];
          sb.AppendLine($"    \"{EscapeJson(keyDisplay)}\": [");

          for (int di = 0; di < descriptors.Count; di++)
          {
            var desc = descriptors[di];
            string implType = desc.ImplementationType?.FullName ?? desc.Implementation?.GetType().FullName ?? "null";
            string implName = desc.Implementation is UnityEngine.Object uo && uo != null ? uo.name : null;

            sb.Append($"      {{ \"type\": \"{EscapeJson(implType)}\"");
            if (implName != null)
              sb.Append($", \"name\": \"{EscapeJson(implName)}\"");
            sb.Append(" }");
            sb.AppendLine(di < descriptors.Count - 1 ? "," : "");
          }

          sb.Append("    ]");
          sb.AppendLine(ki < sortedKeys.Count - 1 ? "," : "");
        }

        sb.Append("  }");
        sb.AppendLine(ti < sortedTypes.Count - 1 ? "," : "");
      }

      sb.AppendLine("}");
      EditorGUIUtility.systemCopyBuffer = sb.ToString();
      Debug.Log($"ServiceLocator state copied to clipboard ({sortedTypes.Count} types).");
    }

    private static string EscapeJson(string s)
    {
      if (string.IsNullOrEmpty(s)) return s;
      return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private void BulkSetRegisterInEditor(bool value, bool useFilters)
    {
      List<ServiceRegistrySO> list;
      if (useFilters)
      {
        if (_registryFilterToolbar == null)
          return;
        list = AssetFinder.FindAssets<ServiceRegistrySO>(_registryFilterToolbar.GetFiltersCopy());
      }
      else
      {
        list = ServiceRegistryEditorBootstrap.FindAllServiceRegistryAssets();
      }

      ServiceRegistryEditorBootstrap.SetRegisterInEditorBulk(value, list);
      Refresh();
    }

    // ════════════════════════════════════════
    //  Refresh
    // ════════════════════════════════════════

    private void Refresh()
    {
      _typeListScroll.Clear();
      _typeRows.Clear();
      _instanceListRows.Clear();
      _filteredInstances.Clear();

      var all = ServiceLocator.RegisteredServices;

      // Build instance-first map: group all registrations by unique implementation object
      var byImpl = new Dictionary<object, InstanceEntry>(new ObjectReferenceComparer());

      foreach (var kvp in all)
      {
        Type serviceType = kvp.Key;
        foreach (var keyPair in kvp.Value)
        {
          string key = keyPair.Key;
          foreach (var desc in keyPair.Value)
          {
            object impl = desc.Implementation;
            if (impl == null)
              continue;

            if (!byImpl.TryGetValue(impl, out var entry))
            {
              string name = impl is UnityEngine.Object uo && uo != null ? uo.name : null;
              entry = new InstanceEntry
              {
                Implementation = impl,
                DisplayName = name ?? ServiceLocatorDebugTypeDisplay.Format(impl.GetType()),
                ImplementationType = impl.GetType()
              };
              byImpl[impl] = entry;
            }

            entry.Registrations.Add(new RegistrationInfo { ServiceType = serviceType, Key = key });
          }
        }
      }

      // Filter by search
      foreach (var entry in byImpl.Values)
      {
        if (!InstanceMatchesFilter(entry))
          continue;
        _filteredInstances.Add(entry);
      }

      _filteredInstances.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));

      int totalInstances = _filteredInstances.Count;
      int totalRegistrations = 0;
      foreach (var e in _filteredInstances)
        totalRegistrations += e.Registrations.Count;

      _summaryLabel.text = totalInstances == 0
        ? "No services registered"
        : $"{totalInstances} instance{(totalInstances != 1 ? "s" : "")}, {totalRegistrations} registration{(totalRegistrations != 1 ? "s" : "")}";

      if (totalInstances == 0)
      {
        ShowEmptyList(byImpl.Count > 0 ? "No matches for search filter." : "No services registered.");
        ClearMiddleAndInspector("No services to display.");
        return;
      }

      PopulateInstanceList();

      // Restore or pick selection
      InstanceEntry selected = _selectedInstance != null
        ? _filteredInstances.Find(e => e.Implementation == _selectedInstance)
        : null;

      if (selected != null)
        SelectInstance(selected);
      else if (_filteredInstances.Count > 0)
        SelectInstance(_filteredInstances[0]);
      else
        ClearMiddleAndInspector("Select an instance.");
    }

    /// <summary>Reference-equality comparer for grouping by implementation object.</summary>
    private class ObjectReferenceComparer : IEqualityComparer<object>
    {
      public new bool Equals(object x, object y) => ReferenceEquals(x, y);
      public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    private void PopulateInstanceList()
    {
      // Group instances by implementation type namespace
      var byNs = new Dictionary<string, List<InstanceEntry>>(StringComparer.OrdinalIgnoreCase);
      foreach (var entry in _filteredInstances)
      {
        string ns = entry.ImplementationType?.Namespace;
        if (string.IsNullOrEmpty(ns)) ns = "(global)";
        if (!byNs.TryGetValue(ns, out var list))
        {
          list = new List<InstanceEntry>();
          byNs[ns] = list;
        }
        list.Add(entry);
      }

      bool forceExpand = !string.IsNullOrEmpty(_searchFilter);

      foreach (string ns in byNs.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
      {
        bool collapsed = !forceExpand && _collapsedNamespaces.Contains(ns);
        var entries = byNs[ns];

        // Namespace header
        var header = new VisualElement();
        header.AddToClassList("sl-namespace-header");
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.paddingTop = 6;
        header.style.paddingBottom = 4;
        header.style.paddingLeft = 8;
        header.style.paddingRight = 8;
        header.style.backgroundColor = NsHeaderBg;
        header.style.borderBottomWidth = 1;
        header.style.borderBottomColor = SeparatorColor;

        var arrow = new Label(collapsed ? "\u25b6" : "\u25bc");
        arrow.style.fontSize = 9;
        arrow.style.width = 14;
        arrow.style.color = MutedText;
        header.Add(arrow);

        var nsLabel = new Label(ns);
        nsLabel.style.fontSize = 10;
        nsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        nsLabel.style.flexGrow = 1;
        header.Add(nsLabel);

        var countBadge = MakeBadge(entries.Count.ToString(), KeyBadgeBg);
        countBadge.style.fontSize = 9;
        header.Add(countBadge);

        var rowsForNs = new List<VisualElement>();
        string capturedNs = ns;
        header.RegisterCallback<ClickEvent>(_ =>
        {
          bool nowCollapsed = _collapsedNamespaces.Contains(capturedNs);
          if (nowCollapsed)
            _collapsedNamespaces.Remove(capturedNs);
          else
            _collapsedNamespaces.Add(capturedNs);
          arrow.text = nowCollapsed ? "\u25bc" : "\u25b6";
          var display = nowCollapsed ? DisplayStyle.Flex : DisplayStyle.None;
          foreach (var r in rowsForNs)
            r.style.display = display;
        });

        _typeListScroll.Add(header);

        foreach (var entry in entries)
        {
          var row = MakeInstanceListRow(entry);
          if (collapsed) row.style.display = DisplayStyle.None;
          _typeListScroll.Add(row);
          _instanceListRows.Add(row);
          rowsForNs.Add(row);
        }
      }
    }

    private VisualElement MakeInstanceListRow(InstanceEntry entry)
    {
      var row = new VisualElement();
      row.AddToClassList("sl-type-row");
      row.userData = entry;
      row.style.flexDirection = FlexDirection.Row;
      row.style.alignItems = Align.Center;
      row.style.paddingTop = 5;
      row.style.paddingBottom = 5;
      row.style.paddingLeft = 10;
      row.style.paddingRight = 8;
      row.style.borderBottomWidth = 1;
      row.style.borderBottomColor = SeparatorColor;

      var dot = new VisualElement();
      dot.style.width = 8;
      dot.style.height = 8;
      SetBorderRadius(dot, 4);
      dot.style.marginRight = 6;
      dot.style.flexShrink = 0;
      dot.style.backgroundColor = GreenDot;
      row.Add(dot);

      var nameLabel = new Label(entry.DisplayName);
      nameLabel.style.flexGrow = 1;
      nameLabel.style.overflow = Overflow.Hidden;
      nameLabel.style.textOverflow = TextOverflow.Ellipsis;
      nameLabel.tooltip = ServiceLocatorDebugTypeDisplay.FormatWithNamespace(entry.ImplementationType)
                          + $"\n{entry.Registrations.Count} registration(s)";
      row.Add(nameLabel);

      // Show unique keys as badges
      var uniqueKeys = new HashSet<string>();
      foreach (var reg in entry.Registrations)
        if (!string.IsNullOrEmpty(reg.Key))
          uniqueKeys.Add(reg.Key);

      foreach (string k in uniqueKeys.OrderBy(x => x))
      {
        var keyBadge = MakeBadge(k, KeyBadgeBg);
        keyBadge.style.fontSize = 9;
        keyBadge.style.marginLeft = 2;
        keyBadge.style.flexShrink = 0;
        row.Add(keyBadge);
      }

      var badge = MakeBadge(entry.Registrations.Count.ToString(), BadgeBg);
      badge.style.minWidth = 22;
      badge.style.unityTextAlign = TextAnchor.MiddleCenter;
      badge.style.marginLeft = 4;
      row.Add(badge);

      row.RegisterCallback<MouseEnterEvent>(_ =>
      {
        if (_selectedInstance != entry.Implementation)
          row.style.backgroundColor = RowHoverBg;
      });
      row.RegisterCallback<MouseLeaveEvent>(_ =>
      {
        if (_selectedInstance != entry.Implementation)
          row.style.backgroundColor = StyleKeyword.Null;
      });
      row.RegisterCallback<ClickEvent>(_ => SelectInstance(entry));

      return row;
    }

    private void SelectInstance(InstanceEntry entry)
    {
      _selectedInstance = entry.Implementation;

      // Update left pane selection highlight
      foreach (var row in _instanceListRows)
      {
        bool sel = row.userData is InstanceEntry re && re.Implementation == entry.Implementation;
        if (sel)
          row.AddToClassList("sl-type-row--selected");
        else
        {
          row.RemoveFromClassList("sl-type-row--selected");
          row.style.backgroundColor = StyleKeyword.Null;
        }
      }

      ShowInstanceDetail(entry);
    }

    private void ShowInstanceDetail(InstanceEntry entry)
    {
      _middleScroll.Clear();
      _instanceRows.Clear();

      // Header
      var header = new VisualElement();
      header.AddToClassList("sl-detail-header");
      header.style.paddingTop = 10;
      header.style.paddingBottom = 10;
      header.style.paddingLeft = 14;
      header.style.paddingRight = 14;
      header.style.backgroundColor = DetailHeaderBg;
      header.style.borderBottomWidth = 1;
      header.style.borderBottomColor = SeparatorColor;

      var nameLabel = new Label(entry.DisplayName);
      nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
      nameLabel.style.fontSize = 14;
      header.Add(nameLabel);

      var typeLabel = new Label(ServiceLocatorDebugTypeDisplay.FormatWithNamespace(entry.ImplementationType));
      typeLabel.style.fontSize = 11;
      typeLabel.style.color = MutedText;
      typeLabel.style.marginTop = 2;
      header.Add(typeLabel);

      var statsRow = new VisualElement();
      statsRow.style.flexDirection = FlexDirection.Row;
      statsRow.style.marginTop = 6;
      statsRow.Add(MakeBadge($"{entry.Registrations.Count} registration{(entry.Registrations.Count != 1 ? "s" : "")}", BadgeBg));

      // Count unique keys
      var uniqueKeys = new HashSet<string>();
      foreach (var reg in entry.Registrations)
        if (!string.IsNullOrEmpty(reg.Key))
          uniqueKeys.Add(reg.Key);
      if (uniqueKeys.Count > 0)
      {
        var keyBadge = MakeBadge(
          uniqueKeys.Count == 1 ? $"key: {uniqueKeys.First()}" : $"{uniqueKeys.Count} keys",
          KeyBadgeBg);
        keyBadge.style.marginLeft = 4;
        statsRow.Add(keyBadge);
      }

      header.Add(statsRow);
      _middleScroll.Add(header);

      // Group registrations by key
      var byKey = new Dictionary<string, List<RegistrationInfo>>();
      foreach (var reg in entry.Registrations)
      {
        string k = reg.Key ?? "";
        if (!byKey.TryGetValue(k, out var list))
        {
          list = new List<RegistrationInfo>();
          byKey[k] = list;
        }
        list.Add(reg);
      }

      foreach (string key in byKey.Keys.OrderBy(k => k))
      {
        var regs = byKey[key];

        // Filter registrations by search
        if (!string.IsNullOrEmpty(_searchFilter))
        {
          bool keyMatches = !string.IsNullOrEmpty(key) && key.ToLowerInvariant().Contains(_searchFilter);
          if (!keyMatches)
          {
            regs = regs.FindAll(r =>
              ServiceLocatorDebugTypeDisplay.TypeMatchesSearch(r.ServiceType, _searchFilter));
            if (regs.Count == 0)
              continue;
          }
        }

        _middleScroll.Add(MakeRegistrationKeyFoldout(entry, key, regs));
      }

      // Set up inspector for this instance
      _selectedDescriptor = null;
      if (entry.Implementation is UnityEngine.Object)
      {
        // Find any descriptor for this instance to power the inspector
        var all = ServiceLocator.RegisteredServices;
        foreach (var kvp in all)
          foreach (var keyList in kvp.Value.Values)
            foreach (var desc in keyList)
              if (desc.Implementation == entry.Implementation)
              {
                _selectedDescriptor = desc;
                goto foundDesc;
              }
        foundDesc: ;
      }

      RebuildInspectorPanel();
    }

    private VisualElement MakeRegistrationKeyFoldout(InstanceEntry entry, string key, List<RegistrationInfo> registrations)
    {
      string keyDisplay = string.IsNullOrEmpty(key) ? "(default)" : key;
      string foldoutId = $"{entry.ImplementationType?.FullName}::{key}";
      bool startExpanded = _expandedKeys.Contains(foldoutId) || !string.IsNullOrEmpty(_searchFilter);

      var container = new VisualElement();
      container.style.marginTop = 2;

      var foldHeader = new VisualElement();
      foldHeader.AddToClassList("sl-foldout-header");
      foldHeader.style.flexDirection = FlexDirection.Row;
      foldHeader.style.alignItems = Align.Center;
      foldHeader.style.paddingTop = 5;
      foldHeader.style.paddingBottom = 5;
      foldHeader.style.paddingLeft = 10;
      foldHeader.style.paddingRight = 10;
      foldHeader.style.backgroundColor = FoldoutHeaderBg;

      var arrow = new Label(startExpanded ? "\u25bc" : "\u25b6");
      arrow.style.fontSize = 10;
      arrow.style.width = 14;
      arrow.style.color = MutedText;
      foldHeader.Add(arrow);

      var keyBadge = MakeBadge(keyDisplay, KeyBadgeBg);
      keyBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
      foldHeader.Add(keyBadge);

      var spacer = new VisualElement();
      spacer.style.flexGrow = 1;
      foldHeader.Add(spacer);

      var countBadge = MakeBadge(registrations.Count.ToString(), BadgeBg);
      countBadge.style.minWidth = 20;
      countBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
      foldHeader.Add(countBadge);

      container.Add(foldHeader);

      var body = new VisualElement();
      body.style.display = startExpanded ? DisplayStyle.Flex : DisplayStyle.None;
      body.style.paddingLeft = 6;

      for (int i = 0; i < registrations.Count; i++)
      {
        var reg = registrations[i];
        var regRow = new VisualElement();
        regRow.style.flexDirection = FlexDirection.Row;
        regRow.style.alignItems = Align.Center;
        regRow.style.paddingTop = 4;
        regRow.style.paddingBottom = 4;
        regRow.style.paddingLeft = 14;
        regRow.style.paddingRight = 14;
        if (i % 2 == 1)
          regRow.style.backgroundColor = RowBg;

        var dot = new VisualElement();
        dot.style.width = 6;
        dot.style.height = 6;
        SetBorderRadius(dot, 3);
        dot.style.marginRight = 8;
        dot.style.flexShrink = 0;
        dot.style.backgroundColor = BadgeBg;
        regRow.Add(dot);

        string typeName = ServiceLocatorDebugTypeDisplay.Format(reg.ServiceType);
        var typeLabel = new Label(typeName);
        typeLabel.style.fontSize = 12;
        typeLabel.style.flexGrow = 1;
        typeLabel.style.overflow = Overflow.Hidden;
        typeLabel.style.textOverflow = TextOverflow.Ellipsis;
        typeLabel.tooltip = ServiceLocatorDebugTypeDisplay.FormatWithNamespace(reg.ServiceType);
        regRow.Add(typeLabel);

        body.Add(regRow);
      }

      container.Add(body);

      foldHeader.RegisterCallback<MouseEnterEvent>(_ => foldHeader.style.backgroundColor = StyleKeyword.Null);
      foldHeader.RegisterCallback<MouseLeaveEvent>(_ => foldHeader.style.backgroundColor = StyleKeyword.Null);
      foldHeader.RegisterCallback<ClickEvent>(_ =>
      {
        bool isExpanded = body.style.display == DisplayStyle.Flex;
        body.style.display = isExpanded ? DisplayStyle.None : DisplayStyle.Flex;
        arrow.text = isExpanded ? "\u25b6" : "\u25bc";
        if (isExpanded)
          _expandedKeys.Remove(foldoutId);
        else
          _expandedKeys.Add(foldoutId);
      });

      return container;
    }

    private void UpdateInspectorColumnVisibility()
    {
      if (_inspectorColumn == null)
        return;
      _inspectorColumn.style.display = _selectedDescriptor != null ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void RebuildInspectorPanel()
    {
      _inspectorPanel.Clear();

      if (_selectedDescriptor == null)
      {
        UpdateInspectorColumnVisibility();
        return;
      }

      UpdateInspectorColumnVisibility();

      if (_selectedDescriptor.Implementation is UnityEngine.Object uo && uo != null)
      {
        try
        {
          var inspector = new InspectorElement(uo);
          inspector.style.flexGrow = 1;
          _inspectorPanel.Add(inspector);
        }
        catch
        {
          _inspectorPanel.Add(MakeMutedCentered("InspectorElement failed for this object. Use Open / Ping."));
        }

        return;
      }

      var card = new VisualElement();
      card.style.paddingLeft = 12;
      card.style.paddingRight = 12;
      card.style.paddingTop = 12;
      card.style.flexGrow = 1;

      void AddLine(string labelTitle, string value)
      {
        var line = new VisualElement();
        line.style.flexDirection = FlexDirection.Row;
        line.style.marginBottom = 4;
        var t = new Label(labelTitle);
        t.style.unityFontStyleAndWeight = FontStyle.Bold;
        t.style.minWidth = 140;
        var v = new Label(value ?? "");
        v.style.flexGrow = 1;
        v.style.whiteSpace = WhiteSpace.Normal;
        line.Add(t);
        line.Add(v);
        card.Add(line);
      }

      AddLine("Implementation type",
        _selectedDescriptor.Implementation != null
          ? ServiceLocatorDebugTypeDisplay.FormatWithNamespace(_selectedDescriptor.Implementation.GetType())
          : _selectedDescriptor.ImplementationType != null
            ? ServiceLocatorDebugTypeDisplay.FormatWithNamespace(_selectedDescriptor.ImplementationType)
            : "—");
      if (_selectedDescriptor.Implementation != null)
      {
        AddLine("Implementation",
          ServiceLocatorDebugTypeDisplay.FormatWithNamespace(_selectedDescriptor.Implementation.GetType()));
        AddLine("ToString", _selectedDescriptor.Implementation.ToString());
      }
      else if (_selectedDescriptor.ImplementationType != null)
      {
        AddLine("Implementation type",
          ServiceLocatorDebugTypeDisplay.FormatWithNamespace(_selectedDescriptor.ImplementationType));
        AddLine("State", "Lazy (not instantiated)");
      }
      else
        AddLine("State", "No implementation");

      _inspectorPanel.Add(card);
    }

    private static Label MakeMutedCentered(string msg)
    {
      var label = new Label(msg);
      label.style.color = MutedText;
      label.style.unityFontStyleAndWeight = FontStyle.Italic;
      label.style.paddingTop = 24;
      label.style.paddingLeft = 12;
      label.style.paddingRight = 12;
      label.style.whiteSpace = WhiteSpace.Normal;
      return label;
    }

    private void ClearMiddleAndInspector(string message)
    {
      _middleScroll.Clear();
      _instanceRows.Clear();
      _selectedDescriptor = null;
      _inspectorPanel.Clear();
      UpdateInspectorColumnVisibility();
      if (!string.IsNullOrEmpty(message))
        _middleScroll.Add(MakeMutedCentered(message));
    }

    private void ShowEmptyList(string message)
    {
      _typeListScroll.Clear();
      var label = new Label(message);
      label.style.color = MutedText;
      label.style.unityFontStyleAndWeight = FontStyle.Italic;
      label.style.paddingTop = 20;
      label.style.unityTextAlign = TextAnchor.MiddleCenter;
      _typeListScroll.Add(label);
    }

    private bool InstanceMatchesFilter(InstanceEntry entry)
    {
      if (string.IsNullOrEmpty(_searchFilter))
        return true;

      // Match instance display name
      if (entry.DisplayName != null && entry.DisplayName.ToLowerInvariant().Contains(_searchFilter))
        return true;

      // Match implementation type
      if (ServiceLocatorDebugTypeDisplay.TypeMatchesSearch(entry.ImplementationType, _searchFilter))
        return true;

      // Match Unity Object name
      if (entry.Implementation is UnityEngine.Object uo && uo != null &&
          uo.name.ToLowerInvariant().Contains(_searchFilter))
        return true;

      // Match any registered service type or key
      foreach (var reg in entry.Registrations)
      {
        if (ServiceLocatorDebugTypeDisplay.TypeMatchesSearch(reg.ServiceType, _searchFilter))
          return true;
        if (!string.IsNullOrEmpty(reg.Key) && reg.Key.ToLowerInvariant().Contains(_searchFilter))
          return true;
      }

      return false;
    }

    private static int CountInstances(Dictionary<string, List<ServiceDescriptor>> keyMap)
    {
      int count = 0;
      foreach (var list in keyMap.Values)
        count += list.Count;
      return count;
    }

    private static Label MakeBadge(string text, Color bg)
    {
      var badge = new Label(text);
      badge.style.backgroundColor = bg;
      SetBorderRadius(badge, 8);
      badge.style.paddingLeft = 6;
      badge.style.paddingRight = 6;
      badge.style.paddingTop = 1;
      badge.style.paddingBottom = 1;
      badge.style.fontSize = 10;
      return badge;
    }

    private static void SetBorderRadius(VisualElement el, float radius)
    {
      el.style.borderTopLeftRadius = radius;
      el.style.borderTopRightRadius = radius;
      el.style.borderBottomLeftRadius = radius;
      el.style.borderBottomRightRadius = radius;
    }

    private static MonoScript FindScriptForDescriptor(ServiceDescriptor desc)
    {
      Type type = desc?.Implementation?.GetType() ?? desc?.ImplementationType;
      return FindScriptForType(type);
    }

    private static MonoScript FindScriptForType(Type type)
    {
      if (type == null)
        return null;

      foreach (MonoScript script in MonoImporter.GetAllRuntimeMonoScripts())
      {
        if (script == null)
          continue;
        if (script.GetClass() == type)
          return script;
      }

      return null;
    }
  }
}
