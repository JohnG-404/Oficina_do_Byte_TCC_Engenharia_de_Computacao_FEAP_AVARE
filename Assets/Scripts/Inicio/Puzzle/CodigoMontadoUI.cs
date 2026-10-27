using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Painel que aparece quando um puzzle é resolvido, mostrando o trecho de
/// código (ou de Inspector) que as peças montaram. O jogador sai do puzzle
/// com a "frase inteira", não só com peças encaixadas.
///
/// Montado em runtime (Canvas próprio, por cima de tudo) — não precisa de nada
/// na cena. Fica aberto até o aluno apertar Enter ou clicar (no botão
/// "Continuar" ou em qualquer ponto da tela). No primeiro segundo Enter e
/// clique são ignorados, pro clique que soltou a última peça não fechar o
/// painel sem querer; o botão aparece apagado nesse tempo.
///
/// O parâmetro <c>seconds</c> de <see cref="Show"/> não fecha mais o painel;
/// continua na assinatura só pra não mexer nos
/// campos segundosCodigo já salvos nas cenas.
/// </summary>
public class CodigoMontadoUI : MonoBehaviour
{
    private static readonly Color PanelColor = new Color(0.043f, 0.067f, 0.106f, 0.97f);
    private static readonly Color VerdeEscuro = new Color32(0x2E, 0x8A, 0x5B, 0xFF);

    /// <summary>Tempo em que Enter e clique ficam travados depois de abrir.</summary>
    private const float Trava = 1f;

    private Action onClosed;
    private float canSkipAt;
    private bool closed;
    private CanvasGroup botaoGrupo;

    /// <summary>Mostra o painel e chama <paramref name="onClosed"/> quando ele fechar.</summary>
    /// <param name="seconds">Ignorado: o painel só fecha com Enter ou clique.</param>
    public static void Show(string title, string code, float seconds, Action onClosed)
    {
        var root = new GameObject("CodigoMontadoUI");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // cabe inteira em qualquer proporção
        root.AddComponent<GraphicRaycaster>();

        // Tema Laboratório: véu, janela de editor (painel_codigo) com o título
        // em dourado, código em JetBrains Mono e, embaixo, o botão Continuar.
        TemaLaboratorio tema = TemaLabUI.Tema;
        TemaLabUI.Veu(root.transform, raycast: true);

        Image janela = TemaLabUI.Imagem(root.transform, "Janela", tema != null ? tema.painelCodigo : null, PanelColor);
        // Largo o bastante pras linhas mais compridas (o Raycast da IA) caberem
        // inteiras; a altura cresce com o número de linhas (o Decidir() da IA
        // tem 14), sem passar da tela. +60 pro rodapé do botão.
        int linhas = string.IsNullOrEmpty(code) ? 1 : code.Split('\n').Length;
        float altura = Mathf.Clamp(276f + linhas * 52f, 540f, 1040f);
        TemaLabUI.Centro(janela.rectTransform, new Vector2(0f, altura > 900f ? 0f : 40f), new Vector2(1600f, altura));

        TextMeshProUGUI titleText = TemaLabUI.Rotulo(janela.transform, "Titulo", title, 24f, TemaLabUI.Dourado);
        var trt = titleText.rectTransform;
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.offsetMin = new Vector2(70f, -128f);
        trt.offsetMax = new Vector2(-40f, -88f);

        TextMeshProUGUI codeText = TemaLabUI.Texto(janela.transform, "Codigo", code, 34f, new Color32(0xDC, 0xE6, 0xF5, 0xFF), TemaLabUI.Fonte.Mono, TextAlignmentOptions.TopLeft);
        codeText.lineSpacing = 18f;
        var crt = codeText.rectTransform;
        crt.anchorMin = new Vector2(0f, 0f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.offsetMin = new Vector2(70f, 130f);
        crt.offsetMax = new Vector2(-40f, -146f);
        // Sem quebra de linha: se uma linha não couber, a letra diminui
        // (auto-size) em vez de jogar o fim da linha pra baixo.
        codeText.textWrappingMode = TextWrappingModes.NoWrap;
        codeText.enableAutoSizing = true;
        codeText.fontSizeMin = 18f;
        codeText.fontSizeMax = 34f;

        var ui = root.AddComponent<CodigoMontadoUI>();

        // Botão verde "Continuar ▶" no canto inferior direito da janela, no
        // lugar da antiga barra de tempo, com o texto de apoio ao lado.
        RectTransform miolo = TemaLabUI.CaixaArredondada(janela.transform, "Continuar", VerdeEscuro, TemaLabUI.Verde, 3f, raycast: true);
        RectTransform botaoRt = (RectTransform)miolo.parent;
        TemaLabUI.Posicionar(botaoRt, new Vector2(1f, 0f), new Vector2(-48f, 36f), new Vector2(300f, 76f));
        var botao = botaoRt.gameObject.AddComponent<Button>();
        botao.targetGraphic = miolo.GetComponent<Image>();
        botao.transition = Selectable.Transition.ColorTint;
        ColorBlock cores = botao.colors;
        cores.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        cores.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        cores.selectedColor = Color.white;
        botao.colors = cores;
        botao.onClick.AddListener(ui.Clicou);

        RectTransform conteudo = TemaLabUI.Novo(miolo, "Conteudo");
        TemaLabUI.Esticar(conteudo);
        conteudo.offsetMin = new Vector2(20f, 4f);
        conteudo.offsetMax = new Vector2(-20f, 0f);
        var linha = conteudo.gameObject.AddComponent<HorizontalLayoutGroup>();
        linha.childAlignment = TextAnchor.MiddleCenter;
        linha.spacing = 14f;
        linha.childControlWidth = linha.childControlHeight = true;
        linha.childForceExpandWidth = linha.childForceExpandHeight = false;

        TextMeshProUGUI rotulo = TemaLabUI.Texto(conteudo, "Label", "Continuar", 34f, Color.white, TemaLabUI.Fonte.Display, TextAlignmentOptions.Center);
        rotulo.textWrappingMode = TextWrappingModes.NoWrap;
        if (tema != null && tema.iconeJogar != null)
        {
            Image seta = TemaLabUI.Imagem(conteudo, "Seta", tema.iconeJogar, Color.white);
            seta.preserveAspect = true;
            var le = seta.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = 34f;
        }
        else
        {
            rotulo.text = "Continuar  >";
        }

        TextMeshProUGUI apoio = TemaLabUI.Texto(janela.transform, "Apoio", "Enter ou clique para seguir", 24f, TemaLabUI.TextoSuave, TemaLabUI.Fonte.Corpo, TextAlignmentOptions.Right);
        apoio.textWrappingMode = TextWrappingModes.NoWrap;
        TemaLabUI.Posicionar(apoio.rectTransform, new Vector2(1f, 0f), new Vector2(-372f, 36f), new Vector2(600f, 76f));
        apoio.alignment = TextAlignmentOptions.MidlineRight;

        ui.botaoGrupo = botaoRt.gameObject.AddComponent<CanvasGroup>();
        ui.botaoGrupo.alpha = 0.45f;
        ui.onClosed = onClosed;
        ui.canSkipAt = Time.unscaledTime + Trava;
    }

    private bool Liberado => Time.unscaledTime >= canSkipAt;

    private void Update()
    {
        if (closed)
        {
            return;
        }

        if (botaoGrupo != null)
        {
            botaoGrupo.alpha = Liberado ? 1f : 0.45f;
        }

        // Clique em qualquer ponto (inclusive no botão) ou Enter fecham —
        // menos o clique/Enter dado na pausa por cima do painel.
        bool skip = Liberado && !JogoBloqueado.PausaPorCima &&
            ((Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
             (InputManager.Instance != null && InputManager.Instance.SubmitAction.WasPressedThisFrame()));

        if (skip)
        {
            Fechar();
        }
    }

    private void Clicou()
    {
        if (Liberado && !JogoBloqueado.PausaPorCima)
        {
            Fechar();
        }
    }

    private void Fechar()
    {
        if (closed)
        {
            return;
        }

        closed = true;
        Action callback = onClosed;
        Destroy(gameObject);
        callback?.Invoke();
    }
}
