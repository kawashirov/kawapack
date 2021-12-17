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
				Debug.Log($"There is no LightProbes at path: {changedPath}", changedObject);
			else
				Debug.Log($"There is multiple ({changedProbesArray.Length}) LightProbes at path: {changedPath}", changedObject);

			return false;
		}

		public override void ToolsGUI() {
			var changedObject = EditorGUILayout.ObjectField("Current LightmapSettings.lightProbes", LightmapSettings.lightProbes, typeof(UnityEngine.Object), true);
			TryAssignLightProbes(changedObject);

			using (new EditorGUI.IndentLevelScope(1)) {
				var path = LightmapSettings.lightProbes ? AssetDatabase.GetAssetPath(LightmapSettings.lightProbes) : null;
				if (string.IsNullOrWhiteSpace(path))
					path = "<does not exist>";
				var isMainAsset = LightmapSettings.lightProbes && AssetDatabase.IsMainAsset(LightmapSettings.lightProbes) ? "Yes" : "No";
				var isSubAsset = LightmapSettings.lightProbes && AssetDatabase.IsSubAsset(LightmapSettings.lightProbes) ? "Yes" : "No";
				EditorGUILayout.TextField("Path", path);
				EditorGUILayout.LabelField("Is Main Asset", isMainAsset);
				EditorGUILayout.LabelField("Is Sub Asset", isSubAsset);
			}

			if (GUILayout.Button("Save Copy of Current LightProbes") && LightmapSettings.lightProbes) {
				var instance_id = LightmapSettings.lightProbes.GetInstanceID();
				var scenePath = EditorSceneManager.GetActiveScene().path;
				var savePath = EditorUtility.SaveFilePanelInProject("Save LightProbes asset", $"light_probes_{instance_id}.asset", "asset", "Save LightProbes asset");
				if (!string.IsNullOrWhiteSpace(savePath)) {
					var newProbes = Instantiate(LightmapSettings.lightProbes);
					AssetDatabase.CreateAsset(newProbes, savePath);
					LightmapSettings.lightProbes = newProbes;
				}
			}

		}

	}
}
#endif // UNITY_EDITOR
