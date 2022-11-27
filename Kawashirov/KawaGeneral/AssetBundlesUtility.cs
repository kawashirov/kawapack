#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Unity.EditorCoroutines.Editor;
using Kawashirov.ToolsGUI;

using Object = UnityEngine.Object;
using UnityEditor.Animations;

namespace Kawashirov {

	[ToolsWindowPanel("Asset Bundles")]
	public class AssetBundlesUtility : AbstractToolPanel {
		private static HashSet<Type> deadEndTypes = new HashSet<Type>(new Type[] {
			typeof(Mesh), typeof(AnimationClip), typeof(AudioClip), typeof(Shader), //typeof(Texture2D),
		});

		[SerializeField] private List<AssetBundleLoad> assetBundles = new List<AssetBundleLoad>();

		private class AssetBundleLoad {
			public AssetBundle bundle;
			public string path;
			public bool keep;
		}

		public override GUIContent GetMenuButtonContent() {
			var image = EditorGUIUtility.IconContent("DefaultAsset Icon").image;
			var guiContent = new GUIContent("Asset Bundles", image);
			return guiContent;
		}

		public override void ToolsGUI() {

			for (var i = 0; i < assetBundles.Count; ++i) {
				var load = assetBundles[i];
				var rects = EditorGUILayout.GetControlRect().RectSplitHorisontal(1, 3, 1).ToArray();
				EditorGUI.LabelField(rects[0], $"{i}");
				if (load.bundle) {
					EditorGUI.ObjectField(rects[1], load.bundle, load.bundle.GetType(), true);
				} else {
					EditorGUI.LabelField(rects[1], $"Not Loaded");
				}
			}

			if (GUILayout.Button("Unload all Asset Bundles")) {
				AssetBundle.UnloadAllAssetBundles(true);
			}

			if (GUILayout.Button("Play Streamed Scene Asset Bundle")) {
				PlayStreamedSceneAssetBundle();
			}

			if (GUILayout.Button("Unpack Asset Bundle")) {
				UnpackAssetBundle();
			}

			if (GUILayout.Button("Instantiate Prefabs from Asset Bundle")) {
				InstantiateAssetBundle();
			}

			if (GUILayout.Button("Load Asset Bundle and keep")) {
				LoadAssetBundleFromFile();
			}
		}

		public override void Update() {
			foreach (var load in assetBundles) {
				if (load.keep && !load.bundle) {
					load.bundle = AssetBundle.LoadFromFile(load.path);
				}
			}
			// Если бандл не прогружается, то нахуй его.
			assetBundles.RemoveAll(load => !load.bundle);
		}

		private AssetBundle LoadAssetBundleUI(string path) {
			try {
				Debug.Log($"Loading AssetBundle from {path}...", this);
				EditorUtility.DisplayProgressBar("Loading AssetBundle...", "Loading AssetBundle...", 0f);
				return AssetBundle.LoadFromFile(path);
			} finally {
				EditorUtility.ClearProgressBar();
			}
		}
		private AssetBundleLoad LoadAssetBundleCached(string path) {
			foreach (var load in assetBundles) {
				if (!string.Equals(load.path, path, System.StringComparison.InvariantCultureIgnoreCase))
					continue;
				// Уже загружали
				if (!load.bundle)
					load.bundle = LoadAssetBundleUI(load.path);
				if (load.bundle)
					return load;
			}

			var bundle = LoadAssetBundleUI(path);
			if (!bundle)
				return null;

			var loadNew = new AssetBundleLoad() { bundle = bundle, path = path, keep = true, };
			assetBundles.Add(loadNew);
			return loadNew;
		}

		private AssetBundleLoad LoadAssetBundleFromFile() {
			var assetBundlePath = EditorUtility.OpenFilePanel("Select AssetBundle", Application.dataPath, "");
			if (string.IsNullOrWhiteSpace(assetBundlePath)) {
				Debug.LogWarning("Loading cancelled: AssetBundle is not selected", this);
				return null;
			}
			return LoadAssetBundleCached(assetBundlePath);
		}

		public void PlayStreamedSceneAssetBundle() {
			try {
				var load = LoadAssetBundleFromFile();

				if (!load.bundle.isStreamedSceneAssetBundle) {
					Debug.LogWarning($"AssetBundle from {load.path} is not Streamed Scene: Can not play it!", this);
					return;
				}

				EditorUtility.DisplayProgressBar("Loading AssetBundle...", "Loading Scenes...", 0f);
				Debug.Log($"Loading Scenes from AssetBundle from {load.path}...", this);
				var scenePaths = load.bundle.GetAllScenePaths();
				if (scenePaths == null || scenePaths.Length < 1) {
					Debug.LogWarning($"AssetBundle from {load.path} does not contains any scenes!", this);
					return;
				}
				if (scenePaths.Length > 1) {
					var scenes = string.Join("\n", scenePaths);
					Debug.LogWarning($"AssetBundle from {load.path} contains multiple scenes:\n{scenes}", this);
				}
				for (var i = 0; i < scenePaths.Length; ++i) {
					Debug.Log($"Loading Scene #{i} {scenePaths[i]} from AssetBundle from {load.path}...", this);
					// EditorSceneManager.LoadSceneAsyncInPlayMode(scenePaths[i], new LoadSceneParameters(i == 0 ? LoadSceneMode.Single : LoadSceneMode.Additive));
					SceneManager.LoadScene(Path.GetFileNameWithoutExtension(scenePaths[i]), i == 0 ? LoadSceneMode.Single : LoadSceneMode.Additive);
				}

			} finally {
				EditorUtility.ClearProgressBar();
			}

		}

		private bool IsDeadEnd(Object obj) {
			return deadEndTypes.Any(t => t.IsAssignableFrom(obj.GetType()));
		}

		private void SerializedTransfer(Object from, Object to) {
			if (typeof(Mesh).IsAssignableFrom(to?.GetType()))
				return; // TODO
			if (typeof(AnimationClip).IsAssignableFrom(to?.GetType()))
				return; // TODO
			if (typeof(AudioClip).IsAssignableFrom(to?.GetType()))
				return; // TODO
			if (typeof(Shader).IsAssignableFrom(to?.GetType()))
				return; // TODO
			using (var to_s = new SerializedObject(to)) {
				// Не всегда все копируется.
				// Перебрасываем данные полностью.
				using (var from_s = new SerializedObject(from))
				using (var iterator = from_s.GetIterator()) {
					do {
						to_s.CopyFromSerializedPropertyIfDifferent(iterator);
					} while (iterator.Next(true));
				}
				to_s.ApplyModifiedPropertiesWithoutUndo();
			}
			//if (from is AnimatorController from_c && to is AnimatorController to_c) {
			//}
		}

		private int ReplaceReferences(Object obj, IDictionary<Object, Object> mapping) {
			if (obj == null)
				throw new ArgumentNullException(nameof(obj));
			if (mapping == null)
				throw new ArgumentNullException(nameof(mapping));
			if (IsDeadEnd(obj))
				return 0; // В этом типе замена не производится

			var replaces = 0;
			using (var serialized = new SerializedObject(obj, this))
			using (var iterator = serialized.GetIterator()) {
				do {
					// Debug.Log($"Iterating: {copy.name}: Property {iterator.propertyType} {iterator.propertyPath}", this);
					if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue != null) {
						var original = iterator.objectReferenceValue;
						if (mapping.TryGetValue(original, out var replace) && replace != null) {
							Debug.Log($"{obj.name}: Replacing {original.GetType()} {original} -> {replace.GetInstanceID()} {replace}!", this);
							iterator.objectReferenceValue = replace;
							++replaces;
						}
					} else if (iterator.propertyType == SerializedPropertyType.ExposedReference && iterator.exposedReferenceValue != null) {
						var original = iterator.exposedReferenceValue;
						if (mapping.TryGetValue(original, out var replace) && replace != null) {
							Debug.Log($"{obj.name}: Replacing {original.GetType()} {original} -> {replace.GetInstanceID()} {replace}!", this);
							iterator.exposedReferenceValue = replace;
							++replaces;
						}
					}
				} while (iterator.Next(true));

				if (serialized.hasModifiedProperties) {
					serialized.ApplyModifiedPropertiesWithoutUndo();
					if (obj)
						EditorUtility.SetDirty(obj);
				}
			}
			return replaces;
		}

		public void UnpackAssetBundle() {
			try {
				var load = LoadAssetBundleFromFile();

				if (load.bundle.isStreamedSceneAssetBundle) {
					Debug.LogWarning($"AssetBundle from {load.path} is Streamed Scene: Can not unpack it!", this);
					return;
				}

				EditorUtility.DisplayProgressBar("Loading AssetBundle...", "Loading Assets...", 0f);
				Debug.Log($"Loading Asses from AssetBundle from {load.path}...", this);
				var allBundled = load.bundle.LoadAllAssets();
				if (allBundled == null || allBundled.Length < 1) {
					Debug.LogWarning($"AssetBundle from {load.path} does not contains any assets!", this);
					return;
				}

				var serachSet = new HashSet<Object>();
				var searchQueue = new Queue<Object>(allBundled);

				while (searchQueue.Count > 0) {
					var original = searchQueue.Dequeue();
					//if (serachSet.Contains(original))
					//	continue;
					serachSet.Add(original);
					var progress_max = Mathf.Max(searchQueue.Count, serachSet.Count);
					var progress_min = Mathf.Min(searchQueue.Count, serachSet.Count);
					var progress = (progress_max - progress_min + 0.5f) / (progress_max + 1f);
					var info = $"Inspecting {searchQueue.Count}/{serachSet.Count} {original.GetType().Name} {original.name}...";
					EditorUtility.DisplayProgressBar("Inspecting AssetBundle...", info, progress);
					Debug.Log($"Search: Iterating: {original.name}: Path: {AssetDatabase.GetAssetPath(original)} ", this);
					// Этот объект еще не был найден, инспектируем.
					if (IsDeadEnd(original))
						continue;
					var isDebug = false; // original is Texture;
					using (var serialized = new SerializedObject(original, this)) {
						using (var iterator = serialized.GetIterator()) {
							do {
								if (isDebug) {
									Debug.Log($"{original.GetType().Name} {original.name}:  {iterator.propertyType} {iterator.propertyPath}", this);
								}
								if (iterator.propertyType == SerializedPropertyType.Boolean && iterator.name == "m_IsReadable") {
									// Unlock some locked read-only assets
									iterator.boolValue = true;
								}
								Object value = null;
								if (iterator.propertyType == SerializedPropertyType.ObjectReference) {
									value = iterator.objectReferenceValue;
								} else if (iterator.propertyType == SerializedPropertyType.ExposedReference) {
									value = iterator.exposedReferenceValue;
								}
								if (!value || serachSet.Contains(value))
									continue;
								// Ссылки очень часто повторяются, по этому что бы не дрочить очередь делаем проверку сразу перед вставкой.
								if (isDebug) {
									Debug.Log($"Enqueue: {iterator.objectReferenceValue} ({searchQueue.Count}/{serachSet.Count})", this);
								}
								searchQueue.Enqueue(value);

							} while (iterator.Next(true));
						}
						if (serialized.hasModifiedProperties) {
							serialized.ApplyModifiedPropertiesWithoutUndo();
							EditorUtility.SetDirty(original);
						}
					}
					if (original is Material material) {

					}
				}
				Debug.Log($"Found {serachSet.Count} objects.", this);

				var allNonSceneCopies = new Dictionary<Object, Object>(serachSet.Count);
				var allSceneCopies = new Dictionary<GameObject, GameObject>(serachSet.Count / 10);

				{
					var i = 0;
					foreach (var original in serachSet) {
						var info = $"Instantiating {i + 1}/{serachSet.Count} {original.GetType().Name} {original.name}...";
						EditorUtility.DisplayProgressBar("Instantiating assets...", info, (i + 1f) / (serachSet.Count + 1f));
						if (original is GameObject gobj) {
							if (gobj.transform.parent == null) {
								Debug.Log($"Instantiating root GameObject {original.name}...", this);
								var copy = Instantiate(gobj);
								allSceneCopies[gobj] = copy;
								copy.name = original.name;
							} else {
								// Ignore non-root game objects
							}
						} else if (original is Component) {
							// skip 
						} else {
							Debug.Log($"Instantiating and saving {original.GetType()} {original.name}...", this);
							if (original is Texture2D tex) {

							}
							var copy = Instantiate(original);

							if (copy) {
								allNonSceneCopies[original] = copy;
								copy.name = original.name;

								SerializedTransfer(original, copy);

								var ext = "asset";
								if (original is Shader) {
									copy.name = copy.name.Replace("Hidden/", "");
								} else if (original is Material material) {
									ext = "mat";
								} else if (original is AnimationClip clip) {
									ext = "anim";
								} else if (original is RuntimeAnimatorController controller) {
									ext = "controller";
								} else if (original is AnimatorOverrideController overrideController) {
									ext = "overrideController";
								}

								// Сохраняем сразу, т.к. GC почему-то убивает их в процессе.
								var filename = $"{copy.name}_{copy.GetInstanceID()}_{copy.GetType().Name}";
								foreach (var forbidden in Path.GetInvalidFileNameChars())
									filename = filename.Replace(forbidden, '_');
								AssetDatabase.CreateAsset(copy, $"Assets/Extracted/{filename}.{ext}");
							} else {
								Debug.LogError($"Instantiatin failed: {original.GetType()} {original.name}...", this);
							}
						}
						++i;
					}
					Debug.Log($"Instantiated {allNonSceneCopies.Count} non-scene and {allSceneCopies.Count} on-scene Objects.", this);
				}

				{
					var i = 0;
					var replaces = 0;
					foreach (var copy in allNonSceneCopies.Values) {
						var info = $"Replacing {replaces}/{i}/{allNonSceneCopies.Count} {copy.GetType().Name} {copy.name}...";
						EditorUtility.DisplayProgressBar("Replacing non-scene references...", info, (i + 1f) / (allNonSceneCopies.Count + 1f));
						replaces += ReplaceReferences(copy, allNonSceneCopies);
						++i;
					}
					Debug.Log($"Replaced {replaces} non-scene references!", this);
					AssetDatabase.SaveAssets();
				}

				{
					var i = 0;
					var replaces = 0;
					foreach (var copy in allSceneCopies.Values) {
						var info = $"Replacing {replaces}/{i}/{allSceneCopies.Count} {copy.GetType().Name} {copy.name}...";
						EditorUtility.DisplayProgressBar("Replacing scene references...", info, (i + 1f) / (allSceneCopies.Count + 1f));
						foreach (var component in copy.GetComponentsInChildren<Component>(true)) {
							replaces += ReplaceReferences(component, allNonSceneCopies);
							++i;
						}
					}
					Debug.Log($"Replaced {replaces} scene references!", this);
				}

				{
					var i = 0;
					foreach (var gobj in allSceneCopies.Values) {
						var info = $"Saving prefabs {i}/{allSceneCopies.Count} {gobj.name}...";
						EditorUtility.DisplayProgressBar("Saving prefabs...", info, (i + 1f) / (allSceneCopies.Count + 1f));

						var name = gobj.name;
						foreach (var forbidden in Path.GetInvalidFileNameChars())
							name = name.Replace(forbidden, '_');
						PrefabUtility.SaveAsPrefabAssetAndConnect(gobj, $"Assets/Extracted/{name}_{gobj.GetInstanceID()}.prefab", InteractionMode.AutomatedAction);
						++i;
					}
					Debug.Log($"Saved {allSceneCopies.Count} prefabs!", this);
				}
			} finally {
				EditorUtility.ClearProgressBar();
			}
		}

		public void InstantiateAssetBundle() {
			try {
				var load = LoadAssetBundleFromFile();
				if (load == null) {
					Debug.LogWarning($"Failed to load AssetBundle from {load.path}!", this);
					return;
				}

				if (load.bundle.isStreamedSceneAssetBundle) {
					Debug.LogWarning($"AssetBundle from {load.path} is Streamed Scene: Can not unpack it!", this);
					return;
				}

				EditorUtility.DisplayProgressBar("Loading AssetBundle...", "Loading Assets...", 0f);
				Debug.Log($"Loading Asses from AssetBundle from {load.path}...", this);
				var allGameObjects = load.bundle.LoadAllAssets<GameObject>();
				if (allGameObjects == null || allGameObjects.Length < 1) {
					Debug.LogWarning($"AssetBundle from {load.path} does not contains any GameObjects!", this);
					return;
				}

				var assesList = string.Join("\n", allGameObjects.Select(obj => $"- {obj.KawaGetFullPath()} ({obj.GetInstanceID()})"));
				Debug.LogWarning($"AssetBundle from {load.path} contains {allGameObjects.Length} Prefabs:\n{assesList}", this);

				for (var i = 0; i < allGameObjects.Length; ++i) {
					var prefab = allGameObjects[i];
					// PrefabUtility.InstantiatePrefab(prefab);
					GameObject.Instantiate(prefab);
				}

			} finally {
				EditorUtility.ClearProgressBar();
				//if (assetBundle)
				//	assetBundle.Unload(true);
			}

		}

	}

}
#endif
