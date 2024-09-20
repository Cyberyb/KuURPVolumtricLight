Shader "KuShader/TestShader"
{
    Properties
    {

    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZTest Always ZWrite Off Cull Off

        HLSLINCLUDE

		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities//blit.hlsl"

        ENDHLSL
        Pass
        {
            Name "DrawProcedure"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment frag

            TEXTURE2D(_GrabTexture);
            SAMPLER(sampler_GrabTexture);
            float4 _ColorTint;


            #if SHADER_API_GLES
                struct M_Attributes
                {
                    float4 posOS       : POSITION;
                    float2 uv          : TEXCOORD0;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };
            #else
            struct M_Attributes
            {
                float4 positionOS : POSITION;
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            #endif

            struct M_Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv   : TEXCOORD0;
            };

            M_Varyings Vert(M_Attributes input)
            {
                M_Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            #if SHADER_API_GLES
                float4 pos = input.positionOS;
                float2 uv  = input.uv;
            #else
                float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);
                float2 uv  = GetFullScreenTriangleTexCoord(input.vertexID);
            #endif

                output.positionCS = pos;
                output.uv   = uv;
                return output;
            }
        
 
        
 
            // 顶点着色器的输入
            struct a2v
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // 顶点着色器的输出

            struct v2f
            {
                float4 positionCS: SV_POSITION;
                float uv : TEXCOORD0;
            };
       
            v2f vert(a2v v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);

                float4 pos = GetFullScreenTriangleVertexPosition(v.vertexID);
                float2 uv  = GetFullScreenTriangleTexCoord(v.vertexID);

                o.positionCS = pos;
                o.uv = uv;
                return o;
             }

            float4 frag(M_Varyings i):SV_Target
            {
                return SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv) * float4(_ColorTint.rgb, 1.0);
            }
            
            ENDHLSL
        }
            
    }
}
