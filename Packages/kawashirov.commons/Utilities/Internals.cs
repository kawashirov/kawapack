#if UNITY_EDITOR && KAWA_DEBUG
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.CodeDom;
using System.Linq;

namespace Kawashirov {
	public static class Internals {
		private const string KawaIconGUID = "302691306fd300648a26254d75364f60";
		private static readonly string[] PackagesPaths = new string[] {
			"Packages/kawashirov.commons",
			"Packages/kawashirov.vrc.worlds",
			"Packages/kawashirov.vrc.avatars"
		};

		[MenuItem("Kawashirov/Internals/Reapply Icons")]
		public static void ReapplyIcons() {
			var icon_path = AssetDatabase.GUIDToAssetPath(KawaIconGUID);
			var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(icon_path);
			if (icon == null) {
				Debug.LogError($"No {nameof(KawaIconGUID)}: GUID={KawaIconGUID}, Path={icon_path}, Icon={icon}");
				return;
			}

			try {
				AssetDatabase.StartAssetEditing();
				var guids = AssetDatabase.FindAssets("", PackagesPaths);
				Debug.LogWarning($"Found {guids.Length} GUIDs...");
				for (var i = 0; i < guids.Length; ++i) {
					var guid = guids[i];
					var asset_path = AssetDatabase.GUIDToAssetPath(guid);
					var importer = AssetImporter.GetAtPath(asset_path);
					if (importer is MonoImporter mono_importer) {
						var script_icon = mono_importer.GetIcon();
						if (script_icon != icon) {
							Debug.LogWarning($"Updating icon at {asset_path}: {script_icon} -> {icon}");
							mono_importer.SetIcon(icon);
							mono_importer.SaveAndReimport();
						}
					}
					var asset = AssetDatabase.LoadMainAssetAtPath(asset_path);
					var labels = AssetDatabase.GetLabels(asset);
					if (!labels.Contains("Kawashirov")) {
						var new_labels = labels.Append("Kawashirov").ToArray();
						Debug.LogWarning($"Adding label on {asset_path}: {new_labels}");
						AssetDatabase.SetLabels(asset, new_labels);
					}
				}
			} finally {
				AssetDatabase.StopAssetEditing();
			}
			AssetDatabase.Refresh();
			Debug.Log($"Done.");
		}

	}
}
#endif