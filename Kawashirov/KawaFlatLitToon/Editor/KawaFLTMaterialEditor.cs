using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Kawashirov;
using Kawashirov.FLT;

using MP = UnityEditor.MaterialProperty;
using EGUIL = UnityEditor.EditorGUILayout;
using SC = Kawashirov.StaticCommons;
using KFLTC = Kawashirov.FLT.Commons;
using static UnityEditor.EditorGUI;

// Имя файла длжно совпадать с именем типа.
// https://forum.unity.com/threads/solved-blank-scriptableobject-on-import.511527/
// Тип не включен в неймспейс Kawashirov.FLT, т.к. эдитор указывается в файле .shader без указания неймспейса.

internal class KawaFLTMaterialEditor : Kawashirov.ShaderBaking.MaterialEditor<Generator> {

	public override IEnumerable<string> GetShaderTagsOfIntrest() => KFLTC.tags;

	protected static double gcd(double a, double b) {
		return a < b ? gcd(b, a) : Math.Abs(b) < 0.001 ? a : gcd(b, a - (Math.Floor(a / b) * b));
	}

	protected void OnGUI_BlendMode() {
		var debug = shaderTags[KFLTC.F_Debug].IsTrue();
		var instancing = shaderTags[KFLTC.F_Instancing].IsTrue();

		if (instancing && debug) {
			EnableInstancingField();
		} else {
			using (new DisabledScope(!instancing)) {
				EGUIL.LabelField("Instancing", instancing ? "Enabled" : "Disabled");
			}
			foreach (var target in targets) {
				var m = target as Material;
				if (m && m.enableInstancing != instancing) {
					m.enableInstancing = instancing;
				}
			}
		}
	}

	protected void OnGUI_Tessellation() {
		var _Tsltn_Uni = FindProperty("_Tsltn_Uni");
		var _Tsltn_Nrm = FindProperty("_Tsltn_Nrm");
		var _Tsltn_Inside = FindProperty("_Tsltn_Inside");
		var tessellation = _Tsltn_Uni != null && _Tsltn_Nrm != null && _Tsltn_Inside != null;
		using (new DisabledScope(!tessellation)) {
			if (tessellation) {
				EGUIL.LabelField("Tessellation", "Enabled");
				using (new IndentLevelScope()) {
					LabelShaderTagEnumValue<TessPartitioning>(KFLTC.F_Partitioning, "Partitioning", "Unknown");
					LabelShaderTagEnumValue<TessDomain>(KFLTC.F_Domain, "Domain", "Unknown");
					ShaderProperty(_Tsltn_Uni, "Uniform factor");
					ShaderProperty(_Tsltn_Nrm, "Factor from curvness");
					ShaderProperty(_Tsltn_Inside, "Inside multiplier");
				}
			} else {
				EGUIL.LabelField("Tessellation", "Disabled");
			}
		}
	}

	protected void OnGUI_Random() {
		EGUIL.LabelField("PRNG Settings");
		using (new IndentLevelScope()) {
			var _Rnd_Seed = FindProperty("_Rnd_Seed");
			var label_tex = new GUIContent("Seed Noise", "R16 texture filled with random values to help generating random numbers.");
			if (_Rnd_Seed != null) {
				using (new GUILayout.HorizontalScope()) {
					this.TexturePropertySmol(label_tex, _Rnd_Seed, false);
					if (GUILayout.Button("Default")) {
						_Rnd_Seed.textureValue = Generator.GetRndDefaultTexture();
					}
				}

				var value = _Rnd_Seed.textureValue as Texture2D;
				if (value == null) {
					EGUIL.HelpBox(
						"No seed noise texture is set!\n" +
						"Some of enabled features using Pseudo-Random Number Generator.\n" +
						"This texture is required, and shader will not properly work without this.",
						MessageType.Error
					);
				} else if (value.format != TextureFormat.R16) {
					EGUIL.HelpBox(
						"Seed noise texture is not encoded as R16!\n(Single red channel, 16 bit integer.)\n" +
						"Pseudo-Random Number Generator features is guaranteed to work only with R16 format.",
						MessageType.Warning
					);
				}
			} else {
				using (new DisabledScope(true))
					EGUIL.LabelField(label_tex, new GUIContent("Disabled"));
			}

			ShaderPropertyDisabled(FindProperty("_Rnd_ScreenScale"), "Screen Space Scale");
		}
	}

	protected void OnGUI_Textures() {
		EGUIL.LabelField("General Rendering Features");
		using (new IndentLevelScope()) {

			var _MainTex = FindProperty("_MainTex");
			var _MainTex_label = new GUIContent("Albedo (Main Texture)", "Albedo Main Color Texture (RGBA)");
			TexturePropertySmolDisabled(_MainTex_label, _MainTex);
			if (_MainTex != null && _MainTex.textureValue == null) {
				EGUIL.HelpBox(
					"No albedo texture is set! Disable main tex feature in shader generator, if you don't need this.",
					MessageType.Warning
				);
			}

			using (new IndentLevelScope()) {
				LabelEnumDisabledFromTagMixed<CutoutMode>("Forward Pass Cutout Mode", KFLTC.F_Cutout_Forward);
				LabelEnumDisabledFromTagMixed<CutoutMode>("Shadow Caster Cutout Mode", KFLTC.F_Cutout_ShadowCaster);

				ShaderPropertyDisabled(FindProperty("_Cutoff"), "Cutout (Classic)");
				ShaderPropertyDisabled(FindProperty("_CutoffMin"), "Cutout Min");
				ShaderPropertyDisabled(FindProperty("_CutoffMax"), "Cutout Max");

				ShaderPropertyDisabled(FindProperty("_Color"), "Color");

				var _ColorMask = FindProperty("_ColorMask");
				var _ColorMask_label = new GUIContent("Color Mask", "Masks Color Tint (R)");
				TexturePropertySmolDisabled(_ColorMask_label, _ColorMask);
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
						TexturePropertySmolDisabled(_EmissionMask_label, _EmissionMask);
						if (_EmissionMask != null && _EmissionMask.textureValue == null) {
							EGUIL.HelpBox(
								"No emission mask texture set! Disable emission mask feature in shader generator, if you don't need this.",
								MessageType.Warning
							);
						}

						var _EmissionMap_label = new GUIContent("Emission Texture", "Custom Emission Texture (RGB)");
						TexturePropertySmolDisabled(_EmissionMap_label, _EmissionMap);
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
			TexturePropertySmolDisabled(label, _BumpMap);
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
					this.TexturePropertySmol(new GUIContent("Ramp Texture", "Ramp Texture (RGB)"), rampTex);
					TextureCompatibilityWarning(rampTex);
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

	protected void OnGUI_MatCap() {
		var _MatCap = FindProperty("_MatCap");
		var _MatCap_Scale = FindProperty("_MatCap_Scale");
		var f_matCap = SC.AnyNotNull(_MatCap, _MatCap_Scale);
		using (new DisabledScope(!f_matCap)) {
			EGUIL.LabelField("MatCap Feature", f_matCap ? "Enabled" : "Disabled");
			using (new IndentLevelScope()) {
				if (f_matCap) {
					LabelEnumDisabledFromTagMixed<DistanceFadeMode>("Mode", KFLTC.F_MatcapMode);
					// TODO KeepUp bool label
					ShaderPropertyDisabled(_MatCap, "MatCap Texture");
					ShaderPropertyDisabled(_MatCap_Scale, "MatCap Power");
				}
			}
		}
	}

	protected void OnGUI_DistanceFade() {
		var _DstFd_Axis = FindProperty("_DstFd_Axis");
		var _DstFd_Near = FindProperty("_DstFd_Near");
		var _DstFd_Far = FindProperty("_DstFd_Far");
		var _DstFd_AdjustPower = FindProperty("_DstFd_AdjustPower");
		var _DstFd_AdjustScale = FindProperty("_DstFd_AdjustScale");
		var f_distanceFade = SC.AnyNotNull(_DstFd_Axis, _DstFd_Near, _DstFd_Far, _DstFd_AdjustPower, _DstFd_AdjustScale);
		using (new DisabledScope(!f_distanceFade)) {
			EGUIL.LabelField("Distance Fade Feature", f_distanceFade ? "Enabled" : "Disabled");
			using (new IndentLevelScope()) {
				if (f_distanceFade) {
					LabelEnumDisabledFromTagMixed<DistanceFadeMode>("Mode", KFLTC.F_DistanceFadeMode);
					ShaderPropertyDisabled(_DstFd_Axis, "Axis weights");
					ShaderPropertyDisabled(_DstFd_Near, "Near Distance");
					ShaderPropertyDisabled(_DstFd_Far, "Far Distance");
					ShaderPropertyDisabled(_DstFd_AdjustPower, "Power Adjust");
					ShaderPropertyDisabled(_DstFd_AdjustScale, "Scale Adjust");
				}
			}
		}
	}

	protected void OnGUI_WNoise() {
		// Commons.MaterialTagBoolCheck(this.target, Commons.KawaFLT_Feature_FPS);
		var _WNoise_Albedo = FindProperty("_WNoise_Albedo");
		var _WNoise_Em = FindProperty("_WNoise_Em");
		var f_wnoise = SC.AnyNotNull(_WNoise_Albedo, _WNoise_Em);
		using (new DisabledScope(!f_wnoise)) {
			EGUIL.LabelField("White Noise Feature", f_wnoise ? "Enabled" : "Disabled");
			using (new IndentLevelScope()) {
				if (f_wnoise) {
					ShaderPropertyDisabled(_WNoise_Albedo, "Noise on Albedo");
					ShaderPropertyDisabled(_WNoise_Em, "Noise on Emission");
				}
			}
		}
	}

	protected void OnGUI_FPS() {
		// Commons.MaterialTagBoolCheck(this.target, Commons.KawaFLT_Feature_FPS);
		var _FPS_TLo = FindProperty("_FPS_TLo");
		var _FPS_THi = FindProperty("_FPS_THi");
		var f_FPS = SC.AnyNotNull(_FPS_TLo, _FPS_THi);
		using (new DisabledScope(!f_FPS)) {
			EGUIL.LabelField("FPS Indication Feature", f_FPS ? "Enabled" : "Disabled");
			using (new IndentLevelScope()) {
				if (f_FPS) {
					LabelEnumDisabledFromTagMixed<FPSMode>("Mode", KFLTC.F_FPSMode);
					ShaderPropertyDisabled(_FPS_TLo, "Low FPS tint");
					ShaderPropertyDisabled(_FPS_THi, "High FPS tint");
				}
			}
		}
	}

	protected void OnGUI_PSX() {
		// Commons.MaterialTagBoolCheck(this.target, Commons.KawaFLT_Feature_FPS);
		var _PSX_SnapScale = FindProperty("_PSX_SnapScale");
		var f_PSX = SC.AnyNotNull(_PSX_SnapScale);
		using (new DisabledScope(!f_PSX)) {
			EGUIL.LabelField("PSX Effect Feature", f_PSX ? "Enabled" : "Disabled");
			using (new IndentLevelScope()) {
				if (f_PSX) {
					ShaderPropertyDisabled(_PSX_SnapScale, "Pixel Snap Scale");
				}
			}
		}
	}

	private void OnGUI_Outline() {
		var _outline_width = FindProperty("_outline_width");
		var _outline_color = FindProperty("_outline_color");
		var _outline_bias = FindProperty("_outline_bias");

		var f_Outline = SC.AnyNotNull(_outline_width, _outline_color, _outline_bias);
		using (new DisabledScope(!f_Outline)) {
			EGUIL.LabelField("Outline Feature", f_Outline ? "Enabled" : "Disabled");
			using (new IndentLevelScope()) {
				if (f_Outline) {
					LabelEnumDisabledFromTagMixed<OutlineMode>("Mode", KFLTC.F_OutlineMode);
					ShaderPropertyDisabled(_outline_width, "Outline width (cm)");
					ShaderPropertyDisabled(_outline_color, "Outline Color (Tint)");
					ShaderPropertyDisabled(_outline_bias, "Outline Z-Bias");
				}
			}
		}
	}

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

	private void OnGUI_PolyColorWave() {
		var _PCW_WvTmLo = FindProperty("_PCW_WvTmLo");
		var _PCW_WvTmAs = FindProperty("_PCW_WvTmAs");
		var _PCW_WvTmHi = FindProperty("_PCW_WvTmHi");
		var _PCW_WvTmDe = FindProperty("_PCW_WvTmDe");
		var _PCW_WvTmRnd = FindProperty("_PCW_WvTmRnd");
		var _PCW_WvTmUV = FindProperty("_PCW_WvTmUV");
		var _PCW_WvTmVtx = FindProperty("_PCW_WvTmVtx");

		var _PCW_Em = FindProperty("_PCW_Em");
		var _PCW_Color = FindProperty("_PCW_Color");
		var _PCW_RnbwTm = FindProperty("_PCW_RnbwTm");
		var _PCW_RnbwTmRnd = FindProperty("_PCW_RnbwTmRnd");
		var _PCW_RnbwStrtn = FindProperty("_PCW_RnbwStrtn");
		var _PCW_RnbwBrghtnss = FindProperty("_PCW_RnbwBrghtnss");
		var _PCW_Mix = FindProperty("_PCW_Mix");

		var f_PCW = shaderTags[KFLTC.F_PCW].IsTrue();
		using (new DisabledScope(!f_PCW)) {
			EGUIL.LabelField("Poly Color Wave Feature", f_PCW ? "Enabled" : "Disabled");
			using (new IndentLevelScope()) {
				if (f_PCW) {
					LabelShaderTagEnumValue<PolyColorWaveMode>(KFLTC.F_PCWMode, "Mode", "Unknown");

					EGUIL.LabelField("Wave timings:");
					float? time_period = null;
					using (new IndentLevelScope()) {
						ShaderPropertyDisabled(_PCW_WvTmLo, "Hidden");
						ShaderPropertyDisabled(_PCW_WvTmAs, "Fade-in");
						ShaderPropertyDisabled(_PCW_WvTmHi, "Shown");
						ShaderPropertyDisabled(_PCW_WvTmDe, "Fade-out");
						time_period = OnGUI_PolyColorWave_WvTmHelper(_PCW_WvTmLo, _PCW_WvTmAs, _PCW_WvTmHi, _PCW_WvTmDe);

						ShaderPropertyDisabled(_PCW_WvTmRnd, "Random per tris");

						EGUIL.LabelField("Time offset from UV0 (XY) and UV1 (ZW):");
						ShaderPropertyDisabled(_PCW_WvTmUV, "");

						EGUIL.LabelField("Time offset from mesh-space coords: ");
						ShaderPropertyDisabled(_PCW_WvTmVtx, "");
					}

					EGUIL.LabelField("Wave coloring:");
					using (new IndentLevelScope()) {
						ShaderPropertyDisabled(_PCW_Em, "Emissiveness");
						ShaderPropertyDisabled(_PCW_Color, "Color");
						ShaderPropertyDisabled(_PCW_RnbwTm, "Rainbow time");
						ShaderPropertyDisabled(_PCW_RnbwTmRnd, "Rainbow time random");
						ShaderPropertyDisabled(_PCW_RnbwStrtn, "Rainbow saturation");
						ShaderPropertyDisabled(_PCW_RnbwBrghtnss, "Rainbow brightness");
						ShaderPropertyDisabled(_PCW_Mix, "Color vs. Rainbow");
					}
					if (time_period.HasValue && _PCW_RnbwTm != null && !_PCW_RnbwTm.hasMixedValue) {
						var time_rainbow = _PCW_RnbwTm.floatValue;
						var gcd_t = gcd(time_rainbow, time_period.Value);
						var lcm_t = time_rainbow * time_period / gcd_t;
						CommonEditor.HelpBoxRich(string.Format(
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
			CommonEditor.HelpBoxRich(string.Format("Time for singe wave cycle: <b>{0:f}</b> sec. ", t4));

			return t4;
		} else {
			return null;
		}
	}

	public override void OnInspectorGUI() {
		if (!isVisible)
			return;

		if (targets.Length > 1) {
			EGUIL.HelpBox("Multi-select is not yet properly tested, it can break your materals! Not yet recomended to use.", MessageType.Warning, true);
		}

		if (!GenaratorGUIDFields())
			return;

		EGUIL.Space();
		OnGUI_BlendMode();

		EGUIL.Space();
		OnGUI_Tessellation();

		EGUIL.Space();
		OnGUI_Random();

		EGUIL.Space();
		OnGUI_Textures();

		EGUIL.Space();
		OnGUI_Shading();

		EGUIL.Space();
		OnGUI_Outline();

		EGUIL.Space();
		OnGUI_MatCap();

		EGUIL.Space();
		OnGUI_DistanceFade();

		EGUIL.Space();
		OnGUI_WNoise();

		EGUIL.Space();
		OnGUI_FPS();

		EGUIL.Space();
		OnGUI_PSX();

		EGUIL.Space();
		OnGUI_InfinityWarDecimation();

		EGUIL.Space();
		OnGUI_PolyColorWave();

	}
}
