#ifndef KAWAFLT_FEATURES_TESSELLATION_INCLUDED
#define KAWAFLT_FEATURES_TESSELLATION_INCLUDED

#include "KawaRND.cginc"
#include "Tessellation.cginc"

/* Disintegration features */
// (IN[i].vertex, rnd) -> (IN[i].vertex, OUT[i].dsntgrtVertexRotated, rnd, dropFace)
inline void dsntgrt_hullconst(inout float edge[3], HULL_IN v0, HULL_IN v1, HULL_IN v2) {
	#if defined(DSNTGRT_ON) && defined(DSNTGRT_FACE)
		// Делаем тест на плоскость _Dsntgrt_Plane
		// тесселлируем если хотя бы один вертекс в пределах спецэффекта  
		bool test0 = UnityDistanceFromPlane(v0.vertex.xyz, _Dsntgrt_Plane) > -0.01;
		bool test1 = UnityDistanceFromPlane(v1.vertex.xyz, _Dsntgrt_Plane) > -0.01;
		bool test2 = UnityDistanceFromPlane(v2.vertex.xyz, _Dsntgrt_Plane) > -0.01;
		if (test0 || test1 || test2) {
			edge[0] += _Dsntgrt_Tsltn;
			edge[1] += _Dsntgrt_Tsltn;
			edge[2] += _Dsntgrt_Tsltn;
		}
		// Нет теста на UnityWorldViewFrustumCull т.к. точная позиция определяется на геометри.
	#endif
}

#endif // KAWAFLT_FEATURES_TESSELLATION_INCLUDED