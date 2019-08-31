using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Kawashirov.FLT;
using System.Linq;

// Имя файла длжно совпадать с именем типа.
// https://forum.unity.com/threads/solved-blank-scriptableobject-on-import.511527/
// Тип не включен в неймспейс Kawashirov.FLT, т.к. эдитор указывается в файле .shader без указания неймспейса.

public class KawaFLTMaterialEditor : MaterialEditor {

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
		//MaterialProperty mp = null;
		//this.materialProperties.TryGetValue(name, out mp);
		var mp = GetMaterialProperty(this.targets, name);
		return mp != null && !string.IsNullOrEmpty(mp.name) && mp.targets != null && mp.targets.Length > 0 ? mp : null;
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

	protected void TexturePropertySmol(GUIContent label, MaterialProperty prop, bool compatibility)
	{
		this.TexturePropertySmol(label, prop);
		if (compatibility) {
			this.TextureCompatibilityWarning(prop);
		}
	}

	protected void TexturePropertySmolDisabled(GUIContent label, MaterialProperty prop, bool compatibility = true)
	{
		using (new EditorGUI.DisabledScope(prop == null)) {
			if (prop != null) {
				this.TexturePropertySmol(label, prop, compatibility);
			} else {
				EditorGUILayout.LabelField(label, new GUIContent("Disabled"));
			}
		}

	}

	protected void ShaderPropertyDisabled(MaterialProperty property, string label = null)
	{
		if (string.IsNullOrEmpty(label) && property != null) {
			label = property.name;
		}
		var gui_label = new GUIContent(label);
		this.ShaderPropertyDisabled(property, gui_label);
	}

	protected void ShaderPropertyDisabled(MaterialProperty property, GUIContent label = null)
	{
		if (property != null) {
			this.ShaderProperty(property, label);
		} else {
			using (new EditorGUI.DisabledScope(true)) {
				EditorGUILayout.LabelField(label, new GUIContent("Disabled"));
			}
		}
	}

	protected static void LabelEnum<E>(string label, E value, Dictionary<E, string> display = null) where E : struct
	{
		string label2 = null;
		if (display != null && display.Count > 0) {
			display.TryGetValue(value, out label2);
		}
		if (string.IsNullOrEmpty(label2)) {
			label2 = Enum.GetName(typeof(E), value);
		}
		EditorGUILayout.LabelField(label, label2);
	}

	protected static void LabelEnumDisabled<E>(string label, E? value, Dictionary<E, string> display = null) where E : struct
	{
		if (value.HasValue) {
			LabelEnum(label, value.Value, display);
		} else if (!string.IsNullOrEmpty(label)) {
			using (new EditorGUI.DisabledScope(true)) {
				EditorGUILayout.LabelField(label, "Disabled");
			}
		}
	}

	protected static void LabelEnumDisabledFromTag<E>(
		string label, object material, string tag, Dictionary<E, string> display = null, E? defualt = null
	) where E : struct
	{
		var e = Commons.MaterialTagEnumGetSafe(material, tag, defualt);
		LabelEnumDisabled(label, e, display);
	}

	protected static void LabelEnumDisabledFromTagMixed<E>(
		string label, IEnumerable<object> materials, string tag, Dictionary<E, string> display = null, E? defualt = null
	) where E : struct
	{
		var values = new HashSet<E>();
		foreach (var material in materials) {
			var value = Commons.MaterialTagEnumGetSafe<E>(material, tag);
			if (value.HasValue) {
				values.Add(value.Value);
			}
		}
		if (values.Count < 1 && defualt.HasValue) {
			values.Add(defualt.Value);
		}
		if (values.Count < 1) {
			using (new EditorGUI.DisabledScope(true)) {
				EditorGUILayout.LabelField(label, "Disabled");
			}
		} else if (values.Count > 1) {
			EditorGUILayout.LabelField(label, "Mixed Values");
		} else {
			LabelEnum(label, values.First(), display);
		}
	}

	protected void OnGUI_BlendMode()
	{
		var debug = Commons.MaterialTagBoolCheck(this.target, "KawaFLT_Feature_Debug");
		var instancing = Commons.MaterialTagBoolCheck(this.target, "KawaFLT_Feature_Instancing");

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
		// Commons.MaterialTagBoolCheck(this.target, Commons.KawaFLT_Feature_Tessellation);
		var _Tsltn_Uni = this.FindProperty("_Tsltn_Uni");
		var _Tsltn_Nrm = this.FindProperty("_Tsltn_Nrm");
		var _Tsltn_Inside = this.FindProperty("_Tsltn_Inside");
		var tessellation = _Tsltn_Uni != null && _Tsltn_Nrm != null && _Tsltn_Inside != null;
		using (new EditorGUI.DisabledScope(!tessellation)) {
			if (tessellation) {
				EditorGUILayout.LabelField("Tessellation", "Enabled");
				using (new EditorGUI.IndentLevelScope()) {
					var partitioning = Commons.MaterialTagEnumGet<TessPartitioning>(this.target, Commons.KawaFLT_Feature_Partitioning);
					var domain = Commons.MaterialTagEnumGet<TessDomain>(this.target, Commons.KawaFLT_Feature_Domain);
					EditorGUILayout.LabelField("Partitioning", Enum.GetName(typeof(TessPartitioning), partitioning));
					EditorGUILayout.LabelField("Domain", Enum.GetName(typeof(TessDomain), domain));
					this.ShaderProperty(_Tsltn_Uni, "Uniform factor");
					this.ShaderProperty(_Tsltn_Nrm, "Factor from curvness");
					this.ShaderProperty(_Tsltn_Inside, "Inside multiplier");
				}
			} else {
				EditorGUILayout.LabelField("Tessellation", "Disabled");
			}
		}
	}

	protected void OnGUI_Random()
	{
		var random = Commons.MaterialTagBoolCheck(this.target, Commons.KawaFLT_Feature_Random);


		var _Rnd_Seed = this.FindProperty("_Rnd_Seed");
		var label = new GUIContent(
			"Seed Noise", "Red-Texture filled with random values to help generating random numbers."
		);
		this.TexturePropertySmolDisabled(label, _Rnd_Seed);
		if (_Rnd_Seed != null && _Rnd_Seed.textureValue == null){
			EditorGUILayout.HelpBox(
				"No seed noise texture is set! " +
				"Some of enabled features using Pseudo-Random Number Generator. " +
				"This texture is required, and shader will not properly work without this.",
				MessageType.Error
			);
		}
	}

	protected void OnGUI_Textures()
	{
		EditorGUILayout.LabelField("General Rendering Features");
		using (new EditorGUI.IndentLevelScope()) {

			var _MainTex = this.FindProperty("_MainTex");
			var _MainTex_label = new GUIContent("Albedo (Main Texture)", "Albedo Main Color Texture (RGBA)");
			this.TexturePropertySmolDisabled(_MainTex_label, _MainTex);
			if (_MainTex != null && _MainTex.textureValue == null) {
				EditorGUILayout.HelpBox(
					"No albedo texture is set! Disable main tex feature in shader generator, if you don't need this.",
					MessageType.Warning
				);
			}

			using (new EditorGUI.IndentLevelScope()) {
				LabelEnumDisabledFromTagMixed<CutoutMode>(
					"Forward Pass Cutout Mode", this.targets, Commons.KawaFLT_Feature_Cutout_Forward
				);

				LabelEnumDisabledFromTagMixed<CutoutMode>(
					"Shadow Caster Cutout Mode", this.targets, Commons.KawaFLT_Feature_Cutout_ShadowCaster
				);

				this.ShaderPropertyDisabled(this.FindProperty("_Cutoff"), "Cutout (Classic)");
				this.ShaderPropertyDisabled(this.FindProperty("_CutoffMin"), "Cutout Min");
				this.ShaderPropertyDisabled(this.FindProperty("_CutoffMax"), "Cutout Max");

				this.ShaderPropertyDisabled(this.FindProperty("_Color"), "Color");

				var _ColorMask = this.FindProperty("_ColorMask");
				var _ColorMask_label = new GUIContent("Color Mask", "Masks Color Tint (R)");
				this.TexturePropertySmolDisabled(_ColorMask_label, _ColorMask);
				if (_ColorMask != null && _ColorMask.textureValue == null) {
					EditorGUILayout.HelpBox(
						"No color mask texture set! Disable main texture color mask feature in shader generator, if you don't need this.",
						MessageType.Warning
					);
				}
			}

			var _EmissionMask = this.FindProperty("_EmissionMask");
			var _EmissionMap = this.FindProperty("_EmissionMap");
			var _EmissionColor = this.FindProperty("_EmissionColor");
			var f_emission = _EmissionMask != null || _EmissionMap != null || _EmissionColor != null;
			
			using (new EditorGUI.DisabledScope(!f_emission)) {
				EditorGUILayout.LabelField("Emission Feature", f_emission ? "Enabled" : "Disabled");
				if (f_emission) {
					using (new EditorGUI.IndentLevelScope()) {
						LabelEnumDisabledFromTagMixed(
							"Emission Mode", this.targets, Commons.KawaFLT_Feature_EmissionMode, Commons.emissionMode
						);

						var _EmissionMask_label = new GUIContent("Emission Mask", "Mask for Emission by Albedo Main Texture (R)");
						this.TexturePropertySmolDisabled(_EmissionMask_label, _EmissionMask);
						if (_EmissionMask != null && _EmissionMask.textureValue == null) {
							EditorGUILayout.HelpBox(
								"No emission mask texture set! Disable emission mask feature in shader generator, if you don't need this.",
								MessageType.Warning
							);
						}

						var _EmissionMap_label = new GUIContent("Emission Texture", "Custom Emission Texture (RGB)");
						this.TexturePropertySmolDisabled(_EmissionMap_label, _EmissionMap);
						if (_EmissionMap != null && _EmissionMap.textureValue == null) {
							EditorGUILayout.HelpBox(
								"No emission map texture set! Disable emission map feature in shader generator, if you don't need this.",
								MessageType.Warning
							);
						}

						this.ShaderPropertyDisabled(_EmissionColor, new GUIContent("Emission Color (Tint)", "Emission Color Tint (RGB)"));
						var _EmissionColor_value = _EmissionColor.colorValue;
						var intencity = (_EmissionColor_value.r + _EmissionColor_value.g + _EmissionColor_value.b) * _EmissionColor_value.a;
						if (intencity < 0.05) {
							EditorGUILayout.HelpBox(
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
				EditorGUILayout.HelpBox(
					"Normal map texture is not set! Disable normal feature in shader generator, if you don't need this.",
					MessageType.Warning
				);
			}
			using (new EditorGUI.IndentLevelScope()) {
				var _BumpScale = this.FindProperty("_BumpScale");
				this.ShaderPropertyDisabled(_BumpScale, "Normal Map Scale");
				if (_BumpScale != null && _BumpScale.floatValue < 0.05) {
					EditorGUILayout.HelpBox(
						"Normal map scale value is close to zero! In this situation, may be it's better to disable normal feature in shader generator, if you don't need this?",
						MessageType.Warning
					);
				}
			}
		}
	}

	protected void OnGUI_Shading()
	{
		var shading = Commons.MaterialTagEnumGet<ShadingMode>(this.target, Commons.KawaFLT_Feature_Shading);
		EditorGUILayout.LabelField("Shading", Enum.GetName(typeof(ShadingMode), shading));
		using (new EditorGUI.IndentLevelScope()) {
			EditorGUILayout.HelpBox(Commons.shadingModeDesc[shading], MessageType.Info);
			if (shading == ShadingMode.CubedParadoxFLT) {
				this.ShaderPropertyDisabled(this.FindProperty("_Sh_Cbdprdx_Shadow"), "Shadow");
			} else if (shading == ShadingMode.KawashirovFLTSingle) {
				this.ShaderPropertyDisabled(this.FindProperty("_Sh_Kwshrv_ShdBlnd"), "RT Shadows blending");

				EditorGUILayout.LabelField("Sides threshold");
				using (new EditorGUI.IndentLevelScope()) {
					this.ShaderPropertyDisabled(this.FindProperty("_Sh_KwshrvSngl_TngntLo"), "Low");
					this.ShaderPropertyDisabled(this.FindProperty("_Sh_KwshrvSngl_TngntHi"), "High");
				}

				EditorGUILayout.LabelField("Brightness");
				using (new EditorGUI.IndentLevelScope()) {
					this.ShaderPropertyDisabled(this.FindProperty("_Sh_KwshrvSngl_ShdLo"), "Back side (Shaded)");
					this.ShaderPropertyDisabled(this.FindProperty("_Sh_KwshrvSngl_ShdHi"), "Front side (Lit)");
				}
			} else if (shading == ShadingMode.KawashirovFLTRamp) {
				var rampTex = this.FindProperty("_Sh_KwshrvRmp_Tex");
				this.ShaderPropertyDisabled(this.FindProperty("_Sh_Kwshrv_ShdBlnd"), "RT Shadows blending");
				this.TexturePropertySmol(new GUIContent("Ramp Texture", "Ramp Texture (RGB)"), rampTex);
				this.TextureCompatibilityWarning(rampTex);
				if (rampTex.textureValue == null) {
					EditorGUILayout.HelpBox(
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
		var f_distanceFade = Commons.AnyNotNull(_DstFd_Axis, _DstFd_Near, _DstFd_Far, _DstFd_AdjustPower, _DstFd_AdjustScale);
		using (new EditorGUI.DisabledScope(!f_distanceFade)) {
			EditorGUILayout.LabelField("Distance Fade Feature", f_distanceFade ? "Enabled" : "Disabled");
			using (new EditorGUI.IndentLevelScope()) {
				if (f_distanceFade) {
					LabelEnumDisabledFromTagMixed<DistanceFadeMode>("Mode", this.targets, Commons.KawaFLT_Feature_DistanceFadeMode);
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
		var f_FPS = Commons.AnyNotNull(_FPS_TLo, _FPS_THi);
		using (new EditorGUI.DisabledScope(!f_FPS)) {
			EditorGUILayout.LabelField("FPS Indication Feature", f_FPS ? "Enabled" : "Disabled");
			using (new EditorGUI.IndentLevelScope()) {
				if (f_FPS) {
					LabelEnumDisabledFromTagMixed<FPSMode>("Mode", this.targets, Commons.KawaFLT_Feature_FPSMode);
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

		var f_Outline = Commons.AnyNotNull(_outline_width,  _outline_color, _outline_bias);
		using (new EditorGUI.DisabledScope(!f_Outline)) {
			EditorGUILayout.LabelField("Outline Feature", f_Outline ? "Enabled" : "Disabled");
			using (new EditorGUI.IndentLevelScope()) {
				if (f_Outline) {
					LabelEnumDisabledFromTagMixed<OutlineMode>("Mode", this.targets, Commons.KawaFLT_Feature_OutlineMode);
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

		var f_InfinityWarDecimation = Commons.AnyNotNull(
			_IWD_Plane, _IWD_PlaneDistRandomness,
			_IWD_DirRandomWeight, _IWD_DirPlaneWeight, _IWD_DirNormalWeight, _IWD_DirObjectWeight, _IWD_DirWorldWeight,
			_IWD_DirObjectVector, _IWD_DirWorldVector,
			_IWD_MoveSpeed, _IWD_MoveAccel,
			_IWD_CmprssFar, _IWD_TintFar, _IWD_TintColor, _IWD_Tsltn
		);

		using (new EditorGUI.DisabledScope(!f_InfinityWarDecimation)) {
			EditorGUILayout.LabelField("Infinity War Decimation Feature", f_InfinityWarDecimation ? "Enabled" : "Disabled");
			using (new EditorGUI.IndentLevelScope()) {
				if (f_InfinityWarDecimation) {
					EditorGUILayout.LabelField("I don't feel so good...");
					EditorGUILayout.LabelField("Particles Front");
					using (new EditorGUI.IndentLevelScope()) {
						EditorGUILayout.LabelField("General equation of a Plane (XYZ is normal, W is offset)");
						this.ShaderPropertyDisabled(_IWD_Plane, "");
						this.ShaderPropertyDisabled(_IWD_PlaneDistRandomness, "Randomness (W)");
					}
					EditorGUILayout.LabelField("Particles Direction");
					using (new EditorGUI.IndentLevelScope()) {
						this.ShaderPropertyDisabled(_IWD_DirRandomWeight, "Random");
						this.ShaderPropertyDisabled(_IWD_DirPlaneWeight, "Particles Front Plane");
						this.ShaderPropertyDisabled(_IWD_DirNormalWeight, "Normal");
						this.ShaderPropertyDisabled(_IWD_DirObjectWeight, "Object Space Vector");
						using (new EditorGUI.IndentLevelScope()) {
							this.ShaderPropertyDisabled(_IWD_DirObjectVector, "");
						}
						this.ShaderPropertyDisabled(_IWD_DirWorldWeight, "World Space Vector");
						using (new EditorGUI.IndentLevelScope()) {
							this.ShaderPropertyDisabled(_IWD_DirWorldVector, "");
						}
					}
					EditorGUILayout.LabelField("Particles Movement");
					using (new EditorGUI.IndentLevelScope()) {
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

		var f_PCW = Commons.MaterialTagBoolCheck(this.target, Commons.KawaFLT_Feature_PCW);
		using (new EditorGUI.DisabledScope(!f_PCW)) {
			EditorGUILayout.LabelField("Poly Color Wave Feature", f_PCW ? "Enabled" : "Disabled");
			using (new EditorGUI.IndentLevelScope()) {
				if (f_PCW) {
					var f_PCWMode = Commons.MaterialTagEnumGet<PolyColorWaveMode>(this.target, Commons.KawaFLT_Feature_PCWMode);
					EditorGUILayout.LabelField("Mode", Enum.GetName(typeof(PolyColorWaveMode), f_PCWMode));

					EditorGUILayout.LabelField("Wave timings:");
					float? time_period = null;
					using (new EditorGUI.IndentLevelScope()) {
						this.ShaderPropertyDisabled(_PCW_WvTmLo, "Hidden");
						this.ShaderPropertyDisabled(_PCW_WvTmAs, "Fade-in");
						this.ShaderPropertyDisabled(_PCW_WvTmHi, "Shown");
						this.ShaderPropertyDisabled(_PCW_WvTmDe, "Fade-out");
						time_period = this.OnGUI_PolyColorWave_WvTmHelper(_PCW_WvTmLo, _PCW_WvTmAs, _PCW_WvTmHi, _PCW_WvTmDe);

						this.ShaderPropertyDisabled(_PCW_WvTmRnd, "Random per tris");

						EditorGUILayout.LabelField("Time offset from UV0 (XY) and UV1 (ZW):");
						this.ShaderPropertyDisabled(_PCW_WvTmUV, "");

						EditorGUILayout.LabelField("Time offset from mesh-space coords: ");
						this.ShaderPropertyDisabled(_PCW_WvTmVtx, "");
					}

					EditorGUILayout.LabelField("Wave coloring:");
					using (new EditorGUI.IndentLevelScope()) {
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
		MaterialProperty _PCW_WvTmLo, MaterialProperty _PCW_WvTmAs, MaterialProperty _PCW_WvTmHi, MaterialProperty _PCW_WvTmDe
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
			EditorGUILayout.CurveField("Preview amplitude (read-only)", time_curve);
			HelpBoxRich(string.Format("Time for singe wave cycle: <b>{0:f}</b> sec. ", t4));

			return t4;
		} else {
			return null;
		}
	}

	public override void OnEnable()
	{
		base.OnEnable();

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
			var generator_guid = Commons.MaterialTagGet(this.target, Commons.KawaFLT_GenaratorGUID);
			var generator_path = AssetDatabase.GUIDToAssetPath(generator_guid);
			var generator_obj = AssetDatabase.LoadAssetAtPath<Generator>(generator_path);
			using (new EditorGUI.DisabledScope(generator_obj == null)) {
				EditorGUILayout.ObjectField("Shader Generator", generator_obj, typeof(Generator), false);
			}
		} catch (Exception exc) {
			EditorGUILayout.LabelField("Shader Generator Error", exc.ToString());
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