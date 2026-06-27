Shader "Custom/FogShadowCaster"
{
    SubShader
    {

        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Tags
            {
                "LightMode" = "UniversalForward"
            }
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS  : POSITION;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float4 shadowCoords : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);

                // 获取顶点位置的 VertexPositionInputs
                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);

                // 将顶点位置转换为阴影贴图坐标
                float4 shadowCoordinates = GetShadowCoord(positions);

                // 传递阴影坐标到片段着色器
                OUT.shadowCoords = shadowCoordinates;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 从阴影贴图中获取阴影值
                half shadowAmount = MainLightRealtimeShadow(IN.shadowCoords);

                // 设置片段颜色为阴影值
                return half4(shadowAmount, shadowAmount, shadowAmount, 1.0);
            }
            
            ENDHLSL
        }
    }
}
