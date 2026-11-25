Shader "Advanced/Scroll_2Layers" {
Properties {
    _MainTex ("Base layer (RGB)", 2D) = "white" {}
    _DetailTex ("2nd layer (RGB)", 2D) = "white" {}
    _ScrollX ("Base layer Scroll speed X", Float) = 1
    _ScrollY ("Base layer Scroll speed Y", Float) = 0
    _Scroll2X ("2nd layer Scroll speed X", Float) = 1
    _Scroll2Y ("2nd layer Scroll speed Y", Float) = 0
    _SineAmplX ("Base layer sine amplitude X", Float) = 0.5
    _SineAmplY ("Base layer sine amplitude Y", Float) = 0.5
    _SineFreqX ("Base layer sine freq X", Float) = 10
    _SineFreqY ("Base layer sine freq Y", Float) = 10
    _SineAmplX2 ("2nd layer sine amplitude X", Float) = 0.5
    _SineAmplY2 ("2nd layer sine amplitude Y", Float) = 0.5
    _SineFreqX2 ("2nd layer sine freq X", Float) = 10
    _SineFreqY2 ("2nd layer sine freq Y", Float) = 10
    _MMultiplier ("Layer Multiplier", Float) = 2
}
SubShader {
    LOD 100
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    Pass {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off
        Fog { Color (0,0,0,0) }
        Blend One One
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        float4 _MainTex_ST;
        sampler2D _DetailTex;
        float4 _DetailTex_ST;
        float _ScrollX, _ScrollY, _Scroll2X, _Scroll2Y;
        float _SineAmplX, _SineAmplY, _SineFreqX, _SineFreqY;
        float _SineAmplX2, _SineAmplY2, _SineFreqX2, _SineFreqY2;
        float _MMultiplier;

        struct appdata {
            float4 vertex : POSITION;
            float2 texcoord : TEXCOORD0;
        };

        struct v2f {
            float4 pos : SV_POSITION;
            float4 uv : TEXCOORD0;
            fixed4 col : TEXCOORD1;
        };

        v2f vert(appdata v) {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv.xy = TRANSFORM_TEX(v.texcoord, _MainTex) + frac(float2(_ScrollX, _ScrollY) * _Time.xy);
            o.uv.zw = TRANSFORM_TEX(v.texcoord, _DetailTex) + frac(float2(_Scroll2X, _Scroll2Y) * _Time.xy);
            o.uv.x += sin(_Time.x * _SineFreqX) * _SineAmplX;
            o.uv.y += sin(_Time.x * _SineFreqY) * _SineAmplY;
            o.uv.z += sin(_Time.x * _SineFreqX2) * _SineAmplX2;
            o.uv.w += sin(_Time.x * _SineFreqY2) * _SineAmplY2;
            o.col = _MMultiplier;
            return o;
        }

        fixed4 frag(v2f i) : SV_Target {
            return tex2D(_MainTex, i.uv.xy) * tex2D(_DetailTex, i.uv.zw) * i.col;
        }
        ENDCG
    }
}
}
