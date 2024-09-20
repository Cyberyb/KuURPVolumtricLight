using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class KuRenderPass : ScriptableRenderPass
{

    #region 字段
    //接取屏幕原图的属性名
    protected static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    //暂存贴图的属性名
    protected static readonly int TempTargetId = Shader.PropertyToID("_TempTargetColorTint");

    //CommandBuffer的名称
    protected string cmdName;
    //继承VolumeComponent的组件（父装子）
    protected VolumeComponent volume;
    //当前Pass使用的材质
    protected Material material;
    //当前渲染的目标
    protected RTHandle currentTarget;
    #endregion

    #region 函数
    //-------------------------构造------------------------------------
    public KuRenderPass(RenderPassEvent evt, Shader shader)
    {
        cmdName = this.GetType().Name + "_cmdName";
        renderPassEvent = evt;//设置渲染事件位置
        //不存在则返回
        if (shader == null)
        {
            Debug.LogError("不存在" + this.GetType().Name + "shader");
            return;
        }
        material = CoreUtils.CreateEngineMaterial(shader);//新建材质
    }

    //----------------------子类继承但禁止重写---------------------------
    public void Setup(in RTHandle currentTarget)
    {
        this.currentTarget = currentTarget;
        //this.Init();
    }


    //Unity每帧每个摄像机调用一次
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        //材质是否存在
        if (material == null)
        {
            Debug.LogError("材质初始化失败");
            return;
        }
        //摄像机关闭后处理
        if (!renderingData.cameraData.postProcessEnabled)
        {
            //Debug.LogError("相机后处理是关闭的！！！");
            return;
        }

        var cmd = CommandBufferPool.Get(cmdName);//从池中获取CMD
        Render(cmd, ref renderingData);//将该Pass的渲染指令写入到CMD中
        context.ExecuteCommandBuffer(cmd);//执行CMD
        CommandBufferPool.Release(cmd);//释放CMD
        //Debug.Log("完成CMD");
    }

    //Unity执行Render Pass之前调用
    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {

    }


    //Unity在Render Pass 执行后销毁材质和临时Render texture
    public void Dispose()
    {
        //Object.Destroy(material);
        if (currentTarget != null) { currentTarget.Release(); }
    }

    //-----------------------子类必须重写----------------------------------
    /// 虚方法，供子类重写，需要将该Pass的具体渲染指令写入到CMD中
    protected virtual void Render(CommandBuffer cmd, ref RenderingData renderingData) { }

    protected virtual void Init() { }
    #endregion








}