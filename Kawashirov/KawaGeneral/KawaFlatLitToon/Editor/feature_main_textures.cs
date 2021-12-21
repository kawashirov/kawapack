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

	public enum MainTexKeywords { NoMainTex, NoMask, ColorMask }
	public enum CutoutMode { Classic, RangeRandom, RangeRandomH01 }
	public enum EmissionMode { AlbedoNoMask, AlbedoMask, Custom }

	internal static partial class Commons {
		internal static readonly string F_MainTex = "KawaFLT_Feature_MainTex";
		internal static readonly string F_Cutout_Forward = "KawaFLT_Feature_Cutout_Forward";
		internal static readonly string F_Cutout_ShadowCaster = "KawaFLT_Feature_Cutout_ShadowCaster";
		internal static readonly string F_Cutout_Classic = "KawaFLT_Feature_Cutout_Classic";
		internal static readonly string F_Cutout_RangeRandom = "KawaFLT_Feature_Cutout_RangeRandom";
		internal static readonly string F_Emission = "KawaFLT_Feature_Emission";
		internal static readonly string F_EmissionMode = "KawaFLT_Feature_EmissionMode";
		internal static readonly string F_NormalMap = "KawaFLT_Feature_NormalMap";

		internal static readonly Dictionary<MainTexKeywords, string> mainTexKeywordsNames = new Dictionary<MainTexKeywords, string> {
			{ MainTexKeywords.NoMainTex, "No Main Texture (Color Only)" },
			{ MainTexKeywords.NoMask, "Main Texture without Color Mask" },
			{ MainTexKeywords.ColorMask, "Main Texture with Color Mask" },
		};

		internal static readonly Dictionary<EmissionMode, string> emissionMode = new Dictionary<EmissionMode, string> {
			{ EmissionMode.AlbedoNoMask, "Emission from Main Texture without Mask" },
			{ EmissionMode.AlbedoMask, "Emission from Main Texture with Mask" },
			{ EmissionMode.Custom, "Custom Emission Texture" },
		};

		internal static readonly Dictionary<CutoutMode, string> cutoutModeNames = new Dictionary<CutoutMode, string>() {
			{ CutoutMode.Classic, "Classic (Single alpha value as threshold)" },
			{ CutoutMode.RangeRandom, "Random Range (Two alpha values defines range where texture randomly fades)" },
			{ CutoutMode.RangeRandomH01, "Random Range H01 (Same as Random Range, but also cubic Hermite spline smooth)" },
		};
	}

	public partial class Generator {
		public MainTexKeywords mainTex = MainTexKeywords.ColorMask;
		public bool mainTexSeparateAlpha = false;
		public CutoutMode cutout = CutoutMode.Classic;
		public bool emission = true;
		public EmissionMode emissionMode = EmissionMode.Custom;
		public bool bumpMap = false;

		private void ConfigureFeatureMainTex(ShaderSetup shader) {
			var mainTex = this.mainTex;
			if (mode == BlendTemplate.Cutout && mainTex == MainTexKeywords.NoMainTex)
				mainTex = MainTexKeywords.NoMask;

			shader.TagEnum(KFLTC.F_MainTex, mainTex);

			switch (this.mainTex) {
				case MainTexKeywords.NoMainTex:
					shader.Define("MAINTEX_OFF 1");
					break;
				case MainTexKeywords.NoMask:
					shader.Define("MAINTEX_NOMASK 1");
					break;
				case MainTexKeywords.ColorMask:
					shader.Define("MAINTEX_COLORMASK 1");
					break;
			}

			if (this.mainTex == MainTexKeywords.ColorMask || this.mainTex == MainTexKeywords.NoMask) {
				shader.properties.Add(new Property2D() { name = "_MainTex" });
				if (this.mainTex == MainTexKeywords.ColorMask) {
					shader.properties.Add(new Property2D() { name = "_ColorMask", defualt = "black" });
				}
				if (mainTexSeparateAlpha) {
					shader.Define("MAINTEX_SEPARATE_ALPHA 1");
					shader.properties.Add(new Property2D() { name = "_MainTexAlpha", defualt = "white" });
				}
			}

			shader.properties.Add(new PropertyColor() { name = "_Color" });
		}

		private void ConfigureFeatureCutoff(ShaderSetup shader) {
			var forward_on = false;
			var forward_mode = CutoutMode.Classic;
			var shadow_on = false;
			var shadow_mode = CutoutMode.Classic;
			var cutoff_fade_flag = false;

			if (mode == BlendTemplate.Cutout) {
				forward_on = true;
				forward_mode = cutout;
				shadow_on = !forceNoShadowCasting;
				shadow_mode = cutout;

			} else if (mode == BlendTemplate.Fade) {
				forward_on = false;
				forward_mode = cutout;
				shadow_on = !forceNoShadowCasting;
				shadow_mode = cutout;

			} else if (mode == BlendTemplate.FadeCutout) {
				// Only classic
				forward_on = true;
				forward_mode = CutoutMode.Classic;
				shadow_on = !forceNoShadowCasting;
				shadow_mode = cutout;
				cutoff_fade_flag = true;
			}

			ConfigureFeatureCutoffPassDefines(shader.forward, forward_on, forward_mode, cutoff_fade_flag);
			ConfigureFeatureCutoffPassDefines(shader.forward_add, forward_on, forward_mode, cutoff_fade_flag);
			ConfigureFeatureCutoffPassDefines(shader.shadowcaster, shadow_on, shadow_mode, cutoff_fade_flag);

			var prop_classic = false;
			var prop_range = false;
			if (forward_on) {
				prop_classic |= forward_mode == CutoutMode.Classic;
				prop_range |= forward_mode == CutoutMode.RangeRandom;
				prop_range |= forward_mode == CutoutMode.RangeRandomH01;
				shader.TagEnum(KFLTC.F_Cutout_Forward, forward_mode);
			}
			if (shadow_on) {
				prop_classic |= shadow_mode == CutoutMode.Classic;
				prop_range |= shadow_mode == CutoutMode.RangeRandom;
				prop_range |= shadow_mode == CutoutMode.RangeRandomH01;
				shader.TagEnum(KFLTC.F_Cutout_ShadowCaster, shadow_mode);
			}

			shader.TagBool(KFLTC.F_Cutout_Classic, prop_classic);
			if (prop_classic) {
				shader.properties.Add(new PropertyFloat() { name = "_Cutoff", defualt = 0.5f, range = new Vector2(0, 1) });
			}
			shader.TagBool(KFLTC.F_Cutout_RangeRandom, prop_range);
			if (prop_range) {
				shader.properties.Add(new PropertyFloat() { name = "_CutoffMin", defualt = 0.4f, range = new Vector2(0, 1) });
				shader.properties.Add(new PropertyFloat() { name = "_CutoffMax", defualt = 0.6f, range = new Vector2(0, 1) });
			}
		}

		private void ConfigureFeatureCutoffPassDefines(PassSetup pass, bool is_on, CutoutMode mode, bool cutoff_fade_flag) {
			if (is_on) {
				pass.defines.Add("CUTOFF_ON 1");

				if (cutoff_fade_flag) {
					pass.defines.Add("CUTOFF_FADE 1");
				}

				if (mode == CutoutMode.Classic && !cutoff_fade_flag) {
					pass.defines.Add("CUTOFF_CLASSIC 1");
					// CUTOFF_FADE итак делает обрезку + коррекцию, вторая не к чему. 
				}
				if (mode == CutoutMode.RangeRandom || mode == CutoutMode.RangeRandomH01) {
					needRandomFrag = true;
					pass.defines.Add("CUTOFF_RANDOM 1");
					if (mode == CutoutMode.RangeRandomH01) {
						pass.defines.Add("CUTOFF_RANDOM_H01 1");
						// CUTOFF_RANDOM_H01 расширяет поведение CUTOFF_RANDOM, а не заменет
					}
				}
			} else {
				pass.defines.Add("CUTOFF_OFF 1");
			}
		}

		private void ConfigureFeatureEmission(ShaderSetup shader) {
			shader.TagBool(KFLTC.F_Emission, emission);
			if (emission) {
				shader.forward.defines.Add("EMISSION_ON 1");
				shader.TagEnum(KFLTC.F_EmissionMode, emissionMode);
				switch (emissionMode) {
					case EmissionMode.AlbedoNoMask:
						shader.forward.defines.Add("EMISSION_ALBEDO_NOMASK 1");
						break;
					case EmissionMode.AlbedoMask:
						shader.forward.defines.Add("EMISSION_ALBEDO_MASK 1");
						break;
					case EmissionMode.Custom:
						shader.forward.defines.Add("EMISSION_CUSTOM 1");
						break;
				}
				if (emissionMode == EmissionMode.AlbedoMask) {
					shader.properties.Add(new Property2D() { name = "_EmissionMask", defualt = "white" });
				}
				if (emissionMode == EmissionMode.Custom) {
					shader.properties.Add(new Property2D() { name = "_EmissionMap", defualt = "white" });
				}
				shader.properties.Add(new PropertyColor() { name = "_EmissionColor", defualt = Color.black });
			} else {
				shader.forward.defines.Add("EMISSION_OFF 1");
			}
		}

		private void ConfigureFeatureNormalMap(ShaderSetup shader) {
			shader.TagBool(KFLTC.F_NormalMap, bumpMap);
			if (bumpMap) {
				shader.forward.defines.Add("_NORMALMAP");
				shader.forward_add.defines.Add("_NORMALMAP");
				shader.properties.Add(new Property2D() { name = "_BumpMap", defualt = "bump", isNormal = true });
				shader.properties.Add(new PropertyFloat() { name = "_BumpScale", defualt = 1.0f });
			}
		}
	}

	public partial class GeneratorEditor {
		private void TexturesGUI() {
			EGUIL.LabelField("General Rendering Features");
			using (new IndentLevelScope()) {
				var mainTex = serializedObject.FindProperty("mainTex");
				KawaGUIUtility.PropertyEnumPopupCustomLabels(mainTex, "Main (Albedo) Texture", KFLTC.mainTexKeywordsNames);
				KawaGUIUtility.DefaultPrpertyField(this, "mainTexSeparateAlpha", "Alpha in separate texture");

				var cutout = serializedObject.FindProperty("cutout");
				KawaGUIUtility.PropertyEnumPopupCustomLabels(cutout, "Cutout Mode", KFLTC.cutoutModeNames);

				var emission = serializedObject.FindProperty("emission");
				KawaGUIUtility.DefaultPrpertyField(emission);
				using (new DisabledScope(!emission.boolValue)) {
					using (new IndentLevelScope()) {
						var emissionMode = serializedObject.FindProperty("emissionMode");
						KawaGUIUtility.PropertyEnumPopupCustomLabels(emissionMode, "Mode", KFLTC.emissionMode);
					}
				}
				KawaGUIUtility.DefaultPrpertyField(this, "bumpMap");
			}
		}
	}
}

internal partial class KawaFLTShaderGUI {
	protected void OnGUI_Textures() {
		EGUIL.LabelField("General Rendering Features");
		using (new IndentLevelScope()) {
			var _MainTex = FindProperty("_MainTex");
			var _Color = FindProperty("_Color");
			var _ColorMask = FindProperty("_ColorMask");

			var _MainTex_label = new GUIContent("Albedo (Main Texture)", "Albedo Main Color Texture (RGBA)");
			var _ColorMask_label = new GUIContent("Color Mask", "Masks Color Tint (R)");

			TexturePropertySingleLineDisabled(_MainTex_label, _MainTex);
			if (_MainTex != null && _MainTex.textureValue == null) {
				EGUIL.HelpBox(
					"No albedo texture is set! Disable main tex feature in shader generator, if you don't need this.",
					MessageType.Warning
				);
			}

			using (new IndentLevelScope()) {
				var _MainTexAlpha = FindProperty("_MainTexAlpha");
				var _MainTexAlpha_label = new GUIContent("Alpha (of Main Texture)", "Separate Alpha-channel for Main Color Texture (R)");
				TexturePropertySingleLineDisabled(_MainTexAlpha_label, _MainTexAlpha);

				LabelEnumDisabledFromTagMixed<CutoutMode>("Forward Pass Cutout Mode", KFLTC.F_Cutout_Forward);
				LabelEnumDisabledFromTagMixed<CutoutMode>("Shadow Caster Cutout Mode", KFLTC.F_Cutout_ShadowCaster);

				ShaderPropertyDisabled(FindProperty("_Cutoff"), "Cutout (Classic)");
				ShaderPropertyDisabled(FindProperty("_CutoffMin"), "Cutout Min");
				ShaderPropertyDisabled(FindProperty("_CutoffMax"), "Cutout Max");

				ShaderPropertyDisabled(_Color, "Color");

				TexturePropertySingleLineDisabled(_ColorMask_label, _ColorMask);
				if (_ColorMask != null && _ColorMask.textureValue == null) {
					EGUIL.HelpBox(
						"No color mask texture set! Disable main texture color mask feature in shader generator, if you don't need this.",
						MessageType.Warning
					);
				}
			}

			var _EmissionMask = FindProperty("_EmissionMask");
			var _EmissionMap = FindProperty("_EmissionMap");
			var _EmissionColor = FindProperty("_EmissionColor");
			var f_emission = _EmissionMask != null || _EmissionMap != null || _EmissionColor != null;

			using (new DisabledScope(!f_emission)) {
				EGUIL.LabelField("Emission Feature", f_emission ? "Enabled" : "Disabled");
				if (f_emission) {
					using (new IndentLevelScope()) {
						LabelEnumDisabledFromTagMixed("Emission Mode", KFLTC.F_EmissionMode, KFLTC.emissionMode);

						var _EmissionMask_label = new GUIContent("Emission Mask", "Mask for Emission by Albedo Main Texture (R)");
						TexturePropertySingleLineDisabled(_EmissionMask_label, _EmissionMask);
						if (_EmissionMask != null && _EmissionMask.textureValue == null) {
							EGUIL.HelpBox(
								"No emission mask texture set! Disable emission mask feature in shader generator, if you don't need this.",
								MessageType.Warning
							);
						}

						var _EmissionMap_label = new GUIContent("Emission Texture", "Custom Emission Texture (RGB)");
						TexturePropertySingleLineDisabled(_EmissionMap_label, _EmissionMap);
						if (_EmissionMap != null && _EmissionMap.textureValue == null) {
							EGUIL.HelpBox(
								"No emission map texture set! Disable emission map feature in shader generator, if you don't need this.",
								MessageType.Warning
							);
						}

						ShaderPropertyDisabled(_EmissionColor, new GUIContent("Emission Color (Tint)", "Emission Color Tint (RGB)"));
						var _EmissionColor_value = _EmissionColor.colorValue;
						var intencity = (_EmissionColor_value.r + _EmissionColor_value.g + _EmissionColor_value.b) * _EmissionColor_value.a;
						if (intencity < 0.05) {
							EGUIL.HelpBox(
								"Emission Color is too dark! disable emission feature in shader generator, if you don't need emission.",
								MessageType.Warning
							);
						}

					}
				}
			}

			var _BumpMap = FindProperty("_BumpMap");
			var label = new GUIContent("Normal Map", "Normal (Bump) Map Texture (RGB)");
			TexturePropertySingleLineDisabled(label, _BumpMap);
			if (_BumpMap != null && _BumpMap.textureValue == null) {
				EGUIL.HelpBox(
					"Normal map texture is not set! Disable normal feature in shader generator, if you don't need this.",
					MessageType.Warning
				);
			}
			using (new IndentLevelScope()) {
				var _BumpScale = FindProperty("_BumpScale");
				ShaderPropertyDisabled(_BumpScale, "Normal Map Scale");
				if (_BumpScale != null && _BumpScale.floatValue < 0.05) {
					EGUIL.HelpBox(
						"Normal map scale value is close to zero! In this situation, may be it's better to disable normal feature in shader generator, if you don't need this?",
						MessageType.Warning
					);
				}
			}
		}
	}
}
