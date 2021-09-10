#ifndef KAWAFLT_STRUCT_SHARED_INCLUDED
#define KAWAFLT_STRUCT_SHARED_INCLUDED

// LPPV принудительно отключен, т.к. не используется в VRC и вообще пиздец лагает.
#undef UNITY_LIGHT_PROBE_PROXY_VOLUME
#define UNITY_LIGHT_PROBE_PROXY_VOLUME 0

// Лайтмапы не поддерживаются. 
#pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED LIGHTMAP_SHADOW_MIXING

// Вместо вариантов, дефайны определены явно в каждом типе шедера
#pragma skip_variants _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON

// Отражений всёравно нет.
#pragma skip_variants _GLOSSYREFLECTIONS_OFF _SPECULAR_HIGHLIGHTS_OFF
#define _GLOSSYREFLECTIONS_OFF
#define _SPECULAR_HIGHLIGHTS_OFF

// в юнити этого нет почему-то
#define KAWA_SAMPLE_TEX2D_LOD(tex,coord,lod) tex.SampleLevel (sampler##tex,coord, lod)
// TODO переписать все семплеры на синтаксисе DX11.

//     Проблема:
// VERTEXLIGHT_ON никогда не дефайнится в не-вертексных шейдерах,
// из-за этого генерация кода отличается для разных этапов шейдера,
// что приводит к несоответствию семантик на выходных-входных регистрах.
//     Ошибка:
// description ID3D11DeviceContext::DrawIndexed:
// Vertex Shader - Pixel Shader linkage error:
// Signatures between stages are incompatible. Semantic '...' is defined for mismatched hardware registers between the output stage and input stage.
//     Последствия:
// Семантики идут по пизде, шейдер работает максимально сломано.
//     Решение:
// На вход фрагмент шейдера half3 vertexlight определен ВСЕГДА.
// На вход прочих этапов шейдера определен bool vertexlight_on,
// который переносит инфу по цепочке с ветексного на фрагментный этап.
//

#include "UnityCG.cginc"

#if defined(KAWAFLT_PASS_FORWARDBASE) || defined(KAWAFLT_PASS_FORWARDADD)
	#define KAWAFLT_PASS_FORWARD 1
#endif

#if defined(_ALPHATEST_ON) || defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)
	#define ALPHA_FEATURES 1
#endif

#if defined(MAINTEX_NOMASK) || defined(MAINTEX_COLORMASK)
	UNITY_DECLARE_TEX2D(_MainTex);
	#define AVAILABLE_MAINTEX 1
	#if defined(MAINTEX_COLORMASK)
		UNITY_DECLARE_TEX2D(_ColorMask);
		#define AVAILABLE_COLORMASK 1
	#endif
#endif

uniform float4 _Color;

#if defined(CUTOFF_CLASSIC) || defined(CUTOFF_FADE)
	uniform float _Cutoff;
#endif
#if defined(CUTOFF_RANDOM)
	uniform float _CutoffMin;
	uniform float _CutoffMax;
#endif

#if defined(EMISSION_ON)
	uniform float4 _EmissionColor;
	#if defined(EMISSION_ALBEDO_MASK)
		UNITY_DECLARE_TEX2D(_EmissionMask);
	#endif
	#if defined(EMISSION_CUSTOM)
		UNITY_DECLARE_TEX2D(_EmissionMap);
	#endif
#endif

#if defined(_NORMALMAP)
	UNITY_DECLARE_TEX2D(_BumpMap);
	uniform float _BumpScale;
#endif

#if defined(AVAILABLE_MAINTEX) || defined(AVAILABLE_COLORMASK) || defined(AVAILABLE_EMISSIONMAP) || defined(AVAILABLE_NORMALMAP)
	#define AVAILABLE_ST 1
	// uniform float4 _MainTex_ST; // Shared ST
#endif

#if defined(RANDOM_SEED_TEX)
	Texture2D<uint> _Rnd_Seed; // DX11, no sampler
	#if defined(RANDOM_SCREEN_SCALE)
		uniform float2 _Rnd_ScreenScale;
	#endif
#endif

/*
	General uniforms and defines
*/

#if defined(KAWAFLT_PASS_FORWARD)
	#if defined(SHADE_KAWAFLT_LOG)
		#define SHADE_KAWAFLT 1
	#elif defined(SHADE_KAWAFLT_RAMP)
		#define SHADE_KAWAFLT 1
	#elif defined(SHADE_KAWAFLT_SINGLE)
		#define SHADE_KAWAFLT 1
	#endif
#endif

/* FPS features */
#if defined(FPS_ON)
	#if defined(FPS_MESH)
		#define NEED_CULL
		// TODO
	#endif
#endif

#if defined(PCW_ON)
	#define NEED_UV1
	// TODO UVS
#endif

/* Helper functions */

inline float3 KawaWorldSpaceCamPos() {
	#if defined(USING_STEREO_MATRICES)
		return (unity_StereoWorldSpaceCameraPos[0] + unity_StereoWorldSpaceCameraPos[1]) * 0.5;
	#else
		return _WorldSpaceCameraPos;
	#endif
}

inline float3 KawaWorldSpaceViewDir(float3 worldPos) {
	float3 dir;
	UNITY_BRANCH if (unity_OrthoParams.w > 0.5) {
		dir = normalize(unity_CameraWorldClipPlanes[4].xyz) * dot(float4(worldPos, 1.0), unity_CameraWorldClipPlanes[4]);
	} else {
		dir = KawaWorldSpaceCamPos() - worldPos;
	}
	return dir;
}

// https://www.laurivan.com/rgb-to-hsv-to-rgb-for-shaders/
inline half3 hsv2rgb(half3 hsv) {
	half4 K = half4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	half3 p = abs(frac(hsv.xxx + K.xyz) * 6.0h - K.www);
	return hsv.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), hsv.y);
}

inline half4x4 kawa_rotation(half3 angles) {
	half3 sins; half3 coss;
	sincos(angles, sins, coss);
	half4x4 rz = {
		coss.z, -sins.z, 0, 0,
		sins.z, coss.z, 0, 0,
		0, 0, 1, 0,
		0, 0, 0, 1,
	};
	half4x4 rx = {
		1, 0, 0, 0,
		0, coss.x, -sins.x, 0,
		0, sins.x, coss.x, 0,
		0, 0, 0, 1,
	};
	half4x4 ry = {
		coss.y, 0, sins.y, 0,
		0, 1, 0, 0,
		-sins.y, 0, coss.y, 0,
		0, 0, 0, 1,
	};
	return mul(mul(ry, rx), rz);
}

inline half4x4 kawa_rotation_inv(half3 angles) {
	half3 sins; half3 coss;
	sincos(angles, sins, coss);
	half4x4 rz = {
		coss.z, sins.z, 0, 0,
		-sins.z, coss.z, 0, 0,
		0, 0, 1, 0,
		0, 0, 0, 1,
	};
	half4x4 rx = {
		1, 0, 0, 0,
		0, coss.x, sins.x, 0,
		0, -sins.x, coss.x, 0,
		0, 0, 0, 1,
	};
	half4x4 ry = {
		coss.y, 0, -sins.y, 0,
		0, 1, 0, 0,
		sins.y, 0, coss.y, 0,
		0, 0, 0, 1,
	};
	return mul(mul(rz, rx), ry);
}

#endif // KAWAFLT_STRUCT_SHARED_INCLUDED