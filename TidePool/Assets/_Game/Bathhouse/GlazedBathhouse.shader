Shader "TidePool/Bathhouse/Architectural Finish"
{
    Properties
    {
        _BaseColor ("Finish color", Color) = (.7,.5,.52,1)
        _JointColor ("Grout", Color) = (.42,.36,.37,1)
        _TileSize ("Module in metres", Float) = .2
        _JointWidth ("Grout width in metres", Float) = .003
        [Enum(Wall,0,Floor,1,Ceiling,2,Wired Glass,3)] _Finish ("Finish", Float) = 0
        _Smoothness ("Dry smoothness", Range(0,1)) = .65
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "BathhouseForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _JointColor;
                float _TileSize, _JointWidth, _Finish, _Smoothness;
            CBUFFER_END

            struct Attributes { float4 pos : POSITION; float3 normal : NORMAL; };
            struct Varyings { float4 pos : SV_POSITION; float3 world : TEXCOORD0; float3 normal : TEXCOORD1; half fog : TEXCOORD2; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.world = TransformObjectToWorld(v.pos.xyz);
                o.pos = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(v.normal);
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }
            float Hash(float2 v)
            {
                float3 p = frac(v.xyx * .1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }
            float Noise(float2 uv)
            {
                float2 i = floor(uv), f = frac(uv);
                f = f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),
                            lerp(Hash(i+float2(0,1)),Hash(i+1),f.x), f.y);
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float3 u = abs(n.x) > .5 ? float3(0,0,1) : float3(1,0,0);
                float3 v = abs(n.y) > .5 ? float3(0,0,1) : float3(0,1,0);
                float2 uv = float2(dot(i.world,u),dot(i.world,v));
                if (_Finish > 2.5)
                {
                    // The view beyond the wired pane is intentionally indistinct.
                    float2 wired = float2(uv.x+uv.y, uv.x-uv.y) / .038;
                    float2 dist = abs(frac(wired+.5)-.5);
                    float2 wireMask = 1-smoothstep(.008,.032+fwidth(wired),dist);
                    float grain = Noise(uv*52)*.065 + Noise(uv*7)*.08;
                    float haze = saturate((uv.y-.55)*.48 + Noise(uv*1.8)*.14);
                    float3 glow = lerp(float3(.86,.32,.13),float3(1.55,.88,.45),haze);
                    return half4(glow*(.89+grain)*(1-.31*max(wireMask.x,wireMask.y)),1);
                }
                float2 tile = uv/_TileSize;
                float2 f = frac(tile);
                float2 edges = min(f,1-f)*_TileSize;
                float halfJoint = _JointWidth*.5;
                float2 aa = clamp(fwidth(uv)*.7,.00018,_TileSize*.18);
                float2 face = smoothstep(halfJoint-aa,halfJoint+aa,edges);
                float interior = face.x*face.y;
                float cell = Hash(floor(tile));
                float wash = Noise(uv*2.2), glaze = Noise(uv*28);
                half3 base = _BaseColor.rgb * (.92 + .075*cell + .065*wash + .018*glaze);
                float smoothness = _Smoothness;
                if (_Finish < .5)
                {
                    float drip = Noise(float2(uv.x*28,uv.y*2)) * Noise(float2(uv.x*3,uv.y*1.3));
                    base *= 1-.13*drip;
                    smoothness += (glaze-.5)*.07;
                }
                else if (_Finish < 1.5)
                {
                    float nearWall = saturate(1-min(abs(i.world.x),abs(6-i.world.x))*.85);
                    float damp = smoothstep(.37,.66, .53*Noise(uv*1.24)+.27*Noise(uv*4.5)+.28*nearWall);
                    smoothness = lerp(smoothness,.89,damp);
                    base *= (1-.24*damp);
                }
                float2 bevel = (1-smoothstep(halfJoint,halfJoint+.004,edges))*sign(f-.5);
                n = normalize(n + (u*bevel.x + v*bevel.y)*.075*interior
                            + (u*(Noise(uv*35)-.5) + v*(Noise(uv*35+80)-.5))*.012);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = lerp(_JointColor.rgb*(.9+.14*glaze),base,interior);
                surface.smoothness = lerp(.15,smoothness,interior);
                surface.occlusion = .88 + .12*interior;
                surface.normalTS = half3(0,0,1);
                surface.alpha = 1;
                InputData light = (InputData)0;
                light.positionWS = i.world;
                light.positionCS = i.pos;
                light.normalWS = n;
                light.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.world);
                light.shadowCoord = TransformWorldToShadowCoord(i.world);
                light.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.pos);
                light.vertexLighting = VertexLighting(i.world,n);
                light.bakedGI = SampleSH(n);
                light.shadowMask = 1;
                half4 color = UniversalFragmentPBR(light,surface);
                color.rgb = MixFog(color.rgb,i.fog);
                return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
