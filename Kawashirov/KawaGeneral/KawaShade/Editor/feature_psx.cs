using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;

namespace Kawashirov.KawaShade {
	internal static partial class KawaShadeCommons {
		internal static readonly string F_PSX = "KawaShade_Feature_PSX";
	}

	public partial class KawaShadeGenerator {
		public bool PSX = false;

		private void ConfigureFeaturePSX(ShaderSetup shader) {
			shader.TagBool(KawaShadeCommons.F_PSX, PSX);
			if (PSX) {
				shader.Define("PSX_ON 1");
				shader.properties.Add(new PropertyFloat() { name = "_PSX_SnapScale", defualt = 1.0f, range = new Vector2(0.1f, 100.0f), power = 2.0f });
			} else {
				shader.Define("PSX_OFF 1");
			}
		}
	}

	public partial class KawaShadeGeneratorEditor {
		private static readonly GUIContent gui_feature_psx = new GUIContent("PSX Feature");
		private void PSXGUI() {
			var PSX = serializedObject.FindProperty("PSX");
			KawaGUIUtility.ToggleLeft(PSX, gui_feature_psx);
		}
	}

	internal partial class KawaShadeGUI {
		protected void OnGUI_PSX() {
			// KawaShadeCommons.MaterialTagBoolCheck(this.target, KawaShadeCommons.KawaFLT_Feature_FPS);
			var _PSX_SnapScale = FindProperty("_PSX_SnapScale");
			var f_PSX = KawaUtilities.AnyNotNull(_PSX_SnapScale);
			using (new EditorGUI.DisabledScope(!f_PSX)) {
				EditorGUILayout.LabelField("PSX Effect Feature", f_PSX ? "Enabled" : "Disabled");
				using (new EditorGUI.IndentLevelScope()) {
					if (f_PSX) {
						ShaderPropertyDisabled(_PSX_SnapScale, "Pixel Snap Scale");
					}
				}
			}
		}
	}
}
