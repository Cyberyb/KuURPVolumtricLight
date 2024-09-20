using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static UnityEngine.XR.XRDisplaySubsystem;

public class KuRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class RenderPassList
    {
        public string RenderPassName;
        //指定该RendererFeature在渲染流程的哪个时机插入
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        //指定一个shader
        public Shader shader;
        //是否开启
        public bool activeff;

        public KuRenderPass renderPass;
    }
    public RenderPassList[] renderPassList;


    /*
     Unity在以下事件上调用Create()
    1.Renderer Feature 首次加载
    2.启用或者禁用Renderer Feature
    3.在inspector中更改Renderer Feature的property
     */
    public override void Create()
    {
        Debug.Log("KuRenderFeature::Create()");
        if (renderPassList.Length == 0)
            Debug.Log("RenderPassList is empty!");

        for (int i = 0; i < renderPassList.Length; i++)
        {
            if (renderPassList[i].shader != null && renderPassList[i].activeff)
            {
                Debug.Log("Create render pass :" + i);

                renderPassList[i].renderPass = Activator.CreateInstance(Type.GetType(renderPassList[i].RenderPassName), renderPassList[i].renderPassEvent, renderPassList[i].shader) as KuRenderPass;
            }
        }
    }

    //Unity每帧每个Camera调用一次，该方法将Scriptable RenderPass实例注入到Scriptable Renderer中
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType == CameraType.Game || renderingData.cameraData.cameraType == CameraType.SceneView)
        {

            for (int i = 0; i < renderPassList.Length; i++)
            {
                if (renderPassList[i].shader != null && renderPassList[i].activeff)
                {
                    renderer.EnqueuePass(renderPassList[i].renderPass);
                }
            }
        }
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType == CameraType.Game || renderingData.cameraData.cameraType == CameraType.SceneView)
        {

            for (int i = 0; i < renderPassList.Length; i++)
            {
                if (renderPassList[i].shader != null && renderPassList[i].activeff)
                {
                    renderPassList[i].renderPass.Setup(renderer.cameraColorTargetHandle);
                }
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        for (int i = 0; i < renderPassList.Length; i++)
        {
            if (renderPassList[i].shader != null && renderPassList[i].activeff)
            {
                renderPassList[i].renderPass.Dispose();
            }
        }
    }
}
