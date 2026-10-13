// The S5 battery's light band (battery.md → S5): bunches of light racing round the ring, evenly spaced. The angle
// round the ring comes from object space about _Centre, so the mesh needs no UVs. BatteryRing sets the values from the
// battery's charge through a property block: how many bunches, how long each is as a share of the gap between them
// (1 is one solid band), how far round they have travelled (the script carries it on, so a change of speed never
// makes them jump), the colour, and the shimmer under strain. Unlit, no lights: it is a bright band, glowing under
// bloom where there is bloom.
Shader "SpaceScape/BatteryRing"
{
    Properties
    {
        [HDR] _EmissionColor ("Bunch Colour", Color) = (1.5, 1.4, 2, 1)
        _DarkColor ("Between Bunches", Color) = (0.02, 0.01, 0.04, 1)
        _Centre ("Ring Centre (object space)", Vector) = (0, 0.12, 0, 0)
        _Segments ("Bunches", Range(0, 20)) = 10
        _Length ("Bunch Length (share of spacing)", Range(0, 1)) = 0.4
        _Phase ("Turns Travelled", Float) = 0
        _Softness ("Edge Softness (share of spacing)", Range(0.001, 0.5)) = 0.08
        _Shimmer ("Shimmer", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _EmissionColor;
            half4 _DarkColor;
            float4 _Centre;
            float _Segments;
            float _Length;
            float _Phase;
            float _Softness;
            half _Shimmer;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionOS = input.positionOS.xyz;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p = input.positionOS.xy - _Centre.xy;
                float turn = atan2(p.y, p.x) / (2.0 * PI) + 0.5;            // 0..1 round the ring

                // Where this point sits in its bunch's spacing, the bunches travelling round.
                float n = floor(_Segments + 0.5);
                float along = frac((turn + _Phase) * max(n, 1.0));
                half lit = 0;
                if (n > 0.5)
                {
                    float soft = _Softness;
                    lit = _Length >= 0.999 ? 1 : smoothstep(0, soft, along) * (1 - smoothstep(_Length - soft, _Length, along));
                }

                // Held back at full: the band flickers slightly along its length.
                half shimmer = 1 + _Shimmer * 0.18 * sin(turn * 90.0 + _Time.y * 23.0) * sin(_Time.y * 7.3 + turn * 31.0);
                half3 colour = lerp(_DarkColor.rgb, _EmissionColor.rgb * shimmer, lit);
                colour = MixFog(colour, input.fogFactor);
                return half4(colour, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
    }
}
