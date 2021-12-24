#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.EditorCoroutines.Editor;
using Kawashirov.ToolsGUI;
using UnityEngine.SceneManagement;
using System.IO;

namespace Kawashirov {

	[ToolsWindowPanel("Asset Bundles")]
	public class AssetBundlesUtility : AbstractToolPanel {

		private AssetBundle assetBundle;
		private string assetBundlePath;
		private string unpackFolder;

		public override void ToolsGUI() {
			if (GUILayout.Button("Unload all Asset Bundle")) {
				AssetBundle.UnloadAllAssetBundles(true);
			}
			if (GUILayout.Button("Play Streamed Scene Asset Bundle")) {
				PlayStreamedSceneAssetBundle();
			}
		}

		public void PlayStreamedSceneAssetBundle() {
			if (assetBundle) {
				Debug.LogWarning("Unloading previously loaded AssetBundle...", this);
				assetBundle.Unload(true);
			}

			assetBundlePath = EditorUtility.OpenFilePanel("Select AssetBundle", Application.dataPath, "");
			if (string.IsNullOrWhiteSpace(assetBundlePath)) {
				Debug.LogWarning("Unpacking cancelled: AssetBundle is not selected", this);
				return;
			}

			try {
				Debug.Log($"Loading AssetBundle from {assetBundlePath}...", this);
				EditorUtility.DisplayProgressBar("Loading AssetBundle...", "Loading AssetBundle...", 0f);
				assetBundle = AssetBundle.LoadFromFile(assetBundlePath);
				if (assetBundle == null) {
					Debug.LogWarning($"Failed to load AssetBundle from {assetBundlePath}!", this);
					return;
				}

				if (!assetBundle.isStreamedSceneAssetBundle) {
					Debug.LogWarning($"AssetBundle from {assetBundlePath} is not Streamed Scene: Can not play it!", this);
					return;
				}

				EditorUtility.DisplayProgressBar("Loading AssetBundle...", "Loading Scenes...", 0f);
				Debug.Log($"Loading Scenes from AssetBundle from {assetBundlePath}...", this);
				var scenePaths = assetBundle.GetAllScenePaths();
				if (scenePaths == null || scenePaths.Length < 1) {
					Debug.LogWarning($"AssetBundle from {assetBundlePath} does not contains any scenes!", this);
					return;
				}
				if (scenePaths.Length > 1) {
					var scenes = string.Join("\n", scenePaths);
					Debug.LogWarning($"AssetBundle from {assetBundlePath} contains multiple scenes:\n{scenes}", this);
				}
				for (var i = 0; i < scenePaths.Length; ++i) {
					Debug.Log($"Loading Scene #{i} {scenePaths[i]} from AssetBundle from {assetBundlePath}...", this);
					// EditorSceneManager.LoadSceneAsyncInPlayMode(scenePaths[i], new LoadSceneParameters(i == 0 ? LoadSceneMode.Single : LoadSceneMode.Additive));
					SceneManager.LoadScene(Path.GetFileNameWithoutExtension(scenePaths[i]), i == 0 ? LoadSceneMode.Single : LoadSceneMode.Additive);
				}

			} finally {
				EditorUtility.ClearProgressBar();
			}

		}


		public IEnumerator OnGUI_UnpackAssetBundle() {
			Debug.Log("Unpacking AssetBundle...", this);

			if (assetBundle) {
				assetBundle.Unload(true);
				assetBundle = null;
			}

			assetBundlePath = EditorUtility.OpenFilePanel("Select *.vrcw or *.vrca", Application.dataPath, "");
			if (string.IsNullOrWhiteSpace(assetBundlePath)) {
				Debug.LogWarning("Unpacking cancelled: AssetBundle is not selected", this);
				yield break;
			}

			unpackFolder = EditorUtility.OpenFolderPanel("Unpack AssetBundle to", Application.dataPath, "");
			if (string.IsNullOrWhiteSpace(unpackFolder)) {
				Debug.LogWarning("Unpacking cancelled: Unpack Folder is not selected", this);
				yield break;
			}

			try {
				Debug.Log($"Unpacking AssetBundle from {assetBundlePath}...", this);
				EditorUtility.DisplayProgressBar("Unpacking AssetBundle", assetBundlePath, 0f);
				assetBundle = AssetBundle.LoadFromFile(assetBundlePath);
				if (assetBundle == null) {
					Debug.LogWarning($"Failed to load AssetBundle from {assetBundlePath}!", this);
					yield break;
				}

				Debug.Log($"Reaning names in AssetBundle...", this);
				EditorUtility.DisplayProgressBar("Unpacking AssetBundle", "Reading list of assets...", 0f);
				var names = assetBundle.GetAllAssetNames();
				var n = names.Length;
				Debug.Log($"Found {n} names in {assetBundlePath}.", this);

				var loadAllRequest = assetBundle.LoadAllAssetsAsync();
				while (!loadAllRequest.isDone) {
					EditorUtility.DisplayProgressBar("Unpacking AssetBundle", "Reading list of assets...", loadAllRequest.progress);
					yield return loadAllRequest;
				}

				var objects = loadAllRequest.allAssets;

				for (var i = 0; i < objects.Length; ++i) {
					var name = objects[i].name;
					EditorUtility.DisplayProgressBar("Unpacking AssetBundle", $"Unpacking {name}... ({i}/{n})", (i + 1.0f) / (n + 1.0f));
					//	var asset = assetBundle.LoadAsset(name);
					var asset = objects[i];
					if (asset == null) {
						Debug.LogWarning($"Asset {name} (#{i}) was not loaded!.", this);
						continue;
					}
					AssetDatabase.CreateAsset(asset, $"{unpackFolder}/{name}_{asset.GetInstanceID()}.asset");
				}


			} finally {
				EditorUtility.ClearProgressBar();
			}




		}

	}

}
#endif
