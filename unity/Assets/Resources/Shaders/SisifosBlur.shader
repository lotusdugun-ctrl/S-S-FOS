// One direction of a separable gaussian blur (UnityBackend runs it twice through
// CommandBuffer.Blit). Outside the texture counts as transparent, as for canvas shadows and
// filter: blur(). Taps are paired so bilinear filtering does two weights per fetch.
Shader "Hidden/Sisifos/Blur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
    }
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            // xy: one texel along the blur direction (uv units); z: sigma in texels; w: taps per side
            float4 _BlurParams;

            float4 Tap(float2 uv)
            {
                if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0) return float4(0.0, 0.0, 0.0, 0.0);
                return tex2Dlod(_MainTex, float4(uv, 0.0, 0.0));
            }

            float4 frag(v2f_img i) : SV_Target
            {
                float sigma = _BlurParams.z;
                int n = (int)_BlurParams.w;
                float k2 = 1.0 / (2.0 * sigma * sigma);
                float4 acc = Tap(i.uv);
                float wsum = 1.0;
                [loop]
                for (int k = 1; k <= n; k += 2)
                {
                    float fk = (float)k;
                    float w1 = exp(-fk * fk * k2);
                    float w2 = exp(-(fk + 1.0) * (fk + 1.0) * k2);
                    float w = w1 + w2;
                    float off = (fk * w1 + (fk + 1.0) * w2) / w;
                    float2 d = _BlurParams.xy * off;
                    acc += (Tap(i.uv + d) + Tap(i.uv - d)) * w;
                    wsum += 2.0 * w;
                }
                return acc / wsum;
            }
            ENDCG
        }
    }
    Fallback Off
}
