#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
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
			var unityObj = refreshable as UnityEngine.Object;
			var refreshSucess = false;
			var undoGroup = Undo.GetCurrentGroup();
			try {
				var undoName = $"Refresh {unityObj.name} ({unityObj.GetType().Name})";
				Undo.SetCurrentGroupName(undoName);
				Undo.RegisterCompleteObjectUndo(unityObj, undoName);
				// Debug.Log($"Refreshing <b>{refreshable}</b>...", unityObj); // DEBUG
				refreshable.Refresh();
				refreshSucess = true;
			} catch (Exception exc) {
				var errMsg = $"Failed to Refresh: \"<b>{exc.Message}</b>\"\n@ <i>{refreshable}</i>\n{exc.StackTrace}";
				Debug.LogError(errMsg, unityObj);
				Debug.LogException(exc, unityObj);
			} finally {
				if (unityObj != null)
					Undo.CollapseUndoOperations(undoGroup);
			}
			return refreshSucess;
		}

		public static void RefreshMultiple<T>(this IEnumerable<T> refreshables) where T : class, IRefreshable {
			var array = refreshables.ToList();
			var errors = 0;
			try {
				Debug.Log($"Refreshing <b>{array.Count}</b> objects...");
				for (var i = 0; i < array.Count; ++i) {
					var refreshable = array[i];
					var path = refreshable.RefreshablePath();
					var info = string.Format($"Refreshing {i + 1}/{array.Count}: {path}");
					var progress = 1.0f * (i + 1) / (array.Count + 1);
					if (EditorUtility.DisplayCancelableProgressBar(info, info, progress))
						break;
					if (!refreshable.RefreshSafe())
						++errors;
				}
			} finally {
				EditorUtility.ClearProgressBar();
			}
			if (errors < 1) {
				Debug.Log($"Refreshed <b>{array.Count}</b> objects. No errors.");
			} else {
				Debug.LogWarning($"Refreshed <b>{array.Count}</b> objects: <b>{errors}</b> errors!");
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
