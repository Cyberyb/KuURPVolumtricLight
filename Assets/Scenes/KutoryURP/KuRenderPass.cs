using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class KuRenderPass : ScriptableRenderPass
{

    #region �ֶ�
    //��ȡ��Ļԭͼ��������
    protected static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    //�ݴ���ͼ��������
    protected static readonly int TempTargetId = Shader.PropertyToID("_TempTargetColorTint");

    //CommandBuffer������
    protected string cmdName;
    //�̳�VolumeComponent���������װ�ӣ�
    protected VolumeComponent volume;
    //��ǰPassʹ�õĲ���
    protected Material material;
    //��ǰ��Ⱦ��Ŀ��
    protected RTHandle currentTarget;
    #endregion

    #region ����
    //-------------------------����------------------------------------
    public KuRenderPass(RenderPassEvent evt, Shader shader)
    {
        cmdName = this.GetType().Name + "_cmdName";
        renderPassEvent = evt;//������Ⱦ�¼�λ��
        //�������򷵻�
        if (shader == null)
        {
            Debug.LogError("������" + this.GetType().Name + "shader");
            return;
        }
        material = CoreUtils.CreateEngineMaterial(shader);//�½�����
    }

    //----------------------����̳е���ֹ��д---------------------------
    public void Setup(in RTHandle currentTarget)
    {
        this.currentTarget = currentTarget;
        //this.Init();
    }


    //Unityÿ֡ÿ�����������һ��
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        //�����Ƿ����
        if (material == null)
        {
            Debug.LogError("���ʳ�ʼ��ʧ��");
            return;
        }
        //������رպ���
        if (!renderingData.cameraData.postProcessEnabled)
        {
            //Debug.LogError("��������ǹرյģ�����");
            return;
        }

        var cmd = CommandBufferPool.Get(cmdName);//�ӳ��л�ȡCMD
        Render(cmd, ref renderingData);//����Pass����Ⱦָ��д�뵽CMD��
        context.ExecuteCommandBuffer(cmd);//ִ��CMD
        CommandBufferPool.Release(cmd);//�ͷ�CMD
        //Debug.Log("���CMD");
    }

    //Unityִ��Render Pass֮ǰ����
    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {

    }


    //Unity��Render Pass ִ�к����ٲ��ʺ���ʱRender texture
    public void Dispose()
    {
        //Object.Destroy(material);
        if (currentTarget != null) { currentTarget.Release(); }
    }

    //-----------------------���������д----------------------------------
    /// �鷽������������д����Ҫ����Pass�ľ�����Ⱦָ��д�뵽CMD��
    protected virtual void Render(CommandBuffer cmd, ref RenderingData renderingData) { }

    protected virtual void Init() { }
    #endregion








}