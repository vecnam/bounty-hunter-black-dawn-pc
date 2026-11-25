Shader "Advanced/Scroll Single AlphaBlended_No_Lightmap" {
Properties {
    _MainTex ("2nd layer (RGB)", 2D) = "white" {}
    _ScrollX ("2nd layer Scroll speed X", Float) = 1
    _ScrollY ("2nd layer Scroll speed Y", Float) = 0
    _SineAmplX ("2nd layer sine amplitude X", Float) = 0.5
    _SineAmplY ("2nd layer sine amplitude Y", Float) = 0.5
    _SineFreqX ("2nd layer sine freq X", Float) = 10
    _SineFreqY ("2nd layer sine freq Y", Float) = 10
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
        float _ScrollX, _ScrollY;
        float _SineAmplX, _SineAmplY, _SineFreqX, _SineFreqY;
        float _MMultiplier;

        struct appdata {
            float4 vertex : POSITION;
            float2 texcoord : TEXCOORD0;
        };

        struct v2f {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
            fixed4 col : TEXCOORD1;
        };

        v2f vert(appdata v) {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv = TRANSFORM_TEX(v.texcoord, _MainTex) + frac(float2(_ScrollX, _ScrollY) * _Time.xy);
            o.uv.x += sin(_Time.x * _SineFreqX) * _SineAmplX;
            o.uv.y += sin(_Time.x * _SineFreqY) * _SineAmplY;
            o.col = _MMultiplier;
            return o;
        }

        fixed4 frag(v2f i) : SV_Target {
            return tex2D(_MainTex, i.uv) * i.col;
        }
        ENDCG
    }
}
}
