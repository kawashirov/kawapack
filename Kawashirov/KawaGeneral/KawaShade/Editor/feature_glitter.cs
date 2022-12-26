using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;
using System.Collections.Generic;

namespace Kawashirov.KawaShade {
	public enum GlitterMode { SolidColor, SolidAlbedo }
	internal static partial class KawaShadeCommons {
		internal static readonly string F_Glitter = "KawaShade_Feature_Glitter";

		internal static readonly Dictionary<GlitterMode, string> glitterModeDesc = new Dictionary<GlitterMode, string>() {
			{ GlitterMode.SolidColor, "Use given Color as glitter color. No randomness." },
			{ GlitterMode.SolidAlbedo, "Use Albedo as glitter color. Brightness can be adjusted. No randomness." },
		};
	}

	public partial class KawaShadeGenerator {
		public bool glitter = false;
		public bool glitterMask = false;
		public GlitterMode glitterMode = GlitterMode.SolidColor;

		private void ConfigureFeatureGlitter(ShaderSetup shader) {
			shader.TagBool(KawaShadeCommons.F_Glitter, glitter);
			if (glitter) {
				shader.Define("GLITTER_ON 1");
				needRandomFrag = true;

				if (glitterMask) {
					shader.Define("GLITTER_MASK_ON 1");
					shader.properties.Add(new Property2D() { name = "_Gltr_Mask" });
				}

				shader.properties.Add(new PropertyFloat() { name = "_Gltr_Dnst", defualt = 0.01f, range = Vector2.up, power = 5 });

				shader.TagEnum(KawaShadeCommons.F_Glitter, glitterMode);
				switch (glitterMode) {
					case GlitterMode.SolidColor:
						shader.Define("GLITTER_MODE_SOLID_COLOR 1");
						shader.properties.Add(new PropertyColor() { name = "_Gltr_Color", defualt = Color.white });
						break;
					case GlitterMode.SolidAlbedo:
						shader.Define("GLITTER_MODE_SOLID_ALBEDO 1");
						break;
				}

				shader.properties.Add(new PropertyFloat() { name = "_Gltr_Brght", defualt = 1, range = Vector2.up });

				shader.properties.Add(new PropertyFloat() { name = "_Gltr_Em", defualt = 1, range = Vector2.up });
			} else {
				shader.Define("GLITTER_OFF 1");
			}
		}
	}

	public partial class KawaShadeGeneratorEditor {
		private static readonly GUIContent gui_feature_glitter = new GUIContent("Simple Glitter Feature");

		private void GlitterGUI() {
			var glitter = serializedObject.FindProperty("glitter");
			KawaGUIUtility.ToggleLeft(glitter, gui_feature_glitter);
			using (new EditorGUI.DisabledScope(glitter.hasMultipleDifferentValues || !glitter.boolValue)) {
				using (new EditorGUI.IndentLevelScope()) {
					KawaGUIUtility.DefaultPrpertyField(this, "glitterMask", "Mask");
					var glitterMode = serializedObject.FindProperty("glitterMode");
					KawaGUIUtility.DefaultPrpertyField(glitterMode, "Mode");
					if (!glitterMode.hasMultipleDifferentValues) {
						var glitterModeE = (GlitterMode)glitterMode.enumValueIndex;
						KawaShadeCommons.glitterModeDesc.TryGetValue(glitterModeE, out var message);
						if (!string.IsNullOrWhiteSpace(message)) {
							using (new EditorGUI.IndentLevelScope()) {
								EditorGUILayout.HelpBox(message, MessageType.None);
							}
						}
					}
				}
			}
		}
	}

	internal partial class KawaShadeGUI {
		private static readonly GUIContent _Gltr_Mask_label = new GUIContent("Mask", "Masks Glitter effect (R)");

		protected void OnGUI_Glitter() {
			var _Gltr_Mask = FindProperty("_Gltr_Mask");
			var _Gltr_Dnst = FindProperty("_Gltr_Dnst");
			var _Gltr_Color = FindProperty("_Gltr_Color");
			var _Gltr_Brght = FindProperty("_Gltr_Brght");
			var _Gltr_Em = FindProperty("_Gltr_Em");

			var f_glitter = KawaUtilities.AnyNotNull(_Gltr_Mask, _Gltr_Dnst, _Gltr_Color, _Gltr_Brght, _Gltr_Em);
			using (new EditorGUI.DisabledScope(!f_glitter)) {
				EditorGUILayout.LabelField("Simple Glitter Feature", f_glitter ? "Enabled" : "Disabled");
				using (new EditorGUI.IndentLevelScope()) {
					if (f_glitter) {
						TexturePropertySingleLineDisabled(_Gltr_Mask_label, _Gltr_Mask);
						ShaderPropertyDisabled(_Gltr_Dnst, "Density");
						ShaderPropertyDisabled(_Gltr_Color, "Color");
						ShaderPropertyDisabled(_Gltr_Brght, "Brightness");
						ShaderPropertyDisabled(_Gltr_Em, "Emission");
					}
				}
			}
		}
	}
}
