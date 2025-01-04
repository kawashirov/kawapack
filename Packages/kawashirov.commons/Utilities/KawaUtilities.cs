using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

using Object = UnityEngine.Object;

namespace Kawashirov {
	public static class KawaUtilities {

		public static Vector2 XY(this Vector3 v) => new Vector2(v.x, v.y);
		public static Vector2 XZ(this Vector3 v) => new Vector2(v.x, v.z);
		public static Vector2 YZ(this Vector3 v) => new Vector2(v.y, v.z);

		public static Color Alpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);

		public class UnityEquality_ : IEqualityComparer<Object> {
			// Object.CompareBaseObjects(x, y)
			bool IEqualityComparer<Object>.Equals(Object x, Object y) => x == y;
			int IEqualityComparer<Object>.GetHashCode(Object obj) => obj.GetHashCode();
		}
		public static readonly UnityEquality_ UnityEquality = new UnityEquality_();

		public class EquatableComparer<T> : IEqualityComparer<T> where T : IEquatable<T> {
			// Object.CompareBaseObjects(x, y)
			bool IEqualityComparer<T>.Equals(T x, T y) => x.Equals(y);
			int IEqualityComparer<T>.GetHashCode(T obj) => obj.GetHashCode();
		}

		public static IEnumerable<T> UnityNotNull<T>(this IEnumerable<T> iter) where T : class
			=> iter.Where(obj => (obj as Object) != null);

		public static Type[] GetTypesSafe(this Assembly asm) {
			try {
				return asm.GetTypes();
			} catch (ReflectionTypeLoadException exc) {
				return exc.Types;
			}
		}

		public static T GetOrAddComponent<T>(this GameObject gobj) where T : Component {
			return gobj.TryGetComponent<T>(out var c) ? c : gobj.AddComponent<T>();
		}

		public static IEnumerable<GameObject> WithTag(this IEnumerable<GameObject> enumerable, string tag) => enumerable.Where(g => g.CompareTag(tag));
		public static IEnumerable<GameObject> WithoutTag(this IEnumerable<GameObject> enumerable, string tag) => enumerable.Where(g => !g.CompareTag(tag));

		private static bool IsRuntimeHideFlags(Object obj) => (obj.hideFlags & (HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild)) == HideFlags.None;

		public static bool IsRuntime(this Object obj) {
			// Имеет ли объект шанс попасть в рантайм после сборки? Если точно известно, что нет, то возвращается false
			if (obj == null)
				return false;
			if (!IsRuntimeHideFlags(obj))
				return false;
			var gameObject = obj as GameObject;
			if (!gameObject && obj is Component c) {
				gameObject = c.gameObject;
				// Если ранее мы проверили hideFlags компонента, то теперь нужно проверить hideFlags объекта.
				if (gameObject && !IsRuntimeHideFlags(gameObject))
					return false;
			}
			if (gameObject) {
				if (gameObject.CompareTag("EditorOnly"))
					return false;
				if (gameObject.TraverseParents().Any(IsEditorOnly))
					return false; // Имеет не-рантайм родителя 
			}
			return true;
		}

		public static bool IsEditorOnly(this Object obj) => obj != null && !IsRuntime(obj);

		public static IEnumerable<T> RuntimeOnly<T>(this IEnumerable<T> enumerable) where T : Object => enumerable.Where(IsRuntime);
		public static IEnumerable<T> EditorOnly<T>(this IEnumerable<T> enumerable) where T : Object => enumerable.Where(IsEditorOnly);

		public static HashSet<GameObject> FindWithTagInactive(IEnumerable<GameObject> where, string tag) {
			var queue = new Queue<GameObject>(where);
			var tagged = new HashSet<GameObject>();
			while (queue.Count > 0) {
				var current = queue.Dequeue();
				if (current.CompareTag(tag)) {
					tagged.Add(current);
				}
				if (!current.CompareTag("EditorOnly")) {
					var t = current.transform;
					for (var i = 0; i < t.childCount; ++i) {
						queue.Enqueue(t.GetChild(i).gameObject);
					}
				}
			}
			return tagged;
		}

		private static string KawaGetFullPath_Transform(Transform transform) {
			if (transform == null)
				return "<unknown transform>";
			var path = transform.name;
			while (transform.parent != null) {
				transform = transform.parent;
				path = $"{transform.name}/{path}";
			}
			return path;
		}

		private static string KawaGetFullPath_GameObject(GameObject gobj) {
			var transform_path = KawaGetFullPath_Transform(gobj.transform);

#if UNITY_EDITOR
			if (PrefabUtility.IsPartOfPrefabAsset(gobj)) {
				var asset_path = AssetDatabase.GetAssetPath(gobj);
				if (string.IsNullOrWhiteSpace(asset_path))
					asset_path = "<unknown prefab asset>";
				return $"{asset_path}/{transform_path}";
			}

			var prefab_stage = PrefabStageUtility.GetPrefabStage(gobj);
			if (prefab_stage != null) {
				var asset_path = prefab_stage.assetPath;
				if (string.IsNullOrWhiteSpace(asset_path))
					asset_path = "<unknown prefab asset stage>";
				return $"{asset_path}/{transform_path}";
			}

			if (EditorUtility.IsPersistent(gobj)) {
				return $"<unknown persistent>/{transform_path}";
			}
#endif // UNITY_EDITOR

			if (gobj.scene.IsValid()) {
				var scene_path = gobj.scene.path;
				if (string.IsNullOrWhiteSpace(scene_path)) {
					var scene_name = gobj.scene.name;
					scene_path = string.IsNullOrWhiteSpace(scene_name) ? "<unknown scene>" : $"scene:{scene_name}";
				}
				return $"{scene_path}/{transform_path}";
			}

			return $"<invalid scene>/{transform_path}";
		}

		private static string KawaGetFullPath_Component(Component component) {
			var path = KawaGetFullPath_GameObject(component.gameObject);
			var index = component.GetComponentIndex();
			return $"{path}[{index}]";
		}

		private static string KawaGetFullPath_WhateverAsset(Object obj) {
#if UNITY_EDITOR
			var asset_path = AssetDatabase.GetAssetPath(obj);
			if (!string.IsNullOrWhiteSpace(asset_path))
				return asset_path;
			else if (EditorUtility.IsPersistent(obj))
				return "<unknown persistent asset>";
#endif
			return "<in-memory asset>";
		}

		public static string KawaGetFullPath(this Object obj) {
			// В основном используется для логов, что бы точно знать где именно контекстный объект
			// Пытается найти максимально подробный путь к объектам.
			if (obj == null) {
				return obj is not null ? $"<object {obj.GetType().Name} destroyed>" : "<null>";
			} else if (obj is GameObject gobj) {
				return KawaGetFullPath_GameObject(gobj);
			} else if (obj is Component component) {
				return KawaGetFullPath_Component(component);
			} else {
				return KawaGetFullPath_WhateverAsset(obj);
			}
		}

		public static IEnumerable<Scene> IterScenes(bool onlyLoaded = true, bool onlyValid = true) {
			for (var i = 0; i < SceneManager.sceneCount; ++i) {
				var scene = SceneManager.GetSceneAt(i);
				if (onlyLoaded && !scene.isLoaded)
					continue;
				if (onlyValid && !scene.IsValid())
					continue;
				yield return scene;
			}
			yield break;
		}

		public static IEnumerable<GameObject> IterScenesRoots(bool onlyLoaded = true, bool onlyValid = true) {
			var less_allocs = new List<GameObject>();
			foreach (var scene in IterScenes(onlyLoaded, onlyValid)) {
				less_allocs.Clear();
				scene.GetRootGameObjects(less_allocs);
				foreach (var g in less_allocs)
					yield return g;
			}
		}

		public static IEnumerable<GameObject> TraverseParents(this GameObject root, bool excludeSelf = true)
			=> root.transform.TraverseParents(excludeSelf).Select(t => t.gameObject);

		public static IEnumerable<Transform> TraverseParents(this Transform transform, bool excludeSelf = true) {
			if (excludeSelf)
				transform = transform?.parent;
			while (transform) {
				yield return transform;
				transform = transform.parent;
			}
			yield break;
		}

		public static IEnumerable<GameObject> Traverse(this GameObject root) => root.transform.Traverse().Select(t => t.gameObject);

		public static IEnumerable<Transform> Traverse(this Transform root) {
			var queue = new Queue<Transform>();
			queue.Enqueue(root);
			while (queue.Count > 0) {
				var t = queue.Dequeue();
				yield return t;
				for (var i = 0; i < t.childCount; ++i)
					queue.Enqueue(t.GetChild(i));
			}
			yield break;
		}

		public static IEnumerable<T> ToEnumerable<T>(this T item) {
			yield return item;
		}

		public static bool AnyNotNull<T>(params T[] objs) {
			foreach (var obj in objs) {
				if (obj != null)
					return true;
			}
			return false;
		}


#if UNITY_EDITOR

#if KAWA_DEBUG
		[MenuItem("Kawashirov/Debug/Path info")]
		public static void ReportInfos() {
			var items = Selection.objects.OfType<GameObject>()
				.SelectMany(g => g.GetComponentsInChildren<Component>(true));
			foreach (var item in items) {
				Debug.LogFormat(
					item, "item={0}\nname={1}\ntransform={2}\nscene={3}\nIsPersistent={4}\nKawaGetFullPath={5}",
					item, item.name, item.KawaGetFullPath(), item.gameObject.scene.path,
					EditorUtility.IsPersistent(item), item.KawaGetFullPath()
				);
			}
		}
#endif

		public class ReadOnlyAttribute : PropertyAttribute { }

		[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
		public class ReadOnlyDrawer : PropertyDrawer {
			public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUI.GetPropertyHeight(property, label, true);

			public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
				using (new EditorGUI.DisabledScope(true)) {
					// TODO tests
					EditorGUI.PropertyField(position, property, label, true);
				}
			}
		}

#endif // UNITY_EDITOR
	}
}
