// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Kawashirov/CmdBuffer-DistanceMetrics"
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_Mask ("Mask (R)", 2D) = "white" {}
		_FocusSize ("Focus Area Size ", Range (0, 1)) = 0.333333
	}
	SubShader
	{
		Pass
		{
			CGPROGRAM
			#include "UnityCG.cginc"
			
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			
			uniform Texture2D<float> _MainTex;
			uniform SamplerState sampler_MainTex;
			uniform float4 _MainTex_TexelSize;

			uniform Texture2D<float> _Mask;
			uniform SamplerState sampler_Mask;
			uniform float4 _Mask_TexelSize;

			uniform float _FocusSize;

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv1 : TEXCOORD0;
				float2 uv2 : TEXCOORD1;
			};

			void vert(appdata v, out v2f o)
			{
				UNITY_INITIALIZE_OUTPUT(v2f, o);

				o.pos = UnityObjectToClipPos(v.vertex);

				float target_size = min(_MainTex_TexelSize.z, _MainTex_TexelSize.w);
				target_size *= _FocusSize;
				float2 uv_scale = target_size * _MainTex_TexelSize.xy; // target_size/width, target_size/height
				float2 uv_offset = float2(1, 1) * 0.5 - uv_scale * 0.5;

				o.uv1 = v.uv;
				o.uv2 = o.uv1 * uv_scale + uv_offset;
			}

			float4 frag(v2f i) : COLOR
			{
				double far = _ProjectionParams.z;
				double depth = UNITY_SAMPLE_TEX2D(_MainTex, i.uv2).r;
				double weight = UNITY_SAMPLE_TEX2D(_Mask, i.uv1).r;
				double depth_01 = 1.0 / (_ZBufferParams.x * depth + _ZBufferParams.y); // Linear01Depth(depth) but double
				double dst = lerp(0, far, depth_01); 
				return float4(dst * weight, i.uv2.x, i.uv2.y, weight);
			}
			
			ENDCG
		}
	} 
}