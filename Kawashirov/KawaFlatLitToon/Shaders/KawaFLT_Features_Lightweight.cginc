#ifndef KAWAFLT_FEATURES_LIGHTWEIGHT_INCLUDED
#define KAWAFLT_FEATURES_LIGHTWEIGHT_INCLUDED

/* Matcap features */

// (world_normal) -> (matcap_uv)
inline void matcap_calc_uv(inout FRAGMENT_IN i) {
	#if defined(MATCAP_ON) && defined(KAWAFLT_PASS_FORWARD)
		// Типа UnityObjectToViewDir, конвертит направление
		half3 normal_view = mul((float3x3)UNITY_MATRIX_V, i.normal_world);
		i.matcap_uv = normal_view * 0.5h + 0.5h;
		// half3 world_view_up = normalize(half3(0, 1, 0) - poiCam.viewDir * dot(poiCam.viewDir, half3(0, 1, 0)));
		// half3 world_view_right = normalize(cross(poiCam.viewDir, world_view_up));
		// half2 matcapUV = half2(dot(world_view_right, world_normal), dot(world_view_up, world_normal)) * 0.5h + 0.5h;
	#endif
}

inline half3 matcap_apply(FRAGMENT_IN i, half3 color) {
	#if defined(MATCAP_ON) && defined(KAWAFLT_PASS_FORWARD)
		float4 matcap = UNITY_SAMPLE_TEX2D(_MatCap, i.matcap_uv);
		#if defined(MATCAP_REPLACE)
			color = lerp(color, matcap.rgb, _MatCap_Scale * matcap.a);
		#endif
		#if defined(MATCAP_MULTIPLE)
			color *= lerp(1, matcap.rgb, _MatCap_Scale * matcap.a);
		#endif
		#if defined(MATCAP_ADD)
			color += matcap.rgb * _MatCap_Scale * matcap.a;
		#endif
	#endif
	return color;
}

/* KawaFLT */

// (v.normalDir) -> (v.vertexlight, v.ambient)
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
				UNITY_BRANCH if (_Sh_Kwshrv_Smth < 0.99) {
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
		}
		#if defined(UNITY_SHOULD_SAMPLE_SH) && (defined(SHADE_KAWAFLT_LOG) || defined(SHADE_KAWAFLT_SINGLE))
			v.ambient = SHEvalLinearL0L1(half4(v.normal_world, 1));
		#endif
	#endif
}

/* General */

// (o.pos) -> (o.pos_screen)
inline void screencoords_fragment_in(inout FRAGMENT_IN o) {
	#if defined(RANDOM_MIX_COORD) || defined(RANDOM_SEED_TEX)
		o.pos_screen = ComputeScreenPos(o.pos);
	#endif
}


#endif // KAWAFLT_FEATURES_LIGHTWEIGHT_INCLUDED