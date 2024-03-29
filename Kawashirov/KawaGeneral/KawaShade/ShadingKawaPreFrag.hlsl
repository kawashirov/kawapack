#ifndef KAWAFLT_SHADING_KAWAFLT_PREFRAG_INCLUDED
#define KAWAFLT_SHADING_KAWAFLT_PREFRAG_INCLUDED

// KawaShade Common Code
// Ее нельзя поместить в Commons т.к. она ссылается на методы из разных типов теней.

// (v.normalDir) -> (v.vertexlight)
inline void kawaflt_fragment_in(inout FRAGMENT_IN v, bool vertexlight_on, float3 wsvd) {
	#if defined(KAWAFLT_PASS_FORWARDBASE) && defined(SHADE_KAWAFLT)
		v.vertexlight = half3(0,0,0);
		if (vertexlight_on) {
			// CAN NOT USE VERTEXLIGHT_ON he, it's only defined for vert shader.

			// v.vertexlight = Shade4PointLights(
			// 	unity_4LightPosX0, unity_4LightPosY0, unity_4LightPosZ0,
			// 	unity_LightColor[0].rgb, unity_LightColor[1].rgb, unity_LightColor[2].rgb, unity_LightColor[3].rgb,
			// 	unity_4LightAtten0, v.pos_world.xyz, normal3
			// );
			// Modified Shade4PointLights

			half3 normal3 = v.normal_world;

			float4 toLightX = unity_4LightPosX0 - v.pos_world.x;
			float4 toLightY = unity_4LightPosY0 - v.pos_world.y;
			float4 toLightZ = unity_4LightPosZ0 - v.pos_world.z;

			float4 lengthSq = toLightX * toLightX + toLightY * toLightY + toLightZ * toLightZ;
			lengthSq = max(lengthSq, 0.000001); // non-zero

			#if defined(SHADE_KAWAFLT_LOG)
				float4 tangency = float4(_Sh_Kwshrv_Smth_Tngnt, _Sh_Kwshrv_Smth_Tngnt, _Sh_Kwshrv_Smth_Tngnt, _Sh_Kwshrv_Smth_Tngnt);
				UNITY_BRANCH if (_Sh_Kwshrv_Smth < 0.999) {
					// Only calc tangency when not fully flat
					float4 prec_tangency = toLightX * normal3.x + toLightY * normal3.y + toLightZ * normal3.z;
					prec_tangency = max(float4(0,0,0,0), prec_tangency * rsqrt(lengthSq));

					// TODO frag_shade_kawaflt_log_smooth_tangency
					// v.vertexlight smoothing
					float4 smooth = float4(_Sh_Kwshrv_Smth, _Sh_Kwshrv_Smth, _Sh_Kwshrv_Smth, _Sh_Kwshrv_Smth);
					tangency = saturate(lerp(prec_tangency, tangency, smooth));
				}
				float4 shade = tangency / (1.0 + lengthSq * unity_4LightAtten0);
				UNITY_UNROLL for(int i = 0; i < 4; ++i) {
					v.vertexlight += unity_LightColor[i].rgb * shade[i];
				}
			#elif defined(SHADE_KAWAFLT_RAMP)
				// Can not be fully computed here because of ramp sampling
				float4 tangency = toLightX * normal3.x + toLightY * normal3.y + toLightZ * normal3.z;
				tangency = max(float4(0,0,0,0), tangency * rsqrt(lengthSq));
				tangency = pow(tangency * 0.5 + 0.5, _Sh_KwshrvRmp_Pwr);
				half3 dnm = 1.0h + lengthSq * unity_4LightAtten0;
				UNITY_UNROLL for(int i = 0; i < 4; ++i) {
					half t = tangency[i];
					float lod = sqrt(length(wsvd) * 0.1h);
					half3 ramp = KAWA_SAMPLE_TEX2D_LOD(_Sh_KwshrvRmp_Tex, half2(t,t), lod).rgb;
					v.vertexlight += unity_LightColor[i].rgb * max(0.0h, ramp / dnm);
				}
			#elif defined(SHADE_KAWAFLT_SINGLE)
				float4 tangency = toLightX * normal3.x + toLightY * normal3.y + toLightZ * normal3.z;
				tangency = tangency * rsqrt(lengthSq);
				half4 shade = shade_kawaflt_single(tangency, 1.0) / (1.0 + lengthSq * unity_4LightAtten0);
				shade = max(half4(0,0,0,0), shade);
				UNITY_UNROLL for(int i = 0; i < 4; ++i) {
					v.vertexlight += unity_LightColor[i].rgb * shade[i];
				}
			#endif
			apply_bitloss(v.vertexlight);
		}
		// ShadeSH9 во фрагментном
	#endif
}

#endif // KAWAFLT_SHADING_KAWAFLT_PREFRAG_INCLUDED