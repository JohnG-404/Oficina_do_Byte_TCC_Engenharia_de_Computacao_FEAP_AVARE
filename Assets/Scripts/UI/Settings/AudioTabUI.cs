using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Conteúdo da aba "Áudio": slider de volume geral, usando o AudioManager.
///
/// Reconhece sua própria estrutura pelo nome dos filhos ("VolumeSlider",
/// "VolumeValueLabel"): se já existirem (porque foram montados/ajustados
/// visualmente no Editor, via GameUIBuilder), apenas reconecta os eventos;
/// senão, constrói a estrutura padrão em runtime. Isso permite editar a
/// aparência no Editor sem perder a ligação dos botões/slider.
/// </summary>
public class AudioTabUI : MonoBehaviour
{
    private Slider volumeSlider;
    private TMP_Text volumeLabel;

    private void Awake()
    {
        EnsureBuilt();
    }

    /// <summary>
    /// Constrói (ou reconecta, se já existir) a estrutura desta aba.
    /// Público para poder ser chamado explicitamente por uma ferramenta de
    /// Editor, já que Awake() nem sempre roda de forma síncrona fora do Play Mode.
    /// </summary>
    public void EnsureBuilt()
    {
        BuildOrFindStructure();
        WireEvents();
        RefreshFromAudioManager();
    }

    private void OnEnable()
    {
        RefreshFromAudioManager();
    }

    private void BuildOrFindStructure()
    {
        Transform sliderT = transform.Find("VolumeSlider");
        Transform labelT = transform.Find("VolumeValueLabel");

        if (sliderT != null && labelT != null)
        {
            volumeSlider = sliderT.GetComponent<Slider>();
            volumeLabel = labelT.GetComponent<TMP_Text>();
            return;
        }

        var titleGO = UIBuildUtils.CreateText(transform, "VolumeLabel", "Volume Geral", 22, TextAlignmentOptions.MidlineLeft);
        titleGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 30);

        volumeSlider = UIBuildUtils.CreateSlider(transform, "VolumeSlider");

        var valueGO = UIBuildUtils.CreateText(transform, "VolumeValueLabel", "100%", 20, TextAlignmentOptions.MidlineLeft);
        valueGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 26);
        volumeLabel = valueGO.GetComponent<TMP_Text>();
    }

    private void WireEvents()
    {
        if (volumeSlider == null)
        {
            return;
        }

        volumeSlider.onValueChanged.RemoveListener(OnSliderChanged);
        volumeSlider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void RefreshFromAudioManager()
    {
        if (AudioManager.Instance != null && volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(AudioManager.Instance.MasterVolume);
            RefreshLabel(AudioManager.Instance.MasterVolume);
        }
    }

    private void OnSliderChanged(float value)
    {
        AudioManager.Instance?.SetMasterVolume(value);
        RefreshLabel(value);
    }

    private void RefreshLabel(float value)
    {
        if (volumeLabel != null)
        {
            volumeLabel.text = Mathf.RoundToInt(value * 100f) + "%";
        }
    }
}
