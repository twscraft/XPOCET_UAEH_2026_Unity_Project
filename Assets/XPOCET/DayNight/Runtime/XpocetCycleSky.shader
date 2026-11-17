Shader "XPOCET/HDRP/BOXOPHOBIC Cycle Sky"
{
    HLSLINCLUDE
    #pragma vertex Vert
    #pragma target 4.5
    #pragma editor_sync_compilation
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Sky/SkyUtils.hlsl"
    TEXTURECUBE(_DayCube); SAMPLER(sampler_DayCube);
    TEXTURECUBE(_NightCube); SAMPLER(sampler_NightCube);
    float4 _Cycle, _SkyTint, _SunDirection, _Disks;
    struct Attributes { uint vertexID:SV_VertexID; UNITY_VERTEX_INPUT_INSTANCE_ID };
    struct Varyings { float4 positionCS:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
    Varyings Vert(Attributes i) {
        Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        o.positionCS=GetFullScreenTriangleVertexPosition(i.vertexID,UNITY_RAW_FAR_CLIP_VALUE); return o;
    }
    float4 Sky(Varyings i) {
        float3 direction=-GetSkyViewDirWS(i.positionCS.xy);
        float sn=sin(_Cycle.z), cs=cos(_Cycle.z);
        float3 uv=float3(cs*direction.x-sn*direction.z,direction.y,sn*direction.x+cs*direction.z);
        float3 day=SAMPLE_TEXTURECUBE_LOD(_DayCube,sampler_DayCube,uv,0).rgb;
        float3 night=SAMPLE_TEXTURECUBE_LOD(_NightCube,sampler_NightCube,uv,0).rgb;
        float3 color=lerp(night,day,_Cycle.x)*_SkyTint.rgb*_Cycle.y;
        // Visible disks follow the same directions as the actual lights. Exclude
        // disks from environment lighting to avoid counting the direct light twice.
        float above=smoothstep(-0.015,0.025,direction.y);
        float sun=smoothstep(0.99980,0.99993,dot(direction,normalize(_SunDirection.xyz)));
        float moon=smoothstep(0.99975,0.99990,dot(direction,-normalize(_SunDirection.xyz)));
        color+=above*_Cycle.w*(sun*_Disks.x*float3(1,0.85,0.65)*_Cycle.y*8 + moon*_Disks.y*float3(0.65,0.78,1)*_Cycle.y*3);
        return float4(color,1);
    }
    float4 Bake(Varyings i):SV_Target { return Sky(i); }
    float4 Render(Varyings i):SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        float4 c=Sky(i); c.rgb*=GetCurrentExposureMultiplier(); return c;
    }
    ENDHLSL
    SubShader {
        Tags { "RenderPipeline"="HDRenderPipeline" }
        Pass {
            ZWrite Off ZTest Always Cull Off Blend Off
            HLSLPROGRAM
            #pragma fragment Bake
            ENDHLSL
        }
        Pass {
            ZWrite Off ZTest LEqual Cull Off Blend Off
            HLSLPROGRAM
            #pragma fragment Render
            ENDHLSL
        }
    }
    Fallback Off
}
