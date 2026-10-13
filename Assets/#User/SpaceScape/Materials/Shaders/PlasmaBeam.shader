// The S3 battery's plasma beam (battery.md → S3): a faint streak from the base up to the sphere, its brightness
// drifting up the beam on a scrolling noise. Additive and two-sided, so its two crossed ribbons read as one beam from
// any side, fading toward their edges. The noise runs in world space, so stretching the beam does not stretch it.
// BatteryLift sets _EmissionColor (colour and strength) from the charge through a property block.
Shader "SpaceScape/PlasmaBeam"
{
    Properties
    {
        [HDR] _EmissionColor ("Colour", Color) = (0.9, 0.5, 1.8, 1)
        _HalfWidth ("Ribbon Half Width (m)", Float) = 0.06
        _NoiseScale ("Noise Scale (per metre)", Float) = 6
        _Speed ("Rise Speed (m per second)", Float) = 1.2
        _Floor ("Dimmest Share", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _EmissionColor;
                float _HalfWidth;
                float _NoiseScale;
                float _Speed;
                half _Floor;
            CBUFFER_END

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x),
                                 lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                                 lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 across : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.across = input.positionOS.xz / max(_HalfWidth, 0.0001);   // its length: 0 on the axis, 1 at an edge
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 p = input.positionWS * _NoiseScale - float3(0, _Time.y * _Speed * _NoiseScale, 0);
                float n = Noise(p) * 0.6 + Noise(p * 2.1 + 3.7) * 0.4;
                half core = 1 - saturate(length(input.across));  // per pixel: every vertex is on an edge
                core *= core;
                return half4(_EmissionColor.rgb * core * lerp(_Floor, 1, n), 1);
            }
            ENDHLSL
        }
    }
}
