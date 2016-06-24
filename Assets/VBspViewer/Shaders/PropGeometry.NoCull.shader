Shader "Custom/PropGeometry.NoCull"
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
        Tags{ "RenderType" = "Opaque" }
        LOD 200

        Cull Off

        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows
        #pragma target 3.0
        #include "PropGeometryShared.cginc"
        ENDCG
    }

    FallBack "Diffuse"
}
