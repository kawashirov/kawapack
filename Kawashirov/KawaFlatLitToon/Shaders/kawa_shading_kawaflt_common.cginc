#ifndef KAWAFLT_SHADING_KAWAFLT_COMMON_INCLUDED
#define KAWAFLT_SHADING_KAWAFLT_COMMON_INCLUDED

#include "UnityLightingCommon.cginc"
#include "UnityStandardUtils.cginc"

/*
	Kawashirov's Flat Lit Toon Common Code
*/

// Тоже, что и UNITY_LIGHT_ATTENUATION из AutoLight.cginc, но без учёта теней.
inline half frag_shade_kawaflt_attenuation_no_shadow(half3 worldPos) {
	#if defined(POINT)
		unityShadowCoord3 lightCoord = mul(unity_WorldToLight, unityShadowCoord4(worldPos, 1)).xyz;
		return tex2D(_LightTexture0, dot(lightCoord, lightCoord).rr).UNITY_ATTEN_CHANNEL;
	#elif defined(SPOT)
		unityShadowCoord4 lightCoord = mul(unity_WorldToLight, unityShadowCoord4(worldPos, 1));
		return (lightCoord.z > 0) * UnitySpotCookie(lightCoord) * UnitySpotAttenuate(lightCoord.xyz);
	#elif defined(DIRECTIONAL)
		return 1.0;
	#elif defined(POINT_COOKIE)
		unityShadowCoord3 lightCoord = mul(unity_WorldToLight, unityShadowCoord4(worldPos, 1)).xyz;
		return tex2D(_LightTextureB0, dot(lightCoord, lightCoord).rr).UNITY_ATTEN_CHANNEL * texCUBE(_LightTexture0, lightCoord).w;
	#elif defined(DIRECTIONAL_COOKIE)
		unityShadowCoord2 lightCoord = mul(unity_WorldToLight, unityShadowCoord4(worldPos, 1)).xy;
		return tex2D(_LightTexture0, lightCoord).w;
	#else
		#error
	#endif
}

#if defined(FRAGMENT_IN)
	inline half frag_forward_get_light_attenuation(FRAGMENT_IN i) {
		UNITY_LIGHT_ATTENUATION(attenuation, i, i.pos_world.xyz);
		return attenuation;
	}
#endif // defined(FRAGMENT_IN)

#endif // KAWAFLT_SHADING_KAWAFLT_COMMON_INCLUDED