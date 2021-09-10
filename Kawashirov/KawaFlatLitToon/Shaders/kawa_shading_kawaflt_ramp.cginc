#if defined(SHADE_KAWAFLT_RAMP) && !defined(KAWAFLT_SHADING_KAWAFLT_RAMP_INCLUDED)
#define KAWAFLT_SHADING_KAWAFLT_RAMP_INCLUDED

#include "UnityLightingCommon.cginc"
#include "UnityStandardUtils.cginc"

#include ".\kawa_shading_kawaflt_common.cginc"

/*
	Kawashirov's Flat Lit Toon Ramp
*/

uniform float _Sh_Kwshrv_ShdBlnd;

UNITY_DECLARE_TEX2D(_Sh_KwshrvRmp_Tex);
uniform float _Sh_KwshrvRmp_Pwr;
uniform float4 _Sh_KwshrvRmp_NdrctClr;

inline half3 frag_shade_kawaflt_ramp_apply(half uv) {
	uv = pow(uv, _Sh_KwshrvRmp_Pwr);
	uv = uv * uv * (3.0 - 2.0 * uv); // Cubic Hermite H01 interoplation
	return UNITY_SAMPLE_TEX2D(_Sh_KwshrvRmp_Tex, half2(uv, uv)).rgb;
}

#if defined(FRAGMENT_IN)
	inline half3 frag_shade_kawaflt_ramp_forward_main(FRAGMENT_IN i, half3 normal) {
		half light_atten = frag_shade_kawaflt_attenuation_no_shadow(i.pos_world.xyz);

		half shadow_atten = UNITY_SHADOW_ATTENUATION(i, i.pos_world.xyz);
		float3 wsld = normalize(UnityWorldSpaceLightDir(i.pos_world.xyz));
		half ramp_uv = dot(normal, wsld) * 0.5 + 0.5;
		half3 shade_blended = frag_shade_kawaflt_ramp_apply(ramp_uv * shadow_atten);
		half3 shade_separated = frag_shade_kawaflt_ramp_apply(ramp_uv) * shadow_atten;
		half3 shade = lerp(shade_separated, shade_blended, _Sh_Kwshrv_ShdBlnd);

		return _LightColor0.rgb * max(0.0h, light_atten * shade);
	}
	
	#ifdef KAWAFLT_PASS_FORWARDBASE
		inline half3 frag_shade_kawaflt_ramp_forward_base(FRAGMENT_IN i, half3 albedo, half3 normal3, half3 emission) {
			half3 ambient = half3(0,0,0);
			#if defined(UNITY_SHOULD_SAMPLE_SH)
				ambient = half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w) * _Sh_KwshrvRmp_NdrctClr.rgb * _Sh_KwshrvRmp_NdrctClr.a;
				ambient = max(half3(0,0,0), ambient);
			#endif

			half3 main = frag_shade_kawaflt_ramp_forward_main(i, normal3);

			return albedo * (main + i.vertexlight + ambient) + emission;
		}
	#endif
	
	#ifdef KAWAFLT_PASS_FORWARDADD
		inline half3 frag_shade_kawaflt_ramp_forward_add(FRAGMENT_IN i, half3 albedo, half3 normal) {
			half3 main = frag_shade_kawaflt_ramp_forward_main(i, normal);
			return max(half3(0,0,0), albedo * main);
		}
	#endif

#endif // defined(FRAGMENT_IN)

#endif // KAWAFLT_SHADING_KAWAFLT_RAMP_INCLUDED