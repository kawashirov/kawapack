using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;

namespace Kawashirov.KawaShade {
	public class FeaturePSX : AbstractFeature {
		internal static readonly string F_PSX = "KawaShade_Feature_PSX";
		internal static readonly GUIContent gui_feature_psx = new GUIContent("PSX Feature");

		public override void PopulateShaderTags(List<string> tags) {
			tags.Add(F_PSX);
		}

		public override void ConfigureShader(KawaShadeGenerator generator, ShaderSetup shader) {
			IncludeFeatureDirect(shader, "kawa_feature_psx.cginc");

			shader.TagBool(F_PSX, generator.PSX);
			if (generator.PSX) {
				shader.Define("PSX_ON 1");
				shader.properties.Add(new PropertyFloat() { name = "_PSX_SnapScale", defualt = 1.0f, range = new Vector2(0.1f, 100.0f), power = 2.0f });
			} else {
				shader.Define("PSX_OFF 1");
			}
		}

		public override void GeneratorEditorGUI(KawaShadeGeneratorEditor editor) {
			var PSX = editor.serializedObject.FindProperty("PSX");
			KawaGUIUtility.ToggleLeft(PSX, gui_feature_psx);
		}

		public override void ShaderEditorGUI(KawaShadeGUI editor) {
			var _PSX_SnapScale = editor.FindProperty("_PSX_SnapScale");
			var f_PSX = KawaUtilities.AnyNotNull(_PSX_SnapScale);
			using (new EditorGUI.DisabledScope(!f_PSX)) {
				EditorGUILayout.LabelField("PSX Effect Feature", f_PSX ? "Enabled" : "Disabled");
				using (new EditorGUI.IndentLevelScope()) {
					if (f_PSX) {
						editor.ShaderPropertyDisabled(_PSX_SnapScale, "Pixel Snap Scale");
					}
				}
			}
		}
	}
	
	public partial class KawaShadeGenerator {
		public bool PSX = false;
	}
}
