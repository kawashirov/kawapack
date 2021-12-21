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

		public void LoadSavedSphericalHarmonicsL2() {
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
				Debug.LogWarning($"Trying to load LightProbes thas already assigned to LightmapSettings.lightProbes: {loadPath}", this);
				return;
			}

			var newPositions = newLightProbes.positions;
			var newSHL2 = newLightProbes.bakedProbes;
			var currentPositions = currentLightProbes.positions;
			var currentSHL2 = currentLightProbes.bakedProbes;

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

			Debug.Log($"Copying LightProbes from path: {loadPath}", this);
			Array.Copy(newSHL2, currentSHL2, newSHL2.Length);
			LightProbesUtility.RegisterLightingUndo("Load Saved SphericalHarmonicsL2");
			currentLightProbes.bakedProbes = currentSHL2;
			LightProbesUtility.SetLightingDirty();
			Debug.Log($"Copied LightProbes from path: {loadPath}", this);
		}

		public override void ToolsGUI() {
			EditorGUILayout.LabelField("Current LightmapSettings.lightProbes:");
			using (new EditorGUI.IndentLevelScope()) {
				var objectRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2);
				EditorGUI.ObjectField(objectRect, LightmapSettings.lightProbes, typeof(UnityEngine.Object), true);
				var path = LightmapSettings.lightProbes ? AssetDatabase.GetAssetPath(LightmapSettings.lightProbes) : null;
				if (string.IsNullOrWhiteSpace(path))
					path = "<does not exist>";
				var isMainAsset = LightmapSettings.lightProbes && AssetDatabase.IsMainAsset(LightmapSettings.lightProbes) ? "Yes" : "No";
				var isSubAsset = LightmapSettings.lightProbes && AssetDatabase.IsSubAsset(LightmapSettings.lightProbes) ? "Yes" : "No";
				EditorGUILayout.TextField("Path", path);
				EditorGUILayout.LabelField("Is Main Asset", isMainAsset);
				EditorGUILayout.LabelField("Is Sub Asset", isSubAsset);
			}

			EditorGUILayout.Space();
			var button1Rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2);
			if (GUI.Button(button1Rect, "Save Copy of Current LightProbes") && LightmapSettings.lightProbes) {
				var instance_id = LightmapSettings.lightProbes.GetInstanceID();
				var savePath = EditorUtility.SaveFilePanelInProject("Save LightProbes asset", $"light_probes_{instance_id}.asset", "asset", "Save LightProbes asset");
				if (!string.IsNullOrWhiteSpace(savePath)) {
					var newProbes = Instantiate(LightmapSettings.lightProbes);
					AssetDatabase.CreateAsset(newProbes, savePath);
				}
			}

			EditorGUILayout.Space();
			var button2Rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2);
			if (GUI.Button(button2Rect, "Load SphericalHarmonicsL2 from Saved Copy of Current LightProbes") && LightmapSettings.lightProbes) {
				LoadSavedSphericalHarmonicsL2();
			}

			EditorGUILayout.Space();
			var button3Rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2);
			if (GUI.Button(button3Rect, "Tetrahedralize LightProbes") && LightmapSettings.lightProbes) {
				LightProbes.Tetrahedralize();
			}

		}

	}
}
#endif // UNITY_EDITOR
