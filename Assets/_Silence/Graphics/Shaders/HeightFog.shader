Shader "SILENCE/HeightFog"
{
    Properties
    {
        _Density("Sea-level extinction", Range(0,.05)) = .008
        _Falloff("Height falloff", Range(0,2)) = .3
        _FogColor("Scattering tint", Color) = (.36,.42,.45,1)
        _StepCount("Steps", Int) = 12
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" }
        Cull Front ZWrite Off ZTest Always Blend One OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Density, _Falloff; half4 _FogColor; int _StepCount;
            float3 _BoundsMin, _BoundsMax, _FlashPosition, _FlashDirection; float4 _FlashParams;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A v) { V o=(V)0; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS=TransformObjectToHClip(v.positionOS.xyz); return o; }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
                float depth=SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                float3 end=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
                float3 origin=GetCameraPositionWS(); float maxDistance=min(distance(origin,end),150);
                float3 direction=normalize(end-origin);
                float3 inv=rcp(direction+float3(1e-6,1e-6,1e-6));
                float3 a=(_BoundsMin-origin)*inv,b=(_BoundsMax-origin)*inv;
                float3 nearPlane=min(a,b),farPlane=max(a,b);
                float start=max(0,max(nearPlane.x,max(nearPlane.y,nearPlane.z)));
                float stop=min(maxDistance,min(farPlane.x,min(farPlane.y,farPlane.z)));
                if(stop<=start)return 0;
                int steps=clamp(_StepCount,8,32); float stride=(stop-start)/steps;
                float transmittance=1; half3 scattering=0;
                [loop] for(int index=0;index<steps;index++)
                {
                    float3 p=origin+direction*(start+(index+.5)*stride);
                    float density=_Density*exp(-max(-1,p.y)*_Falloff);
                    float edge=saturate(min(min(p.x-_BoundsMin.x,_BoundsMax.x-p.x),min(p.z-_BoundsMin.z,_BoundsMax.z-p.z))*.2);
                    density*=edge;
                    float opacity=1-exp(-density*stride);
                    Light sun=GetMainLight(TransformWorldToShadowCoord(p));
                    float phase=.35+.65*pow(saturate(dot(direction,sun.direction)),6);
                    half3 radiance=_FogColor.rgb*(.35+sun.color*sun.shadowAttenuation*phase);
                    if(_FlashParams.w>.5)
                    {
                        float3 fromLight=p-_FlashPosition; float distanceLight=length(fromLight);
                        float cone=smoothstep(_FlashParams.x,min(.999,_FlashParams.x+.055),dot(normalize(fromLight),_FlashDirection));
                        float rangeFade=saturate(1-distanceLight/max(1,_FlashParams.y));
                        radiance+=half3(1,.91,.75)*cone*rangeFade*rangeFade*_FlashParams.z;
                    }
                    scattering+=radiance*opacity*transmittance; transmittance*=1-opacity;
                }
                return half4(scattering,1-transmittance);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
