#ifndef KAWA_FEATURE_IWD_INCLUDED
#define KAWA_FEATURE_IWD_INCLUDED

// Infinity War features (Geometry+)

#define IWD_RND_SEED 26842
#if defined(IWD_ON)
	uniform float4 _IWD_Plane;
	uniform float _IWD_PlaneDistRandomness;

	uniform float _IWD_DirRandomWeight;
	uniform float _IWD_DirPlaneWeight;
	uniform float _IWD_DirNormalWeight;
	uniform float _IWD_DirObjectWeight;
	uniform float4 _IWD_DirObjectVector;
	uniform float _IWD_DirWorldWeight;
	uniform float4 _IWD_DirWorldVector;

	uniform float _IWD_MoveSpeed;
	uniform float _IWD_MoveAccel;

	uniform float4 _IWD_TintColor;
	uniform float _IWD_TintFar;

	uniform float _IWD_CmprssFar;

	#if defined(KAWAFLT_F_TESSELLATION)
		uniform float _IWD_Tsltn;
	#endif

	inline bool iwd_use_plane() { return length(_IWD_Plane.xyz) > 0.001; }

#endif // IWD_ON

#if defined(GEOMETRY_IN) && defined(GEOMETRY_OUT)
	// (IN[i].vertex, rnd) -> (IN[i].vertex, OUT[i].iwd_tint, rnd, drop_face)
	inline void iwd_geometry(inout GEOMETRY_IN IN[3], inout GEOMETRY_OUT OUT[3], inout uint rnd, inout bool drop_face) {
		// Обсчет в обджект-спейсе
		#if defined(IWD_ON)
			float3 pos_mid = (IN[0].vertex.xyz + IN[1].vertex.xyz + IN[2].vertex.xyz) / 3.0;
			
			bool use_plane = iwd_use_plane();

			float plane_distance_mid = 0;
			float plane_distance_random = 0;
			if (use_plane) {
				_IWD_Plane.xyz = normalize(_IWD_Plane.xyz);
				plane_distance_random = -rnd_next_float_01(rnd) * _IWD_PlaneDistRandomness;
				plane_distance_mid = max(0, dot(float4(pos_mid, 1.0f), _IWD_Plane) + plane_distance_random);
			} else {
				plane_distance_mid = _IWD_Plane.w;
			}

			float3 offset = 0;
			if (plane_distance_mid > 0.001) {
				float wn = 0; // weights normalizer
				wn += _IWD_DirRandomWeight;
				wn += _IWD_DirPlaneWeight;
				wn += _IWD_DirNormalWeight;
				wn += _IWD_DirObjectWeight;
				wn += _IWD_DirWorldWeight;
				wn = 1.0 / wn;

				float3 random_normal = rnd_next_direction3(rnd);
				float3 plane_normal = use_plane ? _IWD_Plane.xyz : 0;
				float3 face_normal = normalize(IN[0].normal_obj + IN[1].normal_obj + IN[2].normal_obj);
				float3 object_normal = normalize(_IWD_DirObjectVector.xyz);
				float3 world_normal = normalize(UnityWorldToObjectDir(_IWD_DirWorldVector.xyz)); 

				float3 offset_dir = 0;
				// Здесь я тоже верю в компилятор
				offset_dir += random_normal * (_IWD_DirRandomWeight * wn);
				offset_dir += plane_normal * (_IWD_DirPlaneWeight * wn);
				offset_dir += face_normal * (_IWD_DirNormalWeight * wn);
				offset_dir += object_normal * (_IWD_DirObjectWeight * wn);
				offset_dir += world_normal * (_IWD_DirWorldWeight * wn);

				// polynomial y = x^2 * a + x * b + c = x * (x * a + b) + c;  
				float offset_ammount = plane_distance_mid * (plane_distance_mid * _IWD_MoveAccel + _IWD_MoveSpeed);
				offset = offset_dir * offset_ammount;
			}
			
			// Фактор сжатия считается от сердней точки, т.к. если считать для вершин,
			// то из-за ускорения может начаться растяжение треугольника, а не сжатие
			float factor_compress = saturate(_IWD_CmprssFar > 0.001f ? plane_distance_mid / _IWD_CmprssFar : 1.001f);

			if (factor_compress > 0.999f) {
				// Если фактор стал больше единицы, значит вертексы сожмутся в точку и фейс не будет нужен
				//drop_face = true;
			}

			UNITY_UNROLL for (int j2 = 2; j2 >= 0; j2--) {
				// Фактор потемнения считаетс яот вершин, что бы обеспечить плавность.
				float plane_distance_v = 0;
				if (use_plane) {
					plane_distance_v = dot(float4(IN[j2].vertex.xyz, 1.0), _IWD_Plane) + plane_distance_random;
				} else {
					plane_distance_v = _IWD_Plane.w;
				}
				float factor_tint = _IWD_TintFar > 0.001f ? saturate(plane_distance_v / _IWD_TintFar) : 1.001f;
				factor_tint = factor_tint * factor_tint * (3.0f - 2.0f * factor_tint); // H01
				OUT[j2].iwd_tint = factor_tint;

				IN[j2].vertex.xyz = lerp(IN[j2].vertex.xyz, pos_mid.xyz, factor_compress) + offset;
			}
		#endif
		// else do nothing
	}
#endif // defined(GEOMETRY_IN) && defined(GEOMETRY_OUT)

#ifdef HULL_IN
	inline void iwd_hullconst(inout float edge[3], HULL_IN v0, HULL_IN v1, HULL_IN v2) {
		#if defined(IWD_ON)
			bool use_plane = iwd_use_plane();
			if (use_plane) {
				// Делаем тест на плоскость _IWD_Plane
				// тесселлируем если хотя бы один вертекс в пределах спецэффекта 
				_IWD_Plane.xyz = normalize(_IWD_Plane.xyz); 
				bool test0 = UnityDistanceFromPlane(v0.vertex.xyz, _IWD_Plane) > -0.001;
				bool test1 = UnityDistanceFromPlane(v1.vertex.xyz, _IWD_Plane) > -0.001;
				bool test2 = UnityDistanceFromPlane(v2.vertex.xyz, _IWD_Plane) > -0.001;
				if (test0 || test1 || test2) {
					edge[0] += _IWD_Tsltn;
					edge[1] += _IWD_Tsltn;
					edge[2] += _IWD_Tsltn;
				}
				// Нет теста на UnityWorldViewFrustumCull т.к. точная позиция определяется на геометри.
			} else {
				// Если плоскость не используется, то тесселлируем от W
				if (_IWD_Plane.w > 0.001) {
					edge[0] += _IWD_Tsltn;
					edge[1] += _IWD_Tsltn;
					edge[2] += _IWD_Tsltn;
				}
			}
		#endif
	}
#endif // HULL_IN

inline void iwd_apply(FRAGMENT_IN i, inout half3 albedo, inout half3 emissive) {
	#if defined(IWD_ON)
		albedo = lerp(albedo, _IWD_TintColor.rgb, _IWD_TintColor.a * i.iwd_tint);
		// Затенение эмишона.
		emissive *= saturate(1.0 - _IWD_TintColor.a * i.iwd_tint);
	#endif
}

#endif // KAWA_FEATURE_IWD_INCLUDED