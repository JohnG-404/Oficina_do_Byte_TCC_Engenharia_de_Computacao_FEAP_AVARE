using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Estado "Game Over" da fase "Interacao_Vida": quando a vida zera, em vez
/// de recarregar a fase direto, o jogo congela e mostra este painel com o
/// botão "Reiniciar" (clique ou Enter). Assim o aluno vê que "estado de jogo"
/// é mais uma coisa que reage ao mesmo dado (a vida), sem precisar de uma
/// fase só pra isso.
///
/// Só existe na cena Interacao_Vida (colocado pelo InteracaoVidaHudBuilder).
/// O InteracaoLevelFlow procura este componente: se achar, delega a morte
/// pra cá; se não (fase Interacao), recarrega direto como sempre fez. O
/// reinício usa o mesmo recarregamento do InteracaoLevelFlow (flag
/// "reloading"), então a intro não repete.
///
/// A UI é montada em runtime na hora de aparecer (Canvas próprio).
///
/// Outras fases (Animacao, IA, IA_Aplicacao) usam o mesmo painel por
/// <see cref="Mostrar"/>, passando o próprio jeito de recarregar e uma linha
/// verde lembrando o que não se perde no reinício.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    [Header("Textos")]
    [SerializeField] private string titulo = "Game Over";
    [TextArea(1, 3)]
    [SerializeField] private string mensagem = "Sua vida chegou a zero.";
    [SerializeField] private string textoBotao = "Reiniciar";
    [SerializeField] private string dicaTecla = "Enter ou clique para reiniciar";

    [Tooltip("Ordem de desenho do Canvas do painel (abaixo do menu de pausa, que usa 100).")]
    [SerializeField] private int sortingOrder = 80;

    [Tooltip("Linha verde opcional abaixo dos quadrados (ex: o que continua montado depois do reinício).")]
    [SerializeField] private string lembrete = "";

    private Action aoReiniciar;
    private GameObject raiz;
    private float podeReiniciarEm;
    private bool reiniciando;
    private bool pausou;

    public bool Aberto => raiz != null;

    /// <summary>
    /// Cria e mostra o Game Over em qualquer fase. <paramref name="aoReiniciar"/>
    /// é quem recarrega a fase (cada fase tem a sua flag de reinício).
    /// </summary>
    public static GameOverUI Mostrar(string lembrete, Action aoReiniciar)
    {
        var go = new GameObject("GameOverUI");
        var ui = go.AddComponent<GameOverUI>();
        ui.lembrete = lembrete ?? "";
        ui.aoReiniciar = aoReiniciar;
        ui.Show();
        return ui;
    }

    /// <summary>Chamado pelo InteracaoLevelFlow quando o PlayerHealth avisa que a vida zerou.</summary>
    public void Show()
    {
        if (raiz != null)
        {
            return;
        }

        RegistroAprendizagem.Registrar("game_over");

        // Congela a fase por trás do painel (física, armadilhas, plataformas).
        Time.timeScale = 0f;
        pausou = true;
        // E as teclas do robô e dos itens param de chegar na fase.
        JogoBloqueado.Abrir(this);

        Montar();
        // Pequena espera: a tecla que estava apertada na hora da morte não
        // pode "clicar" o Reiniciar sem o jogador nem ver o painel.
        podeReiniciarEm = Time.unscaledTime + 0.5f;
    }

    private void Update()
    {
        if (raiz == null || reiniciando || Time.unscaledTime < podeReiniciarEm)
        {
            return;
        }

        // Enter que escolhe um botão da pausa não reinicia a fase por trás.
        if (JogoBloqueado.PausaPorCima)
        {
            return;
        }

        bool enter = Keyboard.current != null &&
            (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame);
        bool submit = InputManager.Instance != null && InputManager.Instance.SubmitAction.WasPressedThisFrame();

        if (enter || submit)
        {
            Reiniciar();
        }
    }

    private void OnDestroy()
    {
        JogoBloqueado.Fechar(this);

        // Nunca deixa o jogo congelado se este objeto sumir com o painel aberto.
        if (pausou)
        {
            Time.timeScale = 1f;
        }
    }

    private void Reiniciar()
    {
        if (reiniciando || Time.unscaledTime < podeReiniciarEm || JogoBloqueado.PausaPorCima)
        {
            return;
        }

        reiniciando = true;
        Time.timeScale = 1f;
        pausou = false;

        if (aoReiniciar != null)
        {
            aoReiniciar();
        }
        else if (InteracaoLevelFlow.Instance != null)
        {
            InteracaoLevelFlow.Instance.ReloadAfterGameOver();
        }
        else
        {
            TrocaDeCena.Recarregar();
        }
    }

    private void Montar()
    {
        Canvas canvas = VidaUI.CriarCanvas("GameOverUI", sortingOrder);
        raiz = canvas.gameObject;
        // Filho deste objeto: some junto com ele (e com a cena).
        raiz.transform.SetParent(transform, false);
        var rootRect = (RectTransform)raiz.transform;
        TemaLaboratorio tema = TemaLabUI.Tema;

        VidaUI.Esticar(VidaUI.NovaImagem(rootRect, "Escurecer", new Color(0.024f, 0.035f, 0.067f, 0.72f), raycast: true).rectTransform);

        // Painel do kit (tema Laboratório).
        Image painel = TemaLabUI.Imagem(rootRect, "Painel", tema != null ? tema.painelPopup : null, VidaUI.Fundo, raycast: true);
        bool comLembrete = !string.IsNullOrEmpty(lembrete);
        float desce = comLembrete ? 22f : 0f;
        TemaLabUI.Centro(painel.rectTransform, new Vector2(0f, 20f - desce), new Vector2(800f, 540f + desce * 2f));
        Transform p = painel.transform;

        TemaLabUI.Centro(TemaLabUI.Texto(p, "Titulo", titulo, 96, TemaLabUI.Perigo, TemaLabUI.Fonte.Display, TextAlignmentOptions.Center).rectTransform,
            new Vector2(0f, 160f), new Vector2(740f, 110f));
        TemaLabUI.Centro(TemaLabUI.Texto(p, "Mensagem", mensagem, 32, TemaLabUI.TextoSuave, TemaLabUI.Fonte.CorpoForte, TextAlignmentOptions.Center).rectTransform,
            new Vector2(0f, 82f), new Vector2(740f, 50f));

        // A vida zerada, nos mesmos quadrados do HUD: o Game Over também
        // é só mais uma coisa que reage ao dado "vida".
        PlayerHealth vida = FindAnyObjectByType<PlayerHealth>();
        int quadrados = vida != null ? Mathf.Clamp(vida.MaxHealth, 1, 8) : 3;
        Sprite vazio = tema != null ? tema.vidaVazia : null;
        for (int i = 0; i < quadrados; i++)
        {
            Image q = TemaLabUI.Imagem(p, "Vida", vazio, new Color(1f, 1f, 1f, 0.15f));
            q.preserveAspect = true;
            TemaLabUI.Centro(q.rectTransform, new Vector2((i - (quadrados - 1) * 0.5f) * 72f, 6f), new Vector2(64f, 64f));
        }

        if (comLembrete)
        {
            TextMeshProUGUI linha = TemaLabUI.Texto(p, "Lembrete", lembrete, 25, TemaLabUI.Verde, TemaLabUI.Fonte.CorpoForte, TextAlignmentOptions.Center);
            TemaLabUI.Centro(linha.rectTransform, new Vector2(0f, -48f), new Vector2(740f, 36f));
        }

        Button botao = TemaLabUI.Botao(p, "BotaoReiniciar", textoBotao, tema != null ? tema.botaoPrimario : null, TemaLabUI.Azul,
            out _, tema != null ? tema.iconeJogar : null, 36f, Color.white);
        TemaLabUI.Centro((RectTransform)botao.transform, new Vector2(0f, -110f - desce * 1.2f), new Vector2(380f, 108f));
        botao.onClick.AddListener(Reiniciar);
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(botao.gameObject);

        TemaLabUI.Centro(TemaLabUI.Texto(p, "Dica", dicaTecla, 22, TemaLabUI.TextoSuave, TemaLabUI.Fonte.Corpo, TextAlignmentOptions.Center).rectTransform,
            new Vector2(0f, -205f - desce * 1.5f), new Vector2(740f, 34f));
    }
}
