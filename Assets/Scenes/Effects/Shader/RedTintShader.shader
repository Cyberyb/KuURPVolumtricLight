Shader "KuShader/RedTintShader"
{
	HLSLINCLUDE
		
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float _Intensity;

		float4 RedTint(Varyings input): SV_Target
		{
			float4 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord);

			return color * float4(_Intensity, 0, 0, 1);
		}

		float4 SimpleBlit (Varyings input) : SV_Target
        {
            float4 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord);
            return float4(color.rgb, 1);
        }


	ENDHLSL

	SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZTest Always ZWrite Off Cull Off
        Pass
        {
            Name "RedTint"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment RedTint
            
            ENDHLSL
        }
        
        Pass
        {
            Name "SimpleBlit"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment SimpleBlit
            
            ENDHLSL
        }
    }
}
