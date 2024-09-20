Shader "KuShader/VolumeLight"
{
    Properties
    {
         _MainTex ("Texture", 2D) = "white" {}
         _BlueNosie("Texture", 2D) = "white" {}
    }



	HLSLINCLUDE

        #define MAIN_LIGHT_CALCULATE_SHADOWS  //定义阴影采样
        #define _MAIN_LIGHT_SHADOWS_CASCADE //启用级联阴影
		
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        
 
        
        
        //TEXTURE2D(_CameraDepthTexture);
        //SAMPLER(sampler_CameraDepthTexture);

        // 顶点着色器的输入
        struct a2v
        {
            uint vertexID : SV_VertexID;
            float4 positionOS : POSITION;
            float2 uv :TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // 顶点着色器的输出
        struct v2f
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float4 screen_uv: TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct v2f_Blur
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;

            float4 uv01: TEXCOORD1;
		    float4 uv23: TEXCOORD2;
		    float4 uv45: TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        
        TEXTURE2D(_GrabTexture);
        SAMPLER(sampler_GrabTexture);


        float4 _ColorTint;
        float _Intensity;
        float _PhaseG;
        float _Extinction;
        float4 _BlurOffsetX;
        float4 _BlurOffsetY;
        int _NoiseLoaded;
        float _RangeSigma;

        TEXTURE2D(_Noise);
        SAMPLER(sampler_Noise);

        //float3 _worldCameraPos;
        int _StepTimes;

        //define settings
        #define MAX_RAY_LENGTH 20
        #define random(seed) frac(sin(seed * 641.5467987313875 + 1.943856175))
        #define BLUE_NOISE
        //#define WHITE_NOISE
        //#define NO_NOISE
        
        float3 GetWorldPosition(float2 uv, float3 viewVec, out float depth, out float linearDepth)
        {
            depth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture,sampler_CameraDepthTexture,uv).r;//采样深度图
            depth = Linear01Depth(depth, _ZBufferParams); //转换为线性深度
            linearDepth = LinearEyeDepth(depth,_ZBufferParams);
            float3 viewPos = viewVec * depth; //获取实际的观察空间坐标（插值后）
            float3 worldPos = mul(unity_CameraToWorld, float4(viewPos,1)).xyz; //观察空间-->世界空间坐标
            return worldPos;
        }

        float3 GetWorldPosition2(float4 positionHCS)
        {
            /* get world space position from clip position */

            float2 UV = positionHCS.xy / _ScaledScreenParams.xy;
            #if UNITY_REVERSED_Z
            real depth = SampleSceneDepth(UV);
            #else
            real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(UV));
            #endif
            return ComputeWorldSpacePosition(UV, depth, UNITY_MATRIX_I_VP);
        }

        float3 DepthToWorldPosition(float2 screenPos)
        {
            //传入的screenPos范围在[0,1]
            float depth = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,sampler_CameraDepthTexture,screenPos),_ZBufferParams);
            
            float2 ndcPosXY = screenPos * 2 -1;
            float3 clipPos = float3(ndcPosXY.x, ndcPosXY.y, 1)* _ProjectionParams.z;

            float3 viewPos = mul(unity_CameraInvProjection, clipPos.xyzz).xyz * depth;
            float3 worldPos = mul(UNITY_MATRIX_I_V, float4(viewPos,1)).xyz;
            return worldPos;
        }

        float extinctionAt(float3 pos)
        {
            //消光系数，在均匀介质中为常数
            return _Extinction;
        }

        float Phase(float3 inL, float3 outL)
        {
            float k = 1.55 * _PhaseG - 0.55 * _PhaseG * _PhaseG * _PhaseG;
            float a = 1 - k * k;
            float cosTheta = dot(normalize(inL),normalize(outL));
            float b = 4 * 3.1415 * (1 + k * cosTheta) * (1 + k * cosTheta);
            return a/b;
        }

        float3 lightAt(float3 pos, out float3 lightDir)
        {
            lightDir = GetMainLight().direction;
            //float 
        }

        float GetLightAttenuation(float3 position)
        {
            float4 shadowPos = TransformWorldToShadowCoord(position); //把采样点的世界坐标转到阴影空间
            float intensity = MainLightRealtimeShadow(shadowPos); //进行shadow map采样
            return intensity; //返回阴影值
        }

        float GetRandomNum(float2 st)
        {
            return frac(sin(dot(st.xy,float2(12.9898,78.233)))*43758.5453123);   
        }

        float RGB2Gray(float3 color)
        {
            return 0.299 * color.r + 0.587 * color.g + 0.114 * color.b;
        }

        v2f vert(a2v v)
        {
            v2f o;
            UNITY_SETUP_INSTANCE_ID(v);

            float4 pos2 = TransformObjectToHClip(v.positionOS);

            float4 pos = GetFullScreenTriangleVertexPosition(v.vertexID);
            float2 uv = GetFullScreenTriangleTexCoord(v.vertexID);

            //o.positionCS = pos2;
            o.positionCS = pos;
            o.uv = uv;
            //计算齐次坐标下的屏幕坐标，范围[0,w]
            o.screen_uv = ComputeScreenPos(o.positionCS);
            return o;
        }

        v2f_Blur vertBlurX(a2v v)
        {
            v2f_Blur o;
            UNITY_SETUP_INSTANCE_ID(v);

            float4 pos = GetFullScreenTriangleVertexPosition(v.vertexID);
            float2 uv = GetFullScreenTriangleTexCoord(v.vertexID);

            o.positionCS = pos;
            o.uv = uv;

            o.uv01 = o.uv.xyxy + _BlurOffsetX.xyxy * float4(1, 1, -1, -1);
		    o.uv23 = o.uv.xyxy + _BlurOffsetX.xyxy * float4(1, 1, -1, -1) * 2.0;
		    o.uv45 = o.uv.xyxy + _BlurOffsetX.xyxy * float4(1, 1, -1, -1) * 3.0;

            return o;
        }

        v2f_Blur vertBlurY(a2v v)
        {
            v2f_Blur o;
            UNITY_SETUP_INSTANCE_ID(v);

            float4 pos = GetFullScreenTriangleVertexPosition(v.vertexID);
            float2 uv = GetFullScreenTriangleTexCoord(v.vertexID);

            o.positionCS = pos;
            o.uv = uv;

            o.uv01 = o.uv.xyxy + _BlurOffsetY.xyxy * float4(1, 1, -1, -1);
		    o.uv23 = o.uv.xyxy + _BlurOffsetY.xyxy * float4(1, 1, -1, -1) * 2.0;
		    o.uv45 = o.uv.xyxy + _BlurOffsetY.xyxy * float4(1, 1, -1, -1) * 3.0;

            return o;
        }

        float4 frag(v2f i) : SV_Target
        {
            float depth = 0;
            float linearDepth = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,sampler_CameraDepthTexture,i.screen_uv.xy),_ZBufferParams);;
            //float3 worldPos = GetWorldPosition(i.uv, i.viewVec, depth, linearDepth); //像素的世界坐标
            
            i.screen_uv.xy = i.screen_uv.xy / i.screen_uv.w;

            float3 worldPos = DepthToWorldPosition(i.screen_uv.xy);

            float3 startPos = _WorldSpaceCameraPos.xyz;
            //步进的方向
            float3 rayDir = normalize(worldPos - startPos);
            //光线步进的长度
            float rayLength = min(length(worldPos - startPos), MAX_RAY_LENGTH);

            float stepSize = rayLength / _StepTimes;

            float3 intensity = 0;
            float transmittance = 1;
            float randomBias = 0;

            #ifdef BLUE_NOISE
                randomBias = SAMPLE_TEXTURE2D(_Noise,sampler_LinearClamp,i.uv).r * stepSize;
            #endif
                
            #ifdef WHITE_NOISE
                randomBias = GetRandomNum(i.screen_uv) * stepSize * 0.5;
            #endif

            startPos += randomBias;
            for(float distance = 0; distance < rayLength; distance += stepSize)
            {
                float3 curPos = startPos + distance * rayDir;
                //有一种插值求法lerp(startPos, startPos + rayDir * rayLength, i);

                float3 lightDir = GetMainLight().direction;
                transmittance *= exp(-stepSize * extinctionAt(curPos));

                float atten = transmittance * GetLightAttenuation(curPos) * _Intensity * Phase(lightDir,-rayDir) * stepSize;
                float3 light = atten;
                intensity += light;
            }
            //float testAtten = GetLightAttenuation(startPos);

            //intensity /= 64;


            //return float4(intensity,1);
            //return float4(1,1,0,1);

            float4 cameraColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp,i.uv);
            return float4(intensity,1) * _ColorTint;
        }

        float4 FragGaussianBlur(v2f_Blur i): SV_Target
	    {
		    half4 color = float4(0, 0, 0, 0);
            
            float3 color3 = float3(0,0,0);
            float space_Weight[7] = {0.40, 0.15, 0.15, 0.10, 0.10, 0.05, 0.05};
            float3 sample_Color[7];
            float3 center_Color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv).rgb;
            sample_Color[0] = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv).rgb;
            sample_Color[1] = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv01.xy).rgb;
            sample_Color[2] = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv01.zw).rgb;
            sample_Color[3] = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv23.xy).rgb;
            sample_Color[4] = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv23.zw).rgb;
            sample_Color[5] = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv45.xy).rgb;
            sample_Color[6] = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv45.zw).rgb;

            float colorDistance[7];
            for(int i = 0; i < 7; i++)
            {
                float dis = sample_Color[i] - center_Color;
                colorDistance[i] = dot(dis,dis);
            }
            
            float weightSum = 0.0;
            for(int i = 0; i < 7; i++)
            {
                float3 currColor = sample_Color[i];
                float valueFactor = (-colorDistance[i])/(2 * _RangeSigma* _RangeSigma + 0.0001);
                float valueWeight = (1 / (2 * 3.1415 * _RangeSigma)) * exp(valueFactor);//权重
                weightSum += valueWeight * space_Weight[i];

                color3 += currColor * space_Weight[i] * valueWeight;
            }
		    
            if(weightSum > 0)
                color3 /= weightSum;
                
                /*
		    color += 0.40 * SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv);
		    color += 0.15 * SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv01.xy);
		    color += 0.15 * SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv01.zw);
		    color += 0.10 * SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv23.xy);
		    color += 0.10 * SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv23.zw);
		    color += 0.05 * SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv45.xy);
		    color += 0.05 * SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.uv45.zw);
		*/
            color = float4(color3,1);
		    return color;
	    }

        //合并源图和体积光
        float4 frag_blend(v2f i):SV_Target
        {
            float4 sourceColor = SAMPLE_TEXTURE2D(_GrabTexture, sampler_LinearClamp,i.uv);
            float4 bluredColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp,i.uv);
            return bluredColor + sourceColor;
        }



	ENDHLSL

	SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "ComputeWP"

            HLSLPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
            
            ENDHLSL
        }

        Pass{
            Name "BlurX"
            HLSLPROGRAM

            #pragma vertex vertBlurX
            #pragma fragment FragGaussianBlur

            ENDHLSL
            }

        Pass{
            Name "BlurY"
            HLSLPROGRAM

            #pragma vertex vertBlurY
            #pragma fragment FragGaussianBlur

            ENDHLSL
            }

        Pass{
            Name "Blend"
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag_blend

            ENDHLSL
            }
    }
}
