using Unity.Burst.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable, VolumeComponentMenu("Ku_PostProcessing/VolumeLight")]
public class VolumeLight_Volume : VolumeComponent, IPostProcessComponent
{

    [Header("��������")]
    public ColorParameter _LightTint = new ColorParameter(Color.white, true);//���������true,��ΪHDR����
    public ClampedFloatParameter _LightIntensity = new ClampedFloatParameter(0f, 0f,5f);
    public ClampedIntParameter _StepTimes = new ClampedIntParameter(0,0,128);
    public ClampedFloatParameter _PhaseG = new ClampedFloatParameter(0f, -1f, 1f);
    public ClampedFloatParameter _Extinction = new ClampedFloatParameter(0.7f, 0f, 1f);

    [Header("ģ������")]
    public ClampedFloatParameter _BlurSize = new ClampedFloatParameter(1f, 0.0f, 5f);
    public ClampedFloatParameter _RangeSigma = new ClampedFloatParameter(0.5f,0.0f,3f);

    /// <inheritdoc/>
    public bool IsActive() => _LightIntensity.value != 0f || _StepTimes.value != 0;

    /// <inheritdoc/>
    public bool IsTileCompatible() => true;
}
