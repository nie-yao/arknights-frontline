Shader "ArknightsFrontline/NiuLaiMama"
{
    Properties { _BaseMap("Base", 2D) = "white" {} _SkinColor("Orange fur", Color) = (1,0.29,0.075,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST; half4 _SkinColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half fog:TEXCOORD3; };
            Varyings Vert(Attributes input)
            {
                Varyings o; o.positionWS=TransformObjectToWorld(input.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(input.normalOS); o.uv=TRANSFORM_TEX(input.uv,_BaseMap); o.fog=ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half3 original=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb;
                half saturation=(max(original.r,max(original.g,original.b))-min(original.r,min(original.g,original.b)))/max(0.001,max(original.r,max(original.g,original.b)));
                // Select golden fur; preserve the pale muzzle, eyes and dark details.
                half fur=smoothstep(0.20,0.38,saturation)*smoothstep(0.18,0.32,original.g/max(original.r,0.001))*step(original.b,original.g)*smoothstep(0.18,0.35,original.r);
                half3 color=lerp(original,_SkinColor.rgb*(0.65+0.6*dot(original,half3(0.3,0.59,0.11))),fur);
                half3 normal=normalize(input.normalWS); Light light=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 lighting=SampleSH(normal)+light.color*(0.25+0.75*saturate(dot(normal,light.direction)))*light.shadowAttenuation;
                return half4(MixFog(color*lighting,input.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
