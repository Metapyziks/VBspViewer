Shader "Custom/WorldGeometry"
{
    Properties
    {
        _MainTex( "Albedo (RGB)", 2D ) = "white" {}
        _LightMap( "Light Map (RGB)", 2D ) = "white" {}
        _AmbientColor( "Ambient (RGB)", Color) = (0.25, 0.25, 0.25, 1)
    }

    SubShader
    {
        Tags{ "RenderType" = "Opaque" }
        LOD 200

        Pass
        {
            Tags{ "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd_fullshadows
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"

            struct v2f
            {
                float4 pos : SV_POSITION;
                float shade : TEXCOORDS0;
                float4 uv2 : TEXCOORDS1;
                LIGHTING_COORDS( 2, 3 )
            };

            sampler2D _MainTex;
            sampler2D _LightMap;

            fixed4 _AmbientColor;

            v2f vert( appdata_full v )
            {
                v2f o;
                o.pos = mul( UNITY_MATRIX_MVP, v.vertex );
                o.shade = float (dot( _WorldSpaceLightPos0.xyz, v.normal ) > 0);
                o.uv2 = v.texcoord2;
                TRANSFER_VERTEX_TO_FRAGMENT( o );
                return o;
            }

            fixed4 frag( v2f i ) : COLOR
            {
                float atten = LIGHT_ATTENUATION( i ) * i.shade;
                fixed3 lightmap = tex2D( _LightMap, i.uv2 ).rgb;

                return fixed4( atten * lightmap + (1 - atten) * lightmap * _AmbientColor.rgb, 1 );
            }
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
            #include "UnityCG.cginc"

            struct v2f
            {
                V2F_SHADOW_CASTER;
            };

            sampler2D _MainTex;

            v2f vert( appdata_full v )
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET( o )
                return o;
            }

            fixed4 frag( v2f i ) : COLOR
            {
                SHADOW_CASTER_FRAGMENT( i )
            }
            ENDCG
        }
    }

    //Fallback "Diffuse"
}
