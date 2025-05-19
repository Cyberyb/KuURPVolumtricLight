using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static Unity.Burst.Intrinsics.X86.Avx;

public class VolumeLightRenderPass : KuRenderPass
{
    ProfilingSampler m_ProfilingSampler = new ProfilingSampler("Kutory Volumetric Light");
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

    //计算着色器
    public ComputeShader kucomputeShader;
    //体积RenderTexture
    public RenderTexture volumeTexture;
    //散射RenderTexture
    public RenderTexture scatteringTexture;
    //积分RenderTexture
    public RenderTexture integratedTexture;

    private RenderTexture shadowmapTexture;

    private Matrix4x4 preVP;
    private Matrix4x4 VP;
    
    

    public Texture2D noiseTexture = new Texture2D(470,470);
    private bool isLoadedTexture = false;


    VolumeStack stack = VolumeManager.instance.stack;

    // Start is called before the first frame update
    public VolumeLightRenderPass(RenderPassEvent evt, Shader shader, ComputeShader computeShader) : base(evt, shader, computeShader)
    {
        textureDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 0);

        blurTextureDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 0);

        blurTextureDescriptor2 = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 0);

        sourceDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 0);

        stencilDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.R8, 0);

        

        downSampleDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 0);

        //获取外部图片
        byte[] temp = File.ReadAllBytes(Application.dataPath + "/Scenes/Effects/VolumeLight/LDR_RGBA_0.png");
        isLoadedTexture = noiseTexture.LoadImage(temp);
        noiseTexture.Apply();
        noiseTexture.hideFlags = HideFlags.DontSave;

        if(computeShader != null)
            kucomputeShader = computeShader;

        //开启随机写入
/*        voxelTexture = new RenderTexture(120, 75, 0)
        {
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D, // 设置为 3D 纹理
            volumeDepth = 64, // 设置深度
            enableRandomWrite = true, // 启用随机写入
            graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, // 图像格式
        };
        voxelTexture.Create();*/

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

        //获取Volume中保存的各项参数
        volume = stack.GetComponent<VolumeLight_Volume>();

        int downSample = ((VolumeLight_Volume)volume)._DownSample.value;
        downSampleDescriptor.width = Screen.width / downSample;
        downSampleDescriptor.height = Screen.height / downSample;

        RenderingUtils.ReAllocateIfNeeded(ref downSampleHandle, downSampleDescriptor, FilterMode.Bilinear, name: "_DownSampleTexture");

        volumeTexture = ((VolumeLight_Volume)volume)._VolumeTexture.value;
        scatteringTexture = ((VolumeLight_Volume)volume)._ScatteringTexture.value;
        integratedTexture = ((VolumeLight_Volume)volume)._IntegratedTexture.value;
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
            preVP = VP;
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
        Blit(cmd, cameraTargetHandle, stencilHandle, material, 0);

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

        kucomputeShader.Dispatch(kernelIndex, 256 / 8, 256 / 8, 64 / 8);

        //CS2: 计算散射
        int scatteringIndex = kucomputeShader.FindKernel("CSScatteringLight");
        kucomputeShader.SetTexture(scatteringIndex, "_InputAttribute", volumeTexture);
        kucomputeShader.SetTexture(scatteringIndex, "_OutputScatteringLight", scatteringTexture);
        Texture shadowTexutre = Shader.GetGlobalTexture("_MainLightShadowmapTexture");

        kucomputeShader.Dispatch(scatteringIndex, 256 / 8, 256 / 8, 64 / 8);

        //CS3: 计算积分
        int integrationIndex = kucomputeShader.FindKernel("CSIntegration");


        kucomputeShader.SetTexture(integrationIndex, "_InputScatteringLight", scatteringTexture);
        kucomputeShader.SetTexture(integrationIndex, "_OutputIntegrated", integratedTexture);

        kucomputeShader.Dispatch(integrationIndex, 256 / 8, 256 / 8, 1);


        /*Vertex&Pixel Shader部分*/
        Blit(cmd, cameraTargetHandle, sourceHandle);
        Blit(cmd, sourceHandle, cameraTargetHandle, material, 5);

        //RTHandleRealse();
    }


    protected override void Init()
    {
        
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

        material.SetTexture("_VolumeTexture", integratedTexture);

        material.SetFloat("_FovY", renderingData.cameraData.camera.fieldOfView);
        material.SetFloat("_Aspect", renderingData.cameraData.camera.aspect);
        material.SetFloat("_Farplane", ((VolumeLight_Volume)volume)._FarPlane.value);
        material.SetFloat("_Nearplane", renderingData.cameraData.camera.nearClipPlane);
        material.SetMatrix("_PreVP", preVP);
        material.SetFloat("_ReprojectWeight", ((VolumeLight_Volume)volume)._ReprojectWeight.value);

        if (isLoadedTexture)
        {
            material.SetTexture("_Noise", noiseTexture);
            material.SetInt("_NoiseLoaded", 1);
        }
        else { material.SetInt("_NoiseLoaded", 0); }
            
    }
    private void UpdateComputeValues(ref CommandBuffer cmd, ref RenderingData renderingData)
    {
        float farClip = ((VolumeLight_Volume)volume)._FarPlane.value;
        float nearClip = renderingData.cameraData.camera.nearClipPlane;
        Vector4 zParam = GetZParam(nearClip, farClip);
        GetInverseVP(renderingData.cameraData.camera, nearClip, farClip, out var inverseV, out var inverseVP, ref VP);
        //material.SetMatrix("_InvV", renderingData.cameraData.camera.worldToCameraMatrix.inverse);
        //material.SetMatrix("_InvP", renderingData.cameraData.camera.projectionMatrix.inverse);
        float c = 0.5f;
        Vector4 logarithmicDepthDecodingParams = ComputeLogarithmicDepthDecodingParams(nearClip, farClip, c);
        Vector4 logarithmicDepthEncodingParams = ComputeLogarithmicDepthEncodingParams(nearClip, farClip, c);
        cmd.SetGlobalMatrix("_World2Volume", VP);
        cmd.SetGlobalMatrix("_InverseV", inverseV);
        cmd.SetGlobalMatrix("_InverseVP", inverseVP);
        cmd.SetGlobalFloat("_Farplane", farClip);
        //cmd.SetComputeFloatParam(kucomputeShader, "_Farplane", farClip);
        cmd.SetComputeFloatParam(kucomputeShader, "_Nearplane", nearClip);
        cmd.SetComputeVectorParam(kucomputeShader, "_VolumeSize", new Vector4(256, 256, 64, 1));
        cmd.SetGlobalVector("_ZParam", zParam);
        cmd.SetGlobalVector("_LogarithmicDepthDecodingParams", logarithmicDepthDecodingParams);
        cmd.SetGlobalVector("_LogarithmicDepthEncodingParams", logarithmicDepthEncodingParams);
        cmd.SetGlobalVector("_VolumeColor", ((VolumeLight_Volume)volume)._VolumeColor.value);
        cmd.SetComputeFloatParam(kucomputeShader, "_GlobalFogDensity", ((VolumeLight_Volume)volume)._GlobalFogDensity.value);
        cmd.SetComputeFloatParam(kucomputeShader, "_HeightFallOff", ((VolumeLight_Volume)volume)._HeightFallOff.value);

        cmd.SetComputeFloatParam(kucomputeShader, "_FovY", renderingData.cameraData.camera.fieldOfView);
        cmd.SetComputeFloatParam(kucomputeShader, "_Aspect", renderingData.cameraData.camera.aspect);
 
        cmd.SetComputeVectorParam(kucomputeShader, "_GlobalScatter", ((VolumeLight_Volume)volume)._GlobalScatter.value);
        cmd.SetComputeFloatParam(kucomputeShader, "_GlobalAbsorb", ((VolumeLight_Volume)volume)._GlobalAbsorb.value);
        cmd.SetComputeVectorParam(kucomputeShader, "_GlobalAlbedo", ((VolumeLight_Volume)volume)._GlobalAlbedo.value);
        cmd.SetComputeFloatParam(kucomputeShader, "_GlobalExtinction", ((VolumeLight_Volume)volume)._GlobalExtinction.value);
        cmd.SetComputeFloatParam(kucomputeShader, "_PhaseG", ((VolumeLight_Volume)volume)._PhaseG.value);

        
    }
    private void RTHandleRealse()
    {
        if (sourceHandle != null) { sourceHandle.Release(); }
        if (textureHandle != null) { textureHandle.Release(); }
        if (blurTextureHandle != null) { blurTextureHandle.Release(); }
        if (blurTextureHandle2 != null) { blurTextureHandle2.Release(); }
        if (stencilHandle != null) { stencilHandle.Release(); }
        if (downSampleHandle != null) { downSampleHandle.Release(); }
        if (volumeTexture != null) { volumeTexture.Release(); }
    }

    public new void Dispose()
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

    static void GetInverseVP(Camera camera, float nearClip, float fogFarClip, out Matrix4x4 inverseV, out Matrix4x4 inverseVP, ref Matrix4x4 VP)
    {
        Matrix4x4 proj = Matrix4x4.Perspective(camera.fieldOfView, camera.aspect, nearClip, fogFarClip);
        Matrix4x4 worldToCmaera = camera.worldToCameraMatrix;
        inverseV = worldToCmaera.inverse;
        inverseVP = inverseV * proj.inverse;
        VP = proj * worldToCmaera;
    }

}
