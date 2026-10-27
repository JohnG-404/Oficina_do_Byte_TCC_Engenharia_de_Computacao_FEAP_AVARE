using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Monta o inventário novo da cena "Aplicacao_Fisica" (painel escuro no canto
/// superior esquerdo, um cartão por ferramenta, selo de usos, dica ao passar o
/// mouse e a linha "Você tem") e liga a colisão inicial nos blocos fixos do
/// cenário. Substitui o InventoryPanel antigo, mas mantém os usos que estavam
/// configurados em cada ícone.
///
/// Funciona com a cena aberta sozinha ou junto com outras (modo aditivo).
/// Pode rodar de novo: refaz o painel do zero e não duplica a Parede.
/// </summary>
public static class AplicacaoInventarioBuilder
{
    private const string ScenePath = "Assets/Scenes/Aplicacao_Fisica.unity";
    private const string SpriteDir = "Assets/Art/UI/Aplicacao/";
    private const string IconDir = "Assets/Images/Scene_Col_Rig/";
    private const string FontDir = "Assets/Art/UI/Fonts/";

    // Paleta igual à das outras fases (painéis escuros com borda colorida).
    private static readonly Color Painel = Hex("141B29", 0.96f);
    private static readonly Color PainelBorda = Hex("2E323C");
    private static readonly Color Cartao = Hex("0F1522");
    private static readonly Color Chip = Hex("1E2026");
    private static readonly Color Texto = Hex("E6E9EF");
    private static readonly Color TextoApagado = Hex("8A93A6");
    private static readonly Color DicaFundo = Hex("0B0F18", 0.97f);

    // Blocos que já começam sólidos (cenário fixo). Os outros continuam
    // esperando o Collider2D do jogador.
    private static readonly string[] BlocosComColisao = { "Plataforma2", "Plataforma3", "Plataforma5", "Plataforma7", "Plataforma8" };

    private struct Ferramenta
    {
        public ToolType tipo;
        public string nome;
        public string icone;
        public string etiqueta;
        public string dica;
        public float larguraChip;
    }

    private static readonly Ferramenta[] Ferramentas =
    {
        new Ferramenta { tipo = ToolType.Fisica, nome = "Rigidbody2D", icone = "icon_rigidbody2d_v2", etiqueta = "COMPONENTE", dica = "Dá gravidade e massa. Sem chão sólido embaixo, cai.", larguraChip = 118f },
        new Ferramenta { tipo = ToolType.Colisao, nome = "Collider2D", icone = "icon_collider2d_v2", etiqueta = "COMPONENTE", dica = "Deixa sólido: segura, bloqueia e dá pra pisar.", larguraChip = 110f },
        new Ferramenta { tipo = ToolType.Movimento, nome = "Movimento", icone = "icon_movement_v2", etiqueta = "SCRIPT", dica = "Só no seu personagem: faz andar para os lados.", larguraChip = 102f },
        new Ferramenta { tipo = ToolType.Pulo, nome = "Pulo", icone = "icon_jump", etiqueta = "SCRIPT", dica = "Só no seu personagem: faz pular.", larguraChip = 62f },
    };

    // Chips "Você tem" com 18 px numa grade 2x2 e
    // etiquetas COMPONENTE/SCRIPT com 16 px (antes 13 px e 10 px).
    private const float Pad = 16f, LarguraCartao = 118f, AlturaCartao = 140f, GapCartao = 12f, TopoCartoes = 56f;
    private const float FonteChip = 18f, AlturaChip = 30f, GapChip = 6f, XChips = Pad + 92f;
    private const float FonteEtiqueta = 16f, AlturaEtiqueta = 22f;
    private static float LarguraPainel => Pad * 2 + LarguraCartao * 4 + GapCartao * 3;
    private static float AlturaPainel => TopoCartoes + AlturaCartao + Pad + 84f;
    private static float YLinhaVoceTem => -(TopoCartoes + AlturaCartao + Pad - 2f);
    private static float LarguraEtiqueta(string texto) => texto.Length * 9.8f + 16f;

    [MenuItem("Tools/Aplicação/Chips e etiquetas do inventário maiores")]
    public static void AumentarTextosMenu()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[AplicacaoInventarioBuilder] Abra a cena Aplicacao_Fisica antes de rodar.");
            return;
        }
        Canvas canvas = scene.GetRootGameObjects().Select(g => g.GetComponent<Canvas>()).FirstOrDefault(c => c != null);
        RectTransform painel = canvas != null ? canvas.transform.Find("InventoryPanel") as RectTransform : null;
        if (painel == null)
        {
            Debug.LogError("[AplicacaoInventarioBuilder] InventoryPanel não encontrado.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(painel.gameObject, "Inventário: textos maiores");
        painel.sizeDelta = new Vector2(painel.sizeDelta.x, AlturaPainel);
        if (painel.Find("VoceTem") is RectTransform voceTem)
        {
            PosicionarVoceTem(voceTem);
        }
        for (int i = 0; i < Ferramentas.Length; i++)
        {
            if (painel.Find("Chip_" + Ferramentas[i].nome) is RectTransform chip)
            {
                PosicionarChip(chip, i);
            }
            if (painel.Find("Cartao_" + Ferramentas[i].nome + "/Conteudo/Etiqueta") is RectTransform tag)
            {
                PosicionarEtiqueta(tag, Ferramentas[i].etiqueta);
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[AplicacaoInventarioBuilder] Chips (18 px) e etiquetas (16 px) ajustados; painel com " + AlturaPainel + " px de altura.");
    }

    private static void PosicionarVoceTem(RectTransform rt)
    {
        Canto(rt, new Vector2(Pad, YLinhaVoceTem - 8f), new Vector2(90f, AlturaChip));
        if (rt.GetComponent<TMP_Text>() is TMP_Text t) t.fontSize = FonteChip;
    }

    // Grade 2x2 à direita do "Você tem:"; o fundo e a borda esticam junto.
    private static void PosicionarChip(RectTransform chip, int indice)
    {
        float larguraColuna = (LarguraPainel - Pad - XChips) / 2f;
        float largura = larguraColuna - 8f;
        Canto(chip, new Vector2(XChips + (indice % 2) * larguraColuna, YLinhaVoceTem - 8f - (indice / 2) * (AlturaChip + GapChip)), new Vector2(largura, AlturaChip));
        if (chip.Find("Ponto") is RectTransform ponto)
        {
            Canto(ponto, new Vector2(10f, -(AlturaChip - 10f) / 2f), new Vector2(10f, 10f));
        }
        if (chip.Find("Nome") is RectTransform nome)
        {
            Canto(nome, new Vector2(26f, 0f), new Vector2(largura - 30f, AlturaChip));
            if (nome.GetComponent<TMP_Text>() is TMP_Text t) t.fontSize = FonteChip;
        }
    }

    private static void PosicionarEtiqueta(RectTransform tag, string texto)
    {
        float lw = LarguraEtiqueta(texto);
        Canto(tag, new Vector2((LarguraCartao - lw) / 2f, -108f), new Vector2(lw, AlturaEtiqueta));
        if (tag.Find("Texto") is RectTransform rt)
        {
            Canto(rt, Vector2.zero, new Vector2(lw, AlturaEtiqueta));
            if (rt.GetComponent<TMP_Text>() is TMP_Text t) t.fontSize = FonteEtiqueta;
        }
    }

    [MenuItem("Tools/Aplicação/Montar Inventário Novo e Colisões Iniciais")]
    public static void BuildMenu()
    {
        foreach (string line in Build()) Debug.Log("[AplicacaoInventarioBuilder] " + line);
    }

    public static List<string> Build()
    {
        var log = new List<string>();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            log.Add("ERRO: abra a cena Aplicacao_Fisica antes de rodar.");
            return log;
        }

        ConfigurarSprites();
        log.AddRange(MontarInventario(scene));
        log.AddRange(ColisoesIniciais(scene));
        AtualizarCores(scene, log);
        EditorSceneManager.MarkSceneDirty(scene);
        return log;
    }

    // ---------------------------------------------------------------- sprites

    private static void ConfigurarSprites()
    {
        SetBorder("inv_arredondado.png", 20);
        SetBorder("inv_borda.png", 20);
        SetBorder("inv_pilula.png", 15);
        SetBorder("inv_pilula_borda.png", 15);
        SetBorder("inv_circulo.png", 0);
    }

    private static void SetBorder(string file, int border)
    {
        var importer = AssetImporter.GetAtPath(SpriteDir + file) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("[AplicacaoInventarioBuilder] Sprite não encontrado: " + SpriteDir + file);
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.spriteBorder = new Vector4(border, border, border, border);
        importer.SaveAndReimport();
    }

    private static Sprite S(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + file);

    // ------------------------------------------------------------- inventário

    private static List<string> MontarInventario(Scene scene)
    {
        var log = new List<string>();
        Canvas canvas = scene.GetRootGameObjects().Select(g => g.GetComponent<Canvas>()).FirstOrDefault(c => c != null);
        if (canvas == null)
        {
            log.Add("ERRO: Canvas não encontrado.");
            return log;
        }

        // Usos configurados hoje em cada ferramenta (o resto do painel antigo é descartado).
        var usos = new Dictionary<ToolType, int>();
        foreach (DraggableToolIcon icon in canvas.GetComponentsInChildren<DraggableToolIcon>(true))
        {
            usos[icon.Tool] = new SerializedObject(icon).FindProperty("usosDisponiveis").intValue;
        }

        Transform antigo = canvas.transform.Find("InventoryPanel");
        int indice = antigo != null ? antigo.GetSiblingIndex() : 0;
        if (antigo != null) Undo.DestroyObjectImmediate(antigo.gameObject);
        Transform dicaAntiga = canvas.transform.Find("InventarioDica");
        if (dicaAntiga != null) Undo.DestroyObjectImmediate(dicaAntiga.gameObject);

        const float pad = Pad, cw = LarguraCartao, ch = AlturaCartao, gap = GapCartao, topo = TopoCartoes;
        float largura = LarguraPainel;
        float altura = AlturaPainel;

        RectTransform painel = Novo("InventoryPanel", canvas.transform);
        Undo.RegisterCreatedObjectUndo(painel.gameObject, "Inventário novo");
        painel.SetSiblingIndex(indice);
        Canto(painel, new Vector2(150f, -28f), new Vector2(largura, altura));
        Fatia(painel, "Fundo", "inv_arredondado.png", Painel);
        Fatia(painel, "Borda", "inv_borda.png", PainelBorda);

        // Cabeçalho
        Imagem(painel, "Marca", "inv_arredondado.png", ToolColors.Fisica, new Vector2(18f, -17f), new Vector2(22f, 22f), 3f);
        Rotulo(painel, "Titulo", "Inventário", "Nunito-ExtraBold SDF", 24, Texto, new Vector2(50f, -10f), new Vector2(150f, 34f), TextAlignmentOptions.MidlineLeft);
        Rotulo(painel, "Subtitulo", "arraste até o objeto", "Nunito-Bold SDF", 16, TextoApagado, new Vector2(176f, -12f), new Vector2(200f, 32f), TextAlignmentOptions.MidlineLeft);
        // "Segure [R] p/ recomeçar": a tecla é a atual da ação Reiniciar fase (RotuloTeclaUI).
        float kx = largura - pad - 120f;
        Rotulo(painel, "Segure", "segure", "Nunito-Bold SDF", 15, TextoApagado, new Vector2(kx - 58f, -12f), new Vector2(52f, 30f), TextAlignmentOptions.MidlineRight);
        RectTransform tecla = Imagem(painel, "TeclaR", "inv_arredondado.png", Chip, new Vector2(kx, -14f), new Vector2(26f, 26f), 3f);
        Fatia(tecla, "Borda", "inv_borda.png", PainelBorda, 3f);
        TMP_Text letra = Rotulo(tecla, "Letra", "R", "JetBrainsMono-Bold SDF", 15, Texto, Vector2.zero, new Vector2(26f, 26f), TextAlignmentOptions.Center);
        letra.enableAutoSizing = true;
        letra.fontSizeMin = 8f;
        letra.fontSizeMax = 15f;
        var rotuloTecla = Undo.AddComponent<RotuloTeclaUI>(tecla.gameObject);
        var soTecla = new SerializedObject(rotuloTecla);
        soTecla.FindProperty("texto").objectReferenceValue = letra;
        soTecla.ApplyModifiedPropertiesWithoutUndo();
        Rotulo(painel, "Recomeca", "p/ recomeçar", "Nunito-Bold SDF", 15, TextoApagado, new Vector2(kx + 34f, -12f), new Vector2(90f, 30f), TextAlignmentOptions.MidlineLeft);

        // Cartões
        for (int i = 0; i < Ferramentas.Length; i++)
        {
            Ferramenta f = Ferramentas[i];
            int u = usos.TryGetValue(f.tipo, out int v) ? v : -1;
            CriarCartao(painel, f, u, new Vector2(pad + i * (cw + gap), -topo), new Vector2(cw, ch));
            log.Add($"cartão {f.nome}: usos {(u < 0 ? "sem limite" : u.ToString())}");
        }

        // Linha "Você tem:"
        float ly = -(topo + ch + pad - 2f);
        RectTransform linha = Novo("Separador", painel);
        Canto(linha, new Vector2(pad, ly), new Vector2(largura - pad * 2, 1f));
        Img(linha, null, PainelBorda);
        PosicionarVoceTem((RectTransform)Rotulo(painel, "VoceTem", "Você tem:", "Nunito-Bold SDF", FonteChip, TextoApagado, Vector2.zero, Vector2.zero, TextAlignmentOptions.MidlineLeft).transform);

        var status = painel.gameObject.AddComponent<InventarioStatusJogador>();
        var chips = new List<InventarioStatusJogador.Chip>();
        for (int i = 0; i < Ferramentas.Length; i++)
        {
            Ferramenta f = Ferramentas[i];
            RectTransform chipRt = Novo("Chip_" + f.nome, painel);
            Image fundo = Img(chipRt, S("inv_pilula.png"), Chip, true);
            RectTransform bordaRt = Novo("Borda", chipRt);
            Esticar(bordaRt);
            Image borda = Img(bordaRt, S("inv_pilula_borda.png"), PainelBorda, true);
            Image ponto = Imagem(chipRt, "Ponto", "inv_circulo.png", PainelBorda, Vector2.zero, Vector2.zero).GetComponent<Image>();
            TMP_Text nome = Rotulo(chipRt, "Nome", f.nome, "JetBrainsMono-Medium SDF", FonteChip, TextoApagado, Vector2.zero, Vector2.zero, TextAlignmentOptions.MidlineLeft);
            PosicionarChip(chipRt, i);
            chips.Add(new InventarioStatusJogador.Chip { ferramenta = f.tipo, fundo = fundo, borda = borda, ponto = ponto, nome = nome });
        }

        var so = new SerializedObject(status);
        SerializedProperty arr = so.FindProperty("chips");
        arr.arraySize = chips.Count;
        for (int i = 0; i < chips.Count; i++)
        {
            SerializedProperty el = arr.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("ferramenta").enumValueIndex = (int)chips[i].ferramenta;
            el.FindPropertyRelative("fundo").objectReferenceValue = chips[i].fundo;
            el.FindPropertyRelative("borda").objectReferenceValue = chips[i].borda;
            el.FindPropertyRelative("ponto").objectReferenceValue = chips[i].ponto;
            el.FindPropertyRelative("nome").objectReferenceValue = chips[i].nome;
        }
        so.FindProperty("jogador").objectReferenceValue = scene.GetRootGameObjects().Select(g => g.GetComponent<AplicacaoPlayerController>()).FirstOrDefault(p => p != null);
        so.ApplyModifiedPropertiesWithoutUndo();

        CriarDica(canvas.transform);

        // AplicacaoLevelFlow esconde o inventário durante a intro.
        AplicacaoLevelFlow flow = scene.GetRootGameObjects().Select(g => g.GetComponent<AplicacaoLevelFlow>()).FirstOrDefault(c => c != null);
        if (flow != null)
        {
            var fso = new SerializedObject(flow);
            fso.FindProperty("inventoryPanel").objectReferenceValue = painel.gameObject;
            SerializedProperty intro = fso.FindProperty("introDialogue");
            for (int i = 0; i < intro.arraySize; i++)
            {
                SerializedProperty linhaIntro = intro.GetArrayElementAtIndex(i);
                linhaIntro.stringValue = linhaIntro.stringValue.Replace("canto superior direito", "canto superior esquerdo");
            }
            fso.ApplyModifiedPropertiesWithoutUndo();
            log.Add("AplicacaoLevelFlow: referência do inventário e fala do Prof atualizadas");
        }

        return log;
    }

    private static void CriarCartao(RectTransform painel, Ferramenta f, int usos, Vector2 pos, Vector2 tam)
    {
        Color cor = ToolColors.For(f.tipo);
        RectTransform cartao = Novo("Cartao_" + f.nome, painel);
        Canto(cartao, pos, tam);
        Img(cartao, S("inv_arredondado.png"), Cartao, true);
        cartao.gameObject.AddComponent<CanvasGroup>();
        Fatia(cartao, "Borda", "inv_borda.png", cor);

        // Nome + etiqueta (escondidos enquanto arrasta)
        RectTransform conteudo = Novo("Conteudo", cartao);
        Esticar(conteudo);
        Rotulo(conteudo, "Nome", f.nome, "JetBrainsMono-Bold SDF", f.nome.Length > 9 ? 15 : 16, Texto, new Vector2(0f, -86f), new Vector2(tam.x, 22f), TextAlignmentOptions.Center);
        RectTransform tag = Novo("Etiqueta", conteudo);
        Img(tag, S("inv_pilula.png"), new Color(cor.r, cor.g, cor.b, 0.16f), true);
        RectTransform tagBorda = Novo("Borda", tag);
        Esticar(tagBorda);
        Img(tagBorda, S("inv_pilula_borda.png"), new Color(cor.r, cor.g, cor.b, 0.65f), true);
        Rotulo(tag, "Texto", f.etiqueta, "JetBrainsMono-Bold SDF", FonteEtiqueta, cor, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
        PosicionarEtiqueta(tag, f.etiqueta);

        // "solte no objeto" (só aparece arrastando)
        RectTransform vazio = Novo("Vazio", cartao);
        Esticar(vazio);
        TMP_Text vt = Rotulo(vazio, "Texto", "solte no\nobjeto", "Nunito-Bold SDF", 15, TextoApagado, Vector2.zero, tam, TextAlignmentOptions.Center);
        Esticar((RectTransform)vt.transform);
        vazio.gameObject.SetActive(false);

        // Selo de usos (aparece só quando a ferramenta tem limite)
        RectTransform selo = Novo("Selo", cartao);
        selo.anchorMin = selo.anchorMax = new Vector2(1f, 1f);
        selo.pivot = new Vector2(1f, 0.5f);
        selo.anchoredPosition = new Vector2(6f, 0f);
        selo.sizeDelta = new Vector2(44f, 26f);
        Image seloImg = Img(selo, S("inv_pilula.png"), cor, true);
        RectTransform seloAro = Novo("Aro", selo);
        Esticar(seloAro);
        Img(seloAro, S("inv_pilula_borda.png"), Painel, true);
        TMP_Text seloTxt = Rotulo(selo, "Usos", usos >= 0 ? "x" + usos : "", "JetBrainsMono-Bold SDF", 15, Color.white, Vector2.zero, new Vector2(44f, 26f), TextAlignmentOptions.Center);
        selo.gameObject.SetActive(usos >= 0);

        // Área arrastável: o cartão inteiro recebe o mouse, só o ícone acompanha.
        RectTransform arr = Novo("Arrastavel", cartao);
        Esticar(arr);
        Image hit = arr.gameObject.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);
        RectTransform icone = Novo("Icone", arr);
        icone.anchorMin = icone.anchorMax = new Vector2(0.5f, 1f);
        icone.pivot = new Vector2(0.5f, 0.5f);
        icone.anchoredPosition = new Vector2(0f, -44f);
        icone.sizeDelta = new Vector2(66f, 66f);
        Image ii = icone.gameObject.AddComponent<Image>();
        ii.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + f.icone + ".png");
        ii.preserveAspect = true;
        ii.raycastTarget = false;

        var drag = arr.gameObject.AddComponent<DraggableToolIcon>();
        var so = new SerializedObject(drag);
        so.FindProperty("toolType").enumValueIndex = (int)f.tipo;
        so.FindProperty("usosDisponiveis").intValue = usos;
        so.FindProperty("usosLabel").objectReferenceValue = seloTxt;
        so.FindProperty("dica").stringValue = f.dica;
        so.FindProperty("cartao").objectReferenceValue = cartao;
        so.FindProperty("conteudoCartao").objectReferenceValue = conteudo.gameObject;
        so.FindProperty("espacoVazio").objectReferenceValue = vazio.gameObject;
        so.FindProperty("seloUsos").objectReferenceValue = seloImg;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CriarDica(Transform canvas)
    {
        RectTransform raiz = Novo("InventarioDica", canvas);
        Undo.RegisterCreatedObjectUndo(raiz.gameObject, "Dica do inventário");
        Esticar(raiz);
        raiz.SetAsLastSibling();

        RectTransform caixa = Novo("Caixa", raiz);
        caixa.anchorMin = caixa.anchorMax = new Vector2(0.5f, 0.5f);
        caixa.pivot = new Vector2(0f, 1f);
        caixa.sizeDelta = new Vector2(400f, 70f);
        Img(caixa, S("inv_arredondado.png"), DicaFundo, true);
        RectTransform bordaRt = Novo("Borda", caixa);
        Esticar(bordaRt);
        Image borda = Img(bordaRt, S("inv_borda.png"), ToolColors.Colisao, true);
        TMP_Text titulo = Rotulo(caixa, "Titulo", "", "JetBrainsMono-Bold SDF", 16, ToolColors.Colisao, new Vector2(14f, -8f), new Vector2(372f, 24f), TextAlignmentOptions.MidlineLeft);
        TMP_Text texto = Rotulo(caixa, "Texto", "", "Nunito-Bold SDF", 16, Texto, new Vector2(14f, -36f), new Vector2(372f, 26f), TextAlignmentOptions.MidlineLeft);
        caixa.gameObject.SetActive(false);

        var dica = raiz.gameObject.AddComponent<InventarioDica>();
        var so = new SerializedObject(dica);
        so.FindProperty("caixa").objectReferenceValue = caixa;
        so.FindProperty("titulo").objectReferenceValue = titulo;
        so.FindProperty("texto").objectReferenceValue = texto;
        so.FindProperty("borda").objectReferenceValue = borda;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // -------------------------------------------------------- colisões iniciais

    private static List<string> ColisoesIniciais(Scene scene)
    {
        var log = new List<string>();
        GameObject cenario = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Cenario");
        Transform ground = cenario != null ? cenario.transform.Find("Ground") : null;
        if (ground == null)
        {
            log.Add("ERRO: Cenario/Ground não encontrado.");
            return log;
        }

        foreach (string nome in BlocosComColisao)
        {
            Transform bloco = ground.Find(nome);
            PhysicsTarget pt = bloco != null ? bloco.GetComponent<PhysicsTarget>() : null;
            if (pt == null)
            {
                log.Add("AVISO: bloco não encontrado: " + nome);
                continue;
            }

            SetComecaComColisao(pt, true);
            log.Add(nome + ": começa com colisão");
        }

        log.Add(SepararParede(ground));
        return log;
    }

    private static void SetComecaComColisao(PhysicsTarget pt, bool valor)
    {
        var so = new SerializedObject(pt);
        so.FindProperty("comecaComColisao").boolValue = valor;
        so.ApplyModifiedProperties();
    }

    // A parede da esquerda fazia parte da Plataforma1: sem colisão, dava pra
    // atravessá-la e cair pra fora da fase. Vira um bloco próprio, já sólido;
    // o piso inicial continua sendo a primeira lição (sem colisão).
    private static string SepararParede(Transform ground)
    {
        if (ground.Find("Parede") != null)
        {
            return "Parede: já existia, nada a fazer";
        }

        Transform p1 = ground.Find("Plataforma1");
        if (p1 == null)
        {
            return "AVISO: Plataforma1 não encontrada, parede não separada";
        }

        var tiles = new List<Transform>();
        foreach (Transform t in p1)
        {
            var r = t.GetComponent<SpriteRenderer>();
            if (r != null && r.bounds.min.y >= -4.2f && r.bounds.max.x <= -8.39f)
            {
                tiles.Add(t);
            }
        }

        if (tiles.Count == 0)
        {
            return "AVISO: nenhum tile de parede encontrado na Plataforma1";
        }

        var parede = new GameObject("Parede");
        Undo.RegisterCreatedObjectUndo(parede, "Separar Parede");
        parede.transform.SetParent(ground, false);
        parede.transform.localPosition = p1.localPosition;
        parede.transform.localRotation = p1.localRotation;
        parede.transform.localScale = p1.localScale;
        parede.transform.SetSiblingIndex(p1.GetSiblingIndex() + 1);
        parede.layer = p1.gameObject.layer;

        var rbOrig = p1.GetComponent<Rigidbody2D>();
        var rb = parede.AddComponent<Rigidbody2D>();
        rb.bodyType = rbOrig != null ? rbOrig.bodyType : RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        var ccOrig = p1.GetComponent<CompositeCollider2D>();
        var cc = parede.AddComponent<CompositeCollider2D>();
        if (ccOrig != null)
        {
            cc.geometryType = ccOrig.geometryType;
            cc.generationType = ccOrig.generationType;
            cc.offsetDistance = ccOrig.offsetDistance;
        }

        var pt = parede.AddComponent<PhysicsTarget>();
        var so = new SerializedObject(pt);
        so.FindProperty("comecaComColisao").boolValue = true;
        so.FindProperty("requiresPhysics").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        foreach (Transform t in tiles)
        {
            Undo.SetTransformParent(t, parede.transform, "Separar Parede");
        }

        return $"Parede: {tiles.Count} tiles saíram da Plataforma1 para um bloco próprio, já sólido";
    }

    // Os contornos coloridos foram trocados pelos selos (AplicacaoSelosBuilder):
    // não há mais cores de contorno para atualizar.
    private static void AtualizarCores(Scene scene, List<string> log)
    {
        log.Add("contornos substituídos pelos selos; nada a atualizar");
    }

    // ---------------------------------------------------------------- helpers

    private static RectTransform Novo(string nome, Transform pai)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(pai, false);
        return rt;
    }

    private static void Canto(RectTransform rt, Vector2 pos, Vector2 tam)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
    }

    private static void Esticar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Image Img(RectTransform rt, Sprite sprite, Color cor, bool fatiado = false, float ppuMult = 1f)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = cor;
        img.raycastTarget = false;
        if (fatiado && sprite != null)
        {
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = ppuMult;
        }
        return img;
    }

    private static void Fatia(RectTransform pai, string nome, string sprite, Color cor, float ppuMult = 1f)
    {
        RectTransform rt = Novo(nome, pai);
        Esticar(rt);
        Img(rt, S(sprite), cor, true, ppuMult);
    }

    private static RectTransform Imagem(RectTransform pai, string nome, string sprite, Color cor, Vector2 pos, Vector2 tam, float ppuMult = 1f)
    {
        RectTransform rt = Novo(nome, pai);
        Canto(rt, pos, tam);
        Img(rt, S(sprite), cor, true, ppuMult);
        return rt;
    }

    private static TMP_Text Rotulo(RectTransform pai, string nome, string texto, string fonte, float tamanho, Color cor, Vector2 pos, Vector2 tam, TextAlignmentOptions alinhamento)
    {
        RectTransform rt = Novo(nome, pai);
        Canto(rt, pos, tam);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDir + fonte + ".asset");
        tmp.text = texto;
        tmp.fontSize = tamanho;
        tmp.color = cor;
        tmp.alignment = alinhamento;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Color Hex(string hex, float a = 1f)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        c.a = a;
        return c;
    }
}
