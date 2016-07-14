sampler2D _MainTex;

struct Input
{
    float2 uv_MainTex;
    fixed3 VertColor;
};

half _Glossiness;
half _Metallic;
fixed4 _Color;

#ifdef AMBIENT_CUBE
fixed3 _AmbientCube0;
fixed3 _AmbientCube1;
fixed3 _AmbientCube2;
fixed3 _AmbientCube3;
fixed3 _AmbientCube4;
fixed3 _AmbientCube5;
#endif

#ifdef TREE_SWAY
float _TreeSwayStartHeight;
float _TreeSwayHeight;
float _TreeSwayStartRadius;
float _TreeSwayRadius;
float _TreeSwaySpeed;
float _TreeSwayStrength;

void treeSway( inout appdata_full v )
{
    const float sourceToUnity = 0.01905;

    float dist = length( v.vertex.xyz );

    float heightT = clamp((v.vertex.y / (_TreeSwayHeight * sourceToUnity) - _TreeSwayStartHeight) / (1 - _TreeSwayStartHeight), 0, 1);
    float radiusT = clamp((dist / (_TreeSwayRadius * sourceToUnity) - _TreeSwayStartRadius) / (1 - _TreeSwayStartRadius), 0, 1);

    v.vertex.xyz += dist * sourceToUnity * float3(sin( _Time.y * _TreeSwaySpeed * 3.123 + v.vertex.z * 0.2 ), 0, sin( _Time.y * _TreeSwaySpeed * 3.823 + v.vertex.x * 0.2 )) * heightT * radiusT * _TreeSwayStrength;
}
#endif

void vert( inout appdata_full v, out Input o )
{
    UNITY_INITIALIZE_OUTPUT( Input, o );

#ifdef TREE_SWAY
    treeSway( v );
#endif

    o.uv_MainTex = v.texcoord;
    o.VertColor = v.color;

#ifdef AMBIENT_CUBE
    float3 normal = normalize( mul( unity_ObjectToWorld, v.normal ).xyz );
    fixed3 ambient = fixed3( 0.5, 0.5, 0.5 );

    ambient += pow(max( 0, +normal.x ),2) * _AmbientCube0 * 0.5;
    ambient += pow(max( 0, -normal.x ),2) * _AmbientCube1 * 0.5;
    ambient += pow(max( 0, -normal.z ),2) * _AmbientCube2 * 0.5;
    ambient += pow(max( 0, +normal.z ),2) * _AmbientCube3 * 0.5;
    ambient += pow(max( 0, +normal.y ),2) * _AmbientCube4 * 0.5;
    ambient += pow(max( 0, -normal.y ),2) * _AmbientCube5 * 0.5;

    o.VertColor *= ambient * fixed3(0.25, 0.25, 0.25);
#endif
}

void surf( Input IN, inout SurfaceOutputStandard o )
{
    fixed4 c = fixed4( 1, 1, 1, tex2D( _MainTex, IN.uv_MainTex ).a ) * pow( fixed4( IN.VertColor, 1 ), 1.265 ) * _Color;
    o.Albedo = c.rgb;
    o.Metallic = _Metallic;
    o.Smoothness = _Glossiness;
    o.Alpha = c.a;
}
