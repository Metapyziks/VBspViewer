#include "UnityCG.cginc"

#if defined(ALPHA_TEST) || defined(TRANSLUCENT)
#define HAS_MAIN_TEX
#endif

#ifdef ALPHA_TEST
    float _AlphaCutoff;
#endif

#ifdef HAS_MAIN_TEX
    sampler2D _MainTex;
#endif

#ifdef WORLD_SHADOW_CASTER

    struct v2f
    {
        V2F_SHADOW_CASTER;
#ifdef ALPHA_TEST
        float2 uv : TEXCOORDS0;
#endif
    };

    v2f vert( appdata_full v )
    {
        v2f o;
#ifdef ALPHA_TEST
        o.uv = v.texcoord;
#endif

        TRANSFER_SHADOW_CASTER_NORMALOFFSET( o )
        return o;
    }

    fixed4 frag( v2f i ) : COLOR
    {
#ifdef ALPHA_TEST
        float alpha = tex2D( _MainTex, i.uv ).a;
        clip( alpha - _AlphaCutoff );
#endif
        SHADOW_CASTER_FRAGMENT( i )
    }

#else

    #include "AutoLight.cginc"

    struct v2f
    {
        float4 pos : SV_POSITION;
#ifdef HAS_MAIN_TEX
        float2 uv : TEXCOORDS0;
#endif
        float2 uv2 : TEXCOORDS1;
        float shade : TEXCOORDS2;
        LIGHTING_COORDS( 3, 4 )
    };

    sampler2D _LightMap;

    fixed4 _AmbientColor;

    v2f vert( appdata_full v )
    {
        v2f o;
        o.pos = mul( UNITY_MATRIX_MVP, v.vertex );
        o.shade = float( dot( _WorldSpaceLightPos0.xyz, v.normal ) > 0 );
#ifdef HAS_MAIN_TEX
        o.uv = v.texcoord;
#endif
        o.uv2 = v.texcoord2;
        TRANSFER_VERTEX_TO_FRAGMENT( o );
        return o;
    }

    fixed4 frag( v2f i ) : COLOR
    {
        float atten = LIGHT_ATTENUATION( i ) * i.shade;
        float alpha = 1;
#ifdef HAS_MAIN_TEX
        alpha = tex2D( _MainTex, i.uv ).a;
#ifdef ALPHA_TEST
        clip( alpha - _AlphaCutoff );
#endif
#endif
        fixed3 lightmap = tex2D( _LightMap, i.uv2 ).rgb;
        fixed3 shadow = atten * fixed3( 1, 1, 1 ) + (1 - atten) * fixed3( 1, 1, 1 );

        return fixed4( shadow * pow( lightmap, 1 / 1.6 ), alpha );
    }

#endif
