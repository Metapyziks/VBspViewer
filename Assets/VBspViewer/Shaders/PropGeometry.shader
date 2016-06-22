Shader "Custom/PropGeometry"
{
    Properties
    {
        _Color( "Color", Color ) = (1,1,1,1)
        _MainTex( "Albedo (RGB)", 2D ) = "white" {}
        _Glossiness( "Smoothness", Range( 0,1 ) ) = 0.0
        _Metallic( "Metallic", Range( 0,1 ) ) = 0.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            fixed3 VertColor;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        void vert( inout appdata_full v, out Input o )
        {
            UNITY_INITIALIZE_OUTPUT( Input, o );

            o.uv_MainTex = v.texcoord;
            o.VertColor = v.color;
        }

        void surf( Input IN, inout SurfaceOutputStandard o )
        {
            fixed4 c = tex2D( _MainTex, IN.uv_MainTex ) * pow( fixed4( IN.VertColor, 1 ), 1.265 ) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
