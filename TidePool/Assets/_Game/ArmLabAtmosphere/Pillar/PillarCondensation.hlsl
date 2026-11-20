#ifndef TIDEPOOL_PILLAR_CONDENSATION_INCLUDED
#define TIDEPOOL_PILLAR_CONDENSATION_INCLUDED

float PillarCondensationHash(float2 cell)
{
    float3 p = frac(float3(cell.xyx) * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

void PillarCondensation_float(float3 PositionWS, float3 NormalWS, float WetAmount,
    float StreakScale, float StreakWidth, float DropletDensity, float DropletSize,
    out float WetMask, out float Height)
{
    // Each face of the square column has its own horizontal run. World Y keeps
    // runoff vertical and makes the pattern independent of stretched tile UVs.
    float acrossX = step(abs(NormalWS.z), abs(NormalWS.x));
    float along = lerp(PositionWS.x, PositionWS.z, acrossX);
    float crossFace = lerp(PositionWS.z, PositionWS.x, acrossX);
    float faceSeed = floor(crossFace * 3.0 + 0.5) + 17.0 * acrossX;
    float2 p = float2(along, PositionWS.y);
    float face = 1.0 - smoothstep(0.22, 0.68, abs(NormalWS.y));

    float u = p.x * max(StreakScale, 0.1);
    float lane = floor(u);
    float laneSeed = PillarCondensationHash(float2(lane, faceSeed));
    float sway = 0.018 * sin(p.y * 1.7 + laneSeed * 12.0)
               + 0.009 * sin(p.y * 4.2 + laneSeed * 23.0);
    float across = abs(frac(u) - lerp(0.32, 0.68, laneSeed) - sway);
    float laneAA = clamp(fwidth(u), 0.004, 0.08);
    float width = max(StreakWidth, 0.005) * lerp(0.36, 1.0, laneSeed);
    float streak = 1.0 - smoothstep(width * 0.27, width + laneAA, across);
    float run = p.y * lerp(0.20, 0.41, laneSeed) + laneSeed * 7.1;
    float patch = frac(run);
    float runShape = smoothstep(0.03, 0.26, patch) * (1.0 - smoothstep(0.68, 0.97, patch));
    float runStrength = lerp(0.37, 1.0, PillarCondensationHash(float2(floor(run), lane + faceSeed)));
    float activeLane = smoothstep(0.48, 0.63, laneSeed);
    float streakDetail = 1.0 - smoothstep(0.08, 0.29, fwidth(u));
    streak *= activeLane * runShape * runStrength * streakDetail;

    // Sparse, irregular drops with a slightly longer profile in the Y direction.
    float2 grid = p * float2(5.0, 4.2);
    float2 cell = floor(grid) + float2(faceSeed * 13.0, 0.0);
    float dropSeed = PillarCondensationHash(cell + 7.32);
    float2 center = float2(
        lerp(0.25, 0.75, PillarCondensationHash(cell + 31.7)),
        lerp(0.25, 0.75, PillarCondensationHash(cell + 49.2)));
    float2 delta = (frac(grid) - center) / float2(5.0, 4.2);
    float radius = lerp(0.006, 0.017, dropSeed) * max(DropletSize, 0.1);
    float shape = length(float2(delta.x, delta.y / 1.38)) / max(radius, 0.0001);
    float dropAA = clamp(fwidth(shape), 0.035, 0.45);
    float dropDetail = 1.0 - smoothstep(0.045, 0.28, max(fwidth(grid.x), fwidth(grid.y)));
    float present = 1.0 - step(saturate(DropletDensity), dropSeed);
    float drop = (1.0 - smoothstep(0.54, 1.0 + dropAA, shape)) * present * dropDetail;
    float dome = saturate(1.0 - shape * shape);

    float amount = saturate(WetAmount) * face;
    WetMask = saturate(streak * 0.78 + drop * 0.96) * amount;
    Height = (streak * 0.05 + dome * drop) * amount;
}

#endif
