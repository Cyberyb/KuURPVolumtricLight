Shader "Custom/FogShadow"
{
    Properties
    {
        _TempDepth("BorderDepth", Float) = 10.0
        _ShadowSampleRadius("Shadow Sample Radius", Float) = 1.0
        _BlockerSearchWidth("Border Search Width", Float) = 1.0
        _BlueNoise("Blue Noise Texture", 2D) = "white" {}
        _Bias("Shadow Bias", Float) = 0.005
        _wLight("Light Size", Float) = 0.5
        _ShadowStrength("Shadow Strength", Range(0,1)) = 1.0
        _BaseColor("Base Color", Color) = (1,1,1,1)
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
            #define NUM_SAMPLES 64

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float4x4 _LightVP;
            float _ShadowSampleRadius;
            float _BlockerSearchWidth;
            float _Bias;
            float _wLight;
            float _ShadowStrength;
            Texture2D _BlueNoise;
            SamplerState sampler_BlueNoise;

            float2 disk[NUM_SAMPLES];

            //hash 二维随机
            float hash12(float2 p)
            {
                float3 p3=frac(float3(p.xyx)*0.1031);
                p3+=dot(p3,p3.yzx+33.33);
                return frac((p3.x+p3.y)*p3.z);
            }
            // 泊松圆盘分布
            void poissonDiskSamples(float2 randomSeed)
            {
                // 初始弧度
                float angle = hash12(randomSeed) * 3.1415*3.1415;
                // 初始半径
                float INV_NUM_SAMPLES = 1.0 / float( NUM_SAMPLES );
                float radius = INV_NUM_SAMPLES;
                // 一步的弧度
                float ANGLE_STEP = 3.883222077450933;
                // 一步的半径
                float radiusStep = radius;
                
                for( int i = 0; i < NUM_SAMPLES; i ++ ) 
                {
                    disk[i] = float2(cos(angle),sin(angle)) * pow( radius, 0.75 );
                    radius += radiusStep;
                    angle += ANGLE_STEP;
                }
            }

            static float2 poissonDisk[16] = {
                float2( -0.94201624, -0.39906216 ),
                float2( 0.94558609, -0.76890725 ),
                float2( -0.094184101, -0.92938870 ),
                float2( 0.34495938, 0.29387760 ),
                float2( -0.91588581, 0.45771432 ),
                float2( -0.81544232, -0.87912464 ),
                float2( -0.38277543, 0.27676845 ),
                float2( 0.97484398, 0.75648379 ),
                float2( 0.44323325, -0.97511554 ),
                float2( 0.53742981, -0.47373420 ),
                float2( -0.26496911, -0.41893023 ),
                float2( 0.79197514, 0.19090188 ),
                float2( -0.24188840, 0.99706507 ),
                float2( -0.81409955, 0.91437590 ),
                float2( 0.19984126, 0.78641367 ),
                float2( 0.14383161, -0.14100790 )
            };

            static float2 Poisson64[64] = {
                float2(-0.8750, -0.0313),
                float2(-0.6250, -0.3750),
                float2(-0.3125, -0.6875),
                float2(0.0625, -0.8125),
                float2(0.4375, -0.7500),
                float2(0.7500, -0.5625),
                float2(0.9375, -0.2500),
                float2(0.8750, 0.1875),
                float2(0.5625, 0.5000),
                float2(0.1875, 0.7500),
                float2(-0.1875, 0.8125),
                float2(-0.5625, 0.6875),
                float2(-0.8125, 0.3750),
                float2(-0.9375, -0.2500),
                float2(-0.5000, -0.7500),
                float2(0.1250, -0.4375),
                float2(0.3750, -0.1875),
                float2(-0.1250, -0.1250),
                float2(-0.3750, 0.1875),
                float2(0.0000, 0.3125),
                float2(0.6250, 0.0625),
                float2(-0.6875, -0.6250),
                float2(-0.8125, 0.0000),
                float2(0.2500, -0.6250),
                float2(0.5000, -0.4375),
                float2(0.7500, -0.1875),
                float2(0.3125, 0.1250),
                float2(-0.2500, -0.4375),
                float2(-0.5000, 0.5000),
                float2(-0.1250, 0.6250),
                float2(0.3750, 0.6875),
                float2(0.6875, 0.3750),
                float2(-0.7500, 0.6250),
                float2(-0.9375, 0.1250),
                float2(0.8125, -0.3750),
                float2(0.9375, 0.3750),
                float2(0.0625, 0.0000),
                float2(-0.4375, -0.1875),
                float2(0.1875, -0.8750),
                float2(-0.0625, -0.6875),
                float2(0.5625, -0.7500),
                float2(-0.6250, 0.1250),
                float2(-0.3125, 0.3750),
                float2(0.2500, 0.3750),
                float2(0.8125, 0.6250),
                float2(-0.8750, -0.4375),
                float2(-0.5625, -0.8125),
                float2(0.4375, 0.0000),
                float2(-0.1875, -0.8125),
                float2(0.0000, 0.8750),
                float2(-0.3750, -0.7500),
                float2(0.6250, -0.6250),
                float2(0.1250, 0.1875),
                float2(-0.2500, 0.1250),
                float2(0.3125, -0.3125),
                float2(-0.6875, 0.3125),
                float2(0.7500, 0.1250),
                float2(-0.0625, 0.3125),
                float2(0.5000, 0.2500),
                float2(-0.4375, 0.7500),
                float2(0.1875, 0.5000),
                float2(-0.8125, -0.6250),
                float2(0.9375, -0.0625),
                float2(-0.2200, -0.2200)
            };

            float Random1DTo1D(float value,float a,float b){
                //make value more random by making it bigger
                float random = frac(sin(value+b)*a);
                    return random;
            }

            float RandomBlueNoise(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_BlueNoise, sampler_BlueNoise, uv).r;
            }

            float2 RotateVec2(float2 v, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);

                return float2(v.x*c+v.y*s, -v.x*s+v.y*c);
            }

            //计算平均遮挡深度
            float2 SampleBlockerAvgDepth(float mdepth,float4 shadowCoord, ShadowSamplingData shadowSamplingData, float random)
            {
                float blockDepth = 0;
                int count = 0.0001;

                for(int i = 0; i < NUM_SAMPLES; i++)
                {
                    float2 offset = Poisson64[i];
                    offset = offset * shadowSamplingData.shadowmapSize.xy * _BlockerSearchWidth;
                    float2 sampleUV = shadowCoord.xy + offset;
                    float sampleDepth = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, 
                                                          sampler_LinearClamp, 
                                                          sampleUV);
                    if(sampleDepth - mdepth > _Bias)
                    {   
                        blockDepth += sampleDepth;
                        count += 1.0;
                    }
                }
                // int SAMPLE_RADIUS = 3; // 7x7采样区域半径
                // for(int j = -SAMPLE_RADIUS; j <= SAMPLE_RADIUS; j++)
                // {
                //     for(int k = -SAMPLE_RADIUS; k <= SAMPLE_RADIUS; k++)
                //     {
                //         float2 offset = float2(j, k) * shadowSamplingData.shadowmapSize.xy * _BlockerSearchWidth;
                //         float2 sampleUV = shadowCoord.xy + offset;
                //         float sampleDepth = SAMPLE_TEXTURE2D(_MainLightShadowmapTexture, 
                //                                               sampler_LinearClamp, 
                //                                               sampleUV);
                //         if(sampleDepth - mdepth > 0)
                //         {   
                //             blockDepth += sampleDepth;
                //             count += 1.0;
                //         }
                //     }
                // }
                return float2(blockDepth/count , count);
            }

            // 泊松圆盘PCF采样函数
            half PoissonPCF(float4 shadowCoord, half4 shadowParams, ShadowSamplingData shadowSamplingData, float sampleRadius, float random, float depthDifference)
            {
                half shadow = 0.0;
                half shadowStrength = shadowParams.x;
                int SAMPLE_RADIUS = 3; // 7x7采样区域半径
                
                // 泊松圆盘采样
                for(int i = 0; i < NUM_SAMPLES; i++)
                {
                    // 根据采样半径调整采样偏移
                    float2 offset = Poisson64[i]; // 转换为UV偏移
                    //RotateVec2(offset, random * 6.28318530718)
                    offset = offset * shadowSamplingData.shadowmapSize.xy * sampleRadius * depthDifference; // 随机旋转采样点，避免重复图案
                    float2 sampleUV = clamp(shadowCoord.xy + offset, 0.01, 0.99);
                    float3 sampleUVZ = float3(sampleUV, shadowCoord.z);
                    
                    // 采样阴影图
                    float attenuation = SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, 
                                                          sampler_LinearClampCompare, 
                                                          sampleUVZ);
                    
                    // 深度比较：采样深度 > 当前深度 = 在阴影中
                    // Reverse Z
                    shadow += attenuation;
                }
                
                // 平均采样结果
                shadow /= float(NUM_SAMPLES);
                shadow = 1 - shadow;

                shadow = shadow; // 4次幂增强对比度，使阴影边缘更柔和

                
                // 应用阴影强度
                shadow = lerp(1.0, shadow, shadowStrength);
                
                return shadow;
            }

            half VFPCF(float4 shadowCoord, half4 shadowParams, ShadowSamplingData shadowSamplingData, float sampleRadius, float random, float depthDifference)
            {
                half shadow = 0.0;
                half shadowStrength = shadowParams.x;
                
                // 泊松圆盘采样
                for(int i = 0; i < NUM_SAMPLES; i++)
                {
                    // 根据采样半径调整采样偏移
                    float depthDiffOffset = depthDifference;
                    float2 offset = Poisson64[i]; // 转换为UV偏移
                    offset = offset * shadowSamplingData.shadowmapSize.xy * sampleRadius * depthDiffOffset; // 随机旋转采样点，避免重复图案
                    float2 sampleUV = shadowCoord.xy + offset;
                    float3 sampleUVZ = float3(sampleUV, shadowCoord.z);
                    
                    // 采样阴影图
                    //float attenuation = SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, sampler_LinearClampCompare, sampleUVZ);
                    float attenuation = SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, 
                                                          sampler_LinearClampCompare, 
                                                          sampleUVZ);

                    shadow += attenuation * 3; 
                    //被遮挡时attenuation = 0
                }
                
                // 平均采样结果
                shadow = min(shadow / float(NUM_SAMPLES), 1.0);
                shadow = shadow;
                
                // 应用阴影强度
                shadow = lerp(1.0, shadow, shadowStrength);
                
                return shadow;
            }

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

                /*===================以下为实际阴影渲染代码==================================*/
                //获取随机数
                float random = Random1DTo1D(IN.positionWS.x + IN.positionWS.y, 14375.5964, 0.546);
                float blueNoiseRandom = RandomBlueNoise(IN.positionWS.xy);
                poissonDiskSamples(shadowCoordinates.xy);

                ShadowSamplingData shadowSamplingData = GetMainLightShadowSamplingData();
                half4 shadowParams = GetMainLightShadowParams();
                half shadowFade = GetMainLightShadowFade(IN.positionWS);

                //求遮挡物平均深度
                // float2 blockerData = SampleBlockerAvgDepth(currentDepth, shadowCoordinates, shadowSamplingData, random);

                // float blockerAvgDepth = blockerData.x;
                // float blockerCount = blockerData.y;

                half shadowAtten = 1;
                // float wPenumbra = (blockerAvgDepth - currentDepth)/blockerAvgDepth * _wLight;
                //depthDifference = blockerAvgDepth - currentDepth;
                if(depthDifference > 0)
                {
                    shadowAtten = VFPCF(shadowCoordinates, shadowParams, shadowSamplingData,_ShadowSampleRadius, random, depthDifference);
                }

                // 使用泊松圆盘PCF采样代替标准采样
                //shadowAtten = PoissonPCF(shadowCoordinates, shadowParams, shadowSamplingData, _ShadowSampleRadius, blueNoiseRandom,wPenumbra);
                
                //half shadowAtten = MainLightRealtimeShadow(shadowCoordinates);
                //half shadowAtten = SampleShadowmapVF(TEXTURE2D_SHADOW_ARGS(_MainLightShadowmapTexture, sampler_LinearClampCompare), shadowCoordinates, shadowSamplingData);
                //half shadow = MixRealtimeAndBakedShadows(shadowAtten, half(1.0), shadowFade);
                half shadow = shadowAtten;


                
                
                // 获取主光源信息
                //Light mainLight = GetMainLight(shadowCoordinates);
                
                // 从 _MainLightShadowParams 获取阴影参数
                // (x: shadowStrength, y: >= 1.0 if soft shadows, 0.0 otherwise, z: main light fade scale, w: main light fade bias)
                
                // 获取光源的近远平面参数
                // _MainLightWorldToShadow[4] 的各个分量可以提供范围信息
                // 对于标准URP，近远平面通常是：

                // 世界空间深度差值
                shadow = 1.0 - shadow;
                shadow *= _ShadowStrength;
                shadow = 1.0 - shadow;
                half3 result = half3(shadow, shadow, shadow);
                
                
                return half4(result, 1.0);
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
