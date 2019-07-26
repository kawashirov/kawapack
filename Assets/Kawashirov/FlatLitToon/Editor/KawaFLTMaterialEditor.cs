using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;
using System;
using Kawashirov.FLT;

public class KawaFLTMaterialEditor : MaterialEditor {

	protected bool haveGeometry = false;
	protected bool haveTessellation = false;
	protected IDictionary<string, MaterialProperty> materialProperties;

	protected static void HelpBoxRich(string msg)
	{
		var style = GUI.skin.GetStyle("HelpBox");
		var rt = style.richText;
		style.richText = true;
		EditorGUILayout.TextArea(msg, style);
		style.richText = rt;
	}

	protected static double gcd(double a, double b)
	{
		return a < b ? gcd(b, a) : Math.Abs(b) < 0.001 ? a : gcd(b, a - (Math.Floor(a / b) * b));
	}

	protected MaterialProperty FindProperty(string name)
	{
		//return GetMaterialProperty(this.targets, name);
		MaterialProperty mp = null;
		this.materialProperties.TryGetValue(name, out mp);
		return mp;
	}

	protected void OnGUI_BlendMode()
	{
		this.EnableInstancingField();
	}


	protected void OnGUI_Textures()
	{
		var isMainTexNoMask = Commons.MaterialCheckTag(this.target, "KawaFLT_Feature_MainTex", "NoMask");
		var isMainTexColorMask = Commons.MaterialCheckTag(this.target, "KawaFLT_Feature_MainTex", "ColorMask");

		var isCutoutClassic = Commons.MaterialCheckTag(this.target, "KawaFLT_Feature_Cutout", "Classic");
		var isCutoutRangeRandom = Commons.MaterialCheckTag(this.target, "KawaFLT_Feature_Cutout", "RangeRandom");
		var isCutoutRangePattern = Commons.MaterialCheckTag(this.target, "KawaFLT_Feature_Cutout", "RangePattern");

		var isNormalMapOn = Commons.MaterialCheckTag(this.target, "KawaFLT_Feature_NormalMap", "True");

		var isEmissionAlbedoNoMask = Commons.MaterialCheckTag(this.target, "KawaFLT_Feature_Emission", "AlbedoNoMask");
		var isEmissionAlbedoMask = Commons.MaterialCheckTag(this.target, "KawaFLT_Feature_Emission", "AlbedoMask");
		var isEmissionAlbedoCustom = Commons.MaterialCheckTag(this.target, "KawaFLT_Feature_Emission", "Custom");

		var mainTexture = this.FindProperty("_MainTex");
		var colorMask = this.FindProperty("_ColorMask");
		var normalMap = this.FindProperty("_BumpMap");
		var emissionMap = this.FindProperty("_EmissionMap");

		if (isMainTexNoMask || isMainTexColorMask) {
			var color = this.FindProperty("_Color");
			EditorGUI.showMixedValue = mainTexture.hasMixedValue;
			this.TexturePropertySingleLine(new GUIContent("Albedo", "Main Color Texture (RGBA)"), mainTexture, color);
			EditorGUI.showMixedValue = false;
		}

		EditorGUI.indentLevel += 1;

		if (isCutoutClassic) {
			this.ShaderProperty(this.FindProperty("_Cutoff"), "Cutout", 2);
		} else if (isCutoutRangeRandom || isCutoutRangePattern) {
			if (isCutoutRangePattern) {
				this.TexturePropertySingleLine(new GUIContent("Cutout Pattern", "Cutout Pattern (R)"), this.FindProperty("_CutoffPattern"));
			}
			this.ShaderProperty(this.FindProperty("_CutoffMin"), "Cutout Min", 2);
			this.ShaderProperty(this.FindProperty("_CutoffMax"), "Cutout Max", 2);
		}

		if (isMainTexColorMask) {
			EditorGUI.showMixedValue = colorMask.hasMixedValue;
			this.TexturePropertySingleLine(new GUIContent("Color Mask", "Masks Color Tint"), colorMask);
			EditorGUI.showMixedValue = false;
		}

		EditorGUI.indentLevel -= 1;

		if (isEmissionAlbedoCustom) {
			EditorGUI.showMixedValue = emissionMap.hasMixedValue;
			this.TexturePropertyWithHDRColor(
				new GUIContent("Emission Texture", "Emission Texture (RGB)"), emissionMap,
				this.FindProperty("_EmissionColor"), new ColorPickerHDRConfig(0, 2, 0, 2), true
			);
			EditorGUI.showMixedValue = false;
		} else if (isEmissionAlbedoMask) {
			EditorGUI.showMixedValue = emissionMap.hasMixedValue;
			this.TexturePropertyWithHDRColor(
				new GUIContent("Emission Mask", "Emission Mask (R)"), emissionMap,
				this.FindProperty("_EmissionColor"), new ColorPickerHDRConfig(0, 2, 0, 2), true
			);
			EditorGUI.showMixedValue = false;
		} else if (isEmissionAlbedoNoMask) {
			this.ShaderProperty(this.FindProperty("_EmissionColor"), "Emission");
		}

		if (isNormalMapOn) {
			EditorGUI.showMixedValue = normalMap.hasMixedValue;
			this.TexturePropertySingleLine(new GUIContent("Normal Map", "Normal Map (RGB)"), normalMap);
			EditorGUI.showMixedValue = false;
			EditorGUI.indentLevel += 1;
			this.ShaderProperty(this.FindProperty("_BumpScale"), "Scale", 2);
			EditorGUI.indentLevel -= 1;
		}

		// TODO
		/*
		if (mainTexture != null) {
			EditorGUI.BeginChangeCheck();
			this.TextureScaleOffsetProperty(mainTexture);
			if (EditorGUI.EndChangeCheck()) {
				// It's not used but keep sync
				if (emissionMap != null)
					emissionMap.textureScaleAndOffset = mainTexture.textureScaleAndOffset;
				if (colorMask != null)
					colorMask.textureScaleAndOffset = mainTexture.textureScaleAndOffset;
				if (normalMap != null)
					normalMap.textureScaleAndOffset = mainTexture.textureScaleAndOffset;
			}
		} else if (emissionMap != null) {
			EditorGUI.BeginChangeCheck();
			this.TextureScaleOffsetProperty(emissionMap);
			if (EditorGUI.EndChangeCheck()) {
				if (colorMask != null)
					colorMask.textureScaleAndOffset = emissionMap.textureScaleAndOffset;
				if (normalMap != null)
					normalMap.textureScaleAndOffset = emissionMap.textureScaleAndOffset;
			}
		}
			*/
	}

	protected void OnGUI_Shading()
	{
		var isCubedParadoxFLT = Commons.MaterialCheckTag(this.target, "KawaFLT_Feature_Shading", "CubedParadoxFLT");
		var isKawashirovFLTSingle = Commons.MaterialCheckTag(this.target, "KawaFLT_Feature_Shading", "KawashirovFLTSingle");
		var isKawashirovFLTRamp = Commons.MaterialCheckTag(this.target, "KawaFLT_Feature_Shading", "KawashirovFLTRamp");

		EditorGUILayout.LabelField(string.Format(
		"Shading is set to \"{0}\" in shader generator.",
			isCubedParadoxFLT ? Commons.shadingModeNames[ShadingMode.CubedParadoxFLT] :
			isKawashirovFLTSingle ? Commons.shadingModeNames[ShadingMode.KawashirovFLTSingle] :
			isKawashirovFLTRamp ? Commons.shadingModeNames[ShadingMode.KawashirovFLTRamp] :
			"???"
		));

		EditorGUI.indentLevel += 1;
		if (isCubedParadoxFLT) {
			this.ShaderProperty(this.FindProperty("_Sh_Cbdprdx_Shadow"), "Shadow");
		} else if (isKawashirovFLTSingle) {
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
		} else if (isKawashirovFLTRamp) {
			this.ShaderProperty(this.FindProperty("_Sh_Kwshrv_ShdBlnd"), "RT Shadows blending");
			this.TexturePropertySingleLine(
				new GUIContent("Ramp Texture", "Ramp Texture (RGB)"), this.FindProperty("_Sh_KwshrvRmp_Tex")
			);
			this.ShaderProperty(this.FindProperty("_Sh_KwshrvRmp_Pwr"), "Power");
			this.ShaderProperty(this.FindProperty("_Sh_KwshrvRmp_NdrctClr"), "Indirect Tint");
		}
		EditorGUI.indentLevel -= 1;
	}

	protected void OnGUI_DistanceFade()
	{
		var isDistanceFadeModeRange = Commons.MaterialCheckTag(this.target, "KawaFLT_Feature_DistanceFade", "Range");
		var isDistanceFadeModeInfinity = Commons.MaterialCheckTag(this.target, "KawaFLT_Feature_DistanceFade", "Infinity");
		var isDistanceFadeRandomScreenPattern = Commons.MaterialCheckTag(this.target, "KawaFLT_Feature_DistanceFadeRandom", "ScreenPattern");

		if (isDistanceFadeModeRange || isDistanceFadeModeInfinity) {
			EditorGUI.indentLevel += 1;

			this.ShaderProperty(this.FindProperty("_DstFd_Axis"), "Axis weights");

			var random = this.FindProperty("_DstFd_Random");

			if (isDistanceFadeRandomScreenPattern) {
				EditorGUI.indentLevel += 1;
				this.TexturePropertySingleLine(
					new GUIContent("Fade Pattern", "Fade Pattern (R)"),
					this.FindProperty("_DstFd_Pattern")
				);
				EditorGUI.indentLevel -= 1;
			}

			this.ShaderProperty(this.FindProperty("_DstFd_Near"), "Near Distance");
			if (isDistanceFadeModeRange)
				this.ShaderProperty(this.FindProperty("_DstFd_Far"), "Far Distance");
			this.ShaderProperty(this.FindProperty("_DstFd_AdjustPower"), "Power Adjust");
			if (isDistanceFadeModeInfinity)
				this.ShaderProperty(this.FindProperty("_DstFd_AdjustScale"), "Scale Adjust");

			EditorGUI.indentLevel -= 1;
		}
	}

	protected void OnGUI_FPS()
	{
		var isFPSColor = Commons.MaterialCheckTag(this.target, "KawaFLT_Feature_FPS", "Color");
		var isFPSTexture = Commons.MaterialCheckTag(this.target, "KawaFLT_Feature_FPS", "Texture");
		var isFPSMesh = Commons.MaterialCheckTag(this.target, "KawaFLT_Feature_FPS", "Mesh");
		if (isFPSColor || isFPSTexture || isFPSMesh) {
			EditorGUI.indentLevel += 1;
			this.ShaderProperty(this.FindProperty("_FPS_TLo"), "Low FPS tint");
			this.ShaderProperty(this.FindProperty("_FPS_THi"), "High FPS tint");
			EditorGUI.indentLevel -= 1;
		}
	}

	private void OnGUI_Outline()
	{
		if (!this.haveGeometry)
			return;
		var isOutlineTinted = Commons.MaterialCheckTag(this.target, "KawaFLT_Feature_Outline", "Tinted");
		var isOutlineColored = Commons.MaterialCheckTag(this.target, "KawaFLT_Feature_Outline", "Colored");
		if (isOutlineTinted || isOutlineColored) {
			this.ShaderProperty(this.FindProperty("_outline_color"), "Color", 2);
			this.ShaderProperty(this.FindProperty("_outline_width"), new GUIContent("Width", "Outline width in cm"), 2);
			this.ShaderProperty(this.FindProperty("_outline_bias"), "Bias", 2);
		}
	}

	private void OnGUI_InfinityWarDecimation()
	{
		if (!this.haveGeometry)
			return;

		var isEnabled = Commons.MaterialCheckTag(this.target, "KawaFLT_Feature_InfinityWarDecimation", "True");

		if (!isEnabled)
			return;

		EditorGUILayout.LabelField("I don't feel so good...");
		EditorGUI.indentLevel += 1;

		EditorGUILayout.LabelField("General equation of a Plane (XYZ is normal, W is offset):");
		EditorGUI.indentLevel += 1;
		this.ShaderProperty(this.FindProperty("_Dsntgrt_Plane"), "");
		EditorGUI.indentLevel -= 1;

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
		EditorGUI.indentLevel -= 1;
	}

	private void OnGUI_PolyColorWave()
	{
		if (!this.haveGeometry)
			return;
		var isPCW = Commons.MaterialCheckTag(this.target, "KawaFLT_Feature_PCW", "Enabled");

		if (isPCW) {
			EditorGUI.indentLevel += 1;

			EditorGUILayout.LabelField("Wave timings:");
			EditorGUI.indentLevel += 1;

			var time_low = this.FindProperty("_PCW_WvTmLo");
			this.ShaderProperty(time_low, "Hidden");
			var time_low_f = time_low.floatValue;

			var time_asc = this.FindProperty("_PCW_WvTmAs");
			this.ShaderProperty(time_asc, "Fade-in");
			var time_asc_f = time_asc.floatValue;

			var time_high = this.FindProperty("_PCW_WvTmHi");
			this.ShaderProperty(time_high, "Shown");
			var time_high_f = time_high.floatValue;

			var time_desc = this.FindProperty("_PCW_WvTmDe");
			this.ShaderProperty(time_desc, "Fade-out");
			var time_desc_f = time_desc.floatValue;

			var time_period = time_low_f + time_asc_f + time_high_f + time_desc_f;
			var time_curve = new AnimationCurve();
			time_curve.AddKey(0, 0);
			time_curve.AddKey(time_low_f, 0);
			time_curve.AddKey(time_low_f + time_asc_f, 1);
			time_curve.AddKey(time_low_f + time_asc_f + time_high_f, 1);
			time_curve.AddKey(time_period, 0);
			for (var i = 0; i < time_curve.keys.Length; ++i) {
				AnimationUtility.SetKeyLeftTangentMode(time_curve, i, AnimationUtility.TangentMode.Linear);
				AnimationUtility.SetKeyRightTangentMode(time_curve, i, AnimationUtility.TangentMode.Linear);
			}
			// for(int i = 0; i < time_curve.length; ++i) {
			// 	time_curve.SmoothTangents(i, 1);
			// }
			EditorGUILayout.CurveField("Preview amplitude (read-only)", time_curve);
			HelpBoxRich(string.Format("Time for singe wave cycle: <b>{0:f}</b> sec. ", time_period));
			this.ShaderProperty(this.FindProperty("_PCW_WvTmRnd"), "Random per tris");

			EditorGUILayout.LabelField("Time offset from UV0 (XY) and UV1 (ZW):");
			this.VectorProperty(this.FindProperty("_PCW_WvTmUV"), "");

			EditorGUILayout.LabelField("Time offset from mesh-space coords: ");
			this.VectorProperty(this.FindProperty("_PCW_WvTmVtx"), "");

			EditorGUI.indentLevel -= 1;

			EditorGUILayout.LabelField("Wave coloring:");
			EditorGUI.indentLevel += 1;

			var rainbowTime = this.FindProperty("_PCW_RnbwTm");

			this.ShaderProperty(this.FindProperty("_PCW_Em"), "Emissiveness");
			this.ShaderProperty(this.FindProperty("_PCW_Color"), "Color");
			this.ShaderProperty(rainbowTime, "Rainbow time");
			this.ShaderProperty(this.FindProperty("_PCW_RnbwTmRnd"), "Rainbow time random");
			this.ShaderProperty(this.FindProperty("_PCW_RnbwStrtn"), "Rainbow saturation");
			this.ShaderProperty(this.FindProperty("_PCW_RnbwBrghtnss"), "Rainbow brightness");
			this.ShaderProperty(this.FindProperty("_PCW_Mix"), "Color vs. Rainbow");

			EditorGUI.indentLevel -= 1;

			var time_rainbow = rainbowTime.floatValue;
			var gcd_t = gcd(time_rainbow, time_period);
			var lcm_t = (time_rainbow * time_period) / gcd_t;
			HelpBoxRich(string.Format(
				"Period of the wave <b>{0:f1}</b> sec. and period of Rainbow <b>{1:f1}</b> sec. produces total cycle of ~<b>{2:f1}</b> sec. (GCD: ~<b>{3:f}</b>)",
				time_period, time_rainbow, lcm_t, gcd_t
			));

			EditorGUI.indentLevel -= 1;
		}

	}

	protected bool temporaryBlock = false;

	public override void OnEnable()
	{
		base.OnEnable();

		this.haveGeometry = Commons.MaterialCheckTag(this.targets, "KawaFLT_Feature_Geometry", "True");
		this.haveTessellation = Commons.MaterialCheckTag(this.targets, "KawaFLT_Feature_Tessellation", "True");

		this.materialProperties = new Dictionary<string, MaterialProperty>();
		var shaders = new HashSet<Shader>();
		var names = new HashSet<string>();
		var materials = 0;

		foreach (object target in this.targets) {
			var material = target as Material;
			if (material != null) {
				shaders.Add(material.shader);
				++materials;
			}
		}

		foreach (var shader in shaders) {
			var count = ShaderUtil.GetPropertyCount(shader);
			for (var i = 0; i < count; ++i) {
				names.Add(ShaderUtil.GetPropertyName(shader, i));
			}
		}

		if (shaders.Count > 1) {
			this.temporaryBlock = true;
		} else {
			foreach (var name in names) {
				this.materialProperties[name] = GetMaterialProperty(this.targets, name);
			}
		}

		Debug.Log(string.Format(
			"Tracking {0} properties form {1} names from {2} shaders from {3} meterials from {4} targets.",
			this.materialProperties.Count, names.Count, shaders.Count, materials, this.targets.Length
		));
	}

	public override void OnInspectorGUI()
	{
		if (!this.isVisible)
			return;

		if (this.targets.Length > 1) {
			HelpBoxRich("Multi-select is not yet properly work, it can break your materals! Not yet recomended to use.");
		}

		EditorGUILayout.Space();
		this.OnGUI_BlendMode();

		EditorGUILayout.Space();
		this.OnGUI_Textures();

		EditorGUILayout.Space();
		this.OnGUI_Shading();

		EditorGUILayout.Space();
		this.OnGUI_Outline();

		EditorGUILayout.Space();
		this.OnGUI_DistanceFade();

		EditorGUILayout.Space();
		this.OnGUI_FPS();

		EditorGUILayout.Space();
		this.OnGUI_InfinityWarDecimation();

		EditorGUILayout.Space();
		this.OnGUI_PolyColorWave();

		if (this.haveTessellation) {
			EditorGUILayout.Space();
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Tessellation:");
			this.ShaderProperty(this.FindProperty("_Tsltn_Uni"), "Uniform factor");
			this.ShaderProperty(this.FindProperty("_Tsltn_Nrm"), "Factor from curvness");
			this.ShaderProperty(this.FindProperty("_Tsltn_Inside"), "Inside multiplier");
		}

		KawaEditorUtil.ShaderEditorFooter();
	}

}