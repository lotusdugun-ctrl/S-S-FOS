// The canvas emulation's one draw shader (see Assets/Scripts/App/UnityBackend.cs and the
// reference implementation unity/Headless/CpuBackend.cs). Everything is premultiplied; the
// composite mode is the material's blend state, the clip is the stencil.
//
// Vertex layout (UnityBackend.GVert): POSITION xyz (target pixels, y down), COLOR straight rgba,
// TEXCOORD0 (u, v, kind, row), TEXCOORD1 (p0..p3), TEXCOORD2 (p4, p5).
// kind: 0 solid, 1 linear, 2 radial, 3 image, 4 pattern, 5 mask (PaintKind), and backend-only
// 6 pixels (straight RGBA texture, uv as stored), 7 saturation (reads _DstTex),
// 8 glyphs (alpha of _MainTex, uv as stored, painted in the vertex colour).
Shader "Hidden/Sisifos/Paint"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _SrcBlend ("Src", Float) = 1
        _DstBlend ("Dst", Float) = 10
        _SrcBlendA ("SrcA", Float) = 1
        _DstBlendA ("DstA", Float) = 10
        _StencilRef ("Stencil Ref", Float) = 0
        _StencilComp ("Stencil Comp", Float) = 8
        _StencilPass ("Stencil Pass", Float) = 0
        _ColorMask ("Color Mask", Float) = 15
        _Repeat ("Repeat", Float) = 0
        _DstMode ("Dst Mode", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Cull Off
        ZWrite Off
        ZTest Always
        Lighting Off
        Fog { Mode Off }
        Blend [_SrcBlend] [_DstBlend], [_SrcBlendA] [_DstBlendA]
        ColorMask [_ColorMask]
        Stencil
        {
            Ref [_StencilRef]
            ReadMask 255
            WriteMask 255
            Comp [_StencilComp]
            Pass [_StencilPass]
            Fail Keep
            ZFail Keep
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _GradTex;
            sampler2D _DstTex;
            float4 _GradSize;    // x: texels per row, y: rows in the atlas
            float4 _TargetSize;  // w, h, 1/w, 1/h of the render target
            float _Repeat;
            float _DstMode;      // 1: multiply (reads the target copy in _DstTex)

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                // high precision for everything (COLOR can be lowp on GLES)
                float4 color : TEXCOORD0;
                float4 uv0 : TEXCOORD1;
                float4 uv1 : TEXCOORD2;
                float4 uv2 : TEXCOORD3; // xy: p4 p5, zw: target position 0..1, y down
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(float4(v.vertex.xy, 0.0, 1.0));
                o.color = v.color;
                o.uv0 = v.uv0;
                o.uv1 = v.uv1;
                o.uv2 = float4(v.uv2.xy, v.vertex.xy * _TargetSize.zw);
                return o;
            }

            // LinearGradient.T
            float LinearT(float2 p, float4 g)
            {
                float dx = g.z - g.x, dy = g.w - g.y;
                float l2 = dx * dx + dy * dy;
                if (l2 < 1e-12) return 0.0;
                return ((p.x - g.x) * dx + (p.y - g.y) * dy) / l2;
            }

            // RadialGradient.T: the two-point conical parameter; false where it paints nothing
            bool RadialT(float2 p, float4 g, float2 g2, out float t)
            {
                float x0 = g.x, y0 = g.y, r0 = g.z, x1 = g.w, y1 = g2.x, r1 = g2.y;
                float cdx = x1 - x0, cdy = y1 - y0, dr = r1 - r0;
                float pdx = p.x - x0, pdy = p.y - y0;
                float a = cdx * cdx + cdy * cdy - dr * dr;
                float b = pdx * cdx + pdy * cdy + r0 * dr;
                float c = pdx * pdx + pdy * pdy - r0 * r0;
                t = 0.0;
                if (abs(a) < 1e-9)
                {
                    if (abs(b) < 1e-12) return false;
                    float t0 = c / (2.0 * b);
                    t = t0;
                    return r0 + t0 * dr >= 0.0;
                }
                float disc = b * b - a * c;
                if (disc < 0.0) return false;
                float sq = sqrt(disc);
                float ta = (b + sq) / a, tb = (b - sq) / a;
                float hi = max(ta, tb), lo = min(ta, tb);
                if (r0 + hi * dr >= 0.0) { t = hi; return true; }
                if (r0 + lo * dr >= 0.0) { t = lo; return true; }
                return false;
            }

            float Lum(float3 c) { return 0.3 * c.r + 0.59 * c.g + 0.11 * c.b; }
            float Sat(float3 c) { return max(c.r, max(c.g, c.b)) - min(c.r, min(c.g, c.b)); }

            float3 SetLum(float3 c, float l)
            {
                float d = l - Lum(c);
                c += d;
                float L = Lum(c);
                float n = min(c.r, min(c.g, c.b)), x = max(c.r, max(c.g, c.b));
                if (n < 0.0) c = L + (c - L) * L / (L - n);
                if (x > 1.0) c = L + (c - L) * (1.0 - L) / (x - L);
                return c;
            }

            float3 SetSat(float3 c, float s)
            {
                float mx = max(c.r, max(c.g, c.b)), mn = min(c.r, min(c.g, c.b));
                float span = mx - mn;
                return span > 0.0 ? (c - mn) * s / span : float3(0.0, 0.0, 0.0);
            }

            float4 frag(v2f i) : SV_Target
            {
                float kind = floor(i.uv0.z + 0.5);
                float4 c = i.color;
                float4 s;
                if (kind < 0.5)
                {
                    s = float4(c.rgb * c.a, c.a);
                }
                else if (kind < 2.5)
                {
                    float t;
                    if (kind < 1.5) t = LinearT(i.uv0.xy, i.uv1);
                    else if (!RadialT(i.uv0.xy, i.uv1, i.uv2.xy, t)) discard;
                    float row = floor(i.uv0.w + 0.5);
                    float2 g = float2((saturate(t) * (_GradSize.x - 1.0) + 0.5) / _GradSize.x, (row + 0.5) / _GradSize.y);
                    s = tex2Dlod(_GradTex, float4(g, 0.0, 0.0)) * c.a;
                }
                else if (kind < 5.5)
                {
                    float2 uv = i.uv0.xy;
                    if ((kind > 3.5 && kind < 4.5) || _Repeat > 0.5) uv = frac(uv);
                    // render textures are sampled in Unity's convention (v up); frame uvs are y down
                    float4 tx = tex2Dlod(_MainTex, float4(uv.x, 1.0 - uv.y, 0.0, 0.0));
                    if (kind > 4.5)
                    {
                        float m = tx.a * c.a;
                        s = float4(c.rgb * m, m);
                    }
                    else s = tx * c.a;
                }
                else if (kind < 6.5)
                {
                    float4 tx = tex2Dlod(_MainTex, float4(i.uv0.xy, 0.0, 0.0));
                    s = float4(tx.rgb * tx.a, tx.a);
                }
                else if (kind < 7.5)
                {
                    // the "saturation" blend over the copy of the target: hue and lightness from
                    // the backdrop, saturation from the colour, mixed by the colour's alpha
                    float4 d = tex2Dlod(_DstTex, float4(i.uv2.z, 1.0 - i.uv2.w, 0.0, 0.0));
                    if (d.a <= 0.0) return d;
                    float3 o = d.rgb / d.a;
                    float3 r = SetLum(SetSat(o, Sat(c.rgb)), Lum(o));
                    return float4((o + (r - o) * c.a) * d.a, d.a);
                }
                else
                {
                    float m = tex2Dlod(_MainTex, float4(i.uv0.xy, 0.0, 0.0)).a * c.a;
                    s = float4(c.rgb * m, m);
                }
                if (_DstMode > 0.5)
                {
                    // multiply, with the blend state set to copy
                    float4 d = tex2Dlod(_DstTex, float4(i.uv2.z, 1.0 - i.uv2.w, 0.0, 0.0));
                    s = float4(s.rgb * (1.0 - d.a) + d.rgb * (1.0 - s.a) + s.rgb * d.rgb, s.a + d.a - s.a * d.a);
                }
                return s;
            }
            ENDCG
        }
    }
    Fallback Off
}
