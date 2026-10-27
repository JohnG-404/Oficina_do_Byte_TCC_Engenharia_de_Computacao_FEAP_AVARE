using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tema Laboratório nas fases (Fisica, Aplicacao_Fisica, Animacao, IA,
/// IA_Aplicacao e o diálogo de todas): em vez de editar cada cena, as peças
/// antigas são restilizadas em runtime quando a cena carrega ou quando o
/// componente acorda. Desde 2026-10-14 o resultado também fica salvo nas
/// cenas (Tools > Cenas > Salvar visual do Play nas cenas), para o Editor
/// mostrar o mesmo que o Play; aqui tudo pula o que já está estilizado.
///
/// O que faz:
/// - Caixa de diálogo do Prof Byte (DialogueManager): painel do kit, placa com
///   o nome, retrato parado e texto em Nunito.
/// - "Fase Concluída!" (LevelCompleteUI): popup do kit com o selo de check.
/// - Vida em quadrados (HeartHealthUI): placa VIDA com os sprites do kit.
/// - Fontes: LiberationSans e Bangers viram as do kit em todos os textos da
///   cena (código em JetBrains Mono, o resto em Nunito, títulos em Baloo).
/// - Cartões cinza do EditorKit (puzzles da Fisica e da IA) tingidos de azul.
/// - Inventário da Aplicacao_Fisica: janela do kit.
/// </summary>
public static class TemaLabFases
{
    private const string MarcaEstilizado = "TemaLab";

    // ---------------------------------------------------------------- cena

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Registrar()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        SceneManager.sceneLoaded += AoCarregarCena;
    }

    // sceneLoaded roda depois dos Awake/OnEnable e antes dos Start.
    private static void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        AplicarNaCena(cena);
    }

    /// <summary>Fontes, cartões do EditorKit e inventário de uma cena inteira (público para as capturas de conferência no Editor).</summary>
    public static void AplicarNaCena(Scene cena)
    {
        if (TemaLabUI.Tema == null)
        {
            return;
        }

        foreach (GameObject raiz in cena.GetRootGameObjects())
        {
            foreach (TMP_Text t in raiz.GetComponentsInChildren<TMP_Text>(true))
            {
                TrocarFonte(t);
            }

            foreach (SpriteRenderer sr in raiz.GetComponentsInChildren<SpriteRenderer>(true))
            {
                TingirCartaoEditor(sr);
            }

            foreach (Image img in raiz.GetComponentsInChildren<Image>(true))
            {
                if (img.sprite != null && img.sprite.name.StartsWith("inventory_panel"))
                {
                    EstilizarInventario(img);
                }
            }
        }
    }

    // ---------------------------------------------------------------- fontes

    private static readonly Regex Tags = new Regex("<[^>]+>");
    // Código: tem ( ) = _ ; { } [ ] < >, ou é uma palavra só com ponto, número
    // ou camelCase (transform.position, Rigidbody2D, isGrounded). Frases com
    // espaço e ponto final continuam em Nunito.
    private static readonly Regex Codigo = new Regex(@"[()=_;{}\[\]<>]|^\S*\.\S+$|^[a-z]+[A-Z]\w*$|^[A-Z]\w*\d\w*$|^[A-Z][a-z]+[A-Z]\w*$");

    /// <summary>JetBrains Mono para o que parece código (Rigidbody2D, isGrounded, Distance &lt; x), Nunito para o resto.</summary>
    public static TemaLabUI.Fonte FonteParaTexto(string texto, bool forte = true)
    {
        string limpo = Tags.Replace(texto ?? "", "").Trim();
        if (Codigo.IsMatch(limpo))
        {
            return forte ? TemaLabUI.Fonte.MonoForte : TemaLabUI.Fonte.Mono;
        }
        return forte ? TemaLabUI.Fonte.CorpoForte : TemaLabUI.Fonte.Corpo;
    }

    /// <summary>Troca LiberationSans/Bangers pela fonte do kit certa para aquele texto.</summary>
    public static void TrocarFonte(TMP_Text t)
    {
        if (t == null || t.font == null)
        {
            return;
        }

        string nome = t.font.name;
        bool liberation = nome.StartsWith("LiberationSans");
        bool bangers = nome.StartsWith("Bangers");
        if (!liberation && !bangers)
        {
            return;
        }

        TemaLabUI.Fonte fonte;
        if (bangers)
        {
            fonte = TemaLabUI.Fonte.Display;
        }
        else
        {
            fonte = FonteParaTexto(t.text);
        }

        TMP_FontAsset nova = TemaLabUI.FonteDe(fonte);
        if (nova == null || nova == t.font)
        {
            return;
        }

        t.font = nova;
        t.fontSharedMaterial = nova.material;

        // A JetBrains Mono não tem itálico (ficaria inclinada à força).
        if (fonte == TemaLabUI.Fonte.MonoForte)
        {
            t.fontStyle &= ~FontStyles.Italic;
        }

        // As fontes do kit são mais largas: o texto encolhe se não couber na caixa original.
        if (!t.enableAutoSizing)
        {
            float tamanho = t.fontSize;
            t.enableAutoSizing = true;
            t.fontSizeMax = tamanho;
            t.fontSizeMin = tamanho * 0.5f;
        }
    }

    // ---------------------------------------------------------------- puzzles

    // Os cartões do EditorKit são cinza-editor; um tom azul os aproxima do kit
    // sem trocar os sprites (que já têm tamanho e borda certos nas cenas).
    private static readonly Color TomAzul = new Color(0.74f, 0.86f, 1f, 1f);

    private static void TingirCartaoEditor(SpriteRenderer sr)
    {
        if (sr.sprite == null)
        {
            return;
        }

        string n = sr.sprite.name;
        bool cartao = n.StartsWith("janela_") || n.StartsWith("cartao_") || n.StartsWith("aba_") || n.StartsWith("grade_fundo") || n.StartsWith("divisoria");
        if (!cartao || sr.color != Color.white)
        {
            return;
        }

        sr.color = TomAzul;
    }

    // ---------------------------------------------------------------- diálogo

    /// <summary>Restiliza a caixa de diálogo antiga (DialogueBox) no padrão do kit. Seguro chamar mais de uma vez.</summary>
    public static void EstilizarDialogo(GameObject painel, TMP_Text texto)
    {
        TemaLaboratorio tema = TemaLabUI.Tema;
        if (painel == null || texto == null || tema == null || painel.transform.Find(MarcaEstilizado) != null)
        {
            return;
        }

        var caixa = painel.transform as RectTransform;
        var fundo = painel.GetComponent<Image>();
        if (caixa == null || fundo == null)
        {
            return;
        }

        new GameObject(MarcaEstilizado, typeof(RectTransform)).transform.SetParent(caixa, false);

        TemaLabUI.DefinirSprite(fundo, tema.painelPopup, TemaLabUI.Painel);
        caixa.anchorMin = caixa.anchorMax = new Vector2(0.5f, 0f);
        caixa.pivot = new Vector2(0.5f, 0f);
        caixa.anchoredPosition = new Vector2(0f, 30f);
        caixa.sizeDelta = new Vector2(1520f, 250f);

        // Retrato: o antigo "Prof" (com fundo cinza) some; entra o Prof Byte parado.
        Transform profAntigo = caixa.Find("Prof");
        if (profAntigo != null)
        {
            foreach (Graphic g in profAntigo.GetComponentsInChildren<Graphic>(true))
            {
                g.enabled = false;
            }
        }

        if (tema.retratoProfByte != null)
        {
            Image retrato = TemaLabUI.Imagem(caixa, "RetratoProfByte", tema.retratoProfByte, Color.white);
            retrato.preserveAspect = true;
            TemaLabUI.Posicionar(retrato.rectTransform, new Vector2(0f, 0.5f), new Vector2(36f, -6f), new Vector2(150f, 134f));
        }

        // Nome na placa azul, saindo pela borda de cima.
        Transform nomeAntigo = caixa.Find("CaracterName");
        string nome = "Prof Byte";
        if (nomeAntigo != null)
        {
            TMP_Text t = nomeAntigo.GetComponent<TMP_Text>();
            if (t != null && !string.IsNullOrEmpty(t.text))
            {
                nome = t.text;
            }
            nomeAntigo.gameObject.SetActive(false);
        }

        TextMeshProUGUI placa = TemaLabUI.Placa(caixa, nome, Vector2.zero, new Vector2(300f, 80f), 36f);
        TemaLabUI.Posicionar((RectTransform)placa.transform.parent, new Vector2(0f, 1f), new Vector2(210f, 38f), new Vector2(300f, 80f));

        // Texto da fala.
        texto.font = TemaLabUI.FonteDe(TemaLabUI.Fonte.CorpoForte);
        texto.fontSharedMaterial = texto.font.material;
        texto.fontSize = 34f;
        texto.enableAutoSizing = true;
        texto.fontSizeMin = 22f;
        texto.fontSizeMax = 34f;
        texto.color = TemaLabUI.CorTexto;
        texto.lineSpacing = 4f;
        texto.alignment = TextAlignmentOptions.TopLeft;
        RectTransform rt = texto.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(220f, 52f);
        rt.offsetMax = new Vector2(-60f, -58f);

        TextMeshProUGUI dica = TemaLabUI.Texto(caixa, "Avancar", "▼ Enter ou clique", 22f, TemaLabUI.Ciano, TemaLabUI.Fonte.MonoForte, TextAlignmentOptions.BottomRight);
        TemaLabUI.Posicionar(dica.rectTransform, new Vector2(1f, 0f), new Vector2(-48f, 22f), new Vector2(340f, 30f));
    }

    // ---------------------------------------------------------------- fim de fase

    /// <summary>Monta o popup "Fase Concluída!" do kit dentro do painel antigo (que só tinha um texto).</summary>
    public static void EstilizarFimDeFase(GameObject painel)
    {
        TemaLaboratorio tema = TemaLabUI.Tema;
        if (painel == null || tema == null || painel.transform.Find("Janela") != null)
        {
            return;
        }

        Image veu = painel.GetComponent<Image>();
        if (veu != null)
        {
            veu.sprite = null;
            veu.color = new Color(0.024f, 0.035f, 0.067f, 0.62f);
        }

        string titulo = "Fase Concluída!";
        foreach (Transform filho in painel.transform)
        {
            TMP_Text t = filho.GetComponent<TMP_Text>();
            if (t != null && !string.IsNullOrEmpty(t.text))
            {
                titulo = t.text;
            }
            filho.gameObject.SetActive(false);
        }

        Image janela = TemaLabUI.Imagem(painel.transform, "Janela", tema.painelPopup, TemaLabUI.Painel);
        TemaLabUI.Centro(janela.rectTransform, Vector2.zero, new Vector2(800f, 440f));
        Image selo = TemaLabUI.Imagem(janela.transform, "Selo", tema.seloCheck, TemaLabUI.Verde);
        selo.preserveAspect = true;
        TemaLabUI.Centro(selo.rectTransform, new Vector2(0f, 70f), new Vector2(140f, 140f));
        TextMeshProUGUI txt = TemaLabUI.Texto(janela.transform, "Text", titulo, 84f, TemaLabUI.CorTexto, TemaLabUI.Fonte.Display, TextAlignmentOptions.Center);
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        TemaLabUI.Centro(txt.rectTransform, new Vector2(0f, -86f), new Vector2(740f, 110f));
    }

    // ---------------------------------------------------------------- vida

    /// <summary>
    /// Põe os quadrados de vida antigos (Images lisos) numa placa "VIDA" no
    /// canto de cima à esquerda, com os sprites cheio/vazio do kit. Devolve false se o
    /// tema não tiver os sprites (aí a vida fica como estava).
    /// </summary>
    public static bool EstilizarVida(RectTransform raiz, Image[] quadrados)
    {
        TemaLaboratorio tema = TemaLabUI.Tema;
        if (raiz == null || quadrados == null || quadrados.Length == 0 || tema == null || tema.vidaCheia == null)
        {
            return false;
        }

        var canvas = raiz.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return false;
        }

        Image placa = TemaLabUI.Imagem(canvas.transform, "PlacaVida", tema.painelLinha, TemaLabUI.Painel);
        float largura = 150f + quadrados.Length * 68f;
        // Canto de cima à esquerda, onde a vida já ficava (o painel de estados da Animacao fica à direita).
        TemaLabUI.Posicionar(placa.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -36f), new Vector2(largura, 96f));
        placa.transform.SetAsFirstSibling();

        TextMeshProUGUI rotulo = TemaLabUI.Rotulo(placa.transform, "Rotulo", "VIDA", 20f);
        TemaLabUI.Posicionar(rotulo.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(90f, 30f));

        raiz.SetParent(placa.transform, false);
        raiz.anchorMin = raiz.anchorMax = raiz.pivot = new Vector2(0f, 0.5f);
        raiz.anchoredPosition = new Vector2(112f, 0f);
        raiz.sizeDelta = new Vector2(quadrados.Length * 68f, 56f);

        for (int i = 0; i < quadrados.Length; i++)
        {
            Image q = quadrados[i];
            if (q == null)
            {
                continue;
            }
            q.sprite = tema.vidaCheia;
            q.type = Image.Type.Simple;
            q.preserveAspect = true;
            RectTransform r = q.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 0.5f);
            r.anchoredPosition = new Vector2(i * 68f, 0f);
            r.sizeDelta = new Vector2(56f, 56f);
        }
        return true;
    }

    // ---------------------------------------------------------------- inventário

    private static void EstilizarInventario(Image painel)
    {
        TemaLaboratorio tema = TemaLabUI.Tema;
        if (tema == null || tema.painelJanela == null)
        {
            return;
        }

        TemaLabUI.DefinirSprite(painel, tema.painelJanela, TemaLabUI.Painel);
        painel.pixelsPerUnitMultiplier = 1f;

        // Nomes das ferramentas em fonte de código, claros e numa linha só.
        foreach (TMP_Text rotulo in painel.GetComponentsInChildren<TMP_Text>(true))
        {
            rotulo.font = TemaLabUI.FonteDe(TemaLabUI.Fonte.MonoForte);
            rotulo.fontSharedMaterial = rotulo.font.material;
            rotulo.color = TemaLabUI.CorTexto;
            rotulo.textWrappingMode = TextWrappingModes.NoWrap;
            rotulo.enableAutoSizing = true;
            rotulo.fontSizeMin = 12f;
            rotulo.fontSizeMax = 18f;
        }
    }

    // ---------------------------------------------------------------- objetivo

    /// <summary>Aviso "OBJETIVO ..." no meio do topo da tela, que some sozinho depois de alguns segundos.</summary>
    public static void MostrarObjetivo(string texto, float segundos = 8f)
    {
        TemaLaboratorio tema = TemaLabUI.Tema;
        if (string.IsNullOrEmpty(texto))
        {
            return;
        }

        Canvas canvas = VidaUI.CriarCanvas("ObjetivoUI", 40);
        Image placa = TemaLabUI.Imagem(canvas.transform, "Objetivo", tema != null ? tema.painelLinha : null, TemaLabUI.Painel);
        TemaLabUI.Posicionar(placa.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(640f, 96f));
        TextMeshProUGUI rotulo = TemaLabUI.Rotulo(placa.transform, "Rotulo", "Objetivo", 18f, TemaLabUI.Dourado);
        TemaLabUI.Posicionar(rotulo.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(150f, 30f));
        TextMeshProUGUI t = TemaLabUI.Texto(placa.transform, "Texto", texto, 25f, TemaLabUI.CorTexto, TemaLabUI.Fonte.CorpoForte);
        t.enableAutoSizing = true;
        t.fontSizeMin = 16f;
        t.fontSizeMax = 25f;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform r = TemaLabUI.Esticar(t.rectTransform);
        r.offsetMin = new Vector2(176f, 8f);
        r.offsetMax = new Vector2(-24f, -8f);
        Object.Destroy(canvas.gameObject, Mathf.Max(1f, segundos));
    }
}
