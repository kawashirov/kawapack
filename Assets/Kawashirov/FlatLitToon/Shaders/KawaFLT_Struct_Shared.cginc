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


#if defined(CUTOFF_CLASSIC) || defined(CUTOFF_RANDOM) || defined(CUTOFF_PATTERN)
	#define CUTOFF_ON
#endif
#if defined(CUTOFF_CLASSIC)
	uniform float _Cutoff;
#endif
#if defined(CUTOFF_RANDOM) || defined(CUTOFF_PATTERN)
	uniform float _CutoffMin;
	uniform float _CutoffMax;
#endif
#if defined(CUTOFF_RANDOM)
	#define NEED_SCREENPOS 1
	#define NEED_SCREENPOS_RANDOM 1
#endif
#if defined(CUTOFF_PATTERN)
	#define NEED_SCREENPOS 1
	UNITY_DECLARE_TEX2D(_CutoffPattern);
	float4 _CutoffPattern_TexelSize;
#endif


#if defined(KAWAFLT_PASS_FORWARD)
	#if !defined(NO_OUTLINE)
		uniform float _outline_width;
		uniform float4 _outline_color;
		uniform float _outline_bias;
	#endif
#endif


#if defined(_EMISSION)
	uniform float4 _EmissionColor;
	UNITY_DECLARE_TEX2D(_EmissionMap);
	#define AVAILABLE_EMISSIONMAP 1
#endif

#if defined(_NORMALMAP)
	UNITY_DECLARE_TEX2D(_BumpMap);
	uniform float _BumpScale;
	#define AVAILABLE_NORMALMAP 1
#endif

#if defined(AVAILABLE_MAINTEX) || defined(AVAILABLE_COLORMASK) || defined(AVAILABLE_EMISSIONMAP) || defined(AVAILABLE_NORMALMAP)
	#define AVAILABLE_ST 1
	uniform float4 _MainTex_ST; // Shared ST
#endif


/*
	General uniforms and defines
*/

#if defined(KAWAFLT_PASS_FORWARD)
	#if defined(SHADE_CUBEDPARADOXFLT)

		uniform float _Sh_Cbdprdx_Shadow;

	#elif defined(SHADE_KAWAFLT_LOG)
		#define SHADE_KAWAFLT 1

		uniform float _Sh_Kwshrv_RimScl;
		uniform float4 _Sh_Kwshrv_RimClr;
		uniform float _Sh_Kwshrv_RimPwr;
		uniform float _Sh_Kwshrv_RimBs;

		uniform float _Sh_Kwshrv_FltFctr;
		uniform float _Sh_Kwshrv_BndSmth;
		uniform float _Sh_Kwshrv_FltLogSclA;

		uniform float _Sh_Kwshrv_Smth;
		uniform float _Sh_Kwshrv_Smth_Tngnt;

	#elif defined(SHADE_KAWAFLT_RAMP)
		#define SHADE_KAWAFLT 1

		UNITY_DECLARE_TEX2D(_Sh_KwshrvRmp_Tex);
		uniform float _Sh_KwshrvRmp_Pwr;
		uniform float4 _Sh_KwshrvRmp_NdrctClr;

	#elif defined(SHADE_KAWAFLT_SINGLE)
		#define SHADE_KAWAFLT 1

		uniform float _Sh_Kwshrv_Smth;
		uniform float _Sh_KwshrvSngl_TngntLo;
		uniform float _Sh_KwshrvSngl_TngntHi;
		uniform float _Sh_KwshrvSngl_ShdLo;
		uniform float _Sh_KwshrvSngl_ShdHi;

		inline float shade_kawaflt_single_tangency_transform(float tangency) {
			float2 t;
			t.x = min(_Sh_KwshrvSngl_TngntLo, _Sh_KwshrvSngl_TngntHi);
			t.y = max(_Sh_KwshrvSngl_TngntLo, _Sh_KwshrvSngl_TngntHi);
			t = 2.0 * t - 1.0;
			float ref = saturate( (tangency - t.x) / (t.y - t.x) );
			float flat = lerp(_Sh_KwshrvSngl_ShdLo, _Sh_KwshrvSngl_ShdHi, ref);
			return saturate(lerp(max(0, tangency), flat, _Sh_Kwshrv_Smth));
		}

	#else
		#error SHADE_???
	#endif
#endif


/* Distance fade features */
#define DSTFD_RND_SEED 36179
#if defined(DSTFD_RANGE) || defined(DSTFD_INFINITY)
	#define DSTFD_ON 1

	#if !defined(DSTFD_RANDOM_PIXEL) && !defined(DSTFD_RANDOM_VERTEX) && !defined(DSTFD_RANDOM_PATTERN)
		#error DSTFD_RANDOM_???
	#endif

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

	#if defined(DSTFD_RANDOM_PATTERN)
		UNITY_DECLARE_TEX2D(_DstFd_Pattern);
		float4 _DstFd_Pattern_TexelSize;
		#define NEED_SCREENPOS
	#else
		#define NEED_SCREENPOS_RANDOM
	#endif
#else
	#define DSTFD_OFF 1
#endif


/* FPS features */
#if defined(FPS_COLOR) || defined(FPS_TEX) || defined(FPS_MESH)
	#define FPS_ON 1
	uniform float4 _FPS_TLo;
	uniform float4 _FPS_THi;
#else
	#define FPS_OFF 1
#endif
#if defined(FPS_MESH)
	#define NEED_CULL
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
#else
	#define PCW_OFF
#endif

/*
	Disintegration features
*/

// Feature strings:
// #pragma shader_feature _ DSNTGRT_PIXEL
// #pragma shader_feature _ DSNTGRT_FACE
#define DSNTGRT_RND_SEED 26842
#if defined(DSNTGRT_PIXEL) || defined(DSNTGRT_FACE)
	#define DSNTGRT_ON 1

	uniform float4 _Dsntgrt_Plane;

	// #if defined(DSNTGRT_PIXEL) && defined(DSNTGRT_FACE)
	// 	#define DSNTGRT_PIXEL_FACE 1
	// #endif

	// #if defined(DSNTGRT_PIXEL)
	// 	// uniform float _Dsntgrt_FragDecayNear;
	// 	// uniform float _Dsntgrt_FragDecayFar;
	// 	// uniform float _Dsntgrt_FragPowerAdjust;
	// 	// #define NEED_SCREENPOS_RANDOM
	// #endif

	#if defined(DSNTGRT_FACE)
		uniform float4 _Dsntgrt_Tint;
		uniform float _Dsntgrt_TriSpreadAccel;
		uniform float _Dsntgrt_TriSpreadFactor; 
		uniform float _Dsntgrt_TriDecayNear;
		uniform float _Dsntgrt_TriDecayFar;
		uniform float _Dsntgrt_TriPowerAdjust;
		#if defined(KAWAFLT_FEATURES_TESSELLATION)
			uniform float _Dsntgrt_Tsltn;
		#endif
	#endif
#else
	#define DSNTGRT_OFF
#endif

// v2g & g2f entries
#if defined(DSNTGRT_PIXEL)
	// Vertex position in disintegration space, need for fragment
	#define G2F_DSNTGRT float4 dsntgrtVertexRotated : DSNTGRT_VTR; 
#else
	#define G2F_DSNTGRT
#endif

#if defined(NEED_SCREENPOS_RANDOM)
	#define NEED_SCREENPOS 1
#endif



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
half3 hsv2rgb(half3 hsv) {
	half4 K = half4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	half3 p = abs(frac(hsv.xxx + K.xyz) * 6.0h - K.www);
	return hsv.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), hsv.y);
}

half4x4 kawa_rotation(half3 angles) {
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

half4x4 kawa_rotation_inv(half3 angles) {
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