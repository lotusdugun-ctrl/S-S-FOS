// Premultiplied paint (the canvas surfaces) → straight alpha, for UI Toolkit backgrounds.
Shader "Hidden/SisifosUnpremul"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off Blend Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 frag(v2f_img i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                return c.a > 0.0001 ? float4(saturate(c.rgb / c.a), c.a) : float4(0, 0, 0, 0);
            }
            ENDCG
        }
    }
}
