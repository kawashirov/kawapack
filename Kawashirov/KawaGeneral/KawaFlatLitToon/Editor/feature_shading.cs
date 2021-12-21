using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
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
using SC = Kawashirov.KawaUtilities;

using static UnityEditor.EditorGUI;

namespace Kawashirov.FLT {
	public enum ShadingMode { CubedParadoxFLT, KawashirovFLTSingle, KawashirovFLTRamp }

	internal static partial class Commons {
		internal static readonly string F_Shading = "KawaFLT_Feature_Shading";

		internal static readonly Dictionary<ShadingMode, string> shadingModeNames = new Dictionary<ShadingMode, string>() {
			{ ShadingMode.CubedParadoxFLT, "CubedParadox Flat Lit Toon" },
			{ ShadingMode.KawashirovFLTSingle, "Kawashirov Flat Lit Toon, Single-Step, Diffuse-based, Simple." },
			{ ShadingMode.KawashirovFLTRamp, "Kawashirov Flat Lit Toon, Ramp-based, In dev yet." },
		};

		internal static readonly Dictionary<ShadingMode, string> shadingModeDesc = new Dictionary<ShadingMode, string>() {
			{ ShadingMode.CubedParadoxFLT, "CubedParadox Flat Lit Toon. Legacy. Not recommended. And I dislike this." },
			{ ShadingMode.KawashirovFLTSingle, "Kawashirov Flat Lit Toon, Single-Step, Diffuse-based, Simple. Like CubedParadox, but better: supports more standard unity lighting features and also fast as fuck compare to other cbd-flt-like shaders." },
			{ ShadingMode.KawashirovFLTRamp, "Kawashirov Flat Lit Toon, Ramp-based, In dev yet, need extra tests in various conditions, but you can use it, It should work well." },
		};
	}

	public partial class Generator {
		public ShadingMode shading = ShadingMode.KawashirovFLTSingle;

		private void ConfigureFeatureShading(ShaderSetup shader) {
			shader.TagEnum(KFLTC.F_Shading, shading);
			switch (shading) {
				case ShadingMode.CubedParadoxFLT:
					shader.forward.defines.Add("SHADE_CUBEDPARADOXFLT 1");
					shader.forward_add.defines.Add("SHADE_CUBEDPARADOXFLT 1");
					ConfigureFeatureShadingCubedParadox(shader);
					break;
				case ShadingMode.KawashirovFLTSingle:
					shader.forward.defines.Add("SHADE_KAWAFLT_SINGLE 1");
					shader.forward_add.defines.Add("SHADE_KAWAFLT_SINGLE 1");
					ConfigureFeatureShadingKawashirovFLTSingle(shader);
					break;
				case ShadingMode.KawashirovFLTRamp:
					shader.forward.defines.Add("SHADE_KAWAFLT_RAMP 1");
					shader.forward_add.defines.Add("SHADE_KAWAFLT_RAMP 1");
					ConfigureFeatureShadingKawashirovFLTRamp(shader);
					break;
			}
		}

		private void ConfigureFeatureShadingCubedParadox(ShaderSetup shader) {
			shader.properties.Add(new PropertyFloat() { name = "_Sh_Cbdprdx_Shadow", defualt = 0.4f, range = new Vector2(0, 1) });
		}

		private void ConfigureFeatureShadingKawashirovFLTSingle(ShaderSetup shader) {
			shader.properties.Add(new PropertyFloat() { name = "_Sh_Kwshrv_ShdBlnd", defualt = 0.7f, range = new Vector2(0, 1), power = 2 });
			shader.properties.Add(new PropertyFloat() { name = "_Sh_KwshrvSngl_TngntLo", defualt = 0.7f, range = new Vector2(0, 1), power = 1.5f });
			shader.properties.Add(new PropertyFloat() { name = "_Sh_KwshrvSngl_TngntHi", defualt = 0.8f, range = new Vector2(0, 1), power = 1.5f });
			shader.properties.Add(new PropertyFloat() { name = "_Sh_KwshrvSngl_ShdLo", defualt = 0.4f, range = new Vector2(0, 1) });
			shader.properties.Add(new PropertyFloat() { name = "_Sh_KwshrvSngl_ShdHi", defualt = 0.9f, range = new Vector2(0, 1) });
		}

		private void ConfigureFeatureShadingKawashirovFLTRamp(ShaderSetup shader) {
			shader.properties.Add(new PropertyFloat() { name = "_Sh_Kwshrv_ShdBlnd", defualt = 0.7f, range = new Vector2(0, 1), power = 2 });
			shader.properties.Add(new Property2D() { name = "_Sh_KwshrvRmp_Tex", defualt = "gray" });
			shader.properties.Add(new PropertyColor() { name = "_Sh_KwshrvRmp_NdrctClr", defualt = Color.white });
			shader.properties.Add(new PropertyFloat() { name = "_Sh_KwshrvSngl_TngntLo", defualt = 0.7f, range = new Vector2(0, 1), power = 1.5f });
		}

	}

	public partial class GeneratorEditor {
		private void ShadingGUI() {
			var shading = serializedObject.FindProperty("shading");
			KawaGUIUtility.PropertyEnumPopupCustomLabels(shading, "Shading Method", KFLTC.shadingModeNames);
		}
	}
}

internal partial class KawaFLTShaderGUI {
	protected void OnGUI_Shading() {
		ShadingMode shading = default;
		if (shaderTags[KFLTC.F_Shading].GetEnumValueSafe(ref shading)) {
			EGUIL.LabelField("Shading", Enum.GetName(typeof(ShadingMode), shading));
			using (new IndentLevelScope()) {
				EGUIL.HelpBox(KFLTC.shadingModeDesc[shading], MessageType.Info);
				if (shading == ShadingMode.CubedParadoxFLT) {
					ShaderPropertyDisabled(FindProperty("_Sh_Cbdprdx_Shadow"), "Shadow");
				} else if (shading == ShadingMode.KawashirovFLTSingle) {
					ShaderPropertyDisabled(FindProperty("_Sh_Kwshrv_ShdBlnd"), "RT Shadows blending");

					EGUIL.LabelField("Sides threshold");
					using (new IndentLevelScope()) {
						ShaderPropertyDisabled(FindProperty("_Sh_KwshrvSngl_TngntLo"), "Low");
						ShaderPropertyDisabled(FindProperty("_Sh_KwshrvSngl_TngntHi"), "High");
					}

					EGUIL.LabelField("Brightness");
					using (new IndentLevelScope()) {
						ShaderPropertyDisabled(FindProperty("_Sh_KwshrvSngl_ShdLo"), "Back side (Shaded)");
						ShaderPropertyDisabled(FindProperty("_Sh_KwshrvSngl_ShdHi"), "Front side (Lit)");
					}
				} else if (shading == ShadingMode.KawashirovFLTRamp) {
					var rampTex = FindProperty("_Sh_KwshrvRmp_Tex");
					ShaderPropertyDisabled(FindProperty("_Sh_Kwshrv_ShdBlnd"), "RT Shadows blending");
					materialEditor.TexturePropertySingleLine(new GUIContent("Ramp Texture", "Ramp Texture (RGB)"), rampTex);
					materialEditor.TextureCompatibilityWarning(rampTex);
					if (rampTex.textureValue == null) {
						EGUIL.HelpBox(
							"Ramp texture is not set! This shading model will not work well unless proper ramp texture is set!",
							MessageType.Error
						);
					}
					ShaderPropertyDisabled(FindProperty("_Sh_KwshrvRmp_Pwr"), "Power");
					ShaderPropertyDisabled(FindProperty("_Sh_KwshrvRmp_NdrctClr"), "Indirect Tint");
				}
			}
		} else {
			EGUIL.LabelField("Shading", "Mixed Values or Unknown");
		}
	}
}
