#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEditor;
using Kawashirov;
using Kawashirov.ToolsGUI;
using System.IO;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Kawashirov.LightProbesTools {

	[ToolsWindowPanel("Light Probes/Manage")]
	public partial class LightProbesToolPanelManage : AbstractToolPanel {

		[NonSerialized] public bool loadAllowPosMismatch = false;

		public bool TryAssignLightProbes(UnityEngine.Object changedObject) {
			if (changedObject == null || changedObject == LightmapSettings.lightProbes)
				return false;

			var changedProbes = changedObject as LightProbes;
			if (changedProbes != null)
				return LightProbesUtility.SetLightProbes(changedProbes);

			var changedPath = AssetDatabase.GetAssetPath(changedObject);
			if (string.IsNullOrWhiteSpace(changedPath)) {
				Debug.LogWarning($"Can not find LightProbes asset, provided reference asset is not persistent: {changedObject}", changedObject);
			}

			Debug.Log($"Trying to find LightProbes at path: {changedPath} ...", changedObject);
			var changedProbesArray = AssetDatabase.LoadAllAssetsAtPath(changedPath).OfType<LightProbes>().ToArray();

			if (changedProbesArray.Length == 1)
				return LightProbesUtility.SetLightProbes(changedProbesArray[0]);

			if (changedProbesArray.Length < 1)
				Debug.LogWarning($"There is no LightProbes at path: {changedPath}", changedObject);
			else
				Debug.LogWarning($"There is multiple ({changedProbesArray.Length}) LightProbes at path: {changedPath}", changedObject);

			return false;
		}

		public void LoadSavedSphericalHarmonicsL2(bool allowPosMismatch) {
			var loadPath = EditorUtility.OpenFilePanel("", Application.dataPath, "");

			if (string.IsNullOrWhiteSpace(loadPath)) {
				return;
			}

			if (loadPath.StartsWith(Application.dataPath)) {
				loadPath = "Assets" + loadPath.Substring(Application.dataPath.Length);
			}

			Debug.Log($"Loading LightProbes at path: {loadPath}", this);

			var assets = AssetDatabase.LoadAllAssetsAtPath(loadPath);
			if (assets == null || assets.Length < 1) {
				Debug.LogWarning($"There is no any assets at path: {loadPath}", this);
				return;
			}

			var lightProbesL = assets.OfType<LightProbes>().ToList();
			if (lightProbesL.Count < 1) {
				Debug.LogWarning($"There is no LightProbes at path: {loadPath}", this);
				return;
			}
			if (lightProbesL.Count > 2) {
				Debug.LogWarning($"There is multiple ({lightProbesL.Count}) LightProbes at path: {loadPath}", this);
				return;
			}

			var newLightProbes = lightProbesL[0];
			var currentLightProbes = LightmapSettings.lightProbes;
			if (newLightProbes == currentLightProbes) {
				Debug.LogWarning($"Trying to load LightProbes that's already assigned to LightmapSettings.lightProbes: {loadPath}", this);
				return;
			}

			var newPositions = newLightProbes.positions;
			var newSHL2 = newLightProbes.bakedProbes;
			var currentPositions = currentLightProbes.positions;
			var currentSHL2 = currentLightProbes.bakedProbes;

			if (allowPosMismatch) {

				Debug.Log($"Copying LightProbes (missmatching mode) from path: {loadPath}", this);
				var applyCounter = 0;
				var ni = Math.Min(currentPositions.Length, currentSHL2.Length);
				var nj = Math.Min(newPositions.Length, newSHL2.Length);
				LightProbesUtility.RegisterLightingUndo("Load Saved SphericalHarmonicsL2");
				for (var i = 0; i < ni; ++i) {
					for (var j = 0; j < nj; ++j) {
						if (currentPositions[i] == newPositions[j]) {
							currentSHL2[i] = newSHL2[j];
							++applyCounter;
							break;
						}
					}
				}
				currentLightProbes.bakedProbes = currentSHL2;
				LightProbesUtility.SetLightingDirty();
				Debug.Log($"Copied {applyCounter} of {currentSHL2.Length} and {newSHL2.Length} LightProbes (missmatching mode) from path: {loadPath}", this);

			} else {

				Debug.Log($"Copying LightProbes (matching mode) from path: {loadPath}", this);
				if (newPositions.Length != currentPositions.Length) {
					Debug.LogWarning($"Number of positions of new LightProbes ({newPositions.Length}) and current LightProbes ({currentPositions.Length}) does not match.", this);
					return;
				}
				if (newSHL2.Length != currentSHL2.Length) {
					Debug.LogWarning($"Number of SphericalHarmonicsL2 of new LightProbes ({newSHL2.Length}) and current LightProbes ({currentSHL2.Length}) does not match.", this);
					return;
				}
				for (var i = 0; i < newPositions.Length; ++i) {
					if (newPositions[i] != currentPositions[i]) {
						Debug.LogWarning($"Position at #{i}/{newPositions.Length} of new and current LightProbes does not match.", this);
						return;
					}
				}
				LightProbesUtility.RegisterLightingUndo("Load Saved SphericalHarmonicsL2");
				Array.Copy(newSHL2, currentSHL2, newSHL2.Length);
				currentLightProbes.bakedProbes = currentSHL2;
				LightProbesUtility.SetLightingDirty();
				Debug.Log($"Copied {currentSHL2.Length} LightProbes (matching mode) from path: {loadPath}", this);
			}
		}

		private static bool LightProbesExist() {
			var lightProbes = LightmapSettings.lightProbes;
			if (!lightProbes)
				return false;
			var positions = lightProbes.positions;
			return positions != null && positions.Length > 0;
		}

		private void ToolsGUI_CurrentLightProbes() {
			EditorGUILayout.LabelField("Current LightmapSettings.lightProbes:", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope()) {
				if (!LightProbesExist()) {
					EditorGUILayout.LabelField("No Light Probes exist!");
					return;
				}

				var lightProbes = LightmapSettings.lightProbes;

				var purePath = AssetDatabase.GetAssetPath(lightProbes);
				if (string.IsNullOrWhiteSpace(purePath))
					purePath = null; // null вместо пустоты на всякий
				var displayPath = purePath == null ? "<Asset does not exist>" : purePath;
				var isMainAsset = AssetDatabase.IsMainAsset(lightProbes);
				var isSubAsset = AssetDatabase.IsSubAsset(lightProbes);

				{
					var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2));
					var rects = rect.RectSplitHorisontal(3, 1).ToArray();
					using (new KawaGUIUtility.ZeroIndentScope()) {
						using (new EditorGUI.DisabledScope(true)) {
							// Disabled не открывает окно выбора объекта по клику
							EditorGUI.ObjectField(rects[0], lightProbes, typeof(UnityEngine.Object), true);
						}
						using (new EditorGUI.DisabledScope(purePath == null)) {
							if (GUI.Button(rects[1], "Select")) {
								EditorUtility.FocusProjectWindow();
								// Пинг не подсвечивает файл, если ассет не основной.
								var main = isMainAsset ? lightProbes : AssetDatabase.LoadMainAssetAtPath(purePath);
								if (main == null)
									main = lightProbes; // Такого не должно быть, просто на всякий.
								Selection.objects = main == lightProbes ? new[] { lightProbes } : new[] { main, lightProbes };
								Selection.activeObject = lightProbes;
								EditorGUIUtility.PingObject(main);
							}
						}
					}
				}

				EditorGUILayout.SelectableLabel(displayPath);
				EditorGUILayout.LabelField("Light Probes Count", $"{lightProbes.count}");
				EditorGUILayout.LabelField("CellCount", $"{lightProbes.cellCount}");
				EditorGUILayout.LabelField("Is Main Asset", isMainAsset ? "Yes" : "No");
				EditorGUILayout.LabelField("Is Sub Asset", isSubAsset ? "Yes" : "No");
			}
		}

		public override void ToolsGUI() {
			ToolsGUI_CurrentLightProbes();

			EditorGUILayout.Space();

			using (new EditorGUI.DisabledScope(!LightProbesExist())) {
				var button1Rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2);
				if (GUI.Button(button1Rect, "Save Copy of Current LightProbes")) {
					var instance_id = LightmapSettings.lightProbes.GetInstanceID();
					var savePath = EditorUtility.SaveFilePanelInProject("Save LightProbes asset", $"light_probes_{instance_id}.asset", "asset", "Save LightProbes asset");
					if (!string.IsNullOrWhiteSpace(savePath)) {
						var newProbes = Instantiate(LightmapSettings.lightProbes);
						AssetDatabase.CreateAsset(newProbes, savePath);
					}
				}
			}

			EditorGUILayout.Space();

			using (new EditorGUI.DisabledScope(!LightProbesExist())) {
				loadAllowPosMismatch = EditorGUILayout.ToggleLeft("Allow Position Mismatch", loadAllowPosMismatch);
				var button2Rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2);
				if (GUI.Button(button2Rect, "Load SphericalHarmonicsL2 from Saved Copy of Current LightProbes") && LightmapSettings.lightProbes) {
					LoadSavedSphericalHarmonicsL2(loadAllowPosMismatch);
				}
			}

			EditorGUILayout.Space();

			var button3Rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2);
			if (GUI.Button(button3Rect, "Tetrahedralize LightProbes") && LightmapSettings.lightProbes) {
				LightProbes.Tetrahedralize();
				LightProbesVisualizer.Tetrahedralize();
			}

		}

	}
}
#endif // UNITY_EDITOR
