Shader "Advanced/Skybox" {
Properties {
    _MainTex ("Base layer (RGB)", 2D) = "white" {}
    _DetailTex ("2nd layer (RGB)", 2D) = "white" {}
    _ScrollX ("Base layer Scroll speed X", Float) = 1
    _ScrollY ("Base layer Scroll speed Y", Float) = 0
    _Scroll2X ("2nd layer Scroll speed X", Float) = 1
    _Scroll2Y ("2nd layer Scroll speed Y", Float) = 0
    _AMultiplier ("Layer Multiplier", Float) = 0.5
}
SubShader {
    LOD 100
    Tags { "Queue"="Geometry+10" "RenderType"="Opaque" }
    Pass {
        Tags { "Queue"="Geometry+10" "RenderType"="Opaque" }
        ZWrite Off
        Fog { Mode Off }
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        float4 _MainTex_ST;
        sampler2D _DetailTex;
        float4 _DetailTex_ST;
        float _ScrollX, _ScrollY, _Scroll2X, _Scroll2Y;
        float _AMultiplier;

        struct appdata {
            float4 vertex : POSITION;
            float2 texcoord : TEXCOORD0;
        };

        struct v2f {
            float4 pos : SV_POSITION;
            float2 uv0 : TEXCOORD0;
            float2 uv1 : TEXCOORD1;
            fixed4 col : TEXCOORD2;
        };

        v2f vert(appdata v) {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv0 = TRANSFORM_TEX(v.texcoord, _MainTex) + frac(float2(_ScrollX, _ScrollY) * _Time.xy);
            o.uv1 = TRANSFORM_TEX(v.texcoord, _DetailTex) + frac(float2(_Scroll2X, _Scroll2Y) * _Time.xy);
            o.col = _AMultiplier;
            return o;
        }

        fixed4 frag(v2f i) : SV_Target {
            return tex2D(_MainTex, i.uv0) * tex2D(_DetailTex, i.uv1) * i.col;
        }
        ENDCG
    }
}
}
