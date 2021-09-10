#ifndef KAWAFLT_SHADING_KAWAFLT_LOG_INCLUDED
#define KAWAFLT_SHADING_KAWAFLT_LOG_INCLUDED

#include "UnityLightingCommon.cginc"
#include "UnityStandardUtils.cginc"

#include ".\kawa_shading_kawaflt_common.cginc"

/*
	Kawashirov's Flat Lit Toon Log Diffuse-based
*/

#if defined(SHADE_KAWAFLT_LOG)
	uniform float _Sh_Kwshrv_ShdBlnd;

	uniform float _Sh_Kwshrv_RimScl;
	uniform float4 _Sh_Kwshrv_RimClr;
	uniform float _Sh_Kwshrv_RimPwr;
	uniform float _Sh_Kwshrv_RimBs;

	uniform float _Sh_KwshrvLog_Fltnss;
	uniform float _Sh_Kwshrv_BndSmth;
	uniform float _Sh_Kwshrv_FltLogSclA;

	uniform float _Sh_Kwshrv_Smth;
	uniform float _Sh_Kwshrv_Smth_Tngnt;
#endif

#if defined(FRAGMENT_IN) && defined(SHADE_KAWAFLT_LOG)
	inline half3 frag_shade_kawaflt_log_round(half value) {
		// only apply bound smooth when it's noticeble
		// <0.01 full-sharp bound
		// 0.01..0.99 mixed bound
		// >0.99 full-smooth bound
		UNITY_BRANCH if (_Sh_Kwshrv_BndSmth < 0.01) {
			value = round(value);
		} else {
			half smooth = _Sh_Kwshrv_BndSmth / 2.0h;
			half value_frac = frac(value + 0.5);
			half left_pulse = (saturate(value_frac / smooth) - 1.0h) / 2.0h;
			half right_pulse = saturate((value_frac - 1.0h) / smooth + 1.0h) / 2.0h;
			value = round(value) + left_pulse + right_pulse; 
		}
		return value;
	}

	inline half frag_shade_kawaflt_log_rim_factor(half tangency) {
		return 1.0h + (pow(1.0h - abs(tangency), _Sh_Kwshrv_RimPwr) + _Sh_Kwshrv_RimBs) * _Sh_Kwshrv_RimScl;
	}

	inline float frag_shade_kawaflt_log_smooth_tangency(float tangency) {
		return saturate(lerp(tangency, _Sh_Kwshrv_Smth_Tngnt, _Sh_Kwshrv_Smth));
	}

	inline half frag_shade_kawaflt_log_steps_mono(half atten) {
		UNITY_BRANCH if ( _Sh_KwshrvLog_Fltnss > 0.01 && _Sh_Kwshrv_BndSmth < 0.99 && atten > 0.01) {
			// Only apply steps when flatness noticeble
			float layers = atten;
			layers = log(layers) * _Sh_Kwshrv_FltLogSclA;
			layers = frag_shade_kawaflt_log_round(layers);
			layers = exp(layers / _Sh_Kwshrv_FltLogSclA);
			atten = lerp(atten, layers, _Sh_KwshrvLog_Fltnss);
		}
		return atten;
	}

	inline half frag_shade_kawaflt_log_steps_color(half color) {
		UNITY_BRANCH if ( _Sh_KwshrvLog_Fltnss > 0.01 && _Sh_Kwshrv_BndSmth < 0.99 && all(color > half3(0.01, 0.01, 0.01) )) {
			// Only apply steps when flatness noticeble
			float luma = Luminance(color);
			float layers = color;
			layers = log(layers) * _Sh_Kwshrv_FltLogSclA;
			layers = frag_shade_kawaflt_log_round(layers);
			layers = exp(layers / _Sh_Kwshrv_FltLogSclA);
			color = lerp(color, layers * (layers / luma), _Sh_KwshrvLog_Fltnss);
		}
		return color;
	}

	inline half3 frag_shade_kawaflt_log_forward_main(FRAGMENT_IN i, half3 normal, half rim_factor) {
		half light_atten = frag_shade_kawaflt_attenuation_no_shadow(i.pos_world.xyz);

		float3 wsld = normalize(UnityWorldSpaceLightDir(i.pos_world.xyz));
		float tangency = max(0, dot(normal, wsld));
		tangency = frag_shade_kawaflt_log_smooth_tangency(tangency);
		half shadow_atten = UNITY_SHADOW_ATTENUATION(i, i.pos_world.xyz);
		half shade_blended = frag_shade_kawaflt_log_steps_mono(tangency * rim_factor * shadow_atten);
		half shade_separated = frag_shade_kawaflt_log_steps_mono(tangency * rim_factor) * shadow_atten;
		half shade = lerp(shade_separated, shade_blended, _Sh_Kwshrv_ShdBlnd);

		return _LightColor0.rgb * max(0.0h, light_atten * shade);
	}

	#ifdef KAWAFLT_PASS_FORWARDBASE
		inline half3 frag_shade_kawaflt_log_forward_base(FRAGMENT_IN i, half3 albedo, half3 normal3, half3 emission) {
			float3 view_dir = normalize(KawaWorldSpaceViewDir(i.pos_world));
			float view_tangency = dot(normal3, view_dir);
			half rim_factor = frag_shade_kawaflt_log_rim_factor(view_tangency);

			half3 vertexlight = half3(0,0,0);
			vertexlight = i.vertexlight * rim_factor;
			vertexlight = max(frag_shade_kawaflt_log_steps_color(vertexlight), half3(0,0,0));

			half3 ambient = half3(0,0,0);
			#if defined(UNITY_SHOULD_SAMPLE_SH)
				ambient = i.ambient + SHEvalLinearL2(half4(normal3, 1));
				ambient = lerp(ambient, half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w), /* _Sh_Kwshrv_Smth */ 1.0) * rim_factor;
				// ambient = max(frag_shade_kawaflt_log_steps_color(ambient), half3(0,0,0));
			#endif

			half3 main = frag_shade_kawaflt_log_forward_main(i, normal3, rim_factor);

			return albedo * (main + vertexlight + ambient) + emission;
		}
	#endif
	
	#ifdef KAWAFLT_PASS_FORWARDADD
		inline half3 frag_shade_kawaflt_log_forward_add(FRAGMENT_IN i, half3 albedo, half3 normal) {
			float3 view_dir = normalize(KawaWorldSpaceViewDir(i.pos_world));
			float view_tangency = dot(normal, view_dir);
			half rim_factor = frag_shade_kawaflt_log_rim_factor(view_tangency);

			half3 main = frag_shade_kawaflt_log_forward_main(i, normal, rim_factor);
			return max(half3(0,0,0), albedo * main);
		}
	#endif
#endif // defined(FRAGMENT_IN) && defined(SHADE_KAWAFLT_LOG)

#endif // KAWAFLT_SHADING_KAWAFLT_LOG_INCLUDED