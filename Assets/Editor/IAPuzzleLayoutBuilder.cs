using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Os 3 puzzles da cena IA no mesmo padrão visual do
/// "código com lacunas" da Física (FisicaCodigoBuilder):
/// - cabeçalho "Fase IA · Puzzle X de 3" com a trilha dos 3 puzzles;
/// - janelas no tamanho da Física, abas com título (Guarda, Peças, Dica);
/// - o cérebro do guarda vira uma lista de regras com lacunas (mesmo
///   encaixe da Física: brilho da próxima, faixa da linha, ✔, a palavra
///   aparece escrita ao encaixar) — CodigoEncaixeUI;
/// - Objetivo + prévia + "0 de N encaixes" no tamanho da Física;
/// - peças com a moldura e o ícone da cor do tipo, a mesma fonte e altura e
///   uma etiqueta (AÇÃO, CONDIÇÃO, EVENTO...). O nome do Unity continua
///   numa linha menor embaixo.
/// A lógica (SnapZone, DragPiece, PuzzleControl, IAPecaFixa, IAFlow) não muda.
/// Tudo salvo na cena; idempotente; recusa em Play ou com a IA suja.
/// Backup: SceneBackups/puzzles_ia/.
///
/// Tools > IA > Puzzles no padrão da Física
/// </summary>
public static class IAPuzzleLayoutBuilder
{
    private const string ScenePath = "Assets/Scenes/IA.unity";
    private const string Kit = "Assets/Images/EditorKit/";
    private const string Categorias = Kit + "icones/categorias/";
    private const string GuardaSprite = "Assets/Art/Personagens/NPC_IA/guarda_visor_idle_01.png";
    private const string PlayerSprite = "Assets/Anim/Player/Visor/idle/player_visor_idle_01.png";

    private const string CorTy = "#4FD1C5";
    private const string CorMu = "#5D6B84";
    private static readonly Color CorTexto = new Color32(0xDC, 0xE6, 0xF5, 0xFF);
    private static readonly Color CorRotulo = new Color32(0x9A, 0xA6, 0xBA, 0xFF);
    private static readonly Color CorNumero = new Color32(0x4B, 0x5A, 0x75, 0xFF);
    private static readonly Color CorDourado = new Color32(0xF5, 0xC5, 0x42, 0xFF);
    private static readonly Color CorTitulo = new Color32(0x9F, 0xC2, 0xFF, 0xFF);

    private static readonly Dictionary<string, Color> CoresTipo = new Dictionary<string, Color>
    {
        { "rosa", new Color32(244, 114, 182, 255) },
        { "anil", new Color32(129, 140, 248, 255) },
        { "azul", new Color32(61, 165, 255, 255) },
        { "lima", new Color32(163, 230, 53, 255) },
    };

    private static readonly Dictionary<string, string> MolduraTipo = new Dictionary<string, string>
    {
        { "rosa", "peca_input" },
        { "anil", "peca_vector2" },
        { "azul", "peca_transform" },
        { "lima", "peca_update" },
    };

    // Mesmo layout da Física (unidades de mundo, relativo ao centro do fundo).
    private static readonly Rect JanelaEsq = Rect.MinMaxRect(-8.55f, -2.63f, 3.35f, 4.27f);
    private static readonly Rect JanelaPecas = Rect.MinMaxRect(3.6f, -2.63f, 8.65f, 4.27f);
    private static readonly Rect AreaCodigo = Rect.MinMaxRect(-8.25f, -2.35f, -0.95f, 3.72f);
    private static readonly Rect AreaPrevia = Rect.MinMaxRect(-0.7f, -2.35f, 3.05f, 3.72f);
    private const float LarguraPeca = 4.1f;
    private const float AlturaPeca = 0.64f;
    private const float PassoPeca = 0.76f;
    private const float LarguraLacuna = 3.7f;
    private static readonly Vector2 FundoReferencia = new Vector2(0.21f, 0.18f);

    // ------------------------------------------------------------------
    // Conteúdo
    // ------------------------------------------------------------------
    private class Estilo
    {
        public string cor, etiqueta, icone;
        public Estilo(string cor, string etiqueta, string icone) { this.cor = cor; this.etiqueta = etiqueta; this.icone = icone; }
    }

    // Peça (chave do IAPuzzleRigBuilder) -> cor do tipo, etiqueta e ícone.
    private static readonly Dictionary<string, Estilo> Estilos = new Dictionary<string, Estilo>
    {
        { "irentre", new Estilo("azul", "ação", "horizontal") },
        { "moverate", new Estilo("azul", "ação", "velocity") },
        { "causardano", new Estilo("azul", "ação", "dano") },
        { "pular", new Estilo("azul", "ação", "jump") },
        { "destroy", new Estilo("azul", "ação", "destroy") },
        { "inputgetaxis", new Estilo("rosa", "entrada", "input") },
        { "dist_menor_raiovisao", new Estilo("anil", "condição", "vector2") },
        { "dist_maior_raioperder", new Estilo("anil", "condição", "vector2") },
        { "dist_menor_alcance", new Estilo("anil", "condição", "vector2") },
        { "dist_maior_alcance", new Estilo("anil", "condição", "vector2") },
        { "oncollisionenter2d", new Estilo("rosa", "evento", "oncollisionenter2d") },
        { "start", new Estilo("lima", "método", "start") },
        { "raycast", new Estilo("anil", "condição", "raycast") },
        { "ontriggerenter2d", new Estilo("rosa", "evento", "ontriggerenter2d") },
        { "raiovisao_maior", new Estilo("anil", "condição", "vector2") },
        { "fixa_regra1", new Estilo("anil", "condição", "vector2") },
        { "fixa_regra2", new Estilo("anil", "condição", "vector2") },
        { "fixa_regra3", new Estilo("anil", "condição", "vector2") },
        { "fixa_regra4", new Estilo("anil", "condição", "vector2") },
    };

    private class Info
    {
        public string grupo, nome, legenda, objetivo;
        public int ordem;
        // Depuração: o jogador tem que achar a regra errada, então nada
        // acende a linha dela (sem brilho da lacuna e sem faixa).
        public bool semDestaque;
        public string[] linhas;   // rótulo de cada linha (ordem dos filhos de Encaixar)
        public string[] dicas;    // dica de cada lacuna (SnapZone), na mesma ordem
    }

    private static string Seta(string de, string para) => $"<color={CorTy}>{de}</color> <color={CorMu}>-></color> <color={CorTy}>{para}</color>";

    private static Info[] Infos => new[]
    {
        new Info
        {
            grupo = "IA_Estados", nome = "Estados", ordem = 1,
            legenda = "CÉREBRO DO GUARDA · ESTADOS",
            objetivo = "o guarda patrulha, persegue e ataca",
            linhas = new[] { $"<color={CorTy}>Patrulhar</color>", $"<color={CorTy}>Perseguir</color>", $"<color={CorTy}>Atacar</color>" },
            dicas = new[]
            {
                "Patrulhar: o que o guarda faz enquanto não vê ninguém?",
                "Perseguir: como o guarda vai atrás do jogador?",
                "Atacar: o que o guarda faz quando alcança o jogador?",
            },
        },
        new Info
        {
            grupo = "IA_Transicoes", nome = "Transições", ordem = 2,
            legenda = "CÉREBRO DO GUARDA · TRANSIÇÕES",
            objetivo = "o guarda troca de estado sozinho",
            linhas = new[] { Seta("Patrulhar", "Perseguir"), Seta("Perseguir", "Patrulhar"), Seta("Perseguir", "Atacar"), Seta("Atacar", "Perseguir") },
            dicas = new[]
            {
                "Regra 1: o que faz o guarda começar a perseguir?",
                "Regra 2: quando o guarda desiste?",
                "Regra 3: quando dá pra atacar?",
                "Regra 4: quando ele para de atacar e volta a perseguir?",
            },
        },
        new Info
        {
            grupo = "IA_Depuracao", nome = "Depuração", ordem = 3, semDestaque = true,
            legenda = "CÉREBRO DO GUARDA · LINHA DE VISÃO",
            objetivo = "o guarda não vê através da parede",
            linhas = new[] { Seta("Patrulhar", "Perseguir"), Seta("Perseguir", "Patrulhar"), Seta("Perseguir", "Atacar"), Seta("Atacar", "Perseguir") },
            dicas = new[]
            {
                "Dica: lembre o que o guarda fez quando você estava atrás da parede. Qual das quatro regras deixou ele te ver? Troque a peça dessa regra.",
            },
        },
    };

    private static readonly string[] NomesOrdem = { "Estados", "Transições", "Depuração" };

    // ------------------------------------------------------------------
    // Estado da montagem
    // ------------------------------------------------------------------
    private static List<string> log;
    private static TMP_FontAsset fonteMono, fonteMonoForte, fonteCorpo, fonteCorpoForte;
    private static float larguraCharPorTamanho;
    private static string camada = "Puzzle";

    [MenuItem("Tools/IA/Puzzles no padrão da Física")]
    public static void Menu()
    {
        Aplicar(true);
        foreach (string l in log) Debug.Log("[IAPuzzleLayoutBuilder] " + l);
    }

    public static List<string> Aplicar(bool salvar)
    {
        log = new List<string>();
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            log.Add("ERRO: saia do Play Mode antes.");
            return log;
        }

        Scene cena = SceneManager.GetSceneByPath(ScenePath);
        bool abriAqui = false;
        if (!cena.isLoaded)
        {
            cena = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            abriAqui = true;
        }
        else if (cena.isDirty)
        {
            log.Add("ERRO: a cena IA tem mudanças não salvas. Salve ou descarte antes de rodar.");
            return log;
        }

        PrepararIcones();

        TemaLaboratorio tema = TemaLaboratorio.Atual;
        fonteMono = tema != null ? tema.mono : null;
        fonteMonoForte = tema != null && tema.monoForte != null ? tema.monoForte : fonteMono;
        fonteCorpo = tema != null ? tema.corpo : null;
        fonteCorpoForte = tema != null && tema.corpoForte != null ? tema.corpoForte : fonteCorpo;
        larguraCharPorTamanho = MedirCharMono();

        var defs = IAPuzzleRigBuilder.Definicoes;
        foreach (Info info in Infos)
        {
            GameObject grupo = cena.GetRootGameObjects().FirstOrDefault(g => g.name == info.grupo);
            var def = defs.FirstOrDefault(d => d.groupName == info.grupo);
            if (grupo == null || def == null)
            {
                log.Add($"AVISO: grupo '{info.grupo}' não encontrado.");
                continue;
            }

            bool ativo = grupo.activeSelf;
            grupo.SetActive(true);
            Montar(cena, grupo, info, def);
            grupo.SetActive(ativo);
        }

        EditorSceneManager.MarkSceneDirty(cena);
        if (!salvar)
        {
            log.Add("Cena IA montada sem salvar (prévia).");
            return log;
        }
        EditorSceneManager.SaveScene(cena);
        log.Add("Cena IA salva.");
        if (abriAqui) EditorSceneManager.CloseScene(cena, true);
        return log;
    }

    // Ícones novos (Tools/gerar_icones_ia.py) com a importação dos outros da pasta.
    private static void PrepararIcones()
    {
        AssetDatabase.Refresh();
        var modelo = AssetImporter.GetAtPath(Categorias + "icone_velocity_azul.png") as TextureImporter;
        foreach (string nome in new[] { "horizontal_azul", "jump_azul", "dano_azul", "destroy_azul", "raycast_anil" })
        {
            string path = Categorias + $"icone_{nome}.png";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null || modelo == null) continue;
            if (imp.textureType == TextureImporterType.Sprite && imp.spritePixelsPerUnit == modelo.spritePixelsPerUnit) continue;
            var cfg = new TextureImporterSettings();
            modelo.ReadTextureSettings(cfg);
            imp.SetTextureSettings(cfg);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.SaveAndReimport();
        }
    }

    private static float MedirCharMono()
    {
        var go = new GameObject("_medida");
        go.hideFlags = HideFlags.HideAndDontSave;
        var t = go.AddComponent<TextMeshPro>();
        t.font = fonteMono;
        t.fontSize = 10f;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.text = "0000000000";
        t.ForceMeshUpdate();
        float w = t.preferredWidth / 10f / 10f;
        Object.DestroyImmediate(go);
        return w > 0f ? w : 0.06f;
    }

    // ------------------------------------------------------------------
    // Um puzzle
    // ------------------------------------------------------------------
    private static void Montar(Scene cena, GameObject grupo, Info info, IAPuzzleRigBuilder.PuzzleDef def)
    {
        Transform cenario = grupo.transform.Find("CenarioPuzzle");
        SpriteRenderer fundo = cenario.Find("Fundo").GetComponent<SpriteRenderer>();
        camada = fundo.sortingLayerName;
        Vector2 o = (Vector2)fundo.bounds.center - FundoReferencia;

        foreach (string velho in new[] { "Cabecalho", "Cerebro" })
        {
            Transform t = grupo.transform.Find(velho);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        Transform card = cenario.Find("Card Encaixe");
        if (card != null) card.gameObject.SetActive(false);

        AjustarJanelas(cenario, o);
        Cabecalho(grupo.transform, info, o);

        var pecasPorId = def.pecas.ToDictionary(p => p.id, p => p);

        // --- Cérebro (regras com lacunas) ---
        var raiz = NovoFilho(grupo.transform, "Cerebro");
        var lacunas = new List<CodigoEncaixeUI.Lacuna>();
        var linhas = new List<CodigoEncaixeUI.Linha>();
        Cerebro(grupo.transform, raiz, info, def, pecasPorId, Desloca(AreaCodigo, o), lacunas, linhas);

        // --- Peças ---
        Pecas(grupo.transform, def, o);

        // --- Prévia e progresso ---
        var (pontos, textoProg) = Previa(cenario, info, def, o, lacunas.Count);

        // --- Dica ---
        InfoPanelUI painel = cenario.Find("Descrição")?.GetComponent<InfoPanelUI>();
        if (painel != null) Dica(painel, info.dicas[0]);

        // --- Controlador visual ---
        var ui = raiz.gameObject.AddComponent<CodigoEncaixeUI>();
        var uso = new SerializedObject(ui);
        var pLac = uso.FindProperty("lacunas");
        pLac.arraySize = lacunas.Count;
        for (int i = 0; i < lacunas.Count; i++)
        {
            var e = pLac.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("zona").objectReferenceValue = lacunas[i].zona;
            e.FindPropertyRelative("destaque").objectReferenceValue = lacunas[i].destaque;
            e.FindPropertyRelative("linha").intValue = lacunas[i].linha;
            e.FindPropertyRelative("dica").stringValue = lacunas[i].dica;
        }
        var pLin = uso.FindProperty("linhas");
        pLin.arraySize = linhas.Count;
        for (int i = 0; i < linhas.Count; i++)
        {
            var e = pLin.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("faixaAtual").objectReferenceValue = linhas[i].faixaAtual;
            e.FindPropertyRelative("marcaOk").objectReferenceValue = linhas[i].marcaOk;
        }
        var pPon = uso.FindProperty("pontos");
        pPon.arraySize = pontos.Count;
        for (int i = 0; i < pontos.Count; i++) pPon.GetArrayElementAtIndex(i).objectReferenceValue = pontos[i];
        uso.FindProperty("textoProgresso").objectReferenceValue = textoProg;
        uso.FindProperty("painel").objectReferenceValue = painel;
        uso.ApplyModifiedPropertiesWithoutUndo();

        // Estado salvo = começo do puzzle (1ª lacuna acesa).
        for (int i = 0; i < lacunas.Count; i++) if (lacunas[i].destaque != null) lacunas[i].destaque.SetActive(i == 0);
        for (int i = 0; i < linhas.Count; i++)
        {
            if (linhas[i].faixaAtual != null) linhas[i].faixaAtual.SetActive(lacunas.Count > 0 && lacunas[0].linha == i);
            if (linhas[i].marcaOk != null) linhas[i].marcaOk.SetActive(false);
        }

        // Código montado (o quadro do fim do puzzle) = o código real do GuardaIA.
        PuzzleControl controle = Object.FindObjectsByType<PuzzleControl>(FindObjectsInactive.Include)
            .FirstOrDefault(c => c.name == def.controlName && c.gameObject.scene == cena);
        if (controle != null)
        {
            var cso = new SerializedObject(controle);
            cso.FindProperty("tituloCodigo").stringValue = def.tituloCodigo;
            cso.FindProperty("codigoMontado").stringValue = def.codigo;
            cso.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            log.Add($"AVISO: {def.controlName} não encontrado.");
        }

        log.Add($"{info.grupo}: {lacunas.Count} lacuna(s), {linhas.Count} linhas, {def.pecas.Length} peças.");
    }

    // ------------------------------------------------------------------
    // Janelas, abas e cabeçalho (iguais aos da Física)
    // ------------------------------------------------------------------
    private static void AjustarJanelas(Transform cenario, Vector2 o)
    {
        Transform painel = cenario.Find("Painel");
        RedimensionarSprite(painel.Find("painel_1").GetComponent<SpriteRenderer>(), Desloca(JanelaEsq, o));
        Aba(painel.Find("aba_ativa"), "Guarda", new Vector2(JanelaEsq.xMin + 0.2f, JanelaEsq.yMax) + o, 1.4f);

        Transform painel2 = cenario.Find("Painel_2");
        SpriteRenderer janela2 = painel2.GetComponentsInChildren<SpriteRenderer>(true).First(s => s.name.StartsWith("painel_1"));
        RedimensionarSprite(janela2, Desloca(JanelaPecas, o));
        Aba(painel2.Find("aba_ativa"), "Peças", new Vector2(JanelaPecas.xMin + 0.2f, JanelaPecas.yMax) + o, 1.4f);

        Transform desc = cenario.Find("Descrição");
        Transform abaDesc = desc != null ? desc.Find("aba_ativa") : null;
        if (abaDesc != null) RotuloAba(abaDesc, "Dica");
    }

    private static void Aba(Transform aba, string texto, Vector2 cantoSupEsq, float largura)
    {
        if (aba == null) return;
        var sr = aba.GetComponent<SpriteRenderer>();
        float altura = sr.bounds.size.y;
        RedimensionarSprite(sr, new Rect(cantoSupEsq.x, cantoSupEsq.y - altura - 0.02f, largura, altura));
        RotuloAba(aba, texto);
    }

    private static void RotuloAba(Transform aba, string texto)
    {
        Transform velho = aba.Find("Rotulo");
        if (velho != null) Object.DestroyImmediate(velho.gameObject);
        var sr = aba.GetComponent<SpriteRenderer>();
        Bounds b = sr.bounds;
        var t = Texto(aba, "Rotulo", texto, new Vector2(b.min.x + 0.14f, b.center.y), 0f, fonteCorpoForte, Color.white, TextAlignmentOptions.Left, sr.sortingOrder + 1);
        AjustarAltura(t, 0.19f);
        ManterEscalaUm(t.transform);
    }

    private static void Cabecalho(Transform grupo, Info info, Vector2 o)
    {
        var raiz = NovoFilho(grupo, "Cabecalho");
        float y = 4.62f + o.y;
        var titulo = Texto(raiz, "Titulo", $"<b>Fase IA</b>  <color=#8A97AD>Puzzle {info.ordem} de 3 · {info.nome}</color>",
            new Vector2(-7.45f + o.x, y), 0f, fonteCorpo, Color.white, TextAlignmentOptions.Left, 10);
        AjustarAltura(titulo, 0.3f);

        var partes = new List<string>();
        for (int i = 0; i < NomesOrdem.Length; i++)
        {
            string n = $"{i + 1}. {NomesOrdem[i]}";
            if (i + 1 < info.ordem) partes.Add($"<color=#7EE2A8>{n}</color>");
            else if (i + 1 == info.ordem) partes.Add($"<color=#FFFFFF><b>{n}</b></color>");
            else partes.Add($"<color=#5D6B84>{n}</color>");
        }
        var passos = Texto(raiz, "Passos", string.Join("   ", partes), new Vector2(8.6f + o.x, y), 0f, fonteCorpo, Color.white, TextAlignmentOptions.Right, 10);
        AjustarAltura(passos, 0.22f);
    }

    // ------------------------------------------------------------------
    // Cérebro: uma linha por regra, rótulo à esquerda e lacuna à direita
    // ------------------------------------------------------------------
    private static void Cerebro(Transform grupo, Transform raiz, Info info, IAPuzzleRigBuilder.PuzzleDef def,
        Dictionary<int, IAPuzzleRigBuilder.PecaDef> pecasPorId, Rect area,
        List<CodigoEncaixeUI.Lacuna> lacunas, List<CodigoEncaixeUI.Linha> linhas)
    {
        Spr(raiz, "Fundo", Carregar(Kit + "cenario/janela_previa.png"), area.center, area.size, 1, Color.white);
        Legenda(raiz, info.legenda, area);

        // Retrato do guarda ao lado da legenda (o mesmo do jogo).
        Sprite guarda = Carregar(GuardaSprite);
        if (guarda != null)
        {
            float lado = 0.62f * 1.5f / 1.02f;   // quadro de 384 px com corpo de 262
            Spr(raiz, "Guarda", guarda, new Vector2(area.xMax - 0.5f, area.yMax - 0.42f), new Vector2(lado, lado), 3, Color.white);
        }

        Transform encaixar = grupo.Find("Encaixar");
        var filhos = new List<Transform>();
        foreach (Transform t in encaixar) filhos.Add(t);
        int n = filhos.Count;

        const float topo = 0.9f;
        float passo = Mathf.Min(1.05f, (area.height - topo - 0.3f) / Mathf.Max(1, n));
        float margemNum = 0.62f;
        float xLacunaCentro = area.xMax - 0.2f - LarguraLacuna * 0.5f;
        float xRotulo = area.xMin + margemNum + 0.18f;
        float larguraRotulo = (xLacunaCentro - LarguraLacuna * 0.5f - 0.2f) - xRotulo;

        int maiorRotulo = info.linhas.Max(l => System.Text.RegularExpressions.Regex.Replace(l, "<[^>]+>", "").Length);
        float cw = Mathf.Min(0.115f, larguraRotulo / Mathf.Max(1, maiorRotulo));
        float fonte = cw / larguraCharPorTamanho;

        Spr(raiz, "Margem", Carregar(Kit + "cenario/pixel_branco.png"),
            new Vector2(area.xMin + margemNum, area.center.y - topo * 0.5f + 0.05f), new Vector2(0.015f, area.height - topo - 0.2f), 2, new Color32(0x1E, 0x2A, 0x3E, 0xFF));

        int lacunaIdx = 0;
        for (int i = 0; i < n; i++)
        {
            float y = area.yMax - topo - passo * (i + 0.5f);
            Vector2 centroLacuna = new Vector2(xLacunaCentro, y);
            Vector2 tamLacuna = new Vector2(LarguraLacuna, AlturaPeca);

            var linhaT = NovoFilho(raiz, $"Linha {i + 1}");
            var num = Texto(linhaT, "Numero", (i + 1).ToString(), new Vector2(area.xMin + margemNum - 0.1f, y), fonte * 0.85f, fonteMono, CorNumero, TextAlignmentOptions.Right, 4);
            num.transform.position = new Vector3(area.xMin + margemNum - 0.1f, y, 0f);
            Texto(linhaT, "Rotulo", i < info.linhas.Length ? info.linhas[i] : "", new Vector2(xRotulo, y), fonte, fonteMono, CorTexto, TextAlignmentOptions.Left, 4);

            var infoLinha = new CodigoEncaixeUI.Linha
            {
                faixaAtual = info.semDestaque ? null : Faixa(linhaT, new Rect(area.xMin + 0.05f, y - passo * 0.45f, area.width - 0.1f, passo * 0.9f)),
                marcaOk = MarcaOk(linhaT, new Vector2(area.xMin + 0.22f, y), 0.26f),
            };
            linhas.Add(infoLinha);

            Transform alvo = filhos[i];
            SnapZone zona = alvo.GetComponent<SnapZone>();
            if (zona != null)
            {
                pecasPorId.TryGetValue(zona.correctPieceID, out var certa);
                MontarLacuna(zona, centroLacuna, tamLacuna, certa, fonte);
                lacunas.Add(new CodigoEncaixeUI.Lacuna
                {
                    zona = zona,
                    destaque = info.semDestaque ? null : zona.transform.Find("Destaque").gameObject,
                    linha = i,
                    dica = lacunaIdx < info.dicas.Length ? info.dicas[lacunaIdx] : info.dicas[0],
                });
                lacunaIdx++;
            }
            else
            {
                // Regra já montada (Depuração 2-4): só a peça fixa.
                MontarRegraFixa(alvo, centroLacuna, tamLacuna, def);
            }
        }
    }

    private static void MontarLacuna(SnapZone zona, Vector2 centro, Vector2 tamanho, IAPuzzleRigBuilder.PecaDef certa, float fonte)
    {
        Transform z = zona.transform;
        IAPecaFixa errada = z.GetComponentsInChildren<IAPecaFixa>(true).FirstOrDefault();

        // Tira o visual antigo (e o texto "Patrulhar: ___"), menos a peça errada da Depuração.
        for (int c = z.childCount - 1; c >= 0; c--)
        {
            Transform f = z.GetChild(c);
            if (errada != null && f == errada.transform) continue;
            Object.DestroyImmediate(f.gameObject);
        }

        z.rotation = Quaternion.identity;
        z.position = new Vector3(centro.x, centro.y, z.position.z);
        ManterEscalaUm(z);

        // 1º filho = visual (é o que a SnapZone usa como âncora).
        var fundo = Spr(z, "Fundo", Carregar(Kit + "pecas/encaixe_vazio.png"), centro, tamanho, 3, new Color(0.75f, 0.8f, 0.9f, 0.9f));
        fundo.transform.SetSiblingIndex(0);
        var destaque = Spr(z, "Destaque", Carregar(Kit + "pecas/peca_contorno_selecao.png"), centro, tamanho + new Vector2(0.12f, 0.1f), 4, new Color(1f, 1f, 1f, 0.95f));
        destaque.transform.SetSiblingIndex(1);
        destaque.gameObject.SetActive(false);

        // A palavra certa já escrita, que aparece no lugar da lacuna.
        var pre = NovoFilho(z, "Preenchido");
        pre.SetSiblingIndex(2);
        if (certa != null) VisualPecaEncaixada(pre, certa, centro, tamanho, 5, CorDourado);
        pre.gameObject.SetActive(false);

        var so = new SerializedObject(zona);
        so.FindProperty("preenchido").objectReferenceValue = pre.gameObject;
        so.FindProperty("ajustarAosVizinhos").boolValue = false;
        so.FindProperty("folgaGatilho").vector2Value = new Vector2(0.35f, 0.18f);
        so.ApplyModifiedPropertiesWithoutUndo();

        var box = zona.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.isTrigger = true;
            box.offset = Vector2.zero;
            box.size = tamanho + new Vector2(0.7f, 0.36f);
        }

        if (errada != null)
        {
            // Regra errada da Depuração: fica por cima da lacuna até a certa entrar.
            errada.name = "DicaCodigo";
            errada.transform.SetAsLastSibling();
            ReestilizarFixa(errada, centro, tamanho);
        }
    }

    private static void MontarRegraFixa(Transform regra, Vector2 centro, Vector2 tamanho, IAPuzzleRigBuilder.PuzzleDef def)
    {
        IAPecaFixa fixa = regra.GetComponentsInChildren<IAPecaFixa>(true).FirstOrDefault();
        for (int c = regra.childCount - 1; c >= 0; c--)
        {
            Transform f = regra.GetChild(c);
            if (fixa != null && f == fixa.transform) continue;
            Object.DestroyImmediate(f.gameObject);
        }
        regra.rotation = Quaternion.identity;
        regra.position = new Vector3(centro.x, centro.y, regra.position.z);
        ManterEscalaUm(regra);
        var fundo = Spr(regra, "Fundo", Carregar(Kit + "pecas/encaixe_vazio.png"), centro, tamanho, 3, new Color(0.75f, 0.8f, 0.9f, 0.9f));
        fundo.transform.SetSiblingIndex(0);
        if (fixa != null) ReestilizarFixa(fixa, centro, tamanho);
        else log.Add($"AVISO: {regra.name} sem peça fixa.");
    }

    // Peça "já encaixada" (IAPecaFixa): mesmo desenho do Preenchido.
    private static void ReestilizarFixa(IAPecaFixa fixa, Vector2 centro, Vector2 tamanho)
    {
        string chave = fixa.name.StartsWith("Peca_") ? fixa.name.Substring(5) : null;
        var so = new SerializedObject(fixa);
        string titulo = so.FindProperty("titulo").stringValue;
        IAPuzzleRigBuilder.PecaDef p = AcharFixa(chave, titulo);

        Transform t = fixa.transform;
        for (int c = t.childCount - 1; c >= 0; c--) Object.DestroyImmediate(t.GetChild(c).gameObject);
        t.rotation = Quaternion.identity;
        t.position = new Vector3(centro.x, centro.y, t.position.z);
        ManterEscalaUm(t);

        var (fundo, icone) = p != null ? VisualPecaEncaixada(t, p, centro, tamanho, 5, CorDourado) : (null, null);

        so.FindProperty("fundo").objectReferenceValue = fundo;
        if (icone != null) so.FindProperty("icone").objectReferenceValue = icone;
        so.ApplyModifiedPropertiesWithoutUndo();

        var box = fixa.GetComponent<BoxCollider2D>();
        if (box == null) box = fixa.gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = false;
        box.offset = Vector2.zero;
        Vector3 l = t.lossyScale;
        box.size = new Vector2(tamanho.x / NaoZero(l.x), tamanho.y / NaoZero(l.y));
    }

    private static IAPuzzleRigBuilder.PecaDef AcharFixa(string chave, string titulo)
    {
        var dep = IAPuzzleRigBuilder.Definicoes.First(d => d.depuracao);
        if (chave != null)
        {
            var porChave = dep.fixas.FirstOrDefault(f => f.key == chave);
            if (porChave != null) return porChave;
        }
        // A regra errada (DicaCodigo) é a regra 1.
        return dep.fixas.FirstOrDefault(f => titulo != null && titulo.Contains(f.rotulo)) ?? dep.fixas[0];
    }

    // Moldura neutra + ícone + nome (dourado), como no Preenchido da Física.
    private static (SpriteRenderer, Sprite) VisualPecaEncaixada(Transform pai, IAPuzzleRigBuilder.PecaDef p, Vector2 centro, Vector2 tamanho, int ordem, Color corTexto)
    {
        Estilo e = EstiloDe(p.key);
        var fundo = Spr(pai, "Fundo", Carregar(Kit + "pecas/peca_velocity.png"), centro, tamanho, ordem, Color.white);
        float ic = tamanho.y * 0.62f;
        Sprite icone = Carregar(Categorias + $"icone_{e.icone}_{e.cor}.png");
        Spr(pai, "icone", icone, new Vector2(centro.x - tamanho.x * 0.5f + 0.16f + ic * 0.5f, centro.y), new Vector2(ic, ic), ordem + 1, Color.white);
        float cwPeca = 0.1f;
        var t = Texto(pai, "Texto", $"<b><noparse>{p.rotulo}</noparse></b>", new Vector2(centro.x - tamanho.x * 0.5f + 0.28f + ic, centro.y), cwPeca / larguraCharPorTamanho, fonteMonoForte, corTexto, TextAlignmentOptions.Left, ordem + 1);
        float livre = tamanho.x - (0.45f + ic);
        if (t.preferredWidth > livre) t.fontSize *= livre / t.preferredWidth;
        return (fundo, icone);
    }

    private static Estilo EstiloDe(string chave)
    {
        if (chave != null && Estilos.TryGetValue(chave, out Estilo e)) return e;
        log.Add($"AVISO: sem estilo para a peça '{chave}'.");
        return new Estilo("anil", "peça", "vector2");
    }

    // ------------------------------------------------------------------
    // Peças (bandeja)
    // ------------------------------------------------------------------
    private static void Pecas(Transform grupo, IAPuzzleRigBuilder.PuzzleDef def, Vector2 o)
    {
        Transform raiz = grupo.Find("Peças");
        if (raiz == null)
        {
            log.Add($"ERRO: {grupo.name} sem 'Peças'.");
            return;
        }

        var arrastaveis = raiz.GetComponentsInChildren<DragPiece>(true).ToList();
        Rect janela = Desloca(JanelaPecas, o);
        float centroY = janela.center.y - 0.15f;
        int n = arrastaveis.Count;
        float topoY = centroY + PassoPeca * (n - 1) * 0.5f;
        float xCentro = janela.center.x + 0.12f;
        float fontePeca = 0.112f / larguraCharPorTamanho;

        for (int i = 0; i < n; i++)
        {
            DragPiece drag = arrastaveis[i];
            var p = def.pecas.FirstOrDefault(d => d.id == drag.pieceID);
            if (p == null)
            {
                log.Add($"AVISO: {grupo.name}: peça {drag.name} (id {drag.pieceID}) sem definição.");
                continue;
            }
            Estilo e = EstiloDe(p.key);
            Vector2 c = new Vector2(xCentro, topoY - PassoPeca * i);

            Transform go = drag.transform;
            LimparFilhos(go);
            go.rotation = Quaternion.identity;
            go.position = new Vector3(c.x, c.y, go.position.z);
            ManterEscalaUm(go);

            Color cor = CoresTipo[e.cor];
            // 1º filho com SpriteRenderer = visual (DragPiece usa pra encaixar).
            Spr(go, "Fundo", Carregar(Kit + "pecas/" + MolduraTipo[e.cor] + ".png"), c, new Vector2(LarguraPeca, AlturaPeca), 8, Color.white);
            float ic = AlturaPeca * 0.66f;
            float xIc = c.x - LarguraPeca * 0.5f + 0.32f + ic * 0.5f;
            Sprite icone = Carregar(Categorias + $"icone_{e.icone}_{e.cor}.png");
            Spr(go, "icone_" + e.icone, icone, new Vector2(xIc, c.y), new Vector2(ic, ic), 9, Color.white);

            var et = Texto(go, "Etiqueta", e.etiqueta.ToUpperInvariant(), new Vector2(c.x + LarguraPeca * 0.5f - 0.18f, c.y), 0f, fonteCorpoForte, cor, TextAlignmentOptions.Right, 10);
            et.transform.position = new Vector3(c.x + LarguraPeca * 0.5f - 0.18f, c.y, 0f);
            AjustarAltura(et, 0.13f);
            et.characterSpacing = 4f;
            et.ForceMeshUpdate();

            float xTexto = xIc + ic * 0.5f + 0.16f;
            float livre = (c.x + LarguraPeca * 0.5f - 0.3f - et.preferredWidth) - xTexto - 0.1f;
            bool duas = !string.IsNullOrEmpty(p.rotuloUnity);
            var rot = Texto(go, "Texto", $"<b><noparse>{p.rotulo}</noparse></b>", new Vector2(xTexto, duas ? c.y + 0.1f : c.y), fontePeca, fonteMonoForte, Color.white, TextAlignmentOptions.Left, 10);
            if (rot.preferredWidth > livre) rot.fontSize *= livre / rot.preferredWidth;
            if (duas)
            {
                var un = Texto(go, "Unity", $"<noparse>{p.rotuloUnity}</noparse>", new Vector2(xTexto, c.y - 0.16f), fontePeca * 0.7f, fonteMono, CorRotulo, TextAlignmentOptions.Left, 10);
                if (un.preferredWidth > livre) un.fontSize *= livre / un.preferredWidth;
            }

            var hover = drag.GetComponent<PieceHoverInfo>();
            if (hover != null)
            {
                var hso = new SerializedObject(hover);
                hso.FindProperty("icon").objectReferenceValue = icone;
                string titulo = string.IsNullOrEmpty(p.rotuloUnity) ? p.rotulo : $"{p.rotulo}  ({p.rotuloUnity})";
                hso.FindProperty("title").stringValue = "<noparse>" + titulo + "</noparse>";
                hso.ApplyModifiedPropertiesWithoutUndo();
            }

            var box = drag.GetComponent<BoxCollider2D>();
            if (box != null)
            {
                box.offset = Vector2.zero;
                box.size = new Vector2(LarguraPeca, AlturaPeca);
            }
        }
    }

    // ------------------------------------------------------------------
    // Prévia (Objetivo) + progresso
    // ------------------------------------------------------------------
    private static (List<SpriteRenderer>, TMP_Text) Previa(Transform cenario, Info info, IAPuzzleRigBuilder.PuzzleDef def, Vector2 o, int nLacunas)
    {
        Transform painel = cenario.Find("Painel_Previa");
        Rect area = Desloca(AreaPrevia, o);
        SpriteRenderer janela = painel.Find("janela_previa").GetComponent<SpriteRenderer>();
        RedimensionarSprite(janela, area);

        foreach (var velhoT in painel.GetComponentsInChildren<TMP_Text>(true).Where(x => x.transform.parent == painel).ToList())
        {
            Object.DestroyImmediate(velhoT.gameObject);
        }
        var titulo = Texto(painel, "Titulo", "<b>Objetivo</b>", new Vector2(area.xMin + 0.2f, area.yMax - 0.3f), 0f, fonteCorpoForte, CorTitulo, TextAlignmentOptions.Left, 3);
        AjustarAltura(titulo, 0.26f);
        var frase = Texto(painel, "Frase", info.objetivo, new Vector2(area.xMin + 0.2f, area.yMax - 0.68f), 0f, fonteCorpo, new Color32(0xC9, 0xD6, 0xEA, 0xFF), TextAlignmentOptions.Left, 3);
        AjustarAltura(frase, 0.21f);
        if (frase.preferredWidth > area.width - 0.4f) frase.fontSize *= (area.width - 0.4f) / frase.preferredWidth;
        Transform div = painel.Find("divisoria");
        if (div != null) RedimensionarSprite(div.GetComponent<SpriteRenderer>(), new Rect(area.xMin + 0.18f, area.yMax - 0.98f, area.width - 0.36f, 0.02f));

        Rect palco = Rect.MinMaxRect(area.xMin, area.yMin + 1.05f, area.xMax, area.yMax - 1.0f);
        MontarPalco(painel, janela, palco, def.preview);

        Transform velho = painel.Find("Progresso");
        if (velho != null) Object.DestroyImmediate(velho.gameObject);
        var prog = NovoFilho(painel, "Progresso");
        var pontos = new List<SpriteRenderer>();
        int n = Mathf.Max(1, nLacunas);
        float larg = area.width - 0.4f, gap = 0.08f;
        float w = (larg - gap * (n - 1)) / n;
        Sprite branco = Carregar(Kit + "pecas/caixa_icone.png");
        for (int i = 0; i < n; i++)
        {
            var sr = Spr(prog, $"Ponto {i + 1}", branco, new Vector2(area.xMin + 0.2f + w * 0.5f + (w + gap) * i, area.yMin + 0.78f), new Vector2(w, 0.07f), 6,
                new Color32(0x22, 0x30, 0x4A, 0xFF));
            pontos.Add(sr);
        }
        var texto = Texto(prog, "Texto", $"<b>0 de {n} encaixe{(n == 1 ? "" : "s")}.</b>", new Vector2(area.xMin + 0.2f, area.yMin + 0.5f), 0f, fonteCorpo, CorRotulo, TextAlignmentOptions.Left, 6);
        AjustarAltura(texto, 0.24f);
        var legenda = Texto(prog, "Legenda", "A prévia repete o objetivo em loop.", new Vector2(area.xMin + 0.2f, area.yMin + 0.24f), 0f, fonteCorpo, CorNumero, TextAlignmentOptions.Left, 6);
        AjustarAltura(legenda, 0.2f);
        return (pontos, texto);
    }

    // Mini-cena do IAPuzzlePreview: caixas dos estados em cima, guarda e
    // boneco no chão (e a parede + linha de visão na Depuração).
    private static void MontarPalco(Transform painel, SpriteRenderer janela, Rect palco, IAPuzzlePreview.Modo modo)
    {
        var mask = janela.GetComponent<SpriteMask>();
        if (mask == null) mask = janela.gameObject.AddComponent<SpriteMask>();
        mask.sprite = janela.sprite;

        Transform velho = painel.Find("Preview");
        if (velho != null) Object.DestroyImmediate(velho.gameObject);
        var cont = NovoFilho(painel, "Preview");

        Sprite caixa = Carregar(Kit + "pecas/peca_base_branca.png");
        Sprite chao = Carregar("Assets/Images/Packs/Tecno/platform_tile_mid.png");
        Sprite parede = Carregar("Assets/Images/Packs/Tecno/wall_tile_left.png");
        Sprite linhaSpr = Carregar(Kit + "cenario/divisoria.png");

        // Caixas de estado.
        string[] nomes = { "Patrulhar", "Perseguir", "Atacar" };
        float largCaixa = (palco.width - 0.5f) / 3f;
        float yCaixas = palco.yMax - 0.3f;
        var caixas = new SpriteRenderer[3];
        var textos = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            Vector2 pos = new Vector2(palco.center.x + (i - 1) * (largCaixa + 0.08f), yCaixas);
            caixas[i] = Spr(cont, "Estado_" + nomes[i], caixa, pos, new Vector2(largCaixa, 0.4f), 3, new Color(0.17f, 0.22f, 0.32f, 1f));
            var t = Texto(cont, "Texto_" + nomes[i], nomes[i], pos, 0f, fonteCorpoForte, new Color(0.55f, 0.6f, 0.71f, 1f), TextAlignmentOptions.Left, 4);
            t.alignment = TextAlignmentOptions.Center;
            t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            t.transform.position = new Vector3(pos.x, pos.y, 0f);
            AjustarAltura(t, 0.2f);
            if (t.preferredWidth > largCaixa - 0.1f) t.fontSize *= (largCaixa - 0.1f) / t.preferredWidth;
            textos[i] = t;
        }

        const float ator = 0.45f;
        float chaoTopo = palco.yMin + 0.4f;
        float yAtor = chaoTopo + ator * 0.5f;
        float xMin = palco.xMin + 0.35f;
        float xMax = palco.xMax - 0.35f;

        // Chão centralizado: mesma margem de 0.2 dos dois lados (a da borda
        // da janela e das barras de progresso); a largura do bloco se ajusta
        // para fechar a conta sem sobra à direita.
        float chaoMin = palco.xMin + 0.2f, chaoLarg = palco.width - 0.4f;
        int nChao = Mathf.Max(1, Mathf.RoundToInt(chaoLarg / 0.4f));
        float bloco = chaoLarg / nChao;
        for (int i = 0; i < nChao; i++)
        {
            var c = Spr(cont, "Chao", chao, new Vector2(chaoMin + bloco * (i + 0.5f), chaoTopo - 0.2f), new Vector2(bloco, 0.4f), 2, Color.white);
            c.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        SpriteRenderer paredeSr = null, linha = null;
        if (modo == IAPuzzlePreview.Modo.Raycast)
        {
            float xParede = xMin + 0.66f * (xMax - xMin);
            paredeSr = Spr(cont, "Parede", parede, new Vector2(xParede, chaoTopo + 0.3f), new Vector2(0.3f, 0.6f), 3, Color.white);
            paredeSr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            linha = Spr(cont, "LinhaDeVisao", linhaSpr, new Vector2(xMin, yAtor), new Vector2(1f, 0.04f), 5, Color.white);
            linha.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        // Guarda: quadro do visor (384 px, corpo de 262 px, pivô no centro do corpo).
        float ladoGuarda = ator * 1.5f / 1.0234f;
        var guarda = Spr(cont, "Guarda", Carregar(GuardaSprite), new Vector2(xMin + 0.3f, yAtor), new Vector2(ladoGuarda, ladoGuarda), 4, Color.white);
        guarda.transform.position = new Vector3(xMin + 0.3f, yAtor, 0f);
        guarda.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        // Boneco: visor parado, mesmo enquadramento do guarda.
        var boneco = Spr(cont, "Boneco", Carregar(PlayerSprite), new Vector2(xMax - 0.2f, yAtor), new Vector2(ladoGuarda, ladoGuarda), 4, Color.white);
        boneco.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

        var icone = Texto(cont, "Icone", "?", new Vector2(guarda.transform.position.x, yAtor + 0.42f), 0f, fonteCorpoForte, CorDourado, TextAlignmentOptions.Left, 6);
        icone.alignment = TextAlignmentOptions.Center;
        icone.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        icone.transform.position = new Vector3(guarda.transform.position.x, yAtor + 0.42f, 0f);
        AjustarAltura(icone, 0.2f);

        var previa = painel.GetComponent<IAPuzzlePreview>();
        if (previa == null) previa = painel.gameObject.AddComponent<IAPuzzlePreview>();
        var so = new SerializedObject(previa);
        so.FindProperty("modo").enumValueIndex = (int)modo;
        so.FindProperty("guarda").objectReferenceValue = guarda.transform;
        so.FindProperty("boneco").objectReferenceValue = boneco.transform;
        so.FindProperty("icone").objectReferenceValue = icone;
        so.FindProperty("parede").objectReferenceValue = paredeSr;
        so.FindProperty("linhaVisao").objectReferenceValue = linha;
        var pc = so.FindProperty("caixas");
        var pt = so.FindProperty("textosCaixas");
        pc.arraySize = 3;
        pt.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            pc.GetArrayElementAtIndex(i).objectReferenceValue = caixas[i];
            pt.GetArrayElementAtIndex(i).objectReferenceValue = textos[i];
        }
        so.FindProperty("xMin").floatValue = cont.InverseTransformPoint(new Vector3(xMin, 0f, 0f)).x;
        so.FindProperty("xMax").floatValue = cont.InverseTransformPoint(new Vector3(xMax, 0f, 0f)).x;
        so.FindProperty("chaoY").floatValue = cont.InverseTransformPoint(new Vector3(0f, yAtor, 0f)).y;
        so.FindProperty("offsetIcone").vector2Value = new Vector2(0f, 0.42f / NaoZero(cont.lossyScale.y));
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------
    // Dica (igual à da Física)
    // ------------------------------------------------------------------
    private static void Dica(InfoPanelUI painel, string primeiraDica)
    {
        Transform desc = painel.transform;
        Transform antigos = desc.Find("Text");
        if (antigos != null) antigos.gameObject.SetActive(false);
        foreach (string velho in new[] { "TituloDica", "TextoDica" })
        {
            Transform t = desc.Find(velho);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        SpriteRenderer janela = desc.GetComponentsInChildren<SpriteRenderer>(true).First(s => s.name.StartsWith("painel_1"));
        Bounds b = janela.bounds;
        float x = b.min.x + 1.75f;

        var titulo = Texto(desc, "TituloDica", "Dica", new Vector2(x, b.max.y - 0.5f), 0f, fonteCorpoForte, CorTitulo, TextAlignmentOptions.Left, 3);
        AjustarAltura(titulo, 0.24f);

        var corpo = Texto(desc, "TextoDica", primeiraDica, new Vector2(x, b.max.y - 0.85f), 0f, fonteCorpo, CorTexto, TextAlignmentOptions.Left, 3);
        AjustarAltura(corpo, 0.23f);
        corpo.textWrappingMode = TextWrappingModes.Normal;
        corpo.alignment = TextAlignmentOptions.TopLeft;
        corpo.rectTransform.pivot = new Vector2(0f, 1f);
        corpo.rectTransform.sizeDelta = new Vector2(b.max.x - 0.4f - x, b.size.y - 0.9f);
        corpo.transform.position = new Vector3(x, b.max.y - 0.78f, 0f);

        var so = new SerializedObject(painel);
        so.FindProperty("externalTitle").objectReferenceValue = titulo;
        so.FindProperty("externalDescription").objectReferenceValue = corpo;
        so.FindProperty("defaultTitle").stringValue = "Dica";
        so.FindProperty("defaultDescription").stringValue = primeiraDica;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------
    // Utilitários de desenho (os mesmos do FisicaCodigoBuilder)
    // ------------------------------------------------------------------
    private static Transform NovoFilho(Transform pai, string nome)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        ManterEscalaUm(go.transform);
        return go.transform;
    }

    private static void ManterEscalaUm(Transform t)
    {
        Vector3 pai = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(1f / NaoZero(pai.x), 1f / NaoZero(pai.y), 1f);
    }

    private static float NaoZero(float v) => Mathf.Abs(v) < 0.0001f ? 1f : v;

    private static SpriteRenderer Spr(Transform pai, string nome, Sprite sprite, Vector2 centro, Vector2 tamanho, int ordem, Color cor)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = cor;
        sr.sortingLayerName = camada;
        sr.sortingOrder = ordem;
        go.transform.position = new Vector3(centro.x, centro.y, 0f);
        Vector2 nativo = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
        Vector3 paiEsc = pai.lossyScale;
        go.transform.localScale = new Vector3(tamanho.x / nativo.x / NaoZero(paiEsc.x), tamanho.y / nativo.y / NaoZero(paiEsc.y), 1f);
        return sr;
    }

    private static void RedimensionarSprite(SpriteRenderer sr, Rect r)
    {
        if (sr == null) return;
        sr.drawMode = SpriteDrawMode.Simple;
        Vector2 atual = sr.bounds.size;
        Vector3 s = sr.transform.localScale;
        sr.transform.localScale = new Vector3(s.x * r.width / NaoZero(atual.x), s.y * r.height / NaoZero(atual.y), s.z);
        Vector3 delta = (Vector3)r.center - sr.bounds.center;
        sr.transform.position += new Vector3(delta.x, delta.y, 0f);
    }

    private static TextMeshPro Texto(Transform pai, string nome, string texto, Vector2 pos, float fonte, TMP_FontAsset font, Color cor, TextAlignmentOptions alinhamento, int ordem)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);
        ManterEscalaUm(go.transform);
        var t = go.AddComponent<TextMeshPro>();
        if (font != null) t.font = font;
        t.text = texto;
        t.fontSize = fonte > 0f ? fonte : 3f;
        t.color = cor;
        t.alignment = alinhamento == TextAlignmentOptions.Right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = true;
        var rt = t.rectTransform;
        rt.pivot = new Vector2(alinhamento == TextAlignmentOptions.Right ? 1f : 0f, 0.5f);
        rt.sizeDelta = new Vector2(10f, 1f);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        var mr = go.GetComponent<MeshRenderer>();
        mr.sortingLayerName = camada;
        mr.sortingOrder = ordem;
        t.ForceMeshUpdate();
        return t;
    }

    private static void AjustarAltura(TMP_Text t, float altura)
    {
        string guardado = t.text;
        t.text = "H";
        t.fontSize = 10f;
        t.ForceMeshUpdate();
        float h = t.textBounds.size.y;
        t.text = guardado;
        if (h > 0.0001f) t.fontSize = 10f * altura / h;
        t.ForceMeshUpdate();
    }

    private static void Legenda(Transform sec, string texto, Rect area)
    {
        var t = Texto(sec, "Legenda", texto, new Vector2(area.xMin + 0.2f, area.yMax - 0.25f), 0f, fonteCorpoForte, new Color32(0x6F, 0x7D, 0x95, 0xFF), TextAlignmentOptions.Left, 3);
        AjustarAltura(t, 0.19f);
        t.characterSpacing = 6f;
    }

    private static GameObject Faixa(Transform linha, Rect r)
    {
        var sr = Spr(linha, "FaixaAtual", Carregar(Kit + "cenario/pixel_branco.png"), r.center, r.size, 2, new Color(0.22f, 0.74f, 0.97f, 0.09f));
        sr.gameObject.SetActive(false);
        return sr.gameObject;
    }

    private static GameObject MarcaOk(Transform linha, Vector2 pos, float tamanho)
    {
        var sr = Spr(linha, "MarcaOk", Carregar(Kit + "icones/interface/icone_confirmar@2x.png"), pos, new Vector2(tamanho, tamanho), 4, new Color32(0x7E, 0xE2, 0xA8, 0xFF));
        sr.gameObject.SetActive(false);
        return sr.gameObject;
    }

    private static Rect Desloca(Rect r, Vector2 o) => new Rect(r.x + o.x, r.y + o.y, r.width, r.height);

    private static void LimparFilhos(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--) Object.DestroyImmediate(t.GetChild(i).gameObject);
    }

    private static Sprite Carregar(string caminho)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
        if (s == null) log.Add("AVISO: sprite não encontrado: " + caminho);
        return s;
    }
}
