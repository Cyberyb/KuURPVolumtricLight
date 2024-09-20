using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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

    public Texture2D noiseTexture = new Texture2D(470,470);
    private bool isLoadedTexture = false;


    VolumeStack stack = VolumeManager.instance.stack;

    // Start is called before the first frame update
    public VolumeLightRenderPass(RenderPassEvent evt, Shader shader) : base(evt, shader)
    {
        textureDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 0);

        blurTextureDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 0);

        blurTextureDescriptor2 = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 0);

        sourceDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 0);


        byte[] temp = File.ReadAllBytes(Application.dataPath + "/Scenes/Effects/VolumeLight/LDR_RGBA_0.png");
        isLoadedTexture = noiseTexture.LoadImage(temp);
        noiseTexture.Apply();
        noiseTexture.hideFlags = HideFlags.DontSave;


        Debug.Log("VolumeLight Create Render Pass");
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

        volume = stack.GetComponent<VolumeLight_Volume>();
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


            //获取相机世界位置
            Vector3 cameraWorldPos = renderingData.cameraData.worldSpaceCameraPos;

            RTHandle cameraTargetHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;

            UpdateValues();

            //Blitter.BlitCameraTexture(cmd, cameraTargetHandle, textureHandle, material, 0);
            Blit(cmd, cameraTargetHandle, sourceHandle);
            Blit(cmd, cameraTargetHandle, textureHandle, material, 0);


            //3次高斯模糊
            for (int i = 0; i < 3; i++)
            {
                //水平
                //Blitter.BlitCameraTexture(cmd, textureHandle, blurTextureHandle, material, 1);
                Blit(cmd, textureHandle, blurTextureHandle, material, 1);
                //垂直
                //Blitter.BlitCameraTexture(cmd, blurTextureHandle, textureHandle, material, 1);
                Blit(cmd, blurTextureHandle, blurTextureHandle2, material, 2);
            }


            //Blitter.BlitCameraTexture(cmd, textureHandle, cameraTargetHandle, material, 2);
            Blit(cmd, blurTextureHandle2, cameraTargetHandle, material, 3);
            //Blit(cmd, textureHandle, cameraTargetHandle, material, 3);

            //RTHandleRealse();
        }

    }


    protected override void Init()
    {
    }
    private void UpdateValues()
    {
        material.SetFloat("_Intensity", ((VolumeLight_Volume)volume)._LightIntensity.value);
        material.SetColor("_ColorTint", ((VolumeLight_Volume)volume)._LightTint.value);
        material.SetInt("_StepTimes", ((VolumeLight_Volume)volume)._StepTimes.value);
        material.SetFloat("_PhaseG", ((VolumeLight_Volume)volume)._PhaseG.value);
        material.SetFloat("_Extinction", ((VolumeLight_Volume)volume)._Extinction.value);
        material.SetFloat("_RangeSigma", ((VolumeLight_Volume)volume)._RangeSigma.value);

        material.SetVector("_BlurOffsetX", new Vector4(((VolumeLight_Volume)volume)._BlurSize.value / Screen.width, 0, 0, 0));
        material.SetVector("_BlurOffsetY", new Vector4(0, ((VolumeLight_Volume)volume)._BlurSize.value / Screen.height, 0, 0));
       

        material.SetTexture("_GrabTexture", sourceHandle); ;

        if (isLoadedTexture)
        {
            material.SetTexture("_Noise", noiseTexture);
            material.SetInt("_NoiseLoaded", 1);
        }
        else { material.SetInt("_NoiseLoaded", 0); }
            
    }
    private void RTHandleRealse()
    {
        sourceHandle.Release();
        textureHandle.Release();
        blurTextureHandle.Release();
        blurTextureHandle2.Release();
    }

}
