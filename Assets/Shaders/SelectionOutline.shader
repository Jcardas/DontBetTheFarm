// The pen outline for SelectionHighlight: the mesh drawn again, pushed out along its normals, with front faces culled,
// so only a rim shows around the real mesh. Unlit, colour from _BaseColor (SelectionHighlight pulses it).
Shader "DBTF/Selection Outline"
{
    Properties
    {
        _BaseColor("Color", Color) = (0.55, 1, 0.6, 1)
        _Width("Width (m)", Float) = 0.08
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry+10" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Width;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.positionCS = TransformWorldToHClip(positionWS + normalWS * _Width);
                return output;
            }

            half4 frag(Varyings input) : SV_Target { return _BaseColor; }
            ENDHLSL
        }
    }
}
