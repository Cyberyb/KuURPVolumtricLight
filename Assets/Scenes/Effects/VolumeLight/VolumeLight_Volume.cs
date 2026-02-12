using Unity.Burst.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable, VolumeComponentMenu("Ku_PostProcessing/VolumeLight")]
public class VolumeLight_Volume : VolumeComponent, IPostProcessComponent
{
    [Header("降采样设置")]
    public ClampedIntParameter _DownSample = new ClampedIntParameter(1, 1, 16);

    [Header("光照设置")]
    public ColorParameter _LightTint = new ColorParameter(Color.white, true);//如果有两个true,则为HDR设置
    public ClampedFloatParameter _LightIntensity = new ClampedFloatParameter(0f, 0f,5f);
    public ClampedIntParameter _StepTimes = new ClampedIntParameter(0,0,128);
    public ClampedFloatParameter _PhaseG = new ClampedFloatParameter(0f, -1f, 1f);
    public ClampedFloatParameter _Extinction = new ClampedFloatParameter(0.7f, 0f, 1f);

    [Header("Jitter设置")]
    public TextureParameter _JitterTexture = new TextureParameter(null);

    [Header("模糊设置")]
    public ClampedFloatParameter _BlurSize = new ClampedFloatParameter(1f, 0.0f, 5f);
    public ClampedFloatParameter _RangeSigma = new ClampedFloatParameter(0.5f,0.0f,3f);

    [Header("体素雾空间设置")]
    public ClampedFloatParameter _FarPlane = new ClampedFloatParameter(128.0f, 1.0f, 1280.0f);
    public Vector4Parameter _VolumeColor = new Vector4Parameter(new Vector4(0.2f, 0.6f, 1.0f, 1.0f));
    public RenderTextureParameter _VolumeTexture = new RenderTextureParameter(null);
    public RenderTextureParameter _IntegratedTexture = new RenderTextureParameter(null);
    public RenderTextureParameter _ScatteringTexture = new RenderTextureParameter(null);

    [Header("体素雾属性设置")]
    public ClampedFloatParameter _GlobalFogDensity = new ClampedFloatParameter(1.0f, 0.001f, 2.0f);
    public ClampedFloatParameter _HeightFallOff = new ClampedFloatParameter(0.5f, -5.0f, 5.0f);
    public BoolParameter _UseFroxel = new BoolParameter(true);
    public Vector3Parameter _GlobalScatter = new Vector3Parameter(new Vector3(0.2f,0.3f,1.0f));
    public ClampedFloatParameter _GlobalAbsorb = new ClampedFloatParameter(0.2f, 0.001f, 1.0f);
    public ColorParameter _GlobalAlbedo = new ColorParameter(new Color(0.2f,0.3f,0.6f));
    public ClampedFloatParameter _GlobalExtinction = new ClampedFloatParameter(0.1f, 0.0f, 1.0f);
    public ClampedFloatParameter _ReprojectWeight = new ClampedFloatParameter(0.8f,0.0f,1.0f);


    /// <inheritdoc/>
    public bool IsActive() => _LightIntensity.value != 0f || _StepTimes.value != 0;

    /// <inheritdoc/>
    public bool IsTileCompatible() => true;
}
