using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class VolumeUIControl : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Toggle adaptiveTAAToggle;
    [SerializeField] private Slider minWeightSlider;

    [Header("Runtime")]
    [SerializeField] private bool useAdaptiveTAA = true;
    [SerializeField] private float minWeight = 0.2f;

    public static bool RuntimeUseAdaptiveTAA = true;
    public static float RuntimeMinWeight = 0.2f;

    private VolumeLight_Volume volumeLight;

    private void Awake()
    {
        RuntimeUseAdaptiveTAA = useAdaptiveTAA;
        RuntimeMinWeight = minWeight;
        volumeLight = VolumeManager.instance.stack.GetComponent<VolumeLight_Volume>();
    }

    private void OnEnable()
    {

        if (adaptiveTAAToggle != null)
        {
            adaptiveTAAToggle.isOn = RuntimeUseAdaptiveTAA;
            adaptiveTAAToggle.onValueChanged.AddListener(SetUseAdaptiveTAA);
        }

        if (minWeightSlider != null)
        {
            minWeightSlider.value = RuntimeMinWeight;
            minWeightSlider.onValueChanged.AddListener(SetMinWeight);
        }
    }

    private void OnDisable()
    {

        if (adaptiveTAAToggle != null)
        {
            adaptiveTAAToggle.onValueChanged.RemoveListener(SetUseAdaptiveTAA);
        }

        if (minWeightSlider != null)
        {
            minWeightSlider.onValueChanged.RemoveListener(SetMinWeight);
        }
    }

    private void OnTemporalReprojectToggled(bool isEnabled)
    {
        if (volumeLight != null)
        {
            volumeLight._UseTemporalReproject.value = isEnabled;
        }
    }

    public void SetUseAdaptiveTAA(bool isEnabled)
    {
        useAdaptiveTAA = isEnabled;
        RuntimeUseAdaptiveTAA = isEnabled;
    }

    public void SetMinWeight(float value)
    {
        minWeight = value;
        RuntimeMinWeight = value;
    }
}