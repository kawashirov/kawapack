#if defined(SHADE_KAWAFLT_SINGLE) && !defined(KAWAFLT_SHADING_KAWAFLT_SINGLE_INCLUDED)
#define KAWAFLT_SHADING_KAWAFLT_SINGLE_INCLUDED

// KawaShade Single Diffuse-based

uniform float _Sh_Kwshrv_ShdScl;
uniform float _Sh_Kwshrv_ShdFlt;
uniform float _Sh_KwshrvSngl_TngntLo;
uniform float _Sh_KwshrvSngl_TngntHi;
uniform float _Sh_KwshrvSngl_ShdLo;
uniform float _Sh_KwshrvSngl_ShdHi;

inline float shade_kawaflt_single(float tangency, float shadow_atten) {
	half2 t;
	t.x = min(_Sh_KwshrvSngl_TngntLo, _Sh_KwshrvSngl_TngntHi);
	t.y = max(_Sh_KwshrvSngl_TngntLo, _Sh_KwshrvSngl_TngntHi);
	t = 2.0 * t - 1.0;
	
	half shade_t = saturate( (tangency - t.x) / (t.y - t.x) );
	shade_t = shade_t * shade_t * (3.0 - 2.0 * shade_t); // Cubic Hermite H01 interpolation
	
	half shade = shade_t * shadow_atten;
	shade = lerp(shade, 0.5, _Sh_Kwshrv_ShdFlt);
	
	half shade_low = min(_Sh_KwshrvSngl_ShdLo, _Sh_KwshrvSngl_ShdHi);
	half shade_high = max(_Sh_KwshrvSngl_ShdLo, _Sh_KwshrvSngl_ShdHi);
	shade = lerp(shade_low, shade_high, shade);
	
	apply_bitloss(shade, 57893, 34434, true);
	return shade; // shadow_atten; // max(0, shadow_atten); // shade;
}


#if defined(FRAGMENT_IN)
	inline half3 frag_shade_kawaflt_single_forward_main(FRAGMENT_IN i, half3 normal) {
		half light_atten = frag_shade_kawaflt_attenuation_no_shadow(i.pos_world.xyz);

		half shadow_atten = UNITY_SHADOW_ATTENUATION(i, i.pos_world.xyz);
		float3 dir = normalize(UnityWorldSpaceLightDir(i.pos_world.xyz));
		
		apply_bitloss3(dir, uint3(61909, 40056, 59452), uint3(49176, 44246, 39924), true);
		half tangency = dot(normal, dir);
		apply_bitloss(tangency, 54998, 51730, true);
		half shade_single = shade_kawaflt_single(tangency, shadow_atten);
		
		half3 shade = _LightColor0.rgb * max(0.0h, light_atten * shade_single);
		apply_bitloss3(shade, uint3(45272, 50533, 56004), uint3(36768, 54605, 64743), true);
		shade = max(half3(0,0,0), shade);
		return shade;
	}
	
	#ifdef KAWAFLT_PASS_FORWARDBASE
		inline half3 frag_shade_kawaflt_single_forward_base(FRAGMENT_IN i, half3 normal3) {
			half3 ambient = half3(0,0,0);
			#if defined(UNITY_SHOULD_SAMPLE_SH)
				half3 ambient_sh9 = ShadeSH9(half4(normal3, 1));
				half3 ambient_flat = ShadeSH9(half4(0,0,0,1));
				ambient = lerp(ambient_sh9, ambient_flat, _Sh_Kwshrv_ShdFlt);
				ambient = max(half3(0,0,0), ambient);
				apply_bitloss3(ambient, uint3(40935, 42665, 41008), uint3(60085, 32902, 50218), true);
			#endif

			half3 main = frag_shade_kawaflt_single_forward_main(i, normal3);

			apply_bitloss3(i.vertexlight, uint3(46754, 49739, 44693), uint3(49319, 53179, 55668), true);
			return (main + i.vertexlight + ambient) * _Sh_Kwshrv_ShdScl;
		}
	#endif
	
	#ifdef KAWAFLT_PASS_FORWARDADD
		inline half3 frag_shade_kawaflt_single_forward_add(FRAGMENT_IN i, half3 normal) {
			half3 main = frag_shade_kawaflt_single_forward_main(i, normal);
			return main * _Sh_Kwshrv_ShdScl;
		}
	#endif
#endif // defined(FRAGMENT_IN)

#endif // KAWAFLT_SHADING_KAWAFLT_SINGLE_INCLUDED