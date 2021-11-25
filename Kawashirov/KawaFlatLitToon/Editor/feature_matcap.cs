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
	public enum MatcapMode { Replace, Multiply, Add }

	internal static partial class Commons {
		internal static readonly string F_Matcap = "KawaFLT_Feature_Matcap";
		internal static readonly string F_MatcapMode = "KawaFLT_Feature_MatcapMode";
		internal static readonly string F_MatcapKeepUp = "KawaFLT_Feature_MatcapKeepUp";
	}

	public partial class Generator {
		public bool matcap = false;
		public MatcapMode matcapMode = MatcapMode.Multiply;
		public bool matcapKeepUp = true;

		private void ConfigureFeatureMatcap(ShaderSetup shader) {
			shader.TagBool(KFLTC.F_Matcap, matcap);
			if (matcap) {
				shader.Define("MATCAP_ON 1");
				// needRandomFrag = true;
				shader.TagEnum(KFLTC.F_MatcapMode, matcapMode);
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
				shader.TagBool(KFLTC.F_MatcapKeepUp, matcapKeepUp);
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

	public partial class GeneratorEditor {
		private static readonly GUIContent gui_feature_matcap = new GUIContent("Matcap Feature");

		private void MatcapGUI() {
			var matcap = serializedObject.FindProperty("matcap");
			KawaGUIUtilities.ToggleLeft(matcap, gui_feature_matcap);
			using (new DisabledScope(matcap.hasMultipleDifferentValues || !matcap.boolValue)) {
				using (new IndentLevelScope()) {
					KawaGUIUtilities.DefaultPrpertyField(this, "matcapMode", "Mode");
					KawaGUIUtilities.DefaultPrpertyField(this, "matcapKeepUp", "Keep Upward Direction");
				}
			}
		}
	}
}

internal partial class KawaFLTShaderGUI {
	protected void OnGUI_MatCap() {
		var _MatCap = FindProperty("_MatCap");
		var _MatCap_Scale = FindProperty("_MatCap_Scale");
		var f_matCap = SC.AnyNotNull(_MatCap, _MatCap_Scale);
		using (new DisabledScope(!f_matCap)) {
			EGUIL.LabelField("MatCap Feature", f_matCap ? "Enabled" : "Disabled");
			using (new IndentLevelScope()) {
				if (f_matCap) {
					LabelEnumDisabledFromTagMixed<DistanceFadeMode>("Mode", KFLTC.F_MatcapMode);
					// TODO KeepUp bool label
					ShaderPropertyDisabled(_MatCap, "MatCap Texture");
					ShaderPropertyDisabled(_MatCap_Scale, "MatCap Power");
				}
			}
		}
	}
}
