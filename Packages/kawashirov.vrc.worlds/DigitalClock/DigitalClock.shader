Shader "Unlit/DigitalClock"
{
    Properties
    {
        _NoiseTex ("_NoiseTex", 2D) = "white" {}
        _MaskTex ("_MaskTex", 2D) = "white" {}
        _Color ("_Color", Color) = (1,1,1,1)
        _ColorRemap ("_ColorRemap", Vector) = (0,1,0,1)
        _EdgeRemap ("_EdgeRemap", Vector) = (0,1,0,1)
        _EdgeTH ("_EdgeTH", Float) = 0.1
        _Speed ("_Speed", Float) = 1

        _Digits ("_Digits", Vector) = (1,2,3,4)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
            };

            struct v2f
            {
                float2 uv_m : TEXCOORD0;
                float4 uv_ab : KAWA_UV_NOISE_AB;
                float4 uv_cd : KAWA_UV_NOISE_CD;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float drop : KAWA_DROP;
                float center : KAWA_CENTER;
            };

            uniform sampler2D _NoiseTex;
            uniform float4 _NoiseTex_ST;

            uniform sampler2D _MaskTex;
            uniform float4 _MaskTex_ST;

            uniform float4 _Color;
            uniform float4 _ColorRemap;
            uniform float4 _EdgeRemap;
            uniform float _EdgeTH;
            uniform float _Speed;

            uniform float4 _Digits;

            static const int SEGMENTS[10] = {
                //          MID       L-UP       L-DN       DOWN       R-DN       R-UP         UP
                /* 0 */ (0 << 6) + (1 << 5) + (1 << 4) + (1 << 3) + (1 << 2) + (1 << 1) + (1 << 0),
                /* 1 */ (0 << 6) + (0 << 5) + (0 << 4) + (0 << 3) + (1 << 2) + (1 << 1) + (0 << 0),
                /* 2 */ (1 << 6) + (0 << 5) + (1 << 4) + (1 << 3) + (0 << 2) + (1 << 1) + (1 << 0),
                /* 3 */ (1 << 6) + (0 << 5) + (0 << 4) + (1 << 3) + (1 << 2) + (1 << 1) + (1 << 0),
                /* 4 */ (1 << 6) + (1 << 5) + (0 << 4) + (0 << 3) + (1 << 2) + (1 << 1) + (0 << 0),
                /* 5 */ (1 << 6) + (1 << 5) + (0 << 4) + (1 << 3) + (1 << 2) + (0 << 1) + (1 << 0),
                /* 6 */ (1 << 6) + (1 << 5) + (1 << 4) + (1 << 3) + (1 << 2) + (0 << 1) + (1 << 0),
                /* 7 */ (0 << 6) + (0 << 5) + (0 << 4) + (0 << 3) + (1 << 2) + (1 << 1) + (1 << 0),
                /* 8 */ (1 << 6) + (1 << 5) + (1 << 4) + (1 << 3) + (1 << 2) + (1 << 1) + (1 << 0),
                /* 9 */ (1 << 6) + (1 << 5) + (0 << 4) + (1 << 3) + (1 << 2) + (1 << 1) + (1 << 0),
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);

                o.uv_m = TRANSFORM_TEX(v.uv0, _MaskTex);
                o.uv_ab.xy = TRANSFORM_TEX(v.uv0, _NoiseTex); // base
                o.uv_ab.zw = TRANSFORM_TEX(float2(v.uv0.y, 1.0 - v.uv0.x), _NoiseTex); // +90
                o.uv_cd.xy = TRANSFORM_TEX(float2(1.0 - v.uv0.x, 1.0 - v.uv0.y), _NoiseTex); // 180
                o.uv_cd.zw = TRANSFORM_TEX(float2(1.0 - v.uv0.y, v.uv0.x), _NoiseTex); // -90

                o.center = v.uv2.x;

                int segment_idx = (int) floor(v.uv1.x * 8);
                if (segment_idx >= 7) {
                    o.drop = 0; // semicolon, always on
                } else {
                    int digit_idx = (int) floor(v.uv1.y * 4);
                    int digit = (int) floor(_Digits.wzyx[digit_idx]);
                    o.drop = !(SEGMENTS[digit] & (1 << segment_idx));
                }

                UNITY_TRANSFER_FOG(o, o.vertex);
                return o; 
            }

            float h01(float x) {
                return x * x * (3.0 - 2.0 * x); // Cubic Hermite H01 interoplation
            }

            float4 frag (v2f i) : SV_Target
            {
                if (i.drop > 0.5) discard;

                float noise_a = tex2D(_NoiseTex, i.uv_ab.xy).r;
                float noise_b = tex2D(_NoiseTex, i.uv_ab.zw).r;
                float noise_c = tex2D(_NoiseTex, i.uv_cd.xy).r;
                float noise_d = tex2D(_NoiseTex, i.uv_cd.zw).r;
                float noise = 0;

                float time = _Time.y * _Speed;
                int blend_index = ((int) floor(time)) % 4;
                float time_frac = h01(frac(time));
                if (blend_index == 0) {
                    noise = lerp(noise_a, noise_b, time_frac);
                } else if (blend_index == 1) {
                    noise = lerp(noise_b, noise_c, time_frac);
                } else if (blend_index == 2) {
                    noise = lerp(noise_c, noise_d, time_frac);
                } else {
                    noise = lerp(noise_d, noise_a, time_frac);
                }

                float mask = tex2D(_MaskTex, i.uv_m).r;
                float3 color = smoothstep(_ColorRemap.x, _ColorRemap.y, noise.rrr);
                color = lerp(_ColorRemap.z, _ColorRemap.w, color);
                color *= _Color.rgb * mask;

                float edge = smoothstep(_EdgeRemap.x, _EdgeRemap.y, noise);
                edge = lerp(_EdgeRemap.z, _EdgeRemap.w, edge);
                edge += i.center;
                if (edge < _EdgeTH) discard;
                
                UNITY_APPLY_FOG(i.fogCoord, color);
                return float4(color.r, color.g, color.b, 1);
            }
            ENDCG
        }
    }
}
