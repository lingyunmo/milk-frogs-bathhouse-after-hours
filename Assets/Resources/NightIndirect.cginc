#ifndef AFTER_HOURS_INDIRECT
#define AFTER_HOURS_INDIRECT
sampler3D _NightEmergencyGI, _NightMainsGI;
float3 _NightGICorner;
float4 _NightGIInvSize;
half _NightCircuit;
half3 NightBounce(float3 world)
{
    float3 uv = (world - _NightGICorner) * _NightGIInvSize.xyz;
    if (_NightGIInvSize.w < .5 || any(uv < 0) || any(uv > 1)) return 0;
    return tex3D(_NightEmergencyGI, uv).rgb + _NightCircuit * tex3D(_NightMainsGI, uv).rgb;
}
#endif
