using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Kawashirov {
	public static class StaticCommons {

		public static Vector2 XY(this Vector3 v) => new Vector2(v.x, v.y);
		public static Vector2 XZ(this Vector3 v) => new Vector2(v.x, v.z);
		public static Vector2 YZ(this Vector3 v) => new Vector2(v.y, v.z);

		public static Color Alpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);

		public static IEnumerable<T> UnityNotNull<T>(this IEnumerable<T> iter) where T : class 
			=> iter.Where(obj => (obj as UnityEngine.Object) != null);

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
			if (EditorUtility.IsPersistent(gameObject)) {
				string asset_path = null;
#if UNITY_EDITOR
				asset_path = AssetDatabase.GetAssetPath(gameObject);
#endif
				if (string.IsNullOrWhiteSpace(asset_path))
					asset_path = "<unknown persistent>";
				path = asset_path + "/" + path;
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

		public static List<GameObject> GetScenesRoots(List<GameObject> collection = null, bool onlyLoaded = true, bool onlyValid = true) {
			if (collection == null)
				collection = new List<GameObject>();
			var less_allocs = new List<GameObject>();
			foreach (var scene in IterScenes(onlyLoaded, onlyValid)) {
				less_allocs.Clear();
				scene.GetRootGameObjects(less_allocs);
				collection.AddRange(less_allocs);
			}
			return collection;
		}

		public static bool IsEditorOnly(this Transform transform) {
			// Tags only exist in editor?
			// Any of parents have "EditorOnly" tag.
			while (transform != null) {
				if (transform.CompareTag("EditorOnly"))
					return true;
				transform = transform.parent;
			}
			return false;
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
			foreach(var item in items) {
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

		public static void ShaderEditorFooter() {
			var style = new GUIStyle { richText = true };

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("This thing made by <b>kawashirov</b>; My Contacts:", style);
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Discord server:");
			if (GUILayout.Button("pEugvST")) {
				Application.OpenURL("https://discord.gg/pEugvST");
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.LabelField("Discord tag: kawashirov#8363");
		}

#endif // UNITY_EDITOR
	}
}
