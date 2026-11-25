Shader "iPhone/UnityLightMapBlend" {
Properties {
    _MainTex ("Texture1 (RGB)", 2D) = "white" {}
    _Tex2 ("Texture2 (RGB)", 2D) = "white" {}
    _BlendMap ("Blend Mask", 2D) = "white" {}
}
SubShader {
    Tags { "RenderType"="Opaque" }
    Pass {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        float4 _MainTex_ST;
        sampler2D _Tex2;
        sampler2D _BlendMap;

        struct appdata {
            float4 vertex : POSITION;
            float4 color : COLOR;
            float2 texcoord : TEXCOORD0;
            float2 texcoord1 : TEXCOORD1;
        };

        struct v2f {
            float4 pos : SV_POSITION;
            float2 uv0 : TEXCOORD0;
            float2 uv1 : TEXCOORD1;
        #ifdef LIGHTMAP_ON
            float2 uv2 : TEXCOORD2;
        #endif
        };

        v2f vert(appdata v) {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv0 = TRANSFORM_TEX(v.texcoord, _MainTex);
            o.uv1 = v.color.xy;
        #ifdef LIGHTMAP_ON
            o.uv2 = v.texcoord1 * unity_LightmapST.xy + unity_LightmapST.zw;
        #endif
            return o;
        }

        fixed4 frag(v2f i) : SV_Target {
            fixed4 c1 = tex2D(_MainTex, i.uv0);
            fixed4 c2 = tex2D(_Tex2, i.uv0);
            fixed4 bm = tex2D(_BlendMap, i.uv1);
            fixed3 rgb = lerp(c2.rgb, c1.rgb, bm.a);
        #ifdef LIGHTMAP_ON
            rgb *= 2.0 * UNITY_SAMPLE_TEX2D(unity_Lightmap, i.uv2).rgb;
        #endif
            return fixed4(rgb, c1.a);
        }
        ENDCG
    }
}
}
