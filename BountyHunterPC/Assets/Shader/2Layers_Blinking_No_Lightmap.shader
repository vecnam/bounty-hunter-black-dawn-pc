Shader "Advanced/2_Layer_Blinking_No_Lightmap" {
Properties {
    _TintColor ("Tint Color", Color) = (1,1,1,1)
    _texBase ("MainTex", 2D) = "white" {}
    _tex2 ("Texture2", 2D) = "white" {}
    _FadeOutDistNear ("Near fadeout dist", Float) = 0
    _FadeOutDistFar ("Far fadeout dist", Float) = 1000
    _Multiplier ("Color multiplier", Float) = 1
    _Bias ("Bias", Float) = 0.1
    _TimeOnDuration ("ON duration", Float) = 0.5
    _TimeOffDuration ("OFF duration", Float) = 0.5
    _BlinkingTimeOffsScale ("Blinking time offset scale (seconds)", Float) = 5
    _SizeGrowStartDist ("Size grow start dist", Float) = 5
    _SizeGrowEndDist ("Size grow end dist", Float) = 50
    _MaxGrowSize ("Max grow size", Float) = 2.5
    _NoiseAmount ("Noise amount (when zero, pulse wave is used)", Range(0,0.5)) = 0
    _Color ("Color", Color) = (1,1,1,1)
}
SubShader {
    Pass {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"

        sampler2D _texBase;
        float4 _texBase_ST;
        sampler2D _tex2;
        float4 _tex2_ST;
        float _FadeOutDistNear;
        float _FadeOutDistFar;
        float _Multiplier;
        float _Bias;
        float _TimeOnDuration;
        float _TimeOffDuration;
        float _BlinkingTimeOffsScale;
        float _NoiseAmount;
        fixed4 _Color;

        struct appdata {
            float4 vertex : POSITION;
            float4 color : COLOR;
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
            o.uv.xy = TRANSFORM_TEX(v.texcoord, _texBase);
            o.uv.zw = TRANSFORM_TEX(v.texcoord, _tex2);

            float t = _Time.y + _BlinkingTimeOffsScale * v.color.b;
            float3 viewPos = mul(UNITY_MATRIX_MV, v.vertex).xyz;
            float dist = length(viewPos);
            float nfade = saturate(dist / max(_FadeOutDistNear, 0.0001));
            float ffade = 1.0 - saturate(max(dist - _FadeOutDistFar, 0.0) * 0.2);

            float period = max(_TimeOnDuration + _TimeOffDuration, 0.0001);
            float tm = fmod(t, period);
            float t1 = saturate(tm / max(_TimeOnDuration * 0.25, 0.0001));
            float edge0 = _TimeOnDuration * 0.75;
            float t2 = saturate((tm - edge0) / max(_TimeOnDuration - edge0, 0.0001));
            float pulse = (t1 * t1 * (3.0 - 2.0 * t1)) * (1.0 - (t2 * t2 * (3.0 - 2.0 * t2)));

            float phase = t * (6.28319 / max(_TimeOnDuration, 0.0001));
            float noise = _NoiseAmount * (sin(phase) * (0.5 * cos(phase * 0.6366 + 56.7272) + 0.5)) + (1.0 - _NoiseAmount);
            float wave = (_NoiseAmount < 0.01) ? pulse : noise;

            float n2 = nfade * nfade;
            o.col = (n2 * n2) * (ffade * ffade) * _Color * _Multiplier * (wave + _Bias);
            return o;
        }

        fixed4 frag(v2f i) : SV_Target {
            return tex2D(_texBase, i.uv.xy) + tex2D(_tex2, i.uv.zw) * i.col;
        }
        ENDCG
    }
}
}
