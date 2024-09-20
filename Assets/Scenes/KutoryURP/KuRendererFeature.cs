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
        //ָ����RendererFeature����Ⱦ���̵��ĸ�ʱ������
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        //ָ��һ��shader
        public Shader shader;
        //�Ƿ���
        public bool activeff;

        public KuRenderPass renderPass;
    }
    public RenderPassList[] renderPassList;


    /*
     Unity�������¼��ϵ���Create()
    1.Renderer Feature �״μ���
    2.���û��߽���Renderer Feature
    3.��inspector�и���Renderer Feature��property
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

    //Unityÿ֡ÿ��Camera����һ�Σ��÷�����Scriptable RenderPassʵ��ע�뵽Scriptable Renderer��
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
