Shader "Custom/PropGeometry"
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

        _AmbientCube0( "Ambient Cube[0]", Color ) = (1, 1, 1, 1)
        _AmbientCube1( "Ambient Cube[1]", Color ) = (1, 1, 1, 1)
        _AmbientCube2( "Ambient Cube[2]", Color ) = (1, 1, 1, 1)
        _AmbientCube3( "Ambient Cube[3]", Color ) = (1, 1, 1, 1)
        _AmbientCube4( "Ambient Cube[4]", Color ) = (1, 1, 1, 1)
        _AmbientCube5( "Ambient Cube[5]", Color ) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow
        #pragma multi_compile __ TREE_SWAY
        #pragma multi_compile __ AMBIENT_CUBE
        #pragma target 3.0
        #include "PropGeometryShared.cginc"
        ENDCG
    }

    FallBack "Diffuse"
}
