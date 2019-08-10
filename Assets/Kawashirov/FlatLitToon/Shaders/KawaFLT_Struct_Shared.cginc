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

#if defined(CUTOFF_CLASSIC)
	uniform float _Cutoff;
#endif
#if defined(CUTOFF_RANDOM)
	uniform float _CutoffMin;
	uniform float _CutoffMax;
#endif

#if defined(KAWAFLT_PASS_FORWARD)
	#if !defined(OUTLINE_OFF)
		uniform float _outline_width;
		uniform float4 _outline_color;
		uniform float _outline_bias;
	#endif
#endif

#if defined(_EMISSION)
	uniform float4 _EmissionColor;
	#if defined(EMISSION_ALBEDO_MASK) || defined(EMISSION_CUSTOM)
		UNITY_DECLARE_TEX2D(_EmissionMap);
	#endif
	#define AVAILABLE_EMISSIONMAP 1
#endif

#if defined(_NORMALMAP)
	UNITY_DECLARE_TEX2D(_BumpMap);
	uniform float _BumpScale;
	#define AVAILABLE_NORMALMAP 1
#endif

#if defined(AVAILABLE_MAINTEX) || defined(AVAILABLE_COLORMASK) || defined(AVAILABLE_EMISSIONMAP) || defined(AVAILABLE_NORMALMAP)
	#define AVAILABLE_ST 1
	// uniform float4 _MainTex_ST; // Shared ST
#endif

#if defined(RANDOM_SEED_TEX)
	Texture2D<uint> _Rnd_Seed; // DX11, no sampler
#endif

/*
	General uniforms and defines
*/

#if defined(KAWAFLT_PASS_FORWARD)
	#if defined(SHADE_CUBEDPARADOXFLT)

		uniform float _Sh_Cbdprdx_Shadow;

	#elif defined(SHADE_KAWAFLT_LOG)
		#define SHADE_KAWAFLT 1

		uniform float _Sh_Kwshrv_ShdBlnd;

		uniform float _Sh_Kwshrv_RimScl;
		uniform float4 _Sh_Kwshrv_RimClr;
		uniform float _Sh_Kwshrv_RimPwr;
		uniform float _Sh_Kwshrv_RimBs;

		uniform float _Sh_KwshrvLog_Fltnss;
		uniform float _Sh_Kwshrv_BndSmth;
		uniform float _Sh_Kwshrv_FltLogSclA;

		uniform float _Sh_Kwshrv_Smth;
		uniform float _Sh_Kwshrv_Smth_Tngnt;

	#elif defined(SHADE_KAWAFLT_RAMP)
		#define SHADE_KAWAFLT 1

		uniform float _Sh_Kwshrv_ShdBlnd;

		UNITY_DECLARE_TEX2D(_Sh_KwshrvRmp_Tex);
		uniform float _Sh_KwshrvRmp_Pwr;
		uniform float4 _Sh_KwshrvRmp_NdrctClr;

	#elif defined(SHADE_KAWAFLT_SINGLE)
		#define SHADE_KAWAFLT 1

		uniform float _Sh_Kwshrv_ShdBlnd;

		uniform float _Sh_Kwshrv_Smth;
		uniform float _Sh_KwshrvSngl_TngntLo;
		uniform float _Sh_KwshrvSngl_TngntHi;
		uniform float _Sh_KwshrvSngl_ShdLo;
		uniform float _Sh_KwshrvSngl_ShdHi;

		inline float shade_kawaflt_single(float tangency, float shadow_atten) {
			// Определено здесь, т.к. может использоваться на любом стейдже.
			half2 t;
			t.x = min(_Sh_KwshrvSngl_TngntLo, _Sh_KwshrvSngl_TngntHi);
			t.y = max(_Sh_KwshrvSngl_TngntLo, _Sh_KwshrvSngl_TngntHi);
			t = 2.0 * t - 1.0;
			half ref_light = saturate( (tangency - t.x) / (t.y - t.x) );
			ref_light = ref_light * ref_light * (3.0 - 2.0 * ref_light); // Cubic Hermite H01 interpolation
			half sh_blended = lerp(1.0, shadow_atten, _Sh_Kwshrv_ShdBlnd);
			half sh_separated = lerp(shadow_atten, 1.0, _Sh_Kwshrv_ShdBlnd);
			return lerp(_Sh_KwshrvSngl_ShdLo, _Sh_KwshrvSngl_ShdHi, ref_light * sh_blended) * sh_separated;
		}

	#else
		#error SHADE_???
	#endif
#endif


/* Distance fade features */
#define DSTFD_RND_SEED 36179
#if defined(DSTFD_ON)
	uniform float _DstFd_Near;
	uniform float _DstFd_AdjustPower;
	uniform float4 _DstFd_Axis;

	#if defined(DSTFD_RANGE)
		uniform float _DstFd_Far;
	#endif

	#if defined(DSTFD_INFINITY)
		// uniform float _DstFd_Far;
		uniform float _DstFd_AdjustScale;
	#endif
#endif


/* FPS features */
#if defined(FPS_ON)
	uniform float4 _FPS_TLo;
	uniform float4 _FPS_THi;
	#if defined(FPS_MESH)
		#define NEED_CULL
	#endif
#endif


/* PolyColorWave features */

// Feature strings:
// #pragma shader_feature _ PCW_ON
#define PCW_RND_SEED 22443
#if defined(PCW_ON)
	uniform float _PCW_WvTmLo;
	uniform float _PCW_WvTmAs;
	uniform float _PCW_WvTmHi;
	uniform float _PCW_WvTmDe;
	uniform float4 _PCW_WvTmUV;
	uniform float4 _PCW_WvTmVtx;
	uniform float _PCW_WvTmRnd;
	uniform float _PCW_Em;
	uniform float4 _PCW_Color;
	uniform float _PCW_RnbwTm;
	uniform float _PCW_RnbwTmRnd;
	uniform float _PCW_RnbwStrtn;
	uniform float _PCW_RnbwBrghtnss;
	uniform float _PCW_Mix;
#endif

/*
	Disintegration features
*/

// Feature strings:
#define DSNTGRT_RND_SEED 26842
#if defined(DSNTGRT_ON)
	uniform float4 _Dsntgrt_Plane;
	uniform float4 _Dsntgrt_Tint;
	uniform float _Dsntgrt_TriSpreadAccel;
	uniform float _Dsntgrt_TriSpreadFactor; 
	uniform float _Dsntgrt_TriDecayNear;
	uniform float _Dsntgrt_TriDecayFar;
	uniform float _Dsntgrt_TriPowerAdjust;
	#if defined(KAWAFLT_F_TESSELLATION)
		uniform float _Dsntgrt_Tsltn;
	#endif
#endif

// v2g & g2f entries

static const half3 grayscale_vector = half3(0, 0.3823529, 0.01845836);

inline half grayscaleSH9(half3 normalDirection) {
	return dot(ShadeSH9(half4(normalDirection, 1)), grayscale_vector);
}


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