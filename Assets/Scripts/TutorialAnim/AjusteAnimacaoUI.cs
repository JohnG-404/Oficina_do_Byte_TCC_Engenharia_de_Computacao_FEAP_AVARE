using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Painel "Ajuste a animação" da cena Tutorial_Anim (ver AjusteAnimacao e
/// AnimationSequencePuzzle): botões de velocidade (6/12/24 FPS), botão de
/// Loop ligado/desligado, uma prévia que toca os quadros já montados na
/// velocidade escolhida e um texto curto explicando a opção marcada.
///
/// Montado todo em runtime (Canvas próprio), no mesmo visual do
/// CodigoMontadoUI (painel escuro com borda dourada), pra não precisar
/// mexer na cena. Fica acima da caixa de diálogo do Prof, que continua
/// visível embaixo. Enquanto o Prof fala, os botões ficam travados (o
/// jogador lê primeiro e testa depois); a prévia continua tocando.
///
/// A prévia também é repassada (aoTrocarQuadro) pro SpriteRenderer de
/// prévia que já existe no mundo, então os dois mostram a mesma coisa.
/// </summary>
public class AjusteAnimacaoUI : MonoBehaviour
{
    // Cores do tema Laboratório Tecno (mesmos tokens do menu e da Oficina).
    private static readonly Color InkColor = new Color32(0x0A, 0x0E, 0x17, 0xFF);
    private static readonly Color PanelColor = new Color32(0x1B, 0x24, 0x38, 0xFA);
    private static readonly Color GoldColor = new Color32(0xF5, 0xC5, 0x42, 0xFF);
    private static readonly Color BlueColor = new Color32(0x4A, 0x8C, 0xF7, 0xFF);
    private static readonly Color TextColor = new Color32(0xE8, 0xEE, 0xF9, 0xFF);
    private static readonly Color MutedColor = new Color32(0x9A, 0xA8, 0xC4, 0xFF);
    private static readonly Color ButtonFill = new Color32(0x24, 0x30, 0x49, 0xFF);
    private static readonly Color ButtonBorder = new Color32(0x34, 0x42, 0x5F, 0xFF);
    private static readonly Color PreviewFill = new Color32(0x0B, 0x14, 0x24, 0xFF);

    /// <summary>Fontes e sprite arredondado do tema (preenchidos pela cena, via OficinaAnimacaoHUD). Sem eles, usa o padrão do TMP e cantos retos.</summary>
    public static TMP_FontAsset FonteTitulo;
    public static TMP_FontAsset FonteTexto;
    public static Sprite SpriteArredondado;

    // Medidas do painel (referência 1920x1080). O painel fica deslocado pra
    // cima pra não cobrir a caixa de diálogo, que ocupa a faixa de baixo.
    private const float BorderWidth = 1340f;
    private const float BorderHeight = 680f;
    private const float BorderInset = 4f;
    private const float PanelOffsetY = 120f;

    private sealed class Botao
    {
        public Button button;
        public Image borda;
        public Image fundo;
        public TextMeshProUGUI texto;
    }

    private AjusteAnimacao dados;
    private Sprite[] quadros;
    private Action<Sprite> aoTrocarQuadro;
    private Action<int, bool, int> aoConfirmar;

    private int fpsEscolhido;
    private bool loopEscolhido;
    private int speedEscolhido = 1;

    private int quadroAtual;
    private float acumulado;
    private bool parado;
    private float voltaEm;
    private bool concluido;

    private CanvasGroup grupo;
    private Image imagemPrevia;
    private TextMeshProUGUI textoQuadro;
    private TextMeshProUGUI textoExplicacao;
    private TextMeshProUGUI textoRetorno;
    private int[] fpsDosBotoes;
    private Botao[] botoesFps;
    private Botao botaoLoop;
    private Botao[] botoesSpeed;
    private Botao botaoConfirmar;

    /// <summary>Travado enquanto o Prof fala ou depois de o jogador acertar.</summary>
    private bool Bloqueado => concluido || DialogueManager.IsAnyDialogueActive;

    /// <summary>
    /// Cria o painel. <paramref name="aoConfirmar"/> recebe a velocidade e o
    /// loop escolhidos (e o Speed, 1 ou -1, quando o ajuste usa Speed) quando
    /// o jogador clica em Confirmar; quem avalia e responde é o
    /// AnimationSequencePuzzle.
    /// </summary>
    public static AjusteAnimacaoUI Show(AjusteAnimacao dados, Sprite[] quadros, Action<Sprite> aoTrocarQuadro, Action<int, bool, int> aoConfirmar)
    {
        GarantirEventSystem();

        var root = new GameObject("AjusteAnimacaoUI");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // cabe inteira em qualquer proporção
        root.AddComponent<GraphicRaycaster>();

        var ui = root.AddComponent<AjusteAnimacaoUI>();
        ui.grupo = root.AddComponent<CanvasGroup>();
        ui.dados = dados;
        ui.quadros = quadros ?? Array.Empty<Sprite>();
        ui.aoTrocarQuadro = aoTrocarQuadro;
        ui.aoConfirmar = aoConfirmar;
        ui.fpsEscolhido = dados.fpsInicial > 0 ? dados.fpsInicial : 12;
        ui.loopEscolhido = dados.loopInicial;
        ui.speedEscolhido = dados.speedInicial < 0 ? -1 : 1;
        ui.Montar();
        ui.AtualizarSelecao();
        ui.MostrarQuadro(0);
        return ui;
    }

    /// <summary>Trava os botões (resposta certa já dada); a prévia continua tocando até Fechar.</summary>
    public void MarcarConcluido()
    {
        concluido = true;
    }

    /// <summary>Mostra um retorno em texto no próprio painel (usado quando a cena não tem DialogueManager).</summary>
    public void MostrarRetorno(string texto)
    {
        if (textoRetorno != null)
        {
            textoRetorno.text = texto;
        }
    }

    public void Fechar()
    {
        Destroy(gameObject);
    }

    private void Update()
    {
        grupo.interactable = !Bloqueado;
        AvancarPrevia(Time.deltaTime);
    }

    // Mesma lógica do mockup: com loop, volta pro primeiro quadro; sem loop,
    // para no último, espera um pouco (pausaSemLoop) e toca de novo, pra dar
    // pra ver outra vez sem o jogador precisar fazer nada.
    private void AvancarPrevia(float dt)
    {
        if (quadros.Length == 0)
        {
            return;
        }

        if (parado)
        {
            if (!loopEscolhido && Time.time >= voltaEm)
            {
                parado = false;
                acumulado = 0f;
                MostrarQuadro(0);
            }
            return;
        }

        float passo = 1f / Mathf.Max(1, fpsEscolhido);
        acumulado += dt;

        int proximo = quadroAtual;
        while (acumulado >= passo)
        {
            acumulado -= passo;
            proximo++;

            if (proximo >= quadros.Length)
            {
                if (loopEscolhido)
                {
                    proximo = 0;
                }
                else
                {
                    proximo = quadros.Length - 1;
                    parado = true;
                    voltaEm = Time.time + Mathf.Max(0.2f, dados.pausaSemLoop);
                    acumulado = 0f;
                    break;
                }
            }
        }

        if (proximo != quadroAtual || parado)
        {
            MostrarQuadro(proximo);
        }
    }

    private void MostrarQuadro(int indice)
    {
        if (quadros.Length == 0)
        {
            textoQuadro.text = "";
            return;
        }

        quadroAtual = Mathf.Clamp(indice, 0, quadros.Length - 1);
        // Speed -1: o mesmo clipe, lido do último quadro pro primeiro.
        int real = speedEscolhido < 0 ? quadros.Length - 1 - quadroAtual : quadroAtual;
        Sprite sprite = quadros[real];
        imagemPrevia.sprite = sprite;
        imagemPrevia.enabled = sprite != null;
        aoTrocarQuadro?.Invoke(sprite);

        textoQuadro.text = parado
            ? $"quadro {real + 1} de {quadros.Length} (parou)"
            : $"quadro {real + 1} de {quadros.Length}";
    }

    private void EscolherFps(int fps)
    {
        Desselecionar();
        if (Bloqueado)
        {
            return;
        }

        fpsEscolhido = fps;
        acumulado = 0f;
        if (parado)
        {
            parado = false;
            MostrarQuadro(0);
        }

        textoRetorno.text = "";
        AtualizarSelecao();
    }

    private void AlternarLoop()
    {
        Desselecionar();
        if (Bloqueado)
        {
            return;
        }

        loopEscolhido = !loopEscolhido;
        parado = false;
        acumulado = 0f;
        MostrarQuadro(0);

        textoRetorno.text = "";
        AtualizarSelecao();
    }

    private void EscolherSpeed(int speed)
    {
        Desselecionar();
        if (Bloqueado)
        {
            return;
        }

        speedEscolhido = speed;
        parado = false;
        acumulado = 0f;
        MostrarQuadro(0);

        textoRetorno.text = "";
        AtualizarSelecao();
    }

    private void Confirmar()
    {
        Desselecionar();
        if (Bloqueado)
        {
            return;
        }

        textoRetorno.text = "";
        aoConfirmar?.Invoke(fpsEscolhido, loopEscolhido, speedEscolhido);
    }

    // Sem isso, o botão clicado fica "selecionado" no EventSystem e o
    // Enter/Espaço usado pra avançar o diálogo do Prof clicaria nele de novo.
    private static void Desselecionar()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void AtualizarSelecao()
    {
        for (int i = 0; i < botoesFps.Length; i++)
        {
            PintarBotao(botoesFps[i], fpsDosBotoes[i] == fpsEscolhido);
        }

        botaoLoop.texto.text = loopEscolhido ? dados.textoBotaoLoopLigado : dados.textoBotaoLoopDesligado;
        PintarBotao(botaoLoop, loopEscolhido);

        if (botoesSpeed != null)
        {
            PintarBotao(botoesSpeed[0], speedEscolhido > 0);
            PintarBotao(botoesSpeed[1], speedEscolhido < 0);
        }

        textoExplicacao.text = dados.TextoExplicacao(fpsEscolhido, loopEscolhido, speedEscolhido);
    }

    private static void PintarBotao(Botao botao, bool marcado)
    {
        botao.borda.color = marcado ? InkColor : ButtonBorder;
        botao.fundo.color = marcado ? BlueColor : ButtonFill;
        botao.texto.color = marcado ? Color.white : TextColor;
    }

    // ---------------------------------------------------------------------
    // Montagem do painel
    // ---------------------------------------------------------------------

    private void Montar()
    {
        var borda = NewImage(transform, "Borda", InkColor);
        RectTransform brt = borda.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = new Vector2(0f, PanelOffsetY);
        brt.sizeDelta = new Vector2(BorderWidth, BorderHeight);

        var painel = NewImage(borda.transform, "Painel", PanelColor);
        RectTransform prt = painel.rectTransform;
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = new Vector2(BorderInset, BorderInset);
        prt.offsetMax = new Vector2(-BorderInset, -BorderInset);
        Transform p = painel.transform;

        var titulo = NewText(p, "Titulo", dados.titulo, 40, FontStyles.Bold, GoldColor);
        if (FonteTitulo != null)
        {
            titulo.font = FonteTitulo;
            titulo.fontStyle = FontStyles.Normal;
        }
        Place(titulo.rectTransform, 40, 20, 1252, 50);

        var pedido = NewText(p, "Pedido", dados.pedido, 26, FontStyles.Normal, TextColor);
        Place(pedido.rectTransform, 40, 76, 1252, 80);
        pedido.enableAutoSizing = true;
        pedido.fontSizeMin = 18f;
        pedido.fontSizeMax = 26f;

        // Prévia (coluna da esquerda)
        var previaBorda = NewImage(p, "PreviaBorda", ButtonBorder);
        Place(previaBorda.rectTransform, 40, 170, 340, 340);
        var previaFundo = NewImage(previaBorda.transform, "PreviaFundo", PreviewFill);
        Stretch(previaFundo.rectTransform, 4f);
        imagemPrevia = NewImage(previaFundo.transform, "Previa", Color.white);
        imagemPrevia.preserveAspect = true;
        Stretch(imagemPrevia.rectTransform, 24f);

        textoQuadro = NewText(p, "Quadro", "", 22, FontStyles.Normal, MutedColor);
        textoQuadro.alignment = TextAlignmentOptions.Center;
        Place(textoQuadro.rectTransform, 40, 520, 340, 34);

        // Controles (coluna da direita)
        const float colunaX = 420f;

        var rotuloFps = NewText(p, "RotuloFps", dados.rotuloFps, 24, FontStyles.Bold, TextColor);
        Place(rotuloFps.rectTransform, colunaX, 170, 872, 36);

        fpsDosBotoes = dados.fpsOpcoes != null && dados.fpsOpcoes.Length > 0 ? dados.fpsOpcoes : new[] { 6, 12, 24 };
        botoesFps = new Botao[fpsDosBotoes.Length];
        for (int i = 0; i < fpsDosBotoes.Length; i++)
        {
            int fps = fpsDosBotoes[i];
            Botao botao = NewButton(p, $"Fps{fps}", $"{fps} FPS", () => EscolherFps(fps));
            Place((RectTransform)botao.button.transform, colunaX + i * 196f, 212, 180, 64);
            botoesFps[i] = botao;
        }

        var rotuloLoop = NewText(p, "RotuloLoop", dados.rotuloLoop, 24, FontStyles.Bold, TextColor);
        Place(rotuloLoop.rectTransform, colunaX, 296, 872, 36);

        botaoLoop = NewButton(p, "Loop", dados.textoBotaoLoopLigado, AlternarLoop);
        Place((RectTransform)botaoLoop.button.transform, colunaX, 338, 300, 64);

        // Speed (só nos ajustes que usam): ao lado do Loop.
        if (dados.usarSpeed)
        {
            const float speedX = colunaX + 340f;
            var rotuloSpeed = NewText(p, "RotuloSpeed", dados.rotuloSpeed, 24, FontStyles.Bold, TextColor);
            Place(rotuloSpeed.rectTransform, speedX, 296, 532, 36);

            botoesSpeed = new Botao[2];
            int[] valores = { 1, -1 };
            for (int i = 0; i < valores.Length; i++)
            {
                int speed = valores[i];
                Botao botao = NewButton(p, $"Speed{speed}", $"Speed {speed}", () => EscolherSpeed(speed));
                Place((RectTransform)botao.button.transform, speedX + i * 256f, 338, 240, 64);
                botoesSpeed[i] = botao;
            }
        }

        textoExplicacao = NewText(p, "Explicacao", "", 24, FontStyles.Normal, TextColor);
        Place(textoExplicacao.rectTransform, colunaX, 420, 872, 140);
        textoExplicacao.enableAutoSizing = true;
        textoExplicacao.fontSizeMin = 16f;
        textoExplicacao.fontSizeMax = 24f;
        textoExplicacao.lineSpacing = 8f;

        // Rodapé: retorno em texto (sem DialogueManager) + Confirmar
        textoRetorno = NewText(p, "Retorno", "", 22, FontStyles.Italic, GoldColor);
        Place(textoRetorno.rectTransform, 40, 576, 980, 72);
        textoRetorno.enableAutoSizing = true;
        textoRetorno.fontSizeMin = 14f;
        textoRetorno.fontSizeMax = 22f;

        botaoConfirmar = NewButton(p, "Confirmar", dados.textoConfirmar, Confirmar);
        Place((RectTransform)botaoConfirmar.button.transform, 1052, 576, 240, 72);
        botaoConfirmar.borda.color = InkColor;
        botaoConfirmar.fundo.color = GoldColor;
        botaoConfirmar.texto.color = InkColor;
        botaoConfirmar.texto.fontStyle = FontStyles.Bold;
        if (FonteTitulo != null)
        {
            botaoConfirmar.texto.font = FonteTitulo;
            botaoConfirmar.texto.fontStyle = FontStyles.Normal;
        }
    }

    private static Botao NewButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction onClick)
    {
        var borda = NewImage(parent, name, ButtonBorder);
        borda.raycastTarget = true;

        var fundo = NewImage(borda.transform, "Fundo", ButtonFill);
        Stretch(fundo.rectTransform, 3f);

        var texto = NewText(fundo.transform, "Texto", label, 26, FontStyles.Bold, TextColor);
        texto.alignment = TextAlignmentOptions.Center;
        Stretch(texto.rectTransform, 4f);

        var button = borda.gameObject.AddComponent<Button>();
        button.targetGraphic = fundo;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock cores = ColorBlock.defaultColorBlock;
        cores.normalColor = Color.white;
        cores.highlightedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        cores.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        cores.selectedColor = Color.white;
        cores.disabledColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        button.colors = cores;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(onClick);

        return new Botao { button = button, borda = borda, fundo = fundo, texto = texto };
    }

    private static Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        if (SpriteArredondado != null && name != "Previa")
        {
            img.sprite = SpriteArredondado;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 2f;
        }
        return img;
    }

    private static TextMeshProUGUI NewText(Transform parent, string name, string text, float size, FontStyles style, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (FonteTexto != null)
        {
            tmp.font = FonteTexto;
        }
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.richText = true;
        tmp.raycastTarget = false;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    // Posiciona a partir do canto superior esquerdo do painel (x pra
    // direita, y pra baixo), que é como o layout foi pensado.
    private static void Place(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    // A cena já tem EventSystem (TutorialAnimSceneWiring cria um), mas o
    // painel depende dele pros cliques, então garante por segurança.
    private static void GarantirEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
