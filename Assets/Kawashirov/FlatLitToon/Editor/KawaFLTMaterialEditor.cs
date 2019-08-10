using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System;
using Kawashirov.FLT;
using static Kawashirov.FLT.Commons;
using System.Reflection;

// Имя файла длжно совпадать с именем типа.
// https://forum.unity.com/threads/solved-blank-scriptableobject-on-import.511527/
// Тип не включен в неймспейс Kawashirov.FLT, т.к. эдитор указывается в файле .shader без указания неймспейса.

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

	protected void TexturePropertySmol(GUIContent label, MaterialProperty prop)
	{
		// ЁБАНЫЕ ВЫ СУКА ЧЕРТИ РАЗРАБОТЧИКИ ЮНИТИ Я ВАС В РОТ ЕБАЛ
		// КАКОГО ХУЯ ЧТО БЫ ПРОСТО ПОСТАВИТЬ ИКОНКУ С ТЕКСТУРОЙ СПРАВА, А НЕ СЛЕВА
		// Я ДОЛЖЕН ПИДОРИТЬ СОДЕРЖИМОЕ ДЛЛОК ЧТО БЫ ПОНЯТЬ ЧЁ КАК РАБОТАЕТ
		// И ВЫЗЫВАТЬ ВАШ ПИДОРСКИЙ ГОВНОКОД СЕРЕЗ РЕФЛЕКШЕНЫ?
		// ЧЁ СЛОЖНО БЫЛО protected ВМЕСТО private ПОСТАВИТЬ?
		// КАК Я БЛЯТЬ ЭКСТЕНДИТЬ ЭДИТОРЫ ДОЛЖЕН, СУКА?
		try {
			var flags = BindingFlags.Instance | BindingFlags.NonPublic;
			var m1 = typeof(MaterialEditor).GetMethod("GetControlRectForSingleLine", flags);
			var m2 = typeof(MaterialEditor).GetMethod("TexturePropertyBody", flags);

			var rect = (Rect)m1.Invoke(this, new object[] { });

			var rect_ctrl = EditorGUI.PrefixLabel(rect, label);

			var min = Math.Min(rect_ctrl.height, rect_ctrl.width);
			rect_ctrl.width = Math.Min(rect_ctrl.width, min);
			rect_ctrl.height = Math.Min(rect_ctrl.height, min);

			m2.Invoke(this, new object[] { rect_ctrl, prop });
		} catch (Exception exc) {
			// ЕЖЕЛИ ЧТО-ТО ПОШЛО НЕ ТАК
			this.DefaultShaderProperty(prop, label.text);
		}
	}

	protected void OnGUI_BlendMode()
	{
		var debug = MaterialTagBoolCheck(this.target, "KawaFLT_Feature_Debug");
		var instancing = MaterialTagBoolCheck(this.target, "KawaFLT_Feature_Instancing");

		if (instancing && debug) {
			this.EnableInstancingField();
		} else {
			using (new EditorGUI.DisabledScope(!instancing)) {
				EditorGUILayout.LabelField("Instancing", instancing ? "Enabled" : "Disabled");
			}
			foreach (var target in this.targets) {
				var m = target as Material;
				if (m && m.enableInstancing != instancing) {
					m.enableInstancing = instancing;
				}
			}
		}
	}


	protected void OnGUI_Tessellation() {
		var tessellation = MaterialTagBoolCheck(this.target, KawaFLT_Feature_Tessellation);
		using (new EditorGUI.DisabledScope(!tessellation)) {
			if (tessellation) {
				EditorGUILayout.LabelField("Tessellation", "Enabled");
				using (new EditorGUI.IndentLevelScope()) {
					var partitioning = MaterialTagEnumGet<TessPartitioning>(this.target, KawaFLT_Feature_Partitioning);
					var domain = MaterialTagEnumGet<TessDomain>(this.target, KawaFLT_Feature_Domain);
					EditorGUILayout.LabelField("Partitioning", Enum.GetName(typeof(TessPartitioning), partitioning));
					EditorGUILayout.LabelField("Domain", Enum.GetName(typeof(TessDomain), domain));

					this.ShaderProperty(this.FindProperty("_Tsltn_Uni"), "Uniform factor");
					this.ShaderProperty(this.FindProperty("_Tsltn_Nrm"), "Factor from curvness");
					this.ShaderProperty(this.FindProperty("_Tsltn_Inside"), "Inside multiplier");
				}
			} else {
				EditorGUILayout.LabelField("Tessellation", "Disabled");
			}
		}
	}

	protected void OnGUI_Random()
	{
		var random = MaterialTagBoolCheck(this.target, KawaFLT_Feature_Random);

		using (new EditorGUI.DisabledScope(!random)) {
			if (random) {
				EditorGUILayout.LabelField("Random", "Enabled");
				using (new EditorGUI.IndentLevelScope()) {
					var seedTex = this.FindProperty("_Rnd_Seed");
					this.TexturePropertySmol(new GUIContent(
						"Seed Noise", "Red-Texture filled with random values to help generating random numbers."
					), seedTex);
					this.TextureCompatibilityWarning(seedTex);
					if (seedTex.textureValue == null) {
						EditorGUILayout.HelpBox(
							"No seed noise texture is set! " +
							"Some of enabled features using Pseudo-Random Number Generator. " +
							"This texture is required, and shader will not properly work without this.",
							MessageType.Error
						);
					}
				}
			} else {
				EditorGUILayout.LabelField("Random", "Disabled");
			}
		}
	}

	protected void OnGUI_Textures()
	{
		EditorGUILayout.LabelField("General Rendering Features");
		using (new EditorGUI.IndentLevelScope()) {

			var f_mainTex = MaterialTagEnumGet<MainTexKeywords>(this.target, KawaFLT_Feature_MainTex);
			var f_cutout = MaterialTagEnumGetSafe<CutoutMode>(this.target, KawaFLT_Feature_Cutout);

			var f_normalMap = MaterialTagBoolCheck(this.target, KawaFLT_Feature_NormalMap);

			using (new EditorGUI.DisabledScope(f_mainTex == MainTexKeywords.NoMainTex)) {
				var label = "Albedo (MainTex)";
				var tooltip = "Albedo main Color Texture (RGBA)";
				var mainTexture = this.FindProperty("_MainTex");
				if (f_mainTex != MainTexKeywords.NoMainTex) {
					this.TexturePropertySmol(new GUIContent(label, tooltip), mainTexture);
					this.TextureCompatibilityWarning(mainTexture);
					if (mainTexture.textureValue == null) {
						EditorGUILayout.HelpBox(
							"No albedo texture is set! Disable main tex feature in shader generator, if you don't need this.",
							MessageType.Warning
						);
					}
				} else {
					EditorGUILayout.LabelField(label, "Disabled");
				}
			}

			using (new EditorGUI.IndentLevelScope()) {
				using (new EditorGUI.DisabledScope(!f_cutout.HasValue || f_cutout.Value != CutoutMode.Classic)) {
					if (f_cutout.HasValue && f_cutout.Value == CutoutMode.Classic) {
						this.ShaderProperty(this.FindProperty("_Cutoff"), "Cutout (Classic)");
					} else {
						EditorGUILayout.LabelField("Cutout (Classic)", "Disabled");
					}
				}

				using (new EditorGUI.DisabledScope(!f_cutout.HasValue || f_cutout.Value == CutoutMode.Classic)) {
					if (f_cutout.HasValue && f_cutout.Value != CutoutMode.Classic) {
						this.ShaderProperty(this.FindProperty("_CutoffMin"), "Cutout Min");
						this.ShaderProperty(this.FindProperty("_CutoffMax"), "Cutout Max");
					} else {
						EditorGUILayout.LabelField("Cutout Min", "Disabled");
						EditorGUILayout.LabelField("Cutout Max", "Disabled");
					}
				}

				this.ShaderProperty(this.FindProperty("_Color"), "Color");

				using (new EditorGUI.DisabledScope(f_mainTex != MainTexKeywords.ColorMask)) {
					var label = "Color Mask";
					var tooltip = "Masks Color Tint (R)";
					if (f_mainTex == MainTexKeywords.ColorMask) {
						var colorMask = this.FindProperty("_ColorMask");
						this.TexturePropertySmol(new GUIContent(label, tooltip), colorMask);
						this.TextureCompatibilityWarning(colorMask);
						if (colorMask.textureValue == null) {
							EditorGUILayout.HelpBox(
								"No color mask texture set! Disable main tex clor mask feature in shader generator, if you don't need this.",
								MessageType.Warning
							);
						}
					} else {
						EditorGUILayout.LabelField(label, "Disabled");
					}
				}
			}

			var f_emission = MaterialTagBoolCheck(this.target, KawaFLT_Feature_Emission);
			using (new EditorGUI.DisabledScope(!f_emission)) {
				if (f_emission) {
					var f_emissionMode = MaterialTagEnumGet<EmissionMode>(this.target, KawaFLT_Feature_EmissionMode);
					EditorGUILayout.LabelField("Emission", Enum.GetName(typeof(EmissionMode), f_emissionMode));
					using (new EditorGUI.IndentLevelScope()) {
						using (new EditorGUI.DisabledScope(f_emissionMode != EmissionMode.AlbedoMask)) {
							var label = "Emission Albedo Mask";
							var tooltip = "Masks which parts of albedo is emissive (R)";
							if (f_emissionMode == EmissionMode.AlbedoMask) {
								var emMask = this.FindProperty("_EmissionMap");
								this.TexturePropertySmol(new GUIContent(label, tooltip), emMask);
								this.TextureCompatibilityWarning(emMask);
								if (emMask.textureValue == null) {
									EditorGUILayout.HelpBox(
										"No custom emission mask texture set! Disable emission mask feature in shader generator, if you don't need this.",
										MessageType.Warning
									);
								}
							} else {
								EditorGUILayout.LabelField(label, "Disabled");
							}
						}

						using (new EditorGUI.DisabledScope(f_emissionMode != EmissionMode.Custom)) {
							var label = "Emission Texture";
							var tooltip = "Custom Emission Texture (RGB)";
							if (f_emissionMode == EmissionMode.Custom) {
								var emMap = this.FindProperty("_EmissionMap");
								this.TexturePropertySmol(new GUIContent(label, tooltip), emMap);
								this.TextureCompatibilityWarning(emMap);
								if (emMap.textureValue == null) {
									EditorGUILayout.HelpBox(
										"No custom emission texture set! Disable custom emission feature in shader generator, if you don't need this.",
										MessageType.Warning
									);
								}
							} else {
								EditorGUILayout.LabelField(label, "Disabled");
							}
						}

						var color = this.FindProperty("_EmissionColor");
						this.ShaderProperty(color, new GUIContent("Emission Color (Tint)", "Emission Color Tint (RGB)"));
						var value = color.colorValue;
						var intencity = (value.r + value.g + value.b) * value.a;
						if (intencity < 0.03) {
							EditorGUILayout.HelpBox(
								"Emission Color is too dark! disable emission feature in shader generator, if you don't need emission.",
								MessageType.Warning
							);
						}

					}
				} else {
					EditorGUILayout.LabelField("Emission", "Disabled");
				}
			}

			using (new EditorGUI.DisabledScope(!f_normalMap)) {
				var label = "Normal Map";
				var tooltip = "Normal (Bump) Map Texture (RGB)";
				if (f_normalMap) {
					var bumpMap = this.FindProperty("_BumpMap");
					this.TexturePropertySmol(new GUIContent(label, tooltip), bumpMap);
					this.TextureCompatibilityWarning(bumpMap);
					if (bumpMap.textureValue == null) {
						EditorGUILayout.HelpBox(
							"Normal map texture is not set! Disable normal feature in shader generator, if you don't need this.",
							MessageType.Warning
						);
					}
					using (new EditorGUI.IndentLevelScope()) {
						var bumpScale = this.FindProperty("_BumpScale");
						this.DefaultShaderProperty(bumpScale, "Normal Map Scale");
						if (bumpScale.floatValue < 0.01) {
							EditorGUILayout.HelpBox(
								"Normal map scale value is close to zero! In this situation, may be it's better to disable normal feature in shader generator, if you don't need this?",
								MessageType.Warning
							);
						}
					}
				} else {
					EditorGUILayout.LabelField(label, "Disabled");
					using (new EditorGUI.IndentLevelScope()) {
						EditorGUILayout.LabelField("Normal Map Scale", "Disabled");
					}
				}
			}
		}
	}

	protected void OnGUI_Shading()
	{
		var shading = MaterialTagEnumGet<ShadingMode>(this.target, KawaFLT_Feature_Shading);
		EditorGUILayout.LabelField("Shading", Enum.GetName(typeof(ShadingMode), shading));
		using (new EditorGUI.IndentLevelScope()) {
			EditorGUILayout.HelpBox(shadingModeDesc[shading], MessageType.Info);
			if (shading == ShadingMode.CubedParadoxFLT) {
				this.ShaderProperty(this.FindProperty("_Sh_Cbdprdx_Shadow"), "Shadow");
			} else if (shading == ShadingMode.KawashirovFLTSingle) {
				this.ShaderProperty(this.FindProperty("_Sh_Kwshrv_ShdBlnd"), "RT Shadows blending");

				EditorGUILayout.LabelField("Sides threshold");
				using (new EditorGUI.IndentLevelScope()) {
					this.ShaderProperty(this.FindProperty("_Sh_KwshrvSngl_TngntLo"), "Low");
					this.ShaderProperty(this.FindProperty("_Sh_KwshrvSngl_TngntHi"), "High");
				}

				EditorGUILayout.LabelField("Brightness");
				using (new EditorGUI.IndentLevelScope()) {
					this.ShaderProperty(this.FindProperty("_Sh_KwshrvSngl_ShdLo"), "Back side (Shaded)");
					this.ShaderProperty(this.FindProperty("_Sh_KwshrvSngl_ShdHi"), "Front side (Lit)");
				}
			} else if (shading == ShadingMode.KawashirovFLTRamp) {
				var rampTex = this.FindProperty("_Sh_KwshrvRmp_Tex");
				this.ShaderProperty(this.FindProperty("_Sh_Kwshrv_ShdBlnd"), "RT Shadows blending");
				this.TexturePropertySmol(new GUIContent("Ramp Texture", "Ramp Texture (RGB)"), rampTex);
				this.TextureCompatibilityWarning(rampTex);
				if (rampTex.textureValue == null) {
					EditorGUILayout.HelpBox(
						"Ramp texture is not set! This shading model will not work well unless proper ramp texture is set!",
						MessageType.Error
					);
				}
				this.ShaderProperty(this.FindProperty("_Sh_KwshrvRmp_Pwr"), "Power");
				this.ShaderProperty(this.FindProperty("_Sh_KwshrvRmp_NdrctClr"), "Indirect Tint");
			}
		}
	}

	protected void OnGUI_DistanceFade()
	{
		var f_distanceFade = MaterialTagBoolCheck(this.target, KawaFLT_Feature_DistanceFade);
		using (new EditorGUI.DisabledScope(!f_distanceFade)) {
			EditorGUILayout.LabelField("VF Feature: Distance Fade", f_distanceFade ? "Enabled" : "Disabled");
			using (new EditorGUI.IndentLevelScope()) {
				if (f_distanceFade) {
					var f_DistanceFadeMode = MaterialTagEnumGet<DistanceFadeMode>(this.target, KawaFLT_Feature_DistanceFadeMode);
					EditorGUILayout.LabelField("Mode", Enum.GetName(typeof(DistanceFadeMode), f_DistanceFadeMode));

					this.ShaderProperty(this.FindProperty("_DstFd_Axis"), "Axis weights");

					this.ShaderProperty(this.FindProperty("_DstFd_Near"), "Near Distance");
					if (f_DistanceFadeMode == DistanceFadeMode.Range) {
						this.ShaderProperty(this.FindProperty("_DstFd_Far"), "Far Distance");
					} else {
						using (new EditorGUI.DisabledScope(true)) {
							EditorGUILayout.LabelField("Far Distance", "Disabled");
						}
					}

					this.ShaderProperty(this.FindProperty("_DstFd_AdjustPower"), "Power Adjust");
					if (f_DistanceFadeMode == DistanceFadeMode.Infinity) {
						this.ShaderProperty(this.FindProperty("_DstFd_AdjustScale"), "Scale Adjust");
					} else {
						using (new EditorGUI.DisabledScope(true)) {
							EditorGUILayout.LabelField("Scale Adjust", "Disabled");
						}
					}

				}
			}
		}
	}

	protected void OnGUI_FPS()
	{
		var f_FPS = MaterialTagBoolCheck(this.target, KawaFLT_Feature_FPS);
		using (new EditorGUI.DisabledScope(!f_FPS)) {
			EditorGUILayout.LabelField("VF Feature: FPS Indication", f_FPS ? "Enabled" : "Disabled");
			using (new EditorGUI.IndentLevelScope()) {
				if (f_FPS) {
					var f_FPSMode = MaterialTagEnumGet<FPSMode>(this.target, KawaFLT_Feature_FPSMode);
					EditorGUILayout.LabelField("Mode", Enum.GetName(typeof(FPSMode), f_FPSMode));

					this.ShaderProperty(this.FindProperty("_FPS_TLo"), "Low FPS tint");
					this.ShaderProperty(this.FindProperty("_FPS_THi"), "High FPS tint");
				}
			}
		}
	}

	private void OnGUI_Outline()
	{
		var f_Outline = MaterialTagBoolCheck(this.target, KawaFLT_Feature_Outline);
		using (new EditorGUI.DisabledScope(!f_Outline)) {
			EditorGUILayout.LabelField("VGF Feature: Outline", f_Outline ? "Enabled" : "Disabled");
			using (new EditorGUI.IndentLevelScope()) {
				if (f_Outline) {
					var f_FPSMode = MaterialTagEnumGet<OutlineMode>(this.target, KawaFLT_Feature_OutlineMode);
					EditorGUILayout.LabelField("Mode", Enum.GetName(typeof(FPSMode), f_FPSMode));

					this.ShaderProperty(this.FindProperty("_outline_width"), "Outline width (cm)");
					this.ShaderProperty(this.FindProperty("_outline_color"), "Outline Color (Tint)");
					this.ShaderProperty(this.FindProperty("_outline_bias"), "Outline Z-Bias");
				}
			}
		}
	}

	private void OnGUI_InfinityWarDecimation()
	{
		var f_InfinityWarDecimation = MaterialTagBoolCheck(this.target, KawaFLT_Feature_InfinityWarDecimation);
		var f_Tessellation = MaterialTagBoolCheck(this.target, KawaFLT_Feature_Tessellation);
		using (new EditorGUI.DisabledScope(!f_InfinityWarDecimation)) {
			EditorGUILayout.LabelField("VGF Feature: Infinity War Decimation", f_InfinityWarDecimation ? "Enabled" : "Disabled");
			using (new EditorGUI.IndentLevelScope()) {
				if (f_InfinityWarDecimation) {
					EditorGUILayout.LabelField("I don't feel so good...");
					EditorGUILayout.LabelField("General equation of a Plane (XYZ is normal, W is offset):");
					using (new EditorGUI.IndentLevelScope()) {
						this.ShaderProperty(this.FindProperty("_Dsntgrt_Plane"), "");
					}
					this.ShaderProperty(this.FindProperty("_Dsntgrt_TriSpreadFactor"), "Spread Factor");
					this.ShaderProperty(this.FindProperty("_Dsntgrt_TriSpreadAccel"), "Spread Accel");
					this.ShaderProperty(this.FindProperty("_Dsntgrt_TriDecayFar"), "Far Distance");
					this.ShaderProperty(this.FindProperty("_Dsntgrt_TriPowerAdjust"), "Power Adjust");
					this.ShaderProperty(this.FindProperty("_Dsntgrt_Tint"), "Decay tint");
					if (f_Tessellation) {
						this.ShaderProperty(this.FindProperty("_Dsntgrt_Tsltn"), "Tessellation factor");
					} else {
						using (new EditorGUI.DisabledScope(true)) {
							EditorGUILayout.LabelField("Tessellation factor", "Disabled");
						}
					}
				}
			}
		}
	}

	private void OnGUI_PolyColorWave()
	{
		var f_PCW = MaterialTagBoolCheck(this.target, KawaFLT_Feature_PCW);
		using (new EditorGUI.DisabledScope(!f_PCW)) {
			EditorGUILayout.LabelField("VGF Feature: Poly Color Wave", f_PCW ? "Enabled" : "Disabled");
			using (new EditorGUI.IndentLevelScope()) {
				if (f_PCW) {
					var f_PCWMode = MaterialTagEnumGet<PolyColorWaveMode>(this.target, KawaFLT_Feature_PCWMode);
					EditorGUILayout.LabelField("Mode", Enum.GetName(typeof(PolyColorWaveMode), f_PCWMode));

					var time_low = this.FindProperty("_PCW_WvTmLo");
					var time_asc = this.FindProperty("_PCW_WvTmAs");
					var time_high = this.FindProperty("_PCW_WvTmHi");
					var time_desc = this.FindProperty("_PCW_WvTmDe");

					EditorGUILayout.LabelField("Wave timings:");
					using (new EditorGUI.IndentLevelScope()) {
						this.ShaderProperty(time_low, "Hidden");
						this.ShaderProperty(time_asc, "Fade-in");
						this.ShaderProperty(time_high, "Shown");
						this.ShaderProperty(time_desc, "Fade-out");
					}

					var time_low_f = time_low.floatValue;
					var time_asc_f = time_asc.floatValue;
					var time_high_f = time_high.floatValue;
					var time_desc_f = time_desc.floatValue;
					var time_period = time_low_f + time_asc_f + time_high_f + time_desc_f;

					using (new EditorGUI.IndentLevelScope()) {
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
						EditorGUILayout.CurveField("Preview amplitude (read-only)", time_curve);
						HelpBoxRich(string.Format("Time for singe wave cycle: <b>{0:f}</b> sec. ", time_period));
						this.ShaderProperty(this.FindProperty("_PCW_WvTmRnd"), "Random per tris");

						EditorGUILayout.LabelField("Time offset from UV0 (XY) and UV1 (ZW):");
						this.VectorProperty(this.FindProperty("_PCW_WvTmUV"), "");

						EditorGUILayout.LabelField("Time offset from mesh-space coords: ");
						this.VectorProperty(this.FindProperty("_PCW_WvTmVtx"), "");
					}

					EditorGUILayout.LabelField("Wave coloring:");
					var rainbowTime = this.FindProperty("_PCW_RnbwTm");
					using (new EditorGUI.IndentLevelScope()) {
						this.ShaderProperty(this.FindProperty("_PCW_Em"), "Emissiveness");
						this.ShaderProperty(this.FindProperty("_PCW_Color"), "Color");
						this.ShaderProperty(rainbowTime, "Rainbow time");
						this.ShaderProperty(this.FindProperty("_PCW_RnbwTmRnd"), "Rainbow time random");
						this.ShaderProperty(this.FindProperty("_PCW_RnbwStrtn"), "Rainbow saturation");
						this.ShaderProperty(this.FindProperty("_PCW_RnbwBrghtnss"), "Rainbow brightness");
						this.ShaderProperty(this.FindProperty("_PCW_Mix"), "Color vs. Rainbow");
					}

					var time_rainbow = rainbowTime.floatValue;
					var gcd_t = gcd(time_rainbow, time_period);
					var lcm_t = (time_rainbow * time_period) / gcd_t;
					HelpBoxRich(string.Format(
						"Period of the wave <b>{0:f1}</b> sec. and period of Rainbow <b>{1:f1}</b> sec. produces total cycle of ~<b>{2:f1}</b> sec. (GCD: ~<b>{3:f}</b>)",
						time_period, time_rainbow, lcm_t, gcd_t
					));

				}
			}
		}

		if (!this.haveGeometry)
			return;
		var isPCW = Commons.MaterialTagCheck(this.target, "KawaFLT_Feature_PCW", "Enabled");

		if (isPCW) {
			EditorGUI.indentLevel += 1;

		}

	}

	protected bool temporaryBlock = false;

	public override void OnEnable()
	{
		base.OnEnable();

		this.haveGeometry = Commons.MaterialTagCheck(this.targets, "KawaFLT_Feature_Geometry", "True");
		this.haveTessellation = Commons.MaterialTagCheck(this.targets, "KawaFLT_Feature_Tessellation", "True");

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
		this.OnGUI_Tessellation();

		EditorGUILayout.Space();
		this.OnGUI_Random();

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

	}

}