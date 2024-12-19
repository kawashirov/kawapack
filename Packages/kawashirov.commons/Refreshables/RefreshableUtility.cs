#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kawashirov.Refreshables {
	public static class RefreshableUtility {

		public static void GetRefresahblesByGUID(string guid, Type sub_type, ICollection<IRefreshable> container) {
			var path = AssetDatabase.GUIDToAssetPath(guid);
			var objects = AssetDatabase.LoadAllAssetsAtPath(path)
				.Where(obj => obj != null && obj is IRefreshable && (
					sub_type == null || sub_type.IsAssignableFrom(obj.GetType())
				));
			foreach (var obj in objects)
				if (obj is IRefreshable robj)
					container.Add(robj);
		}

		public static HashSet<IRefreshable> LoadAllRefreshablesInProject(Type sub_type) {
			Debug.Log($"[KawaEditor] Searching refreshable ({sub_type}) assets...");
			var guids = AssetDatabase.FindAssets("t:" + typeof(ScriptableObject).Name);
			Debug.Log($"[KawaEditor] Found {guids.Length} scriptables GUIDs, loading...");
			var set = new HashSet<IRefreshable>();
			foreach (var guid in guids)
				GetRefresahblesByGUID(guid, sub_type, set);
			Debug.Log($"[KawaEditor] Loaded {set.Count} refreshable ({sub_type}) objects...");
			return set;
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
