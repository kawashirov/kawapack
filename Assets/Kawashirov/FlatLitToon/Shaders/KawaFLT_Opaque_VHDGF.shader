Shader "Kawashirov/Flat Lit Toon/Opaque VHDGF (Tessellation+Geometry)" {
	Properties {

		_MainTex("MainTex", 2D) = "white" {}
		[HDR] _Color("Color", Color) = (1,1,1,1)
		_ColorMask("ColorMask", 2D) = "black" {}
		[Normal] _BumpMap("BumpMap", 2D) = "bump" {}
		_BumpScale("_BumpScale", Float) = 1.0
		_EmissionMap("Emission Map", 2D) = "white" {}
		// /* Editor */ [HideInInspector]  _EmissionMode("_EmissionMode", Float) = 0.0
		[HDR] _EmissionColor("Emission Color", Color) = (0,0,0,1)

		[HideInInspector] _Tsltn_Prt ("_Tsltn_Prt", Float) = 0.0
		[HideInInspector] _Tsltn_Dmn ("_Tsltn_Dmn", Float) = 0.0
		[HideInInspector] _Tsltn_Phng ("_Tsltn_Phng", Float) = 0.0
		[PowerSlider(2.0)] _Tsltn_Uni("_Tsltn_Uni", Range(0.9, 10)) = 1
		[PowerSlider(2.0)] _Tsltn_Nrm("_Tsltn_Nrm", Range(0, 20)) = 1
		[PowerSlider(10.0)] _Tsltn_Inside("_Tsltn_Inside", Range(0.1, 10)) = 1

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

		/* Editor */ [HideInInspector] _OutlineMode("__outline_mode", Float) = 0.0
		_outline_width("outline_width", Range(0, 1)) = 0.2
		[HDR] _outline_color("outline_color", Color) = (0.5,0.5,0.5,1)
		_outline_tint("outline_tint", Range(0, 1)) = 0.5
		_outline_bias("outline_bias", Range(-1, 5)) = 0

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

		// Disintegration feature
		/* Editor */ [HideInInspector] _Dsntgrt_Mode("_Dsntgrt_Mode", Float) = 0.0
		_Dsntgrt_Plane("_Dsntgrt_Plane", Vector) = (0, 0, 0, 0) // XYZ rotation and W offset
		_Dsntgrt_TriSplitArea("_Dsntgrt_TriSplitArea", Range(0, 20)) = 2
		_Dsntgrt_TriSpreadAccel("_Dsntgrt_TriSpreadAccel", Range(0, 5)) = 2
		_Dsntgrt_TriSpreadFactor("_Dsntgrt_TriSpreadFactor", Range(0, 2)) = 0.1
		_Dsntgrt_TriDecayNear("_Dsntgrt_TriDecayNear", Range(0, 10)) = 0
		_Dsntgrt_TriDecayFar("_Dsntgrt_TriDecayFar", Range(0, 10)) = .5
		_Dsntgrt_TriPowerAdjust("_Dsntgrt_TriPowerAdjust", Range(0.5, 2)) = 1
		_Dsntgrt_Tint("_Dsntgrt_Tint", Color) = (0.2,0.2,0.2,0.1)
		[PowerSlider(2.0)] _Dsntgrt_Tsltn("_Dsntgrt_Tsltn", Range(0, 10)) = 1

		// PolyColorWave feature
		/* Editor */ [HideInInspector] _PCW_Mode("_PCW_Mode", Float) = 0.0
		_PCW_WvTmLo("_PCW_WvTmLo", Float) = 4
		_PCW_WvTmAs("_PCW_WvTmAs", Float) = 0.25
		_PCW_WvTmHi("_PCW_WvTmHi", Float) = 0.5
		_PCW_WvTmDe("_PCW_WvTmDe", Float) = 0.25
		_PCW_WvTmUV("_PCW_WvTmUV", Vector) = (0,10,0,0)
		_PCW_WvTmVtx("_PCW_WvTmVtx", Vector) = (0,0,0,0)
		_PCW_WvTmRnd("_PCW_WvTmRnd", Float) = 5
		[PowerSlider(2.0)] _PCW_Em("_PCW_Em", Range(0, 1)) = .5
		[HDR] _PCW_Color("_PCW_Color", Color) = (1, 1, 1, 1)
		_PCW_RnbwTm("_PCW_RnbwTm", Float) = 0.5
		_PCW_RnbwTmRnd("_PCW_RnbwTmRnd", Float) = 0.5
		_PCW_RnbwStrtn("_PCW_RnbwStrtn", Range(0, 1)) = 1
		_PCW_RnbwBrghtnss("_PCW_RnbwBrghtnss", Range(0, 1)) = 0.5
		_PCW_Mix("_PCW_Mix", Range(0, 1)) = .5

		[HideInInspector] _Mode ("__mode", Float) = 0.0
		[HideInInspector] _BlendKeywords ("_BlendKeywords", Float) = 0.0
		[HideInInspector] _Cull ("_Cull", Float) = 2.0

	}

	SubShader {
		Tags {
			"KawaFLT_Features"="Geometry,Tessellation"
			"KawaFLT_RenderType"="Opaque"
			"Queue"="Geometry"
			"RenderType"="Opaque"
			"IgnoreProjector"="True"
			"DisableBatching"="True"
		}

		Pass {
			Name "FORWARD"
			Tags { "LightMode" = "ForwardBase" }

			Blend One Zero
			ZWrite On
			Cull [_Cull]

			CGPROGRAM
			#define KAWAFLT_PASS_FORWARDBASE 1
			#define KAWAFLT_FEATURES_GEOMETRY 1
			#define KAWAFLT_FEATURES_TESSELLATION 1

			// #pragma enable_d3d11_debug_symbols
			
			#pragma shader_feature _ TESS_D_QUAD
			#pragma shader_feature TESS_P_INT TESS_P_EVEN TESS_P_ODD TESS_P_POW2

			#pragma shader_feature SHADE_CUBEDPARADOXFLT SHADE_KAWAFLT_DIFFUSE SHADE_KAWAFLT_RAMP	

			#pragma shader_feature MAINTEX_OFF MAINTEX_NOMASK MAINTEX_COLORMASK
			#pragma shader_feature _ _NORMALMAP
			#pragma shader_feature _ _EMISSION

			#pragma shader_feature NO_OUTLINE TINTED_OUTLINE COLORED_OUTLINE

			#pragma shader_feature _ DSTFD_RANGE DSTFD_INFINITY
			#if defined(DSTFD_RANGE) || defined(DSTFD_INFINITY)
				#pragma shader_feature _ DSTFD_DIRECTION_BACKWARD
				#pragma shader_feature DSTFD_RANDOM_PIXEL DSTFD_RANDOM_PATTERN
			#endif

			#pragma shader_feature _ FPS_COLOR FPS_TEX FPS_MESH

			// #pragma shader_feature _ DSNTGRT_PIXEL
			#pragma shader_feature _ DSNTGRT_FACE

			#pragma shader_feature _ PCW_ON
			
			#include "KawaFLT_Struct_VHDGF.cginc"
			#include "KawaFLT_PreFrag_VHDGF.cginc"
			#include "KawaFLT_Frag_ForwardBase.cginc"
			
			#pragma vertex vert
			#pragma hull hull
			#pragma domain domain
			#pragma geometry geom
			#pragma fragment frag_forwardbase

			#pragma only_renderers d3d11
			#pragma target 5.0

			#pragma multi_compile_fwdbase
			#pragma multi_compile_fog
			//#pragma multi_compile_instancing
			#pragma skip_variants INSTANCING_ON
			
			ENDCG
		}

		Pass {
			Name "FORWARD_DELTA"
			Tags { "LightMode" = "ForwardAdd" }
			Blend One One
			ZWrite Off
			Cull [_Cull]

			CGPROGRAM
			#define KAWAFLT_PASS_FORWARDADD 1
			#define KAWAFLT_FEATURES_GEOMETRY 1
			#define KAWAFLT_FEATURES_TESSELLATION 1

			// #pragma enable_d3d11_debug_symbols
			
			#pragma shader_feature _ TESS_D_QUAD
			#pragma shader_feature TESS_P_INT TESS_P_EVEN TESS_P_ODD TESS_P_POW2
			
			#pragma shader_feature SHADE_CUBEDPARADOXFLT SHADE_KAWAFLT_DIFFUSE SHADE_KAWAFLT_RAMP

			#pragma shader_feature MAINTEX_OFF MAINTEX_NOMASK MAINTEX_COLORMASK
			#pragma shader_feature _ _NORMALMAP

			#pragma shader_feature NO_OUTLINE TINTED_OUTLINE COLORED_OUTLINE

			#pragma shader_feature _ DSTFD_RANGE DSTFD_INFINITY
			#if defined(DSTFD_RANGE) || defined(DSTFD_INFINITY)
				#pragma shader_feature _ DSTFD_DIRECTION_BACKWARD
				#pragma shader_feature DSTFD_RANDOM_PIXEL DSTFD_RANDOM_PATTERN
			#endif

			#pragma shader_feature _ FPS_COLOR FPS_TEX FPS_MESH

			// #pragma shader_feature _ DSNTGRT_PIXEL
			#pragma shader_feature _ DSNTGRT_FACE
			
			#include "KawaFLT_Struct_VHDGF.cginc"
			#include "KawaFLT_PreFrag_VHDGF.cginc"
			#include "KawaFLT_Frag_ForwardAdd.cginc"
			
			#pragma vertex vert
			#pragma hull hull
			#pragma domain domain
			#pragma geometry geom
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
			ZTest LEqual
			Cull [_Cull]

			CGPROGRAM
			#define KAWAFLT_PASS_SHADOWCASTER 1
			#define KAWAFLT_FEATURES_GEOMETRY 1
			#define KAWAFLT_FEATURES_TESSELLATION 1
			
			// #pragma enable_d3d11_debug_symbols
			
			#pragma shader_feature _ TESS_D_QUAD
			#pragma shader_feature TESS_P_INT TESS_P_EVEN TESS_P_ODD TESS_P_POW2

			#pragma shader_feature MAINTEX_OFF MAINTEX_NOMASK MAINTEX_COLORMASK

			#pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON

			#define NO_OUTLINE 1

			#pragma shader_feature _ DSTFD_RANGE DSTFD_INFINITY
			#if defined(DSTFD_RANGE) || defined(DSTFD_INFINITY)
				#pragma shader_feature _ DSTFD_DIRECTION_BACKWARD
				#pragma shader_feature DSTFD_RANDOM_PIXEL DSTFD_RANDOM_PATTERN
			#endif

			#pragma shader_feature _ FPS_COLOR FPS_TEX FPS_MESH

			// #pragma shader_feature _ DSNTGRT_PIXEL
			#pragma shader_feature _ DSNTGRT_FACE
			
			#include "KawaFLT_Struct_VHDGF.cginc"
			#include "KawaFLT_PreFrag_VHDGF.cginc"
			#include "KawaFLT_Frag_ShadowCaster.cginc"
			
			#pragma vertex vert
			#pragma hull hull
			#pragma domain domain
			#pragma geometry geom
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

	FallBack "CubedParadox/Flat Lit Toon"
	CustomEditor "KawaFLTInspector"
}