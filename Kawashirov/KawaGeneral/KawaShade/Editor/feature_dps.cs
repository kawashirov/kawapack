using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;
using Serilog.Parsing;
using System.Collections.Generic;

namespace Kawashirov.KawaShade {
	public class FeaturePenetrationSystem : AbstractFeature {
		internal static readonly string F_PenetrationSystem = "KawaShade_Feature_PenetrationSystem";
		internal static readonly string F_PenetrationSystemMode = "KawaShade_Feature_PenetrationSystemMode";

		internal static readonly GUIContent gui_feature_penetration_system = new GUIContent("Penetration System Feature");

		public enum Mode { Orifice, Penetrator }

		public override void PopulateShaderTags(List<string> tags) {
			tags.Add(F_PenetrationSystem);
			tags.Add(F_PenetrationSystemMode);
		}

		public override void ConfigureShader(KawaShadeGenerator gen, ShaderSetup shader) {
			IncludeFeatureDirect(shader, "kawa_feature_dps.cginc");

			shader.TagBool(F_PenetrationSystem, gen.penetrationSystem);
			if (gen.penetrationSystem) {
				shader.Define("PENETRATION_SYSTEM_ON 1");
				shader.TagEnum(F_PenetrationSystemMode, gen.penetrationMode);
				shader.properties.Add(new PropertyFloat() { name = "_DPS_Toggle", defualt = 1f, range = Vector2.up });
				if (gen.penetrationMode == Mode.Orifice) {
					shader.Define("PENETRATION_SYSTEM_ORIFICE 1");
					shader.properties.Add(new Property2D() { name = "_OrificeData" });
					shader.properties.Add(new PropertyFloat() { name = "_EntryOpenDuration", defualt = 0.1f, range = Vector2.up });
					shader.properties.Add(new PropertyFloat() { name = "_Shape1Depth", defualt = 0.1f, range = Vector2.up * 5 });
					shader.properties.Add(new PropertyFloat() { name = "_Shape1Duration", defualt = 0.1f, range = Vector2.up });
					shader.properties.Add(new PropertyFloat() { name = "_Shape2Depth", defualt = 0.2f, range = Vector2.up * 5 });
					shader.properties.Add(new PropertyFloat() { name = "_Shape2Duration", defualt = 0.1f, range = Vector2.up });
					shader.properties.Add(new PropertyFloat() { name = "_Shape3Depth", defualt = 0.3f, range = Vector2.up * 5 });
					shader.properties.Add(new PropertyFloat() { name = "_Shape3Duration", defualt = 0.1f, range = Vector2.up });
					shader.properties.Add(new PropertyFloat() { name = "_BlendshapePower", defualt = 1, range = Vector2.up * 5 });
					shader.properties.Add(new PropertyFloat() { name = "_BlendshapeBadScaleFix", defualt = 1, range = Vector2.up * 100 });
				} else if (gen.penetrationMode == Mode.Penetrator) {
					shader.Define("PENETRATION_SYSTEM_PENETRATOR 1");
					shader.properties.Add(new PropertyFloat() { name = "_squeeze", defualt = 0, range = Vector2.up * 0.2f });
					shader.properties.Add(new PropertyFloat() { name = "_SqueezeDist", defualt = 0, range = Vector2.up * 0.1f });
					shader.properties.Add(new PropertyFloat() { name = "_BulgePower", defualt = 0, range = Vector2.up * 0.01f });
					shader.properties.Add(new PropertyFloat() { name = "_BulgeOffset", defualt = 0, range = Vector2.up * 0.3f });
					shader.properties.Add(new PropertyFloat() { name = "_Length", defualt = 0, range = Vector2.up * 0.3f });
					shader.properties.Add(new PropertyFloat() { name = "_EntranceStiffness", defualt = 0.01f, range = new Vector2(0.01f, 1f) });
					shader.properties.Add(new PropertyFloat() { name = "_Curvature", defualt = 0, range = new Vector2(-1, 1) });
					shader.properties.Add(new PropertyFloat() { name = "_ReCurvature", defualt = 0, range = new Vector2(-1, 1) });
					shader.properties.Add(new PropertyFloat() { name = "_Wriggle", defualt = 0, range = Vector2.up });
					shader.properties.Add(new PropertyFloat() { name = "_WriggleSpeed", defualt = 0.28f, range = new Vector2(0.1f, 30f) });
				}
				shader.properties.Add(new PropertyFloat() { name = "_OrificeChannel", defualt = 0 });
			} else {
				shader.Define("PENETRATION_SYSTEM_OFF 1");
			}
		}

		public override void GeneratorEditorGUI(KawaShadeGeneratorEditor editor) {
			var penetrationSystem = editor.serializedObject.FindProperty("penetrationSystem");
			KawaGUIUtility.ToggleLeft(penetrationSystem, gui_feature_penetration_system);
			using (new EditorGUI.DisabledScope(penetrationSystem.hasMultipleDifferentValues || !penetrationSystem.boolValue)) {
				using (new EditorGUI.IndentLevelScope()) {
					KawaGUIUtility.DefaultPrpertyField(editor, "penetrationMode", "Mode");
				}
			}
		}

		public override void ShaderEditorGUI(KawaShadeGUI editor) {
			var _DPS_Toggle = editor.FindProperty("_DPS_Toggle");

			var _OrificeData = editor.FindProperty("_OrificeData");
			var _EntryOpenDuration = editor.FindProperty("_EntryOpenDuration");
			var _Shape1Depth = editor.FindProperty("_Shape1Depth");
			var _Shape1Duration = editor.FindProperty("_Shape1Duration");
			var _Shape2Depth = editor.FindProperty("_Shape2Depth");
			var _Shape2Duration = editor.FindProperty("_Shape2Duration");
			var _Shape3Depth = editor.FindProperty("_Shape3Depth");
			var _Shape3Duration = editor.FindProperty("_Shape3Duration");
			var _BlendshapePower = editor.FindProperty("_BlendshapePower");
			var _BlendshapeBadScaleFix = editor.FindProperty("_BlendshapeBadScaleFix");

			var _squeeze = editor.FindProperty("_squeeze");
			var _SqueezeDist = editor.FindProperty("_SqueezeDist");
			var _BulgePower = editor.FindProperty("_BulgePower");
			var _BulgeOffset = editor.FindProperty("_BulgeOffset");
			var _Length = editor.FindProperty("_Length");
			var _EntranceStiffness = editor.FindProperty("_EntranceStiffness");
			var _Curvature = editor.FindProperty("_Curvature");
			var _ReCurvature = editor.FindProperty("_ReCurvature");
			var _Wriggle = editor.FindProperty("_Wriggle");
			var _WriggleSpeed = editor.FindProperty("_WriggleSpeed");

			var _OrificeChannel = editor.FindProperty("_OrificeChannel");

			var f_DPS_Oriface = KawaUtilities.AnyNotNull(
				_OrificeData, _EntryOpenDuration, _Shape1Depth, _Shape1Duration, _Shape2Depth,
				_Shape2Duration, _Shape3Depth, _Shape3Duration, _BlendshapePower, _BlendshapeBadScaleFix
			);
			var f_DPS_Penetrator = KawaUtilities.AnyNotNull(
				_squeeze, _SqueezeDist, _BulgePower, _BulgeOffset, _Length,
				_EntranceStiffness, _Curvature, _ReCurvature, _Wriggle, _WriggleSpeed
			);
			var f_DPS = f_DPS_Oriface || f_DPS_Penetrator || KawaUtilities.AnyNotNull(_DPS_Toggle, _OrificeChannel);
			using (new EditorGUI.DisabledScope(!f_DPS)) {
				EditorGUILayout.LabelField("Penetration System Feature", f_DPS ? "Enabled" : "Disabled");
				using (new EditorGUI.IndentLevelScope()) {
					if (f_DPS) {
						editor.ShaderPropertyDisabled(_DPS_Toggle, "Dynamic Feature Toggle");
					}
					if (f_DPS_Oriface) {
						editor.TexturePropertySingleLineDisabled(new GUIContent("OrificeData"), _OrificeData);
						editor.ShaderPropertyDisabled(_EntryOpenDuration, "Entry Trigger Duration");
						editor.ShaderPropertyDisabled(_Shape1Depth, "Shape 1 Trigger Depth");
						editor.ShaderPropertyDisabled(_Shape1Duration, "Shape 1 Trigger Duration");
						editor.ShaderPropertyDisabled(_Shape2Depth, "Shape 2 Trigger Depth");
						editor.ShaderPropertyDisabled(_Shape2Duration, "Shape 2 Trigger Duration");
						editor.ShaderPropertyDisabled(_Shape3Depth, "Shape 2 Trigger Depth");
						editor.ShaderPropertyDisabled(_Shape3Duration, "Shape 2 Trigger Duration");
						editor.ShaderPropertyDisabled(_BlendshapePower, "Blend Shape Power");
						editor.ShaderPropertyDisabled(_BlendshapeBadScaleFix, "Blend Shape Bad Scale Fix");
					}
					if (f_DPS_Penetrator) {
						editor.ShaderPropertyDisabled(_squeeze, "Squeeze Minimum Size");
						editor.ShaderPropertyDisabled(_SqueezeDist, "Squeeze Smoothness");
						editor.ShaderPropertyDisabled(_BulgePower, "Bulge Amount");
						editor.ShaderPropertyDisabled(_BulgeOffset, "Bulge Length");
						editor.ShaderPropertyDisabled(_Length, "Length of Penetrator Model");
						editor.ShaderPropertyDisabled(_EntranceStiffness, "Entrance Stiffness");
						editor.ShaderPropertyDisabled(_Curvature, "Curvature");
						editor.ShaderPropertyDisabled(_ReCurvature, "ReCurvature");
						editor.ShaderPropertyDisabled(_Wriggle, "Wriggle Amount");
						editor.ShaderPropertyDisabled(_WriggleSpeed, "Wriggle Speed");
					}
					if (f_DPS) {
						editor.ShaderPropertyDisabled(_OrificeChannel, "Orifice Channel (Please Use 0)");
					}
				}
			}
		}
	}

	public partial class KawaShadeGenerator {
		public bool penetrationSystem = false;
		public FeaturePenetrationSystem.Mode penetrationMode = FeaturePenetrationSystem.Mode.Orifice;
	}
}
