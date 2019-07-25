using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Kawashirov.FLT  
{

public class Inspector : MaterialEditor {

	protected bool updateMeta = false;
	protected bool checkValues = false;
	protected bool haveGeometry = false;
	protected bool haveTessellation = false;
	protected IDictionary<string, MaterialProperty> materialProperties;

	// protected static bool MaterialIsLightweight(Material material) {
	// 	return MaterialCheckTag(material, "KawaFLT_Features", "Geometry");
	// }

	// protected static bool MaterialIsGeometry(Material material) {
	// 	return material && string.Equals("Geometry", material.GetTag("KawaFLT_Features", false, ""));
	// }

	// protected static bool MaterialIsOpaque(Material material) {
	// 	return material && string.Equals("Opaque", material.GetTag("KawaFLT_RenderType", false, ""));
	// }

	// protected static bool MaterialIsOpaque(Material material) {
	// 	return material && string.Equals("Opaque", material.GetTag("KawaFLT_RenderType", false, ""));
	// }

	// protected static bool EditorHaveGeometry(Editor editor) {
	// 	foreach( var target in editor.targets )
	// 		if (MaterialIsGeometry(target as Material)) return true;
	// 	return false;
	// }

	protected static void SetKeyword(Material m, string keyword, bool state) {
		if (state)
			m.EnableKeyword(keyword);
		else
			m.DisableKeyword(keyword);
	}

	protected void SetKeywords(string keyword, bool state) {
		foreach(var obj in this.targets) {
			Material mat = (Material) obj;
			SetKeyword(mat, keyword, state);
		}
	}

	protected void SetOverrideTag(string key, string val) {
		foreach(var obj in this.targets) {
			Material mat = (Material) obj;
			mat.SetOverrideTag(key, val);
			Debug.Log(String.Format("Set {0}={1} for {2}", key, val, mat));
		}
	}

	protected string GetOverrideTag(string key) {
		HashSet<string> values = new HashSet<string>();
		string last_value = "";
		foreach(var obj in this.targets) {
			Material mat = (Material) obj;
			last_value = mat.GetTag(key, false);
			values.Add(last_value);
			// Debug.Log(values);
		}
		if (values.Count > 1)
			return String.Format("( {0} mixed values )", values.Count);
		else
			return last_value == "" ? "( not set )" : last_value;
	}


	protected virtual void SetupMaterialMeta(Material material) {
		// SetupMaterialWithBlendTemplate(material);
		SetupMaterialWithMainTexKeywords(material);
		SetupMaterialWithCutoutMode(material);
		SetupMaterialWithNormalMap(material);
		SetupMaterialWithEmissionMap(material);
		SetupMaterialWithShadingMode(material);
		SetupMaterialWithDistanceFadeMode(material);
		SetupMaterialWithFPSMode(material);

		SetupMaterialWithOutlineMode(material);
		SetupMaterialWithDisintegrationMode(material);
		SetupMaterialWithPolyColorWaveMode(material);
	}

	protected static void SetupMaterialWithBlendTemplate(Material material) {
		// derp
	}

	protected static void SetupMaterialWithMainTexKeywords(Material material) {
		bool mainTexSet = material.GetTexture("_MainTex") != null;
		bool colorMaskSet = material.GetTexture("_ColorMask") != null;
		MainTexKeywords mainTexKeywords = !mainTexSet ? MainTexKeywords.Off : (colorMaskSet ? MainTexKeywords.ColorMask : MainTexKeywords.NoMask);
		SetKeyword(material, "MAINTEX_OFF", mainTexKeywords == MainTexKeywords.Off);
		SetKeyword(material, "MAINTEX_NOMASK", mainTexKeywords == MainTexKeywords.NoMask);
		SetKeyword(material, "MAINTEX_COLORMASK", mainTexKeywords == MainTexKeywords.ColorMask);
	}

	protected static void SetupMaterialWithCutoutMode(Material material) {
		bool isCutout = Commons.MaterialCheckTag(material, "KawaFLT_RenderType", "Cutout");
		CutoutMode mode = (CutoutMode) (isCutout ? material.GetFloat("_CutoffMode") : 0);
		SetKeyword(material, "CUTOFF_CLASSIC", isCutout && mode == CutoutMode.Classic);
		SetKeyword(material, "CUTOFF_RANDOM", isCutout && mode == CutoutMode.RangeRandom);
		SetKeyword(material, "CUTOFF_PATTERN", isCutout && mode == CutoutMode.RangePattern);
	}

	protected static void SetupMaterialWithNormalMap(Material material) {
		SetKeyword(material, "NORMALMAP_ON", material.GetTexture("_BumpMap") != null);
	}

	protected static void SetupMaterialWithEmissionMap(Material material) {
		Color em_c = material.GetColor("_EmissionColor");
		bool isEm = material.GetTexture("_EmissionMap") != null || em_c.r > 0.01f || em_c.g > 0.01f || em_c.b > 0.01f;
		SetKeyword(material, "_EMISSION", isEm);
	}

	protected static void SetupMaterialWithShadingMode(Material material) {
		ShadingMode shadingMode = (ShadingMode) material.GetFloat("_Sh_Mode");
		SetKeyword(material, "SHADE_CUBEDPARADOXFLT", shadingMode == ShadingMode.CubedParadoxFLT);
		SetKeyword(material, "SHADE_KAWAFLT_LOG", shadingMode == ShadingMode.KawashirovFLTLog);
		SetKeyword(material, "SHADE_KAWAFLT_RAMP", shadingMode == ShadingMode.KawashirovFLTRamp);
		SetKeyword(material, "SHADE_KAWAFLT_SINGLE", shadingMode == ShadingMode.KawashirovFLTSingle);
		// Legacy
		SetKeyword(material, "SHADE_KAWAFLT_FLAT_LINEAR", false);
		SetKeyword(material, "SHADE_KAWAFLT_FLAT_LOG", false);
	}

	protected static void SetupMaterialWithDistanceFadeMode(Material material) {
		DistanceFadeMode mode = (DistanceFadeMode) material.GetFloat("_DstFd_Mode");
		SetKeyword(material, "DSTFD_RANGE", mode == DistanceFadeMode.Range);
		SetKeyword(material, "DSTFD_INFINITY", mode == DistanceFadeMode.Infinity);
		if (mode == DistanceFadeMode.None) {
			SetKeyword(material, "DSTFD_RANDOM_PIXEL", false);
			SetKeyword(material, "DSTFD_RANDOM_PATTERN", false);
		} else { // DSTFD_RANGE DSTFD_INFINITY
			DistanceFadeRandom random = (DistanceFadeRandom) material.GetFloat("_DstFd_Random");
			SetKeyword(material, "DSTFD_RANDOM_PIXEL", random == DistanceFadeRandom.PerPixel);
			SetKeyword(material, "DSTFD_RANDOM_PATTERN", random == DistanceFadeRandom.ScreenPattern);
		}
	}

	protected static void SetupMaterialWithFPSMode(Material material) {
		FPSMode mode = (FPSMode) material.GetFloat("_FPS_Mode");
		SetKeyword(material, "FPS_COLOR", mode == FPSMode.Color);
		SetKeyword(material, "FPS_TEX", mode == FPSMode.Texture);
		SetKeyword(material, "FPS_MESH", mode == FPSMode.Mesh);
	}

	protected static void SetupMaterialWithOutlineMode(Material material) {
		bool isGeom = Commons.CheckMaterialTagContains(material, "KawaFLT_Features", "Geometry");
		OutlineMode outlineMode = (OutlineMode) (isGeom ? material.GetFloat("_OutlineMode") : 0);
		SetKeyword(material, "NO_OUTLINE", isGeom && outlineMode == OutlineMode.None);
		SetKeyword(material, "TINTED_OUTLINE", isGeom && outlineMode == OutlineMode.Tinted);
		SetKeyword(material, "COLORED_OUTLINE", isGeom && outlineMode == OutlineMode.Colored);
	}

	public static void SetupMaterialWithDisintegrationMode(Material material) {
		bool isGeom = Commons.CheckMaterialTagContains(material, "KawaFLT_Features", "Geometry");
		DisintegrationMode mode = (DisintegrationMode) (isGeom ? material.GetFloat("_Dsntgrt_Mode") : 0);
		// SetKeyword(material, "DSNTGRT_PIXEL", mode == DisintegrationMode.PixelAndFace || mode == DisintegrationMode.Pixel);
		SetKeyword(material, "DSNTGRT_FACE", isGeom && mode == DisintegrationMode.Face); // mode == DisintegrationMode.PixelAndFace || 
	}

	public static void SetupMaterialWithPolyColorWaveMode(Material material) {
		bool isGeom = Commons.CheckMaterialTagContains(material, "KawaFLT_Features", "Geometry");
		PolyColorWaveMode mode = (PolyColorWaveMode) (isGeom ? material.GetFloat("_PCW_Mode") : 0);
		SetKeyword(material, "PCW_ON", isGeom && mode == PolyColorWaveMode.Enabled);
	}

	protected static int PopupProperty(string label, MaterialProperty property, string[] displayedOptions, Action<bool> isChanged = null) {
		int value = (int) property.floatValue;
		EditorGUI.showMixedValue = property.hasMixedValue;
		EditorGUI.BeginChangeCheck();
		value = EditorGUILayout.Popup(label, value, displayedOptions);
		if (EditorGUI.EndChangeCheck() ) {
			property.floatValue = (float) value;
			if (isChanged != null) isChanged(true);
		} else {
			if (isChanged != null) isChanged(false);
		}
		EditorGUI.showMixedValue = false;
		return value;
	}

	protected static void HelpBoxRich(string msg) {
		GUIStyle style = GUI.skin.GetStyle("HelpBox");
		bool rt = style.richText;
		style.richText = true;
		EditorGUILayout.TextArea(msg, style);
		style.richText = rt;
	}

	protected static double gcd(double a, double b) {
		if (a < b) return gcd(b, a);
		if (Math.Abs(b) < 0.001)
			return a;
		else
			return gcd(b, a - Math.Floor(a / b) * b);
	}

	protected void needUpdateMeta(bool need) {
		this.updateMeta = need || this.updateMeta;
	}

	protected MaterialProperty FindProperty(string name) {
		//return GetMaterialProperty(this.targets, name);
		return this.materialProperties[name];
	}

	protected void OnGUI_BlendMode() {
		MaterialProperty cull = this.FindProperty("_Cull");
		PopupProperty("Cull faces", cull, Commons.cullModeNames, this.needUpdateMeta);
		this.EnableInstancingField();
	}


	protected void OnGUI_Textures() {
		MaterialProperty mainTexture = this.FindProperty("_MainTex");
		MaterialProperty color = this.FindProperty("_Color");
		MaterialProperty colorMask = this.FindProperty("_ColorMask");
		MaterialProperty normalMap = this.FindProperty("_BumpMap");
		MaterialProperty emissionMap = this.FindProperty("_EmissionMap");

		EditorGUI.showMixedValue = mainTexture.hasMixedValue;
		EditorGUI.BeginChangeCheck();
			this.TexturePropertySingleLine(new GUIContent("Albedo", "Main Color Texture (RGBA)"), mainTexture, color);
		this.updateMeta = EditorGUI.EndChangeCheck() || this.updateMeta;
		EditorGUI.showMixedValue = false;

		EditorGUI.indentLevel += 1;
			bool isNotOpaque = !Commons.MaterialCheckTag(this.target, "KawaFLT_RenderType", "Opaque");
			bool isCutout = Commons.MaterialCheckTag(this.target, "KawaFLT_RenderType", "Cutout");
			bool isForceNoShadowCasting = Commons.MaterialCheckTag(this.target, "ForceNoShadowCasting", "True");

			if (isNotOpaque) {
				bool showCutoutSettings = false;
				if(!isCutout) {
					if(!isForceNoShadowCasting) {
						showCutoutSettings = true;
					}
				} else {
					showCutoutSettings = true;
				}
				if(showCutoutSettings) {
					MaterialProperty mode = this.FindProperty("_CutoffMode");
					var modeEnum = (CutoutMode) PopupProperty("Cutout Mode", mode, Commons.cutoutModeNames, this.needUpdateMeta);
					if (modeEnum == CutoutMode.Classic) {
						this.ShaderProperty(this.FindProperty("_Cutoff"), "Cutout", 2);
					} else {
						if (modeEnum == CutoutMode.RangePattern) {
							EditorGUI.BeginChangeCheck();
								this.TexturePropertySingleLine(new GUIContent("Cutout Pattern", "Cutout Pattern (R)"), this.FindProperty("_CutoffPattern"));
							this.updateMeta = EditorGUI.EndChangeCheck() || this.updateMeta;
						}
						this.ShaderProperty(this.FindProperty("_CutoffMin"), "Cutout Min", 2);
						this.ShaderProperty(this.FindProperty("_CutoffMax"), "Cutout Max", 2);
					}
				}
				
			}
			EditorGUI.showMixedValue = colorMask.hasMixedValue;
			EditorGUI.BeginChangeCheck();
				this.TexturePropertySingleLine(new GUIContent("Color Mask", "Masks Color Tint"), colorMask);
			this.updateMeta = EditorGUI.EndChangeCheck() || this.updateMeta;
			EditorGUI.showMixedValue = false;
		EditorGUI.indentLevel -= 1;
		
		EditorGUI.showMixedValue = normalMap.hasMixedValue;
		EditorGUI.BeginChangeCheck();
			this.TexturePropertySingleLine(new GUIContent( "Normal Map", "Normal Map (RGB)"), normalMap);
		this.updateMeta = EditorGUI.EndChangeCheck() || this.updateMeta;
		EditorGUI.showMixedValue = false;
		EditorGUI.indentLevel += 1;
			this.ShaderProperty(this.FindProperty("_BumpScale"), "Scale", 2);
		EditorGUI.indentLevel -= 1;
		
		EditorGUI.showMixedValue = emissionMap.hasMixedValue;
		EditorGUI.BeginChangeCheck();
		this.TexturePropertyWithHDRColor(
			new GUIContent("Emission", "Emission (RGB)"), emissionMap,
			this.FindProperty("_EmissionColor"), new ColorPickerHDRConfig(0,2,0,2), true
		);
		this.updateMeta = EditorGUI.EndChangeCheck() || this.updateMeta;
		EditorGUI.showMixedValue = false;
		
		EditorGUI.BeginChangeCheck();
		this.TextureScaleOffsetProperty(mainTexture);
		if (EditorGUI.EndChangeCheck() || this.checkValues) {
			// It's not used but keep sync
			emissionMap.textureScaleAndOffset = mainTexture.textureScaleAndOffset;
			colorMask.textureScaleAndOffset = mainTexture.textureScaleAndOffset;
			normalMap.textureScaleAndOffset = mainTexture.textureScaleAndOffset;
		}
	}

	protected void OnGUI_Shading() {
		MaterialProperty shadingMode = this.FindProperty("_Sh_Mode");
		ShadingMode shadingModeEnum = (ShadingMode) shadingMode.floatValue;

		PopupProperty("Shading Mode", shadingMode, Commons.shadingModeNames, this.needUpdateMeta);

		EditorGUI.indentLevel += 1;
		if (shadingModeEnum == ShadingMode.CubedParadoxFLT) {
			this.ShaderProperty(this.FindProperty("_Sh_Cbdprdx_Shadow"), "Shadow");
		} else if (shadingModeEnum == ShadingMode.KawashirovFLTLog ) {
			this.ShaderProperty(this.FindProperty("_Sh_Kwshrv_ShdBlnd"), "RT Shadows blending");
			this.ShaderProperty(this.FindProperty("_Sh_Kwshrv_Smth"), "Light smooth");
			EditorGUI.indentLevel += 1;
				this.ShaderProperty(this.FindProperty("_Sh_Kwshrv_Smth_Tngnt"), "Correction");
			EditorGUI.indentLevel -= 1;
			
			this.ShaderProperty(this.FindProperty("_Sh_KwshrvLog_Fltnss"), "Flatness Direct");
			EditorGUI.indentLevel += 1;
				this.ShaderProperty(this.FindProperty("_Sh_Kwshrv_BndSmth"), "Smoothness");
				this.ShaderProperty(this.FindProperty("_Sh_Kwshrv_FltLogSclA"), "Scale");
			EditorGUI.indentLevel -= 1;
			
			this.ShaderProperty(this.FindProperty("_Sh_Kwshrv_RimScl"), "Rim Effect");
			EditorGUI.indentLevel += 1;
				this.ShaderProperty(this.FindProperty("_Sh_Kwshrv_RimPwr"), "Power");
				this.ShaderProperty(this.FindProperty("_Sh_Kwshrv_RimBs"), "Bias");
			EditorGUI.indentLevel -= 1;
		} else if (shadingModeEnum == ShadingMode.KawashirovFLTRamp) {
			this.ShaderProperty(this.FindProperty("_Sh_Kwshrv_ShdBlnd"), "RT Shadows blending");
			this.TexturePropertySingleLine(
				new GUIContent("Ramp Texture", "Ramp Texture (RGB)"), this.FindProperty("_Sh_KwshrvRmp_Tex")
			);
			this.ShaderProperty(this.FindProperty("_Sh_KwshrvRmp_Pwr"), "Power");
			this.ShaderProperty(this.FindProperty("_Sh_KwshrvRmp_NdrctClr"), "Indirect Tint");
		} else if (shadingModeEnum == ShadingMode.KawashirovFLTSingle ) {
			this.ShaderProperty(this.FindProperty("_Sh_Kwshrv_ShdBlnd"), "RT Shadows blending");
			EditorGUILayout.LabelField("Sides threshold");
			EditorGUI.indentLevel += 1;
				this.ShaderProperty(this.FindProperty("_Sh_KwshrvSngl_TngntLo"), "Low");
				this.ShaderProperty(this.FindProperty("_Sh_KwshrvSngl_TngntHi"), "High");
			EditorGUI.indentLevel -= 1;
			EditorGUILayout.LabelField("Brightness");
			EditorGUI.indentLevel += 1;
				this.ShaderProperty(this.FindProperty("_Sh_KwshrvSngl_ShdLo"), "Back side (Shaded)");
				this.ShaderProperty(this.FindProperty("_Sh_KwshrvSngl_ShdHi"), "Front side (Lit)");
			EditorGUI.indentLevel -= 1;
		}
		EditorGUI.indentLevel -= 1;
	}

	protected void OnGUI_DistanceFade() { 
		MaterialProperty mode = this.FindProperty("_DstFd_Mode");
		var modeEnum = (DistanceFadeMode) PopupProperty("Distance Fade", mode, Commons.distanceFadeModeNames, this.needUpdateMeta);
		if (modeEnum != DistanceFadeMode.None) {
			EditorGUI.indentLevel += 1;

			this.ShaderProperty(this.FindProperty("_DstFd_Axis"), "Axis weights");

			MaterialProperty random = this.FindProperty("_DstFd_Random");
			if (((int) random.floatValue) == ((int) DistanceFadeRandom.PerVertex)) {
				random.floatValue = (int) DistanceFadeRandom.PerPixel;
				this.needUpdateMeta(true);
			}
			var randomEnum = (DistanceFadeRandom) PopupProperty("Random", random, Commons.distanceFadeRandomNames, this.needUpdateMeta);
			

			if (randomEnum == DistanceFadeRandom.ScreenPattern) {
				EditorGUI.indentLevel += 1;
				this.TexturePropertySingleLine(
					new GUIContent("Fade Pattern", "Fade Pattern (R)"),
					this.FindProperty("_DstFd_Pattern")
				);
				EditorGUI.indentLevel -= 1;
			}

			this.ShaderProperty(this.FindProperty("_DstFd_Near"), "Near Distance");
			if(modeEnum == DistanceFadeMode.Range)
				this.ShaderProperty(this.FindProperty("_DstFd_Far"), "Far Distance");
			this.ShaderProperty(this.FindProperty("_DstFd_AdjustPower"), "Power Adjust");
			if(modeEnum == DistanceFadeMode.Infinity)
				this.ShaderProperty(this.FindProperty("_DstFd_AdjustScale"), "Scale Adjust");
			
			
			EditorGUI.indentLevel -= 1;
		}
	}

	protected void OnGUI_FPS() {
		MaterialProperty mode = this.FindProperty("_FPS_Mode");
		var modeEnum = (FPSMode) PopupProperty("FPS Indication", mode, Commons.FPSModeNames, this.needUpdateMeta);
		if (modeEnum != FPSMode.None) {
			EditorGUI.indentLevel += 1;

			this.ShaderProperty(this.FindProperty("_FPS_TLo"), "Low FPS tint");
			this.ShaderProperty(this.FindProperty("_FPS_THi"), "High FPS tint");
			
			EditorGUI.indentLevel -= 1;
		}
	}

	private void OnGUI_Outline() {
		MaterialProperty outlineMode = this.FindProperty("_OutlineMode");
		OutlineMode outlineModeEnum = (OutlineMode) PopupProperty("Outline Mode", outlineMode, Commons.outlineModeNames, this.needUpdateMeta);
		if (outlineModeEnum == OutlineMode.Tinted || outlineModeEnum == OutlineMode.Colored) {
			this.ShaderProperty(this.FindProperty("_outline_color"), "Color", 2);
			this.ShaderProperty(this.FindProperty("_outline_width"), new GUIContent("Width", "Outline width in cm"), 2);
			this.ShaderProperty(this.FindProperty("_outline_bias"), "Bias", 2);
		}
	}

	private void OnGUI_Disintegration() {
		MaterialProperty disintegrationMode = this.FindProperty("_Dsntgrt_Mode");
		var disintegrationModeEnum = (DisintegrationMode) PopupProperty("Disintegration", disintegrationMode, Commons.disintegrationModeNames, this.needUpdateMeta);
		if (disintegrationModeEnum != DisintegrationMode.None) {
			EditorGUILayout.LabelField("I don't feel so good...");
			EditorGUI.indentLevel += 1;
			
			EditorGUILayout.LabelField("General equation of a Plane (XYZ is normal, W is offset):");
			EditorGUI.indentLevel += 1;
				this.ShaderProperty(this.FindProperty("_Dsntgrt_Plane"), "");
			EditorGUI.indentLevel -= 1;
			
			if (disintegrationModeEnum == DisintegrationMode.Face ) { // || disintegrationModeEnum == DisintegrationMode.PixelAndFace
				EditorGUILayout.LabelField("Geometry (triangles) fade:");
				EditorGUI.indentLevel += 1;
					this.ShaderProperty(this.FindProperty("_Dsntgrt_TriSpreadFactor"), "Spread Factor");
					this.ShaderProperty(this.FindProperty("_Dsntgrt_TriSpreadAccel"), "Spread Accel");
					this.ShaderProperty(this.FindProperty("_Dsntgrt_TriDecayFar"), "Far Distance");
					this.ShaderProperty(this.FindProperty("_Dsntgrt_TriPowerAdjust"), "Power Adjust");
					this.ShaderProperty(this.FindProperty("_Dsntgrt_Tint"), "Decay tint");
					if (this.haveTessellation) {
						this.ShaderProperty(this.FindProperty("_Dsntgrt_Tsltn"), "Tessellation factor");
					}
				EditorGUI.indentLevel -= 1;
			}
			
			EditorGUI.indentLevel -= 1;
		}
	}

	private void OnGUI_PCW() {
		MaterialProperty polyColorWaveMode = this.FindProperty("_PCW_Mode");
		var polyColorWaveModeEnum = (PolyColorWaveMode) PopupProperty("Poly Color Wave", polyColorWaveMode, Commons.polyColorWaveModeNames, this.needUpdateMeta);
		if (polyColorWaveModeEnum != PolyColorWaveMode.None) {
			EditorGUI.indentLevel += 1;

			EditorGUILayout.LabelField("Wave timings:");
			EditorGUI.indentLevel += 1;

				MaterialProperty time_low = this.FindProperty("_PCW_WvTmLo");
				this.ShaderProperty(time_low, "Hidden");
				float time_low_f = time_low.floatValue;

				MaterialProperty time_asc = this.FindProperty("_PCW_WvTmAs");
				this.ShaderProperty(time_asc, "Fade-in");
				float time_asc_f = time_asc.floatValue;

				MaterialProperty time_high = this.FindProperty("_PCW_WvTmHi");
				this.ShaderProperty(time_high, "Shown");
				float time_high_f = time_high.floatValue;

				MaterialProperty time_desc = this.FindProperty("_PCW_WvTmDe");
				this.ShaderProperty(time_desc, "Fade-out");
				float time_desc_f = time_desc.floatValue;

				float time_period = time_low_f + time_asc_f + time_high_f + time_desc_f;
				AnimationCurve time_curve = new AnimationCurve();
				time_curve.AddKey(0, 0);
				time_curve.AddKey(time_low_f, 0);
				time_curve.AddKey(time_low_f + time_asc_f, 1);
				time_curve.AddKey(time_low_f + time_asc_f + time_high_f, 1);
				time_curve.AddKey(time_period, 0);
				for(int i = 0; i < time_curve.keys.Length; ++i) {
					AnimationUtility.SetKeyLeftTangentMode(time_curve, i, AnimationUtility.TangentMode.Linear);
					AnimationUtility.SetKeyRightTangentMode(time_curve, i, AnimationUtility.TangentMode.Linear);
				}
				// for(int i = 0; i < time_curve.length; ++i) {
				// 	time_curve.SmoothTangents(i, 1);
				// }
				EditorGUILayout.CurveField("Preview amplitude (read-only)", time_curve);
				HelpBoxRich(String.Format("Time for singe wave cycle: <b>{0:f}</b> sec. ", time_period));
				this.ShaderProperty(this.FindProperty("_PCW_WvTmRnd"), "Random per tris");

				EditorGUILayout.LabelField("Time offset from UV0 (XY) and UV1 (ZW):");
				this.VectorProperty(FindProperty("_PCW_WvTmUV"), "");
				
				EditorGUILayout.LabelField("Time offset from mesh-space coords: ");
				this.VectorProperty(FindProperty("_PCW_WvTmVtx"), "");

			EditorGUI.indentLevel -= 1;

			EditorGUILayout.LabelField("Wave coloring:");
			EditorGUI.indentLevel += 1;

				MaterialProperty rainbowTime = this.FindProperty("_PCW_RnbwTm");

				this.ShaderProperty(this.FindProperty("_PCW_Em"), "Emissiveness");
				this.ShaderProperty(this.FindProperty("_PCW_Color"), "Color");
				this.ShaderProperty(rainbowTime, "Rainbow time");
				this.ShaderProperty(this.FindProperty("_PCW_RnbwTmRnd"), "Rainbow time random");
				this.ShaderProperty(this.FindProperty("_PCW_RnbwStrtn"), "Rainbow saturation");
				this.ShaderProperty(this.FindProperty("_PCW_RnbwBrghtnss"), "Rainbow brightness");
				this.ShaderProperty(this.FindProperty("_PCW_Mix"), "Color vs. Rainbow");

			EditorGUI.indentLevel -= 1;

			float time_rainbow = rainbowTime.floatValue;
			double gcd_t = gcd(time_rainbow, time_period);
			double lcm_t = (time_rainbow * time_period) / gcd_t;
			HelpBoxRich(String.Format(
				"Period of the wave <b>{0:f1}</b> sec. and period of Rainbow <b>{1:f1}</b> sec. produces total cycle of ~<b>{2:f1}</b> sec. (GCD: ~<b>{3:f}</b>)",
				time_period, time_rainbow, lcm_t, gcd_t
			));

			EditorGUI.indentLevel -= 1;
		}

	}

	protected bool temporaryBlock = false;

	public override void OnEnable() {
		base.OnEnable();

		this.haveGeometry = Commons.CheckAllMaterialsTagContains(this.targets, "KawaFLT_Features", "Geometry");
		this.haveTessellation = Commons.CheckAllMaterialsTagContains(this.targets, "KawaFLT_Features", "Tessellation");

		this.materialProperties = new Dictionary<string, MaterialProperty>();
		var shaders = new HashSet<Shader>();
		var names = new HashSet<string>();
		int materials = 0;

		foreach(object target in this.targets) {
			Material material = target as Material;
			if (material != null) {
				shaders.Add(material.shader);
				++materials;
			}
		}

		foreach(Shader shader in shaders) {
			int count = ShaderUtil.GetPropertyCount(shader);
			for(int i = 0; i < count; ++i) {
				names.Add(ShaderUtil.GetPropertyName(shader, i));
			}
		}

		if (shaders.Count > 1) {
			temporaryBlock = true;
		} else {
			foreach(string name in names) {
				this.materialProperties[name] = GetMaterialProperty(this.targets, name);
			}
		}

		Debug.Log(String.Format(
			"Tracking {0} properties form {1} names from {2} shaders from {3} meterials from {4} targets.",
			this.materialProperties.Count, names.Count, shaders.Count, materials, this.targets.Length
		));
	}

	// public override void OnDisable() {
	// 	base.OnDisable();
	// 	Debug.Log("OnDisable", this);
	// }

	public override void OnInspectorGUI() {
		if (!this.isVisible) return;


		if (temporaryBlock) {
			HelpBoxRich("Multi-select from different shaders is not yet work. Please, select materials of same shader type.");
			return;
		}

		this.updateMeta = false;
		this.checkValues = GUILayout.Button("Force fix values and compilation keywords");
		if (this.checkValues) {
			this.PropertiesChanged(); // TODO
		}

		if (this.targets.Length > 1) {
			HelpBoxRich("Multi-select is not yet properly work, it can break your materals! Not yet recomended to use.");
		}

		EditorGUILayout.Space();
		OnGUI_BlendMode();

		EditorGUILayout.Space();
		OnGUI_Textures();

		EditorGUILayout.Space();
		OnGUI_Shading();

		if(this.haveGeometry) {
			EditorGUILayout.Space();
			OnGUI_Outline();
		}

		EditorGUILayout.Space();
		OnGUI_DistanceFade();

		EditorGUILayout.Space();
		OnGUI_FPS();
		
		if(this.haveGeometry) {
			EditorGUILayout.Space();
			OnGUI_Disintegration();

			EditorGUILayout.Space();
			OnGUI_PCW();
		}


		if(this.haveTessellation) {
			EditorGUILayout.Space();
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Tessellation:");
			this.ShaderProperty(this.FindProperty("_Tsltn_Uni"), "Uniform factor");
			this.ShaderProperty(this.FindProperty("_Tsltn_Nrm"), "Factor from curvness");
			this.ShaderProperty(this.FindProperty("_Tsltn_Inside"), "Inside multiplier");
		}

		KawaEditorUtil.ShaderEditorFooter();

		if (this.updateMeta || this.checkValues) {
			foreach (var obj in this.targets) {
				Material mat = (Material) obj;
				SetupMaterialMeta(mat);
			}
		}

	}

}

}