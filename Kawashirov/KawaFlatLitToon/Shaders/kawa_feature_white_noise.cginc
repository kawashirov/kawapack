#ifndef KAWAFLT_FEATURE_WNOISE_INCLUDED
#define KAWAFLT_FEATURE_WNOISE_INCLUDED

#include ".\kawa_rnd.cginc"

/*
	White Noise features
*/

#define WNOISE_RND_SEED 43178
#if defined(WNOISE_ON)
	uniform float _WNoise_Albedo;
	#if defined(EMISSION_ON)
		uniform float _WNoise_Em;
	#endif
#endif

#if defined(FRAGMENT_IN)
	inline half3 wnoise_mix(half3 color, FRAGMENT_IN i, bool is_emission, inout uint rnd) {
		#if defined(WNOISE_ON)
			float wnoise = rnd_next_float_01(rnd);
			float factor_em = 0;
			#if defined(EMISSION_ON)
				factor_em = _WNoise_Em;
			#endif
			float factor = is_emission ? factor_em : _WNoise_Albedo;
			color.rgb = lerp(color.rgb, wnoise.rrr, factor);
		#endif
		return color;
	}
#endif // defined(FRAGMENT_IN)

#endif // KAWAFLT_FEATURE_WNOISE_INCLUDED