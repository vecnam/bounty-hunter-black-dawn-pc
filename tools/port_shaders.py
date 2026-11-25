"""Ports the 14 NGUI shaders to HLSL, removes the .asset shader copies in AssetBundles and repoints materials/built-ins.

Usage:
  python -I -X utf8 tools/port_shaders.py             apply to BountyHunterPC/Assets (idempotent)
  python -I -X utf8 tools/port_shaders.py --selftest  run the unit tests
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
ASSETS = ROOT / "BountyHunterPC" / "Assets"
EXPORT_SHADERS = ROOT / "ripped" / "ExportedProject" / "Assets" / "Shader"

META_TMPL = """fileFormatVersion: 2
guid: {guid}
timeCreated: 1795212155
licenseType: Free
ShaderImporter:
  name:
  userData:
"""

NGUI_SHADERS = {
    "GUI - Text Shader (AlphaClip)": """Shader "GUI/Text Shader (AlphaClip)" {
Properties {
    _MainTex ("Alpha (A)", 2D) = "white" {}
}
SubShader {
    LOD 200
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    Pass {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off
        Fog { Mode Off }
        Blend SrcAlpha OneMinusSrcAlpha
        Offset -1, -1
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_ST;
        struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };
        struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; float2 uv : TEXCOORD0; float2 worldPos : TEXCOORD1; };
        v2f vert(appdata v) {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.col = v.color;
            o.uv = v.texcoord;
            o.worldPos = v.vertex.xy * _MainTex_ST.xy + _MainTex_ST.zw;
            return o;
        }
        fixed4 frag(v2f i) : SV_Target {
            fixed4 col = i.col;
            col.a *= tex2D(_MainTex, i.uv).a;
            float2 f = abs(i.worldPos);
            if (1.0 - max(f.x, f.y) < 0.0) col.a = 0.0;
            return col;
        }
        ENDCG
    }
}
}
""",
    "GUI - Text Shader (SoftClip)": """Shader "GUI/Text Shader (SoftClip)" {
Properties {
    _MainTex ("Alpha (A)", 2D) = "white" {}
}
SubShader {
    LOD 200
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    Pass {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off
        Fog { Mode Off }
        Blend SrcAlpha OneMinusSrcAlpha
        Offset -1, -1
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_ST;
        float2 _ClipSharpness;
        struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };
        struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; float2 uv : TEXCOORD0; float2 worldPos : TEXCOORD1; };
        v2f vert(appdata v) {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.col = v.color;
            o.uv = v.texcoord;
            o.worldPos = v.vertex.xy * _MainTex_ST.xy + _MainTex_ST.zw;
            return o;
        }
        fixed4 frag(v2f i) : SV_Target {
            fixed4 col = i.col;
            col.a *= tex2D(_MainTex, i.uv).a;
            float2 f = (1.0 - abs(i.worldPos)) * _ClipSharpness;
            col.a *= clamp(min(f.x, f.y), 0.0, 1.0);
            return col;
        }
        ENDCG
    }
}
}
""",
    "Unlit - Dynamic Font": """Shader "Unlit/Dynamic Font" {
Properties {
    _MainTex ("Alpha (A)", 2D) = "white" {}
}
SubShader {
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    Pass {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off
        Fog { Mode Off }
        Blend SrcAlpha OneMinusSrcAlpha
        Offset -1, -1
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_ST;
        struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };
        struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; float2 uv : TEXCOORD0; };
        v2f vert(appdata v) {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.col = v.color;
            o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
            return o;
        }
        fixed4 frag(v2f i) : SV_Target {
            fixed4 col = i.col;
            col.a *= tex2D(_MainTex, i.uv).a;
            return col;
        }
        ENDCG
    }
}
}
""",
    "Unlit - Dynamic Font (AlphaClip)": """Shader "Unlit/Dynamic Font (AlphaClip)" {
Properties {
    _MainTex ("Alpha (A)", 2D) = "white" {}
}
SubShader {
    LOD 200
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    Pass {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off
        Fog { Mode Off }
        Blend SrcAlpha OneMinusSrcAlpha
        Offset -1, -1
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_ST;
        struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };
        struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; float2 uv : TEXCOORD0; float2 worldPos : TEXCOORD1; };
        v2f vert(appdata v) {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.col = v.color;
            o.uv = v.texcoord;
            o.worldPos = v.vertex.xy * _MainTex_ST.xy + _MainTex_ST.zw;
            return o;
        }
        fixed4 frag(v2f i) : SV_Target {
            fixed4 col = i.col;
            col.a *= tex2D(_MainTex, i.uv).a;
            float2 f = abs(i.worldPos);
            if (1.0 - max(f.x, f.y) < 0.0) col.a = 0.0;
            return col;
        }
        ENDCG
    }
}
}
""",
    "Unlit - Dynamic Font (SoftClip)": """Shader "Unlit/Dynamic Font (SoftClip)" {
Properties {
    _MainTex ("Alpha (A)", 2D) = "white" {}
}
SubShader {
    LOD 200
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    Pass {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off
        Fog { Mode Off }
        Blend SrcAlpha OneMinusSrcAlpha
        Offset -1, -1
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_ST;
        float2 _ClipSharpness;
        struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };
        struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; float2 uv : TEXCOORD0; float2 worldPos : TEXCOORD1; };
        v2f vert(appdata v) {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.col = v.color;
            o.uv = v.texcoord;
            o.worldPos = v.vertex.xy * _MainTex_ST.xy + _MainTex_ST.zw;
            return o;
        }
        fixed4 frag(v2f i) : SV_Target {
            fixed4 col = i.col;
            col.a *= tex2D(_MainTex, i.uv).a;
            float2 f = (1.0 - abs(i.worldPos)) * _ClipSharpness;
            col.a *= clamp(min(f.x, f.y), 0.0, 1.0);
            return col;
        }
        ENDCG
    }
}
}
""",
    "Unlit - Masked Colored": """Shader "Unlit/Masked Colored" {
Properties {
    _MainTex ("Base (RGB) Mask (A)", 2D) = "white" {}
    _Color ("Tint Color", Color) = (1,1,1,1)
}
SubShader {
    LOD 200
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    Pass {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off
        Fog { Mode Off }
        ColorMask RGB
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_ST;
        fixed4 _Color;
        struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };
        struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; float2 uv : TEXCOORD0; };
        v2f vert(appdata v) {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.col = v.color;
            o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
            return o;
        }
        fixed4 frag(v2f i) : SV_Target {
            fixed4 col = tex2D(_MainTex, i.uv) * i.col;
            col.rgb = lerp(col.rgb, col.rgb * _Color.rgb, col.a);
            return col;
        }
        ENDCG
    }
}
}
""",
    "Unlit - Premultiplied Colored": """Shader "Unlit/Premultiplied Colored" {
Properties {
    _MainTex ("Base (RGB), Alpha (A)", 2D) = "white" {}
}
SubShader {
    LOD 200
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    Pass {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off
        Fog { Mode Off }
        Blend One OneMinusSrcAlpha
        ColorMask RGB
        Offset -1, -1
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };
        struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; float2 uv : TEXCOORD0; };
        v2f vert(appdata v) {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.col = v.color;
            o.uv = v.texcoord;
            return o;
        }
        fixed4 frag(v2f i) : SV_Target {
            return tex2D(_MainTex, i.uv) * i.col;
        }
        ENDCG
    }
}
}
""",
    "Unlit - Premultiplied Colored (AlphaClip)": """Shader "Unlit/Premultiplied Colored (AlphaClip)" {
Properties {
    _MainTex ("Base (RGB), Alpha (A)", 2D) = "white" {}
}
SubShader {
    LOD 200
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    Pass {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off
        Fog { Mode Off }
        Blend One OneMinusSrcAlpha
        ColorMask RGB
        Offset -1, -1
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_ST;
        struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };
        struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; float2 uv : TEXCOORD0; float2 worldPos : TEXCOORD1; };
        v2f vert(appdata v) {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.col = v.color;
            o.uv = v.texcoord;
            o.worldPos = v.vertex.xy * _MainTex_ST.xy + _MainTex_ST.zw;
            return o;
        }
        fixed4 frag(v2f i) : SV_Target {
            fixed4 col = tex2D(_MainTex, i.uv) * i.col;
            float2 f = abs(i.worldPos);
            if (1.0 - max(f.x, f.y) < 0.0) col = fixed4(0, 0, 0, 0);
            return col;
        }
        ENDCG
    }
}
}
""",
    "Unlit - Premultiplied Colored (SoftClip)": """Shader "Unlit/Premultiplied Colored (SoftClip)" {
Properties {
    _MainTex ("Base (RGB), Alpha (A)", 2D) = "white" {}
}
SubShader {
    LOD 200
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    Pass {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off
        Fog { Mode Off }
        Blend One OneMinusSrcAlpha
        ColorMask RGB
        Offset -1, -1
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_ST;
        float2 _ClipSharpness;
        struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };
        struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; float2 uv : TEXCOORD0; float2 worldPos : TEXCOORD1; };
        v2f vert(appdata v) {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.col = v.color;
            o.uv = v.texcoord;
            o.worldPos = v.vertex.xy * _MainTex_ST.xy + _MainTex_ST.zw;
            return o;
        }
        fixed4 frag(v2f i) : SV_Target {
            fixed4 col = tex2D(_MainTex, i.uv) * i.col;
            float2 f = (1.0 - abs(i.worldPos)) * _ClipSharpness;
            float factor = clamp(min(f.x, f.y), 0.0, 1.0);
            col.rgb *= factor;
            col.a *= factor;
            return col;
        }
        ENDCG
    }
}
}
""",
    "Unlit - Transparent Colored (AlphaClip)": """Shader "Unlit/Transparent Colored (AlphaClip)" {
Properties {
    _MainTex ("Base (RGB), Alpha (A)", 2D) = "white" {}
}
SubShader {
    LOD 200
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    Pass {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off
        Fog { Mode Off }
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGB
        Offset -1, -1
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_ST;
        struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };
        struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; float2 uv : TEXCOORD0; float2 worldPos : TEXCOORD1; };
        v2f vert(appdata v) {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.col = v.color;
            o.uv = v.texcoord;
            o.worldPos = v.vertex.xy * _MainTex_ST.xy + _MainTex_ST.zw;
            return o;
        }
        fixed4 frag(v2f i) : SV_Target {
            fixed4 col = tex2D(_MainTex, i.uv) * i.col;
            float2 f = abs(i.worldPos);
            if (1.0 - max(f.x, f.y) < 0.0) col.a = 0.0;
            return col;
        }
        ENDCG
    }
}
}
""",
    "Unlit - Transparent Colored (SoftClip)": """Shader "Unlit/Transparent Colored (SoftClip)" {
Properties {
    _MainTex ("Base (RGB), Alpha (A)", 2D) = "white" {}
}
SubShader {
    LOD 200
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    Pass {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off
        Fog { Mode Off }
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGB
        Offset -1, -1
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_ST;
        float2 _ClipSharpness;
        struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };
        struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; float2 uv : TEXCOORD0; float2 worldPos : TEXCOORD1; };
        v2f vert(appdata v) {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.col = v.color;
            o.uv = v.texcoord;
            o.worldPos = v.vertex.xy * _MainTex_ST.xy + _MainTex_ST.zw;
            return o;
        }
        fixed4 frag(v2f i) : SV_Target {
            fixed4 col = tex2D(_MainTex, i.uv) * i.col;
            float2 f = (1.0 - abs(i.worldPos)) * _ClipSharpness;
            col.a *= clamp(min(f.x, f.y), 0.0, 1.0);
            return col;
        }
        ENDCG
    }
}
}
""",
    "Unlit - Transparent Colored (Packed)": """Shader "Unlit/Transparent Colored (Packed)" {
Properties {
    _MainTex ("Base (RGB), Alpha (A)", 2D) = "white" {}
}
SubShader {
    LOD 200
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    Pass {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off
        Fog { Mode Off }
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGB
        Offset -1, -1
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };
        struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; float2 uv : TEXCOORD0; };
        v2f vert(appdata v) {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.col = v.color;
            o.uv = v.texcoord;
            return o;
        }
        fixed4 frag(v2f i) : SV_Target {
            fixed4 mask = tex2D(_MainTex, i.uv);
            fixed4 mixed = saturate(ceil(i.col - 0.5));
            fixed4 col = saturate((mixed * 0.51 - i.col) / -0.49);
            mask *= mixed;
            col.a *= mask.r + mask.g + mask.b + mask.a;
            return col;
        }
        ENDCG
    }
}
}
""",
    "Unlit - Transparent Colored (Packed) (AlphaClip)": """Shader "Unlit/Transparent Colored (Packed) (AlphaClip)" {
Properties {
    _MainTex ("Base (RGB), Alpha (A)", 2D) = "white" {}
}
SubShader {
    LOD 200
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    Pass {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off
        Fog { Mode Off }
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGB
        Offset -1, -1
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_ST;
        struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };
        struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; float2 uv : TEXCOORD0; float2 worldPos : TEXCOORD1; };
        v2f vert(appdata v) {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.col = v.color;
            o.uv = v.texcoord;
            o.worldPos = v.vertex.xy * _MainTex_ST.xy + _MainTex_ST.zw;
            return o;
        }
        fixed4 frag(v2f i) : SV_Target {
            fixed4 mask = tex2D(_MainTex, i.uv);
            fixed4 mixed = saturate(ceil(i.col - 0.5));
            fixed4 col = saturate((mixed * 0.51 - i.col) / -0.49);
            float2 f = abs(i.worldPos);
            if (1.0 - max(f.x, f.y) < 0.0) col.a = 0.0;
            mask *= mixed;
            col.a *= mask.r + mask.g + mask.b + mask.a;
            return col;
        }
        ENDCG
    }
}
}
""",
    "Unlit - Transparent Colored (Packed) (SoftClip)": """Shader "Unlit/Transparent Colored (Packed) (SoftClip)" {
Properties {
    _MainTex ("Base (RGB), Alpha (A)", 2D) = "white" {}
}
SubShader {
    LOD 200
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    Pass {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off
        Fog { Mode Off }
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGB
        Offset -1, -1
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_ST;
        float2 _ClipSharpness;
        struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };
        struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; float2 uv : TEXCOORD0; float2 worldPos : TEXCOORD1; };
        v2f vert(appdata v) {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.col = v.color;
            o.uv = v.texcoord;
            o.worldPos = v.vertex.xy * _MainTex_ST.xy + _MainTex_ST.zw;
            return o;
        }
        fixed4 frag(v2f i) : SV_Target {
            fixed4 mask = tex2D(_MainTex, i.uv);
            fixed4 mixed = saturate(ceil(i.col - 0.5));
            fixed4 col = saturate((mixed * 0.51 - i.col) / -0.49);
            float2 f = (1.0 - abs(i.worldPos)) * _ClipSharpness;
            mask *= mixed;
            col.a *= clamp(min(f.x, f.y), 0.0, 1.0) * (mask.r + mask.g + mask.b + mask.a);
            return col;
        }
        ENDCG
    }
}
}
""",
}

BUILTIN_FIDS = {"7", "30", "200", "203"}
OLD_BUILTIN_GUID = "0000000000000000e000000000000000"
NEW_BUILTIN_GUID = "0000000000000000f000000000000000"

SHADER_REF_RE = re.compile(
    r"(m_Shader:\s*\{fileID:\s*)(-?\d+)(,\s*guid:\s*)([0-9a-f]+)(,\s*type:\s*)(\d+)(\})"
)


def read_guid(meta_path):
    m = re.search(r"guid:\s*([0-9a-f]+)", meta_path.read_text(encoding="utf-8", errors="ignore"))
    return m.group(1) if m else None


def rewrite_mat_text(text, guid_map):
    """Updates m_Shader in a material YAML using guid_map (old_guid -> new_guid) and the built-ins."""
    def repl(m):
        prefix, fid, mid1, guid, mid2, tp, suffix = m.groups()
        if guid == OLD_BUILTIN_GUID and fid in BUILTIN_FIDS:
            return f"{prefix}{fid}{mid1}{NEW_BUILTIN_GUID}{mid2}0{suffix}"
        if guid in guid_map:
            return f"{prefix}4800000{mid1}{guid_map[guid]}{mid2}3{suffix}"
        return m.group(0)

    return SHADER_REF_RE.sub(repl, text)


def canonical_stem(name):
    """Normalizes _0/_1/... suffixes and the alternative names of Foam_add to match Assets/Shader."""
    stem = re.sub(r"_\d+$", "", name)
    if stem.lower() == "foam_add":
        return "shader_1"
    return stem.lower()


def main():
    # 1. Convert the 14 NGUI shaders in Resources/shaders/
    ngui_dir = ASSETS / "Resources" / "shaders"
    ngui_guids = set()
    converted_ngui = 0
    for stem, src in NGUI_SHADERS.items():
        asset_p = ngui_dir / f"{stem}.asset"
        asset_meta = ngui_dir / f"{stem}.asset.meta"
        shader_p = ngui_dir / f"{stem}.shader"
        shader_meta = ngui_dir / f"{stem}.shader.meta"
        if asset_meta.exists():
            guid = read_guid(asset_meta)
            shader_p.write_text(src, encoding="utf-8", newline="\n")
            shader_meta.write_text(META_TMPL.format(guid=guid), encoding="utf-8", newline="\n")
            asset_p.unlink(missing_ok=True)
            asset_meta.unlink(missing_ok=True)
            converted_ngui += 1
        elif shader_meta.exists():
            guid = read_guid(shader_meta)
            shader_p.write_text(src, encoding="utf-8", newline="\n")
        else:
            raise RuntimeError(f"No .meta for NGUI shader: {stem}")
        ngui_guids.add(guid)

    # 2. Canonical map of every shader in Assets/Shader/ and Resources/shaders/
    canon = {}
    for sdir in (ASSETS / "Shader", ngui_dir):
        for sp in sdir.glob("*.shader"):
            guid = read_guid(sp.with_suffix(".shader.meta"))
            canon[sp.stem.lower()] = guid

    # 3. Map the old GUIDs (converted NGUI + every .asset in AssetBundles) to the canonical one
    guid_map = {g: g for g in ngui_guids}
    for sp in (ASSETS / "Shader").glob("*.shader"):
        g = read_guid(sp.with_suffix(".shader.meta"))
        guid_map[g] = g

    # The export's own Assets/Shader: AssetRipper gives assets different GUIDs on each export, while the .meta files of
    # our rewritten shaders (in git) keep the GUIDs of one past export, so materials are repointed by shader name.
    unmatched = []
    for meta in sorted(EXPORT_SHADERS.glob("*.meta")):
        target = canon.get(canonical_stem(meta.name.split(".")[0]))
        if target:
            guid_map[read_guid(meta)] = target
        else:
            unmatched.append(meta.name)
    if unmatched:
        raise RuntimeError(f"No rewritten shader in Assets/Shader for exported {unmatched}")

    ab_dir = ASSETS / "AssetBundles"
    ab_assets_removed = 0
    for p in sorted(ab_dir.rglob("*.asset")):
        head = p.read_text(encoding="utf-8", errors="ignore")[:300]
        if "Shader:" not in head and "m_ParsedForm:" not in head:
            continue
        meta = pathlib.Path(str(p) + ".meta")
        old_guid = read_guid(meta) if meta.exists() else None
        target = canon.get(canonical_stem(p.stem))
        if not target:
            raise RuntimeError(f"No canonical shader for {p}")
        if old_guid:
            guid_map[old_guid] = target
        p.unlink()
        if meta.exists():
            meta.unlink()
        ab_assets_removed += 1

    # Also remove duplicated .shader copies inside AssetBundles, repointing their GUIDs
    ab_shaders_removed = 0
    for p in sorted(ab_dir.rglob("*.shader")):
        meta = pathlib.Path(str(p) + ".meta")
        old_guid = read_guid(meta) if meta.exists() else None
        target = canon.get(canonical_stem(p.stem))
        if target and old_guid:
            guid_map[old_guid] = target
            p.unlink()
            if meta.exists():
                meta.unlink()
            ab_shaders_removed += 1

    # 4. Update every .mat
    mats_updated = 0
    for mat in ASSETS.rglob("*.mat"):
        raw = mat.read_bytes()
        text = raw.decode("utf-8", errors="ignore")
        new_text = rewrite_mat_text(text, guid_map)
        if new_text != text:
            mat.write_bytes(new_text.encode("utf-8"))
            mats_updated += 1

    print(
        f"NGUI converted: {converted_ngui}; "
        f"AssetBundle .asset removed: {ab_assets_removed}, .shader removed: {ab_shaders_removed}; "
        f"materials updated: {mats_updated}"
    )


def selftest():
    gmap = {"aaaa": "bbbb", "cccc": "cccc"}
    sample = (
        "  m_Shader: {fileID: 4800000, guid: aaaa, type: 2}\n"
        "  m_Shader: {fileID: 200, guid: 0000000000000000e000000000000000, type: 0}\n"
        "  m_Shader: {fileID: 4800000, guid: cccc, type: 2}\n"
    )
    out = rewrite_mat_text(sample, gmap)
    assert "guid: bbbb, type: 3" in out, out
    assert "fileID: 200, guid: 0000000000000000f000000000000000, type: 0" in out, out
    assert "guid: cccc, type: 3" in out, out
    print("selftest ok")


if __name__ == "__main__":
    if "--selftest" in sys.argv[1:]:
        selftest()
    else:
        main()
