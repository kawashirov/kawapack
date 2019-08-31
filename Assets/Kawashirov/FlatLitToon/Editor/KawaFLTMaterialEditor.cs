using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Kawashirov;
using Kawashirov.FLT;

using MP = UnityEditor.MaterialProperty;
using EGUIL = UnityEditor.EditorGUILayout;
using DisabledScope = UnityEditor.EditorGUI.DisabledScope;
using IndentLevelScope = UnityEditor.EditorGUI.IndentLevelScope;
using GC = Kawashirov.GeneralCommons;
using UMC = Kawashirov.UnityMaterialCommons;
using KCT = Kawashirov.KawaCommonsTags;
using KFLTC = Kawashirov.FLT.Commons;

// Имя файла длжно совпадать с именем типа.
// https://forum.unity.com/threads/solved-blank-scriptableobject-on-import.511527/
// Тип не включен в неймспейс Kawashirov.FLT, т.к. эдитор указывается в файле .shader без указания неймспейса.

internal class KawaFLTMaterialEditor : KawaMaterialEditor {

	protected IDictionary<string, MP> materialProperties;

	protected static double gcd(double a, double b)
	{
		return a < b ? gcd(b, a) : Math.Abs(b) < 0.001 ? a : gcd(b, a - (Math.Floor(a / b) * b));
	}

	protected void OnGUI_BlendMode()
	{
		var debug = UMC.MaterialTagBoolCheck(this.target, "KawaFLT_Feature_Debug");
		var instancing = UMC.MaterialTagBoolCheck(this.target, "KawaFLT_Feature_Instancing");

		if (instancing && debug) {
			this.EnableInstancingField();
		} else {
			using (new DisabledScope(!instancing)) {
				EGUIL.LabelField("Instancing", instancing ? "Enabled" : "Disabled");
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
		var _Tsltn_Uni = this.FindProperty("_Tsltn_Uni");
		var _Tsltn_Nrm = this.FindProperty("_Tsltn_Nrm");
		var _Tsltn_Inside = this.FindProperty("_Tsltn_Inside");
		var tessellation = _Tsltn_Uni != null && _Tsltn_Nrm != null && _Tsltn_Inside != null;
		using (new DisabledScope(!tessellation)) {
			if (tessellation) {
				EGUIL.LabelField("Tessellation", "Enabled");
				using (new IndentLevelScope()) {
					var partitioning = UMC.MaterialTagEnumGet<TessPartitioning>(this.target, KFLTC.F_Partitioning);
					var domain = UMC.MaterialTagEnumGet<TessDomain>(this.target, KFLTC.F_Domain);
					EGUIL.LabelField("Partitioning", Enum.GetName(typeof(TessPartitioning), partitioning));
					EGUIL.LabelField("Domain", Enum.GetName(typeof(TessDomain), domain));
					this.ShaderProperty(_Tsltn_Uni, "Uniform factor");
					this.ShaderProperty(_Tsltn_Nrm, "Factor from curvness");
					this.ShaderProperty(_Tsltn_Inside, "Inside multiplier");
				}
			} else {
				EGUIL.LabelField("Tessellation", "Disabled");
			}
		}
	}

	protected void OnGUI_Random()
	{
		var random = UMC.MaterialTagBoolCheck(this.target, KFLTC.F_Random);


		var _Rnd_Seed = this.FindProperty("_Rnd_Seed");
		var label = new GUIContent(
			"Seed Noise", "Red-Texture filled with random values to help generating random numbers."
		);
		this.TexturePropertySmolDisabled(label, _Rnd_Seed);
		if (_Rnd_Seed != null && _Rnd_Seed.textureValue == null){
			EGUIL.HelpBox(
				"No seed noise texture is set! " +
				"Some of enabled features using Pseudo-Random Number Generator. " +
				"This texture is required, and shader will not properly work without this.",
				MessageType.Error
			);
		}
	}

	protected void OnGUI_Textures()
	{
		EGUIL.LabelField("General Rendering Features");
		using (new IndentLevelScope()) {

			var _MainTex = this.FindProperty("_MainTex");
			var _MainTex_label = new GUIContent("Albedo (Main Texture)", "Albedo Main Color Texture (RGBA)");
			this.TexturePropertySmolDisabled(_MainTex_label, _MainTex);
			if (_MainTex != null && _MainTex.textureValue == null) {
				EGUIL.HelpBox(
					"No albedo texture is set! Disable main tex feature in shader generator, if you don't need this.",
					MessageType.Warning
				);
			}

			using (new IndentLevelScope()) {
				LabelEnumDisabledFromTagMixed<CutoutMode>(
					"Forward Pass Cutout Mode", this.targets, KFLTC.F_Cutout_Forward
				);

				LabelEnumDisabledFromTagMixed<CutoutMode>(
					"Shadow Caster Cutout Mode", this.targets, KFLTC.F_Cutout_ShadowCaster
				);

				this.ShaderPropertyDisabled(this.FindProperty("_Cutoff"), "Cutout (Classic)");
				this.ShaderPropertyDisabled(this.FindProperty("_CutoffMin"), "Cutout Min");
				this.ShaderPropertyDisabled(this.FindProperty("_CutoffMax"), "Cutout Max");

				this.ShaderPropertyDisabled(this.FindProperty("_Color"), "Color");

				var _ColorMask = this.FindProperty("_ColorMask");
				var _ColorMask_label = new GUIContent("Color Mask", "Masks Color Tint (R)");
				this.TexturePropertySmolDisabled(_ColorMask_label, _ColorMask);
				if (_ColorMask != null && _ColorMask.textureValue == null) {
					EGUIL.HelpBox(
						"No color mask texture set! Disable main texture color mask feature in shader generator, if you don't need this.",
						MessageType.Warning
					);
				}
			}

			var _EmissionMask = this.FindProperty("_EmissionMask");
			var _EmissionMap = this.FindProperty("_EmissionMap");
			var _EmissionColor = this.FindProperty("_EmissionColor");
			var f_emission = _EmissionMask != null || _EmissionMap != null || _EmissionColor != null;
			
			using (new DisabledScope(!f_emission)) {
				EGUIL.LabelField("Emission Feature", f_emission ? "Enabled" : "Disabled");
				if (f_emission) {
					using (new IndentLevelScope()) {
						LabelEnumDisabledFromTagMixed(
							"Emission Mode", this.targets, KFLTC.F_EmissionMode, KFLTC.emissionMode
						);

						var _EmissionMask_label = new GUIContent("Emission Mask", "Mask for Emission by Albedo Main Texture (R)");
						this.TexturePropertySmolDisabled(_EmissionMask_label, _EmissionMask);
						if (_EmissionMask != null && _EmissionMask.textureValue == null) {
							EGUIL.HelpBox(
								"No emission mask texture set! Disable emission mask feature in shader generator, if you don't need this.",
								MessageType.Warning
							);
						}

						var _EmissionMap_label = new GUIContent("Emission Texture", "Custom Emission Texture (RGB)");
						this.TexturePropertySmolDisabled(_EmissionMap_label, _EmissionMap);
						if (_EmissionMap != null && _EmissionMap.textureValue == null) {
							EGUIL.HelpBox(
								"No emission map texture set! Disable emission map feature in shader generator, if you don't need this.",
								MessageType.Warning
							);
						}

						this.ShaderPropertyDisabled(_EmissionColor, new GUIContent("Emission Color (Tint)", "Emission Color Tint (RGB)"));
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

			var _BumpMap = this.FindProperty("_BumpMap");
			var label = new GUIContent("Normal Map", "Normal (Bump) Map Texture (RGB)");
			this.TexturePropertySmolDisabled(label, _BumpMap);
			if (_BumpMap != null && _BumpMap.textureValue == null) {
				EGUIL.HelpBox(
					"Normal map texture is not set! Disable normal feature in shader generator, if you don't need this.",
					MessageType.Warning
				);
			}
			using (new IndentLevelScope()) {
				var _BumpScale = this.FindProperty("_BumpScale");
				this.ShaderPropertyDisabled(_BumpScale, "Normal Map Scale");
				if (_BumpScale != null && _BumpScale.floatValue < 0.05) {
					EGUIL.HelpBox(
						"Normal map scale value is close to zero! In this situation, may be it's better to disable normal feature in shader generator, if you don't need this?",
						MessageType.Warning
					);
				}
			}
		}
	}

	protected void OnGUI_Shading()
	{
		var shading = UMC.MaterialTagEnumGet<ShadingMode>(this.target, KFLTC.F_Shading);
		EGUIL.LabelField("Shading", Enum.GetName(typeof(ShadingMode), shading));
		using (new IndentLevelScope()) {
			EGUIL.HelpBox(KFLTC.shadingModeDesc[shading], MessageType.Info);
			if (shading == ShadingMode.CubedParadoxFLT) {
				this.ShaderPropertyDisabled(this.FindProperty("_Sh_Cbdprdx_Shadow"), "Shadow");
			} else if (shading == ShadingMode.KawashirovFLTSingle) {
				this.ShaderPropertyDisabled(this.FindProperty("_Sh_Kwshrv_ShdBlnd"), "RT Shadows blending");

				EGUIL.LabelField("Sides threshold");
				using (new IndentLevelScope()) {
					this.ShaderPropertyDisabled(this.FindProperty("_Sh_KwshrvSngl_TngntLo"), "Low");
					this.ShaderPropertyDisabled(this.FindProperty("_Sh_KwshrvSngl_TngntHi"), "High");
				}

				EGUIL.LabelField("Brightness");
				using (new IndentLevelScope()) {
					this.ShaderPropertyDisabled(this.FindProperty("_Sh_KwshrvSngl_ShdLo"), "Back side (Shaded)");
					this.ShaderPropertyDisabled(this.FindProperty("_Sh_KwshrvSngl_ShdHi"), "Front side (Lit)");
				}
			} else if (shading == ShadingMode.KawashirovFLTRamp) {
				var rampTex = this.FindProperty("_Sh_KwshrvRmp_Tex");
				this.ShaderPropertyDisabled(this.FindProperty("_Sh_Kwshrv_ShdBlnd"), "RT Shadows blending");
				this.TexturePropertySmol(new GUIContent("Ramp Texture", "Ramp Texture (RGB)"), rampTex);
				this.TextureCompatibilityWarning(rampTex);
				if (rampTex.textureValue == null) {
					EGUIL.HelpBox(
						"Ramp texture is not set! This shading model will not work well unless proper ramp texture is set!",
						MessageType.Error
					);
				}
				this.ShaderPropertyDisabled(this.FindProperty("_Sh_KwshrvRmp_Pwr"), "Power");
				this.ShaderPropertyDisabled(this.FindProperty("_Sh_KwshrvRmp_NdrctClr"), "Indirect Tint");
			}
		}
	}

	protected void OnGUI_DistanceFade()
	{
		var _DstFd_Axis = this.FindProperty("_DstFd_Axis");
		var _DstFd_Near = this.FindProperty("_DstFd_Near");
		var _DstFd_Far = this.FindProperty("_DstFd_Far");
		var _DstFd_AdjustPower = this.FindProperty("_DstFd_AdjustPower");
		var _DstFd_AdjustScale = this.FindProperty("_DstFd_AdjustScale");
		var f_distanceFade = GC.AnyNotNull(_DstFd_Axis, _DstFd_Near, _DstFd_Far, _DstFd_AdjustPower, _DstFd_AdjustScale);
		using (new DisabledScope(!f_distanceFade)) {
			EGUIL.LabelField("Distance Fade Feature", f_distanceFade ? "Enabled" : "Disabled");
			using (new IndentLevelScope()) {
				if (f_distanceFade) {
					LabelEnumDisabledFromTagMixed<DistanceFadeMode>("Mode", this.targets, KFLTC.F_DistanceFadeMode);
					this.ShaderPropertyDisabled(_DstFd_Axis, "Axis weights");
					this.ShaderPropertyDisabled(_DstFd_Near, "Near Distance");
					this.ShaderPropertyDisabled(_DstFd_Far, "Far Distance");
					this.ShaderPropertyDisabled(_DstFd_AdjustPower, "Power Adjust");
					this.ShaderPropertyDisabled(_DstFd_AdjustScale, "Scale Adjust");
				}
			}
		}
	}

	protected void OnGUI_FPS()
	{
		// Commons.MaterialTagBoolCheck(this.target, Commons.KawaFLT_Feature_FPS);
		var _FPS_TLo = this.FindProperty("_FPS_TLo");
		var _FPS_THi = this.FindProperty("_FPS_THi");
		var f_FPS = GC.AnyNotNull(_FPS_TLo, _FPS_THi);
		using (new DisabledScope(!f_FPS)) {
			EGUIL.LabelField("FPS Indication Feature", f_FPS ? "Enabled" : "Disabled");
			using (new IndentLevelScope()) {
				if (f_FPS) {
					LabelEnumDisabledFromTagMixed<FPSMode>("Mode", this.targets, KFLTC.F_FPSMode);
					this.ShaderPropertyDisabled(_FPS_TLo, "Low FPS tint");
					this.ShaderPropertyDisabled(_FPS_THi, "High FPS tint");
				}
			}
		}
	}

	private void OnGUI_Outline()
	{
		var _outline_width = this.FindProperty("_outline_width");
		var _outline_color = this.FindProperty("_outline_color");
		var _outline_bias = this.FindProperty("_outline_bias");

		var f_Outline = GC.AnyNotNull(_outline_width,  _outline_color, _outline_bias);
		using (new DisabledScope(!f_Outline)) {
			EGUIL.LabelField("Outline Feature", f_Outline ? "Enabled" : "Disabled");
			using (new IndentLevelScope()) {
				if (f_Outline) {
					LabelEnumDisabledFromTagMixed<OutlineMode>("Mode", this.targets, KFLTC.F_OutlineMode);
					this.ShaderPropertyDisabled(_outline_width, "Outline width (cm)");
					this.ShaderPropertyDisabled(_outline_color, "Outline Color (Tint)");
					this.ShaderPropertyDisabled(_outline_bias, "Outline Z-Bias");
				}
			}
		}
	}

	private void OnGUI_InfinityWarDecimation()
	{
		var _IWD_Plane = this.FindProperty("_IWD_Plane");
		var _IWD_PlaneDistRandomness = this.FindProperty("_IWD_PlaneDistRandomness");

		var _IWD_DirRandomWeight = this.FindProperty("_IWD_DirRandomWeight");
		var _IWD_DirPlaneWeight = this.FindProperty("_IWD_DirPlaneWeight");
		var _IWD_DirNormalWeight = this.FindProperty("_IWD_DirNormalWeight");
		var _IWD_DirObjectWeight = this.FindProperty("_IWD_DirObjectWeight");
		var _IWD_DirObjectVector = this.FindProperty("_IWD_DirObjectVector");
		var _IWD_DirWorldWeight = this.FindProperty("_IWD_DirWorldWeight");
		var _IWD_DirWorldVector = this.FindProperty("_IWD_DirWorldVector");

		var _IWD_MoveSpeed = this.FindProperty("_IWD_MoveSpeed");
		var _IWD_MoveAccel = this.FindProperty("_IWD_MoveAccel");

		var _IWD_CmprssFar = this.FindProperty("_IWD_CmprssFar");
		var _IWD_TintFar = this.FindProperty("_IWD_TintFar");
		var _IWD_TintColor = this.FindProperty("_IWD_TintColor");
		var _IWD_Tsltn = this.FindProperty("_IWD_Tsltn");

		var f_InfinityWarDecimation = GC.AnyNotNull(
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
						this.ShaderPropertyDisabled(_IWD_Plane, "");
						this.ShaderPropertyDisabled(_IWD_PlaneDistRandomness, "Randomness (W)");
					}
					EGUIL.LabelField("Particles Direction");
					using (new IndentLevelScope()) {
						this.ShaderPropertyDisabled(_IWD_DirRandomWeight, "Random");
						this.ShaderPropertyDisabled(_IWD_DirPlaneWeight, "Particles Front Plane");
						this.ShaderPropertyDisabled(_IWD_DirNormalWeight, "Normal");
						this.ShaderPropertyDisabled(_IWD_DirObjectWeight, "Object Space Vector");
						using (new IndentLevelScope()) {
							this.ShaderPropertyDisabled(_IWD_DirObjectVector, "");
						}
						this.ShaderPropertyDisabled(_IWD_DirWorldWeight, "World Space Vector");
						using (new IndentLevelScope()) {
							this.ShaderPropertyDisabled(_IWD_DirWorldVector, "");
						}
					}
					EGUIL.LabelField("Particles Movement");
					using (new IndentLevelScope()) {
						this.ShaderPropertyDisabled(_IWD_MoveSpeed, "Speed");
						this.ShaderPropertyDisabled(_IWD_MoveAccel, "Accel");
					}
					this.ShaderPropertyDisabled(_IWD_CmprssFar, "Compression Distance");
					this.ShaderPropertyDisabled(_IWD_TintFar, "Tint Distance");
					this.ShaderPropertyDisabled(_IWD_TintColor, "Tint Color");
					this.ShaderPropertyDisabled(_IWD_Tsltn, "Tessellation factor");
				}
			}
		}
	}

	private void OnGUI_PolyColorWave()
	{
		var _PCW_WvTmLo = this.FindProperty("_PCW_WvTmLo");
		var _PCW_WvTmAs = this.FindProperty("_PCW_WvTmAs");
		var _PCW_WvTmHi = this.FindProperty("_PCW_WvTmHi");
		var _PCW_WvTmDe = this.FindProperty("_PCW_WvTmDe");
		var _PCW_WvTmRnd = this.FindProperty("_PCW_WvTmRnd");
		var _PCW_WvTmUV = this.FindProperty("_PCW_WvTmUV");
		var _PCW_WvTmVtx = this.FindProperty("_PCW_WvTmVtx");

		var _PCW_Em = this.FindProperty("_PCW_Em");
		var _PCW_Color = this.FindProperty("_PCW_Color");
		var _PCW_RnbwTm = this.FindProperty("_PCW_RnbwTm");
		var _PCW_RnbwTmRnd = this.FindProperty("_PCW_RnbwTmRnd");
		var _PCW_RnbwStrtn = this.FindProperty("_PCW_RnbwStrtn");
		var _PCW_RnbwBrghtnss = this.FindProperty("_PCW_RnbwBrghtnss");
		var _PCW_Mix = this.FindProperty("_PCW_Mix");

		var f_PCW = UMC.MaterialTagBoolCheck(this.target, KFLTC.F_PCW);
		using (new DisabledScope(!f_PCW)) {
			EGUIL.LabelField("Poly Color Wave Feature", f_PCW ? "Enabled" : "Disabled");
			using (new IndentLevelScope()) {
				if (f_PCW) {
					var f_PCWMode = UMC.MaterialTagEnumGet<PolyColorWaveMode>(this.target, KFLTC.F_PCWMode);
					EGUIL.LabelField("Mode", Enum.GetName(typeof(PolyColorWaveMode), f_PCWMode));

					EGUIL.LabelField("Wave timings:");
					float? time_period = null;
					using (new IndentLevelScope()) {
						this.ShaderPropertyDisabled(_PCW_WvTmLo, "Hidden");
						this.ShaderPropertyDisabled(_PCW_WvTmAs, "Fade-in");
						this.ShaderPropertyDisabled(_PCW_WvTmHi, "Shown");
						this.ShaderPropertyDisabled(_PCW_WvTmDe, "Fade-out");
						time_period = this.OnGUI_PolyColorWave_WvTmHelper(_PCW_WvTmLo, _PCW_WvTmAs, _PCW_WvTmHi, _PCW_WvTmDe);

						this.ShaderPropertyDisabled(_PCW_WvTmRnd, "Random per tris");

						EGUIL.LabelField("Time offset from UV0 (XY) and UV1 (ZW):");
						this.ShaderPropertyDisabled(_PCW_WvTmUV, "");

						EGUIL.LabelField("Time offset from mesh-space coords: ");
						this.ShaderPropertyDisabled(_PCW_WvTmVtx, "");
					}

					EGUIL.LabelField("Wave coloring:");
					using (new IndentLevelScope()) {
						this.ShaderPropertyDisabled(_PCW_Em, "Emissiveness");
						this.ShaderPropertyDisabled(_PCW_Color, "Color");
						this.ShaderPropertyDisabled(_PCW_RnbwTm, "Rainbow time");
						this.ShaderPropertyDisabled(_PCW_RnbwTmRnd, "Rainbow time random");
						this.ShaderPropertyDisabled(_PCW_RnbwStrtn, "Rainbow saturation");
						this.ShaderPropertyDisabled(_PCW_RnbwBrghtnss, "Rainbow brightness");
						this.ShaderPropertyDisabled(_PCW_Mix, "Color vs. Rainbow");
					}
					if (time_period.HasValue && _PCW_RnbwTm != null && !_PCW_RnbwTm.hasMixedValue) {
						var time_rainbow = _PCW_RnbwTm.floatValue;
						var gcd_t = gcd(time_rainbow, time_period.Value);
						var lcm_t = (time_rainbow * time_period) / gcd_t;
						HelpBoxRich(string.Format(
							"Period of the wave <b>{0:f1}</b> sec. and period of Rainbow <b>{1:f1}</b> sec. produces total cycle of ~<b>{2:f1}</b> sec. (GCD: ~<b>{3:f}</b>)",
							time_period, time_rainbow, lcm_t, gcd_t
						));
					}
				}
			}
		}
	}

	private float? OnGUI_PolyColorWave_WvTmHelper(
		MP _PCW_WvTmLo, MP _PCW_WvTmAs, MP _PCW_WvTmHi, MP _PCW_WvTmDe
	) {
		if (
			_PCW_WvTmLo != null && !_PCW_WvTmLo.hasMixedValue &&
			_PCW_WvTmAs != null && !_PCW_WvTmAs.hasMixedValue &&
			_PCW_WvTmHi != null && !_PCW_WvTmHi.hasMixedValue &&
			_PCW_WvTmDe != null && !_PCW_WvTmDe.hasMixedValue
		) {
			var t0 = 0.0f;
			var t1 = t0 + _PCW_WvTmLo.floatValue;
			var t2 = t1 + _PCW_WvTmAs.floatValue;
			var t3 = t2 + _PCW_WvTmHi.floatValue;
			var t4 = t3 + _PCW_WvTmDe.floatValue;

			var time_curve = new AnimationCurve();
			time_curve.AddKey(t0, 0);
			time_curve.AddKey(t1, 0);
			time_curve.AddKey(t2, 1);
			time_curve.AddKey(t3, 1);
			time_curve.AddKey(t4, 0);
			for (var i = 0; i < time_curve.keys.Length; ++i) {
				AnimationUtility.SetKeyLeftTangentMode(time_curve, i, AnimationUtility.TangentMode.Linear);
				AnimationUtility.SetKeyRightTangentMode(time_curve, i, AnimationUtility.TangentMode.Linear);
			}
			EGUIL.CurveField("Preview amplitude (read-only)", time_curve);
			HelpBoxRich(string.Format("Time for singe wave cycle: <b>{0:f}</b> sec. ", t4));

			return t4;
		} else {
			return null;
		}
	}

	public override void OnEnable()
	{
		base.OnEnable();

		this.materialProperties = new Dictionary<string, MP>();
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

		foreach (var name in names) {
			this.materialProperties[name] = GetMaterialProperty(this.targets, name);
		}
	}

	public override void OnInspectorGUI()
	{
		if (!this.isVisible)
			return;

		if (this.targets.Length > 1) {
			HelpBoxRich("Multi-select is not yet properly work, it can break your materals! Not yet recomended to use.");
		}

		try {
			var generator_guid = UMC.MaterialTagGet(this.target, KCT.GenaratorGUID);
			var generator_path = AssetDatabase.GUIDToAssetPath(generator_guid);
			var generator_obj = AssetDatabase.LoadAssetAtPath<Generator>(generator_path);
			using (new DisabledScope(generator_obj == null)) {
				EGUIL.ObjectField("Shader Generator", generator_obj, typeof(Generator), false);
			}
		} catch (Exception exc) {
			EGUIL.LabelField("Shader Generator Error", exc.ToString());
		}

		EGUIL.Space();
		this.OnGUI_BlendMode();

		EGUIL.Space();
		this.OnGUI_Tessellation();

		EGUIL.Space();
		this.OnGUI_Random();

		EGUIL.Space();
		this.OnGUI_Textures();

		EGUIL.Space();
		this.OnGUI_Shading();

		EGUIL.Space();
		this.OnGUI_Outline();

		EGUIL.Space();
		this.OnGUI_DistanceFade();

		EGUIL.Space();
		this.OnGUI_FPS();

		EGUIL.Space();
		this.OnGUI_InfinityWarDecimation();

		EGUIL.Space();
		this.OnGUI_PolyColorWave();

	}

}