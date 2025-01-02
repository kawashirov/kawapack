#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace Kawashirov {
	public static class Internals {
		private const string KawaIconGUID = "302691306fd300648a26254d75364f60";
		private const string KawaFileIconGUID = "a184db8c1b83f2e458273d59d80ae006";
		private static readonly string[] PackagesPaths = new string[] {
			"Packages/kawashirov.commons",
			"Packages/kawashirov.vrc.worlds",
			"Packages/kawashirov.vrc.avatars"
		};

		private static readonly BuildTargetGroup[] BuildTargets = new BuildTargetGroup[]{
			BuildTargetGroup.Standalone, BuildTargetGroup.Android
		};
		private static readonly string[] DebugDefines = new string[] {
			"KAWA_DEBUG", "UNITY_ASSERTIONS"
		};


		public static bool DefinesDiffers(string[] old_defines, string[] new_defines) {
			if (old_defines.Length == new_defines.Length &&
				old_defines.Zip(new_defines, (x, y) => string.Equals(x, y)).All(x => x))
				return false;
			var old_s = string.Join("\n", old_defines);
			var new_s = string.Join("\n", old_defines);
			Debug.LogWarning($"Changing defines from:\n\n{old_s}\n\nto:\n\n{new_s}");
			return true;
		}

		[MenuItem("Kawashirov/Internals/Enable Debug")]
		public static void EnableDebug() {
			foreach (var target in BuildTargets) {
				PlayerSettings.GetScriptingDefineSymbolsForGroup(target, out var old_defines);
				var new_defines = old_defines.Concat(DebugDefines).Distinct().ToArray();
				if (DefinesDiffers(old_defines, new_defines))
					PlayerSettings.SetScriptingDefineSymbolsForGroup(target, new_defines);
			}
		}

		[MenuItem("Kawashirov/Internals/Disable Debug")]
		public static void DisableDebug() {
			foreach (var target in BuildTargets) {
				PlayerSettings.GetScriptingDefineSymbolsForGroup(target, out var old_defines);
				var new_defines = old_defines.Except(DebugDefines).Distinct().ToArray();
				if (DefinesDiffers(old_defines, new_defines)) {
					PlayerSettings.SetScriptingDefineSymbolsForGroup(target, new_defines);
				}
			}
		}

		public static bool IsUnityObjectClass(MonoImporter importer) {
			var script = importer.GetScript();
			if (script == null)
				return false;

			var clazz = script.GetClass();
			if (clazz == null)
				return false;

			if (clazz.IsAbstract)
				return false;

			if (clazz.IsGenericType)
				return false;

			return typeof(Object).IsAssignableFrom(clazz);
		}

		public static Texture2D LoadIcon(string guid, string name) {
			var icon_path = AssetDatabase.GUIDToAssetPath(guid);
			var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(icon_path);
			if (icon == null) {
				Debug.LogError($"No {name}: GUID={guid}, Path={icon_path}, Icon={icon}");
				return null;
			}
			return icon;
		}

#if KAWA_DEBUG
		[MenuItem("Kawashirov/Internals/Reapply Icons")]
#endif
		public static void ReapplyIcons() {
			var icon_general = LoadIcon(KawaIconGUID, nameof(KawaIconGUID));
			var icon_file = LoadIcon(KawaFileIconGUID, nameof(KawaFileIconGUID));

			try {
				AssetDatabase.StartAssetEditing();
				var guids = AssetDatabase.FindAssets("", PackagesPaths);
				Debug.LogWarning($"Found {guids.Length} GUIDs...");
				for (var i = 0; i < guids.Length; ++i) {
					var guid = guids[i];
					var asset_path = AssetDatabase.GUIDToAssetPath(guid);
					var importer = AssetImporter.GetAtPath(asset_path);
					if (importer is MonoImporter mono_importer) {
						var icon = IsUnityObjectClass(mono_importer) ? icon_general : icon_file;
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