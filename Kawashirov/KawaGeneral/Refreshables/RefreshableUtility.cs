#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Kawashirov.Refreshables {
	public static class RefreshableUtility {

		[MenuItem("Kawashirov/Refreshables/Refresh every IRefreshable in loaded scenes")]
		public static void RefreshEverytingInLoadedScenes() {
			KawaUtilities.IterScenesRoots()
				.SelectMany(g => g.GetComponentsInChildren<IRefreshable>(true))
				.ToList().RefreshMultiple();
		}

		[MenuItem("Kawashirov/Refreshables/Refresh every IRefreshable asset in project")]
		public static void RefreshEveryIRefreshableInProject() {
			RefreshEverytingInProject<IRefreshable>(true);
		}

		public static List<T> FindAllRefreshablesInProject<T>(bool ui = false) where T : class, IRefreshable {
			Debug.LogFormat("[KawaEditor] Searching <b>{0}</b> assets...", typeof(T));
			var list = new List<T>();
			try {
				if (ui)
					EditorUtility.DisplayProgressBar("Searching assets...", "Searching assets...", 0.5f);
				var guids = AssetDatabase.FindAssets("t:" + typeof(ScriptableObject).Name);
				Debug.LogFormat("[KawaEditor] Loading <b>{0}</b> scriptable object assets...", guids.Length);
				for (var i = 0; i < guids.Length; ++i) {
					var s = string.Format("{0}/{1}", i + 1, guids.Length);
					var p = 1.0f * (i + 1) / (guids.Length + 1);
					if (ui && EditorUtility.DisplayCancelableProgressBar("Searching assets...", s, p))
						break;
					var path = AssetDatabase.GUIDToAssetPath(guids[i]);
					var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
					if (obj is T refreshable)
						list.Add(refreshable);
				}
			} finally {
				if (ui)
					EditorUtility.ClearProgressBar();
			}
			Debug.LogFormat("[KawaEditor] Found <b>{0}</b> <b>{1}</b> assets.", list.Count, typeof(T));
			return list;
		}

		public static void RefreshEverytingInProject<T>(bool ui = false) where T : class, IRefreshable {
			var list = FindAllRefreshablesInProject<T>(ui);

			list.RefreshMultiple();

			try {
				Debug.Log("[KawaEditor] Unloading unused assets...");
				EditorUtility.DisplayProgressBar("Unloading unused assets...", "Unloading unused assets..", 0.5f);
				EditorUtility.UnloadUnusedAssetsImmediate();
			} finally {
				EditorUtility.ClearProgressBar();
			}
			Debug.LogFormat("[KawaEditor] Done processing <b>{0}</b> <b>{1}</b> assets.", list.Count, typeof(T));
		}

		public static bool RefreshSafe(this IRefreshable refreshable) {
			UnityEngine.Object context = null;
			var state = false;
			try {
				context = refreshable.AsUnityObject();
				refreshable.Refresh();
				state = true;
			} catch (Exception exc) {
				Debug.LogErrorFormat(
					context,
					"[KawaEditor] Failed to Refresh: \"<b>{1}</b>\"\n@ <i>{0}</i>\n{2}",
					refreshable, exc.Message, exc.StackTrace
				);
				Debug.LogException(exc, context);
			}
			return state;
		}

		public static void RefreshMultiple<T>(this IEnumerable<T> refreshables) where T : class, IRefreshable {
			var array = refreshables.ToList();
			var errors = 0;
			try {
				Debug.LogFormat("[KawaEditor] Refreshing <b>{0}</b> objects...", array.Count);
				for (var i = 0; i < array.Count; ++i) {
					var r = array[i];
					var ctx = r.AsUnityObject();
					var path = r.RefreshablePath();
					var info = string.Format("Refreshing {0}/{1}: {2}", i + 1, array.Count, ctx);
					var progress = 1.0f * i / array.Count;
					if (EditorUtility.DisplayCancelableProgressBar(info, info, progress))
						break;
					Debug.LogFormat(ctx, "[KawaEditor] Refreshing <b>{1}</b>...\n@ <i>{0}</i>", path, r);
					r.RefreshSafe();
				}
			} finally {
				EditorUtility.ClearProgressBar();
			}
			if (errors < 1) {
				Debug.LogFormat("[KawaEditor] Refreshed <b>{0}</b> objects. No errors.", array.Count);
			} else {
				Debug.LogWarningFormat("[KawaEditor] Refreshed <b>{0}</b> objects: <b>{1}</b> errors!", array.Count, errors);
			}
		}

		public static void BehaviourRefreshGUI(this Editor editor) {
			var refreshables = editor.targets.OfType<IRefreshable>().ToList();

			if (refreshables.Count < 1)
				return;

			if (GUILayout.Button("Only refresh this")) {
				refreshables.RefreshMultiple();
			}

			var scenes = editor.targets.OfType<Component>().Select(r => r.gameObject.scene).Distinct().ToList();
			var scene_str = string.Join(", ", scenes.Select(s => s.name));

			var types = editor.targets.Select(t => t.GetType()).Distinct().ToList();
			var types_str = string.Join(", ", types.Select(t => t.Name));

			var types_btn = string.Format("Refresh every {0} on scene: {1}", types_str, scene_str);
			if (GUILayout.Button(types_btn)) {
				var all_targets = scenes.SelectMany(s => s.GetRootGameObjects())
						.SelectMany(g => types.SelectMany(t => g.GetComponentsInChildren(t, true)))
						.Distinct().OfType<IRefreshable>().ToList();
				all_targets.RefreshMultiple();
			}

			var scene_btn = string.Format("Refresh every Behaviour on scene: {0}", scene_str);
			if (GUILayout.Button(scene_btn)) {
				var all_targets = scenes.SelectMany(s => s.GetRootGameObjects())
						.SelectMany(g => g.GetComponentsInChildren<IRefreshable>(true)).ToList();
				all_targets.RefreshMultiple();
			}
		}
	}
}

#endif // UNITY_EDITOR
