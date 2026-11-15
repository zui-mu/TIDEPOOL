#ifndef TIDEPOOL_GLAZED_CERAMIC_INCLUDED
#define TIDEPOOL_GLAZED_CERAMIC_INCLUDED

float CeramicTileHash(float2 cell)
{
    float3 p = frac(float3(cell.xyx) * float3(0.1031, 0.11369, 0.13787));
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

void CeramicGlaze_float(UnityTexture2D BaseMap, float2 UV, float WetMask, float WetHeight,
    float GlazeSmoothness, float WetSmoothness, float GroutSmoothness, float GroutWidth,
    float GroutTone, float TileVariation, float GlazeRelief, float DropletRelief, float WetTone,
    out float ColorFactor, out float Smoothness, out float Height, out float Occlusion)
{
    // Align the groove and glaze with the same UV and material tiling as the tile albedo.
    float2 tiledUV = BaseMap.GetTransformedUV(UV);
    float2 tileCell = floor(tiledUV);
    float2 tileLocal = frac(tiledUV);
    float2 edgeDistance = min(tileLocal, 1.0 - tileLocal);
    float edge = min(edgeDistance.x, edgeDistance.y);
    float2 pixelSpan = fwidth(tiledUV);
    float footprint = max(pixelSpan.x, pixelSpan.y);
    float halfGrout = 0.5 * GroutWidth;
    float antialias = clamp(0.5 * footprint, 0.0007, 0.14);
    float face = smoothstep(max(0.0, halfGrout - antialias), halfGrout + antialias, edge);
    float resolvedDetail = 1.0 - smoothstep(0.09, 0.30, footprint);
    float faceCoverage = (1.0 - GroutWidth) * (1.0 - GroutWidth);
    face = lerp(face, faceCoverage, smoothstep(0.13, 0.38, footprint));

    float tileSeed = CeramicTileHash(tileCell);
    float toneVariation = 1.0 + (2.0 * tileSeed - 1.0) * TileVariation * resolvedDetail;
    float wetness = saturate(WetMask);
    float glazedFaceTone = toneVariation * lerp(1.0, WetTone, wetness);
    float glaze = saturate(GlazeSmoothness + (tileSeed - 0.5) * 0.09 * resolvedDetail);
    ColorFactor = lerp(GroutTone, glazedFaceTone, face);
    Smoothness = lerp(GroutSmoothness, lerp(glaze, WetSmoothness, wetness), face);
    Occlusion = lerp(0.74, 1.0, face);

    // Soft rims catch the fixtures in short segments. Small variations in the
    // fired glaze make neighboring tile faces send highlights in different directions.
    float rim = smoothstep(halfGrout, halfGrout + 0.080, edge);
    float2 tilt = float2(tileSeed, CeramicTileHash(tileCell + 17.73)) - 0.5;
    float ripple = sin((tileLocal.x + tileSeed * 0.37) * 13.0)
                 * sin((tileLocal.y - tileSeed * 0.28) * 9.0);
    float glazedProfile = 0.86 + 0.44 * dot(tilt, tileLocal - 0.5) + 0.09 * ripple;
    Height = GlazeRelief * rim * glazedProfile * resolvedDetail
           + DropletRelief * WetHeight * face;
}

#endif
