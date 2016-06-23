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
    fixed4 c = fixed4( 1, 1, 1, tex2D( _MainTex, IN.uv_MainTex ).a ) * pow( fixed4( IN.VertColor, 1 ), 1.265 ) * _Color;
    o.Albedo = c.rgb;
    o.Metallic = _Metallic;
    o.Smoothness = _Glossiness;
    o.Alpha = c.a;
}
