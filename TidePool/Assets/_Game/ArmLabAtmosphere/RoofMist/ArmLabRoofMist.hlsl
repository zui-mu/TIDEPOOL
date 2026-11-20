#ifndef TIDEPOOL_ARM_LAB_ROOF_MIST_INCLUDED
#define TIDEPOOL_ARM_LAB_ROOF_MIST_INCLUDED

float RoofMistHash(float2 cell)
{
    float3 p = frac(cell.xyx * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float RoofMistNoise(float2 coordinates)
{
    float2 cell = floor(coordinates);
    float2 f = frac(coordinates);
    f = f * f * (3.0 - 2.0 * f);
    float lower = lerp(RoofMistHash(cell), RoofMistHash(cell + float2(1.0, 0.0)), f.x);
    float upper = lerp(RoofMistHash(cell + float2(0.0, 1.0)),
                       RoofMistHash(cell + float2(1.0, 1.0)), f.x);
    return lerp(lower, upper, f.y);
}

void IntegrateRoofMist_float(float3 ExitWS, float RawDepth, float Density, float MottleScale,
                            float DriftSpeed, out float Opacity)
{
#if defined(SHADERGRAPH_PREVIEW)
    Opacity = saturate(Density * 2.0);
#else
    Opacity = 0.0;
    if (Density <= 0.0)
        return;

    // The cube draws its back faces only. Each fragment starts at the exit
    // point of its view ray, even when the camera is inside the cube.
    float3 cameraForward = GetViewForwardDir();
    bool perspective = IsPerspectiveProjection();
    float3 rayWS = perspective ? SafeNormalize(ExitWS - GetCameraPositionWS()) : cameraForward;
    float forwardProjection = max(dot(rayWS, cameraForward), 0.001);
    float exitEyeDepth = -TransformWorldToView(ExitWS).z;
    float sceneEyeDepth = perspective ? LinearEyeDepth(RawDepth, _ZBufferParams)
                                      : LinearDepthToEyeDepth(RawDepth);

    // Keep object-space direction in units of local distance per world metre.
    // This preserves the ray distance when the unit cube is stretched.
    float3 exitOS = clamp(TransformWorldToObject(ExitWS), -0.5, 0.5);
    float3 rayOS = mul((float3x3)GetWorldToObjectMatrix(), rayWS);
    float3 axisSpeed = abs(rayOS);
    float3 wallDistance = (0.5 + sign(rayOS) * exitOS) / max(axisSpeed, 0.000001);
    wallDistance = lerp(float3(1e6, 1e6, 1e6), wallDistance, step(0.000001, axisSpeed));
    float entryDistance = min(wallDistance.x, min(wallDistance.y, wallDistance.z));

    // Clip the backwards interval to the near plane and the opaque surface.
    // The small eye-space offset avoids leaking mist through coplanar trim.
    float nearDistance = min(entryDistance, (exitEyeDepth - _ProjectionParams.y) / forwardProjection);
    float farDistance = max(0.0, (exitEyeDepth - sceneEyeDepth + 0.015) / forwardProjection);
    float span = nearDistance - farDistance;
    if (span <= 0.0)
        return;

    const int sampleCount = 6;
    float stride = span / sampleCount;
    float accumulatedDensity = 0.0;
    float2 drift = _Time.y * DriftSpeed * float2(1.0, 0.24);

    [unroll]
    for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
    {
        float distanceFromExit = farDistance + (sampleIndex + 0.5) * stride;
        float3 pOS = exitOS - rayOS * distanceFromExit;
        float3 pWS = ExitWS - rayWS * distanceFromExit;
        float height = pOS.y + 0.5;
        float verticalFade = smoothstep(0.0, 0.68, height)
                           * (1.0 - smoothstep(0.87, 1.0, height));
        float edgeDistance = 0.5 - max(abs(pOS.x), abs(pOS.z));
        float edgeFade = smoothstep(0.0, 0.16, edgeDistance);

        float2 cloudPosition = (pWS.xz + drift) * MottleScale * float2(1.0, 0.65)
                             + float2(0.17, -0.12) * pWS.y;
        float broadMottle = RoofMistNoise(cloudPosition);
        float fineMottle = RoofMistNoise(cloudPosition * 2.15 - drift * (MottleScale * 0.42) + 19.4);
        float variation = smoothstep(0.13, 0.78, broadMottle * 0.8 + fineMottle * 0.2);
        accumulatedDensity += verticalFade * edgeFade * lerp(0.28, 1.0, variation);
    }

    // Let longer glancing rays accumulate haze with a soft opacity ceiling.
    const float maxOpacity = 0.34;
    float opticalDepth = Density * accumulatedDensity * stride;
    Opacity = maxOpacity * (1.0 - exp(-opticalDepth / maxOpacity));
#endif
}

#endif
