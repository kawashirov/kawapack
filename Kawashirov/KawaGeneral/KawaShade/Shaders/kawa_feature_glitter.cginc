#ifndef KAWAFLT_FEATURE_GLITTER_INCLUDED
#define KAWAFLT_FEATURE_GLITTER_INCLUDED

#include ".\kawa_rnd.cginc"

/*
	Glitter features
*/

#define GLITTER_RND_M 55229
#define GLITTER_RND_C 49690
#if defined(GLITTER_ON)
	#if defined(GLITTER_MASK_ON)
		UNITY_DECLARE_TEX2D(_Gltr_Mask);
	#endif
	uniform float _Gltr_Dnst;
	#if defined(GLITTER_MODE_SOLID_COLOR)
		uniform float4 _Gltr_Color;
	#endif
	uniform float _Gltr_Brght;
	uniform float _Gltr_Em;
#endif

#if defined(FRAGMENT_IN)
	inline void apply_glitter(inout half3 albedo, inout half3 emissive, float2 texST, uint rnd) {
		#if defined(GLITTER_ON)
			rnd = rnd * GLITTER_RND_M + GLITTER_RND_C;
			float glitter_rnd = rnd_next_float_01(rnd);
			float density = _Gltr_Dnst;
			#if defined(GLITTER_MASK_ON)
				density *= UNITY_SAMPLE_TEX2D(_Gltr_Mask, texST).r;
			#endif
			if (glitter_rnd < density) {
				half3 glitter_color = half3(0,0,0);
				#if defined(GLITTER_MODE_SOLID_COLOR)
					glitter_color = _Gltr_Color.rgb;
				#elif defined(GLITTER_MODE_SOLID_ALBEDO)
					glitter_color = albedo;
				#endif
				glitter_color *= _Gltr_Brght;
				albedo = glitter_color;
				emissive += glitter_color * _Gltr_Em;
			}
			//albedo = half3(0,0,0); 
			//emissive = glitter_rnd < _Gltr_Dnst ? half3(1,1,1) : half3(0,0,0);
		#endif
	}
#endif // defined(FRAGMENT_IN)

#endif // KAWAFLT_FEATURE_GLITTER_INCLUDED