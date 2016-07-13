Shader "Custom/WorldGeometry.NoCull"
{
    Properties
    {
        _LightMap( "Light Map (RGB)", 2D ) = "white" {}
        _AmbientColor( "Ambient (RGB)", Color ) = (0.25, 0.25, 0.25, 1)
    }

    SubShader
    {
        Tags{ "RenderType" = "Opaque" }
        LOD 200

        Pass
        {
            Tags{ "LightMode" = "ForwardBase" }

            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd_fullshadows
            #include "WorldGeometryShared.cginc"
            ENDCG
        }
    }

    Fallback "Custom/WorldGeometry"
}
