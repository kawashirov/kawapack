using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Kawashirov {
	public static class KawaUtilities {

		public static Vector2 XY(this Vector3 v) => new Vector2(v.x, v.y);
		public static Vector2 XZ(this Vector3 v) => new Vector2(v.x, v.z);
		public static Vector2 YZ(this Vector3 v) => new Vector2(v.y, v.z);

		public static Color Alpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);

		public static T GetOrAddComponent<T>(this GameObject gobj) where T : Component {
			var c = gobj.GetComponent<T>();
			if (c == null) {
				c = gobj.AddComponent<T>();
			}
			return c;
		}

		public static IEnumerable<GameObject> WithTag(this IEnumerable<GameObject> enumerable, string tag) => enumerable.Where(g => g.CompareTag(tag));
		public static IEnumerable<GameObject> WithoutTag(this IEnumerable<GameObject> enumerable, string tag) => enumerable.Where(g => !g.CompareTag(tag));

		private static bool IsRuntimeHideFlags(UnityEngine.Object obj) => (obj.hideFlags & (HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild)) == HideFlags.None;

		public static bool IsRuntime(this UnityEngine.Object obj) {
			// Имеет ли объект шанс попасть в рантайм после сборки? Если точно известно, что нет, то возвращается false
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

		public static bool IsEditorOnly(this UnityEngine.Object obj) => !IsRuntime(obj);

		public static IEnumerable<T> RuntimeOnly<T>(this IEnumerable<T> enumerable) where T : UnityEngine.Object => enumerable.Where(IsRuntime);
		public static IEnumerable<T> EditorOnly<T>(this IEnumerable<T> enumerable) where T : UnityEngine.Object => enumerable.Where(IsEditorOnly);

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

		public static string KawaGetHierarchyPath(this Transform transform) {
			var path = transform.name;
			while (transform.parent != null) {
				transform = transform.parent;
				path = transform.name + "/" + path;
			}
			return path;
		}

		public static string KawaGetFullPath(this GameObject gameObject) {
			var path = gameObject.transform.KawaGetHierarchyPath();

			var persistent = false;
#if UNITY_EDITOR
			persistent = EditorUtility.IsPersistent(gameObject);
#endif
			if (persistent) {
#if UNITY_EDITOR
				string asset_path = null;
				asset_path = AssetDatabase.GetAssetPath(gameObject);
				if (string.IsNullOrWhiteSpace(asset_path))
					asset_path = "<unknown persistent>";
				path = asset_path + "/" + path;
#endif
			} else if (gameObject.scene.IsValid()) {
				var scene_path = gameObject.scene.path;
				if (string.IsNullOrWhiteSpace(scene_path))
					scene_path = "<unknown scene>";
				path = scene_path + "/" + path;
			} else {
				path = "<unknown>/" + path;
			}
			return path;
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

		public static bool AnyEq(int value, params int[] values) {
			foreach (var v in values) {
				if (value == v)
					return true;
			}
			return false;
		}

#if UNITY_EDITOR

		[MenuItem("Kawashirov/Path info")]
		public static void ReportInfos() {
			var items = Selection.objects.OfType<GameObject>()
				.SelectMany(g => g.GetComponentsInChildren<Component>(true));
			foreach (var item in items) {
				Debug.LogFormat(
					item, "item={0}\nname={1}\ntransform={2}\nscene={3}\nIsPersistent={4}\nKawaGetFullPath={5}",
					item, item.name, item.transform.KawaGetHierarchyPath(), item.gameObject.scene.path,
					EditorUtility.IsPersistent(item), item.gameObject.KawaGetFullPath()
				);
			}
		}


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
