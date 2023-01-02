#ifndef KAWAFLT_FEATURE_WNOISE_INCLUDED
#define KAWAFLT_FEATURE_WNOISE_INCLUDED

/*
	White Noise features
*/

#define WNOISE_RND_M 53534
#define WNOISE_RND_C 43178

#if defined(WNOISE_ON)
	uniform float _WNoise_Albedo;
	#if defined(EMISSION_ON)
		uniform float _WNoise_Em;
	#endif
#endif

inline void wnoise_apply(FRAGMENT_IN i, uint rnd, half3 albedo, half3 emissive) {
	#if defined(WNOISE_ON)
		rnd = rnd * WNOISE_RND_M + WNOISE_RND_C;
		rnd = rnd_apply_time(rnd);
		float wnoise = rnd_next_float_01(rnd);
		float factor_em = 0;
		#if defined(EMISSION_ON)
			factor_em = _WNoise_Em;
		#endif
		albedo = lerp(albedo, wnoise.rrr, _WNoise_Albedo);
		emissive = lerp(emissive, wnoise.rrr, factor_em);
	#endif
}

#endif // KAWAFLT_FEATURE_WNOISE_INCLUDED