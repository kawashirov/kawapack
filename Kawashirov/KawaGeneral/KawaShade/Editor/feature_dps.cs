using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;
using Serilog.Parsing;

namespace Kawashirov.KawaShade {
	public enum PenetrationSystemMode { Orifice, Penetrator }

	internal static partial class KawaShadeCommons {
		internal static readonly string F_PenetrationSystem = "KawaShade_Feature_PenetrationSystem";
		internal static readonly string F_PenetrationSystemMode = "KawaShade_Feature_PenetrationSystemMode";
	}

	public partial class KawaShadeGenerator {
		public bool penetrationSystem = false;
		public PenetrationSystemMode penetrationMode = PenetrationSystemMode.Orifice;

		private void ConfigureFeaturePenetrationSystem(ShaderSetup shader) {
			shader.TagBool(KawaShadeCommons.F_PenetrationSystem, penetrationSystem);
			if (penetrationSystem) {
				shader.Define("PENETRATION_SYSTEM_ON 1");
				shader.TagEnum(KawaShadeCommons.F_PenetrationSystemMode, penetrationMode);
				shader.properties.Add(new PropertyFloat() { name = "_DPS_Toggle", defualt = 1f, range = Vector2.up });
				if (penetrationMode == PenetrationSystemMode.Orifice) {
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
				} else if (penetrationMode == PenetrationSystemMode.Penetrator) {
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
	}

	public partial class KawaShadeGeneratorEditor {
		private static readonly GUIContent gui_feature_penetration_system = new GUIContent("Penetration System Feature");
		private void PenetrationSystemGUI() {
			var penetrationSystem = serializedObject.FindProperty("penetrationSystem");
			KawaGUIUtility.ToggleLeft(penetrationSystem, gui_feature_penetration_system);
			using (new EditorGUI.DisabledScope(penetrationSystem.hasMultipleDifferentValues || !penetrationSystem.boolValue)) {
				using (new EditorGUI.IndentLevelScope()) {
					KawaGUIUtility.DefaultPrpertyField(this, "penetrationMode", "Mode");
				}
			}
		}
	}

	internal partial class KawaShadeGUI {
		protected void OnGUI_PenetrationSystem() {
			var _DPS_Toggle = FindProperty("_DPS_Toggle");

			var _OrificeData = FindProperty("_OrificeData");
			var _EntryOpenDuration = FindProperty("_EntryOpenDuration");
			var _Shape1Depth = FindProperty("_Shape1Depth");
			var _Shape1Duration = FindProperty("_Shape1Duration");
			var _Shape2Depth = FindProperty("_Shape2Depth");
			var _Shape2Duration = FindProperty("_Shape2Duration");
			var _Shape3Depth = FindProperty("_Shape3Depth");
			var _Shape3Duration = FindProperty("_Shape3Duration");
			var _BlendshapePower = FindProperty("_BlendshapePower");
			var _BlendshapeBadScaleFix = FindProperty("_BlendshapeBadScaleFix");

			var _squeeze = FindProperty("_squeeze");
			var _SqueezeDist = FindProperty("_SqueezeDist");
			var _BulgePower = FindProperty("_BulgePower");
			var _BulgeOffset = FindProperty("_BulgeOffset");
			var _Length = FindProperty("_Length");
			var _EntranceStiffness = FindProperty("_EntranceStiffness");
			var _Curvature = FindProperty("_Curvature");
			var _ReCurvature = FindProperty("_ReCurvature");
			var _Wriggle = FindProperty("_Wriggle");
			var _WriggleSpeed = FindProperty("_WriggleSpeed");

			var _OrificeChannel = FindProperty("_OrificeChannel");

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
						ShaderPropertyDisabled(_DPS_Toggle, "Dynamic Feature Toggle");
					}
					if (f_DPS_Oriface) {
						TexturePropertySingleLineDisabled(new GUIContent("OrificeData"), _OrificeData);
						ShaderPropertyDisabled(_EntryOpenDuration, "Entry Trigger Duration");
						ShaderPropertyDisabled(_Shape1Depth, "Shape 1 Trigger Depth");
						ShaderPropertyDisabled(_Shape1Duration, "Shape 1 Trigger Duration");
						ShaderPropertyDisabled(_Shape2Depth, "Shape 2 Trigger Depth");
						ShaderPropertyDisabled(_Shape2Duration, "Shape 2 Trigger Duration");
						ShaderPropertyDisabled(_Shape3Depth, "Shape 2 Trigger Depth");
						ShaderPropertyDisabled(_Shape3Duration, "Shape 2 Trigger Duration");
						ShaderPropertyDisabled(_BlendshapePower, "Blend Shape Power");
						ShaderPropertyDisabled(_BlendshapeBadScaleFix, "Blend Shape Bad Scale Fix");
					}
					if (f_DPS_Penetrator) {
						ShaderPropertyDisabled(_squeeze, "Squeeze Minimum Size");
						ShaderPropertyDisabled(_SqueezeDist, "Squeeze Smoothness");
						ShaderPropertyDisabled(_BulgePower, "Bulge Amount");
						ShaderPropertyDisabled(_BulgeOffset, "Bulge Length");
						ShaderPropertyDisabled(_Length, "Length of Penetrator Model");
						ShaderPropertyDisabled(_EntranceStiffness, "Entrance Stiffness");
						ShaderPropertyDisabled(_Curvature, "Curvature");
						ShaderPropertyDisabled(_ReCurvature, "ReCurvature");
						ShaderPropertyDisabled(_Wriggle, "Wriggle Amount");
						ShaderPropertyDisabled(_WriggleSpeed, "Wriggle Speed");
					}
					if (f_DPS) {
						ShaderPropertyDisabled(_OrificeChannel, "Orifice Channel (Please Use 0)");
					}
				}
			}
		}
	}
}
