using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Painel "Fase Concluída!": cartão único com o que o jogador
/// montou na fase (um check e uma frase por item), Tempo / Erros ou
/// Reinícios / Tentativa e "Próxima: X  [Enter] Continuar".
///
/// O cartão fica salvo na cena (Tools > Fases > Faixa de objetivo e fim de
/// fase), desativado. Quem termina a fase chama <see cref="Mostrar"/> com os
/// itens que sabe na hora (puzzles concluídos, itens configurados...) e o que
/// fazer ao continuar; o painel espera Enter ou clique. Tempo, erros, falhas
/// e tentativas vêm da última conclusão gravada pelo ProgressoJogo; "Próxima"
/// vem da ordem de ProgressoJogo.Fases.
///
/// Cena sem o cartão novo (só o painel antigo de título): <see cref="Show"/>
/// cai no popup do tema Laboratório, como antes.
/// </summary>
public class LevelCompleteUI : MonoBehaviour
{
    public struct Item
    {
        public string nome;
        public string frase;

        public Item(string nome, string frase)
        {
            this.nome = nome;
            this.frase = frase;
        }
    }

    [Serializable]
    public class Linha
    {
        public GameObject raiz;
        public TMP_Text nome;
        public TMP_Text frase;
    }

    /// <summary>True enquanto algum painel de fim de fase está na tela (a faixa de objetivo some).</summary>
    public static bool Aberto { get; private set; }

    [Header("Cartão (montado pelo builder)")]
    [SerializeField] private RectTransform janela;
    [SerializeField] private TMP_Text tituloFase;
    [SerializeField] private TMP_Text rotuloSecao;
    [SerializeField] private Linha[] linhas = new Linha[0];
    [SerializeField] private RectTransform blocoStats;
    [SerializeField] private TMP_Text valorTempo;
    [SerializeField] private TMP_Text rotuloContagem;
    [SerializeField] private TMP_Text valorContagem;
    [SerializeField] private TMP_Text valorTentativa;
    [SerializeField] private TMP_Text proxima;

    [Header("Layout")]
    [SerializeField] private float alturaBase = 470f;
    [SerializeField] private float alturaLinha = 62f;
    [SerializeField] private float topoLinhas = -192f;

    [Header("Ordem de desenho")]
    [Tooltip("O cartão desenha por cima do HUD e das etiquetas da fase (o menu de pausa fica em 100).")]
    [SerializeField] private int ordemDeDesenho = 95;

    [Header("Entrada")]
    [Tooltip("Segundos (tempo real) antes de aceitar Enter/clique, pra não pular o painel sem querer.")]
    [SerializeField] private float travaInicial = 0.6f;

    private Action aoContinuar;
    private float abertoEm;
    private bool esperando;

    public bool TemCartao => janela != null && linhas != null && linhas.Length > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Aberto = false;
    }

    private void OnDisable()
    {
        Aberto = false;
    }

    /// <summary>Compatibilidade: painel sem itens (cenas antigas ou flows sem resumo).</summary>
    public void Show()
    {
        if (!TemCartao)
        {
            TemaLabFases.EstilizarFimDeFase(gameObject);
            gameObject.SetActive(true);
            GarantirPorCima();
            Aberto = true;
            return;
        }

        Mostrar(null, false, null);
    }

    /// <summary>
    /// Mostra o cartão. <paramref name="contarErros"/>: true = "Peças erradas"
    /// (ErrosDePuzzle), false = "Reinícios" (quedas, mortes e reinícios).
    /// <paramref name="continuar"/> é chamado com Enter/clique; null = só mostra.
    /// </summary>
    public void Mostrar(IList<Item> itens, bool contarErros, Action continuar)
    {
        if (!TemCartao)
        {
            Show();
            esperando = continuar != null;
            aoContinuar = continuar;
            abertoEm = Time.unscaledTime;
            return;
        }

        string cena = RegistroAprendizagem.CenaAtual;
        if (string.IsNullOrEmpty(cena))
        {
            cena = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }

        if (tituloFase != null) tituloFase.text = ProgressoJogo.TituloDaCena(cena);

        int n = itens != null ? Mathf.Min(itens.Count, linhas.Length) : 0;
        if (rotuloSecao != null) rotuloSecao.gameObject.SetActive(n > 0);
        for (int i = 0; i < linhas.Length; i++)
        {
            Linha l = linhas[i];
            if (l == null || l.raiz == null) continue;
            bool usa = i < n;
            l.raiz.SetActive(usa);
            if (!usa) continue;
            if (l.nome != null) l.nome.text = itens[i].nome;
            if (l.frase != null) l.frase.text = itens[i].frase;
        }

        // Cartão cresce com o número de itens; as estatísticas vêm logo abaixo.
        float altura = alturaBase + Mathf.Max(1, n) * alturaLinha - (n == 0 ? alturaLinha : 0f);
        janela.sizeDelta = new Vector2(janela.sizeDelta.x, altura);
        if (blocoStats != null)
        {
            float y = n > 0 ? topoLinhas - n * alturaLinha - 18f : -150f;
            blocoStats.anchoredPosition = new Vector2(blocoStats.anchoredPosition.x, y);
        }

        ProgressoJogo.Resultado r = ProgressoJogo.UltimaConclusao;
        bool desta = r != null && r.cena == cena;
        if (valorTempo != null) valorTempo.text = desta && r.segundos >= 0f ? FormatarTempo(r.segundos) : "—";
        if (rotuloContagem != null) rotuloContagem.text = contarErros ? "Peças erradas" : "Reinícios";
        int contagem = !desta ? -1 : (contarErros ? r.erros : r.falhas);
        if (valorContagem != null)
        {
            valorContagem.text = contagem >= 0 ? contagem.ToString() : "—";
            valorContagem.color = contagem == 0 ? TemaLabUI.Verde : (contagem > 0 ? TemaLabUI.Dourado : TemaLabUI.CorTexto);
        }
        if (valorTentativa != null) valorTentativa.text = desta && r.tentativa > 0 ? r.tentativa + "ª" : "—";
        if (proxima != null) proxima.text = ProgressoJogo.TituloDaProxima(cena);

        aoContinuar = continuar;
        esperando = continuar != null;
        abertoEm = Time.unscaledTime;
        gameObject.SetActive(true);
        GarantirPorCima();
        Aberto = true;
    }

    // Canvas próprio com ordem fixa: o painel fica dentro do Canvas do HUD de
    // cada cena, e algumas etiquetas da fase desenham depois dele.
    private void GarantirPorCima()
    {
        Canvas c = GetComponent<Canvas>();
        if (c == null)
        {
            c = gameObject.AddComponent<Canvas>();
        }
        c.overrideSorting = true;
        c.sortingOrder = ordemDeDesenho;
        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    private void Update()
    {
        if (!esperando || Time.unscaledTime - abertoEm < travaInicial)
        {
            return;
        }

        // Clique ou Enter dado na pausa (ou nas Configurações) fica com ela.
        if (JogoBloqueado.PausaPorCima)
        {
            return;
        }

        bool tecla = Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame
            || Keyboard.current.numpadEnterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame);
        bool clique = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        if (!tecla && !clique)
        {
            return;
        }

        esperando = false;
        Action acao = aoContinuar;
        aoContinuar = null;
        acao?.Invoke();
    }

    private static string FormatarTempo(float segundos)
    {
        int total = Mathf.RoundToInt(segundos);
        return total >= 3600 ? $"{total / 3600}:{total / 60 % 60:00}:{total % 60:00}" : $"{total / 60}:{total % 60:00}";
    }
}
