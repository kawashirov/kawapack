using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;
using System;

public class KawaFLTInspector : ShaderGUI {

	protected static string[] blendModeNames = Enum.GetNames(typeof(BlendMode));
	protected static string[] cullModeNames = Enum.GetNames(typeof(CullMode));

	protected enum TessDomain { Triangles, Quads }
	protected enum TessPartioning { integer, fractional_even, fractional_odd, pow2 }
	//protected enum TessPhong { None, fractional_even, fractional_odd, pow2 }
	protected static string[] TessDomainNames = Enum.GetNames(typeof(TessDomain));
	protected static string[] TessPartioningNames = Enum.GetNames(typeof(TessPartioning));

	// protected enum BlendTemplate { Opaque, Cutout, Fade, Transparent, Custom }
	// protected enum BlendKeywords { None, AlphaTest, AlphaBlend, AlphaPreMultiply }
	protected enum MainTexKeywords { Off, NoMask, ColorMask }
	protected enum CutoutMode { Classic, RangeRandom, RangePattern }
	// protected static string[] blendTemplateNames = Enum.GetNames(typeof(BlendTemplate));
	// protected static string[] blendKeywordsNames = Enum.GetNames(typeof(BlendKeywords));
	protected static string[] mainTexKeywordsNames = Enum.GetNames(typeof(MainTexKeywords));
	protected static string[] cutoutModeNames = Enum.GetNames(typeof(CutoutMode));

	protected enum ShadingMode { CubedParadoxFLT, KawashirovFLTDiffuse, KawashirovFLTRamp }
	// protected enum ShadingKawashirovSmooth { None, Four, Six, Eight }
	// protected enum ShadingKawashirovFlatMode { None, Linear, Logarithmic }
	protected static string[] shadingModeNames = new string[] {
		"CubedParadox FLT (Для любителей навернуть говнеца)", "Kawashirov FLT Diffuse-based (Пластилиновый™)", "Kawashirov FLT Ramp-based (In Dev)"
	};
	//protected static string[] shadingKawashirovSmoothNames = Enum.GetNames(typeof(ShadingKawashirovSmooth));
	// protected static string[] shadingKawashirovFlatModeNames = Enum.GetNames(typeof(ShadingKawashirovFlatMode));

	protected enum DistanceFadeMode { None, Range, Infinity }
	protected enum DistanceFadeRandom { PerPixel, PerVertex, ScreenPattern }
	protected static string[] distanceFadeModeNames = Enum.GetNames(typeof(DistanceFadeMode));
	protected static string[] distanceFadeRandomNames = Enum.GetNames(typeof(DistanceFadeRandom));

	protected enum FPSMode { None, Color, Texture, Mesh }
	protected static string[] FPSModeNames = Enum.GetNames(typeof(FPSMode));

	public enum OutlineMode { None, Tinted, Colored }
	protected static string[] outlineModeNames = Enum.GetNames(typeof(OutlineMode));

	public enum DisintegrationMode { None, Pixel, Face, PixelAndFace }
	protected static string[] disintegrationModeNames = Enum.GetNames(typeof(DisintegrationMode));

	public enum PolyColorWaveMode { None, Enabled }
	protected static string[] polyColorWaveModeNames = Enum.GetNames(typeof(PolyColorWaveMode));

	static KawaFLTInspector() {
		string rem = "[ Feature removed, do not use it ]";
		distanceFadeRandomNames[1] = rem;
		disintegrationModeNames[1] = rem;
		disintegrationModeNames[3] = rem;
	}

	protected MaterialEditor materialEditor = null;
	protected MaterialProperty[] materialProperties = null;
	protected bool updateMeta = false;
	protected bool checkValues = false;
	protected bool haveGeometry = false;
	protected bool haveTessellation = false;

	protected static bool MaterialCheckTag(object material, string tag, string value) {
		Material m = material as Material;
		string tag_v = m ? m.GetTag(tag, false, "") : null;
		// Debug.Log(String.Format("{0}: {1}={2}", material, tag, tag_v));
		return m && string.Equals(value, tag_v, StringComparison.InvariantCultureIgnoreCase);
	}

	protected static bool MaterialCheckTagContains(object material, string tag, string value) {
		Material m = material as Material;
		if (!m) return false;
		string tag_v = m.GetTag(tag, false, "");
		return tag_v.Split(',').ToList<string>().Any(v => string.Equals(value, v, StringComparison.InvariantCultureIgnoreCase));
	}

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

	protected static void SetKeywords(MaterialEditor materialEditor, string keyword, bool state) {
		foreach(var obj in materialEditor.targets) {
			Material mat = (Material) obj;
			SetKeyword(mat, keyword, state);
		}
	}

	protected static void SetOverrideTag(MaterialEditor materialEditor, string key, string val) {
		foreach(var obj in materialEditor.targets) {
			Material mat = (Material) obj;
			mat.SetOverrideTag(key, val);
			Debug.Log(String.Format("Set {0}={1} for {2}", key, val, mat));
		}
	}

	protected static string GetOverrideTag(MaterialEditor materialEditor, string key) {
		HashSet<string> values = new HashSet<string>();
		string last_value = "";
		foreach(var obj in materialEditor.targets) {
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
		bool isCutout = MaterialCheckTag(material, "KawaFLT_RenderType", "Cutout");
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
		SetKeyword(material, "SHADE_KAWAFLT_DIFFUSE", shadingMode == ShadingMode.KawashirovFLTDiffuse);
		SetKeyword(material, "SHADE_KAWAFLT_RAMP", shadingMode == ShadingMode.KawashirovFLTRamp);
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
		bool isGeom = MaterialCheckTagContains(material, "KawaFLT_Features", "Geometry");
		OutlineMode outlineMode = (OutlineMode) (isGeom ? material.GetFloat("_OutlineMode") : 0);
		SetKeyword(material, "NO_OUTLINE", isGeom && outlineMode == OutlineMode.None);
		SetKeyword(material, "TINTED_OUTLINE", isGeom && outlineMode == OutlineMode.Tinted);
		SetKeyword(material, "COLORED_OUTLINE", isGeom && outlineMode == OutlineMode.Colored);
	}

	public static void SetupMaterialWithDisintegrationMode(Material material) {
		bool isGeom = MaterialCheckTagContains(material, "KawaFLT_Features", "Geometry");
		DisintegrationMode mode = (DisintegrationMode) (isGeom ? material.GetFloat("_Dsntgrt_Mode") : 0);
		// SetKeyword(material, "DSNTGRT_PIXEL", mode == DisintegrationMode.PixelAndFace || mode == DisintegrationMode.Pixel);
		SetKeyword(material, "DSNTGRT_FACE", isGeom && mode == DisintegrationMode.Face); // mode == DisintegrationMode.PixelAndFace || 
	}

	public static void SetupMaterialWithPolyColorWaveMode(Material material) {
		bool isGeom = MaterialCheckTagContains(material, "KawaFLT_Features", "Geometry");
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

	protected void OnGUI_BlendMode() {
		MaterialProperty cull = FindProperty("_Cull", this.materialProperties);
		PopupProperty("Cull faces", cull, cullModeNames, this.needUpdateMeta);
		this.materialEditor.EnableInstancingField();
	}

	protected void OnGUI_Textures() {
		MaterialProperty mainTexture = FindProperty("_MainTex", this.materialProperties);
		MaterialProperty color = FindProperty("_Color", this.materialProperties);
		MaterialProperty colorMask = FindProperty("_ColorMask", this.materialProperties);
		MaterialProperty normalMap = FindProperty("_BumpMap", this.materialProperties);
		MaterialProperty emissionMap = FindProperty("_EmissionMap", this.materialProperties);

		EditorGUI.showMixedValue = mainTexture.hasMixedValue;
		EditorGUI.BeginChangeCheck();
		materialEditor.TexturePropertySingleLine(
			new GUIContent("Main Texture", "Main Color Texture (RGBA)"), mainTexture, color
		);
		this.updateMeta = EditorGUI.EndChangeCheck() || this.updateMeta;
		EditorGUI.showMixedValue = false;

		EditorGUI.indentLevel += 1;
			bool isNotOpaque = !MaterialCheckTag(this.materialEditor.target, "KawaFLT_RenderType", "Opaque");
			bool isCutout = MaterialCheckTag(this.materialEditor.target, "KawaFLT_RenderType", "Cutout");
			bool isForceNoShadowCasting = MaterialCheckTag(this.materialEditor.target, "ForceNoShadowCasting", "True");

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
					MaterialProperty mode = FindProperty("_CutoffMode", this.materialProperties);
					var modeEnum = (CutoutMode) PopupProperty("Cutout Mode", mode, cutoutModeNames, this.needUpdateMeta);
					if (modeEnum == CutoutMode.Classic) {
						materialEditor.ShaderProperty(FindProperty("_Cutoff", this.materialProperties), "Cutout", 2);
					} else {
						if (modeEnum == CutoutMode.RangePattern) {
							materialEditor.TexturePropertySingleLine(
								new GUIContent("Cutout Pattern", "Cutout Pattern (R)"),
								FindProperty("_CutoffPattern", this.materialProperties)
							);
						}
						materialEditor.ShaderProperty(FindProperty("_CutoffMin", this.materialProperties), "Cutout Min", 2);
						materialEditor.ShaderProperty(FindProperty("_CutoffMax", this.materialProperties), "Cutout Max", 2);
					}
				}
				
			}
			EditorGUI.showMixedValue = colorMask.hasMixedValue;
			EditorGUI.BeginChangeCheck();
			materialEditor.TexturePropertySingleLine(
				new GUIContent("Color Mask", "Masks Color Tint"), colorMask
			);
			this.updateMeta = EditorGUI.EndChangeCheck() || this.updateMeta;
			EditorGUI.showMixedValue = false;
		EditorGUI.indentLevel -= 1;
		
		EditorGUI.showMixedValue = normalMap.hasMixedValue;
		EditorGUI.BeginChangeCheck();
		materialEditor.TexturePropertySingleLine(new GUIContent( "Normal Map", "Normal Map (RGB)"), normalMap);
		this.updateMeta = EditorGUI.EndChangeCheck() || this.updateMeta;
		EditorGUI.showMixedValue = false;
		EditorGUI.indentLevel += 1;
			materialEditor.ShaderProperty(FindProperty("_BumpScale", this.materialProperties), "Scale", 2);
		EditorGUI.indentLevel -= 1;
		
		EditorGUI.showMixedValue = emissionMap.hasMixedValue;
		EditorGUI.BeginChangeCheck();
		materialEditor.TexturePropertyWithHDRColor(
			new GUIContent("Emission", "Emission (RGB)"), emissionMap,
			FindProperty("_EmissionColor", this.materialProperties), new ColorPickerHDRConfig(0,2,0,2), true
		);
		this.updateMeta = EditorGUI.EndChangeCheck() || this.updateMeta;
		EditorGUI.showMixedValue = false;
		
		EditorGUI.BeginChangeCheck();
		materialEditor.TextureScaleOffsetProperty(mainTexture);
		if (EditorGUI.EndChangeCheck() || this.checkValues) {
			// It's not used but keep sync
			emissionMap.textureScaleAndOffset = mainTexture.textureScaleAndOffset;
			colorMask.textureScaleAndOffset = mainTexture.textureScaleAndOffset;
			normalMap.textureScaleAndOffset = mainTexture.textureScaleAndOffset;
		}
	}

	protected void OnGUI_Shading() {
		MaterialProperty shadingMode = FindProperty("_Sh_Mode", this.materialProperties);
		ShadingMode shadingModeEnum = (ShadingMode) shadingMode.floatValue;

		PopupProperty("Shading Mode", shadingMode, shadingModeNames, this.needUpdateMeta);

		EditorGUI.indentLevel += 1;
		if (shadingModeEnum == ShadingMode.CubedParadoxFLT) {
			materialEditor.ShaderProperty(FindProperty("_Sh_Cbdprdx_Shadow", this.materialProperties), "Shadow");
		} else if (shadingModeEnum == ShadingMode.KawashirovFLTDiffuse ) {
			materialEditor.ShaderProperty(FindProperty("_Sh_Kwshrv_Smth", this.materialProperties), "Light smooth");
			EditorGUI.indentLevel += 1;
				materialEditor.ShaderProperty(FindProperty("_Sh_Kwshrv_Smth_Tngnt", this.materialProperties), "Correction");
			EditorGUI.indentLevel -= 1;
			
			materialEditor.ShaderProperty(FindProperty("_Sh_Kwshrv_FltFctr", this.materialProperties), "Flatness");
			EditorGUI.indentLevel += 1;
				materialEditor.ShaderProperty(FindProperty("_Sh_Kwshrv_BndSmth", this.materialProperties), "Smoothness");
				materialEditor.ShaderProperty(FindProperty("_Sh_Kwshrv_FltLogSclA", this.materialProperties), "Scale");
			EditorGUI.indentLevel -= 1;
			
			materialEditor.ShaderProperty(FindProperty("_Sh_Kwshrv_RimScl", this.materialProperties), "Rim Effect");
			EditorGUI.indentLevel += 1;
				materialEditor.ShaderProperty(FindProperty("_Sh_Kwshrv_RimPwr", this.materialProperties), "Power");
				materialEditor.ShaderProperty(FindProperty("_Sh_Kwshrv_RimBs", this.materialProperties), "Bias");
			EditorGUI.indentLevel -= 1;
		} else if (shadingModeEnum == ShadingMode.KawashirovFLTRamp) {
			materialEditor.TexturePropertySingleLine(
				new GUIContent("Ramp Texture", "Ramp Texture (RGB)"), FindProperty("_Sh_KwshrvRmp_Tex", this.materialProperties)
			);
			materialEditor.ShaderProperty(FindProperty("_Sh_KwshrvRmp_Pwr", this.materialProperties), "Power");
			materialEditor.ShaderProperty(FindProperty("_Sh_KwshrvRmp_NdrctClr", this.materialProperties), "Indirect Tint");
		}
		EditorGUI.indentLevel -= 1;
	}

	protected void OnGUI_DistanceFade() { 
		MaterialProperty mode = FindProperty("_DstFd_Mode", this.materialProperties);
		var modeEnum = (DistanceFadeMode) PopupProperty("Distance Fade", mode, distanceFadeModeNames, this.needUpdateMeta);
		if (modeEnum != DistanceFadeMode.None) {
			EditorGUI.indentLevel += 1;

			materialEditor.ShaderProperty(FindProperty("_DstFd_Axis", this.materialProperties), "Axis weights");

			MaterialProperty random = FindProperty("_DstFd_Random", this.materialProperties);
			if (((int) random.floatValue) == ((int) DistanceFadeRandom.PerVertex)) {
				random.floatValue = (int) DistanceFadeRandom.PerPixel;
				this.needUpdateMeta(true);
			}
			var randomEnum = (DistanceFadeRandom) PopupProperty("Random", random, distanceFadeRandomNames, this.needUpdateMeta);
			

			if (randomEnum == DistanceFadeRandom.ScreenPattern) {
				EditorGUI.indentLevel += 1;
				materialEditor.TexturePropertySingleLine(
					new GUIContent("Fade Pattern", "Fade Pattern (R)"),
					FindProperty("_DstFd_Pattern", this.materialProperties)
				);
				EditorGUI.indentLevel -= 1;
			}

			materialEditor.ShaderProperty(FindProperty("_DstFd_Near", this.materialProperties), "Near Distance");
			if(modeEnum == DistanceFadeMode.Range)
				materialEditor.ShaderProperty(FindProperty("_DstFd_Far", this.materialProperties), "Far Distance");
			materialEditor.ShaderProperty(FindProperty("_DstFd_AdjustPower", this.materialProperties), "Power Adjust");
			if(modeEnum == DistanceFadeMode.Infinity)
				materialEditor.ShaderProperty(FindProperty("_DstFd_AdjustScale", this.materialProperties), "Scale Adjust");
			
			
			EditorGUI.indentLevel -= 1;
		}
	}

	protected void OnGUI_FPS() {
		MaterialProperty mode = FindProperty("_FPS_Mode", this.materialProperties);
		var modeEnum = (FPSMode) PopupProperty("FPS Indication", mode, FPSModeNames, this.needUpdateMeta);
		if (modeEnum != FPSMode.None) {
			EditorGUI.indentLevel += 1;

			materialEditor.ShaderProperty(FindProperty("_FPS_TLo", this.materialProperties), "Low FPS tint");
			materialEditor.ShaderProperty(FindProperty("_FPS_THi", this.materialProperties), "High FPS tint");
			
			EditorGUI.indentLevel -= 1;
		}
	}

	private void OnGUI_Outline() {
		MaterialProperty outlineMode = FindProperty("_OutlineMode", this.materialProperties);
		OutlineMode outlineModeEnum = (OutlineMode) PopupProperty("Outline Mode", outlineMode, outlineModeNames, this.needUpdateMeta);
		if (outlineModeEnum == OutlineMode.Tinted || outlineModeEnum == OutlineMode.Colored) {
			materialEditor.ShaderProperty(FindProperty("_outline_color", this.materialProperties), "Color", 2);
			materialEditor.ShaderProperty(FindProperty("_outline_width", this.materialProperties), new GUIContent("Width", "Outline width in cm"), 2);
			materialEditor.ShaderProperty(FindProperty("_outline_bias", this.materialProperties), "Bias", 2);
		}
	}

	private void OnGUI_Disintegration() {
		MaterialProperty disintegrationMode = FindProperty("_Dsntgrt_Mode", this.materialProperties);
		var disintegrationModeEnum = (DisintegrationMode) PopupProperty("Disintegration", disintegrationMode, disintegrationModeNames, this.needUpdateMeta);
		if (disintegrationModeEnum != DisintegrationMode.None) {
			EditorGUILayout.LabelField("I don't feel so good...");
			EditorGUI.indentLevel += 1;
			
			EditorGUILayout.LabelField("General equation of a Plane (XYZ is normal, W is offset):");
			EditorGUI.indentLevel += 1;
				materialEditor.ShaderProperty(FindProperty("_Dsntgrt_Plane", this.materialProperties), "");
			EditorGUI.indentLevel -= 1;
			
			if (disintegrationModeEnum == DisintegrationMode.Face ) { // || disintegrationModeEnum == DisintegrationMode.PixelAndFace
				EditorGUILayout.LabelField("Geometry (triangles) fade:");
				EditorGUI.indentLevel += 1;
					materialEditor.ShaderProperty(FindProperty("_Dsntgrt_TriSpreadFactor", this.materialProperties), "Spread Factor");
					materialEditor.ShaderProperty(FindProperty("_Dsntgrt_TriSpreadAccel", this.materialProperties), "Spread Accel");
					// materialEditor.ShaderProperty(FindProperty("_Dsntgrt_TriDecayNear", this.materialProperties), "Near Distance");
					materialEditor.ShaderProperty(FindProperty("_Dsntgrt_TriDecayFar", this.materialProperties), "Far Distance");
					materialEditor.ShaderProperty(FindProperty("_Dsntgrt_TriPowerAdjust", this.materialProperties), "Power Adjust");
					materialEditor.ShaderProperty(FindProperty("_Dsntgrt_Tint", this.materialProperties), "Decay tint");
					if (this.haveTessellation) {
						materialEditor.ShaderProperty(FindProperty("_Dsntgrt_Tsltn", this.materialProperties), "Tessellation factor");
					}
				EditorGUI.indentLevel -= 1;
			}
			
			EditorGUI.indentLevel -= 1;
		}
	}

	private void OnGUI_PCW() {
		MaterialProperty polyColorWaveMode = FindProperty("_PCW_Mode", this.materialProperties);
		var polyColorWaveModeEnum = (PolyColorWaveMode) PopupProperty("Poly Color Wave", polyColorWaveMode, polyColorWaveModeNames, this.needUpdateMeta);
		if (polyColorWaveModeEnum != PolyColorWaveMode.None) {
			EditorGUI.indentLevel += 1;

			EditorGUILayout.LabelField("Wave timings:");
			EditorGUI.indentLevel += 1;

				MaterialProperty time_low = FindProperty("_PCW_WvTmLo", this.materialProperties);
				materialEditor.ShaderProperty(time_low, "Hidden");
				float time_low_f = time_low.floatValue;

				MaterialProperty time_asc = FindProperty("_PCW_WvTmAs", this.materialProperties);
				materialEditor.ShaderProperty(time_asc, "Fade-in");
				float time_asc_f = time_asc.floatValue;

				MaterialProperty time_high = FindProperty("_PCW_WvTmHi", this.materialProperties);
				materialEditor.ShaderProperty(time_high, "Shown");
				float time_high_f = time_high.floatValue;

				MaterialProperty time_desc = FindProperty("_PCW_WvTmDe", this.materialProperties);
				materialEditor.ShaderProperty(time_desc, "Fade-out");
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
				materialEditor.ShaderProperty(FindProperty("_PCW_WvTmRnd", this.materialProperties), "Random per tris");

				EditorGUILayout.LabelField("Time offset from UV0 (XY) and UV1 (ZW):");
				materialEditor.VectorProperty(FindProperty("_PCW_WvTmUV", this.materialProperties), "");
				
				EditorGUILayout.LabelField("Time offset from mesh-space coords: ");
				materialEditor.VectorProperty(FindProperty("_PCW_WvTmVtx", this.materialProperties), "");

			EditorGUI.indentLevel -= 1;

			EditorGUILayout.LabelField("Wave coloring:");
			EditorGUI.indentLevel += 1;

				MaterialProperty rainbowTime = FindProperty("_PCW_RnbwTm", this.materialProperties);

				materialEditor.ShaderProperty(FindProperty("_PCW_Em", this.materialProperties), "Emissiveness");
				materialEditor.ShaderProperty(FindProperty("_PCW_Color", this.materialProperties), "Color");
				materialEditor.ShaderProperty(rainbowTime, "Rainbow time");
				materialEditor.ShaderProperty(FindProperty("_PCW_RnbwTmRnd", this.materialProperties), "Rainbow time random");
				materialEditor.ShaderProperty(FindProperty("_PCW_RnbwStrtn", this.materialProperties), "Rainbow saturation");
				materialEditor.ShaderProperty(FindProperty("_PCW_RnbwBrghtnss", this.materialProperties), "Rainbow brightness");
				materialEditor.ShaderProperty(FindProperty("_PCW_Mix", this.materialProperties), "Color vs. Rainbow");

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

	public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] materialProperties) {
		this.materialEditor = materialEditor;
		this.materialProperties = materialProperties;
		this.updateMeta = false;
		this.checkValues = false;
		// this.blendKeywords = BlendKeywords.None;

		this.haveGeometry = MaterialCheckTagContains(materialEditor.target, "KawaFLT_Features", "Geometry");
		this.haveTessellation = MaterialCheckTagContains(materialEditor.target, "KawaFLT_Features", "Tessellation");

		if (materialEditor.targets.Length > 1) {
			HelpBoxRich("Multi-select is not properly work, it can break your materals! Not recomended to use.");
		}
		
		//EditorGUIUtility.labelWidth = 0f;
		
		this.checkValues = GUILayout.Button("Force fix values and compilation keywords");
		if (this.checkValues) {
			materialEditor.PropertiesChanged(); // TODO
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
			materialEditor.ShaderProperty(FindProperty("_Tsltn_Uni", this.materialProperties), "Uniform factor");
			materialEditor.ShaderProperty(FindProperty("_Tsltn_Nrm", this.materialProperties), "Factor from curvness");
			materialEditor.ShaderProperty(FindProperty("_Tsltn_Inside", this.materialProperties), "Inside multiplier");
		}

		KawaEditorUtil.ShaderEditorFooter();

		if (this.updateMeta || this.checkValues) {
			foreach (var obj in materialEditor.targets) {
				Material mat = (Material) obj;
				SetupMaterialMeta(mat);
			}
		}

	}


}
