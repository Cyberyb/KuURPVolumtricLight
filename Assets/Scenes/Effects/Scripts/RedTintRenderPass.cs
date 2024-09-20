using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RedTintRenderPass : KuRenderPass
{
    [SerializeField]
    public float Intensity = 1.0F;
    private RenderTextureDescriptor textureDescriptor;
    public RTHandle textureHandle;

    public RedTintRenderPass(RenderPassEvent evt, Shader shader) : base(evt, shader)
    {
        textureDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 0);
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        textureDescriptor.width = cameraTextureDescriptor.width;
        textureDescriptor.height = cameraTextureDescriptor.height;

        RenderingUtils.ReAllocateIfNeeded(ref textureHandle, textureDescriptor);
    }

    protected override void Render(CommandBuffer cmd, ref RenderingData renderingData)
    {
        material.SetFloat("_Intensity", Intensity);

        RTHandle cameraTargetHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;

        Blit(cmd, cameraTargetHandle, textureHandle, material, 0);

        Blit(cmd, textureHandle, cameraTargetHandle, material, 1);
    }

    protected override void Init()
    {
        this.SetValue(Intensity);
    }
    private void SetValue(float intensity)
    {
        Intensity = intensity;
    }
}
