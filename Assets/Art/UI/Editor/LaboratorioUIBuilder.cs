using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Aplica o tema de UI "Laboratório Tecno" (sprites em Assets/Art/UI) no menu
/// principal, na tela de Configurações/Pausa (Resources/GameUICanvas) e no
/// seletor de fases (Resources/SeletorFases). Só mexe no visual: os botões
/// continuam com os mesmos OnClick e os scripts de tela continuam achando os
/// filhos pelos mesmos nomes. Pode rodar de novo sem duplicar nada.
///
/// Backups das versões anteriores: SceneBackups/UI_Laboratorio_2026-09-29/.
/// </summary>
public static partial class LaboratorioUIBuilder
{
    const string Pasta = "Assets/Art/UI/";
    const string CenaMenu = "Assets/Scenes/Menu.unity";
    const string PrefabGameUI = "Assets/Resources/GameUICanvas.prefab";
    const string PrefabSeletor = "Assets/Resources/SeletorFases.prefab";
    const string PrefabLinhaControle = "Assets/Resources/ControlRowUI.prefab";

    // Tokens
    static readonly Color Fundo = Hex("#0E1320");
    static readonly Color Painel = Hex("#1B2438");
    static readonly Color PainelElevado = Hex("#243049");
    static readonly Color Linha = Hex("#34425F");
    static readonly Color Azul = Hex("#4A8CF7");
    static readonly Color Ciano = Hex("#5FD4E8");
    static readonly Color Roxo = Hex("#9B7BF0");
    static readonly Color Dourado = Hex("#F5C542");
    static readonly Color Verde = Hex("#52C98A");
    static readonly Color Texto = Hex("#E8EEF9");
    static readonly Color TextoSuave = Hex("#9AA8C4");
    static readonly Color Apagado = Hex("#6B7A99");

    static TMP_FontAsset fTitulo, fTexto, fTextoBold, fCodigo, fCodigoBold;

    // ------------------------------------------------------------------
    // Menus
    // ------------------------------------------------------------------

    [MenuItem("Tools/UI Laboratório/1. Criar fontes TMP")]
    public static void CriarFontes()
    {
        string chars = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
                       "áéíóúâêôãõçàüÁÉÍÓÚÂÊÔÃÕÇÀÜ·•—–…“”‘’°ªº×←→↑↓";

        fTitulo = CriarFonte("Baloo2-ExtraBold", chars);
        fTexto = CriarFonte("Nunito-ExtraBold", chars);
        fTextoBold = CriarFonte("Nunito-Bold", chars);
        fCodigo = CriarFonte("JetBrainsMono-Medium", chars);
        fCodigoBold = CriarFonte("JetBrainsMono-Bold", chars);

        // Setas e símbolos que faltarem numa fonte caem na JetBrains Mono e depois na padrão do TMP.
        foreach (TMP_FontAsset f in new[] { fTitulo, fTexto, fTextoBold, fCodigo, fCodigoBold })
        {
            var fallback = new List<TMP_FontAsset>();
            if (f != fCodigoBold) fallback.Add(fCodigoBold);
            if (TMP_Settings.defaultFontAsset != null && TMP_Settings.defaultFontAsset != f) fallback.Add(TMP_Settings.defaultFontAsset);
            f.fallbackFontAssetTable = fallback;
            EditorUtility.SetDirty(f);
        }

        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/UI Laboratório/2. Aplicar no Menu principal")]
    public static void AplicarMenu() { Preparar(); MontarMenu(); }

    [MenuItem("Tools/UI Laboratório/3. Aplicar em Configurações e Pausa")]
    public static void AplicarConfiguracoes() { Preparar(); MontarLinhaControle(); MontarGameUI(); }

    [MenuItem("Tools/UI Laboratório/4. Aplicar no Seletor de fases")]
    public static void AplicarSeletor() { Preparar(); MontarSeletor(); }

    [MenuItem("Tools/UI Laboratório/Aplicar tudo")]
    public static void AplicarTudo()
    {
        Preparar();
        MontarMenu();
        MontarLinhaControle();
        MontarGameUI();
        MontarSeletor();
    }

    static void Preparar()
    {
        // Os fundos têm 2560 px de largura: sem isso o Unity reduz pra 2048.
        foreach (string bg in new[] { "Backgrounds/menu_completo.png", "Backgrounds/calmo_completo.png" })
        {
            var imp = AssetImporter.GetAtPath(Pasta + bg) as TextureImporter;
            if (imp != null && imp.maxTextureSize < 4096)
            {
                imp.maxTextureSize = 4096;
                imp.SaveAndReimport();
            }
        }

        if (fTitulo == null) CriarFontes();
    }

    // ------------------------------------------------------------------
    // Menu principal
    // ------------------------------------------------------------------

    static void MontarMenu()
    {
        Scene cena = SceneManager.GetSceneByPath(CenaMenu);
        bool jaAberta = cena.IsValid() && cena.isLoaded;
        if (jaAberta && cena.isDirty)
        {
            Debug.LogWarning("[LaboratorioUI] A cena Menu está aberta com alterações não salvas: salve ou descarte antes (pra não gravar mudanças de outra pessoa junto).");
            return;
        }
        if (!jaAberta) cena = EditorSceneManager.OpenScene(CenaMenu, OpenSceneMode.Additive);

        GameObject canvasGO = null, esGO = null, menuControlGO = null;
        foreach (GameObject r in cena.GetRootGameObjects())
        {
            if (r.name == "Canvas") canvasGO = r;
            if (r.GetComponent<EventSystem>() != null) esGO = r;
            if (r.GetComponent<MenuControl>() != null) menuControlGO = r;
        }

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        Escalar(scaler);

        RectTransform painel = (RectTransform)canvasGO.transform.Find("PainelMenu");
        Esticar(painel);

        // Fundo antigo fica desligado (não apagado).
        Transform bgAntigo = painel.Find("BG");
        if (bgAntigo != null) bgAntigo.gameObject.SetActive(false);

        // Fundo novo, cobrindo a tela (sobra lateral pra 21:9).
        RectTransform fundo = Filho(painel, "FundoLab");
        fundo.SetSiblingIndex(0);
        Esticar(fundo);
        Img(fundo.gameObject, null, Fundo);
        RectTransform fundoImg = Filho(fundo, "Imagem");
        Envelope(fundoImg, S("../Backgrounds/menu_completo"), 2560f / 1080f);

        // Logo
        RectTransform logo = Filho(painel, "Logo");
        Pos(logo, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(64, -64), new Vector2(620, 200));
        Img(logo.gameObject, S("moldura_logo"), Color.white, true);
        var titulo = painel.Find("Titulo") ?? logo.Find("Titulo");
        titulo.SetParent(logo, false);
        var tituloTxt = titulo.GetComponent<TMP_Text>();
        Estilo(tituloTxt, fTitulo, 80, Texto, TextAlignmentOptions.MidlineLeft);
        tituloTxt.enableAutoSizing = true;
        tituloTxt.fontSizeMin = 40;
        tituloTxt.fontSizeMax = 80;
        tituloTxt.textWrappingMode = TextWrappingModes.NoWrap;
        var tituloRt = (RectTransform)titulo;
        Esticar(tituloRt);
        tituloRt.offsetMin = new Vector2(48, 24);
        tituloRt.offsetMax = new Vector2(-48, -24);

        // Coluna de botões
        RectTransform coluna = Filho(painel, "Botoes");
        Pos(coluna, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(64, -300), new Vector2(600, 520));
        var vlg = Garantir<VerticalLayoutGroup>(coluna.gameObject);
        vlg.spacing = 24;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        Button jogar = BotaoMenu(painel, coluna, "Jogar", "primario", 128, "icone_continuar", null);
        Button fases = BotaoMenu(painel, coluna, "Fases", "secundario", 96, "icone_fases", "Seleção de fases");
        Button config = BotaoMenu(painel, coluna, "Configuracoes", "secundario", 96, "icone_config", "Configurações");
        Button sair = BotaoMenu(painel, coluna, "Sair", "secundario", 96, "icone_sair", "Sair");
        Encadear(jogar, fases, config, sair);

        // "Continuar" + subtítulo com a fase salva
        RectTransform rotuloJogar = (RectTransform)jogar.transform.Find("Text (TMP)");
        var rotuloJogarTxt = rotuloJogar.GetComponent<TMP_Text>();
        // Posição final (com/sem subtítulo) é aplicada pelo MenuLaboratorioUI em runtime;
        // aqui fica o estado "sem progresso": título sozinho no meio do botão.
        Estilo(rotuloJogarTxt, fTitulo, 40, Color.white, TextAlignmentOptions.MidlineLeft);
        CentroV(rotuloJogar, 2, 64);
        var sub = Txt(jogar.transform, "Subtitulo", "Fase 1 · Física", fTextoBold, 24, Hex("#EAF1FF"), TextAlignmentOptions.MidlineLeft);
        CentroV((RectTransform)sub.transform, -20, 44);
        sub.gameObject.SetActive(false);
        sub.textWrappingMode = TextWrappingModes.NoWrap;
        sub.overflowMode = TextOverflowModes.Ellipsis;

        // Progresso no topo à direita
        RectTransform prog = Filho(painel, "Progresso");
        Pos(prog, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-64, -72), new Vector2(560, 48));
        var progTxt = Txt(prog, "Texto", "0 de 8 fases", fTexto, 30, TextoSuave, TextAlignmentOptions.MidlineRight);
        Pos((RectTransform)progTxt.transform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
        ((RectTransform)progTxt.transform).offsetMax = new Vector2(-244, 0);
        RectTransform trilho = Filho(prog, "Barra");
        Pos(trilho, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(220, 24));
        Img(trilho.gameObject, S("slider_trilho"), Color.white, true);
        RectTransform preench = Filho(trilho, "Preenchimento");
        preench.anchorMin = Vector2.zero;
        preench.anchorMax = new Vector2(0.375f, 1);
        preench.pivot = new Vector2(0, 0.5f);
        preench.offsetMin = new Vector2(5, 5);
        preench.offsetMax = new Vector2(-5, -5);
        // Verde puro (o sprite de preenchimento é azul e ficaria verde-azulado com tint).
        var preenchImg = Img(preench.gameObject, null, Verde);

        // Painel de código à direita
        RectTransform painelCodigo = Filho(painel, "PainelCodigo");
        Pos(painelCodigo, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-64, -16), new Vector2(760, 790));
        Img(painelCodigo.gameObject, S("painel_codigo"), Color.white, true);
        var arquivo = Txt(painelCodigo, "NomeArquivo", "TrechosDoJogo.cs", fCodigoBold, 22, TextoSuave, TextAlignmentOptions.MidlineLeft);
        Pos((RectTransform)arquivo.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(140, -4), new Vector2(-160, 56));

        Transform scrollerT = painel.Find("Scroller") ?? painelCodigo.Find("Codigo");
        scrollerT.name = "Codigo";
        scrollerT.SetParent(painelCodigo, false);
        var codigoRt = (RectTransform)scrollerT;
        Esticar(codigoRt);
        codigoRt.offsetMin = new Vector2(0, 14);
        codigoRt.offsetMax = new Vector2(-10, -66);
        Garantir<RectMask2D>(scrollerT.gameObject);
        var scroller = scrollerT.GetComponent<InfiniteCodeScroller>();
        var so = new SerializedObject(scroller);
        var snippets = so.FindProperty("snippets");
        List<string> blocos = BlocosDeCodigo();
        snippets.arraySize = blocos.Count;
        for (int i = 0; i < blocos.Count; i++) snippets.GetArrayElementAtIndex(i).stringValue = blocos[i];
        so.FindProperty("font").objectReferenceValue = fCodigo;
        so.FindProperty("fontSize").floatValue = 21f;
        so.FindProperty("textColor").colorValue = Texto;
        so.FindProperty("blockWidth").floatValue = 750f;
        so.FindProperty("gapBetweenBlocks").floatValue = 28f;
        so.FindProperty("speed").floatValue = 36f;
        so.FindProperty("quebrarLinhas").boolValue = false;
        so.FindProperty("rolarParaCima").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        RectTransform brilhoTopo = Filho(painelCodigo, "BrilhoTopo");
        Pos(brilhoTopo, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(-5, -66), new Vector2(-14, 90));
        Img(brilhoTopo.gameObject, S("painel_codigo_brilho"), Color.white).raycastTarget = false;
        RectTransform brilhoBase = Filho(painelCodigo, "BrilhoBase");
        Pos(brilhoBase, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1), new Vector2(-5, 14), new Vector2(-14, 90));
        brilhoBase.localScale = new Vector3(1, -1, 1);
        Img(brilhoBase.gameObject, S("painel_codigo_brilho"), Color.white).raycastTarget = false;
        brilhoTopo.SetAsLastSibling();
        brilhoBase.SetAsLastSibling();

        // Rodapé: versão e legenda de teclas
        RectTransform rodape = Filho(painel, "Rodape");
        Pos(rodape, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(-128, 52));
        var versao = Txt(rodape, "Versao", "v1.0 · TCC 2026", fCodigo, 22, TextoSuave, TextAlignmentOptions.MidlineLeft);
        Pos((RectTransform)versao.transform, new Vector2(0, 0), new Vector2(0.4f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
        Legenda(rodape, ("↑↓", "Navegar"), ("Enter", "Selecionar"), ("Esc", "Configurações"));

        // Componentes de apoio (só visual)
        var mlu = Garantir<MenuLaboratorioUI>(painel.gameObject);
        var mso = new SerializedObject(mlu);
        mso.FindProperty("subtituloContinuar").objectReferenceValue = sub;
        mso.FindProperty("rotuloContinuar").objectReferenceValue = rotuloJogarTxt;
        mso.FindProperty("iconeContinuar").objectReferenceValue = jogar.transform.Find("Icone").GetComponent<Image>();
        mso.FindProperty("spriteContinuar").objectReferenceValue = S("icone_continuar");
        mso.FindProperty("spriteJogar").objectReferenceValue = S("icone_jogar");
        mso.FindProperty("textoProgresso").objectReferenceValue = progTxt;
        mso.FindProperty("barraProgresso").objectReferenceValue = preenchImg;
        mso.FindProperty("textoVersao").objectReferenceValue = versao;
        mso.ApplyModifiedPropertiesWithoutUndo();

        Foco(painel.gameObject, jogar, fases, config, sair);

        if (menuControlGO != null)
        {
            var mc = new SerializedObject(menuControlGO.GetComponent<MenuControl>());
            mc.FindProperty("rotuloJogar").objectReferenceValue = rotuloJogarTxt;
            mc.ApplyModifiedPropertiesWithoutUndo();
        }

        if (esGO != null)
        {
            esGO.GetComponent<EventSystem>().firstSelectedGameObject = jogar.gameObject;
        }

        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        if (!jaAberta) EditorSceneManager.CloseScene(cena, true);
        Debug.Log("[LaboratorioUI] Menu principal atualizado.");
    }

    static Button BotaoMenu(Transform painel, RectTransform coluna, string nome, string tipo, float altura, string icone, string rotulo)
    {
        Transform t = painel.Find(nome) ?? coluna.Find(nome);
        t.SetParent(coluna, false);
        var le = Garantir<LayoutElement>(t.gameObject);
        le.minHeight = le.preferredHeight = altura;
        var b = t.GetComponent<Button>();
        EstiloBotao(b, tipo);

        RectTransform ic = Filho(t, "Icone");
        Pos(ic, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(64, 0), new Vector2(44, 44));
        Img(ic.gameObject, S(icone), Color.white).raycastTarget = false;

        var label = t.Find("Text (TMP)");
        var txt = label.GetComponent<TMP_Text>();
        if (rotulo != null)
        {
            txt.text = rotulo;
            Estilo(txt, fTitulo, 36, Texto, TextAlignmentOptions.MidlineLeft);
            var rt = (RectTransform)label;
            Esticar(rt);
            rt.offsetMin = new Vector2(112, 6);
            rt.offsetMax = new Vector2(-24, 0);
        }
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        return b;
    }

    // Trechos reais dos scripts do jogo, agrupados por módulo.
    static List<string[]> Trechos() => new List<string[]>
    {
        new[]
        {
            "// FÍSICA · andar (AplicacaoPlayerController.cs)",
            "float moveInput = InputManager.Instance.MoveAction.ReadValue<float>();",
            "rb.linearVelocity = new Vector2(moveInput * velocidade, rb.linearVelocity.y);",
        },
        new[]
        {
            "// FÍSICA · pular (coyote time + buffer)",
            "if (jumpConsumedSinceGrounded || jumpBufferTimer <= 0f || coyoteTimer <= 0f)",
            "    return;",
            "jumpConsumedSinceGrounded = true;",
            "rb.linearVelocity = new Vector2(rb.linearVelocity.x, forcaPulo);",
        },
        new[]
        {
            "// FÍSICA · cair mais rápido do que sobe",
            "if (rb.linearVelocity.y < 0f)",
            "    rb.gravityScale = gravidadeBase * multiplicadorQueda;",
            "else if (rb.linearVelocity.y > 0f && !JumpHeld())",
            "    rb.gravityScale = gravidadeBase * multiplicadorPuloCurto;",
        },
        new[]
        {
            "// FÍSICA · o chão é um Collider2D",
            "IsGrounded = Time.time >= ignorarChaoAte && rb.IsTouching(filtroChao);",
        },
        new[]
        {
            "// INTERAÇÃO · chegar perto do item (ItemProximityZone.cs)",
            "private void OnTriggerEnter2D(Collider2D other)",
            "{",
            "    if (owner == null || !InteractableItem.IsPlayer(other))",
            "    {",
            "        return;",
            "    }",
            "    owner.HandleProximityEnter(other);",
            "}",
        },
        new[]
        {
            "// INTERAÇÃO · quem encostou é o jogador?",
            "if (other.CompareTag(PlayerTag))",
            "    return true;",
        },
        new[]
        {
            "// INTERAÇÃO · vida → evento → HUD (PlayerHealth.cs)",
            "CurrentHealth = Mathf.Clamp(CurrentHealth - amount, 0, maxHealth);",
            "OnHealthChanged?.Invoke(CurrentHealth, maxHealth);",
            "if (CurrentHealth <= 0)",
            "{",
            "    IsDead = true;",
            "    OnDeath?.Invoke();",
            "}",
        },
        new[]
        {
            "// ANIMAÇÃO · física → Animator (AnimacaoPlayerFisica.cs)",
            "animator.SetFloat(hashVelocidadeX, Mathf.Abs(rb.linearVelocity.x));",
            "animator.SetBool(hashNoChao, IsGrounded);",
        },
        new[]
        {
            "// ANIMAÇÃO · virar o sprite pro lado certo",
            "float vx = rb.linearVelocity.x;",
            "if (vx > LimiarParado) spriteRenderer.flipX = false;",
            "else if (vx < -LimiarParado) spriteRenderer.flipX = true;",
        },
        new[]
        {
            "// IA · máquina de estados do guarda (GuardaIA.cs)",
            "switch (estado)",
            "{",
            "    case Estado.Patrulhar:",
            "        velocidadeDesejadaX = IrEntre();",
            "        break;",
            "    case Estado.Perseguir:",
            "        velocidadeDesejadaX = MoverAte(alvo);",
            "        break;",
            "}",
        },
        new[]
        {
            "// IA · transição Perseguir → Atacar",
            "else if (DistanciaAte(alvo) < alcance)",
            "    MudarEstado(Estado.Atacar);",
        },
        new[]
        {
            "// IA · visão com Raycast (IAVisao.cs)",
            "return DistanciaAte(t) < raioVisao && TemLinhaDeVisao(t);",
            "RaycastHit2D[] hits = Physics2D.RaycastAll(origem, delta / distancia, distancia, obstaculos);",
        },
        new[]
        {
            "// CÂMERA · seguir o jogador (CameraFollow.cs)",
            "Vector3 smoothed = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime, maxSpeed);",
        },
    };

    static readonly HashSet<string> PalavrasChave = new HashSet<string>
    {
        "if", "else", "return", "void", "private", "public", "float", "int", "bool", "new", "true", "false",
        "switch", "case", "break", "null", "ref", "static", "class", "using", "var",
    };

    static readonly Regex Token = new Regex("(\"[^\"]*\")|(\\b\\d+(?:\\.\\d+)?f?\\b)|([A-Za-z_]\\w*)(?=\\s*\\()|([A-Za-z_]\\w*)");

    static List<string> BlocosDeCodigo()
    {
        var blocos = new List<string>();
        int n = 1;
        foreach (string[] trecho in Trechos())
        {
            var sb = new StringBuilder();
            for (int i = 0; i < trecho.Length; i++)
            {
                // Linha longa quebra em várias; só a primeira leva o número, como num editor com quebra automática.
                List<string> partes = QuebrarLinha(trecho[i], MaxColunasCodigo);
                for (int p = 0; p < partes.Count; p++)
                {
                    if (sb.Length > 0) sb.Append('\n');
                    if (p == 0) sb.Append("<color=#4A5570><pos=4>").Append(n.ToString().PadLeft(3)).Append("</color>");
                    sb.Append("<pos=80>").Append(Colorir(partes[p]));
                }
                n++;
            }
            n++; // a folga entre blocos conta como uma linha em branco
            blocos.Add(sb.ToString());
        }
        return blocos;
    }

    // Quantos caracteres cabem no painel de código (fonte 21, 670 px depois do número da linha).
    const int MaxColunasCodigo = 51;

    // Quebra uma linha longa onde um programador quebraria: depois de ", " ou antes de "||"/"&&";
    // senão depois de " = "; senão no último espaço; senão antes de um ".". A continuação ganha
    // 4 espaços de recuo (comentário continua com "// ").
    static List<string> QuebrarLinha(string linha, int max)
    {
        var partes = new List<string>();
        int recuo = linha.Length - linha.TrimStart().Length;
        bool comentario = linha.TrimStart().StartsWith("//");
        string prefixo = new string(' ', recuo) + (comentario ? "// " : "    ");
        string resto = linha;
        bool primeira = true;
        while (resto.Length > max)
        {
            int minimo = primeira ? recuo + 4 : prefixo.Length + 4;
            int corte = PontoDeQuebra(resto, max, minimo, comentario);
            partes.Add(resto.Substring(0, corte).TrimEnd());
            resto = prefixo + resto.Substring(corte).TrimStart();
            primeira = false;
        }
        partes.Add(resto);
        return partes;
    }

    // Índice onde começa a parte de baixo. Dentro de cada prioridade, o ponto mais à direita que cabe.
    static int PontoDeQuebra(string s, int max, int minimo, bool comentario)
    {
        int[] melhor = { -1, -1, -1, -1, -1 };
        for (int i = minimo; i < s.Length - 1 && i <= max; i++)
        {
            if (s[i] == '.' && !comentario && i > minimo)
            {
                melhor[4] = i;
                continue;
            }
            if (s[i] != ' ') continue;
            int inicio = i + 1;
            int prio = 3;
            if (!comentario)
            {
                string seguinte = inicio + 1 < s.Length ? s.Substring(inicio, 2) : "";
                if (s[i - 1] == ',' || seguinte == "||" || seguinte == "&&") prio = 1;
                else if (i > 1 && s[i - 1] == '=' && s[i - 2] == ' ') prio = 2;
            }
            melhor[prio] = inicio;
        }
        for (int p = 1; p < melhor.Length; p++)
            if (melhor[p] > 0) return melhor[p];
        return max;
    }

    // Regrava só os trechos do painel de código do Menu, sem refazer o resto da tela.
    [MenuItem("Tools/UI Laboratório/Quebrar linhas do código do Menu")]
    public static void AtualizarCodigoDoMenu()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[LaboratorioUI] Saia do Play antes de atualizar o código do Menu.");
            return;
        }
        Scene cena = SceneManager.GetSceneByPath(CenaMenu);
        bool jaAberta = cena.IsValid() && cena.isLoaded;
        if (jaAberta && cena.isDirty)
        {
            Debug.LogWarning("[LaboratorioUI] A cena Menu está aberta com alterações não salvas: salve ou descarte antes.");
            return;
        }
        if (!jaAberta) cena = EditorSceneManager.OpenScene(CenaMenu, OpenSceneMode.Additive);

        InfiniteCodeScroller scroller = null;
        foreach (GameObject raiz in cena.GetRootGameObjects())
        {
            scroller = raiz.GetComponentInChildren<InfiniteCodeScroller>(true);
            if (scroller != null) break;
        }
        if (scroller == null)
        {
            Debug.LogError("[LaboratorioUI] InfiniteCodeScroller não encontrado na cena Menu.");
            if (!jaAberta) EditorSceneManager.CloseScene(cena, true);
            return;
        }

        var so = new SerializedObject(scroller);
        var snippets = so.FindProperty("snippets");
        List<string> blocos = BlocosDeCodigo();
        snippets.arraySize = blocos.Count;
        for (int i = 0; i < blocos.Count; i++) snippets.GetArrayElementAtIndex(i).stringValue = blocos[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        if (!jaAberta) EditorSceneManager.CloseScene(cena, true);
        Debug.Log("[LaboratorioUI] Código do Menu atualizado com as linhas longas quebradas.");
    }

    static string Colorir(string linha)
    {
        string semEspaco = linha.TrimStart();
        if (semEspaco.StartsWith("//"))
        {
            return $"<color=#6B7A99>{linha}</color>";
        }

        return Token.Replace(linha, m =>
        {
            if (m.Groups[1].Success || m.Groups[2].Success) return $"<color=#F5C542>{m.Value}</color>";
            if (PalavrasChave.Contains(m.Value)) return $"<color=#9B7BF0>{m.Value}</color>";
            if (m.Groups[3].Success) return $"<color=#5FD4E8>{m.Value}</color>";
            return m.Value;
        });
    }

    // ------------------------------------------------------------------
    // Configurações + Pausa (Resources/GameUICanvas)
    // ------------------------------------------------------------------

    static void MontarGameUI()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabGameUI);
        try
        {
            Escalar(root.GetComponent<CanvasScaler>());
            MontarPausa((RectTransform)root.transform.Find("PausePanel"));
            MontarConfiguracoes((RectTransform)root.transform.Find("SettingsPanel"));
            MontarConfirmacaoReinicio(root.transform);
            MontarAnelReinicio(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabGameUI);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log("[LaboratorioUI] Configurações e Pausa atualizados.");
    }

    static void MontarPausa(RectTransform pausa)
    {
        pausa.anchorMin = pausa.anchorMax = pausa.pivot = new Vector2(0.5f, 0.5f);
        pausa.anchoredPosition = new Vector2(0, -20);
        pausa.sizeDelta = new Vector2(640, 590); // 4 botões (com "Reiniciar fase")
        // A altura segue os botões visíveis: sem "Reiniciar fase" (Questionário) a janela encolhe.
        var ajuste = Garantir<ContentSizeFitter>(pausa.gameObject);
        ajuste.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        ajuste.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var fundoPausa = pausa.GetComponent<Image>();
        fundoPausa.enabled = false; // o fundo agora é o filho "Janela" (fica atrás do véu na ordem certa)

        var vlg = pausa.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(56, 56, 92, 48);
        vlg.spacing = 22;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        RectTransform veu = Filho(pausa, "Veu");
        veu.SetSiblingIndex(0);
        Garantir<LayoutElement>(veu.gameObject).ignoreLayout = true;
        Pos(veu, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(8000, 8000));
        Img(veu.gameObject, S("veu_popup"), Color.white);

        RectTransform janela = Filho(pausa, "Janela");
        janela.SetSiblingIndex(1);
        Garantir<LayoutElement>(janela.gameObject).ignoreLayout = true;
        Esticar(janela);
        Img(janela.gameObject, S("painel_janela"), Color.white, true);

        RectTransform faixa = Filho(pausa, "FaixaTitulo");
        faixa.SetSiblingIndex(2);
        Garantir<LayoutElement>(faixa.gameObject).ignoreLayout = true;
        Pos(faixa, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(380, 88));
        Img(faixa.gameObject, S("painel_titulo"), Color.white, true);

        Transform titulo = pausa.Find("Titulo");
        Garantir<LayoutElement>(titulo.gameObject).ignoreLayout = true;
        Pos((RectTransform)titulo, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(380, 88));
        var tt = titulo.GetComponent<TMP_Text>();
        tt.text = "Pausado";
        Estilo(tt, fTitulo, 48, Color.white, TextAlignmentOptions.Center);
        titulo.SetSiblingIndex(3);

        Button continuar = BotaoPainel(pausa, "ContinuarButton", "primario", "Continuar", "icone_jogar", 96);
        Button reiniciar = MontarBotaoReiniciar(pausa);
        Button config = BotaoPainel(pausa, "ConfiguracoesButton", "secundario", "Configurações", "icone_config", 96);
        Button menu = BotaoPainel(pausa, "MenuButton", "secundario", "Menu principal", "icone_sair", 96);
        Encadear(continuar, reiniciar, config, menu);
        Foco(pausa.gameObject, continuar, reiniciar, config, menu);
        MontarDicasPausa(pausa);
    }

    static Button BotaoPainel(Transform pai, string nome, string tipo, string rotulo, string icone, float altura)
    {
        Transform t = pai.Find(nome);
        var le = Garantir<LayoutElement>(t.gameObject);
        le.minHeight = le.preferredHeight = altura;
        var b = t.GetComponent<Button>();
        EstiloBotao(b, tipo);

        var label = t.Find("Label");
        var txt = label.GetComponent<TMP_Text>();
        txt.text = rotulo;
        Estilo(txt, fTitulo, 34, tipo == "primario" ? Color.white : Texto, TextAlignmentOptions.Center);
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        var rt = (RectTransform)label;
        Esticar(rt);
        rt.offsetMin = new Vector2(icone != null ? 56 : 16, 4);
        rt.offsetMax = new Vector2(-16, 0);

        if (icone != null)
        {
            RectTransform ic = Filho(t, "Icone");
            Pos(ic, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(52, 0), new Vector2(40, 40));
            Img(ic.gameObject, S(icone), Color.white).raycastTarget = false;
        }
        return b;
    }

    static void MontarConfiguracoes(RectTransform painel)
    {
        // Véu por cima da fase (pausa) e, no Menu, o fundo "calmo" cobrindo tudo.
        var veu = painel.GetComponent<Image>();
        veu.sprite = S("veu_popup");
        veu.color = Color.white;
        veu.type = Image.Type.Simple;

        RectTransform fundoMenu = Filho(painel, "FundoMenu");
        fundoMenu.SetSiblingIndex(0);
        Envelope(fundoMenu, S("../Backgrounds/calmo_completo"), 2560f / 1080f);
        Garantir<FundoSomenteNoMenuUI>(fundoMenu.gameObject);

        // Janela 1280 x 800 centralizada (y do centro = -30).
        const float cy = -30f, largura = 1280f, altura = 800f;
        float topo = cy + altura / 2f, base_ = cy - altura / 2f;

        RectTransform janela = Filho(painel, "Janela");
        janela.SetSiblingIndex(1);
        Centro(janela, new Vector2(0, cy), new Vector2(largura, altura));
        Img(janela.gameObject, S("painel_janela"), Color.white, true);

        RectTransform faixa = Filho(painel, "FaixaTitulo");
        faixa.SetSiblingIndex(2);
        Centro(faixa, new Vector2(0, topo + 4), new Vector2(480, 88));
        Img(faixa.gameObject, S("painel_titulo"), Color.white, true);

        var titulo = (RectTransform)painel.Find("Titulo");
        titulo.SetSiblingIndex(3);
        Centro(titulo, new Vector2(0, topo + 8), new Vector2(480, 88));
        var tt = titulo.GetComponent<TMP_Text>();
        tt.text = "Configurações";
        Estilo(tt, fTitulo, 48, Color.white, TextAlignmentOptions.Center);

        // Abas
        var abas = (RectTransform)painel.Find("TabBar");
        Centro(abas, new Vector2(0, topo - 94), new Vector2(largura - 80, 76));
        var hlg = abas.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = true;
        Button abaAudio = Aba(abas, "AudioTabButton", "Áudio", "icone_som");
        Button abaGraf = Aba(abas, "GraphicsTabButton", "Gráficos", "icone_tela");
        Button abaCtrl = Aba(abas, "ControlsTabButton", "Controles", "icone_teclado");

        // Conteúdo: entre as abas e a barra de botões
        float conteudoTopo = topo - 94 - 38 - 24;       // 244
        float barraCentro = base_ + 40 + 48;            // -342
        float conteudoBase = barraCentro + 48 + 24;     // -270: 24 px acima da barra
        var area = (RectTransform)painel.Find("ContentArea");
        Centro(area, new Vector2(0, (conteudoTopo + conteudoBase) / 2f), new Vector2(largura - 80, conteudoTopo - conteudoBase));

        MontarAbaAudio((RectTransform)area.Find("AudioContent"));
        MontarOpcoesQualidadeVida(painel); // Gráficos (resolução, modo de tela...), Jogo e linha extra do Áudio
        MontarAbaControles((RectTransform)area.Find("ControlsContent"), barraCentro - conteudoBase);

        // Voltar (secundário, à direita). Não existe "Aplicar" na lógica: tudo vale na hora.
        var voltar = (RectTransform)painel.Find("VoltarButton");
        Centro(voltar, new Vector2(largura / 2f - 40 - 150, barraCentro), new Vector2(300, 96));
        var vb = voltar.GetComponent<Button>();
        EstiloBotao(vb, "secundario");
        var vl = voltar.Find("Label").GetComponent<TMP_Text>();
        vl.text = "Voltar";
        Estilo(vl, fTitulo, 34, Texto, TextAlignmentOptions.Center);
        var vlr = (RectTransform)vl.transform;
        Esticar(vlr);
        vlr.offsetMin = new Vector2(56, 4);
        vlr.offsetMax = new Vector2(-16, 0);
        RectTransform vic = Filho(voltar, "Icone");
        Pos(vic, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(64, 0), new Vector2(40, 40));
        Img(vic.gameObject, S("icone_voltar"), Color.white).raycastTarget = false;

        // Legenda de teclas no rodapé da tela
        RectTransform rodape = Filho(painel, "Rodape");
        Pos(rodape, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(-128, 52));
        Legenda(rodape, ("↑↓", "Navegar"), ("←→", "Ajustar"), ("Enter", "Selecionar"), ("Esc", "Voltar"));

        foreach (Button b in new[] { abaAudio, abaGraf, abaCtrl, abas.Find("GameTabButton").GetComponent<Button>() })
        {
            b.GetComponent<AbaVisualUI>().Definir(b == abaAudio);
        }

        Foco(painel.gameObject,
            area.Find("AudioContent/VolumeSlider").GetComponent<Selectable>(),
            area.Find("GraphicsContent/LinhaResolucao").GetComponent<Selectable>(),
            abaCtrl, abaAudio);
    }

    static Button Aba(RectTransform barra, string nome, string rotulo, string icone)
    {
        Transform t = barra.Find(nome);
        var b = t.GetComponent<Button>();
        var img = t.GetComponent<Image>();
        img.sprite = S("aba_inativa");
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        b.targetGraphic = img;
        b.transition = Selectable.Transition.SpriteSwap;
        b.spriteState = new SpriteState { highlightedSprite = S("aba_foco"), pressedSprite = S("aba_ativa"), selectedSprite = S("aba_foco") };

        RectTransform conteudo = Filho(t, "Conteudo");
        Esticar(conteudo);
        var h = Garantir<HorizontalLayoutGroup>(conteudo.gameObject);
        h.childAlignment = TextAnchor.MiddleCenter;
        h.spacing = 14;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        h.padding = new RectOffset(0, 0, 6, 0);

        RectTransform ic = Filho(conteudo, "Icone");
        ic.SetSiblingIndex(0);
        var icImg = Img(ic.gameObject, S(icone), TextoSuave);
        icImg.raycastTarget = false;
        var icLe = Garantir<LayoutElement>(ic.gameObject);
        icLe.preferredWidth = icLe.minWidth = 36;
        icLe.preferredHeight = icLe.minHeight = 36;

        Transform label = t.Find("Label") ?? conteudo.Find("Label");
        label.SetParent(conteudo, false);
        var txt = label.GetComponent<TMP_Text>();
        txt.text = rotulo;
        Estilo(txt, fTitulo, 32, TextoSuave, TextAlignmentOptions.Midline);
        txt.textWrappingMode = TextWrappingModes.NoWrap;

        var aba = Garantir<AbaVisualUI>(t.gameObject);
        var so = new SerializedObject(aba);
        so.FindProperty("fundo").objectReferenceValue = img;
        so.FindProperty("spriteAtiva").objectReferenceValue = S("aba_ativa");
        so.FindProperty("spriteInativa").objectReferenceValue = S("aba_inativa");
        so.FindProperty("rotulo").objectReferenceValue = txt;
        so.FindProperty("icone").objectReferenceValue = icImg;
        so.FindProperty("corAtiva").colorValue = Texto;
        so.FindProperty("corInativa").colorValue = TextoSuave;
        so.ApplyModifiedPropertiesWithoutUndo();
        return b;
    }

    static RectTransform LinhaFundo(RectTransform pai, string nome, float y, float altura, params Transform[] observar)
    {
        RectTransform linha = Filho(pai, nome);
        Pos(linha, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(0, altura));
        Img(linha.gameObject, S("painel_linha"), Color.white, true);
        var lf = Garantir<LinhaFocoUI>(linha.gameObject);
        var so = new SerializedObject(lf);
        so.FindProperty("spriteNormal").objectReferenceValue = S("painel_linha");
        so.FindProperty("spriteFoco").objectReferenceValue = S("painel_linha_foco");
        var obs = so.FindProperty("observar");
        obs.arraySize = observar.Length;
        for (int i = 0; i < observar.Length; i++) obs.GetArrayElementAtIndex(i).objectReferenceValue = observar[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return linha;
    }

    static void MontarAbaAudio(RectTransform aba)
    {
        var layout = aba.GetComponent<VerticalLayoutGroup>();
        if (layout != null) layout.enabled = false; // os três itens precisam ficar soltos na aba (AudioTabUI acha pelo nome)

        var slider = aba.Find("VolumeSlider");
        RectTransform linha = LinhaFundo(aba, "LinhaVolume", 0, 96, slider);
        linha.SetSiblingIndex(0);

        var rotulo = (RectTransform)aba.Find("VolumeLabel");
        Pos(rotulo, new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(0, 0.5f), new Vector2(32, -48), new Vector2(-32, 60));
        var rt = rotulo.GetComponent<TMP_Text>();
        rt.text = "Volume geral";
        Estilo(rt, fTexto, 30, Texto, TextAlignmentOptions.MidlineLeft);

        var sRt = (RectTransform)slider;
        Pos(sRt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-150, -48), new Vector2(420, 24));
        EstiloSlider(slider.GetComponent<Slider>());

        var valor = (RectTransform)aba.Find("VolumeValueLabel");
        Pos(valor, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-32, -48), new Vector2(100, 48));
        Estilo(valor.GetComponent<TMP_Text>(), fCodigoBold, 28, Texto, TextAlignmentOptions.MidlineRight);

        // O Volume geral continua gravado pelo AudioTabUI; a linha só acompanha o OnVolumeChanged.
        LigarVolume(linha.gameObject, VolumeLinhaUI.Canal.Geral, slider.GetComponent<Slider>(), valor.GetComponent<TMP_Text>(), true);

        // Música e Efeitos: linhas completas (slider e valor dentro da linha).
        Slider musica = LinhaVolume(aba, "LinhaMusica", "Música", "MusicaSlider", 112, VolumeLinhaUI.Canal.Musica);
        Slider efeitos = LinhaVolume(aba, "LinhaEfeitos", "Efeitos sonoros", "EfeitosSlider", 224, VolumeLinhaUI.Canal.Efeitos);

        // ↑↓ entre as linhas; ←→ fica com o slider (ajusta o valor).
        Slider geral = slider.GetComponent<Slider>();
        Selectable abaAudio = aba.parent.parent.Find("TabBar/AudioTabButton").GetComponent<Selectable>();
        Selectable voltar = aba.parent.parent.Find("VoltarButton").GetComponent<Selectable>();
        geral.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = abaAudio, selectOnDown = musica };
        musica.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = geral, selectOnDown = efeitos };
        efeitos.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = musica, selectOnDown = voltar };
    }

    static Slider LinhaVolume(RectTransform aba, string nome, string rotulo, string nomeSlider, float y, VolumeLinhaUI.Canal canal)
    {
        RectTransform linha = LinhaFundo(aba, nome, y, 96);
        var rot = Txt(linha, "Rotulo", rotulo, fTexto, 30, Texto, TextAlignmentOptions.MidlineLeft);
        Pos((RectTransform)rot.transform, new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(0, 0.5f), new Vector2(32, 0), new Vector2(-32, 0));

        Transform st = linha.Find(nomeSlider);
        Slider s = st != null ? st.GetComponent<Slider>() : UIBuildUtils.CreateSlider(linha, nomeSlider);
        s.minValue = 0f;
        s.maxValue = 1f;
        s.value = 1f;
        Pos((RectTransform)s.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-150, 0), new Vector2(420, 24));
        EstiloSlider(s);

        var val = Txt(linha, "Valor", "100%", fCodigoBold, 28, Texto, TextAlignmentOptions.MidlineRight);
        Pos((RectTransform)val.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-32, 0), new Vector2(100, 48));

        LigarVolume(linha.gameObject, canal, s, val, false);
        return s;
    }

    static void LigarVolume(GameObject go, VolumeLinhaUI.Canal canal, Slider s, TMP_Text valor, bool soAcompanhar)
    {
        var v = Garantir<VolumeLinhaUI>(go);
        var so = new SerializedObject(v);
        so.FindProperty("canal").enumValueIndex = (int)canal;
        so.FindProperty("slider").objectReferenceValue = s;
        so.FindProperty("valor").objectReferenceValue = valor;
        so.FindProperty("soAcompanhar").boolValue = soAcompanhar;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void EstiloSlider(Slider s)
    {
        Transform bg = s.transform.Find("Background");
        var bgImg = bg.GetComponent<Image>();
        bgImg.sprite = S("slider_trilho");
        bgImg.type = Image.Type.Sliced;
        bgImg.color = Color.white;

        var fillArea = (RectTransform)s.transform.Find("Fill Area");
        Esticar(fillArea);
        var fill = s.transform.Find("Fill Area/Fill");
        var fillImg = fill.GetComponent<Image>();
        fillImg.sprite = S("slider_preenchimento");
        fillImg.type = Image.Type.Sliced;
        fillImg.color = Color.white;
        ((RectTransform)fill).sizeDelta = Vector2.zero;

        var handleArea = (RectTransform)s.transform.Find("Handle Slide Area");
        Esticar(handleArea);
        handleArea.offsetMin = new Vector2(12, 0);
        handleArea.offsetMax = new Vector2(-12, 0);
        var handle = (RectTransform)s.transform.Find("Handle Slide Area/Handle");
        handle.sizeDelta = new Vector2(48, 24);
        var hImg = handle.GetComponent<Image>();
        hImg.sprite = S("slider_alca_normal");
        hImg.type = Image.Type.Simple;
        hImg.color = Color.white;
        hImg.preserveAspect = true;

        s.targetGraphic = hImg;
        s.transition = Selectable.Transition.SpriteSwap;
        s.spriteState = new SpriteState { highlightedSprite = S("slider_alca_normal"), pressedSprite = S("slider_alca_foco"), selectedSprite = S("slider_alca_foco") };
    }

    // Versão antiga (só 1280x720 / 1920x1080 e Tela cheia), substituída por
    // MontarOpcoesQualidadeVida em LaboratorioUIBuilder.QualidadeVida.cs.
    static void MontarAbaGraficosAntiga(RectTransform aba)
    {
        var layout = aba.GetComponent<VerticalLayoutGroup>();
        if (layout != null) layout.enabled = false; // GraphicsTabUI acha os botões pelo nome, direto na aba

        Transform b720 = aba.Find("Resolucao720Button");
        Transform b1080 = aba.Find("Resolucao1080Button");
        Transform bTela = aba.Find("FullscreenButton");

        RectTransform linha = LinhaFundo(aba, "LinhaResolucao", 0, 96, b720, b1080);
        linha.SetSiblingIndex(0);
        var rot = Txt(linha, "Rotulo", "Resolução", fTexto, 30, Texto, TextAlignmentOptions.MidlineLeft);
        Pos((RectTransform)rot.transform, new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(0, 0.5f), new Vector2(32, 0), new Vector2(-32, 0));

        var marcas = new GameObject[2];
        int i = 0;
        foreach (var (t, x, texto) in new[] { (b720, -272f, "1280 × 720"), (b1080, -32f, "1920 × 1080") })
        {
            var r = (RectTransform)t;
            Pos(r, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(x, -48), new Vector2(224, 68));
            EstiloBotao(t.GetComponent<Button>(), "secundario", 1.5f);
            var l = t.Find("Label").GetComponent<TMP_Text>();
            l.text = texto;
            Estilo(l, fCodigoBold, 24, Texto, TextAlignmentOptions.Center);
            var lr = (RectTransform)l.transform;
            Esticar(lr);
            lr.offsetMin = new Vector2(30, 2);
            lr.offsetMax = new Vector2(-8, 0);
            RectTransform marca = Filho(t, "EmUso");
            Pos(marca, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(26, 2), new Vector2(24, 24));
            Img(marca.gameObject, S("icone_check"), Verde).raycastTarget = false;
            marcas[i++] = marca.gameObject;
        }

        var tela = (RectTransform)bTela;
        Pos(tela, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(0, 96));
        var tb = bTela.GetComponent<Button>();
        var timg = bTela.GetComponent<Image>();
        timg.sprite = S("painel_linha");
        timg.type = Image.Type.Sliced;
        timg.color = Color.white;
        timg.pixelsPerUnitMultiplier = 1f;
        tb.targetGraphic = timg;
        tb.transition = Selectable.Transition.SpriteSwap;
        tb.spriteState = new SpriteState { highlightedSprite = S("painel_linha_foco"), pressedSprite = S("painel_linha_foco"), selectedSprite = S("painel_linha_foco") };
        var tl = bTela.Find("Label").GetComponent<TMP_Text>();
        Estilo(tl, fTexto, 30, Texto, TextAlignmentOptions.MidlineLeft);
        var tlr = (RectTransform)tl.transform;
        Esticar(tlr);
        tlr.offsetMin = new Vector2(32, 0);
        tlr.offsetMax = new Vector2(-180, 0);
        RectTransform chave = Filho(bTela, "Interruptor");
        Pos(chave, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-32, 0), new Vector2(98, 63));
        var chaveImg = Img(chave.gameObject, S("interruptor_desligado"), Color.white);
        chaveImg.raycastTarget = false;
        chaveImg.preserveAspect = true;

        var gv = Garantir<GraficosVisualUI>(aba.gameObject);
        var so = new SerializedObject(gv);
        so.FindProperty("interruptor").objectReferenceValue = chaveImg;
        so.FindProperty("spriteLigado").objectReferenceValue = S("interruptor_ligado");
        so.FindProperty("spriteDesligado").objectReferenceValue = S("interruptor_desligado");
        var m = so.FindProperty("marcasResolucao");
        m.arraySize = 2;
        m.GetArrayElementAtIndex(0).objectReferenceValue = marcas[0];
        m.GetArrayElementAtIndex(1).objectReferenceValue = marcas[1];
        so.ApplyModifiedPropertiesWithoutUndo();

        Encadear(b720.GetComponent<Button>(), tb);
        var n720 = b720.GetComponent<Button>().navigation;
        n720.selectOnRight = b1080.GetComponent<Button>();
        b720.GetComponent<Button>().navigation = n720;
        var n1080 = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = b720.GetComponent<Button>(), selectOnDown = tb };
        b1080.GetComponent<Button>().navigation = n1080;
        var ntb = tb.navigation;
        ntb.selectOnRight = null;
        tb.navigation = ntb;
    }

    static void MontarAbaControles(RectTransform aba, float yBarraAbaixoDoConteudo)
    {
        var scrollArea = (RectTransform)aba.Find("ScrollArea");
        Esticar(scrollArea);
        var sv = scrollArea.Find("ScrollView");
        var svImg = sv.GetComponent<Image>();
        svImg.color = Color.white;
        svImg.sprite = null;
        var mask = sv.GetComponent<Mask>();
        if (mask != null) mask.showMaskGraphic = false;
        var content = sv.Find("Content");
        var v = content.GetComponent<VerticalLayoutGroup>();
        v.spacing = 12;
        v.padding = new RectOffset(0, 0, 0, 0);

        // "Restaurar padrão" continua dentro da aba Controles (ControlsTabUI acha pelo nome),
        // mas desce pra barra de botões, à esquerda.
        var restaurar = (RectTransform)aba.Find("RestaurarPadraoButton");
        Pos(restaurar, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0.5f), new Vector2(0, yBarraAbaixoDoConteudo), new Vector2(360, 96));
        EstiloBotao(restaurar.GetComponent<Button>(), "secundario");
        var rl = restaurar.Find("Label").GetComponent<TMP_Text>();
        rl.text = "Restaurar padrão";
        Estilo(rl, fTitulo, 32, Texto, TextAlignmentOptions.Center);
        var rlr = (RectTransform)rl.transform;
        Esticar(rlr);
        rlr.offsetMin = new Vector2(16, 4);
        rlr.offsetMax = new Vector2(-16, 0);
    }

    static void MontarLinhaControle()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabLinhaControle);
        try
        {
            var img = Garantir<Image>(root);
            img.sprite = S("painel_linha");
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            var lf = Garantir<LinhaFocoUI>(root);
            var so = new SerializedObject(lf);
            so.FindProperty("spriteNormal").objectReferenceValue = S("painel_linha");
            so.FindProperty("spriteFoco").objectReferenceValue = S("painel_linha_foco");
            so.ApplyModifiedPropertiesWithoutUndo();

            var le = root.GetComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = 84;
            var rootRt = (RectTransform)root.transform;
            rootRt.sizeDelta = new Vector2(rootRt.sizeDelta.x, 84); // a lista (Content) usa a altura do RectTransform

            // O Button da raiz não faz nada: fora da navegação, só o botão da tecla recebe foco.
            var raiz = root.GetComponent<Button>();
            raiz.transition = Selectable.Transition.None;
            raiz.navigation = new Navigation { mode = Navigation.Mode.None };

            var h = root.GetComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(32, 20, 10, 10);
            h.spacing = 16;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;

            var acao = root.transform.Find("ActionLabel");
            Estilo(acao.GetComponent<TMP_Text>(), fTexto, 30, Texto, TextAlignmentOptions.MidlineLeft);
            var ale = Garantir<LayoutElement>(acao.gameObject);
            ale.flexibleWidth = 1;
            ale.preferredHeight = 60;

            var tecla = root.transform.Find("RebindButton");
            var tle = Garantir<LayoutElement>(tecla.gameObject);
            tle.preferredWidth = tle.minWidth = 240;
            tle.preferredHeight = tle.minHeight = 62;
            tle.flexibleWidth = 0;
            EstiloBotao(tecla.GetComponent<Button>(), "secundario", 1.6f);
            var tn = tecla.GetComponent<Button>().navigation;
            tn.mode = Navigation.Mode.Automatic;
            tecla.GetComponent<Button>().navigation = tn;
            var bl = tecla.Find("BindingLabel").GetComponent<TMP_Text>();
            Estilo(bl, fCodigoBold, 24, Texto, TextAlignmentOptions.Center);
            bl.enableAutoSizing = true;
            bl.fontSizeMin = 16;
            bl.fontSizeMax = 24;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabLinhaControle);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ------------------------------------------------------------------
    // Seletor de fases (Resources/SeletorFases)
    // ------------------------------------------------------------------

    static readonly string[] Modulos = { "Física", "Interação", "Animação", "IA" };
    static readonly Color[] CoresModulo = { Hex("#4A8CF7"), Hex("#52C98A"), Hex("#9B7BF0"), Hex("#F5C542") };

    static void MontarSeletor()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabSeletor);
        try
        {
            Escalar(root.GetComponent<CanvasScaler>());
            var seletor = root.GetComponent<SeletorFasesUI>();
            var fundo = (RectTransform)root.transform.Find("Fundo");
            var fundoImg = fundo.GetComponent<Image>();
            fundoImg.sprite = null;
            fundoImg.color = Fundo;

            RectTransform imagem = Filho(fundo, "Imagem");
            imagem.SetSiblingIndex(0);
            Envelope(imagem, S("../Backgrounds/calmo_completo"), 2560f / 1080f);
            imagem.GetComponent<Image>().raycastTarget = false;

            // Título + voltar redondo
            var voltar = (RectTransform)fundo.Find("Voltar");
            Pos(voltar, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(64, -68), new Vector2(88, 88));
            EstiloBotao(voltar.GetComponent<Button>(), "icone");
            var vlab = voltar.Find("Label");
            if (vlab != null) vlab.gameObject.SetActive(false);
            RectTransform vic = Filho(voltar, "Icone");
            Pos(vic, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(44, 44));
            Img(vic.gameObject, S("icone_voltar"), Color.white).raycastTarget = false;

            var titulo = (RectTransform)fundo.Find("Titulo");
            Pos(titulo, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(180, -60), new Vector2(900, 100));
            var tt = titulo.GetComponent<TMP_Text>();
            tt.text = "Seleção de fases";
            Estilo(tt, fTitulo, 64, Texto, TextAlignmentOptions.MidlineLeft);

            var subt = (RectTransform)fundo.Find("Subtitulo");
            Pos(subt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-64, -84), new Vector2(1000, 52));
            Estilo(subt.GetComponent<TMP_Text>(), fTexto, 30, TextoSuave, TextAlignmentOptions.MidlineRight);

            var grade = fundo.Find("Grade");
            if (grade != null) grade.gameObject.SetActive(false);

            // Mapa das fases: 4 colunas de módulo, lição em cima e aplicação embaixo.
            const float larguraDetalhes = 560f;
            RectTransform mapa = Filho(fundo, "Mapa");
            mapa.anchorMin = Vector2.zero;
            mapa.anchorMax = Vector2.one;
            mapa.pivot = new Vector2(0.5f, 0.5f);
            mapa.offsetMin = new Vector2(64, 190);
            mapa.offsetMax = new Vector2(-(64 + larguraDetalhes + 48), -190);

            var linhaPre = LinhaQuestionario(mapa, "LinhaPreTeste", true);
            var linhaPos = LinhaQuestionario(mapa, "LinhaPosTeste", false);

            var vagas = new RectTransform[8];
            var trechos = new List<(int depois, Image[] pontos)>();
            for (int c = 0; c < 4; c++)
            {
                RectTransform col = Filho(mapa, "Coluna_" + Modulos[c]);
                col.anchorMin = new Vector2(c * 0.25f, 0);
                col.anchorMax = new Vector2((c + 1) * 0.25f, 1);
                col.pivot = new Vector2(0.5f, 1);
                col.offsetMin = new Vector2(0, 70);
                col.offsetMax = new Vector2(0, -70);

                var cab = Txt(col, "Cabecalho", $"<color=#{ColorUtility.ToHtmlStringRGB(CoresModulo[c])}>■</color> {Modulos[c]}", fTitulo, 34, CoresModulo[c], TextAlignmentOptions.Center);
                Pos((RectTransform)cab.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(0, 52));
                cab.textWrappingMode = TextWrappingModes.NoWrap;

                for (int l = 0; l < 2; l++)
                {
                    RectTransform vaga = Filho(col, l == 0 ? "Vaga_Licao" : "Vaga_Aplicacao");
                    Pos(vaga, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, l == 0 ? -146 : -418), new Vector2(150, 167));
                    vagas[c * 2 + l] = vaga;
                }

                // Pontos verticais: lição → aplicação
                var pv = new Image[2];
                for (int p = 0; p < 2; p++)
                {
                    RectTransform pt = Filho(col, "Ponto_" + p);
                    Pos(pt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -298 - p * 26), new Vector2(18, 18));
                    pv[p] = Img(pt.gameObject, S("caminho_ponto"), Color.white);
                    pv[p].raycastTarget = false;
                }
                trechos.Add((c * 2, pv));

                // Pontos horizontais: aplicação desta coluna → lição da próxima
                if (c < 3)
                {
                    RectTransform grupo = Filho(mapa, "Caminho_" + c);
                    grupo.anchorMin = grupo.anchorMax = new Vector2((c + 1) * 0.25f, 1);
                    grupo.pivot = new Vector2(0.5f, 0.5f);
                    grupo.anchoredPosition = new Vector2(0, -70 - 311);
                    grupo.sizeDelta = new Vector2(80, 20);
                    var ph = new Image[3];
                    for (int p = 0; p < 3; p++)
                    {
                        RectTransform pt = Filho(grupo, "Ponto_" + p);
                        Pos(pt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-28 + p * 28, 0), new Vector2(18, 18));
                        ph[p] = Img(pt.gameObject, S("caminho_ponto"), Color.white);
                        ph[p].raycastTarget = false;
                    }
                    trechos.Add((c * 2 + 1, ph));
                }
            }

            // Cartão-modelo
            var cartao = MontarCartao((RectTransform)root.transform.Find("Modelos/CartaoFase"));

            // Painel de detalhes
            var det = MontarDetalhes(fundo, larguraDetalhes);

            // Aviso, apagar progresso, legenda
            var aviso = (RectTransform)fundo.Find("Aviso");
            Pos(aviso, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(64, 124), new Vector2(1180, 48));
            Estilo(aviso.GetComponent<TMP_Text>(), fTexto, 26, Dourado, TextAlignmentOptions.MidlineLeft);

            var apagar = (RectTransform)fundo.Find("ApagarProgresso");
            Pos(apagar, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(64, 36), new Vector2(330, 76));
            EstiloBotao(apagar.GetComponent<Button>(), "perigo", 1.4f);
            var al = apagar.Find("Label").GetComponent<TMP_Text>();
            al.text = "Apagar progresso";
            Estilo(al, fTexto, 26, Color.white, TextAlignmentOptions.Center);
            Esticar((RectTransform)al.transform);

            RectTransform rodape = Filho(fundo, "Rodape");
            Pos(rodape, new Vector2(0.4f, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-64, 40), new Vector2(-64, 52));
            Legenda(rodape, ("←→↑↓", "Escolher fase"), ("Enter", "Jogar"), ("Esc", "Voltar"));

            MontarConfirmacao((RectTransform)fundo.Find("Confirmacao"));
            fundo.Find("Confirmacao").SetAsLastSibling();

            // Ligações do SeletorFasesUI
            var so = new SerializedObject(seletor);
            var pv2 = so.FindProperty("vagas");
            pv2.arraySize = 8;
            for (int i = 0; i < 8; i++) pv2.GetArrayElementAtIndex(i).objectReferenceValue = vagas[i];
            so.FindProperty("cartaoModelo").objectReferenceValue = cartao;
            so.FindProperty("linhaPreTeste").objectReferenceValue = linhaPre.botao;
            so.FindProperty("textoPreTeste").objectReferenceValue = linhaPre.texto;
            so.FindProperty("iconePreTeste").objectReferenceValue = linhaPre.icone;
            so.FindProperty("linhaPosTeste").objectReferenceValue = linhaPos.botao;
            so.FindProperty("textoPosTeste").objectReferenceValue = linhaPos.texto;
            so.FindProperty("iconePosTeste").objectReferenceValue = linhaPos.icone;
            so.FindProperty("spriteFeito").objectReferenceValue = S("icone_check");
            so.FindProperty("spritePendente").objectReferenceValue = S("icone_info");
            so.FindProperty("spriteBloqueado").objectReferenceValue = S("icone_cadeado");
            so.FindProperty("spritePonto").objectReferenceValue = S("caminho_ponto");
            so.FindProperty("spritePontoFeito").objectReferenceValue = S("caminho_ponto_feito");
            var cam = so.FindProperty("caminho");
            cam.arraySize = trechos.Count;
            for (int i = 0; i < trechos.Count; i++)
            {
                var e = cam.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("depoisDaFase").intValue = trechos[i].depois;
                var pts = e.FindPropertyRelative("pontos");
                pts.arraySize = trechos[i].pontos.Length;
                for (int p = 0; p < trechos[i].pontos.Length; p++) pts.GetArrayElementAtIndex(p).objectReferenceValue = trechos[i].pontos[p];
            }
            so.FindProperty("detalheModulo").objectReferenceValue = det.modulo;
            so.FindProperty("detalheTitulo").objectReferenceValue = det.titulo;
            so.FindProperty("detalheDescricao").objectReferenceValue = det.descricao;
            so.FindProperty("detalheConceitos").objectReferenceValue = det.chips;
            so.FindProperty("chipModelo").objectReferenceValue = det.chipModelo;
            so.FindProperty("detalheTentativas").objectReferenceValue = det.tentativas;
            so.FindProperty("detalheMelhorTempo").objectReferenceValue = det.tempo;
            so.FindProperty("detalheStatus").objectReferenceValue = det.status;
            so.FindProperty("botaoJogarFase").objectReferenceValue = det.jogar;
            so.FindProperty("rotuloJogarFase").objectReferenceValue = det.jogarRotulo;
            so.FindProperty("formatoSubtitulo").stringValue = "{0} de {1} concluídas";
            so.ApplyModifiedPropertiesWithoutUndo();

            Foco(root, det.jogar, voltar.GetComponent<Button>());

            PrefabUtility.SaveAsPrefabAsset(root, PrefabSeletor);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log("[LaboratorioUI] Seletor de fases atualizado.");
    }

    static (Button botao, TMP_Text texto, Image icone) LinhaQuestionario(RectTransform mapa, string nome, bool emCima)
    {
        RectTransform linha = Filho(mapa, nome);
        Pos(linha, new Vector2(0.5f, emCima ? 1 : 0), new Vector2(0.5f, emCima ? 1 : 0), new Vector2(0.5f, emCima ? 1 : 0),
            Vector2.zero, new Vector2(820, 60));
        var img = Img(linha.gameObject, S("painel_linha_foco"), Color.white, true);
        var b = Garantir<Button>(linha.gameObject);
        b.targetGraphic = img;
        b.transition = Selectable.Transition.ColorTint;
        var cores = ColorBlock.defaultColorBlock;
        cores.normalColor = new Color(1, 1, 1, 0);
        cores.highlightedColor = new Color(1, 1, 1, 0.55f);
        cores.selectedColor = Color.white;
        cores.pressedColor = new Color(1, 1, 1, 0.8f);
        cores.disabledColor = new Color(1, 1, 1, 0);
        b.colors = cores;

        RectTransform conteudo = Filho(linha, "Conteudo");
        Esticar(conteudo);
        var h = Garantir<HorizontalLayoutGroup>(conteudo.gameObject);
        h.childAlignment = TextAnchor.MiddleCenter;
        h.spacing = 14;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        RectTransform ic = Filho(conteudo, "Icone");
        var icImg = Img(ic.gameObject, S(emCima ? "icone_check" : "icone_cadeado"), Verde);
        icImg.raycastTarget = false;
        var le = Garantir<LayoutElement>(ic.gameObject);
        le.preferredWidth = le.minWidth = le.preferredHeight = le.minHeight = 34;
        var txt = Txt(conteudo, "Texto", emCima ? "Questionário inicial respondido" : "Questionário final · libera depois da fase 8",
            fTexto, 28, Verde, TextAlignmentOptions.MidlineLeft);
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        return (b, txt, icImg);
    }

    static CartaoFaseUI MontarCartao(RectTransform c)
    {
        c.sizeDelta = new Vector2(150, 167);
        var cartao = c.GetComponent<CartaoFaseUI>();
        var raizImg = c.GetComponent<Image>();
        if (raizImg != null) raizImg.enabled = false; // o fundo agora é o filho "Fundo", pra o anel de foco ficar atrás
        var outline = c.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;

        RectTransform foco = Filho(c, "Foco");
        foco.SetSiblingIndex(0);
        Esticar(foco);
        Img(foco.gameObject, S("card_fase_foco"), Color.white).raycastTarget = false;
        foco.gameObject.SetActive(false);

        RectTransform fundo = Filho(c, "Fundo");
        fundo.SetSiblingIndex(1);
        Esticar(fundo);
        var fundoImg = Img(fundo.gameObject, S("card_fase_disponivel"), Color.white);

        var botao = c.GetComponent<Button>();
        botao.targetGraphic = fundoImg;
        botao.transition = Selectable.Transition.None;

        var numero = (RectTransform)c.Find("Numero");
        Pos(numero, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(120, 36));
        Estilo(numero.GetComponent<TMP_Text>(), fCodigoBold, 26, TextoSuave, TextAlignmentOptions.Center);

        var concluida = (RectTransform)c.Find("IconeConcluida");
        Pos(concluida, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -42), new Vector2(64, 64));
        var ci = concluida.GetComponent<Image>();
        ci.sprite = S("selo_check");
        ci.color = Color.white;
        ci.raycastTarget = false;

        var bloqueada = c.Find("IconeBloqueada");
        if (bloqueada != null) bloqueada.gameObject.SetActive(false); // o cadeado já vem no sprite card_fase_bloqueada

        RectTransform selo = Filho(c, "SeloAtual");
        Pos(selo, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -42), new Vector2(64, 64));
        Img(selo.gameObject, S("selo_numero"), Color.white).raycastTarget = false;
        var seloNum = Txt(selo, "Numero", "1", fTitulo, 36, Fundo, TextAlignmentOptions.Center);
        Esticar((RectTransform)seloNum.transform);
        ((RectTransform)seloNum.transform).offsetMin = new Vector2(0, -2);
        selo.gameObject.SetActive(false);

        var titulo = (RectTransform)c.Find("Titulo");
        // Nome embaixo do cartão (no bloqueado o cadeado ocupa o meio do sprite).
        Pos(titulo, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(230, 56));
        var tt = titulo.GetComponent<TMP_Text>();
        Estilo(tt, fTexto, 22, Texto, TextAlignmentOptions.Top);
        tt.enableAutoSizing = true;
        tt.fontSizeMin = 16;
        tt.fontSizeMax = 22;
        tt.textWrappingMode = TextWrappingModes.Normal;

        foreach (string n in new[] { "Conceito", "Status" })
        {
            var t = c.Find(n);
            if (t != null) t.gameObject.SetActive(false);
        }

        // Ordem de desenho: foco, fundo, textos/selos
        foreach (string n in new[] { "Numero", "IconeConcluida", "SeloAtual", "Titulo" }) c.Find(n).SetAsLastSibling();

        var so = new SerializedObject(cartao);
        so.FindProperty("fundo").objectReferenceValue = fundoImg;
        so.FindProperty("borda").objectReferenceValue = null;
        so.FindProperty("iconeBloqueada").objectReferenceValue = null;
        so.FindProperty("iconeConcluida").objectReferenceValue = concluida.gameObject;
        so.FindProperty("seloAtual").objectReferenceValue = selo.gameObject;
        so.FindProperty("seloAtualNumero").objectReferenceValue = seloNum;
        so.FindProperty("foco").objectReferenceValue = foco.gameObject;
        so.FindProperty("esconderNumeroComSelo").boolValue = true;
        EstiloCartao(so.FindProperty("estiloBloqueada"), "card_fase_bloqueada", Apagado, TextoSuave);
        EstiloCartao(so.FindProperty("estiloDisponivel"), "card_fase_disponivel", TextoSuave, Texto);
        EstiloCartao(so.FindProperty("estiloConcluida"), "card_fase_concluida", TextoSuave, Texto);
        EstiloCartao(so.FindProperty("estiloAtual"), "card_fase_atual", Color.white, Color.white);
        so.ApplyModifiedPropertiesWithoutUndo();
        return cartao;
    }

    static void EstiloCartao(SerializedProperty p, string sprite, Color numero, Color titulo)
    {
        p.FindPropertyRelative("fundo").colorValue = Color.white;
        p.FindPropertyRelative("spriteFundo").objectReferenceValue = S(sprite);
        p.FindPropertyRelative("borda").colorValue = Color.clear;
        p.FindPropertyRelative("numero").colorValue = numero;
        p.FindPropertyRelative("titulo").colorValue = titulo;
        p.FindPropertyRelative("conceito").colorValue = TextoSuave;
        p.FindPropertyRelative("status").colorValue = TextoSuave;
    }

    struct Detalhes
    {
        public TMP_Text modulo, titulo, descricao, tentativas, tempo, status, jogarRotulo;
        public RectTransform chips;
        public GameObject chipModelo;
        public Button jogar;
    }

    static Detalhes MontarDetalhes(RectTransform fundo, float largura)
    {
        var d = new Detalhes();
        RectTransform p = Filho(fundo, "PainelDetalhes");
        Pos(p, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-64, -6), new Vector2(largura, 700));
        Img(p.gameObject, S("painel_janela"), Color.white, true);
        var v = Garantir<VerticalLayoutGroup>(p.gameObject);
        v.padding = new RectOffset(40, 40, 38, 40);
        v.spacing = 12;
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        d.modulo = Txt(p, "Modulo", "MÓDULO FÍSICA · FASE 1", fCodigoBold, 24, Azul, TextAlignmentOptions.MidlineLeft);
        d.titulo = Txt(p, "Titulo", "Física", fTitulo, 46, Texto, TextAlignmentOptions.MidlineLeft);
        d.titulo.enableAutoSizing = true;
        d.titulo.fontSizeMin = 30;
        d.titulo.fontSizeMax = 46;
        d.titulo.textWrappingMode = TextWrappingModes.NoWrap;
        var tle = Garantir<LayoutElement>(d.titulo.gameObject);
        tle.preferredHeight = tle.minHeight = 72;
        d.descricao = Txt(p, "Descricao", "Descrição da fase.", fTextoBold, 26, TextoSuave, TextAlignmentOptions.TopLeft);
        var dle = Garantir<LayoutElement>(d.descricao.gameObject);
        dle.minHeight = 70;
        d.status = Txt(p, "Status", "Liberada", fTexto, 24, Ciano, TextAlignmentOptions.MidlineLeft);
        Txt(p, "VoceVaiUsar", "VOCÊ VAI USAR", fTexto, 22, TextoSuave, TextAlignmentOptions.MidlineLeft).characterSpacing = 4;

        RectTransform chips = Filho(p, "Conceitos");
        var g = Garantir<GridLayoutGroup>(chips.gameObject);
        g.cellSize = new Vector2(232, 48);
        g.spacing = new Vector2(12, 12);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 2;
        g.childAlignment = TextAnchor.UpperLeft;
        d.chips = chips;
        RectTransform chip = Filho(chips, "ChipModelo");
        Img(chip.gameObject, S("seletor_caixa"), Color.white, true).pixelsPerUnitMultiplier = 2f;
        var ct = Txt(chip, "Texto", "Rigidbody2D", fCodigoBold, 22, Ciano, TextAlignmentOptions.Center);
        Esticar((RectTransform)ct.transform);
        ((RectTransform)ct.transform).offsetMin = new Vector2(10, 0);
        ((RectTransform)ct.transform).offsetMax = new Vector2(-10, 0);
        ct.enableAutoSizing = true;
        ct.fontSizeMin = 14;
        ct.fontSizeMax = 22;
        ct.textWrappingMode = TextWrappingModes.NoWrap;
        chip.gameObject.SetActive(false);
        d.chipModelo = chip.gameObject;

        RectTransform stats = Filho(p, "Estatisticas");
        var sh = Garantir<HorizontalLayoutGroup>(stats.gameObject);
        sh.spacing = 48;
        sh.childControlWidth = sh.childControlHeight = true;
        sh.childForceExpandWidth = sh.childForceExpandHeight = false;
        sh.padding = new RectOffset(0, 0, 8, 4);
        d.tentativas = Estatistica(stats, "Tentativas", "Tentativas", "0");
        d.tempo = Estatistica(stats, "MelhorTempo", "Melhor tempo", "--:--");

        RectTransform espaco = Filho(p, "Espaco");
        var ele = Garantir<LayoutElement>(espaco.gameObject);
        ele.flexibleHeight = 1;

        RectTransform jogar = Filho(p, "JogarFase");
        var jle = Garantir<LayoutElement>(jogar.gameObject);
        jle.preferredHeight = jle.minHeight = 96;
        Img(jogar.gameObject, S("btn_primario_normal"), Color.white, true);
        var jb = Garantir<Button>(jogar.gameObject);
        EstiloBotao(jb, "primario");
        RectTransform jic = Filho(jogar, "Icone");
        Pos(jic, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-108, 2), new Vector2(40, 40));
        Img(jic.gameObject, S("icone_jogar"), Color.white).raycastTarget = false;
        var jt = Txt(jogar, "Label", "Jogar fase", fTitulo, 36, Color.white, TextAlignmentOptions.Center);
        Esticar((RectTransform)jt.transform);
        ((RectTransform)jt.transform).offsetMin = new Vector2(40, 4);
        d.jogar = jb;
        d.jogarRotulo = jt;
        return d;
    }

    static TMP_Text Estatistica(RectTransform pai, string nome, string rotulo, string valor)
    {
        RectTransform col = Filho(pai, nome);
        var v = Garantir<VerticalLayoutGroup>(col.gameObject);
        v.spacing = 0;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = v.childForceExpandHeight = false;
        Txt(col, "Rotulo", rotulo, fTexto, 24, TextoSuave, TextAlignmentOptions.MidlineLeft);
        return Txt(col, "Valor", valor, fCodigoBold, 36, Texto, TextAlignmentOptions.MidlineLeft);
    }

    static void MontarConfirmacao(RectTransform conf)
    {
        var veu = conf.GetComponent<Image>();
        veu.sprite = S("veu_popup");
        veu.color = Color.white;
        var caixa = (RectTransform)conf.Find("Caixa");
        caixa.sizeDelta = new Vector2(900, 400);
        var ci = caixa.GetComponent<Image>();
        ci.sprite = S("painel_popup");
        ci.type = Image.Type.Sliced;
        ci.color = Color.white;
        Estilo(caixa.Find("Pergunta").GetComponent<TMP_Text>(), fTitulo, 44, Texto, TextAlignmentOptions.Center);
        Estilo(caixa.Find("Detalhe").GetComponent<TMP_Text>(), fTextoBold, 28, TextoSuave, TextAlignmentOptions.Top);
        foreach (var (nome, tipo, x) in new[] { ("Cancelar", "secundario", -300f), ("Apagar", "perigo", 20f) })
        {
            var r = (RectTransform)caixa.Find(nome);
            r.sizeDelta = new Vector2(280, 96);
            r.anchoredPosition = new Vector2(x, 44);
            EstiloBotao(r.GetComponent<Button>(), tipo);
            var l = r.Find("Label").GetComponent<TMP_Text>();
            Estilo(l, fTitulo, 34, tipo == "perigo" ? Color.white : Texto, TextAlignmentOptions.Center);
            Esticar((RectTransform)l.transform);
        }
    }

    // ------------------------------------------------------------------
    // Utilitários
    // ------------------------------------------------------------------

    static TMP_FontAsset CriarFonte(string nome, string chars)
    {
        string caminho = Pasta + "Fonts/" + nome + " SDF.asset";
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(caminho);
        if (fa == null)
        {
            var fonte = AssetDatabase.LoadAssetAtPath<Font>(Pasta + "Fonts/" + nome + ".ttf");
            fa = TMP_FontAsset.CreateFontAsset(fonte, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = nome + " SDF";
            AssetDatabase.CreateAsset(fa, caminho);
            fa.atlasTextures[0].name = nome + " SDF Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = nome + " SDF Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
        }

        fa.TryAddCharacters(chars, out string faltando);
        if (!string.IsNullOrEmpty(faltando)) Debug.Log($"[LaboratorioUI] {nome}: sem glifo para \"{faltando}\" (usa a fonte reserva).");
        EditorUtility.SetDirty(fa);
        return fa;
    }

    static Sprite S(string nome)
    {
        string caminho = nome.StartsWith("../") ? Pasta + nome.Substring(3) + ".png" : Pasta + "Sprites/" + nome + ".png";
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
        if (s == null) Debug.LogWarning("[LaboratorioUI] Sprite não encontrado: " + caminho);
        return s;
    }

    static Color Hex(string h)
    {
        ColorUtility.TryParseHtmlString(h, out Color c);
        return c;
    }

    static void Escalar(CanvasScaler scaler)
    {
        if (scaler == null) return;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        // Expand: a área 1920x1080 sempre cabe inteira na tela, em qualquer proporção.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.matchWidthOrHeight = 0.5f;
    }

    static T Garantir<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static RectTransform Filho(Transform pai, string nome)
    {
        Transform t = pai.Find(nome);
        if (t == null)
        {
            var go = new GameObject(nome, typeof(RectTransform));
            go.layer = pai.gameObject.layer;
            go.transform.SetParent(pai, false);
            t = go.transform;
        }
        return (RectTransform)t;
    }

    static void Esticar(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = r.offsetMax = Vector2.zero;
        r.localScale = Vector3.one;
    }

    static void Pos(RectTransform r, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 tamanho)
    {
        r.anchorMin = aMin;
        r.anchorMax = aMax;
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = tamanho;
        r.localScale = new Vector3(1, r.localScale.y < 0 ? -1 : 1, 1);
    }

    // Faixa horizontal do botão (depois do ícone), centrada em y em relação ao meio.
    static void CentroV(RectTransform r, float y, float altura)
    {
        r.anchorMin = new Vector2(0, 0.5f);
        r.anchorMax = new Vector2(1, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(112, y - altura / 2f);
        r.offsetMax = new Vector2(-24, y + altura / 2f);
    }

    static void Centro(RectTransform r, Vector2 pos, Vector2 tamanho) =>
        Pos(r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, tamanho);

    static void Envelope(RectTransform r, Sprite s, float proporcao)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero;
        var img = Img(r.gameObject, s, Color.white);
        img.raycastTarget = false;
        var fit = Garantir<AspectRatioFitter>(r.gameObject);
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = proporcao;
    }

    static Image Img(GameObject go, Sprite s, Color cor, bool fatiada = false)
    {
        var img = Garantir<Image>(go);
        img.sprite = s;
        img.color = cor;
        img.type = fatiada ? Image.Type.Sliced : Image.Type.Simple;
        img.pixelsPerUnitMultiplier = 1f;
        return img;
    }

    static TextMeshProUGUI Txt(Transform pai, string nome, string texto, TMP_FontAsset f, float tamanho, Color cor, TextAlignmentOptions al)
    {
        RectTransform r = Filho(pai, nome);
        var t = Garantir<TextMeshProUGUI>(r.gameObject);
        t.text = texto;
        Estilo(t, f, tamanho, cor, al);
        t.raycastTarget = false;
        return t;
    }

    static void Estilo(TMP_Text t, TMP_FontAsset f, float tamanho, Color cor, TextAlignmentOptions al)
    {
        t.font = f;
        t.fontSharedMaterial = f.material;
        t.fontSize = tamanho;
        t.enableAutoSizing = false;
        t.fontStyle = FontStyles.Normal;
        t.color = cor;
        t.alignment = al;
        t.richText = true;
    }

    static void EstiloBotao(Button b, string tipo, float ppu = 1f)
    {
        var img = b.GetComponent<Image>();
        bool icone = tipo == "icone";
        img.sprite = S($"btn_{tipo}_normal");
        img.type = icone ? Image.Type.Simple : Image.Type.Sliced;
        img.color = Color.white;
        img.pixelsPerUnitMultiplier = ppu;
        img.preserveAspect = icone;
        b.targetGraphic = img;
        b.transition = Selectable.Transition.SpriteSwap;
        b.spriteState = new SpriteState
        {
            highlightedSprite = S($"btn_{tipo}_hover"),
            pressedSprite = S($"btn_{tipo}_pressed"),
            selectedSprite = S($"btn_{tipo}_focus"),
            disabledSprite = icone ? null : S($"btn_{tipo}_disabled"),
        };
    }

    // Navegação explícita em coluna (cima/baixo), pra o teclado não "pular" pra outro painel.
    static void Encadear(params Selectable[] itens)
    {
        for (int i = 0; i < itens.Length; i++)
        {
            var n = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = i > 0 ? itens[i - 1] : null,
                selectOnDown = i < itens.Length - 1 ? itens[i + 1] : null,
            };
            itens[i].navigation = n;
        }
    }

    static void Foco(GameObject go, params Selectable[] candidatos)
    {
        var f = Garantir<FocoInicialUI>(go);
        var so = new SerializedObject(f);
        var p = so.FindProperty("candidatos");
        p.arraySize = candidatos.Length;
        for (int i = 0; i < candidatos.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = candidatos[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Legenda(RectTransform pai, params (string tecla, string rotulo)[] itens)
    {
        RectTransform leg = Filho(pai, "Legenda");
        Pos(leg, new Vector2(0.3f, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, Vector2.zero);
        var h = Garantir<HorizontalLayoutGroup>(leg.gameObject);
        h.childAlignment = TextAnchor.MiddleRight;
        h.spacing = 12;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        for (int i = leg.childCount - 1; i >= 0; i--) Object.DestroyImmediate(leg.GetChild(i).gameObject);

        foreach (var (tecla, rotulo) in itens)
        {
            RectTransform k = Filho(leg, "Tecla_" + rotulo);
            Img(k.gameObject, S("btn_secundario_normal"), Color.white, true).pixelsPerUnitMultiplier = 2.6f;
            var kh = Garantir<HorizontalLayoutGroup>(k.gameObject);
            kh.padding = new RectOffset(12, 12, 4, 6);
            kh.childControlWidth = kh.childControlHeight = true;
            kh.childForceExpandWidth = kh.childForceExpandHeight = false;
            kh.childAlignment = TextAnchor.MiddleCenter;
            var kle = Garantir<LayoutElement>(k.gameObject);
            kle.minHeight = 44;
            kle.minWidth = 44;
            Txt(k, "Texto", tecla, fCodigoBold, 22, Texto, TextAlignmentOptions.Center).textWrappingMode = TextWrappingModes.NoWrap;
            var r = Txt(leg, "Rotulo_" + rotulo, rotulo, fTexto, 24, TextoSuave, TextAlignmentOptions.MidlineLeft);
            r.textWrappingMode = TextWrappingModes.NoWrap;
            r.margin = new Vector4(0, 0, 18, 0);
        }
    }
}
