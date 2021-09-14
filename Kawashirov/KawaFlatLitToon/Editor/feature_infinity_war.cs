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
	[Flags]
	public enum IWDDirections {
		Plane = 1,
		Random = 2,
		Normal = 4,
		ObjectVector = 8,
		WorldVector = 16,
	}

	internal static partial class Commons {
		internal static readonly string F_IWD = "KawaFLT_Feature_IWD";
		internal static readonly string F_IWDDirections = "KawaFLT_Feature_IWDDirections";
	}

	public partial class Generator {
		public bool iwd = false;
		public IWDDirections iwdDirections = 0;

		private void ConfigureFeatureInfinityWarDecimation(ShaderSetup shader) {
			shader.TagBool(KFLTC.F_IWD, iwd);
			if (iwd && (complexity == ShaderComplexity.VGF || complexity == ShaderComplexity.VHDGF)) {
				needRandomVert = true;
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
				if (complexity == ShaderComplexity.VHDGF) {
					shader.properties.Add(new PropertyFloat() { name = "_IWD_Tsltn", defualt = 1, range = new Vector2(0, 10), power = 2 });
				}
			} else {
				shader.Define("IWD_OFF 1");
			}
		}
	}
	public partial class GeneratorEditor {
		private static readonly GUIContent gui_feature_iwd = new GUIContent("Infinity War Decimation Feature");

		private void IWDGUI() {
			using (new DisabledScope(!complexity_VGF && !complexity_VHDGF)) {
				var iwd = serializedObject.FindProperty("iwd");
				ToggleLeft(iwd, gui_feature_iwd);
				using (new DisabledScope(
					iwd.hasMultipleDifferentValues || !iwd.boolValue || (!complexity_VGF && !complexity_VHDGF)
				)) {
					using (new IndentLevelScope()) {
						DefaultPrpertyField("iwdDirections", "Directions");
					}
				}
			}
		}

	}
}

internal partial class KawaFLTShaderGUI {

	private void OnGUI_InfinityWarDecimation() {
		var _IWD_Plane = FindProperty("_IWD_Plane");
		var _IWD_PlaneDistRandomness = FindProperty("_IWD_PlaneDistRandomness");

		var _IWD_DirRandomWeight = FindProperty("_IWD_DirRandomWeight");
		var _IWD_DirPlaneWeight = FindProperty("_IWD_DirPlaneWeight");
		var _IWD_DirNormalWeight = FindProperty("_IWD_DirNormalWeight");
		var _IWD_DirObjectWeight = FindProperty("_IWD_DirObjectWeight");
		var _IWD_DirObjectVector = FindProperty("_IWD_DirObjectVector");
		var _IWD_DirWorldWeight = FindProperty("_IWD_DirWorldWeight");
		var _IWD_DirWorldVector = FindProperty("_IWD_DirWorldVector");

		var _IWD_MoveSpeed = FindProperty("_IWD_MoveSpeed");
		var _IWD_MoveAccel = FindProperty("_IWD_MoveAccel");

		var _IWD_CmprssFar = FindProperty("_IWD_CmprssFar");
		var _IWD_TintFar = FindProperty("_IWD_TintFar");
		var _IWD_TintColor = FindProperty("_IWD_TintColor");
		var _IWD_Tsltn = FindProperty("_IWD_Tsltn");

		var f_InfinityWarDecimation = SC.AnyNotNull(
			_IWD_Plane, _IWD_PlaneDistRandomness,
			_IWD_DirRandomWeight, _IWD_DirPlaneWeight, _IWD_DirNormalWeight, _IWD_DirObjectWeight, _IWD_DirWorldWeight,
			_IWD_DirObjectVector, _IWD_DirWorldVector,
			_IWD_MoveSpeed, _IWD_MoveAccel,
			_IWD_CmprssFar, _IWD_TintFar, _IWD_TintColor, _IWD_Tsltn
		);

		using (new DisabledScope(!f_InfinityWarDecimation)) {
			EGUIL.LabelField("Infinity War Decimation Feature", f_InfinityWarDecimation ? "Enabled" : "Disabled");
			using (new IndentLevelScope()) {
				if (f_InfinityWarDecimation) {
					EGUIL.LabelField("I don't feel so good...");
					EGUIL.LabelField("Particles Front");
					using (new IndentLevelScope()) {
						EGUIL.LabelField("General equation of a Plane (XYZ is normal, W is offset)");
						ShaderPropertyDisabled(_IWD_Plane, "");
						ShaderPropertyDisabled(_IWD_PlaneDistRandomness, "Randomness (W)");
					}
					EGUIL.LabelField("Particles Direction");
					using (new IndentLevelScope()) {
						ShaderPropertyDisabled(_IWD_DirRandomWeight, "Random");
						ShaderPropertyDisabled(_IWD_DirPlaneWeight, "Particles Front Plane");
						ShaderPropertyDisabled(_IWD_DirNormalWeight, "Normal");
						ShaderPropertyDisabled(_IWD_DirObjectWeight, "Object Space Vector");
						using (new IndentLevelScope()) {
							ShaderPropertyDisabled(_IWD_DirObjectVector, "");
						}
						ShaderPropertyDisabled(_IWD_DirWorldWeight, "World Space Vector");
						using (new IndentLevelScope()) {
							ShaderPropertyDisabled(_IWD_DirWorldVector, "");
						}
					}
					EGUIL.LabelField("Particles Movement");
					using (new IndentLevelScope()) {
						ShaderPropertyDisabled(_IWD_MoveSpeed, "Speed");
						ShaderPropertyDisabled(_IWD_MoveAccel, "Accel");
					}
					ShaderPropertyDisabled(_IWD_CmprssFar, "Compression Distance");
					ShaderPropertyDisabled(_IWD_TintFar, "Tint Distance");
					ShaderPropertyDisabled(_IWD_TintColor, "Tint Color");
					ShaderPropertyDisabled(_IWD_Tsltn, "Tessellation factor");
				}
			}
		}
	}

}
