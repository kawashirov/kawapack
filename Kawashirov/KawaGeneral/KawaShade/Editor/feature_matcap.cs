using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;

namespace Kawashirov.KawaShade {
	public enum MatcapMode { Replace, Multiply, Add }

	internal static partial class KawaShadeCommons {
		internal static readonly string F_Matcap = "KawaShade_Feature_Matcap";
		internal static readonly string F_MatcapMode = "KawaShade_Feature_MatcapMode";
		internal static readonly string F_MatcapKeepUp = "KawaShade_Feature_MatcapKeepUp";
	}

	public partial class KawaShadeGenerator {
		public bool matcap = false;
		public MatcapMode matcapMode = MatcapMode.Multiply;
		public bool matcapKeepUp = true;

		private void ConfigureFeatureMatcap(ShaderSetup shader) {
			shader.TagBool(KawaShadeCommons.F_Matcap, matcap);
			if (matcap) {
				shader.Define("MATCAP_ON 1");
				// needRandomFrag = true;
				shader.TagEnum(KawaShadeCommons.F_MatcapMode, matcapMode);
				switch (matcapMode) {
					case MatcapMode.Replace:
						shader.Define("MATCAP_REPLACE 1");
						break;
					case MatcapMode.Multiply:
						shader.Define("MATCAP_MULTIPLY 1");
						break;
					case MatcapMode.Add:
						shader.Define("MATCAP_ADD 1");
						break;
				}
				shader.TagBool(KawaShadeCommons.F_MatcapKeepUp, matcapKeepUp);
				if (matcapKeepUp) {
					shader.Define("MATCAP_KEEPUP 1");
				}
				shader.properties.Add(new Property2D() { name = "_MatCap", defualt = "white" });
				shader.properties.Add(new PropertyFloat() { name = "_MatCap_Scale", defualt = 1f, range = new Vector2(0, 1) });
			} else {
				shader.Define("MATCAP_OFF 1");
			}
		}
	}

	public partial class KawaShadeGeneratorEditor {
		private static readonly GUIContent gui_feature_matcap = new GUIContent("Matcap Feature");

		private void MatcapGUI() {
			var matcap = serializedObject.FindProperty("matcap");
			KawaGUIUtility.ToggleLeft(matcap, gui_feature_matcap);
			using (new EditorGUI.DisabledScope(matcap.hasMultipleDifferentValues || !matcap.boolValue)) {
				using (new EditorGUI.IndentLevelScope()) {
					KawaGUIUtility.DefaultPrpertyField(this, "matcapMode", "Mode");
					KawaGUIUtility.DefaultPrpertyField(this, "matcapKeepUp", "Keep Upward Direction");
				}
			}
		}
	}

	internal partial class KawaShadeGUI {
		protected void OnGUI_MatCap() {
			var _MatCap = FindProperty("_MatCap");
			var _MatCap_Scale = FindProperty("_MatCap_Scale");
			var f_matCap = KawaUtilities.AnyNotNull(_MatCap, _MatCap_Scale);
			using (new EditorGUI.DisabledScope(!f_matCap)) {
				EditorGUILayout.LabelField("MatCap Feature", f_matCap ? "Enabled" : "Disabled");
				using (new EditorGUI.IndentLevelScope()) {
					if (f_matCap) {
						LabelEnumDisabledFromTagMixed<DistanceFadeMode>("Mode", KawaShadeCommons.F_MatcapMode);
						// TODO KeepUp bool label
						ShaderPropertyDisabled(_MatCap, "MatCap Texture");
						ShaderPropertyDisabled(_MatCap_Scale, "MatCap Power");
					}
				}
			}
		}
	}
}
