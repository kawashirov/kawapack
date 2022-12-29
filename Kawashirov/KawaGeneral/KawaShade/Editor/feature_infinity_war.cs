using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;
using System.Collections.Generic;

namespace Kawashirov.KawaShade {
	public class FeatureInfinityWarDecimation : AbstractFeature {
		internal static readonly string ShaderTag_IWD = "KawaShade_Feature_IWD";
		internal static readonly string ShaderTag_IWDDirections = "KawaShade_Feature_IWDDirections";

		private static readonly GUIContent gui_feature_iwd = new GUIContent("Infinity War Decimation Feature");

		[System.Flags]
		public enum Directions {
			Plane = 1,
			Random = 2,
			Normal = 4,
			ObjectVector = 8,
			WorldVector = 16,
		}

		public override void PopulateShaderTags(List<string> tags) {
			tags.Add(ShaderTag_IWD);
			tags.Add(ShaderTag_IWDDirections);
		}

		public override void ConfigureShader(KawaShadeGenerator gen, ShaderSetup shader) {
			IncludeFeatureDirect(shader, "kawa_feature_infinity_war.cginc");

			var complexity = (gen.complexity == ShaderComplexity.VGF || gen.complexity == ShaderComplexity.VHDGF);
			shader.TagBool(ShaderTag_IWD, gen.iwd);
			if (gen.iwd && complexity) {
				gen.needRandomVert = true;
				shader.Define("IWD_ON 1");
				shader.properties.Add(new PropertyVector() { name = "_IWD_Plane", defualt = Vector4.zero });
				shader.properties.Add(new PropertyFloat() { name = "_IWD_PlaneDistRandomness", defualt = 0, range = new Vector2(0, 10), power = 3 });
				shader.properties.Add(new PropertyFloat() { name = "_IWD_DirRandomWeight", defualt = 0.5f, range = new Vector2(0, 10), power = 3 });
				shader.properties.Add(new PropertyFloat() { name = "_IWD_DirPlaneWeight", defualt = 0.5f, range = new Vector2(0, 10), power = 3 });
				shader.properties.Add(new PropertyFloat() { name = "_IWD_DirNormalWeight", defualt = 0.5f, range = new Vector2(0, 10), power = 3 });
				shader.properties.Add(new PropertyFloat() { name = "_IWD_DirObjectWeight", defualt = 0.5f, range = new Vector2(0, 10), power = 3 });
				shader.properties.Add(new PropertyVector() { name = "_IWD_DirObjectVector", defualt = Vector4.zero });
				shader.properties.Add(new PropertyFloat() { name = "_IWD_DirWorldWeight", defualt = 0.5f, range = new Vector2(0, 10), power = 3 });
				shader.properties.Add(new PropertyVector() { name = "_IWD_DirWorldVector", defualt = Vector4.zero });
				shader.properties.Add(new PropertyFloat() { name = "_IWD_MoveSpeed", defualt = 1, range = new Vector2(0, 15), power = 5 });
				shader.properties.Add(new PropertyFloat() { name = "_IWD_MoveAccel", defualt = 1, range = new Vector2(0, 15), power = 5 });
				shader.properties.Add(new PropertyColor() { name = "_IWD_TintColor", defualt = new Color(0.2f, 0.2f, 0.2f, 0.1f) });
				shader.properties.Add(new PropertyFloat() { name = "_IWD_TintFar", defualt = 0.5f, range = new Vector2(0, 10), power = 5 });
				shader.properties.Add(new PropertyFloat() { name = "_IWD_CmprssFar", defualt = 0.5f, range = new Vector2(0, 10), power = 5 });
				if (gen.complexity == ShaderComplexity.VHDGF) {
					shader.properties.Add(new PropertyFloat() { name = "_IWD_Tsltn", defualt = 1, range = new Vector2(0, 10), power = 2 });
				}
			} else {
				shader.Define("IWD_OFF 1");
			}
		}

		public override void GeneratorEditorGUI(KawaShadeGeneratorEditor editor) {
			var complexity = (editor.complexity_VGF || editor.complexity_VHDGF);
			using (new EditorGUI.DisabledScope(!complexity)) {
				var iwd = editor.serializedObject.FindProperty("iwd");
				KawaGUIUtility.ToggleLeft(iwd, gui_feature_iwd);
				using (new EditorGUI.DisabledScope(!iwd.hasMultipleDifferentValues && !iwd.boolValue)) {
					using (new EditorGUI.IndentLevelScope()) {
						KawaGUIUtility.DefaultPrpertyField(editor, "iwdDirections", "Directions");
					}
				}
			}
		}

		public override void ShaderEditorGUI(KawaShadeGUI editor) {
			var _IWD_Plane = editor.FindProperty("_IWD_Plane");
			var _IWD_PlaneDistRandomness = editor.FindProperty("_IWD_PlaneDistRandomness");

			var _IWD_DirRandomWeight = editor.FindProperty("_IWD_DirRandomWeight");
			var _IWD_DirPlaneWeight = editor.FindProperty("_IWD_DirPlaneWeight");
			var _IWD_DirNormalWeight = editor.FindProperty("_IWD_DirNormalWeight");
			var _IWD_DirObjectWeight = editor.FindProperty("_IWD_DirObjectWeight");
			var _IWD_DirObjectVector = editor.FindProperty("_IWD_DirObjectVector");
			var _IWD_DirWorldWeight = editor.FindProperty("_IWD_DirWorldWeight");
			var _IWD_DirWorldVector = editor.FindProperty("_IWD_DirWorldVector");

			var _IWD_MoveSpeed = editor.FindProperty("_IWD_MoveSpeed");
			var _IWD_MoveAccel = editor.FindProperty("_IWD_MoveAccel");

			var _IWD_CmprssFar = editor.FindProperty("_IWD_CmprssFar");
			var _IWD_TintFar = editor.FindProperty("_IWD_TintFar");
			var _IWD_TintColor = editor.FindProperty("_IWD_TintColor");
			var _IWD_Tsltn = editor.FindProperty("_IWD_Tsltn");

			var f_InfinityWarDecimation = KawaUtilities.AnyNotNull(
				_IWD_Plane, _IWD_PlaneDistRandomness,
				_IWD_DirRandomWeight, _IWD_DirPlaneWeight, _IWD_DirNormalWeight, _IWD_DirObjectWeight, _IWD_DirWorldWeight,
				_IWD_DirObjectVector, _IWD_DirWorldVector,
				_IWD_MoveSpeed, _IWD_MoveAccel,
				_IWD_CmprssFar, _IWD_TintFar, _IWD_TintColor, _IWD_Tsltn
			);

			using (new EditorGUI.DisabledScope(!f_InfinityWarDecimation)) {
				EditorGUILayout.LabelField("Infinity War Decimation Feature", f_InfinityWarDecimation ? "Enabled" : "Disabled");
				using (new EditorGUI.IndentLevelScope()) {
					if (f_InfinityWarDecimation) {
						EditorGUILayout.LabelField("I don't feel so good...");
						EditorGUILayout.LabelField("Particles Front");
						using (new EditorGUI.IndentLevelScope()) {
							EditorGUILayout.LabelField("General equation of a Plane (XYZ is normal, W is offset)");
							editor.ShaderPropertyDisabled(_IWD_Plane, "");
							editor.ShaderPropertyDisabled(_IWD_PlaneDistRandomness, "Randomness (W)");
						}
						EditorGUILayout.LabelField("Particles Direction");
						using (new EditorGUI.IndentLevelScope()) {
							editor.ShaderPropertyDisabled(_IWD_DirRandomWeight, "Random");
							editor.ShaderPropertyDisabled(_IWD_DirPlaneWeight, "Particles Front Plane");
							editor.ShaderPropertyDisabled(_IWD_DirNormalWeight, "Normal");
							editor.ShaderPropertyDisabled(_IWD_DirObjectWeight, "Object Space Vector");
							using (new EditorGUI.IndentLevelScope()) {
								editor.ShaderPropertyDisabled(_IWD_DirObjectVector, "");
							}
							editor.ShaderPropertyDisabled(_IWD_DirWorldWeight, "World Space Vector");
							using (new EditorGUI.IndentLevelScope()) {
								editor.ShaderPropertyDisabled(_IWD_DirWorldVector, "");
							}
						}
						EditorGUILayout.LabelField("Particles Movement");
						using (new EditorGUI.IndentLevelScope()) {
							editor.ShaderPropertyDisabled(_IWD_MoveSpeed, "Speed");
							editor.ShaderPropertyDisabled(_IWD_MoveAccel, "Accel");
						}
						editor.ShaderPropertyDisabled(_IWD_CmprssFar, "Compression Distance");
						editor.ShaderPropertyDisabled(_IWD_TintFar, "Tint Distance");
						editor.ShaderPropertyDisabled(_IWD_TintColor, "Tint Color");
						editor.ShaderPropertyDisabled(_IWD_Tsltn, "Tessellation factor");
					}
				}
			}
		}
	}

	public partial class KawaShadeGenerator {
		public bool iwd = false;
		public FeatureInfinityWarDecimation.Directions iwdDirections = 0;
	}
}
