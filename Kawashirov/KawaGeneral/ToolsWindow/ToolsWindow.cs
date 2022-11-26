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

namespace Kawashirov.ToolsGUI {
	public class ToolsWindow : EditorWindow, ISerializationCallbackReceiver {

		private readonly char[] pathSplitChars = new char[] { '/', '\\' };

		private static ToolsWindow window;

		private static readonly Lazy<Texture2D> kawaIcon = new Lazy<Texture2D>(GetKawaIcon);

		private static Texture2D GetKawaIcon() {
			var path = AssetDatabase.GUIDToAssetPath("302691306fd300648a26254d75364f60");
			return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
		private PanelsHierarchy panelsHierarchy;
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

		private AbstractToolPanel GetPanelInstance(Type type) {
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
			Debug.Log($"Temporary asset for panel of type {type} created at: {path}", panel);
			return panel;
		}

		public void ReloadPanels() {
			Debug.Log("Loading panels...", this);
			panelsLoaded = true;
			panelsHierarchy = new PanelsHierarchy();

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
				panelInstances[type] = GetPanelInstance(type);
			}

			foreach ((var type, var attribute) in panelTypes) {
				// Debug.Log($"Creating panel for {type} in \"{attribute.path}\"...", this);
				var path = attribute.path.Split(pathSplitChars, StringSplitOptions.RemoveEmptyEntries);
				var hierarchy = panelsHierarchy;
				foreach (var folder in path) {
					hierarchy = hierarchy.GetSubPanel(folder, true);
				}
				hierarchy.panel = panelInstances[type];
			}
			Debug.Log($"Loaded {panelTypes.Count} panels.", this);
		}

		public void OnGUI() {
			if (GUILayout.Button("Reload Panels"))
				ReloadPanels();

			EditorGUILayout.Space();

			var hierarchy = panelsHierarchy;
			while (hierarchy != null && hierarchy.subHierarchy != null && hierarchy.subHierarchy.Count > 0) {
				hierarchy.currentPanel = GUILayout.Toolbar(hierarchy.currentPanel % hierarchy.subHierarchy.Count, hierarchy.GetTitles());
				hierarchy = hierarchy.subHierarchy[hierarchy.currentPanel];
			}


			if (hierarchy != null && hierarchy.panel) {
				if (currentPanel)
					currentPanel.active = false;
				currentPanel = hierarchy.panel;
				currentPanel.active = true;
				EditorGUILayout.Space();
				using (var scrollScope = new EditorGUILayout.ScrollViewScope(scroll, false, false)) {
					currentPanel.ToolsGUI();
					scroll = scrollScope.scrollPosition;
				}
			} else {
				currentPanel = null;
				EditorGUILayout.HelpBox("Error: GUI Panel Destroyed.", MessageType.Error);
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
