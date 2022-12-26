using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;

namespace Kawashirov.KawaShade {
	public enum DistanceFadeMode { Range, Infinity }

	internal static partial class KawaShadeCommons {
		internal static readonly string F_DistanceFade = "KawaShade_Feature_DistanceFade";
		internal static readonly string F_DistanceFadeMode = "KawaShade_Feature_DistanceFadeMode";
	}

	public partial class KawaShadeGenerator {
		public bool distanceFade = false;
		public DistanceFadeMode distanceFadeMode = DistanceFadeMode.Range;

		private void ConfigureFeatureDistanceFade(ShaderSetup shader) {
			shader.TagBool(KawaShadeCommons.F_DistanceFade, distanceFade);
			if (distanceFade) {
				shader.Define("DSTFD_ON 1");
				needRandomFrag = true;
				shader.TagEnum(KawaShadeCommons.F_DistanceFadeMode, distanceFadeMode);
				switch (distanceFadeMode) {
					case DistanceFadeMode.Range:
						shader.Define("DSTFD_RANGE 1");
						break;
					case DistanceFadeMode.Infinity:
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

	}

	public partial class KawaShadeGeneratorEditor {
		private static readonly GUIContent gui_feature_dstfd = new GUIContent("Distance Dithering Fade Feature");

		private void DistanceFadeGUI() {
			var distanceFade = serializedObject.FindProperty("distanceFade");
			KawaGUIUtility.ToggleLeft(distanceFade, gui_feature_dstfd);
			using (new EditorGUI.DisabledScope(distanceFade.hasMultipleDifferentValues || !distanceFade.boolValue)) {
				using (new EditorGUI.IndentLevelScope()) {
					KawaGUIUtility.DefaultPrpertyField(this, "distanceFadeMode", "Mode");
				}
			}
		}
	}

	internal partial class KawaShadeGUI {
		protected void OnGUI_DistanceFade() {
			var _DstFd_Axis = FindProperty("_DstFd_Axis");
			var _DstFd_Near = FindProperty("_DstFd_Near");
			var _DstFd_Far = FindProperty("_DstFd_Far");
			var _DstFd_AdjustPower = FindProperty("_DstFd_AdjustPower");
			var _DstFd_AdjustScale = FindProperty("_DstFd_AdjustScale");
			var f_distanceFade = KawaUtilities.AnyNotNull(_DstFd_Axis, _DstFd_Near, _DstFd_Far, _DstFd_AdjustPower, _DstFd_AdjustScale);
			using (new EditorGUI.DisabledScope(!f_distanceFade)) {
				EditorGUILayout.LabelField("Distance Fade Feature", f_distanceFade ? "Enabled" : "Disabled");
				using (new EditorGUI.IndentLevelScope()) {
					if (f_distanceFade) {
						LabelEnumDisabledFromTagMixed<DistanceFadeMode>("Mode", KawaShadeCommons.F_DistanceFadeMode);
						ShaderPropertyDisabled(_DstFd_Axis, "Axis weights");
						ShaderPropertyDisabled(_DstFd_Near, "Near Distance");
						ShaderPropertyDisabled(_DstFd_Far, "Far Distance");
						ShaderPropertyDisabled(_DstFd_AdjustPower, "Power Adjust");
						ShaderPropertyDisabled(_DstFd_AdjustScale, "Scale Adjust");
					}
				}
			}
		}
	}
}

