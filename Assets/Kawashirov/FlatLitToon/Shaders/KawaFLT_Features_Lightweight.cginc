#ifndef KAWAFLT_FEATURES_LIGHTWEIGHT_INCLUDED
#define KAWAFLT_FEATURES_LIGHTWEIGHT_INCLUDED


/* Distance Fade features */

// (o.pos_world) -> (o.dstfd_distance)
inline void dstfade_frament_in(inout FRAGMENT_IN o) {
	#if defined(DSTFD_ON)
		o.dstfd_distance = length(KawaWorldSpaceViewDir(o.pos_world.xyz) * _DstFd_Axis.xyz);
	#endif
}

/* FPS features */

// (v_out.uv0) -> (v_in.vertex, v_out.fps_cull)
inline void fps_vertex(inout VERTEX_IN v_in, inout VERTEX_OUT v_out) {
	#if defined(FPS_MESH)
		uint fps = clamp((uint) round(unity_DeltaTime.w), 0, 99);
		uint fps_digit_0 = fps % 10;
		uint fps_digit_1 = (fps / 10) % 10;

		uint v_digit = (uint) round(v_out.uv0.x * 10.0h);
		uint v_pos = (uint) round(v_out.uv0.y * 2.0h);

		uint fps_digit = v_pos == 0 ? fps_digit_0 : fps_digit_1;
		if (fps_digit != v_digit) {
			v_out.cull = true;
			#if defined(PIPELINE_VF)
				// TODO
				v_in.vertex = float4(0,0,0,0);
				// Без геометри стейджа у нас нет возможность сбрасывать примитивы,
				// по этому сжимаем в 0 что бы минимизировать растризацию
			#endif
		} else {
			v_out.cull = false;
		}
	#endif
}


/* KawaFLT */

// (v.normalDir) -> (v.vertexlight, v.vertexlight_uv, v.vertexlight_a, v.ambient)
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