Shader "Custom/WorldGeometry.AlphaTest"
{
    Properties
    {
        _MainTex( "Albedo (RGB)", 2D ) = "white" {}
        _LightMap( "Light Map (RGB)", 2D ) = "white" {}
        _AmbientColor( "Ambient (RGB)", Color) = (0.25, 0.25, 0.25, 1)
        _AlphaCutoff( "Alpha Cutoff", Range( 0, 1 ) ) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        Pass
        {
            Tags
            {
                "Queue" = "AlphaTest"
                "RenderType" = "TransparentCutout"
                "IgnoreProjector" = "True"
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd_fullshadows
            #define ALPHA_TEST
            #include "WorldGeometryShared.cginc"
            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }

            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #define ALPHA_TEST
            #define WORLD_SHADOW_CASTER
            #include "WorldGeometryShared.cginc"
            ENDCG
        }
    }
}
