Shader "Hidden/KawaProbesVisualizer"
{
    Properties
    {
        kawa_SHAr ("kawa_SHAr", Vector) = (0,0,0,0)
        kawa_SHAg ("kawa_SHAg", Vector) = (0,0,0,0)
        kawa_SHAb ("kawa_SHAb", Vector) = (0,0,0,0)
        kawa_SHBr ("kawa_SHBr", Vector) = (0,0,0,0)
        kawa_SHBg ("kawa_SHBg", Vector) = (0,0,0,0)
        kawa_SHBb ("kawa_SHBb", Vector) = (0,0,0,0)
        kawa_SHC ("kawa_SHC", Vector) = (0,0,0,0)
        kawa_Range ("kawa_Range", Vector) = (0,1,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            
            // SH lighting environment
            uniform half4 kawa_SHAr;
            uniform half4 kawa_SHAg;
            uniform half4 kawa_SHAb;
            uniform half4 kawa_SHBr;
            uniform half4 kawa_SHBg;
            uniform half4 kawa_SHBb;
            uniform half4 kawa_SHC;
            uniform half4 kawa_Range;

            struct v2f
            {
                float3 normal : NORMAL;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = normalize(UnityObjectToWorldNormal(v.normal));
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                unity_SHAr = kawa_SHAr;
                unity_SHAg = kawa_SHAg;
                unity_SHAb = kawa_SHAb;
                unity_SHBr = kawa_SHBr;
                unity_SHBg = kawa_SHBg;
                unity_SHBb = kawa_SHBb;
                unity_SHC = kawa_SHC;

                half4 color;
                half4 normal;
                normal.xyz = i.normal;
                normal.w = 1.0f;
                color.rgb = ShadeSH9(normal);
                color.rgb = (color.rgb - kawa_Range.x) / (kawa_Range.y - kawa_Range.x);
                color.a = 1.0f;
                return color;
            }
            ENDCG
        }
    }
}
