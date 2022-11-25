#if defined(SHADE_KAWAFLT_SINGLE) && !defined(KAWAFLT_SHADING_KAWAFLT_SINGLE_INCLUDED)
#define KAWAFLT_SHADING_KAWAFLT_SINGLE_INCLUDED

#include "UnityLightingCommon.cginc"
#include "UnityStandardUtils.cginc"

#include ".\kawa_shading_kawaflt_common.cginc"

/*
	Kawashirov's Flat Lit Toon Single Diffuse-based
*/

uniform float _Sh_Kwshrv_ShdBlnd;
uniform float _Sh_Kwshrv_ShdAmbnt;
uniform float _Sh_KwshrvSngl_TngntLo;
uniform float _Sh_KwshrvSngl_TngntHi;
uniform float _Sh_KwshrvSngl_ShdLo;
uniform float _Sh_KwshrvSngl_ShdHi;

inline float shade_kawaflt_single(float tangency, float shadow_atten) {
	half2 t;
	t.x = min(_Sh_KwshrvSngl_TngntLo, _Sh_KwshrvSngl_TngntHi);
	t.y = max(_Sh_KwshrvSngl_TngntLo, _Sh_KwshrvSngl_TngntHi);
	t = 2.0 * t - 1.0;
	half ref_light = saturate( (tangency - t.x) / (t.y - t.x) );
	ref_light = ref_light * ref_light * (3.0 - 2.0 * ref_light); // Cubic Hermite H01 interpolation
	half sh_blended = lerp(1.0, shadow_atten, _Sh_Kwshrv_ShdBlnd);
	half sh_separated = lerp(shadow_atten, 1.0, _Sh_Kwshrv_ShdBlnd);
	return lerp(_Sh_KwshrvSngl_ShdLo, _Sh_KwshrvSngl_ShdHi, ref_light * sh_blended) * sh_separated;
}


#if defined(FRAGMENT_IN)
	inline half3 frag_shade_kawaflt_single_forward_main(FRAGMENT_IN i, half3 normal) {
		half light_atten = frag_shade_kawaflt_attenuation_no_shadow(i.pos_world.xyz);

		half shadow_atten = UNITY_SHADOW_ATTENUATION(i, i.pos_world.xyz);
		float3 dir = normalize(UnityWorldSpaceLightDir(i.pos_world.xyz));
		half tangency = dot(normal, dir);
		half shade = shade_kawaflt_single(tangency, shadow_atten);

		return _LightColor0.rgb * max(0.0h, light_atten * shade);
	}
	
	#ifdef KAWAFLT_PASS_FORWARDBASE
		inline half3 frag_shade_kawaflt_single_forward_base(FRAGMENT_IN i, half3 albedo, half3 normal3, half3 emission) {
			half3 ambient = half3(0,0,0);
			#if defined(UNITY_SHOULD_SAMPLE_SH)
				half3 ambient_l2 = lerp(half3(0,0,0), SHEvalLinearL2(half4(normal3, 1)), _Sh_Kwshrv_ShdAmbnt);
				ambient = i.ambient + max(half3(0,0,0), ambient_l2);
			#endif

			half3 main = frag_shade_kawaflt_single_forward_main(i, normal3);

			return albedo * (main + i.vertexlight + ambient) + emission;
		}
	#endif
	
	#ifdef KAWAFLT_PASS_FORWARDADD
		inline half3 frag_shade_kawaflt_single_forward_add(FRAGMENT_IN i, half3 albedo, half3 normal) {
			half3 main = frag_shade_kawaflt_single_forward_main(i, normal);
			return max(half3(0,0,0), albedo * main);
		}
	#endif
#endif // defined(FRAGMENT_IN)

#endif // KAWAFLT_SHADING_KAWAFLT_SINGLE_INCLUDED