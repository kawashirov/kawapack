using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;


#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Kawashirov.SceneBuilding {
	public class SaveBuildingSceneAction : BaseBuildingAction {
#if UNITY_EDITOR
		public bool ActivateThisScene = true;
		public bool EnsureOtherScenesUnloaded = true;

		[HideInInspector] public string OriginalScenePath;

		public static string GetBuildingPath(Scene scene) {
			var src_path = scene.path;
			var path = FileUtil.GetLogicalPath(Path.GetDirectoryName(src_path)); // .Replace("\\", "/");
			var name = Path.GetFileNameWithoutExtension(src_path);
			var ext = Path.GetExtension(src_path);
			return $"{path}/Building{name}{ext}";
		}

		public void OpenOriginal(bool unload) {
			if (string.IsNullOrWhiteSpace(OriginalScenePath)) {
				Debug.LogError($"Can't open original scene, there is no {nameof(OriginalScenePath)} set.", this);
			}
			var original_scene_path = OriginalScenePath;

			var building_scene = gameObject.scene;
			var is_active = EditorSceneManager.GetActiveScene() == building_scene;
			Scene original_scene;
			try {
				Debug.Log($"Opening original scene \"{original_scene_path}\"...", this);
				original_scene = EditorSceneManager.OpenScene(original_scene_path, OpenSceneMode.Additive);
			} catch (ArgumentException exc) {
				Debug.LogException(exc);
				Debug.LogError($"Failed to open original scene \"{original_scene_path}\": {exc}", this);
				return;
			}

			if (is_active) {
				Debug.Log($"Activating original scene \"{original_scene_path}\"...", this);
				EditorSceneManager.SetActiveScene(original_scene);
			}

			if (unload) {
				Debug.Log($"Unloading building scene \"{original_scene_path}\"...", this);
				var op = EditorSceneManager.UnloadSceneAsync(building_scene);
				op.completed += a => Debug.Log($"Unloaded building scene \"{original_scene_path}\".");
			}
		}

		public override void RunSync() {
			var scene = gameObject.scene;
			var original_scene_path = OriginalScenePath = scene.path;
			EditorUtility.SetDirty(this);
			var building_scene_path = GetBuildingPath(scene);
			Debug.Log($"Selected building scene path: \"{building_scene_path}\".", this);

			if (ActivateThisScene) {
				Debug.Log($"Activating current scene...", this);
				EditorSceneManager.SetActiveScene(scene);
			}

			var scenes_to_unload = new List<Scene>();
			for (var i = 0; i < EditorSceneManager.loadedRootSceneCount; ++i) {
				
				var whatever_scene = EditorSceneManager.GetSceneAt(i);
				// Debug.Log($"Scene #{i} path: \"{whatever_scene.path}\"", this);
				if (EnsureOtherScenesUnloaded && whatever_scene != scene) {
					// Если требуется отгрузить вообще все сцены, кроме текущей. 
					scenes_to_unload.Add(scene);
				} else if (string.Equals(whatever_scene.path, building_scene_path)) {
					// Прежде чем сохраниться в building_scene_path
					// нужно убедиться, что чичего по этому пути нет в EditorSceneManager
					scenes_to_unload.Add(scene);
				}
			}
			foreach (var scene_to_unload in scenes_to_unload) {
				Debug.Log($"Found that building scene \"{building_scene_path}\" already opened, closing...", this);
				// EditorSceneManager.UnloadSceneAsync(scene_to_unload);
				if (EditorSceneManager.CloseScene(scene_to_unload, true)) {
					Debug.Log($"Closed old building scene \"{building_scene_path}\".", this);
				} else {
					Debug.LogError($"Old building scene \"{building_scene_path}\" can't be closed for some reason.", this);
				}
			}

			// Теперь можно снести старую сцену сборки
			Debug.Log($"Removing old building scene \"{building_scene_path}\"...", this);
			var del_result = FileUtil.DeleteFileOrDirectory(building_scene_path);
			Debug.Log($"Removed old building scene \"{building_scene_path}\": {del_result}", this);

			// Теперь можно сохранить эту сцену на место сцены сборки
			if (!EditorSceneManager.SaveScene(scene, building_scene_path)) {
				var msg = $"Failed to save building scene: \"{building_scene_path}\"";
				Debug.LogError(msg, this);
				throw new FailedToSaveBuildingScene(msg);
			}
			EditorSceneManager.MarkSceneDirty(scene);
			Debug.Log($"Scene saved as building to new location \"{building_scene_path}\".", this);

			// И подгрузить оригинал на менеджер сцен
			Debug.Log($"Adding back original scene \"{original_scene_path}\"...", this);
			EditorSceneManager.OpenScene(original_scene_path, OpenSceneMode.AdditiveWithoutLoading);
			Debug.Log($"Added back original scene \"{original_scene_path}\".", this);
		}

		[CustomEditor(typeof(SaveBuildingSceneAction), true)]
		public class SaveBuildingSceneActionEditor : BaseBuildingActionEditor {
			public override bool ShowIKnowWhatIamDoing() => true;

			public override void BuildingActionGUI() {
				var target = this.target as SaveBuildingSceneAction;
				if (!target)
					return;

				GUILayout.Label("Original Scene Path:");
				using (new EditorGUI.IndentLevelScope(1)) {
					var osp = target.OriginalScenePath;
					GUILayout.Label(string.IsNullOrWhiteSpace(osp) ? "<not set>" : osp);
				}

				using (new EditorGUILayout.HorizontalScope()) {
					var no_osp = string.IsNullOrWhiteSpace(target.OriginalScenePath);
					using (new EditorGUI.DisabledScope(no_osp)) {
						if (GUILayout.Button("Go Back to Original...")) {
							target.OpenOriginal(true);
						}
					}
					using (new EditorGUI.DisabledScope(no_osp || !IKnowWhatIamDoing)) {
						if (GUILayout.Button("...But Not Unload Building")) {
							IKnowWhatIamDoing = false;
							target.OpenOriginal(false);
						}
					}
				}

				IKnowWhatIamDoingGUI();
			}
		}
#endif
	}
}