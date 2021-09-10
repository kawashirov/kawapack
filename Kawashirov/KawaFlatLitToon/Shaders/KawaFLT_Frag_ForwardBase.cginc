#ifndef KAWAFLT_FRAG_FORWARD_BASE_INCLUDED
#define KAWAFLT_FRAG_FORWARD_BASE_INCLUDED

#if !defined(KAWAFLT_PASS_FORWARDBASE)
	#error KAWAFLT_PASS_FORWARDBASE not defined, but KAWAFLT_STAGE_FRAGMENT_FORWARD_BASE included. 
#endif

// #include "UnityInstancing.cginc"
// #include "UnityStandardUtils.cginc"

#include ".\KawaFLT_Frag_Shared.cginc"

#include ".\kawa_feature_matcap.cginc"

/* ForwardBase only utils */

inline half3 frag_forward_get_emission_color(inout FRAGMENT_IN i, half3 baseColor, float2 texST, inout uint rnd) {
	half3 em = half3(0,0,0);
	#if defined(EMISSION_ALBEDO_NOMASK)
		em = baseColor;
	#elif defined(EMISSION_ALBEDO_MASK)
		em = baseColor * UNITY_SAMPLE_TEX2D(_EmissionMask, texST).r;
	#elif defined(EMISSION_CUSTOM)
		em = UNITY_SAMPLE_TEX2D(_EmissionMap, texST).rgb;
	#endif
	#if defined(EMISSION_ON)
		em = em * _EmissionColor.rgb * _EmissionColor.a;
		em = em * em; // TODO FIXME Gamma fix?
	#endif
	em = wnoise_mix(em, i, true, rnd);
	em = fps_mix(half4(em, 0)).rgb;
	em = pcw_mix(em, i, true); // Mix-in Poly Color Wave
	em = iwd_mix_emission(em, i);
	return em;
}


/* CubedParadox's Flat Lit Toon shading */
#if defined(SHADE_CUBEDPARADOXFLT)

	inline half3 frag_shade_cbdprdx_forward_base(FRAGMENT_IN i, half3 baseColor, float3 normalDirection, half3 emissive) {
		
		half3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
		UNITY_LIGHT_ATTENUATION(attenuation, i, i.pos_world.xyz);

		half grayscalelightcolor = dot(_LightColor0.rgb, grayscale_vector);
		half bottomIndirectLighting = grayscaleSH9(half3(0, -1, 0));
		half topIndirectLighting = grayscaleSH9(half3(0, 1, 0));
		half grayscaleDirectLighting = dot(lightDirection, normalDirection)*grayscalelightcolor*attenuation + grayscaleSH9(normalDirection);

		half lightDifference = topIndirectLighting + grayscalelightcolor - bottomIndirectLighting;
		half remappedLight = (grayscaleDirectLighting - bottomIndirectLighting) / lightDifference;

		half3 indirectLighting = saturate((ShadeSH9(half4(0, -1, 0, 1))));
		half3 directLighting = saturate((ShadeSH9(half4(0, 1, 0, 1)) + _LightColor0.rgb));
		half3 directContribution = saturate((1.0h - _Sh_Cbdprdx_Shadow) + floor(saturate(remappedLight) * 2.0h));

		half3 finalColor = (baseColor * lerp(indirectLighting, directLighting, directContribution)) + emissive;
		return finalColor;
	}

#endif


/* Kawashirov's Flat Lit Toon Log Diffuse-based */
#if defined(SHADE_KAWAFLT_LOG)

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


/* Kawashirov's Flat Lit Toon Ramp */
#if defined(SHADE_KAWAFLT_RAMP)

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

/* Kawashirov's Flat Lit Toon Single Diffuse-based */
#if defined(SHADE_KAWAFLT_SINGLE)

	inline half3 frag_shade_kawaflt_single_forward_base(FRAGMENT_IN i, half3 albedo, half3 normal3, half3 emission) {
		half3 ambient = half3(0,0,0);
		#if defined(UNITY_SHOULD_SAMPLE_SH)
			ambient = i.ambient + SHEvalLinearL2(half4(normal3, 1));
			ambient = lerp(ambient, half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w), /*_Sh_Kwshrv_Smth*/ 1.0);
			ambient = max(half3(0,0,0), ambient);
		#endif

		half3 main = frag_shade_kawaflt_single_forward_main(i, normal3);

		return albedo * (main + i.vertexlight + ambient) + emission;
	}

#endif


/* General fragment function */
half4 frag_forwardbase(FRAGMENT_IN i) : COLOR {
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

	frag_cull(i);
	fps_frag(i);

	float2 texST = frag_applyst(i.uv0);

	uint rnd4_sc = frag_rnd_init(i);
	
	dstfd_frag_clip(i, rnd4_sc);

	half3 normal3 = frag_forward_get_normal(i, texST);
	half4 albedo = frag_forward_get_albedo(i, texST, rnd4_sc);
	half3 emissive = frag_forward_get_emission_color(i, albedo, texST, rnd4_sc);
	
	frag_alphatest(i, rnd4_sc, albedo.a);

	albedo.rgb = matcap_apply(i, albedo.rgb);
	
	half4 finalColor;
	finalColor.a = albedo.a;
	#if defined(SHADE_CUBEDPARADOXFLT)
		finalColor.rgb = frag_shade_cbdprdx_forward_base(i, albedo.rgb, normal3, emissive);
	#endif
	#if defined(SHADE_KAWAFLT_LOG)
		finalColor.rgb = frag_shade_kawaflt_log_forward_base(i, albedo.rgb, normal3, emissive);
	#endif
	#if defined(SHADE_KAWAFLT_RAMP)
		finalColor.rgb = frag_shade_kawaflt_ramp_forward_base(i, albedo.rgb, normal3, emissive);
	#endif
	#if defined(SHADE_KAWAFLT_SINGLE)
		finalColor.rgb = frag_shade_kawaflt_single_forward_base(i, albedo.rgb, normal3, emissive);
	#endif
	
	UNITY_APPLY_FOG(i.fogCoord, finalColor);
		
	return finalColor;
}

#endif // KAWAFLT_FRAG_FORWARD_BASE_INCLUDED
