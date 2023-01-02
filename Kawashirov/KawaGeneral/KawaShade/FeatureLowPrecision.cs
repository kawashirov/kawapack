using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;

namespace Kawashirov.KawaShade {
	public class FeatureLowPrecision : AbstractFeature {
		internal static readonly string F_PSX = "KawaShade_Feature_PSX";
		internal static readonly string F_PrecLoss = "KawaShade_Feature_PrecLoss";

		internal static readonly GUIContent gui_feature_psx = new GUIContent("PSX Feature");
		internal static readonly GUIContent gui_feature_PrecLoss = new GUIContent("Precision Loss Feature");

		internal static readonly GUIContent gui_prop_PSX_SnapScale = new GUIContent("PSX Pixel Snap");
		internal static readonly GUIContent gui_prop_PrecLoss =
			new GUIContent("FP Mantissa Loss", "Nuber of bits lost in mantissa part of IEEE 754 32-bit floating point");

		public override int GetOrder() => (int)Order.LIBRARY + 100;

		public override void PopulateShaderTags(List<string> tags) {
			tags.Add(F_PSX);
			tags.Add(F_PrecLoss);
		}

		public override void ConfigureShader(KawaShadeGenerator generator, ShaderSetup shader) {
			var path = KawaShadeGenerator.GetCGIncPath("FeatureLowPrecision.hlsl");
			shader.Include(ShaderInclude.Direct((int)KawaShadeGenerator.IncludeOrders.LIBRARY + 10, path));

			shader.TagBool(F_PSX, generator.PSX);
			if (generator.PSX) {
				shader.Define("PSX_ON 1");
				shader.properties.Add(new PropertyFloat() { name = "_PSX_SnapScale", defualt = 1.0f, range = new Vector2(0.1f, 100.0f), power = 2.0f });
			} else {
				shader.Define("PSX_OFF 1");
			}

			shader.TagBool(F_PrecLoss, generator.PrecLoss);
			if (generator.PrecLoss) {
				shader.Define("PRECISION_LOSS_ON 1");
				shader.properties.Add(new PropertyFloat() { name = "_PrecLoss", defualt = 13, range = new Vector2(0, 23) });
			} else {
				shader.Define("PRECISION_LOSS_OFF 1");
			}
		}

		public override void GeneratorEditorGUI(KawaShadeGeneratorEditor editor) {
			var PSX = editor.serializedObject.FindProperty("PSX");
			KawaGUIUtility.ToggleLeft(PSX, gui_feature_psx);

			EditorGUILayout.Space();

			var PrecLoss = editor.serializedObject.FindProperty("PrecLoss");
			KawaGUIUtility.ToggleLeft(PrecLoss, gui_feature_PrecLoss);
		}

		public override void ShaderEditorGUI(KawaShadeGUI editor) {
			var _PSX_SnapScale = editor.FindProperty("_PSX_SnapScale");
			var _PrecLoss = editor.FindProperty("_PrecLoss");

			var f_precision = KawaUtilities.AnyNotNull(_PSX_SnapScale, _PrecLoss);
			using (new EditorGUI.DisabledScope(!f_precision)) {
				EditorGUILayout.LabelField("Precision Features", f_precision ? "Enabled" : "Disabled");
				using (new EditorGUI.IndentLevelScope()) {
					if (f_precision) {
						editor.ShaderPropertyDisabled(_PSX_SnapScale, gui_prop_PSX_SnapScale);
						editor.ShaderPropertyDisabled(_PrecLoss, gui_prop_PrecLoss);
					}
				}
			}
		}
	}

	public partial class KawaShadeGenerator {
		public bool PSX = false;
		public bool PrecLoss = false;
	}
}
