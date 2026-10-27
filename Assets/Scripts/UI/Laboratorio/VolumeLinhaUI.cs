using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Uma linha de volume da aba Áudio (tema Laboratório): liga o slider ao
/// AudioManager (valor inicial, Set ao mexer, e atualiza sozinha se o volume
/// mudar por fora) e mostra a porcentagem.
///
/// O "Volume geral" continua sendo gravado pelo AudioTabUI (que acha o
/// VolumeSlider pelo nome): na linha dele deixe "Só acompanhar" ligado, pra
/// esta linha apenas refletir o OnVolumeChanged sem gravar de novo.
/// </summary>
public class VolumeLinhaUI : MonoBehaviour
{
    public enum Canal { Geral, Musica, Efeitos }

    [SerializeField] private Canal canal = Canal.Musica;
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text valor;
    [Tooltip("Ligado = não chama o Set do AudioManager (outro script já grava este canal); só acompanha as mudanças.")]
    [SerializeField] private bool soAcompanhar;

    private AudioManager inscrito;

    private void OnEnable()
    {
        if (slider != null && !soAcompanhar)
        {
            slider.onValueChanged.RemoveListener(AoMexer);
            slider.onValueChanged.AddListener(AoMexer);
        }

        Inscrever();
        if (AudioManager.Instance != null) Mostrar(Ler(AudioManager.Instance));
    }

    private void OnDisable()
    {
        if (slider != null) slider.onValueChanged.RemoveListener(AoMexer);
        Desinscrever();
    }

    private void Update()
    {
        // O AudioManager pode nascer depois desta tela (bootstrap): inscreve quando aparecer.
        if (inscrito == null && AudioManager.Instance != null)
        {
            Inscrever();
            Mostrar(Ler(AudioManager.Instance));
        }
    }

    private void AoMexer(float v)
    {
        AudioManager am = AudioManager.Instance;
        if (am != null)
        {
            switch (canal)
            {
                case Canal.Geral: am.SetMasterVolume(v); break;
                case Canal.Musica: am.SetMusicVolume(v); break;
                case Canal.Efeitos: am.SetSfxVolume(v); break;
            }
        }
        MostrarTexto(v);
    }

    private void Inscrever()
    {
        AudioManager am = AudioManager.Instance;
        if (am == null || inscrito == am) return;
        Desinscrever();
        inscrito = am;
        switch (canal)
        {
            case Canal.Geral: am.OnVolumeChanged += Mostrar; break;
            case Canal.Musica: am.OnMusicVolumeChanged += Mostrar; break;
            case Canal.Efeitos: am.OnSfxVolumeChanged += Mostrar; break;
        }
    }

    private void Desinscrever()
    {
        if (inscrito == null) return;
        switch (canal)
        {
            case Canal.Geral: inscrito.OnVolumeChanged -= Mostrar; break;
            case Canal.Musica: inscrito.OnMusicVolumeChanged -= Mostrar; break;
            case Canal.Efeitos: inscrito.OnSfxVolumeChanged -= Mostrar; break;
        }
        inscrito = null;
    }

    private float Ler(AudioManager am) => canal switch
    {
        Canal.Geral => am.MasterVolume,
        Canal.Musica => am.MusicVolume,
        _ => am.SfxVolume,
    };

    private void Mostrar(float v)
    {
        if (slider != null) slider.SetValueWithoutNotify(v);
        MostrarTexto(v);
    }

    private void MostrarTexto(float v)
    {
        if (valor != null) valor.text = Mathf.RoundToInt(v * 100f) + "%";
    }
}
