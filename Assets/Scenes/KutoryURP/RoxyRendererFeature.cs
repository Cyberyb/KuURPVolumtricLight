using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RoxyRendererFeature : ScriptableRendererFeature
{
    

    [SerializeField]
    public Shader m_shader;
    public RenderPassEvent renderPassEvent;
    public Color colorTint;

    RoxyRenderPass m_ScriptablePass;
    private Material m_material;
    

    /// <inheritdoc/>
    public override void Create()
    {
        this.name = "Roxy Shader Test";
        Debug.Log("RoxyRendererFeature Created!!!");
        if(m_shader == null)
        {
            return;
        }

        if(m_material == null)
            m_material = CoreUtils.CreateEngineMaterial(m_shader);

        m_ScriptablePass = new RoxyRenderPass(m_material, colorTint);

        // Configures where the render pass should be injected.
        m_ScriptablePass.renderPassEvent = renderPassEvent;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType == CameraType.Game || renderingData.cameraData.cameraType == CameraType.SceneView)
        {
            renderer.EnqueuePass(m_ScriptablePass);
            m_ScriptablePass.ConfigureInput(ScriptableRenderPassInput.Color);
        }    
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        m_ScriptablePass.Setup(renderer.cameraColorTargetHandle);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        CoreUtils.Destroy(m_material);
        m_ScriptablePass.Dispose();
    }
}


class RoxyRenderPass : ScriptableRenderPass
{
    private RTHandle GrabTex;
    private Color color;
    private const string profilerTag = "Roxy Shader Test";
    private ProfilingSampler RoxySampler = new(profilerTag);
    private int RoxyTint = Shader.PropertyToID("_ColorTint");

    private Material material;
    private RTHandle cameraTargetHandle;
    public RoxyRenderPass(Material material, Color color)
    {
        Debug.Log("Roxy Render Pass Created!!!");
        this.material = material;
        this.color = color;
    }

    // This method is called before executing the render pass.
    // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
    // When empty this render pass will render to the active camera render target.
    // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
    // The render pipeline will ensure target setup and clearing happens in a performant manner.
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
        descriptor.depthBufferBits = 0;
        RenderingUtils.ReAllocateIfNeeded(ref GrabTex, descriptor, FilterMode.Bilinear, name: "_GrabTexture");
        ConfigureTarget(GrabTex);
        ConfigureClear(ClearFlag.All, Color.clear);
    
    }

    // Here you can implement the rendering logic.
    // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
    // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
    // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get("Roxy's Postpocess");
        material.SetColor(RoxyTint, color);
        using (new ProfilingScope(cmd,RoxySampler))
        {
            Blitter.BlitCameraTexture(cmd, cameraTargetHandle, GrabTex);
            CoreUtils.SetRenderTarget(cmd, cameraTargetHandle);
            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
        }


        context.ExecuteCommandBuffer(cmd);
        //cmd.Clear();
        //cmd.Dispose();
        CommandBufferPool.Release(cmd);
    }

    // Cleanup any allocated resources that were created during the execution of this render pass.
    public override void OnCameraCleanup(CommandBuffer cmd)
    {
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
/*        textureDescriptor.width = cameraTextureDescriptor.width;
        textureDescriptor.height = cameraTextureDescriptor.height;

        RenderingUtils.ReAllocateIfNeeded(ref textureHandle, textureDescriptor);*/
    }

    public void Setup(RTHandle cameraColorTargetHandle)
    {
        this.cameraTargetHandle = cameraColorTargetHandle;
    }

    public void Dispose()
    {
        GrabTex?.Release();
    }
}

