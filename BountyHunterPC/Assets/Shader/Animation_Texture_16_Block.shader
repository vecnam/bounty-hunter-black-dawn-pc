Shader "Advanced/Animation_Texture_16_Block" {
Properties {
    _MainTex ("Base (RGB)", 2D) = "white" {}
    _NumTexTiles ("Num tex tiles", Vector) = (1,4,0,0)
    _ReplaySpeed ("Replay speed - FPS", Float) = 4
    _Color ("Color", Color) = (1,1,1,1)
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
            float2 blockIdx = floor(v.texcoord / 0.25);

            float4 tile;
            tile.xy = float2(itime, floor(itime / tiles.x));
            tile.zw = float2(ntime, floor(ntime / tiles.x));
            tile = fmod(tile, tiles.xyxy);

            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv = ((v.texcoord.xyxy - blockIdx.xyxy * 0.25) + tile * 0.25) / tiles.xyxy + blockIdx.xyxy * 0.25;
            o.col = fixed4(_Color.rgb * v.color.rgb, f);
            return o;
        }

        fixed4 frag(v2f i) : SV_Target {
            return tex2D(_MainTex, i.uv.xy) * i.col;
        }
        ENDCG
    }
}
}
