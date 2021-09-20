Shader "Custom/PlanetShader"
{
    Properties
    {
        _ColorMin("ColorMin", Color) = (1,1,1,1)
        _ColorMax("ColorMax", Color) = (1,1,1,1)
        _MainTex("Albedo (RGB)", 2D) = "white" {}
        _HeightMin("HeightMin", float) = 0
        _HeightMax("HeightMax", float) = 5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _ColorMin;
        fixed4 _ColorMax;
        float _HeightMin;
        float _HeightMax;
        float currentHeight = 0;

        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float currentBias = currentHeight - _HeightMin / _HeightMax - _HeightMin;
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * (_ColorMin * currentBias) * (_ColorMax * (1-currentBias));
            o.Albedo = c.rgb;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
