#ifndef KAWAFLT_FEATURE_MATCAP_INCLUDED
#define KAWAFLT_FEATURE_MATCAP_INCLUDED

/*
	MatCap features
*/

#if defined(MATCAP_ON)
	UNITY_DECLARE_TEX2D(_MatCap);
	uniform float _MatCap_Scale;
#endif

#if defined(FRAGMENT_IN)
	// (world_normal) -> (matcap_uv)
	inline void matcap_calc_uv(inout FRAGMENT_IN i) {
		#if defined(MATCAP_ON) && defined(KAWAFLT_PASS_FORWARD)
			// half3 normal_view = mul((float3x3)UNITY_MATRIX_V, i.normal_world);
			// i.matcap_uv = normal_view.xy * 0.5h + 0.5h;

			float3 world_view_up;
			half3 world_view_right;

			#if defined(MATCAP_KEEPUP)
				// Также как и в маткапе VRChat, сохраняет верхнее направление в мире.
				half3 view_dir = normalize(KawaWorldSpaceViewDir(i.pos_world.xyz));
				half3 world_up = float3(0,1,0);
				world_view_up = normalize(world_up - view_dir * dot(view_dir, world_up));
				world_view_right = normalize(cross(view_dir, world_view_up));
			#else
				// Классический маткап, используем векторы камеры
				world_view_up = normalize(mul((float3x3)unity_CameraToWorld, float3(0,1,0))).xyz;
				world_view_right = normalize(mul((float3x3)unity_CameraToWorld, float3(1,0,0))).xyz;
				// TODO потестить UNITY_MATRIX_V[1].xyz (вверх) и UNITY_MATRIX_V[0].xyz (вправо)  
			#endif

			half2 matcap_uv = half2(dot(world_view_right, i.normal_world), dot(world_view_up, i.normal_world));
			matcap_uv = matcap_uv * 0.5h + 0.5h;
			i.matcap_uv = matcap_uv;
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
#endif // defined(FRAGMENT_IN)

#endif // KAWAFLT_FEATURE_MATCAP_INCLUDED