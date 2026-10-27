using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Confirmação "Reiniciar a fase?" aberta pelo botão (ou pela tecla) na pausa.
/// Cancelar (Esc) volta pra pausa; Reiniciar (Enter, já em foco pelo
/// FocoInicialUI) recarrega a fase. O Esc é tratado pelo PauseMenuUI, que
/// já cuida do "voltar um nível" das telas por cima da fase.
///
/// Nas fases que guardam os puzzles no
/// reinício (IA, Animação, Interação e Vida), uma linha verde abaixo do texto
/// diz o que continua montado (ReinicioFase.Lembrete), como a do Game Over.
/// A linha é criada aqui, copiando o "Texto" da caixa, e a caixa cresce só
/// quando ela aparece.
/// </summary>
public class ConfirmarReinicioUI : MonoBehaviour
{
    [SerializeField] private Button cancelar;
    [SerializeField] private Button reiniciar;

    private Action aoConfirmar;
    private Action aoCancelar;

    private const string TextoComLembrete = "Você volta ao começo desta fase, sem repetir a explicação do Prof Byte.";
    private const float AlturaLembrete = 70f;
    private static readonly Color CorLembrete = new Color32(0x52, 0xC9, 0x8A, 0xFF);

    private TMP_Text texto;
    private TMP_Text lembrete;
    private string textoPadrao;
    private RectTransform caixa;
    private float alturaCaixa;

    public bool Aberto => gameObject.activeSelf;

    private void Awake()
    {
        if (cancelar != null)
        {
            cancelar.onClick.RemoveListener(Cancelar);
            cancelar.onClick.AddListener(Cancelar);
        }

        if (reiniciar != null)
        {
            reiniciar.onClick.RemoveListener(Confirmar);
            reiniciar.onClick.AddListener(Confirmar);
        }
    }

    public void Abrir(Action confirmar, Action cancelarAcao)
    {
        aoConfirmar = confirmar;
        aoCancelar = cancelarAcao;
        AtualizarTexto(ReinicioFase.Lembrete);
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
    }

    private void AtualizarTexto(string linha)
    {
        PrepararTextos();
        if (texto == null)
        {
            return;
        }

        bool comLinha = !string.IsNullOrEmpty(linha);
        texto.text = comLinha ? TextoComLembrete : textoPadrao;
        lembrete.text = comLinha ? linha : "";
        lembrete.gameObject.SetActive(comLinha);
        caixa.sizeDelta = new Vector2(caixa.sizeDelta.x, alturaCaixa + (comLinha ? AlturaLembrete : 0f));
    }

    private void PrepararTextos()
    {
        if (texto != null)
        {
            return;
        }

        Transform t = transform.Find("Caixa/Texto");
        texto = t != null ? t.GetComponent<TMP_Text>() : null;
        if (texto == null)
        {
            return;
        }

        textoPadrao = texto.text;
        caixa = (RectTransform)t.parent;
        alturaCaixa = caixa.sizeDelta.y;

        lembrete = Instantiate(texto, caixa);
        lembrete.name = "Lembrete";
        lembrete.color = CorLembrete;
        lembrete.fontSize = texto.fontSize - 2f;
        RectTransform r = lembrete.rectTransform;
        RectTransform rt = texto.rectTransform;
        r.anchoredPosition = rt.anchoredPosition - new Vector2(0f, rt.sizeDelta.y);
        r.sizeDelta = new Vector2(rt.sizeDelta.x + 80f, AlturaLembrete);
        lembrete.gameObject.SetActive(false);
    }

    public void Cancelar()
    {
        if (!Aberto)
        {
            return;
        }

        gameObject.SetActive(false);
        Action acao = aoCancelar;
        aoConfirmar = aoCancelar = null;
        acao?.Invoke();
    }

    private void Confirmar()
    {
        if (!Aberto)
        {
            return;
        }

        gameObject.SetActive(false);
        Action acao = aoConfirmar;
        aoConfirmar = aoCancelar = null;
        acao?.Invoke();
    }
}
