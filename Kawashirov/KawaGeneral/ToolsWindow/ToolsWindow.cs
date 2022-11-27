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
using System.Runtime.InteropServices.Expando;

namespace Kawashirov.ToolsGUI {
	public class ToolsWindow : EditorWindow, ISerializationCallbackReceiver {

		private readonly char[] pathSplitChars = new char[] { '/', '\\' };

		private static ToolsWindow window;

		private static readonly Lazy<Texture2D> kawaIcon = new Lazy<Texture2D>(GetKawaIcon);
		private static Texture2D GetKawaIcon() {
			var path = AssetDatabase.GUIDToAssetPath("302691306fd300648a26254d75364f60");
			return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
		}

		private static readonly Lazy<GUIContent> kawaMenu = new Lazy<GUIContent>(GetKawaMenu);
		private static GUIContent GetKawaMenu() {
			return new GUIContent("Toolbox\nMenu", kawaIcon.Value);
		}

		[MenuItem("Kawashirov/Toolbox Window", priority = -100)]
		[MenuItem("Window/Kawashirov's Toolbox Window")]
		static void ShowToolsWindow() {
			if (!window) {
				window = GetWindow<ToolsWindow>("Kawa's Toolbox", true);
			}
			window.Show();
			window.Focus();
		}

		private class PanelsHierarchy {
			public string name;
			public int currentPanel = 0;
			// Должен быть либо subHierarchy != null либо panel != null
			public List<PanelsHierarchy> subHierarchy;
			private GUIContent[] titles;
			private bool needTitlesUpdate;
			//
			public AbstractToolPanel panel;

			public PanelsHierarchy GetSubPanel(string name, bool create) {
				if (subHierarchy == null) {
					if (!create)
						return null;
					subHierarchy = new List<PanelsHierarchy>();
				}
				var panelH = subHierarchy.FirstOrDefault(h => string.Equals(h.name, name));
				if (panelH == null && create) {
					panelH = new PanelsHierarchy() { name = name };
					subHierarchy.Add(panelH);
					needTitlesUpdate = true;
				}
				return panelH;
			}

			public GUIContent[] GetTitles() {
				if (needTitlesUpdate || titles == null) {
					if (subHierarchy == null || subHierarchy.Count < 1) {
						titles = new GUIContent[0];
					} else {
						titles = subHierarchy.Select(ph => new GUIContent(ph.name)).ToArray();
					}
					needTitlesUpdate = false;
				}
				return titles;
			}

		}

		[ExecuteInEditMode]
		public class ProxyBehaviour : MonoBehaviour {
			public void OnDrawGizmos() {
				if (window)
					window.OnDrawGizmosProxy();
			}

			public void Focus(bool select = true) {
				EditorGUIUtility.PingObject(gameObject);
				EditorGUIUtility.PingObject(this);
				var bounds = new Bounds(transform.position, Vector3.one);
				foreach (var obj in SceneView.sceneViews) {
					if (obj is SceneView view) {
						view.Frame(bounds);
					}
				}
				if (select) {
					var objects = Selection.objects;
					ArrayUtility.AddRange(ref objects, new UnityEngine.Object[] { gameObject, transform, this });
					Selection.objects = objects;
					Selection.activeGameObject = gameObject;
					Selection.activeTransform = transform;
					Selection.activeObject = this;
				}
			}

		}

		[CustomEditor(typeof(ProxyBehaviour))]
		private class ProxyBehaviourEditor : Editor {
			public override void OnInspectorGUI() {
				EditorGUILayout.HelpBox("This is temporary GameObject, it will not be saved.", MessageType.Warning, true);
				var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 3);
				if (GUI.Button(rect, "Open Kawashirov's Toolbox Window")) {
					ShowToolsWindow();
				}
			}
		}

		private Dictionary<Type, AbstractToolPanel> panelInstances;
		// private PanelsHierarchy panelsHierarchy;

		public TreeViewState panelsTreeState;
		private PanelsTreeView panelsTree;
		private bool panelsLoaded = false;
		private AbstractToolPanel currentPanel;
		[SerializeField] private Vector2 scroll;
		private static ProxyBehaviour proxy;

		public static Type[] GetTypesSafe(Assembly asm) {
			try {
				return asm.GetTypes();
			} catch (ReflectionTypeLoadException exc) {
				return exc.Types;
			}
		}

		private static ProxyBehaviour TryFindProxy() {
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

		public void Awake() {
			// ValidateWindow();
		}

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
			panelsLoaded = true;

			var panelTypes = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(GetTypesSafe)
				.Where(t => t != null && t.IsSubclassOf(typeof(AbstractToolPanel)))
				.Select(t => (t, Attribute.GetCustomAttributes(t, typeof(ToolsWindowPanelAttribute))))
				.Where(x => x.Item2.Length == 1)
				.Select(x => (x.t, (ToolsWindowPanelAttribute)x.Item2[0]))
				.ToList();
			Debug.Log($"Found {panelTypes.Count} panel types...", this);

			if (panelInstances != null) {
				panelInstances.Clear();
			} else {
				panelInstances = new Dictionary<Type, AbstractToolPanel>(panelTypes.Count);
			}
			foreach ((var type, var attribute) in panelTypes) {
				panelInstances[type] = LoadOrCreatePanelInstance(type);
			}

			if (panelsTree == null) {
				panelsTreeState = new TreeViewState();
				panelsTree = new PanelsTreeView();
			}
			panelsTree.panelInstances = panelInstances.Values.ToList();
			panelsTree.Reload();

			Debug.Log($"Loaded {panelTypes.Count} panels.", this);
		}

		public void OnGUI() {
			GUILayoutUtility.GetRect(1, EditorGUIUtility.singleLineHeight * 0.5f);
			var headerH = EditorGUIUtility.singleLineHeight * 2;
			var header = GUILayoutUtility.GetRect(1, 2000, headerH, headerH, GUILayout.ExpandWidth(true));
			header.x += EditorGUIUtility.singleLineHeight * 0.5f;
			header.width -= EditorGUIUtility.singleLineHeight;
			var headerCells = header.RectSplitHorisontal(1, 3, 1).ToArray();


			if (GUI.Button(headerCells[0], kawaMenu.Value)) {
				currentPanel = null;
				if (!panelsLoaded) {
					ReloadPanels();
				}
			}

			if (currentPanel == null) {
				GUI.Label(headerCells[1], "Select Tool");
			} else {
				var headerContent = currentPanel.GetMenuHeaderContent();
				if (headerContent == null) {
					headerContent = new GUIContent("");
				}
				GUI.Label(headerCells[1], headerContent);
			}

			if (GUI.Button(headerCells[2], "Reload\nPanels")) {
				ReloadPanels();
			}

			EditorGUILayout.Space();

			if (currentPanel != null) {
				currentPanel.ToolsGUI();
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
