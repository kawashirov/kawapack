#ifndef KAWAFLT_FEATURE_WNOISE_INCLUDED
#define KAWAFLT_FEATURE_WNOISE_INCLUDED

/*
	White Noise features
*/

#if defined(WNOISE_ON)
	uniform float _WNoise_Albedo;
	#if defined(EMISSION_ON)
		uniform float _WNoise_Em;
	#endif
#endif

inline void wnoise_apply(FRAGMENT_IN i, half3 albedo, half3 emissive) {
	#if defined(WNOISE_ON)
		float wnoise = rnd_float_01(62542, 57838, true);
		float factor_em = 0;
		#if defined(EMISSION_ON)
			factor_em = _WNoise_Em;
		#endif
		albedo = lerp(albedo, wnoise.rrr, _WNoise_Albedo);
		emissive = lerp(emissive, wnoise.rrr, factor_em);
	#endif
}

#endif // KAWAFLT_FEATURE_WNOISE_INCLUDED