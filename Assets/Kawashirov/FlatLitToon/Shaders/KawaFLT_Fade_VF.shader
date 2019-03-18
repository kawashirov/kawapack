Shader "Kawashirov/Flat Lit Toon/Fade VF (Lightweight)" {
	Properties {

		_MainTex("MainTex", 2D) = "white" {}
		[HDR] _Color("Color", Color) = (1,1,1,1)
		_ColorMask("ColorMask", 2D) = "black" {}
		[Normal] _BumpMap("BumpMap", 2D) = "bump" {}
		_BumpScale("_BumpScale", Float) = 1.0
		_EmissionMap("Emission Map", 2D) = "white" {}
		// /* Editor */ [HideInInspector]  _EmissionMode("_EmissionMode", Float) = 0.0
		[HDR] _EmissionColor("Emission Color", Color) = (0,0,0,1)

		/* Editor */ [HideInInspector] _CutoffMode("_CutoffMode", Float) = 0.0
		_CutoffPattern("_CutoffPattern", 2D) = "gray" {}
		_Cutoff("_Cutoff", Range(0,1)) = 0.5
		_CutoffMin("_CutoffMin", Range(0,1)) = 0.4
		_CutoffMax("_CutoffMax", Range(0,1)) = 0.6

		/* Editor */ [HideInInspector] _Sh_Mode("_Sh_Mode", Float) = 0

		_Sh_Cbdprdx_Shadow("_Sh_Cbdprdx_Shadow", Range(0, 1)) = 0.4

		_Sh_Kwshrv_Smth("_Sh_Kwshrv_Smth", Range(0, 1)) = 0.1
		_Sh_Kwshrv_Smth_Tngnt("_Sh_Kwshrv_Smth_Tngnt", Range(0, 1)) = 0.4
		_Sh_Kwshrv_FltFctr("_Sh_Kwshrv_FltFctr", Range(0, 1)) = 0.9
		[PowerSlider(2.0)] _Sh_Kwshrv_BndSmth("_Sh_Kwshrv_BndSmth", Range(0, 1)) = 0.1
		/* Editor */ [HideInInspector] _Sh_Kwshrv_FltMode("_Sh_Kwshrv_FltMode", Float) = 0
		[PowerSlider(10)] _Sh_Kwshrv_FltLinScl("_Sh_Kwshrv_FltLinScl", Range(0, 20)) = 5
		_Sh_Kwshrv_FltLinCnst("_Sh_Kwshrv_FltLinCnst", Range(-10, 10)) = 0
		[PowerSlider(10)] _Sh_Kwshrv_FltLogSclA("_Sh_Kwshrv_FltLogSclA", Range(0.1, 10)) = 1.0
		[PowerSlider(2.0)] _Sh_Kwshrv_RimScl("_Sh_Kwshrv_RimScl", Range(-5, 5)) = 0.1
		[HDR] _Sh_Kwshrv_RimClr("_Sh_Kwshrv_RimClr", Color) = (1,1,1,1)
		[PowerSlider(5.0)] _Sh_Kwshrv_RimPwr("_Sh_Kwshrv_RimPwr", Range(0.1, 10)) = 1
		_Sh_Kwshrv_RimBs("_Sh_Kwshrv_RimBs", Range(-1, 1)) = 0

		_Sh_KwshrvRmp_Tex("_Sh_KwshrvRmp_Tex", 2D) = "gray" {}
		[HDR] _Sh_KwshrvRmp_NdrctClr("_Sh_KwshrvRmp_NdrctClr", Color) = (1,1,1,1)
		[PowerSlider(5.0)] _Sh_KwshrvRmp_Pwr("_Sh_KwshrvRmp_Pwr", Range(0.1, 10)) = 1

		[PowerSlider(1.5)] _Sh_KwshrvSngl_TngntLo("_Sh_KwshrvSngl_TngntLo", Range(0, 1)) = 0.7
		[PowerSlider(1.5)] _Sh_KwshrvSngl_TngntHi("_Sh_KwshrvSngl_TngntHi", Range(0, 1)) = 0.8
		_Sh_KwshrvSngl_ShdLo("_Sh_KwshrvSngl_ShdLo", Range(0, 1)) = 0.4 
		_Sh_KwshrvSngl_ShdHi("_Sh_KwshrvSngl_ShdHi", Range(0, 1)) = 0.9

		// Ditance fade feature
		/* Editor */ [HideInInspector] _DstFd_Mode("_DstFd_Mode", Float) = 0.0
		/* Editor */ [HideInInspector] _DstFd_Random("_DstFd_Random", Float) = 0.0
		_DstFd_Axis("_DstFd_Axis", Vector) = (1,1,1,1)
		_DstFd_Near("_DstFd_Near", Range(0, 100)) = 1.0
		_DstFd_Far("_DstFd_Far", Range(0, 100)) = 2.0
		[PowerSlider(10)] _DstFd_AdjustPower("_DstFd_AdjustPower", Range(0.1, 10)) = 1.0
		_DstFd_AdjustScale("_DstFd_AdjustScale", Range(0.1, 10)) = 1.0
		_DstFd_Pattern("_DstFd_Pattern", 2D) = "white" {}

		/* Editor */ [HideInInspector] _FPS_Mode("_FPS_Mode", Float) = 0.0
		[HDR] _FPS_TLo("_FPS_TLo", Color) = (1,0.5,0.5,1)
		[HDR] _FPS_THi("_FPS_THi", Color) = (0.5,1,0.5,1)

		[HideInInspector] _Mode ("__mode", Float) = 0.0
		[HideInInspector] _BlendKeywords ("_BlendKeywords", Float) = 0.0
		[HideInInspector] _Cull ("_Cull", Float) = 2.0

	}

	SubShader {
		Tags {
			"KawaFLT_Features"="Lightweight"
			"KawaFLT_RenderType"="Fade"
			"Queue"="Transparent"
			"RenderType"="Transparent"
			"IgnoreProjector"="True"
			"ForceNoShadowCasting"="True"
		}

		Pass {
			Name "FORWARD"
			Tags { "LightMode" = "ForwardBase" }

			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Cull [_Cull]

			CGPROGRAM
			#define KAWAFLT_PASS_FORWARDBASE 1
			#define KAWAFLT_PASS_LIGHTWEIGHT 1
			#define _ALPHABLEND_ON 1

			// #pragma enable_d3d11_debug_symbols
			
			#pragma shader_feature SHADE_CUBEDPARADOXFLT SHADE_KAWAFLT_LOG SHADE_KAWAFLT_RAMP SHADE_KAWAFLT_SINGLE

			#pragma shader_feature MAINTEX_OFF MAINTEX_NOMASK MAINTEX_COLORMASK
			#pragma shader_feature _ _NORMALMAP
			#pragma shader_feature _ _EMISSION

			#pragma shader_feature _ DSTFD_RANGE DSTFD_INFINITY
			#if defined(DSTFD_RANGE) || defined(DSTFD_INFINITY)
				#pragma shader_feature _ DSTFD_DIRECTION_BACKWARD
				#pragma shader_feature DSTFD_RANDOM_PIXEL DSTFD_RANDOM_PATTERN
			#endif

			#pragma shader_feature _ FPS_COLOR FPS_TEX FPS_MESH
			
			#include "KawaFLT_Struct_VF.cginc"
			#include "KawaFLT_PreFrag_VF.cginc"
			#include "KawaFLT_Frag_ForwardBase.cginc"
			
			#pragma vertex vert
			#pragma fragment frag_forwardbase

			#pragma only_renderers d3d11
			#pragma target 5.0

			#pragma multi_compile_fwdbase
			#pragma multi_compile_fog
			#pragma multi_compile_instancing

			
			ENDCG
		}

		Pass {
			Name "FORWARD_DELTA"
			Tags { "LightMode" = "ForwardAdd" }
			Blend SrcAlpha One
			ZWrite Off
			Cull [_Cull]

			CGPROGRAM
			#define KAWAFLT_PASS_FORWARDADD 1
			#define KAWAFLT_PASS_LIGHTWEIGHT 1
			#define _ALPHABLEND_ON 1

			// #pragma enable_d3d11_debug_symbols
			
			#pragma shader_feature SHADE_CUBEDPARADOXFLT SHADE_KAWAFLT_LOG SHADE_KAWAFLT_RAMP SHADE_KAWAFLT_SINGLE

			#pragma shader_feature MAINTEX_OFF MAINTEX_NOMASK MAINTEX_COLORMASK
			#pragma shader_feature _ _NORMALMAP

			#pragma shader_feature _ DSTFD_RANGE DSTFD_INFINITY
			#if defined(DSTFD_RANGE) || defined(DSTFD_INFINITY)
				#pragma shader_feature _ DSTFD_DIRECTION_BACKWARD
				#pragma shader_feature DSTFD_RANDOM_PIXEL DSTFD_RANDOM_PATTERN
			#endif

			#pragma shader_feature _ FPS_COLOR FPS_TEX FPS_MESH
			
			#include "KawaFLT_Struct_VF.cginc"
			#include "KawaFLT_PreFrag_VF.cginc"
			#include "KawaFLT_Frag_ForwardAdd.cginc"
			
			#pragma vertex vert
			#pragma fragment frag_forwardadd

			#pragma only_renderers d3d11
			#pragma target 5.0

			#pragma multi_compile_fwdadd_fullshadows
			#pragma multi_compile_fog
			//#pragma multi_compile_instancing
			#pragma skip_variants INSTANCING_ON
			
			ENDCG
		}
		
		Pass {
			Name "SHADOW_CASTER"
			Tags { "LightMode" = "ShadowCaster" }

			ZWrite On
			Cull [_Cull]

			CGPROGRAM
			#define KAWAFLT_PASS_SHADOWCASTER 1
			#define KAWAFLT_PASS_LIGHTWEIGHT 1
			#define _ALPHABLEND_ON 1

			// #pragma enable_d3d11_debug_symbols
			
			#pragma shader_feature MAINTEX_OFF MAINTEX_NOMASK MAINTEX_COLORMASK
			#pragma shader_feature CUTOFF_CLASSIC CUTOFF_RANDOM CUTOFF_PATTERN

			#pragma shader_feature _ DSTFD_RANGE DSTFD_INFINITY
			#if defined(DSTFD_RANGE) || defined(DSTFD_INFINITY)
				#pragma shader_feature _ DSTFD_DIRECTION_BACKWARD
				#pragma shader_feature DSTFD_RANDOM_PIXEL DSTFD_RANDOM_PATTERN
			#endif

			#pragma shader_feature _ FPS_COLOR FPS_TEX FPS_MESH
			
			#include "KawaFLT_Struct_VF.cginc"
			#include "KawaFLT_PreFrag_VF.cginc"
			#include "KawaFLT_Frag_ShadowCaster.cginc"
			
			#pragma vertex vert
			#pragma fragment frag_shadowcaster
			
			#pragma only_renderers d3d11
			#pragma target 5.0

			#pragma multi_compile_shadowcaster
			#pragma fragmentoption ARB_precision_hint_fastest
			//#pragma multi_compile_instancing
			#pragma skip_variants INSTANCING_ON
			
			ENDCG
		}
	}

	FallBack "CubedParadox/Flat Lit Toon Lite Fade"
	CustomEditor "KawaFLTInspector"
}