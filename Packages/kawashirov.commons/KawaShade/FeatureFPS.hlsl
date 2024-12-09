#ifndef KAWAFLT_FEATURE_FPS_INCLUDED
#define KAWAFLT_FEATURE_FPS_INCLUDED

/*
	FPS features
*/

#if defined(FPS_ON)
	uniform float4 _FPS_TLo;
	uniform float4 _FPS_THi;
#endif

#if defined(VERTEX_IN) && defined(VERTEX_OUT)
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
				#if defined(PIPELINE_VF)
					// TODO
					v_in.vertex = float4(0,0,0,0);
					// Без геометри стейджа у нас нет возможность сбрасывать примитивы,
					// по этому сжимаем в 0 что бы минимизировать растризацию
				#endif
			}
			#if defined(NEED_VERT_CULL)
				v_out.cull = fps_digit != v_digit;
			#endif
		#endif
	}
#endif // defined(VERTEX_IN) && defined(VERTEX_OUT)

inline void fps_apply_uv(inout half2 uv0) {
	#if defined(FPS_TEX)
		uint fps = clamp((uint) round(unity_DeltaTime.w), 0, 99);
		uint digit = (uv0.x > 0.5 ? fps : (fps / 10)) % 10;
		uv0.x = frac(uv0.x * 2) / 10 + half(digit) / 10;
	#endif
}

inline void fps_apply_colors(inout half3 albedo, inout half3 emissive) {
	#if defined(FPS_ON)
		// TODO
		albedo *= lerp(_FPS_TLo, _FPS_THi, unity_DeltaTime.w / 91.0h);
		emissive *= lerp(_FPS_TLo, _FPS_THi, unity_DeltaTime.w / 91.0h);
	#endif
}

#endif // KAWAFLT_FEATURE_FPS_INCLUDED