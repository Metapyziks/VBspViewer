Shader "Custom/PropGeometry.Translucent"
{
    Properties
    {
        _Color( "Color", Color ) = (1,1,1,1)
        _MainTex( "Albedo (RGB)", 2D ) = "white" {}
        _Glossiness( "Smoothness", Range( 0,1 ) ) = 0.0
        _Metallic( "Metallic", Range( 0,1 ) ) = 0.0

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
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }
        LOD 200

        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        CGPROGRAM
        #pragma surface surf Standard vertex:vert alpha
        #pragma multi_compile __ TREE_SWAY
        #pragma target 3.0
        #include "PropGeometryShared.cginc"
        ENDCG
    }

    FallBack "Diffuse"
}
