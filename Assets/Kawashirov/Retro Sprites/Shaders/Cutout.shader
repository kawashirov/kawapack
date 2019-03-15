Shader "Kawashirov/Retro Sprites Cutout" {
	Properties {
		[NoScaleOffset] _TexFront ("Front", 2D) = "white" {}
		[NoScaleOffset] _TexFrontRight ("Front-Right", 2D) = "white" {}
		[NoScaleOffset] _TexRight ("Right", 2D) = "white" {}
		[NoScaleOffset] _TexBackRight ("Back-Right", 2D) = "white" {}
		[NoScaleOffset] _TexBack ("Back", 2D) = "white" {}
		[NoScaleOffset] _TexBackLeft ("Back-Left", 2D) = "white" {}
		[NoScaleOffset] _TexLeft ("Left", 2D) = "white" {}
		[NoScaleOffset] _TexFrontLeft ("Front-Left", 2D) = "white" {}

		_Cutoff ("Cutout", Range(0, 1)) = 0.1
		[HDR] _Color ("Color", Color) = (1,1,1,1)
		[HDR] _Emission ("Emission", Color) = (1,1,1,0.1)
		
		_xtiles ("X Tiles", Int) = 1
		_ytiles ("Y Tiles", Int) = 1
		_framerate ("Frames Per Second", Int) = 1
		_frame ("Manual Frame Number", Float) = 0.0
		
		_MainTex ("Fallback texture", 2D) = "white" {}

		[HideInInspector] __TexMode ("__TexMode", Float) = 0.0
	}

	SubShader {
		Tags {
			"Queue"="AlphaTest"
			"RenderType"="TransparentCutout"
			"KawaRS_RenderType"="Cutout"
			"IgnoreProjector"="True"
			"PreviewType"="Plane"
		}
		
		Cull Off
		Zwrite On
		
		CGPROGRAM

			#pragma only_renderers d3d11

			#pragma target 5.0

			#pragma shader_feature SIDES_OFF SIDES_ONE SIDES_TWO SIDES_FOUR SIDES_EIGHT
			#pragma multi_compile _ VERTEXLIGHT_ON
			#pragma multi_compile_instancing

			#pragma surface surf Sprite vertex:vert alphatest:_Cutoff exclude_path:deferred exclude_path:prepass nometa nolightmap addshadow fullforwardshadows 

			#include "Shared.cginc"

	 	ENDCG
	}

	FallBack "Legacy Shaders/Diffuse"
	CustomEditor "KawaRSInspector"
}