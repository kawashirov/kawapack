#ifndef KAWAFLT_SHADING_CUBEDPARADOX_INCLUDED
#define KAWAFLT_SHADING_CUBEDPARADOX_INCLUDED

#include "UnityLightingCommon.cginc"
#include "UnityStandardUtils.cginc"

/*
	Cubed's Flat Lit Toon
	(Copied from old 2018 shader)
*/

static const half3 grayscale_vector = half3(0, 0.3823529, 0.01845836);

inline half grayscaleSH9(half3 normalDirection) {
	return dot(ShadeSH9(half4(normalDirection, 1)), grayscale_vector);
}

#if defined(SHADE_CUBEDPARADOXFLT)
		uniform float _Sh_Cbdprdx_Shadow;
#endif

#if defined(FRAGMENT_IN) && defined(SHADE_CUBEDPARADOXFLT)

	#ifdef KAWAFLT_PASS_FORWARDBASE
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
	
	#ifdef KAWAFLT_PASS_FORWARDADD
		inline half3 frag_shade_cbdprdx_forward_add(FRAGMENT_IN i, half3 baseColor, half3 normal) {
			// float4 objPos = mul(unity_ObjectToWorld, float4(0,0,0,1));
			//float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
			//float3 lightColor = _LightColor0.rgb;
			UNITY_LIGHT_ATTENUATION(attenuation, i, i.pos_world.xyz);
			half lightContribution = dot(normalize(_WorldSpaceLightPos0.xyz - i.pos_world.xyz),normal)*attenuation;
			half3 directContribution = floor(saturate(lightContribution) * 2.0h);
			half lerp_v = saturate(directContribution + ((1.0h - _Sh_Cbdprdx_Shadow) * attenuation));
			return baseColor * lerp(0.0h, _LightColor0.rgb, lerp_v);
		}
	#endif

#endif // defined(FRAGMENT_IN) && defined(SHADE_CUBEDPARADOXFLT)

#endif // KAWAFLT_SHADING_CUBEDPARADOX_INCLUDED