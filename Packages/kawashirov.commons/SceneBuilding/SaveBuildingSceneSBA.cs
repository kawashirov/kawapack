#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Kawashirov.SceneBuilding {
	public class SaveBuildingSceneSBA : BaseSBA {
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
				LogError($"Can't open original scene, there is no {nameof(OriginalScenePath)} set.");
			}
			var original_scene_path = OriginalScenePath;

			var building_scene = gameObject.scene;
			var is_active = EditorSceneManager.GetActiveScene() == building_scene;
			Scene original_scene;
			try {
				Log($"Opening original scene \"{original_scene_path}\"...");
				original_scene = EditorSceneManager.OpenScene(original_scene_path, OpenSceneMode.Additive);
			} catch (ArgumentException exc) {
				LogException($"Failed to open original scene \"{original_scene_path}\"", exc);
				throw exc;
			}

			if (is_active) {
				Log($"Activating original scene \"{original_scene_path}\"...");
				EditorSceneManager.SetActiveScene(original_scene);
			}

			if (unload) {
				Log($"Unloading building scene \"{original_scene_path}\"...");
				var op = EditorSceneManager.UnloadSceneAsync(building_scene);
				op.completed += a => Log($"Unloaded building scene \"{original_scene_path}\".");
			}
		}

		public override void RunSync(BuildingScenario scenario) {
			var scene = gameObject.scene;
			var original_scene_path = OriginalScenePath = scene.path;
			EditorUtility.SetDirty(this);
			var building_scene_path = GetBuildingPath(scene);
			Log($"Selected building scene path: \"{building_scene_path}\".");

			if (ActivateThisScene) {
				LogDebug($"Activating current scene...");
				EditorSceneManager.SetActiveScene(scene);
			}

			var scenes_to_unload = new List<Scene>();
			for (var i = 0; i < EditorSceneManager.loadedRootSceneCount; ++i) {

				var whatever_scene = EditorSceneManager.GetSceneAt(i);
				// Log($"Scene #{i} path: \"{whatever_scene.path}\"");
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
				LogDebug($"Found that building scene \"{building_scene_path}\" already opened, closing...");
				// EditorSceneManager.UnloadSceneAsync(scene_to_unload);
				if (EditorSceneManager.CloseScene(scene_to_unload, true)) {
					LogDebug($"Closed old building scene \"{building_scene_path}\".");
				} else {
					LogError($"Old building scene \"{building_scene_path}\" can't be closed for some reason.");
				}
			}

			// Теперь можно снести старую сцену сборки
			LogDebug($"Removing old building scene \"{building_scene_path}\"...");
			var del_result = FileUtil.DeleteFileOrDirectory(building_scene_path);
			LogDebug($"Removed old building scene \"{building_scene_path}\": {del_result}");

			// Теперь можно сохранить эту сцену на место сцены сборки
			if (!EditorSceneManager.SaveScene(scene, building_scene_path)) {
				ThrowException(new FailedToSaveBuildingScene($"Failed to save building scene: \"{building_scene_path}\""));
			}
			EditorSceneManager.MarkSceneDirty(scene);
			Log($"Scene saved as building to new location \"{building_scene_path}\".");

			// И подгрузить оригинал на менеджер сцен
			LogDebug($"Adding back original scene \"{original_scene_path}\"...");
			EditorSceneManager.OpenScene(original_scene_path, OpenSceneMode.AdditiveWithoutLoading);
			LogDebug($"Added back original scene \"{original_scene_path}\".");
		}

		[CustomEditor(typeof(SaveBuildingSceneSBA), true)]
		public class SaveBuildingSceneSBAEditor : BaseSBAEditor {
			public override bool ShowIKnowWhatIamDoing() => true;

			public override void BuildingActionGUI() {
				var target = this.target as SaveBuildingSceneSBA;
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
			}
		}
	}
}
#endif
