using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
//using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 局部体积雾数据结构体，用于传递给Shader
/// </summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct LocalFogData
{
    public Vector3 center;
    public float density;
    public Vector3 extent;
    public float extinction;
    public Vector3 albedo;
    public float padding;
    public Matrix4x4 worldToLocalMatrix;
}

/// <summary>
/// 点光源数据结构体
/// </summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct PointLightData
{
    public Vector3 position;      // 光源世界位置
    public float range;           // 光源范围
    public Vector3Int minVoxel;   // 体素范围最小值
    public int paddingA;          // 对齐填充
    public Vector3Int maxVoxel;   // 体素范围最大值
    public int paddingB;          // 对齐填充
}


public class VolumetricFogRenderPass : KuRenderPass
{
    ProfilingSampler m_ProfilingSampler = new ProfilingSampler("Kutory Volumetric Fog");
    
    // 计算着色器Pass的性能分析采样器
    private ProfilingSampler m_CSMainSampler = new ProfilingSampler("CS: Volumetric Fog Main");
    private ProfilingSampler m_CSDownsampleSampler = new ProfilingSampler("CS: Downsample Depth");
    private ProfilingSampler m_CSScatteringSampler = new ProfilingSampler("CS: Scattering Light");
    private ProfilingSampler m_CSIntegrationSampler = new ProfilingSampler("CS: Integration");
    
    private RenderTextureDescriptor textureDescriptor;
    public RTHandle textureHandle;

    private RenderTextureDescriptor blurTextureDescriptor;
    public RTHandle blurTextureHandle;

    private RenderTextureDescriptor blurTextureDescriptor2;
    public RTHandle blurTextureHandle2;

    private RenderTextureDescriptor sourceDescriptor;
    public RTHandle sourceHandle;

    private RenderTextureDescriptor downSampleDescriptor;
    public RTHandle downSampleHandle;

    //模板缓存贴图
    private RenderTextureDescriptor stencilDescriptor;
    public RTHandle stencilHandle;

    //体积雾散射积分纹理
    private RenderTextureDescriptor integratedDescriptor;
    public RTHandle integratedHandle;

    //Jitter纹理
    public Texture jitterTexture;

    //计算着色器
    public ComputeShader kucomputeShader;
    //体积RenderTexture
    public RenderTexture volumeTexture;
    //散射RenderTexture
    public RenderTexture scatteringTexture;
    //积分RenderTexture
    public RenderTexture integratedTexture;
    //History Scattering RenderTexture
    public RenderTexture prevScatteringTexture;
    public RenderTexture lowPrevScatteringTexture;
    public RenderTexture screenIntegratedTexture;
    public RenderTexture lightGridsTexture;
    private RenderTexture shadowmapTexture;

    public RenderTexture debugTexture;

    public RenderTexture debugTexture2;
    
    // 降采样深度纹理（1/8分辨率）
    private RenderTexture downsampledDepthTexture;

    private Matrix4x4 preWorldToVolume;
    private Matrix4x4 worldToVolume;

    private Matrix4x4 VP;
    
   private LocalKeyword screenIntergratedKeyword;
   private LocalKeyword temporalReprojectKeyword;
   private LocalKeyword adaptiveTAAKeyword;

    //体素雾相关参数
    private const int defaultGridPixelSize = 8;
    private const int defaultGridSizeZ = 64;
    private int gridPixelSize = defaultGridPixelSize;
    private int voxelTextureSizeX = 240;
    private int voxelTextureSizeY = 135;
    private int voxelTextureDepth = defaultGridSizeZ;

    private const RenderTextureFormat volumeRuntimeTextureFormat = RenderTextureFormat.ARGBHalf;
    private const RenderTextureFormat lightGridRuntimeTextureFormat = RenderTextureFormat.RGHalf;
    private static readonly int mainLightShadowmapTextureId = Shader.PropertyToID("_MainLightShadowmapTexture");
    private static readonly int additionalLightsShadowmapTextureId = Shader.PropertyToID("_AdditionalLightsShadowmapTexture");
    private static readonly int cameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
    
    // 局部体积雾相关常量
    public const int MAX_LOCAL_FOG_COUNT = 16;

    // 存储场景中的局部体积雾
    private List<CubeLocalFog> localFogList = new List<CubeLocalFog>();
    
    // 局部体积雾ComputeBuffer
    private ComputeBuffer localFogBuffer;
    private LocalFogData[] localFogDataArray;

    // 点光源数据相关
    public const int MAX_POINT_LIGHT_COUNT = 32;
    private List<Light> pointLightList = new List<Light>();
    private ComputeBuffer pointLightDataBuffer;
    private PointLightData[] pointLightDataArray;

    private List<VisibleLight> visibleLightList = new List<VisibleLight>();

    VolumeStack stack = VolumeManager.instance.stack;

    // Start is called before the first frame update
    public VolumetricFogRenderPass(RenderPassEvent evt, Shader shader, ComputeShader computeShader) : base(evt, shader, computeShader)
    {
        textureDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.ARGBHalf, 0);

        blurTextureDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.ARGBHalf, 0);

        blurTextureDescriptor2 = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.ARGBHalf, 0);

        sourceDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.ARGBHalf, 0);

        stencilDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.R8, 0);

        downSampleDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.ARGBHalf, 0);

        integratedDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.ARGBHalf, 0);

        if(computeShader != null)
            kucomputeShader = computeShader;

        Debug.Log("VolumeLight Create Render Pass(From VolumeLightRenderPass constructor)");
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        textureDescriptor.width = cameraTextureDescriptor.width;
        textureDescriptor.height = cameraTextureDescriptor.height;

        RenderingUtils.ReAllocateIfNeeded(ref textureHandle, textureDescriptor, FilterMode.Bilinear, name: "_VLTexture");
        /*        ConfigureTarget(textureHandle);
                ConfigureClear(ClearFlag.All, Color.clear);*/
        RenderingUtils.ReAllocateIfNeeded(ref blurTextureHandle, blurTextureDescriptor, FilterMode.Bilinear, name: "_BlurTexture");

        RenderingUtils.ReAllocateIfNeeded(ref blurTextureHandle2, blurTextureDescriptor2, FilterMode.Bilinear, name: "_BlurTexture2");

        RenderingUtils.ReAllocateIfNeeded(ref sourceHandle, sourceDescriptor, FilterMode.Bilinear, name: "_GrabTexture");

        RenderingUtils.ReAllocateIfNeeded(ref stencilHandle, stencilDescriptor, FilterMode.Point, name: "_StencilTexture");

        RenderingUtils.ReAllocateIfNeeded(ref integratedHandle, integratedDescriptor, FilterMode.Bilinear, name: "_IntegratedTexture");
        //获取Volume中保存的各项参数
        volume = stack.GetComponent<VolumeLight_Volume>();
        UpdateVolumeGridSize(cameraTextureDescriptor);

        int downSample = ((VolumeLight_Volume)volume)._DownSample.value;
        downSampleDescriptor.width = Screen.width / downSample;
        downSampleDescriptor.height = Screen.height / downSample;

        RenderingUtils.ReAllocateIfNeeded(ref downSampleHandle, downSampleDescriptor, FilterMode.Bilinear, name: "_DownSampleTexture");

        EnsureDownsampledDepthTexture();

        EnsureVolumeRuntimeTextures();
        //screenIntegratedTexture = ((VolumeLight_Volume)volume)._ScreenIntegratedTexture.value;
        jitterTexture = ((VolumeLight_Volume)volume)._JitterTexture.value;

        //设置宏
        temporalReprojectKeyword = new LocalKeyword(kucomputeShader, "USE_TEMPORAL_REPROJECTION");
        adaptiveTAAKeyword = new LocalKeyword(kucomputeShader, "USE_ADAPTIVE_TAA");
    }

    private void UpdateVolumeGridSize(RenderTextureDescriptor cameraTextureDescriptor)
    {
        var volumeSettings = (VolumeLight_Volume)volume;
        gridPixelSize = Mathf.Max(1, volumeSettings != null ? volumeSettings._GridPixelSize.value : defaultGridPixelSize);
        voxelTextureSizeX = Mathf.Max(1, Mathf.CeilToInt(cameraTextureDescriptor.width / (float)gridPixelSize));
        voxelTextureSizeY = Mathf.Max(1, Mathf.CeilToInt(cameraTextureDescriptor.height / (float)gridPixelSize));
        voxelTextureDepth = Mathf.Max(1, volumeSettings != null ? volumeSettings._GridSizeZ.value : defaultGridSizeZ);
    }

    private void EnsureDownsampledDepthTexture()
    {
        if (downsampledDepthTexture != null &&
            downsampledDepthTexture.IsCreated() &&
            downsampledDepthTexture.width == voxelTextureSizeX &&
            downsampledDepthTexture.height == voxelTextureSizeY)
        {
            return;
        }

        if (downsampledDepthTexture != null)
        {
            downsampledDepthTexture.Release();
        }

        downsampledDepthTexture = new RenderTexture(voxelTextureSizeX, voxelTextureSizeY, 0, RenderTextureFormat.RFloat)
        {
            enableRandomWrite = true,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "DownsampledDepth"
        };
        downsampledDepthTexture.Create();
    }

    private void EnsureVolumeRuntimeTextures()
    {
        EnsureVolumeRuntimeTexture(ref volumeTexture, "_VolumeAttributeTexture", volumeRuntimeTextureFormat);
        EnsureVolumeRuntimeTexture(ref scatteringTexture, "_VolumeScatteringTexture", volumeRuntimeTextureFormat);
        EnsureVolumeRuntimeTexture(ref integratedTexture, "_VolumeIntegratedTexture", volumeRuntimeTextureFormat);
        EnsureVolumeRuntimeTexture(ref prevScatteringTexture, "_VolumePrevScatteringTexture", volumeRuntimeTextureFormat);
        EnsureVolumeRuntimeTexture(ref lightGridsTexture, "_VolumeLightGridTexture", lightGridRuntimeTextureFormat, FilterMode.Point);
        EnsureVolumeRuntimeTexture(ref debugTexture, "_VolumeDebugTexture", volumeRuntimeTextureFormat);
        EnsureVolumeRuntimeTexture(ref debugTexture2, "_VolumeDebugTexture2", volumeRuntimeTextureFormat);
    }

    private void EnsureVolumeRuntimeTexture(
        ref RenderTexture texture,
        string textureName,
        RenderTextureFormat textureFormat,
        FilterMode filterMode = FilterMode.Bilinear)
    {
        RenderTextureFormat actualFormat = SystemInfo.SupportsRenderTextureFormat(textureFormat)
            ? textureFormat
            : volumeRuntimeTextureFormat;

        if (texture != null &&
            texture.IsCreated() &&
            texture.width == voxelTextureSizeX &&
            texture.height == voxelTextureSizeY &&
            texture.volumeDepth == voxelTextureDepth &&
            texture.format == actualFormat &&
            texture.dimension == TextureDimension.Tex3D &&
            texture.enableRandomWrite)
        {
            return;
        }

        if (texture != null)
        {
            texture.Release();
        }

        texture = new RenderTexture(voxelTextureSizeX, voxelTextureSizeY, 0, actualFormat)
        {
            name = textureName,
            dimension = TextureDimension.Tex3D,
            volumeDepth = voxelTextureDepth,
            enableRandomWrite = true,
            filterMode = filterMode,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false
        };
        texture.Create();
    }

    protected override void Render(CommandBuffer cmd, ref RenderingData renderingData)
    {
        using (new ProfilingScope(cmd, m_ProfilingSampler))
        {
            //获取volume数据
            //var stack = VolumeManager.instance.stack;
            //volume = stack.GetComponent<VolumeLight_Volume>();
            if (!((VolumeLight_Volume)volume).IsActive())
                return;

            if(((VolumeLight_Volume)volume)._UseFroxel.value)
            {
                RenderVoxelFog(cmd, ref renderingData);
            }
            else
            {
                RenderRaymarchingFog(cmd, ref renderingData);
            }
            // Debug.Log("preVP Matrix:\n" + preVP.ToString("F4"));
            // Debug.Log("VP Matrix:\n" + VP.ToString("F4"));
            preWorldToVolume = worldToVolume;
        }
        

    }

    protected void RenderRaymarchingFog(CommandBuffer cmd, ref RenderingData renderingData)
    {
        //获取相机世界位置
        Vector3 cameraWorldPos = renderingData.cameraData.worldSpaceCameraPos;

        RTHandle cameraTargetHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;


        UpdateValues(ref cmd, ref renderingData);


        /*Vertex&Pixel Shader部分*/
        //第一步，获取光照区域的stencil
        //Blit(cmd, cameraTargetHandle, stencilHandle, material, 0);

        Blit(cmd, cameraTargetHandle, sourceHandle);


        //Blit(cmd, cameraTargetHandle, textureHandle, material, 1);
        Blit(cmd, cameraTargetHandle, downSampleHandle, material, 1);

        //3次高斯模糊
        for (int i = 0; i < 3; i++)
        {
            //水平
            //Blitter.BlitCameraTexture(cmd, textureHandle, blurTextureHandle, material, 1);
            Blit(cmd, downSampleHandle, blurTextureHandle, material, 2);
            //垂直
            Blit(cmd, blurTextureHandle, blurTextureHandle2, material, 3);
        }


        Blit(cmd, blurTextureHandle2, cameraTargetHandle, material, 4);

        //RTHandleRealse();
    }

    protected void RenderVoxelFog(CommandBuffer cmd, ref RenderingData renderingData)
    {

        //获取相机世界位置
        Vector3 cameraWorldPos = renderingData.cameraData.worldSpaceCameraPos;

        RTHandle cameraTargetHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;
            
        UpdateComputeValues(ref cmd, ref renderingData);
        UpdateValues(ref cmd, ref renderingData);

        /*Compute Shader部分*/
        //CS1: 存放属性
        int kernelIndex = kucomputeShader.FindKernel("CSMain");

        kucomputeShader.SetTexture(kernelIndex, "_OutputAttribute", volumeTexture);
        kucomputeShader.SetTexture(kernelIndex,"_LightGridsTexture", lightGridsTexture);
        SetComputeShadowTextures(kernelIndex);
        
        using (new ProfilingScope(cmd, m_CSMainSampler))
        {
            kucomputeShader.Dispatch(kernelIndex, (voxelTextureSizeX + 7) / 8, (voxelTextureSizeY + 7) / 8, (voxelTextureDepth + 7) / 8);
        }

        //CS1.5：相机深度下采样（可选，视性能需求而定）
        // 在 RenderVoxelFog 中，CSMain 和 CSScatteringLight 之间添加
        int downsampleIndex = kucomputeShader.FindKernel("CSDownsampleDepth");
        SetComputeCameraDepthTexture(downsampleIndex, ref renderingData);
        kucomputeShader.SetTexture(downsampleIndex, "_DownsampledDepth", downsampledDepthTexture); // 需要创建这个纹理
        
        using (new ProfilingScope(cmd, m_CSDownsampleSampler))
        {
            kucomputeShader.Dispatch(downsampleIndex, (voxelTextureSizeX + 7) / 8, (voxelTextureSizeY + 7) / 8, 1);
        }

        //CS2: 计算散射
        int scatteringIndex = kucomputeShader.FindKernel("CSScatteringLight");
        kucomputeShader.SetTexture(scatteringIndex, "_InDownsampledDepth", downsampledDepthTexture);
        kucomputeShader.SetTexture(scatteringIndex, "_LightGridsTexture", lightGridsTexture);
        kucomputeShader.SetTexture(scatteringIndex, "_InputAttribute", volumeTexture);
        kucomputeShader.SetTexture(scatteringIndex, "_OutputScatteringLight", scatteringTexture);
        kucomputeShader.SetTexture(scatteringIndex, "_PrevScatteringLight", prevScatteringTexture);
        SetComputeShadowTextures(scatteringIndex);
        SetComputeCameraDepthTexture(scatteringIndex, ref renderingData);

        kucomputeShader.SetTexture(scatteringIndex, "_DebugTexture", debugTexture);
        kucomputeShader.SetTexture(scatteringIndex, "_DebugTexture2", debugTexture2);
        
        using (new ProfilingScope(cmd, m_CSScatteringSampler))
        {
            kucomputeShader.Dispatch(scatteringIndex, (voxelTextureSizeX + 7) / 8, (voxelTextureSizeY + 7) / 8, (voxelTextureDepth + 7) / 8);
        }


        cmd.CopyTexture(scatteringTexture, prevScatteringTexture);

        //CS3: 计算积分
        int integrationIndex = kucomputeShader.FindKernel("CSIntegration");
        kucomputeShader.SetTexture(integrationIndex, "_InputScatteringLight", scatteringTexture);
        kucomputeShader.SetTexture(integrationIndex, "_OutputIntegrated", integratedTexture);

        using (new ProfilingScope(cmd, m_CSIntegrationSampler))
        {
            kucomputeShader.Dispatch(integrationIndex, (voxelTextureSizeX + 7) / 8, (voxelTextureSizeY + 7) / 8, 1);
        }
        /*Vertex&Pixel Shader部分*/
        Blit(cmd, cameraTargetHandle, sourceHandle);
        
        Blit(cmd, cameraTargetHandle, integratedHandle, material, 6);

        Blit(cmd, sourceHandle, cameraTargetHandle, material, 5);
        //cmd.Blit(sourceHandle, cameraTargetHandle, material, 5);

        //RTHandleRealse();
    }

    private void SetComputeShadowTextures(int kernelIndex)
    {
        Texture mainLightShadowmap = Shader.GetGlobalTexture(mainLightShadowmapTextureId);
        kucomputeShader.SetTexture(kernelIndex, mainLightShadowmapTextureId, mainLightShadowmap != null ? mainLightShadowmap : Texture2D.whiteTexture);

        Texture additionalLightsShadowmap = Shader.GetGlobalTexture(additionalLightsShadowmapTextureId);
        kucomputeShader.SetTexture(kernelIndex, additionalLightsShadowmapTextureId, additionalLightsShadowmap != null ? additionalLightsShadowmap : Texture2D.whiteTexture);
    }

    private void SetComputeCameraDepthTexture(int kernelIndex, ref RenderingData renderingData)
    {
        kucomputeShader.SetTexture(kernelIndex, cameraDepthTextureId, renderingData.cameraData.renderer.cameraDepthTargetHandle);
    }


    protected override void Init()
    {
        // 收集场景中所有的CubeLocalFog组件
        CollectLocalFogs();
        
        // 收集场景中所有的点光源
        //CollectPointLights();
    }
    
    /// <summary>
    /// 收集场景中所有的点光源
    /// </summary>
    private void CollectPointLights()
    {
        pointLightList.Clear();
        
        // 使用 Light.GetLights 代替 FindObjectsOfType，效率更高
        Light[] allLights = Light.GetLights(LightType.Point, -1);
        
        int pointLightCount = 0;
        foreach (Light light in allLights)
        {
            // 只收集启用的点光源
            if (light.enabled && pointLightCount < MAX_POINT_LIGHT_COUNT)
            {
                pointLightList.Add(light);
                pointLightCount++;
            }
        }
        
        if (allLights.Length > MAX_POINT_LIGHT_COUNT)
        {
            Debug.LogWarning($"场景中的点光源数量({allLights.Length})超过最大支持数量({MAX_POINT_LIGHT_COUNT})，将只使用前{MAX_POINT_LIGHT_COUNT}个");
        }
        
        Debug.Log($"已收集{pointLightCount}个点光源");
    }
    
    /// <summary>
    /// 使用RenderingData中的光源数据收集点光源（更高效，推荐使用）
    /// </summary>
    private void CollectPointLightsFromRenderingData(RenderingData renderingData)
    {
        visibleLightList.Clear();
        
        // 从 URP 的光源数据中获取额外光源数量
        int additionalLightsCount = renderingData.lightData.additionalLightsCount;

        var visibleLights = renderingData.lightData.visibleLights;
        
        int pointLightCount = 0;
        int spotLightCount = 0;
        foreach (var light in visibleLights)
        {
            if(light.lightType == LightType.Point && pointLightCount < MAX_POINT_LIGHT_COUNT)
            {
                visibleLightList.Add(light);
                pointLightCount++;
            }
            if(light.lightType == LightType.Spot)
            {
                spotLightCount++;
            }
        }
        
        if (visibleLights.Length > MAX_POINT_LIGHT_COUNT)
        {
            Debug.LogWarning($"场景中的点光源数量({visibleLights.Length})超过最大支持数量({MAX_POINT_LIGHT_COUNT})，将只使用前{MAX_POINT_LIGHT_COUNT}个");
        }
        
        //Debug.Log($"从RenderingData收集{pointLightCount}个点光源，{spotLightCount}个聚光灯 (URP额外光源数: {additionalLightsCount})");
    }
    
    private void UpdatePointLightData()
    {
        int lightCount = visibleLightList.Count;
        
        if (lightCount == 0)
        {
            return;
        }
        
        // 初始化数组（如果还没有或大小不匹配）
        if (pointLightDataArray == null || pointLightDataArray.Length != lightCount)
        {
            pointLightDataArray = new PointLightData[lightCount];
        }
        
        // 填充数组数据
        for (int i = 0; i < lightCount; i++)
        {
            VisibleLight visibleLight = visibleLightList[i];
            Vector3 lightPos = visibleLight.light.transform.position;
            float lightRange = visibleLight.range;
            
            pointLightDataArray[i] = new PointLightData
            {
                position = lightPos,
                range = lightRange,
                minVoxel = Vector3Int.zero,
                maxVoxel = Vector3Int.zero,
                paddingA = 0,
                paddingB = 0
            };
        }
        
        // ✨ 注意：不在这里调用 SetData()，等待体素范围数据更新完成后再统一提交
    }
    
    /// <summary>
    /// 更新点光源体素范围数据（仅更新数组，不提交到GPU）
    /// </summary>
    private void UpdatePointLightVoxelData(Matrix4x4 worldToVolume, Vector4 encodingParams)
    {
        int lightCount = visibleLightList.Count;
        
        if (lightCount == 0 || pointLightDataArray == null)
        {
            return;
        }
        
        // 为每个光源计算体素范围
        for (int i = 0; i < lightCount; i++)
        {
            var (minVoxel, maxVoxel) = GetLightVoxelRange(i, worldToVolume, encodingParams);
            
            // 更新数组中的体素范围数据
            pointLightDataArray[i].minVoxel = minVoxel;
            pointLightDataArray[i].maxVoxel = maxVoxel;
        }
        
        // ✨ 注意：不在这里调用 SetData()，由调用者负责统一提交
    }
    
    /// <summary>
    /// 计算光源在视锥体素空间中的体素范围
    /// </summary>
    private (Vector3Int minVoxel, Vector3Int maxVoxel) GetLightVoxelRange(int lightIndex, Matrix4x4 worldToVolume, Vector4 encodingParams)
    {
        if (lightIndex < 0 || lightIndex >= pointLightDataArray.Length)
            return (Vector3Int.zero, Vector3Int.zero);
        
        //处理点光源
        PointLightData lightData = pointLightDataArray[lightIndex];
        Vector3 cameraPos = Camera.main.transform.position;
        Vector3 cameraUp = Camera.main.transform.up;
        Vector3 cameraForward = Camera.main.transform.forward;
        
        Vector3 lightToCamera = Vector3.Normalize(-cameraForward);
        Vector3 lightRight = Vector3.Normalize(Vector3.Cross(cameraUp, lightToCamera));
        Vector3 lightUp = Vector3.Normalize(cameraUp);
        Vector3 aabbMin = lightData.position + lightRight * lightData.range - lightUp * lightData.range + lightToCamera * lightData.range;
        //Debug.Log("aabbMin:" + aabbMin);
        Vector3 aabbMax = lightData.position - lightRight * lightData.range + lightUp * lightData.range - lightToCamera * lightData.range;
        
        Vector3Int minVoxel = GetPointVoxelIndex(aabbMin, worldToVolume, encodingParams);
        Vector3Int maxVoxel = GetPointVoxelIndex(aabbMax, worldToVolume, encodingParams);
        
        return (minVoxel, maxVoxel);
        //return (Vector3Int.zero, Vector3Int.zero);
    }
    

    /// <summary>
    /// 计算单个坐标在体素空间中的体素索引
    /// </summary>
    private Vector3Int GetPointVoxelIndex(Vector3 worldPos, Matrix4x4 worldToVolume, Vector4 encodingParams)
    {
        Vector4 clipPos = worldToVolume * new Vector4(worldPos.x, worldPos.y, worldPos.z, 1);
        float z = EncodeLogarithmicDepthGeneralized(clipPos.w, encodingParams);
        float ndcX = clipPos.x / clipPos.w;
        float ndcY = clipPos.y / clipPos.w;
        Vector3 uvz = new Vector3(clipPos.x / clipPos.w * 0.5f + 0.5f, clipPos.y / clipPos.w * 0.5f + 0.5f, z);
        Vector3 volumePos = new Vector3(uvz.x, uvz.y, uvz.z);
        Vector3Int voxelIndex = Vector3Int.FloorToInt(Vector3.Scale(volumePos, new Vector3(voxelTextureSizeX, voxelTextureSizeY, voxelTextureDepth)));
        // 钳制到有效范围
        voxelIndex = Vector3Int.Max(voxelIndex, Vector3Int.zero);
        voxelIndex = Vector3Int.Min(voxelIndex, new Vector3Int(voxelTextureSizeX, voxelTextureSizeY, voxelTextureDepth) - Vector3Int.one);
        return voxelIndex;
    }

    /// <summary>
    /// 获取所有光源的体素范围（调试用）
    /// </summary>
    public void DebugPrintLightVoxelRanges(Matrix4x4 worldToVolume, Vector4 encodingParams)
    {
        //Debug.Log($"========== 点光源体素范围调试信息 ==========");
        for (int i = 0; i < pointLightDataArray.Length; i++)
        {
            var (minVoxel, maxVoxel) = GetLightVoxelRange(i, worldToVolume, encodingParams);
            PointLightData lightData = pointLightDataArray[i];
            Debug.Log($" 光源 {i}: 位置={lightData.position}, 范围={lightData.range}, 体素范围: Min={minVoxel}, Max={maxVoxel}");
        }
        //Debug.Log($"==========================================");
    }
    private void CollectLocalFogs()
    {
        localFogList.Clear();
        CubeLocalFog[] allLocalFogs = UnityEngine.Object.FindObjectsOfType<CubeLocalFog>();
        
        if (allLocalFogs.Length > MAX_LOCAL_FOG_COUNT)
        {
            Debug.LogWarning($"场景中的CubeLocalFog数量({allLocalFogs.Length})超过最大支持数量({MAX_LOCAL_FOG_COUNT})，将只使用前{MAX_LOCAL_FOG_COUNT}个");
        }
        
        for (int i = 0; i < Mathf.Min(allLocalFogs.Length, MAX_LOCAL_FOG_COUNT); i++)
        {
            localFogList.Add(allLocalFogs[i]);
        }
    }
    private void UpdateValues(ref CommandBuffer cmd, ref RenderingData renderingData)
    {
        material.SetFloat("_Intensity", ((VolumeLight_Volume)volume)._LightIntensity.value);
        material.SetColor("_ColorTint", ((VolumeLight_Volume)volume)._LightTint.value);
        material.SetInt("_StepTimes", ((VolumeLight_Volume)volume)._StepTimes.value);
        material.SetFloat("_PhaseG", ((VolumeLight_Volume)volume)._PhaseG.value);
        material.SetFloat("_Extinction", ((VolumeLight_Volume)volume)._Extinction.value);
        material.SetFloat("_RangeSigma", ((VolumeLight_Volume)volume)._RangeSigma.value);

        material.SetVector("_BlurOffsetX", new Vector4(((VolumeLight_Volume)volume)._BlurSize.value / Screen.width, 0, 0, 0));
        material.SetVector("_BlurOffsetY", new Vector4(0, ((VolumeLight_Volume)volume)._BlurSize.value / Screen.height, 0, 0));
        

        material.SetTexture("_GrabTexture", sourceHandle);
        material.SetTexture("_StencilTexture", stencilHandle);

        // 设置体积纹理采样为双线性插值（3D纹理下为三线性）
        if (integratedTexture != null)
        {
            integratedTexture.filterMode = FilterMode.Bilinear;
            material.SetTexture("_VolumeTexture", integratedTexture);
        }

        material.SetTexture("_JitterTexture", jitterTexture);
        material.SetTexture("_ScreenIntegrated", integratedHandle);

        material.SetFloat("_FovY", renderingData.cameraData.camera.fieldOfView);
        material.SetFloat("_Aspect", renderingData.cameraData.camera.aspect);
        material.SetFloat("_Farplane", ((VolumeLight_Volume)volume)._FarPlane.value);
        material.SetFloat("_Nearplane", renderingData.cameraData.camera.nearClipPlane);
    
        //宏定义
        //material.SetKeyword(screenIntergratedKeyword, ((VolumeLight_Volume)volume)._UseScreenIntergrated.value);
    }
    private void UpdateComputeValues(ref CommandBuffer cmd, ref RenderingData renderingData)
    {
        //深度相关参数计算
        float farClip = ((VolumeLight_Volume)volume)._FarPlane.value;
        float nearClip = renderingData.cameraData.camera.nearClipPlane;
        float cameraFarClip = renderingData.cameraData.camera.farClipPlane;
        Vector4 zParam = GetZParam(nearClip, farClip);
        GetInverseVP(renderingData.cameraData.camera, nearClip, farClip, out var inverseV, out var volumeToWorld, ref worldToVolume);
        //material.SetMatrix("_InvV", renderingData.cameraData.camera.worldToCameraMatrix.inverse);
        //material.SetMatrix("_InvP", renderingData.cameraData.camera.projectionMatrix.inverse);
        float c = ((VolumeLight_Volume)volume)._DepthFactor.value;
        Vector4 logarithmicDepthDecodingParams = ComputeLogarithmicDepthDecodingParams(nearClip, farClip, c);
        Vector4 logarithmicDepthEncodingParams = ComputeLogarithmicDepthEncodingParams(nearClip, farClip, c);

        //Jitter参数计算
        int frameNumber = Time.renderedFrameCount;
        Vector3 frameJitterValue = VolumetricFogTemporalRandom(frameNumber);
        cmd.SetGlobalFloat("_JitterWeight", ((VolumeLight_Volume)volume)._JitterWeight.value);
        cmd.SetGlobalFloat("_FrameNumberMod8", frameNumber % 8);
        cmd.SetGlobalVector("_FrameJitterValue", frameJitterValue);
        cmd.SetGlobalFloat("_ReprojectWeight", ((VolumeLight_Volume)volume)._ReprojectWeight.value);
        cmd.SetGlobalMatrix("_World2Volume", worldToVolume);
        cmd.SetGlobalMatrix("_PrevVP", preWorldToVolume);
        cmd.SetGlobalMatrix("_InverseV", inverseV);
        cmd.SetGlobalMatrix("_InverseVP", volumeToWorld);
        cmd.SetGlobalFloat("_Farplane", farClip);
        cmd.SetGlobalMatrix("_VP", renderingData.cameraData.camera.projectionMatrix * renderingData.cameraData.camera.worldToCameraMatrix);
        //cmd.SetComputeFloatParam(kucomputeShader, "_Farplane", farClip);
        cmd.SetComputeFloatParam(kucomputeShader, "_Nearplane", nearClip);
        cmd.SetGlobalVector("_VolumeSize", new Vector4(voxelTextureSizeX, voxelTextureSizeY, voxelTextureDepth, 1));
        cmd.SetGlobalInt("_GridPixelSize", gridPixelSize);
        cmd.SetGlobalVector("_ZParam", zParam);
        cmd.SetGlobalVector("_LogarithmicDepthDecodingParams", logarithmicDepthDecodingParams);
        cmd.SetGlobalVector("_LogarithmicDepthEncodingParams", logarithmicDepthEncodingParams);
        cmd.SetGlobalVector("_VolumeColor", ((VolumeLight_Volume)volume)._VolumeColor.value);
        cmd.SetComputeFloatParam(kucomputeShader, "_GlobalFogDensity", ((VolumeLight_Volume)volume)._GlobalFogDensity.value);
        cmd.SetComputeFloatParam(kucomputeShader, "_HeightFallOff", ((VolumeLight_Volume)volume)._HeightFallOff.value);
        cmd.SetComputeFloatParam(kucomputeShader, "_FogBaseHeight", ((VolumeLight_Volume)volume)._FogBaseHeight.value);

        cmd.SetComputeFloatParam(kucomputeShader, "_FovY", renderingData.cameraData.camera.fieldOfView);
        cmd.SetComputeFloatParam(kucomputeShader, "_Aspect", renderingData.cameraData.camera.aspect);
 
        cmd.SetComputeFloatParam(kucomputeShader, "_SunLightScatterIntensity", ((VolumeLight_Volume)volume)._SunLightScatterIntensity.value);
        cmd.SetComputeFloatParam(kucomputeShader, "_AddLightScatterIntensity", ((VolumeLight_Volume)volume)._AddLightScatterIntensity.value);

        cmd.SetComputeVectorParam(kucomputeShader, "_GlobalScatter", ((VolumeLight_Volume)volume)._GlobalScatter.value);
        cmd.SetComputeFloatParam(kucomputeShader, "_GlobalAbsorb", ((VolumeLight_Volume)volume)._GlobalAbsorb.value);
        cmd.SetComputeVectorParam(kucomputeShader, "_GlobalAlbedo", ((VolumeLight_Volume)volume)._GlobalAlbedo.value);
        cmd.SetComputeFloatParam(kucomputeShader, "_GlobalExtinction", ((VolumeLight_Volume)volume)._GlobalExtinction.value);
        cmd.SetComputeFloatParam(kucomputeShader, "_PhaseG", ((VolumeLight_Volume)volume)._PhaseG.value);
        cmd.SetComputeFloatParam(kucomputeShader, "_MinWeight", VolumeUIControl.RuntimeMinWeight);
        //设置宏
        kucomputeShader.SetKeyword(temporalReprojectKeyword, ((VolumeLight_Volume)volume)._UseTemporalReproject.value);
        //kucomputeShader.SetKeyword(adaptiveTAAKeyword, VolumeUIControl.RuntimeUseAdaptiveTAA);
        kucomputeShader.SetKeyword(adaptiveTAAKeyword, ((VolumeLight_Volume)volume)._UseAdaptiveTAA.value);
        // 设置局部体积雾参数
        SetLocalFogParameters(cmd);
        
        // 设置点光源参数（一次性收集、计算、提交完整数据）
        SetPointLightParameters(cmd, renderingData, worldToVolume, logarithmicDepthEncodingParams);

        //DebugPrintLightVoxelRanges(worldToVolume,logarithmicDepthEncodingParams);
    }
    
    /// <summary>
    /// 将点光源参数传递给ComputeShader（推荐：使用RenderingData版本）
    /// </summary>
    private void SetPointLightParameters(CommandBuffer cmd, RenderingData renderingData, Matrix4x4 worldToVolume, Vector4 encodingParams)
    {
        // 使用 RenderingData 中的光源数据（推荐方式）
        CollectPointLightsFromRenderingData(renderingData);
        
        int lightCount = visibleLightList.Count;

        // 重新创建ComputeBuffer（仅在大小改变时）
        if (pointLightDataBuffer == null || pointLightDataBuffer.count != lightCount)
        {
            pointLightDataBuffer?.Release();
            int bufferSize = Mathf.Max(1, lightCount); // 确保至少有一个元素，避免Create时出错
            pointLightDataBuffer = new ComputeBuffer(bufferSize, System.Runtime.InteropServices.Marshal.SizeOf(typeof(PointLightData)));
        }
        
        // 如果点光源的数量为0，则不需要设置
        if (lightCount > 0)
        {
            // 更新基础数据（位置和范围）
            UpdatePointLightData();
            
            // 计算体素范围数据
            UpdatePointLightVoxelData(worldToVolume, encodingParams);
            
            // ✨ 所有数据准备完毕后，一次性提交到GPU
            if (pointLightDataBuffer != null)
            {
                pointLightDataBuffer.SetData(pointLightDataArray, 0, 0, lightCount);
            }
        }
        
        // 传递ComputeBuffer和数量到Shader
        int kernelIndex = kucomputeShader.FindKernel("CSMain");
        cmd.SetComputeBufferParam(kucomputeShader, kernelIndex, "_PointLightData", pointLightDataBuffer);
        cmd.SetComputeIntParam(kucomputeShader, "_PointLightCount", lightCount);
    }
    
    /// <summary>
    /// 将点光源数据参数传递给ComputeShader（备用：仅使用Light.GetLights）
    /// </summary>
    private void SetPointLightParametersSimple(CommandBuffer cmd, Matrix4x4 worldToVolume, Vector4 encodingParams)
    {
        // 重新收集点光源（支持运行时添加/删除）
        CollectPointLights();
        
        int lightCount = pointLightList.Count;
        
        // 如果点光源的数量为0，则不需要设置
        if (lightCount == 0)
        {
            cmd.SetComputeIntParam(kucomputeShader, "_PointLightCount", 0);
            return;
        }
        
        // 初始化数据数组（如果还没有或大小不匹配）
        if (pointLightDataArray == null || pointLightDataArray.Length != lightCount)
        {
            pointLightDataArray = new PointLightData[lightCount];
        }
        
        // 填充基础数据
        for (int i = 0; i < lightCount; i++)
        {
            Light light = pointLightList[i];
            Vector3 lightPos = light.transform.position;
            float lightRange = light.range;
            
            pointLightDataArray[i] = new PointLightData
            {
                position = lightPos,
                range = lightRange,
                minVoxel = Vector3Int.zero,
                maxVoxel = Vector3Int.zero,
                paddingA = 0,
                paddingB = 0
            };
        }
        
        // 重新创建ComputeBuffer（如果大小改变）
        if (pointLightDataBuffer == null || pointLightDataBuffer.count != lightCount)
        {
            pointLightDataBuffer?.Release();
            pointLightDataBuffer = new ComputeBuffer(lightCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(PointLightData)));
        }
        
        // 计算体素范围数据
        UpdatePointLightVoxelData(worldToVolume, encodingParams);
        
        // ✨ 一次性提交完整数据到GPU
        pointLightDataBuffer.SetData(pointLightDataArray, 0, 0, lightCount);
        
        // 传递ComputeBuffer和数量到Shader
        int kernelIndex = kucomputeShader.FindKernel("CSScatteringLight");
        cmd.SetComputeBufferParam(kucomputeShader, kernelIndex, "_PointLightData", pointLightDataBuffer);
        cmd.SetComputeIntParam(kucomputeShader, "_PointLightCount", lightCount);
    }
    
    /// <summary>
    /// 获取点光源数据（用于调试或其他用途）
    /// </summary>
    public PointLightData[] GetPointLightData()
    {
        return pointLightDataArray;
    }
    
    /// <summary>
    /// 获取点光源数量
    /// </summary>
    public int GetPointLightCount()
    {
        return visibleLightList.Count;
    }
    
    /// <summary>
    /// 将点光源数据打印到日志（调试用）
    /// </summary>
    public void DebugPrintPointLights()
    {
        Debug.Log($"========== 点光源数据调试信息 ==========");
        Debug.Log($"总光源数量: {visibleLightList.Count}");
        
        for (int i = 0; i < visibleLightList.Count; i++)
        {
            PointLightData lightData = pointLightDataArray[i];
            Debug.Log($"光源 {i}: 位置={lightData.position}, 范围={lightData.range}");
        }
        Debug.Log($"==========================================");
    }
    
    /// <summary>
    /// 将局部体积雾参数传递给Shader
    /// </summary>
    private void SetLocalFogParameters(CommandBuffer cmd)
    {
        // 重新收集本地雾（支持运行时添加/删除）
        CollectLocalFogs();
        
        int fogCount = localFogList.Count;

        // 重新创建ComputeBuffer（如果大小改变）
        if (localFogBuffer == null || localFogBuffer.count != fogCount)
        {
            localFogBuffer?.Release();
            int bufferSize = Mathf.Max(1, fogCount);
            localFogBuffer = new ComputeBuffer(bufferSize, System.Runtime.InteropServices.Marshal.SizeOf(typeof(LocalFogData)));
        }
        
        // 如果雾的数量为0，则不需要设置
        if (fogCount > 0)
        {
            // 初始化数组（如果还没有或大小不匹配）
            if (localFogDataArray == null || localFogDataArray.Length != fogCount)
            {
                localFogDataArray = new LocalFogData[fogCount];
            }
            
            // 填充数组数据
            for (int i = 0; i < fogCount; i++)
            {
                LocalFog fog = localFogList[i].GetLocalFog();
                if (fog == null)
                    continue;
                
                localFogDataArray[i] = new LocalFogData
                {
                    center = fog.center,
                    density = fog.density,
                    extent = fog.extent,
                    extinction = fog.extinction,
                    albedo = fog.albedo,
                    padding = 0,
                    worldToLocalMatrix = fog.worldToLocalMatrix
                };
            }
            
            // 设置ComputeBuffer数据
            localFogBuffer.SetData(localFogDataArray, 0, 0, fogCount);
        }
        

        
        // 传递ComputeBuffer和数量到Shader
        cmd.SetComputeBufferParam(kucomputeShader, kucomputeShader.FindKernel("CSMain"), "_LocalFogData", localFogBuffer);
        cmd.SetComputeIntParam(kucomputeShader, "_LocalFogCount", fogCount);
    }
    private void RTHandleRealse()
    {
        if (sourceHandle != null) { sourceHandle.Release(); }
        if (textureHandle != null) { textureHandle.Release(); }
        if (blurTextureHandle != null) { blurTextureHandle.Release(); }
        if (blurTextureHandle2 != null) { blurTextureHandle2.Release(); }
        if (stencilHandle != null) { stencilHandle.Release(); }
        if (downSampleHandle != null) { downSampleHandle.Release(); }
        ReleaseVolumeRuntimeTextures();
        if (downsampledDepthTexture != null)
        {
            downsampledDepthTexture.Release();
            downsampledDepthTexture = null;
        }
        
        // 释放局部体积雾ComputeBuffer
        if (localFogBuffer != null)
        {
            localFogBuffer.Release();
            localFogBuffer = null;
        }

        if (pointLightDataBuffer != null)
        {
            pointLightDataBuffer.Release();
            pointLightDataBuffer = null;
        }
    }

    private void ReleaseVolumeRuntimeTextures()
    {
        ReleaseVolumeRuntimeTexture(ref volumeTexture);
        ReleaseVolumeRuntimeTexture(ref scatteringTexture);
        ReleaseVolumeRuntimeTexture(ref integratedTexture);
        ReleaseVolumeRuntimeTexture(ref prevScatteringTexture);
        ReleaseVolumeRuntimeTexture(ref lightGridsTexture);
        ReleaseVolumeRuntimeTexture(ref debugTexture);
        ReleaseVolumeRuntimeTexture(ref debugTexture2);
    }

    private static void ReleaseVolumeRuntimeTexture(ref RenderTexture texture)
    {
        if (texture == null)
            return;

        texture.Release();
        texture = null;
    }

    public override void Dispose()
    {
        RTHandleRealse();
    }

 /*   public override void OnCameraCleanup(CommandBuffer cmd)
    {
        base.OnCameraCleanup(cmd);
        //RTHandleRealse();
    }*/


    //一些工具函数
    //farPlane是自定义的雾效最远距离 
    // See EncodeLogarithmicDepthGeneralized().
    static Vector4 ComputeLogarithmicDepthEncodingParams(float nearPlane, float farPlane, float c)
    {
        Vector4 depthParams = new Vector4();

        float n = nearPlane;
        float f = farPlane;

        depthParams.y = 1.0f / Mathf.Log(c * (f - n) + 1, 2);
        depthParams.x = Mathf.Log(c, 2) * depthParams.y;
        depthParams.z = n - 1.0f / c; // Same
        depthParams.w = 0.0f;

        return depthParams;
    }
    //farPlane是自定义的雾效最远距离 
    // See DecodeLogarithmicDepthGeneralized().
    static Vector4 ComputeLogarithmicDepthDecodingParams(float nearPlane, float farPlane, float c)
    {
        Vector4 depthParams = new Vector4();

        float n = nearPlane;
        float f = farPlane;

        depthParams.x = 1.0f / c;
        depthParams.y = Mathf.Log(c * (f - n) + 1, 2);
        depthParams.z = n - 1.0f / c; // Same
        depthParams.w = 0.0f;

        return depthParams;
    }
    //farClip是自定义的雾效最远距离 
    static Vector4 GetZParam(float nearClip, float farClip)
    {
        bool reZ = SystemInfo.usesReversedZBuffer;
        Vector4 zParam;
        if (reZ)
        {
            zParam.x = -1 + farClip / nearClip;
            zParam.y = 1;
            zParam.z = zParam.x / farClip;
            zParam.w = 1 / farClip;
        }
        else
        {
            zParam.x = 1 - farClip / nearClip;
            zParam.y = farClip / nearClip;
            zParam.z = zParam.x / farClip;
            zParam.w = zParam.y / farClip;
        }
        return zParam;
    }

    static void GetInverseVP(Camera camera, float nearClip, float fogFarClip, 
    out Matrix4x4 inverseV, out Matrix4x4 volumeToWorld, ref Matrix4x4 worldToVolume)
    {
        Matrix4x4 projInVolume = Matrix4x4.Perspective(camera.fieldOfView, camera.aspect, nearClip, fogFarClip);
        Matrix4x4 worldToCamera = camera.worldToCameraMatrix;
        inverseV = worldToCamera.inverse;
        volumeToWorld = inverseV * projInVolume.inverse;
        worldToVolume = projInVolume * worldToCamera;
        //Matrix4x4 proj = Matrix4x4.Perspective(camera.fieldOfView, camera.aspect, camera.nearClip, fogFarClip);
    }

    static Vector3 VolumetricFogTemporalRandom(int FrameNumber)
    {
        // Center of the voxel
        Vector3 RandomOffsetValue = new Vector3(.5f, .5f, .5f);

        RandomOffsetValue = new Vector3(VolumeHelper.Halton(FrameNumber & 1023, 2), VolumeHelper.Halton(FrameNumber & 1023, 3), VolumeHelper.Halton(FrameNumber & 1023, 5));
        
        return RandomOffsetValue;
    }

    static float EncodeLogarithmicDepthGeneralized(float z, Vector4 encodingParams)
    {
        return encodingParams.x + encodingParams.y * Mathf.Log(Mathf.Max(0, z - encodingParams.z), 2);
    }
}
