Shader "Advanced/LightmapWithBright" {
Properties {
    _MainTex ("Base (RGB) Gloss (A)", 2D) = "white" {}
    _SpecOffset ("Specular Offset from Camera", Vector) = (1,10,2,0)
    _SpecRange ("Specular Range", Float) = 20
    _SpecColor ("Specular Color", Color) = (0.5,0.5,0.5,1)
    _Shininess ("Shininess", Range(0.01,1)) = 0.078125
    _ScrollingSpeed ("Scrolling speed", Vector) = (0,0,0,0)
    _ColorOffset ("Color Offset", Float) = 1
}
SubShader {
    LOD 100
    Tags { "RenderType"="Opaque" }
    Pass {
        Tags { "LightMode"="ForwardBase" "RenderType"="Opaque" }
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        float4 _SpecOffset;
        float _SpecRange;
        fixed4 _SpecColor;
        float _Shininess;
        float4 _ScrollingSpeed;
        float _ColorOffset;

        struct appdata {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float2 texcoord : TEXCOORD0;
            float2 texcoord1 : TEXCOORD1;
        };

        struct v2f {
            float4 pos : SV_POSITION;
            float2 uv0 : TEXCOORD0;
        #ifdef LIGHTMAP_ON
            float2 uv1 : TEXCOORD1;
        #endif
            fixed3 spec : TEXCOORD2;
        };

        v2f vert(appdata v) {
            v2f o;
            float3 viewPos = mul(UNITY_MATRIX_MV, v.vertex).xyz - (_SpecOffset.xyz * float3(1.0, 1.0, -1.0));
            float3 n = normalize(mul((float3x3)UNITY_MATRIX_MV, normalize(v.normal)));
            float3 h = normalize(float3(0.0, 0.0, 1.0) + normalize(-viewPos));
            float atten = 1.0 - saturate(length(viewPos) / max(_SpecRange, 0.0001));
            o.spec = _SpecColor.rgb * pow(saturate(dot(n, h)), _Shininess * 128.0) * 2.0 * atten;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv0 = v.texcoord + frac(_ScrollingSpeed.xy * _Time.y);
        #ifdef LIGHTMAP_ON
            o.uv1 = v.texcoord1 * unity_LightmapST.xy + unity_LightmapST.zw;
        #endif
            return o;
        }

        fixed4 frag(v2f i) : SV_Target {
            fixed4 tex = tex2D(_MainTex, i.uv0);
            fixed3 rgb = tex.rgb + i.spec * tex.a;
        #ifdef LIGHTMAP_ON
            rgb *= (2.0 * UNITY_SAMPLE_TEX2D(unity_Lightmap, i.uv1).rgb) * _ColorOffset;
        #else
            rgb *= _ColorOffset;
        #endif
            return fixed4(rgb, tex.a);
        }
        ENDCG
    }
}
}
