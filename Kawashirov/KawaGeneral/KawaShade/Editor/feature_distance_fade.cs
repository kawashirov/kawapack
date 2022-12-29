using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;
using System.Collections.Generic;

namespace Kawashirov.KawaShade {
	public class FeatureDistanceFade : AbstractFeature {
		internal static readonly string F_DistanceFade = "KawaShade_Feature_DistanceFade";
		internal static readonly string F_DistanceFadeMode = "KawaShade_Feature_DistanceFadeMode";

		internal static readonly GUIContent gui_feature_dstfd = new GUIContent("Distance Dithering Fade Feature");

		public enum Mode { Range, Infinity }

		public override void PopulateShaderTags(List<string> tags) {
			tags.Add(F_DistanceFade);
			tags.Add(F_DistanceFadeMode);
		}

		public override void ConfigureShader(KawaShadeGenerator gen, ShaderSetup shader) {
			IncludeFeatureDirect(shader, "kawa_feature_distance_fade.cginc");


			shader.TagBool(F_DistanceFade, gen.distanceFade);
			if (gen.distanceFade) {
				shader.Define("DSTFD_ON 1");
				gen.needRandomFrag = true;
				shader.TagEnum(F_DistanceFadeMode, gen.distanceFadeMode);
				switch (gen.distanceFadeMode) {
					case Mode.Range:
						shader.Define("DSTFD_RANGE 1");
						break;
					case Mode.Infinity:
						shader.Define("DSTFD_INFINITY 1");
						break;
				}
				shader.properties.Add(new PropertyVector() { name = "_DstFd_Axis", defualt = Vector4.one });
				shader.properties.Add(new PropertyFloat() { name = "_DstFd_Near", defualt = 1.0f });
				shader.properties.Add(new PropertyFloat() { name = "_DstFd_Far", defualt = 2.0f });
				shader.properties.Add(new PropertyFloat() { name = "_DstFd_AdjustPower", defualt = 1.0f, range = new Vector2(0.1f, 10), power = 10 });
				shader.properties.Add(new PropertyFloat() { name = "_DstFd_AdjustScale", defualt = 1.0f, range = new Vector2(0.1f, 10) });
			} else {
				shader.Define("DSTFD_OFF 1");
			}
		}

		public override void GeneratorEditorGUI(KawaShadeGeneratorEditor editor) {
			var distanceFade = editor.serializedObject.FindProperty("distanceFade");
			KawaGUIUtility.ToggleLeft(distanceFade, gui_feature_dstfd);
			using (new EditorGUI.DisabledScope(distanceFade.hasMultipleDifferentValues || !distanceFade.boolValue)) {
				using (new EditorGUI.IndentLevelScope()) {
					KawaGUIUtility.DefaultPrpertyField(editor, "distanceFadeMode", "Mode");
				}
			}
		}

		public override void ShaderEditorGUI(KawaShadeGUI editor) {
			var _DstFd_Axis = editor.FindProperty("_DstFd_Axis");
			var _DstFd_Near = editor.FindProperty("_DstFd_Near");
			var _DstFd_Far = editor.FindProperty("_DstFd_Far");
			var _DstFd_AdjustPower = editor.FindProperty("_DstFd_AdjustPower");
			var _DstFd_AdjustScale = editor.FindProperty("_DstFd_AdjustScale");
			var f_distanceFade = KawaUtilities.AnyNotNull(_DstFd_Axis, _DstFd_Near, _DstFd_Far, _DstFd_AdjustPower, _DstFd_AdjustScale);
			using (new EditorGUI.DisabledScope(!f_distanceFade)) {
				EditorGUILayout.LabelField("Distance Fade Feature", f_distanceFade ? "Enabled" : "Disabled");
				using (new EditorGUI.IndentLevelScope()) {
					if (f_distanceFade) {
						editor.LabelEnumDisabledFromTagMixed<Mode>("Mode", F_DistanceFadeMode);
						editor.ShaderPropertyDisabled(_DstFd_Axis, "Axis weights");
						editor.ShaderPropertyDisabled(_DstFd_Near, "Near Distance");
						editor.ShaderPropertyDisabled(_DstFd_Far, "Far Distance");
						editor.ShaderPropertyDisabled(_DstFd_AdjustPower, "Power Adjust");
						editor.ShaderPropertyDisabled(_DstFd_AdjustScale, "Scale Adjust");
					}
				}
			}
		}
	}

	public partial class KawaShadeGenerator {
		public bool distanceFade = false;
		public FeatureDistanceFade.Mode distanceFadeMode = FeatureDistanceFade.Mode.Range;
	}
}

