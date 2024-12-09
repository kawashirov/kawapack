#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Kawashirov {
	public static class AssetUtility {

		private static readonly HashSet<string> GetSelectedAssetPaths_Folders = new HashSet<string>();
		private static readonly HashSet<string> GetSelectedAssetPaths_Paths = new HashSet<string>();
		private static readonly string[] GetSelectedAssetPaths_Empty = new string[0];
		public static string[] GetSelectedAssetPaths() {
			var assets = Selection.GetFiltered(typeof(Object), SelectionMode.Assets);
			if (assets.Length < 1)
				return GetSelectedAssetPaths_Empty;
			GetSelectedAssetPaths_Paths.Clear();
			GetSelectedAssetPaths_Folders.Clear();
			foreach (var asset in assets) {
				var assetPath = AssetDatabase.GetAssetPath(asset);
				if (AssetDatabase.IsValidFolder(assetPath)) {
					GetSelectedAssetPaths_Folders.Add(assetPath);
				} else {
					GetSelectedAssetPaths_Paths.Add(assetPath);
				}
			}
			if (GetSelectedAssetPaths_Folders.Count > 0) {
				var pathsFolders = AssetDatabase.FindAssets("", GetSelectedAssetPaths_Folders.ToArray()).Select(AssetDatabase.GUIDToAssetPath);
				GetSelectedAssetPaths_Paths.UnionWith(pathsFolders);
			}
			return GetSelectedAssetPaths_Paths.ToArray();
		}

		public static string[] FindAllGameObjectAssetPaths()
			=> AssetDatabase.FindAssets("t:GameObject").Select(AssetDatabase.GUIDToAssetPath).ToArray();

	}
}
#endif // UNITY_EDITOR
