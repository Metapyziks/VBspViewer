Shader "Custom/PropGeometry.NoCull.AlphaTest"
{
    Properties
    {
        _Color( "Color", Color ) = (1,1,1,1)
        _MainTex( "Albedo (RGB)", 2D ) = "white" {}
        _Glossiness( "Smoothness", Range( 0,1 ) ) = 0.0
        _Metallic( "Metallic", Range( 0,1 ) ) = 0.0
        _AlphaCutoff( "Alpha Cutoff", Range( 0, 1 ) ) = 0.5

        _TreeSwayStartHeight( "Tree Sway Start Height", Range( 0, 1 ) ) = .5
        _TreeSwayHeight( "Tree Sway Height", Float ) = 300
        _TreeSwayStartRadius( "Tree Sway Start Radius", Range( 0, 1 ) ) = 0
        _TreeSwayRadius( "Tree Sway Radius", Float ) = 200
        _TreeSwaySpeed( "Tree Sway Speed", Float ) = 0.2
        _TreeSwayStrength( "Tree Sway Strength", Float ) = 0.4
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

        Cull Off

        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow alphatest:_AlphaCutoff
        #pragma multi_compile __ TREE_SWAY
        #pragma target 3.0
        #include "PropGeometryShared.cginc"
        ENDCG
    }

    FallBack "Diffuse"
}
