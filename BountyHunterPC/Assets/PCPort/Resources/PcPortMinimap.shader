Shader "PcPort/Minimap"
{
    Properties
    {
        _MainTex ("Map Texture", 2D) = "black" {}
        _MapCenter ("Map Center UV", Vector) = (0.5, 0.5, 0, 0)
        _MapRight ("Map Right UV Step", Vector) = (0.2, 0, 0, 0)
        _MapUp ("Map Up UV Step", Vector) = (0, 0.2, 0, 0)
        _EdgeAA ("Edge AA", Float) = 0.012
    }
    SubShader
    {
        LOD 100
        Tags { "Queue"="Transparent-2" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Pass
        {
            Tags { "Queue"="Transparent-2" "IgnoreProjector"="True" "RenderType"="Transparent" }
            ZWrite Off
            Cull Off
            Fog { Mode Off }
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MapCenter;
            float4 _MapRight;
            float4 _MapUp;
            float _EdgeAA;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 col : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.col = v.color;
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;
                float r = length(p);
                float aa = max(_EdgeAA, 0.004);

                float outerMask = 1.0 - smoothstep(1.0 - aa, 1.0, r);
                if (outerMask <= 0.001)
                    discard;

                float2 mapUV = _MapCenter.xy + p.x * _MapRight.xy + p.y * _MapUp.xy;
                float2 inBounds = smoothstep(0.0, 0.012, mapUV) * (1.0 - smoothstep(0.988, 1.0, mapUV));
                float boundsMask = inBounds.x * inBounds.y;

                fixed3 darkBg = fixed3(0.02, 0.045, 0.07);
                fixed3 teal = fixed3(0.64, 1.0, 0.91);

                fixed3 mapCol = tex2D(_MainTex, saturate(mapUV)).rgb;
                fixed3 col = lerp(darkBg, mapCol, boundsMask);

                // Soft vignette at the edges so the icons stand out
                float vignette = smoothstep(0.62, 0.95, r);
                col = lerp(col, darkBg, vignette * 0.42);

                // View cone pointing up (camera direction)
                float tanAng = abs(p.x) / max(p.y, 0.0001);
                float coneAng = step(0.0, p.y) * (1.0 - smoothstep(0.54, 0.66, tanAng));
                float coneRad = smoothstep(0.05, 0.14, r) * (1.0 - smoothstep(0.28, 0.88, r));
                col = lerp(col, teal, coneAng * coneRad * 0.22);

                // Turquoise ring + dark inner line, matching PcPortHud
                float ringMask = smoothstep(0.945 - aa, 0.945 + aa, r) * (1.0 - smoothstep(0.982 - aa, 0.982 + aa, r));
                float innerBevel = smoothstep(0.928 - aa, 0.928 + aa, r) * (1.0 - smoothstep(0.945 - aa, 0.945 + aa, r));
                col = lerp(col, darkBg, innerBevel * 0.85);
                col = lerp(col, teal, ringMask * 0.92);

                float alpha = lerp(0.86, 0.96, max(ringMask, innerBevel)) * outerMask * i.col.a;
                return fixed4(col * i.col.rgb, alpha);
            }
            ENDCG
        }
    }
}
