Shader "TidePool/RoofWater"
{
    Properties { _Stream("Stream",Float)=0 }
    SubShader
    {
        Tags {"RenderType"="Transparent" "Queue"="Transparent+12" "RenderPipeline"="UniversalPipeline"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float3 positionWS:TEXCOORD1;};
            float _TidePoolSeepWaterLevel;
            float _TidePoolSeepEnabled;
            CBUFFER_START(UnityPerMaterial)
            float _Stream;
            CBUFFER_END
            Varyings vert(Attributes v){Varyings o;o.positionWS=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.color=v.color;o.uv=v.uv;return o;}
            half4 frag(Varyings i):SV_Target
            {
                clip(_TidePoolSeepEnabled-.5);
                clip(i.positionWS.y-_TidePoolSeepWaterLevel);
                float across=lerp(i.uv.x,i.uv.y,_Stream);
                float along=lerp(i.uv.y,i.uv.x,_Stream);
                float band=pow(saturate(1-abs(across*2-1)),1.5);
                float tips=lerp(saturate(along*8)*saturate((1-along)*8),1,_Stream);
                float glint=pow(saturate(1-abs(across-.35)*12),3);
                float shimmer=.8+.2*sin(along*65-_Time.y*18);
                return half4(i.color.rgb+glint*.25,i.color.a*band*tips*shimmer);
            }
            ENDHLSL
        }
    }
}
