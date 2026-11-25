Shader "Advanced/Animation_Texture" {
Properties {
    _MainTex ("Base (RGB)", 2D) = "white" {}
    _NumTexTiles ("Num tex tiles", Vector) = (4,4,0,0)
    _ReplaySpeed ("Replay speed - FPS", Float) = 4
    _Randomize ("Randomize", Float) = 0
    _Color ("Color", Color) = (1,1,1,1)
    _Fade ("Fade", Float) = 1
}
SubShader {
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
        float4 _NumTexTiles;
        float _ReplaySpeed;
        fixed4 _Color;
        float _Fade;

        struct appdata {
            float4 vertex : POSITION;
            float4 color : COLOR;
            float2 texcoord : TEXCOORD0;
        };

        struct v2f {
            float4 pos : SV_POSITION;
            float4 uv : TEXCOORD0;
            fixed4 col : COLOR;
        };

        v2f vert(appdata v) {
            v2f o;
            float time = (v.color.a * 60.0 + _Time.y) * _ReplaySpeed;
            float itime = floor(time);
            float ntime = itime + 1.0;
            float f = time - itime;

            float2 tiles = max(_NumTexTiles.xy, float2(1.0, 1.0));
            float4 tile;
            tile.xy = float2(itime, floor(itime / tiles.x));
            tile.zw = float2(ntime, floor(ntime / tiles.x));
            tile = fmod(tile, tiles.xyxy);

            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv = (v.texcoord.xyxy + tile) / tiles.xyxy;
            o.col = fixed4(_Color.rgb * v.color.rgb, f);
            return o;
        }

        fixed4 frag(v2f i) : SV_Target {
            fixed4 c0 = tex2D(_MainTex, i.uv.xy);
            if (_Fade > 0.0) {
                fixed4 c1 = tex2D(_MainTex, i.uv.zw);
                return lerp(c0, c1, i.col.a) * i.col;
            }
            return c0 * i.col;
        }
        ENDCG
    }
}
}
