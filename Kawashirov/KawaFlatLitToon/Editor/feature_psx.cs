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
using SC = Kawashirov.StaticCommons;

using static UnityEditor.EditorGUI;

namespace Kawashirov.FLT {
	internal static partial class Commons {
		internal static readonly string F_PSX = "KawaFLT_Feature_PSX";
	}

	public partial class Generator {
		public bool PSX = false;

		private void ConfigureFeaturePSX(ShaderSetup shader) {
			shader.TagBool(KFLTC.F_PSX, PSX);
			if (PSX) {
				shader.Define("PSX_ON 1");
				shader.properties.Add(new PropertyFloat() { name = "_PSX_SnapScale", defualt = 1.0f, range = new Vector2(0.1f, 100.0f), power = 2.0f });
			} else {
				shader.Define("PSX_OFF 1");
			}
		}
	}

	public partial class GeneratorEditor {
		private static readonly GUIContent gui_feature_psx = new GUIContent("PSX Feature");
		private void PSXGUI() {
			var PSX = serializedObject.FindProperty("PSX");
			ToggleLeft(PSX, gui_feature_psx);
		}
	}
}

internal partial class KawaFLTShaderGUI {
	protected void OnGUI_PSX() {
		// Commons.MaterialTagBoolCheck(this.target, Commons.KawaFLT_Feature_FPS);
		var _PSX_SnapScale = FindProperty("_PSX_SnapScale");
		var f_PSX = SC.AnyNotNull(_PSX_SnapScale);
		using (new DisabledScope(!f_PSX)) {
			EGUIL.LabelField("PSX Effect Feature", f_PSX ? "Enabled" : "Disabled");
			using (new IndentLevelScope()) {
				if (f_PSX) {
					ShaderPropertyDisabled(_PSX_SnapScale, "Pixel Snap Scale");
				}
			}
		}
	}
}
