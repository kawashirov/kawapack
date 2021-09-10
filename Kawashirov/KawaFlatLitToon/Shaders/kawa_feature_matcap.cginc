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
#endif // defined(FRAGMENT_IN)

#endif // KAWAFLT_FEATURE_MATCAP_INCLUDED