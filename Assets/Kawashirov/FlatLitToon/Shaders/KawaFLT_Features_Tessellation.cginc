#ifndef KAWAFLT_FEATURES_TESSELLATION_INCLUDED
#define KAWAFLT_FEATURES_TESSELLATION_INCLUDED

#include "KawaRND.cginc"
#include "Tessellation.cginc"

/* Infinity War features */

inline void iwd_hullconst(inout float edge[3], HULL_IN v0, HULL_IN v1, HULL_IN v2) {
	#if defined(IWD_ON)
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
	#endif
}

#endif // KAWAFLT_FEATURES_TESSELLATION_INCLUDED