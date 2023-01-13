#if defined(SHADE_KAWAFLT_RAMP) && !defined(KAWAFLT_SHADING_KAWAFLT_RAMP_INCLUDED)
#define KAWAFLT_SHADING_KAWAFLT_RAMP_INCLUDED

// KawaShade Ramp

uniform float _Sh_Kwshrv_ShdScl;
uniform float _Sh_Kwshrv_ShdFlt;

UNITY_DECLARE_TEX2D(_Sh_KwshrvRmp_Tex);
uniform float _Sh_KwshrvRmp_Pwr;
uniform float4 _Sh_KwshrvRmp_NdrctClr;

inline half3 frag_shade_kawaflt_ramp_apply(half uv) {
	uv = pow(uv, _Sh_KwshrvRmp_Pwr);
	uv = uv * uv * (3.0 - 2.0 * uv); // Cubic Hermite H01 interoplation
	apply_bitloss(uv);
	return UNITY_SAMPLE_TEX2D(_Sh_KwshrvRmp_Tex, half2(uv, uv)).rgb;
}

#if defined(FRAGMENT_IN)
	inline half3 frag_shade_kawaflt_ramp_forward_main(FRAGMENT_IN i, half3 normal) {
		half light_atten = frag_shade_kawaflt_attenuation_no_shadow(i.pos_world.xyz);

		half shadow_atten = UNITY_SHADOW_ATTENUATION(i, i.pos_world.xyz);
		apply_bitloss(shadow_atten);
		float3 wsld = normalize(UnityWorldSpaceLightDir(i.pos_world.xyz));
		apply_bitloss(wsld);
		half ramp_uv = dot(normal, wsld) * 0.5 + 0.5;
		
		half3 shade_ramp = frag_shade_kawaflt_ramp_apply(ramp_uv * shadow_atten);
		half3 shade = _LightColor0.rgb * max(0.0h, light_atten * shade_ramp);
		apply_bitloss(shade);
		shade = max(half3(0,0,0), shade);
		return shade;
	}
	
	#ifdef KAWAFLT_PASS_FORWARDBASE
		inline half3 frag_shade_kawaflt_ramp_forward_base(FRAGMENT_IN i, half3 normal3) {
			half3 ambient = half3(0,0,0);
			#if defined(UNITY_SHOULD_SAMPLE_SH)
				half3 ambient_sh9 = ShadeSH9(half4(normal3, 1));
				half3 ambient_flat = ShadeSH9(half4(0,0,0,1));
				ambient = lerp(ambient_sh9, ambient_flat, _Sh_Kwshrv_ShdFlt);
				ambient = max(half3(0,0,0), ambient);
				ambient = ambient * _Sh_KwshrvRmp_NdrctClr.rgb * _Sh_KwshrvRmp_NdrctClr.a;
			#endif

			half3 main = frag_shade_kawaflt_ramp_forward_main(i, normal3);

			return (main + i.vertexlight + ambient) * _Sh_Kwshrv_ShdScl;
		}
	#endif
	
	#ifdef KAWAFLT_PASS_FORWARDADD
		inline half3 frag_shade_kawaflt_ramp_forward_add(FRAGMENT_IN i, half3 normal) {
			half3 main = frag_shade_kawaflt_ramp_forward_main(i, normal);
			return main * _Sh_Kwshrv_ShdScl;
		}
	#endif

#endif // defined(FRAGMENT_IN)

#endif // KAWAFLT_SHADING_KAWAFLT_RAMP_INCLUDED