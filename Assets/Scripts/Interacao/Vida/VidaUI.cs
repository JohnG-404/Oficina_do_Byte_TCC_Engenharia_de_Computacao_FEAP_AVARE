using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Peças de UI montadas por código usadas pela mini-lição de HUD e pelo
/// Game Over da fase "Interacao_Vida" (ver HudLicao, HudPuzzleUI,
/// GameOverUI). Tudo é criado em runtime, com Canvas próprio, pra não
/// depender de nada salvo na cena além do componente.
///
/// Paleta escura igual à dos outros painéis do jogo (CodigoMontadoUI,
/// mockups): fundo #0F1623, texto claro, bordas coloridas. Caixas com cantos
/// arredondados e fontes do tema Laboratório (ver TemaLabUI).
/// </summary>
public static class VidaUI
{
    public static readonly Color Fundo = new Color32(0x0F, 0x16, 0x23, 0xF7);
    public static readonly Color FundoClaro = new Color32(0x16, 0x20, 0x33, 0xFF);
    public static readonly Color FundoPeca = new Color32(0x1A, 0x22, 0x33, 0xFF);
    public static readonly Color Moldura = new Color32(0x2A, 0x35, 0x50, 0xFF);
    public static readonly Color Tracejado = new Color32(0x56, 0x65, 0x7F, 0xFF);
    public static readonly Color Texto = new Color32(0xDC, 0xE6, 0xF5, 0xFF);
    public static readonly Color TextoApagado = new Color32(0x8B, 0x95, 0xA7, 0xFF);
    public static readonly Color Amarelo = new Color32(0xF5, 0xC5, 0x42, 0xFF);
    public static readonly Color Vermelho = new Color32(0xEF, 0x4B, 0x4B, 0xFF);
    public static readonly Color Verde = new Color32(0x4C, 0xC9, 0x7A, 0xFF);

    /// <summary>Canvas Screen Space Overlay com escala por resolução (referência 1920x1080), igual aos outros painéis runtime.</summary>
    public static Canvas CriarCanvas(string nome, int sortingOrder)
    {
        var root = new GameObject(nome, typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // cabe inteira em qualquer proporção

        root.AddComponent<GraphicRaycaster>();
        GarantirEventSystem();
        return canvas;
    }

    /// <summary>
    /// As cenas já têm EventSystem com InputSystemUIInputModule; se por
    /// algum motivo não houver, cria um (sem ele nada de UI recebe clique
    /// nem arrasto).
    /// </summary>
    public static void GarantirEventSystem()
    {
        if (EventSystem.current != null || Object.FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    public static Image NovaImagem(Transform parent, string nome, Color cor, bool raycast = false)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = cor;
        img.raycastTarget = raycast;
        return img;
    }

    public static TextMeshProUGUI NovoTexto(Transform parent, string nome, string texto, float tamanho, Color cor,
        TextAlignmentOptions alinhamento = TextAlignmentOptions.Center, FontStyles estilo = FontStyles.Normal)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        // Fonte do tema Laboratório (Nunito); sem o tema, a padrão do TMP.
        TMP_FontAsset fonte = TemaLabUI.FonteDe(TemaLabUI.Fonte.CorpoForte);
        if (fonte != null)
        {
            tmp.font = fonte;
        }
        tmp.text = texto;
        tmp.fontSize = tamanho;
        tmp.color = cor;
        tmp.fontStyle = estilo;
        tmp.alignment = alinhamento;
        tmp.richText = true;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    /// <summary>Posiciona pelo centro: âncora e pivô no meio do pai, em unidades da resolução de referência.</summary>
    public static RectTransform Centro(RectTransform rt, Vector2 posicao, Vector2 tamanho)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = posicao;
        rt.sizeDelta = tamanho;
        return rt;
    }

    public static void Esticar(RectTransform rt, float margem = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(margem, margem);
        rt.offsetMax = new Vector2(-margem, -margem);
    }

    /// <summary>
    /// Caixa com borda colorida: um Image da cor da borda e, dentro dele, o
    /// fundo. Devolve o fundo (onde vai o conteúdo).
    /// </summary>
    public static RectTransform CaixaComBorda(Transform parent, string nome, Vector2 posicao, Vector2 tamanho,
        Color corBorda, Color corFundo, float espessura = 3f, bool raycast = false)
    {
        Image borda = NovaImagem(parent, nome, corBorda, raycast);
        Centro(borda.rectTransform, posicao, tamanho);
        Image fundo = NovaImagem(borda.transform, "Fundo", corFundo);
        Esticar(fundo.rectTransform, espessura);
        Arredondar(borda);
        Arredondar(fundo);
        return fundo.rectTransform;
    }

    /// <summary>Cantos arredondados do tema Laboratório (sprite branco tingido pela cor do Image).</summary>
    public static void Arredondar(Image img)
    {
        Sprite s = TemaLaboratorio.Atual != null ? TemaLaboratorio.Atual.bordaArredondada : null;
        if (s == null || img == null)
        {
            return;
        }

        img.sprite = s;
        img.type = Image.Type.Sliced;
    }

    /// <summary>
    /// Borda tracejada (UGUI não tem isso pronto): pequenos Images ao longo
    /// das 4 bordas de um retângulo do tamanho informado, centrado no pai.
    /// </summary>
    public static void BordaTracejada(RectTransform parent, Vector2 tamanho, Color cor, float traco = 14f, float espaco = 9f, float espessura = 3f)
    {
        var grupo = new GameObject("Tracejado", typeof(RectTransform));
        var grt = (RectTransform)grupo.transform;
        grt.SetParent(parent, false);
        Esticar(grt);

        float w = tamanho.x;
        float h = tamanho.y;

        // Horizontais (topo e base)
        for (float x = -w * 0.5f; x < w * 0.5f; x += traco + espaco)
        {
            float len = Mathf.Min(traco, w * 0.5f - x);
            float cx = x + len * 0.5f;
            Centro(NovaImagem(grt, "t", cor).rectTransform, new Vector2(cx, h * 0.5f - espessura * 0.5f), new Vector2(len, espessura));
            Centro(NovaImagem(grt, "b", cor).rectTransform, new Vector2(cx, -h * 0.5f + espessura * 0.5f), new Vector2(len, espessura));
        }

        // Verticais (esquerda e direita)
        for (float y = -h * 0.5f; y < h * 0.5f; y += traco + espaco)
        {
            float len = Mathf.Min(traco, h * 0.5f - y);
            float cy = y + len * 0.5f;
            Centro(NovaImagem(grt, "l", cor).rectTransform, new Vector2(-w * 0.5f + espessura * 0.5f, cy), new Vector2(espessura, len));
            Centro(NovaImagem(grt, "r", cor).rectTransform, new Vector2(w * 0.5f - espessura * 0.5f, cy), new Vector2(espessura, len));
        }
    }

    /// <summary>Converte um ponto de tela pra posição local (anchoredPosition com âncora no centro) dentro de um Canvas overlay.</summary>
    public static Vector2 TelaParaLocal(RectTransform canvasRect, Vector2 pontoTela)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, pontoTela, null, out Vector2 local);
        return local;
    }
}
