Shader "Advanced/Blinking_Atmosphere" {
Properties {
    _MainTex ("Base texture", 2D) = "white" {}
    _FadeOutDistNear ("Near fadeout dist", Float) = 10
    _FadeOutDistFar ("Far fadeout dist", Float) = 10000
    _Multiplier ("Color multiplier", Float) = 1
    _Bias ("Bias", Float) = 0
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
        float _FadeOutDistNear;
        float _FadeOutDistFar;
        float _Multiplier;
        float _Bias;
        float _TimeOnDuration;
        float _TimeOffDuration;
        float _BlinkingTimeOffsScale;
        float _SizeGrowStartDist;
        float _SizeGrowEndDist;
        float _MaxGrowSize;
        float _NoiseAmount;
        fixed4 _Color;

        struct appdata {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float4 color : COLOR;
            float2 texcoord : TEXCOORD0;
        };

        struct v2f {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
            fixed4 col : TEXCOORD1;
        };

        v2f vert(appdata v) {
            v2f o;
            float3 n = normalize(v.normal);
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

            float grow = min(max(dist - _SizeGrowStartDist, 0.0) / max(_SizeGrowEndDist, 0.0001), 1.0);
            float4 mdlPos = v.vertex;
            mdlPos.xyz += (grow * grow * _MaxGrowSize * v.color.a) * n;

            float n2 = nfade * nfade;
            o.pos = UnityObjectToClipPos(mdlPos);
            o.uv = v.texcoord;
            o.col = (n2 * n2) * (ffade * ffade) * _Color * _Multiplier * (wave + _Bias);
            return o;
        }

        fixed4 frag(v2f i) : SV_Target {
            return tex2D(_MainTex, i.uv) * i.col;
        }
        ENDCG
    }
}
}
