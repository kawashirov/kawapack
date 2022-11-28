#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using UnityEditor.IMGUI.Controls;
using static Kawashirov.KawaGUIUtility;

namespace Kawashirov.ToolsGUI {
	public class ToolsWindow : EditorWindow, ISerializationCallbackReceiver {

		internal static ToolsWindow window;
		internal static ToolsWindowHeaderGUI header;

		[MenuItem("Kawashirov/Toolbox Window", priority = -100)]
		[MenuItem("Window/Kawashirov's Toolbox Window")]
		internal static void ShowToolsWindow() {
			if (!window) {
				window = GetWindow<ToolsWindow>("Kawa's Toolbox", true);
			}
			window.Show();
			window.Focus();
		}

		internal Dictionary<Type, AbstractToolPanel> panelInstances = new Dictionary<Type, AbstractToolPanel>();

		internal TreeViewState panelsTreeState;
		internal PanelsTreeView panelsTree;
		internal bool panelsLoaded = false;
		internal AbstractToolPanel currentPanel;
		[SerializeField] private Vector2 scroll;
		internal static ProxyBehaviour proxy;

		public static Type[] GetTypesSafe(Assembly asm) {
			try {
				return asm.GetTypes();
			} catch (ReflectionTypeLoadException exc) {
				return exc.Types;
			}
		}

		internal static ProxyBehaviour TryFindProxy() {
			for (var i = 0; i < EditorSceneManager.sceneCount; ++i) {
				var scene = EditorSceneManager.GetSceneAt(i);
				if (!(scene.isLoaded && scene.IsValid()))
					continue;
				foreach (var rootgobj in scene.GetRootGameObjects()) {
					var p = rootgobj.GetComponentInChildren<ProxyBehaviour>();
					if (p)
						return p;
				}
			}
			var proxyGameObject = new GameObject("KawaToolsTemporaryProxy", typeof(ProxyBehaviour));
			return proxyGameObject.GetComponent<ProxyBehaviour>();
		}

		public static ProxyBehaviour ValidateProxy() {
			if (!proxy)
				proxy = TryFindProxy();
			proxy.hideFlags = HideFlags.DontSave;
			proxy.gameObject.hideFlags = HideFlags.DontSave;
			return proxy;
		}

		public static bool IsActive() {
			return window;
		}

		public void ValidateWindow() {
			if (window == this)
				return;

			if (window != null) {
				// Существует какое-то другое окно: уничтожаем его.
				window.Close();
				DestroyImmediate(window);
			}

			window = this;

			if (window.titleContent == null) {
				window.titleContent = new GUIContent("Kawa's Toolbox", kawaIcon.Value);
			} else if (window.titleContent.image == null) {
				window.titleContent.image = kawaIcon.Value;
			}
		}

		//public void Awake() {
		//	// ValidateWindow();
		//}

		public void OnEnable() {
			// ValidateWindow();
			// ReloadPanels();
			SceneView.duringSceneGui += OnSceneGUI;
		}

		public void OnDisable() {
			SceneView.duringSceneGui -= OnSceneGUI;
		}

		public void OnInspectorUpdate() {
			ValidateWindow();
			if (!panelsLoaded)
				ReloadPanels();
			if (currentPanel)
				currentPanel.Update();
		}

		private int OnSceneGUI_lastRenderedFrame = -1;
		public void OnSceneGUI(SceneView sceneView) {
			if (!currentPanel)
				return;

			currentPanel.OnSceneGUI(sceneView);

			if (Event.current.type == EventType.Layout) {
				// Workaraund: https://answers.unity.com/questions/594420/how-to-flush-mesh-batch-in-editor-or-how-to-draw-a.html
				var proxy = ValidateProxy();
				if (currentPanel.ShouldCallSceneGUIDrawMesh(sceneView)) {
					if (proxy)
						EditorUtility.SetDirty(proxy.gameObject);
					if (OnSceneGUI_lastRenderedFrame != Time.renderedFrameCount) {
						OnSceneGUI_lastRenderedFrame = Time.renderedFrameCount;
						// Debug.Log($"OnSceneGUIDrawMesh Event: {Event.current.type}", this);
						currentPanel.OnSceneGUIDrawMesh(sceneView);
					}
				} else {
					// Еще раз, что бы сменить кадр
					if (proxy && OnSceneGUI_lastRenderedFrame == Time.renderedFrameCount)
						EditorUtility.SetDirty(proxy.gameObject);
				}
			}
		}

		public static void ActivatePanel(Type type) {
			if (type == null) {
				window.currentPanel = null;
			} else if (window.panelInstances.TryGetValue(type, out var panel)) {
				window.currentPanel = panel;
				window.Focus();
			} else {
				Debug.LogWarning($"Panel of type {type.Name} is not loaded, can not show.");
			}
		}

		public static void ActivatePanel(AbstractToolPanel panel) {
			if (panel == null) {
				window.currentPanel = null;
			} else {
				var type = panel.GetType();
				if (window.panelInstances.TryGetValue(type, out var instancedPanel)) {
					if (instancedPanel != panel) {
						Debug.LogWarning($"Showind unregistred panel {type.Name}...");
					}
				}
				window.currentPanel = panel;
				window.Focus();
			}
		}

		private static string GetPanelFolder(AbstractToolPanel panel) {
			var monoScript = MonoScript.FromScriptableObject(panel);
			if (monoScript == null)
				return null;
			var path = AssetDatabase.GetAssetPath(monoScript);
			if (string.IsNullOrWhiteSpace(path))
				return null;
			path = Path.GetDirectoryName(path);
			return string.IsNullOrWhiteSpace(path) ? null : path;
		}

		private AbstractToolPanel LoadOrCreatePanelInstance(Type type) {
			var assets = AssetDatabase.FindAssets($"t:{type}")
					.Select(AssetDatabase.GUIDToAssetPath)
					.Select(p => AssetDatabase.LoadAssetAtPath(p, type))
					.OfType<AbstractToolPanel>().ToList();
			if (assets.Count == 1) {
				return assets[0];
			} else if (assets.Count > 1) {
				var paths = string.Join("\n", AssetDatabase.FindAssets($"t:{type}").Select(AssetDatabase.GUIDToAssetPath));
				Debug.LogWarning($"There is more than one asset for panel of type {type}. First one will be used. Please, resolve:\n{paths}", this);
				return assets[0];
			}
			Debug.LogWarning($"There is no asset for panel of type {type} found. Creating temporary asset...", this);
			var panel = (AbstractToolPanel)CreateInstance(type);
			var path = GetPanelFolder(panel) ?? "Assets";
			path = $"{path}/tmp_{type}_{panel.GetInstanceID()}.asset";
			AssetDatabase.CreateAsset(panel, path);
			panelInstances.Add(type, panel);
			Debug.Log($"Temporary asset for panel of type {type} created at: {path}", panel);
			return panel;
		}

		public void ReloadPanels() {
			Debug.Log("Loading panels...", this);

			var panelTypes = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(GetTypesSafe)
				.Where(t => t != null && t.IsSubclassOf(typeof(AbstractToolPanel)))
				.Select(t => (t, Attribute.GetCustomAttributes(t, typeof(ToolsWindowPanelAttribute))))
				.Where(x => x.Item2.Length == 1)
				.Select(x => (x.t, (ToolsWindowPanelAttribute)x.Item2[0]))
				.ToList();
			Debug.Log($"Found {panelTypes.Count} panel types...", this);

			panelInstances.Clear();
			foreach ((var type, var attribute) in panelTypes) {
				panelInstances[type] = LoadOrCreatePanelInstance(type);
			}

			if (panelsTree == null) {
				panelsTreeState = new TreeViewState();
				panelsTree = new PanelsTreeView();
			}
			panelsTree.panelInstances = panelInstances.Values.ToList();
			panelsTree.Reload();

			panelsLoaded = true;
			Debug.Log($"Loaded {panelTypes.Count} panels.", this);
		}

		public void OnGUI() {
			if (header == null) {
				header = new ToolsWindowHeaderGUI();
			}
			header.OnHeaderGUI(this);

			if (currentPanel != null) {
				var panelType = currentPanel.GetType();
				try {
					scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
					currentPanel.ToolsGUI();
				} catch (Exception exc) {
					var msg = $"Panel {panelType.Name} GUI failed: {exc.Message}";
					Debug.LogError(msg, currentPanel);
					Debug.LogException(exc, currentPanel);
					EditorGUILayout.HelpBox(msg, MessageType.Error);
				} finally {
					EditorGUILayout.EndScrollView();
				}
			} else if (panelsTree == null || !panelsLoaded) {
				EditorGUILayout.HelpBox("Panels not loaded.", MessageType.Info);
			} else {
				panelsTree.state.selectedIDs.Clear();
				var panelsTreeRect = GUILayoutUtility.GetRect(10, 2000, 10, 2000, GUILayout.ExpandHeight(true));
				panelsTreeRect.x += EditorGUIUtility.singleLineHeight * 0.5f;
				panelsTreeRect.y += EditorGUIUtility.singleLineHeight * 0.5f;
				panelsTreeRect.width -= EditorGUIUtility.singleLineHeight;
				panelsTreeRect.height -= EditorGUIUtility.singleLineHeight;
				panelsTree.OnGUI(panelsTreeRect);
			}
		}

		public void OnDrawGizmosProxy() {
			if (currentPanel)
				currentPanel.DrawGizmos();
		}

		public void OnBeforeSerialize() {
			foreach (var panel in panelInstances.Values)
				if (panel != null)
					EditorUtility.SetDirty(panel);
		}

		public void OnAfterDeserialize() { } // => ReloadPanels();
	}
}
#endif // UNITY_EDITOR
