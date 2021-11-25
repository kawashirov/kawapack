using System;
using System.IO;
using System.Text;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;
using Kawashirov.FLT;

using GUIL = UnityEngine.GUILayout;
using EGUIL = UnityEditor.EditorGUILayout;
using EU = UnityEditor.EditorUtility;
using KST = Kawashirov.ShaderTag;
using KSBC = Kawashirov.ShaderBaking.Commons;
using KFLTC = Kawashirov.FLT.Commons;
using SC = Kawashirov.KawaUtilities;

using static UnityEditor.EditorGUI;

namespace Kawashirov.FLT {
	public enum OutlineMode { Tinted, Colored }

	internal static partial class Commons {
		internal static readonly string F_Outline = "KawaFLT_Feature_Outline";
		internal static readonly string F_OutlineMode = "KawaFLT_Feature_OutlineMode";
	}

	public partial class Generator {
		public bool outline = false;
		public OutlineMode outlineMode = OutlineMode.Tinted;

		private void ConfigureFeatureOutline(ShaderSetup shader) {
			shader.TagBool(KFLTC.F_Outline, outline);
			if (outline) {
				shader.Define("OUTLINE_ON 1");
				shader.TagEnum(KFLTC.F_OutlineMode, outlineMode);
				if (outlineMode == OutlineMode.Colored) {
					shader.Define("OUTLINE_COLORED 1");
				} else if (outlineMode == OutlineMode.Tinted) {
					shader.Define("OUTLINE_TINTED 1");
				}
				shader.properties.Add(new PropertyFloat() { name = "_outline_width", defualt = 0.2f, range = new Vector2(0, 1) });
				shader.properties.Add(new PropertyColor() { name = "_outline_color", defualt = new Color(0.5f, 0.5f, 0.5f, 1) });
				shader.properties.Add(new PropertyFloat() { name = "_outline_bias", defualt = 0, range = new Vector2(-1, 5) });
			} else {
				shader.Define("OUTLINE_OFF 1");
			}
		}
	}

	public partial class GeneratorEditor {
		private static readonly GUIContent gui_feature_outline = new GUIContent("Outline Feature");

		private void OutlineGUI() {
			using (new DisabledScope(!complexity_VGF && !complexity_VHDGF)) {
				var outline = serializedObject.FindProperty("outline");
				KawaGUIUtilities.ToggleLeft(outline, gui_feature_outline);
				using (new DisabledScope(
					outline.hasMultipleDifferentValues || !outline.boolValue || (!complexity_VGF && !complexity_VHDGF)
				)) {
					using (new IndentLevelScope()) {
						KawaGUIUtilities.DefaultPrpertyField(this, "outlineMode", "Mode");
					}
				}
			}
		}
	}
}

internal partial class KawaFLTShaderGUI {
	private void OnGUI_Outline() {
		var _outline_width = FindProperty("_outline_width");
		var _outline_color = FindProperty("_outline_color");
		var _outline_bias = FindProperty("_outline_bias");

		var f_Outline = SC.AnyNotNull(_outline_width, _outline_color, _outline_bias);
		using (new DisabledScope(!f_Outline)) {
			EGUIL.LabelField("Outline Feature", f_Outline ? "Enabled" : "Disabled");
			using (new IndentLevelScope()) {
				if (f_Outline) {
					LabelEnumDisabledFromTagMixed<OutlineMode>("Mode", KFLTC.F_OutlineMode);
					ShaderPropertyDisabled(_outline_width, "Outline width (cm)");
					ShaderPropertyDisabled(_outline_color, "Outline Color (Tint)");
					ShaderPropertyDisabled(_outline_bias, "Outline Z-Bias");
				}
			}
		}
	}
}
