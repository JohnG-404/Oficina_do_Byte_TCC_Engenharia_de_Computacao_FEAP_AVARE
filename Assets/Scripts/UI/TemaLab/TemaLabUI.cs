using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Peças de UI do tema Laboratório para telas montadas por código: painéis
/// 9-slice, textos com as fontes do kit e botões com troca de sprite
/// (normal, hover, pressionado, foco, desabilitado). Cores iguais aos tokens
/// da proposta aprovada (ver galeria "UI do Prof Byte").
///
/// Tudo tem fallback: sem o asset TemaLaboratorio, os painéis viram Images
/// lisos da cor do token e os textos usam a fonte padrão do TMP.
/// </summary>
public static class TemaLabUI
{
    public static readonly Color Fundo = new Color32(0x0E, 0x13, 0x20, 0xFF);
    public static readonly Color Painel = new Color32(0x1B, 0x24, 0x38, 0xFF);
    public static readonly Color PainelElevado = new Color32(0x24, 0x30, 0x49, 0xFF);
    public static readonly Color Poco = new Color32(0x0F, 0x15, 0x24, 0xFF);
    public static readonly Color Contorno = new Color32(0x33, 0x41, 0x5F, 0xFF);
    public static readonly Color Azul = new Color32(0x4A, 0x8C, 0xF7, 0xFF);
    public static readonly Color Ciano = new Color32(0x5F, 0xD4, 0xE8, 0xFF);
    public static readonly Color Dourado = new Color32(0xF5, 0xC5, 0x42, 0xFF);
    public static readonly Color Verde = new Color32(0x52, 0xC9, 0x8A, 0xFF);
    public static readonly Color Perigo = new Color32(0xE5, 0x64, 0x6A, 0xFF);
    public static readonly Color CorTexto = new Color32(0xE8, 0xEE, 0xF9, 0xFF);
    public static readonly Color TextoSuave = new Color32(0x9A, 0xA8, 0xC4, 0xFF);
    public static readonly Color TextoSeloNumero = new Color32(0x3A, 0x2A, 0x05, 0xFF);
    public static readonly Color CorVeu = new Color(0.024f, 0.035f, 0.067f, 0.62f);

    public enum Fonte { Display, Corpo, CorpoForte, Mono, MonoForte }

    public static TemaLaboratorio Tema => TemaLaboratorio.Atual;

    public static TMP_FontAsset FonteDe(Fonte fonte)
    {
        TemaLaboratorio t = Tema;
        TMP_FontAsset f = null;
        if (t != null)
        {
            switch (fonte)
            {
                case Fonte.Display: f = t.display; break;
                case Fonte.Corpo: f = t.corpo; break;
                case Fonte.CorpoForte: f = t.corpoForte != null ? t.corpoForte : t.corpo; break;
                case Fonte.Mono: f = t.mono; break;
                case Fonte.MonoForte: f = t.monoForte != null ? t.monoForte : t.mono; break;
            }
        }
        return f != null ? f : TMP_Settings.defaultFontAsset;
    }

    // ---------------------------------------------------------------- layout

    public static RectTransform Novo(Transform parent, string nome)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    public static RectTransform Esticar(RectTransform rt, float margem = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(margem, margem);
        rt.offsetMax = new Vector2(-margem, -margem);
        return rt;
    }

    /// <summary>Âncora e pivô no mesmo ponto do pai (0..1), em unidades da resolução de referência.</summary>
    public static RectTransform Posicionar(RectTransform rt, Vector2 ancora, Vector2 posicao, Vector2 tamanho)
    {
        rt.anchorMin = rt.anchorMax = ancora;
        rt.pivot = ancora;
        rt.anchoredPosition = posicao;
        rt.sizeDelta = tamanho;
        return rt;
    }

    public static RectTransform Centro(RectTransform rt, Vector2 posicao, Vector2 tamanho)
    {
        return Posicionar(rt, new Vector2(0.5f, 0.5f), posicao, tamanho);
    }

    // ---------------------------------------------------------------- imagens

    /// <summary>
    /// Image com um sprite do kit (Sliced quando o sprite tem borda 9-slice).
    /// Sem sprite, vira um Image liso com a cor de fallback.
    /// </summary>
    public static Image Imagem(Transform parent, string nome, Sprite sprite, Color corSemSprite, bool raycast = false)
    {
        Image img = Novo(parent, nome).gameObject.AddComponent<Image>();
        img.raycastTarget = raycast;
        DefinirSprite(img, sprite, corSemSprite);
        return img;
    }

    public static void DefinirSprite(Image img, Sprite sprite, Color corSemSprite)
    {
        img.sprite = sprite;
        if (sprite == null)
        {
            img.color = corSemSprite;
            img.type = Image.Type.Simple;
            return;
        }

        img.color = Color.white;
        img.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        img.preserveAspect = false;
    }

    /// <summary>Caixa arredondada tingida (sprite branco do tema) com borda de outra cor. Devolve o miolo.</summary>
    public static RectTransform CaixaArredondada(Transform parent, string nome, Color borda, Color miolo, float espessura, bool raycast = false)
    {
        Sprite s = Tema != null ? Tema.bordaArredondada : null;
        Image fora = Imagem(parent, nome, s, borda, raycast);
        fora.color = borda;
        Image dentro = Imagem(fora.transform, "Fundo", s, miolo);
        dentro.color = miolo;
        Esticar(dentro.rectTransform, espessura);
        return dentro.rectTransform;
    }

    public static Image Veu(Transform parent, bool raycast = true)
    {
        Image veu = Imagem(parent, "Veu", null, CorVeu, raycast);
        Esticar(veu.rectTransform);
        return veu;
    }

    // ---------------------------------------------------------------- textos

    public static TextMeshProUGUI Texto(Transform parent, string nome, string texto, float tamanho, Color cor,
        Fonte fonte = Fonte.Corpo, TextAlignmentOptions alinhamento = TextAlignmentOptions.Left)
    {
        var tmp = Novo(parent, nome).gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = FonteDe(fonte);
        tmp.text = texto;
        tmp.fontSize = tamanho;
        tmp.color = cor;
        tmp.alignment = alinhamento;
        tmp.richText = true;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        if (fonte == Fonte.Mono || fonte == Fonte.MonoForte)
        {
            tmp.characterSpacing = 0f;
        }
        return tmp;
    }

    /// <summary>Rótulo pequeno em fonte de código, maiúsculo e espaçado (o "eyebrow" das telas do kit).</summary>
    public static TextMeshProUGUI Rotulo(Transform parent, string nome, string texto, float tamanho = 22f, Color? cor = null)
    {
        TextMeshProUGUI t = Texto(parent, nome, texto, tamanho, cor ?? Ciano, Fonte.MonoForte);
        t.characterSpacing = 8f;
        t.fontStyle = FontStyles.UpperCase;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    // ---------------------------------------------------------------- botões

    /// <summary>
    /// Botão do kit: fundo 9-slice com troca de sprite por estado e rótulo em
    /// Baloo. Com <paramref name="icone"/>, o ícone vai à esquerda do rótulo.
    /// </summary>
    public static Button Botao(Transform parent, string nome, string rotulo, TemaLaboratorio.Estados estados, Color corSemSprite,
        out TextMeshProUGUI label, Sprite icone = null, float tamanhoFonte = 34f, Color? corTexto = null)
    {
        Image fundo = Imagem(parent, nome, estados != null ? estados.normal : null, corSemSprite, raycast: true);
        var botao = fundo.gameObject.AddComponent<Button>();
        botao.targetGraphic = fundo;
        AplicarEstados(botao, estados);

        RectTransform conteudo = Novo(fundo.transform, "Conteudo");
        Esticar(conteudo);
        conteudo.offsetMin = new Vector2(24f, 6f);
        conteudo.offsetMax = new Vector2(-24f, 0f);
        var linha = conteudo.gameObject.AddComponent<HorizontalLayoutGroup>();
        linha.childAlignment = TextAnchor.MiddleCenter;
        linha.spacing = 14f;
        linha.childControlWidth = true;
        linha.childControlHeight = true;
        linha.childForceExpandWidth = false;
        linha.childForceExpandHeight = false;

        if (icone != null)
        {
            Image ic = Imagem(conteudo, "Icone", icone, Color.white);
            ic.preserveAspect = true;
            var le = ic.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = tamanhoFonte * 1.15f;
        }

        label = Texto(conteudo, "Label", rotulo, tamanhoFonte, corTexto ?? CorTexto, Fonte.Display, TextAlignmentOptions.Center);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return botao;
    }

    public static void AplicarEstados(Selectable alvo, TemaLaboratorio.Estados estados)
    {
        if (estados == null || estados.normal == null)
        {
            return;
        }

        alvo.transition = Selectable.Transition.SpriteSwap;
        alvo.spriteState = new SpriteState
        {
            highlightedSprite = estados.hover != null ? estados.hover : estados.normal,
            pressedSprite = estados.pressed != null ? estados.pressed : estados.normal,
            selectedSprite = estados.focus != null ? estados.focus : estados.normal,
            disabledSprite = estados.disabled != null ? estados.disabled : estados.normal,
        };
    }

    /// <summary>Linha de opção (painel_linha): hover e foco trocam para painel_linha_foco.</summary>
    public static Button Linha(Transform parent, string nome)
    {
        TemaLaboratorio t = Tema;
        Image fundo = Imagem(parent, nome, t != null ? t.painelLinha : null, Painel, raycast: true);
        var botao = fundo.gameObject.AddComponent<Button>();
        botao.targetGraphic = fundo;
        if (t != null && t.painelLinha != null)
        {
            AplicarEstados(botao, new TemaLaboratorio.Estados
            {
                normal = t.painelLinha,
                hover = t.painelLinhaFoco,
                pressed = t.painelLinhaFoco,
                focus = t.painelLinhaFoco,
                disabled = t.painelLinha,
            });
        }
        return botao;
    }

    /// <summary>Placa azul de título (painel_titulo) com texto em Baloo.</summary>
    public static TextMeshProUGUI Placa(Transform parent, string texto, Vector2 posicao, Vector2 tamanho, float tamanhoFonte = 48f)
    {
        TemaLaboratorio t = Tema;
        Image placa = Imagem(parent, "Placa", t != null ? t.painelTitulo : null, Azul);
        Centro(placa.rectTransform, posicao, tamanho);
        TextMeshProUGUI txt = Texto(placa.transform, "Titulo", texto, tamanhoFonte, Color.white, Fonte.Display, TextAlignmentOptions.Center);
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        txt.enableAutoSizing = true;
        txt.fontSizeMin = 24f;
        txt.fontSizeMax = tamanhoFonte;
        RectTransform r = Esticar(txt.rectTransform);
        r.offsetMin = new Vector2(28f, 12f);
        r.offsetMax = new Vector2(-28f, -4f);
        return txt;
    }

    /// <summary>Legenda de teclas (tecla + ação), como no rodapé do menu.</summary>
    public static RectTransform LegendaTeclas(Transform parent, params string[] teclaAcao)
    {
        RectTransform raiz = Novo(parent, "LegendaTeclas");
        Posicionar(raiz, new Vector2(1f, 0f), new Vector2(-64f, 22f), new Vector2(900f, 52f));
        var linha = raiz.gameObject.AddComponent<HorizontalLayoutGroup>();
        linha.childAlignment = TextAnchor.MiddleRight;
        linha.spacing = 12f;
        linha.childControlWidth = true;
        linha.childControlHeight = true;
        linha.childForceExpandWidth = false;
        linha.childForceExpandHeight = false;

        for (int i = 0; i + 1 < teclaAcao.Length; i += 2)
        {
            RectTransform tecla = CaixaArredondada(raiz, "Tecla", Contorno, Painel, 3f);
            tecla.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var fit = tecla.parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            fit.padding = new RectOffset(12, 12, 6, 8);
            fit.childControlWidth = fit.childControlHeight = true;
            fit.childForceExpandWidth = fit.childForceExpandHeight = false;
            TextMeshProUGUI k = Texto(tecla.parent, "K", teclaAcao[i], 22f, CorTexto, Fonte.MonoForte, TextAlignmentOptions.Center);
            k.textWrappingMode = TextWrappingModes.NoWrap;
            k.transform.SetAsLastSibling();

            TextMeshProUGUI acao = Texto(raiz, "Acao", teclaAcao[i + 1], 26f, TextoSuave, Fonte.CorpoForte);
            acao.textWrappingMode = TextWrappingModes.NoWrap;
            acao.margin = new Vector4(0f, 0f, 18f, 0f);
        }
        return raiz;
    }
}
