#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using Object = UnityEngine.Object;

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
			// "Безопасное" обновление IRefreshable
			// Регистрация Undo и подробные сообщения об ошибке.
			// Возвращает true если получилось сделать Refresh, иначе false
			var refresh_sucess = false;
			if (refreshable is Object uobj) {
				var undoGroup = Undo.GetCurrentGroup();
				try {
					var undoName = $"Refresh {uobj.name} ({uobj.GetType().Name})";
					Undo.SetCurrentGroupName(undoName);
					Undo.RegisterCompleteObjectUndo(uobj, undoName);
					// Debug.Log($"Refreshing <b>{refreshable}</b>...", unityObj); // DEBUG
					refreshable.Refresh();
					refresh_sucess = true;
				} catch (Exception exc) {
					Debug.LogException(exc, uobj);
					var errMsg = $"Failed to Refresh: \"<b>{exc.Message}</b>\"\n@ <i>{refreshable}</i>\n{exc.StackTrace}";
					Debug.LogError(errMsg, uobj);
				} finally {
					if (uobj != null)
						Undo.CollapseUndoOperations(undoGroup);
				}
			} else {
				try {
					refreshable.Refresh();
					refresh_sucess = true;
				} catch (Exception exc) {
					Debug.LogException(exc);
					var errMsg = $"Failed to Refresh: \"<b>{exc.Message}</b>\"\n@ <i>{refreshable}</i>\n{exc.StackTrace}";
					Debug.LogError(errMsg);
				}
			}
			return refresh_sucess;
		}

		public static void RefreshMultiple<T>(this IEnumerable<T> refreshables) where T : class, IRefreshable {
			var array = refreshables.ToList();
			var errors = 0;
			try {
				Debug.Log($"Refreshing <b>{array.Count}</b> objects...");
				for (var i = 0; i < array.Count; ++i) {
					var refreshable = array[i];
					var path = (refreshable is Object uobj) ? uobj.KawaGetFullPath() : "<unknown path>";
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

			if (refreshables.Count == 1) {
				// Simple mode (performant, most common)
				var refreshable = refreshables[0];
				GUILayout.Label("Refresh:");
				using (new EditorGUILayout.HorizontalScope()) {
					if (GUILayout.Button("Only This")) {
						refreshable.RefreshSafe();
					}
					var type = refreshable.GetType();
					if (GUILayout.Button($"Every {type.Name}")) {
						var component = refreshable as Component;
						var scene = component != null ? component.gameObject.scene : EditorSceneManager.GetActiveScene();
						var all_targets = scene.GetRootGameObjects()
								.SelectMany(g => g.GetComponentsInChildren(type, true))
								.Distinct().OfType<IRefreshable>().ToList();
						all_targets.RefreshMultiple();
					}
				}
				return;
			}

			// Advanced mode

			var types = editor.targets.Select(t => t.GetType()).Distinct().ToList();
			var scenes = editor.targets.OfType<Component>().Select(r => r.gameObject.scene).Distinct().ToList();

			GUILayout.Label($"Refresh ({refreshables.Count} objects):");
			if (types.Count > 1 || scenes.Count > 1) {
				using (new EditorGUI.IndentLevelScope()) {
					if (types.Count > 1) {
						var types_str = string.Join(", ", types.Select(t => t.Name));
						GUILayout.Label($"Types ({types.Count}): {types_str})");
					}
					if (scenes.Count > 1) {
						var scenes_str = string.Join(", ", scenes.Select(s => s.name));
						GUILayout.Label($"Scenes ({scenes.Count}): {scenes_str})");
					}
				}
			}
			using (new EditorGUILayout.HorizontalScope()) {
				if (GUILayout.Button($"Selected ({refreshables.Count}")) {
					refreshables.RefreshMultiple();
				}
				if (GUILayout.Button($"Every Type ({types.Count})")) {
					var all_targets = scenes.SelectMany(s => s.GetRootGameObjects())
							.SelectMany(g => types.SelectMany(t => g.GetComponentsInChildren(t, true)))
							.Distinct().OfType<IRefreshable>().ToList();
					all_targets.RefreshMultiple();
				}
			}
		}
	}
}

#endif // UNITY_EDITOR
