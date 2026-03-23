Shader "Custom/FogShadow"
{
    Properties
    {
        _TempDepth("BorderDepth", Float) = 10.0
    }
    SubShader
    {

        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float4x4 _LightVP;

            struct Attributes
            {
                float4 positionOS  : POSITION;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);

                // 只传递世界坐标到片段着色器
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 在片段着色器中计算阴影坐标
                float4 shadowCoordinates = TransformWorldToShadowCoord(IN.positionWS);

                // 获取当前级联索引
                half cascadeIndex = ComputeCascadeIndex(IN.positionWS);

                // 从矩阵中提取投影参数
                // _MainLightWorldToShadow[cascadeIndex] 是 View*Projection 矩阵
                float4x4 vpMatrix = _MainLightWorldToShadow[int(cascadeIndex)];
                
                // 投影矩阵的m22 = -(far+near)/(far-near)
                // 投影矩阵的m23 = -2*far*near/(far-near)
                // 对于正交投影：[2/(r-l), 0, 0, -(r+l)/(r-l)]
                //              [0, 2/(t-b), 0, -(t+b)/(t-b)]
                //              [0, 0, -2/(f-n), -(f+n)/(f-n)]
                //              [0, 0, 0, 1]
                
                float m22 = vpMatrix[2][2];  // -2/(f-n)
                float m23 = vpMatrix[2][3];  // -(f+n)/(f-n)
                
                // 反推 near 和 far
                // m22 = -2 / (far - near)
                // m23 = -(far + near) / (far - near)
                // 解这个方程组：
                float depthRange = -2.0 / m22;  // far - near
                float depthSum = -1.0 * m23 * depthRange;  // far + near
                
                float cascadeFar = (depthSum + depthRange) * 0.5;
                float cascadeNear = (depthSum - depthRange) * 0.5;

                float4 positionLightClip = mul(_LightVP, float4(IN.positionWS, 1.0));
                // 裁剪空间 → 归一化设备坐标（NDC）
                float3 positionLightNDC = positionLightClip.xyz / positionLightClip.w;
                // NDC的Z轴转回光源空间深度（线性）
                float depth01 = positionLightNDC.z * 0.5 + 0.5;
                float shadowCamNear = 0.1;
                float shadowCamFar = 150.0;
                
                // ✨ 解决方案：直接采样原始深度而不是使用 _SHADOW 宏
                // SAMPLE_TEXTURE2D_SHADOW 返回比较结果(0/1)，无法获得真实深度差值
                // 需要用 SAMPLE_TEXTURE2D + sampler_LinearClamp 获取原始深度
                float currentDepth = shadowCoordinates.z;
                
                // 方案1：使用普通采样获取原始深度值（0~1范围）
                float sampledDepth = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, 
                                                      sampler_PointClamp, 
                                                      shadowCoordinates.xy).r;
                
                // 计算原始深度差值（在0~1范围内）
                float depthDifference = sampledDepth - currentDepth;

                float worldDepthDiffVis = depthDifference * (cascadeFar - cascadeNear); // 放大差值以增强可见性
                
                // 获取主光源信息
                //Light mainLight = GetMainLight(shadowCoordinates);
                
                // 从 _MainLightShadowParams 获取阴影参数
                // x: 1.0 / (1.0 + shadowDistance)
                // y: shadowDistance
                // z: CB offset (for complex light setup)
                // w: fade parameter
                //float shadowFade = mainLight.shadowAttenuation;  // 包含cascade fade
                
                // 获取光源的近远平面参数
                // _MainLightWorldToShadow[4] 的各个分量可以提供范围信息
                // 对于标准URP，近远平面通常是：

    

                // 1. 原始深度差值
                half3 result = half3(0,0,0);
                // 2. 世界空间深度差值
                result = half3(worldDepthDiffVis, worldDepthDiffVis, worldDepthDiffVis);
                
                // 3. 采样深度
                //result = half3(sampledDepth, sampledDepth, sampledDepth);
                
                // 4. 当前深度
                //result = half3(currentDepth, currentDepth, currentDepth);
                
                // 5. 差值符号（红=当前更近，蓝=采样更近）
                //result = depthDifference > 0 ? half3(1, 0, 0) : half3(0, 0, 1);
                //result = half3(depthDifference, depthDifference, depthDifference);
                
                return half4(result / 10.0f, 1.0);
            }
            
            ENDHLSL
        }

        // ShadowCaster Pass - 用于投射阴影
        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS   : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(float3(0, 1, 0));

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                output.positionCS = positionCS;

                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }
            ENDHLSL
        }

        // DepthOnly Pass - 用于前向渲染深度预处理
        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }
            ENDHLSL
        }
    }
}
