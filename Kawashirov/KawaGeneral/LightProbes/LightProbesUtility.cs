#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.SceneManagement;
using UnityEditor;

namespace Kawashirov.LightProbesTools {
	public static class LightProbesUtility {

		[Obsolete("Does not work. Unity just does not want to saves changes.")]
		public static bool SetLightProbes(LightProbes newProbes) {
			var oldProbes = LightmapSettings.lightProbes;
			if (oldProbes == newProbes)
				return false;
			var oldInfo = oldProbes ? $"{AssetDatabase.GetAssetPath(oldProbes)} №{oldProbes.GetInstanceID()}" : "null";
			var newInfo = newProbes ? $"{AssetDatabase.GetAssetPath(newProbes)} №{newProbes.GetInstanceID()}" : "null";
			LightmapSettings.lightProbes = newProbes;
			Debug.Log($"Changed LightProbes: {oldInfo} -> {newInfo}");
			return true;
		}

		[MenuItem("Kawashirov/Smooth SH")]
		public static void SmoothSH() {
			// TODO panel
			try {
				var probes = LightmapSettings.lightProbes.bakedProbes;
				var ambient_probe = new SphericalHarmonicsL2();
				var zero_dir = new Vector3[] { Vector3.zero };
				var ambient_color = new Color[1];
				for (var i = 0; i < probes.Length; ++i) {
					if (i % 10 == 0)
						EditorUtility.DisplayProgressBar("Smoothing Lightprobes", string.Format("{0}/{1}", i, probes.Length), 1.0f * i / probes.Length);
					ambient_probe.Clear();
					var src_probe = probes[i];
					src_probe.Evaluate(zero_dir, ambient_color);
					ambient_probe.AddAmbientLight(ambient_color[0]);
					probes[i] = src_probe * 0.8f + ambient_probe * 0.2f;
				}
				LightmapSettings.lightProbes.bakedProbes = probes;
				Undo.RecordObject(LightmapSettings.lightProbes, "Smooth SH");
				EditorUtility.SetDirty(LightmapSettings.lightProbes);
				EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
			} finally {
				EditorUtility.ClearProgressBar();
			}

		}

		public static void Report(UnityEngine.Object obj, string path, int depth, ref HashSet<UnityEngine.Object> noLoops) {
			if (obj == null) {
				Debug.Log($"Object is null: {path}");
				return;
			}
			if (depth > 20) {
				Debug.Log($"Max depth reached: {obj}: {path}");
				return;
			}
			if (noLoops.Contains(obj)) {
				Debug.Log($"Object loop detected: {obj}: {path}");
				return;
			}
			noLoops.Add(obj);

			var asset = AssetDatabase.GetAssetPath(obj);
			Debug.Log($"Asset path {path} = {asset}");

			var sobj = new SerializedObject(obj);
			var prop = sobj.GetIterator();
			for (var i = 0; i < 10000; ++i) {
				Debug.Log($"{path}/{prop.propertyPath} : {prop.propertyType}, {prop.type}");
				if (prop.propertyType == SerializedPropertyType.ObjectReference) {
					Report(prop.objectReferenceValue, $"{path}/{prop.propertyPath}", depth + 1, ref noLoops);
				} else if (prop.propertyType == SerializedPropertyType.ExposedReference) {
					Report(prop.exposedReferenceValue, $"{path}/{prop.propertyPath}", depth + 1, ref noLoops);
				} else if (prop.propertyType == SerializedPropertyType.ManagedReference) {
					Debug.Log($"Managed value: {prop.managedReferenceFieldTypename}, {prop.managedReferenceFullTypename}");
				} else if (prop.propertyType == SerializedPropertyType.Integer) {
					Debug.Log($"{path}/{prop.propertyPath} = {prop.longValue} = {prop.intValue}");
				}
				if (!prop.Next(true))
					break;
			}
		}

		[MenuItem("Kawashirov/Report LightingData")]
		public static void ReportLightingData() {
			// TODO panel
			Debug.Log("LightingDataAsset begin:");

			var data = Lightmapping.lightingDataAsset;
			var noLoops = new HashSet<UnityEngine.Object>();
			Report(data, "", 0, ref noLoops);

			Debug.Log("LightingDataAsset end.");
		}


	}
}
#endif // UNITY_EDITOR
