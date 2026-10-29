// The finished frame, post-processed on its way to the screen (PostFx.cs): the bright parts
// bleed light (bloom), a low sun throws shafts through cloud and over the ridges (light
// shafts, a radial blur of the bright parts toward the sun), mist drifts low over the
// ground, rain falls in three depths, worked out per pixel, and a fine grain sits over all
// of it. Gamma space, as the rest of the project.
//
// Passes: 0 prefilter (quarter size, bright parts only) · 1 blur across · 2 blur down ·
// 3 light shafts · 4 composite · 5 brightened only (low quality).
Shader "Hidden/Sisifos/Post"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
    }
    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    sampler2D _RaysTex;
    // xy: one texel of the quarter-size buffers (uv)
    float4 _PostTexel;
    // xy: the sun in uv (y up); z: how much of it shows; w: bloom strength
    float4 _Sun;
    float4 _SunColor;
    // rgb: mist colour; a: how thick
    float4 _Fog;
    // x: rain amount; y: lean; z: time; w: grain
    float4 _Rain;
    // xy: output size in pixels; z: the horizon (fraction of height from the top)
    float4 _Screen;

    float hash12(float2 p)
    {
        float3 p3 = frac(float3(p.xyx) * 0.1031);
        p3 += dot(p3, p3.yzx + 33.33);
        return frac((p3.x + p3.y) * p3.z);
    }

    float3 hash32(float2 p)
    {
        float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
        p3 += dot(p3, p3.yxz + 33.33);
        return frac((p3.xxy + p3.yzz) * p3.zyx);
    }

    float vnoise(float2 p)
    {
        float2 i = floor(p);
        float2 f = frac(p);
        float2 u = f * f * (3.0 - 2.0 * f);
        float a = hash12(i);
        float b = hash12(i + float2(1, 0));
        float c = hash12(i + float2(0, 1));
        float d = hash12(i + float2(1, 1));
        return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
    }

    float fbm(float2 p)
    {
        float v = 0.0;
        float a = 0.5;
        for (int k = 0; k < 3; k++)
        {
            v += vnoise(p) * a;
            p = p * 2.03 + float2(17.1, 9.7);
            a *= 0.5;
        }
        return v / 0.875;
    }

    // one depth of rain: thin slanting streaks in a grid of tall cells, each cell holding
    // one drop or none. `px` in pixels, y up; `k` the depth's size.
    float rainLayer(float2 px, float k, float speed, float density, float seed)
    {
        float lean = _Rain.y;
        float2 q = float2(px.x - px.y * lean, px.y);
        float2 cell = float2(9.0, 120.0) * k;
        q.y += _Rain.z * speed * k;
        q.x += seed * 37.0;
        float2 id = floor(q / cell);
        float2 f = frac(q / cell);
        float3 h = hash32(id + seed * 13.1);
        if (h.x > density) return 0.0;
        float xc = 0.2 + 0.6 * h.y;
        float dx = abs(f.x - xc) * cell.x;
        float width = 0.55 * k;
        float a = saturate(1.0 - dx / width);
        float y0 = h.z * 0.6;
        float len = 0.22 + 0.12 * h.y;
        float y = f.y - y0;
        a *= smoothstep(0.0, 0.05, y) * smoothstep(len, len - 0.12, y);
        return a;
    }

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    float4 fragPrefilter(v2f i) : SV_Target
    {
        float2 t = _MainTex_TexelSize.xy;
        float3 c = tex2D(_MainTex, i.uv + t * float2(-1, -1)).rgb;
        c += tex2D(_MainTex, i.uv + t * float2(1, -1)).rgb;
        c += tex2D(_MainTex, i.uv + t * float2(-1, 1)).rgb;
        c += tex2D(_MainTex, i.uv + t * float2(1, 1)).rgb;
        c *= 0.25;
        // a soft knee: only the bright parts pass, and they fade in rather than switch on
        float br = max(c.r, max(c.g, c.b));
        float th = 0.72, knee = 0.2;
        float soft = clamp(br - th + knee, 0.0, 2.0 * knee);
        soft = soft * soft / (4.0 * knee + 1e-4);
        float w = max(soft, br - th) / max(br, 1e-4);
        return float4(c * w, 1.0);
    }

    float4 blur(float2 uv, float2 d)
    {
        float3 c = tex2D(_MainTex, uv).rgb * 0.227027;
        c += tex2D(_MainTex, uv + d * 1.384615).rgb * 0.316216;
        c += tex2D(_MainTex, uv - d * 1.384615).rgb * 0.316216;
        c += tex2D(_MainTex, uv + d * 3.230769).rgb * 0.070270;
        c += tex2D(_MainTex, uv - d * 3.230769).rgb * 0.070270;
        return float4(c, 1.0);
    }

    float4 fragBlurX(v2f i) : SV_Target { return blur(i.uv, float2(_PostTexel.x * 1.5, 0)); }
    float4 fragBlurY(v2f i) : SV_Target { return blur(i.uv, float2(0, _PostTexel.y * 1.5)); }

    // light shafts: march from the pixel toward the sun through the bright parts; where a
    // ridge or a cloud stands in the way the march finds dark, and the shaft is cut
    float4 fragRays(v2f i) : SV_Target
    {
        if (_Sun.z <= 0.001) return float4(0, 0, 0, 1);
        const int N = 28;
        float2 d = (_Sun.xy - i.uv) * (0.9 / N);
        float2 uv = i.uv;
        float3 acc = 0;
        float decay = 1.0;
        float2 asp0 = float2(_Screen.x / _Screen.y, 1.0);
        for (int k = 0; k < N; k++)
        {
            uv += d;
            float2 c = saturate(uv);
            // only the light around the sun itself makes shafts: a sky full of bright
            // white cloud, marched toward a sun off at the side, washed the whole frame
            // in a warm haze
            float2 o = (c - _Sun.xy) * asp0;
            float nearSun = exp(-dot(o, o) * 14.0);
            acc += tex2D(_MainTex, c).rgb * (decay * nearSun);
            decay *= 0.965;
        }
        acc /= N;
        // the shafts fall off away from the sun
        float2 asp = float2(_Screen.x / _Screen.y, 1.0);
        float dist = length((i.uv - _Sun.xy) * asp);
        acc *= saturate(1.2 - dist * 0.9);
        return float4(acc, 1);
    }

    float4 fragComposite(v2f i) : SV_Target
    {
        float3 col = tex2D(_MainTex, i.uv).rgb;
        float2 px = i.uv * _Screen.xy;
        float k = _Screen.y / 720.0;

        // mist: drifting banks lying along the far horizon and the valleys under it —
        // in the distance, not over the ground at his feet
        if (_Fog.a > 0.001)
        {
            float yd = 1.0 - i.uv.y;
            float2 p = float2(px.x / _Screen.y, yd) * float2(2.2, 5.0);
            float n = fbm(p + float2(_Rain.z * 0.035, 0.0)) * 0.7 + fbm(p * 1.7 - float2(_Rain.z * 0.06, 0.0)) * 0.3;
            float hz = _Screen.z;
            float band = smoothstep(hz - 0.16, hz - 0.01, yd) * (1.0 - smoothstep(hz + 0.02, hz + 0.16, yd));
            float a = _Fog.a * band * (0.3 + 0.9 * n) * 0.45;
            col = lerp(col, _Fog.rgb, saturate(a));
        }

        // bloom and the light shafts, screened over the frame so nothing clips hard
        float3 add = tex2D(_BloomTex, i.uv).rgb * _Sun.w;
        add += tex2D(_RaysTex, i.uv).rgb * _SunColor.rgb * (_Sun.z * 2.6);
        col = 1.0 - (1.0 - col) * (1.0 - saturate(add));

        // rain in three depths: the near one larger, faster and fainter
        if (_Rain.x > 0.001)
        {
            float r = rainLayer(px, k * 0.7, 900.0, 0.55 * _Rain.x, 1.0) * 0.55;
            r += rainLayer(px, k * 1.0, 1150.0, 0.45 * _Rain.x, 2.0) * 0.45;
            r += rainLayer(px, k * 1.6, 1500.0, 0.3 * _Rain.x, 3.0) * 0.32;
            col = lerp(col, float3(0.80, 0.84, 0.91), saturate(r) * (0.35 + 0.25 * _Rain.x));
        }

        // grain: fine, moving, a little stronger in the shadows
        float g = hash12(floor(px) + frac(_Rain.z * 7.31) * 467.0) - 0.5;
        col += g * _Rain.w * (0.045 - 0.02 * dot(col, float3(0.33, 0.33, 0.33)));
        // Brighter: on a phone at ordinary brightness the picture only looked right with
        // the screen turned all the way up. The mid-tones are lifted and the whole raised
        // a little; black stays black and the highlights roll off instead of clipping.
        col = saturate(col);
        col = pow(col, _Screen.w > 0 ? 1.0 / _Screen.w : 1.0);
        col = 1.0 - (1.0 - col) * (1.0 - col * 0.08);
        return float4(col, 1.0);
    }
    // the frame as drawn, only brightened (low quality: no effects, but the same lift)
    float4 fragPlain(v2f i) : SV_Target
    {
        float3 col = saturate(tex2D(_MainTex, i.uv).rgb);
        col = pow(col, _Screen.w > 0 ? 1.0 / _Screen.w : 1.0);
        col = 1.0 - (1.0 - col) * (1.0 - col * 0.08);
        return float4(col, 1.0);
    }
    ENDCG

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPrefilter
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlurX
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlurY
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragRays
            #pragma target 3.0
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragComposite
            #pragma target 3.0
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPlain
            ENDCG }
    }
    Fallback Off
}
