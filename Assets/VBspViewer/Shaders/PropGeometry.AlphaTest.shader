Shader "Custom/PropGeometry.AlphaTest"
{
    Properties
    {
        _Color( "Color", Color ) = (1,1,1,1)
        _MainTex( "Albedo (RGB)", 2D ) = "white" {}
        _Glossiness( "Smoothness", Range( 0,1 ) ) = 0.0
        _Metallic( "Metallic", Range( 0,1 ) ) = 0.0
        _AlphaCutoff( "Alpha Cutoff", Range( 0, 1) ) = 0.5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
            "IgnoreProjector" = "True"
        }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow alphatest:_AlphaCutoff
        #pragma target 3.0
        #include "PropGeometryShared.cginc"
        ENDCG
    }

    FallBack "Transparent/Cutout/Diffuse"
}
