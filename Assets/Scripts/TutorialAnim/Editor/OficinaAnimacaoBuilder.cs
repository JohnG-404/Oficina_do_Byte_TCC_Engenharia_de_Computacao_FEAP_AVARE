using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Monta o layout da "Oficina de Animação" na cena Tutorial_Anim (tema
/// Laboratório Tecno): fundo do laboratório, barra de rodadas, linha do
/// tempo com 5 encaixes numerados, monitor de prévia com os botões Tocar e
/// Dica, bandeja com 6 frames soltos e o painel "como jogar". Também
/// preenche as rodadas do AnimationSequencePuzzle e liga o HUD.
///
/// Apaga o layout antigo (encaixes cinza, sinal de igual, prévia vermelha e
/// bandeja roxa) e as cópias do DialogueBox que ninguém usa. Faça backup da
/// cena antes (SceneBackups/). Pode rodar de novo: ele refaz tudo.
/// </summary>
public static class OficinaAnimacaoBuilder
{
    private const string Pasta = "Assets/Anim/TutorialAnim/";

    // Layout (unidades de mundo; câmera ortográfica tamanho 5, 16:9 = 17,78 x 10).
    private const float EsqX = -8.33f, EsqLarg = 11.78f;
    private const float DirX = 3.95f, DirLarg = 4.38f;
    private const float LadoEncaixe = 2.0f, PassoEncaixe = 2.3f, EncaixeY = 1.8f;
    private const float LadoPeca = 1.55f, PassoPeca = 1.85f, PecaY = -1.42f;

    private static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out Color c); return c; }
    private static readonly Color Ink = Hex("#0A0E17"), Superficie = Hex("#151C2C"), Elevado = Hex("#1B2438"), Elevado2 = Hex("#243049");
    private static readonly Color Linha = Hex("#34425F"), Texto = Hex("#E8EEF9"), Suave = Hex("#9AA8C4"), Azul = Hex("#4A8CF7"), AzulHi = Hex("#7AAEFF");
    private static readonly Color Ciano = Hex("#5FD4E8"), Roxo = Hex("#9B7BF0"), Dourado = Hex("#F5C542"), Tela = Hex("#0B1424");

    private static Sprite sFundo, sBorda, sBordaGrossa, sCirculo, sGrade, sBranco;
    private static TMP_FontAsset fTitulo, fTexto, fMono;

    [MenuItem("Tools/Tutorial Animação/Montar Oficina de Animação")]
    public static void Montar()
    {
        var cena = EditorSceneManager.GetActiveScene();
        if (cena.name != "Tutorial_Anim")
        {
            Debug.LogError("[Oficina] Abra a cena Tutorial_Anim antes.");
            return;
        }

        CarregarAssets();

        // Procura só dentro da Tutorial_Anim: outra cena pode estar aberta junto no Editor.
        var puzzle = EmCena<AnimationSequencePuzzle>(cena).Count > 0 ? EmCena<AnimationSequencePuzzle>(cena)[0] : null;
        if (puzzle == null)
        {
            Debug.LogError("[Oficina] AnimationSequencePuzzle não encontrado na cena.");
            return;
        }

        Transform cenario = null;
        foreach (GameObject raiz in cena.GetRootGameObjects())
        {
            if (raiz.name == "Cenario")
            {
                cenario = raiz.transform;
            }
        }
        if (cenario == null)
        {
            cenario = new GameObject("Cenario").transform;
        }
        cenario.position = Vector3.zero;

        // --- limpa o layout antigo -------------------------------------
        for (int i = cenario.childCount - 1; i >= 0; i--)
        {
            Transform filho = cenario.GetChild(i);
            if (filho != puzzle.transform)
            {
                Object.DestroyImmediate(filho.gameObject);
            }
        }
        puzzle.transform.SetParent(cenario, false);
        puzzle.transform.localPosition = Vector3.zero;
        puzzle.gameObject.name = "Encaixe";
        for (int i = puzzle.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(puzzle.transform.GetChild(i).gameObject);
        }
        foreach (GameObject raiz in cena.GetRootGameObjects())
        {
            if (raiz.name == "Peças")
            {
                Object.DestroyImmediate(raiz);
            }
        }

        var hud = puzzle.GetComponent<OficinaAnimacaoHUD>() ?? puzzle.gameObject.AddComponent<OficinaAnimacaoHUD>();

        // Câmera: fundo no azul-marinho do tema e ajuste pra telas 4:3/16:10.
        foreach (Camera cam in EmCena<Camera>(cena))
        {
            cam.backgroundColor = Hex("#0E1320");
            cam.clearFlags = CameraClearFlags.SolidColor;
            if (cam.GetComponent<CameraOficinaAjuste>() == null)
            {
                cam.gameObject.AddComponent<CameraOficinaAjuste>();
            }
        }

        // --- fundo -------------------------------------------------------
        var fundo = NovoSprite(cenario, "Fundo", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Lab/bg_laboratorio.png"), -20);
        Cobrir(fundo, 17.9f, 10.1f);
        var veu = NovoSprite(cenario, "Veu", sBranco, -19);
        veu.drawMode = SpriteDrawMode.Sliced;
        veu.size = new Vector2(24f, 16f);
        veu.color = new Color(Hex("#0E1320").r, Hex("#0E1320").g, Hex("#0E1320").b, 0.78f);

        // --- barra do topo ------------------------------------------------
        var topo = NovoGrupo(cenario, "Topo", new Vector3(0, 4.3f, 0));
        Painel(topo, "Titulo", new Vector2(EsqX + 2.7f, 0), new Vector2(5.4f, 0.74f), Elevado, Ink, 0);
        TMP_Text titulo = NovoTexto(topo, "TextoTitulo", "Oficina de Animação", fTitulo, 0.44f, Texto, new Vector2(EsqX + 0.3f, 0), new Vector2(4.2f, 0.7f), TextAlignmentOptions.Left, 6);
        TMP_Text contagem = NovoTexto(topo, "Contagem", "1 / 5", fMono, 0.26f, Ciano, new Vector2(EsqX + 4.55f, 0), new Vector2(0.9f, 0.6f), TextAlignmentOptions.Right, 6);
        var barra = NovoGrupo(topo, "BarraRodadas", new Vector3(EsqX + 5.7f, 0, 0));

        // --- linha do tempo ----------------------------------------------
        var linha = NovoGrupo(cenario, "LinhaDoTempo", Vector3.zero);
        Painel(linha, "Painel", new Vector2(EsqX + EsqLarg / 2, 2.05f), new Vector2(EsqLarg, 3.4f), Elevado, Ink, 0);
        NovoTexto(linha, "Rotulo", "LINHA DO TEMPO", fMono, 0.2f, Suave, new Vector2(EsqX + 0.3f, 3.45f), new Vector2(3f, 0.4f), TextAlignmentOptions.Left, 6);
        TMP_Text tituloRodada = NovoTexto(linha, "TituloRodada", "Lâmpada acendendo", fTitulo, 0.3f, Ciano, new Vector2(EsqX + 2.55f, 3.47f), new Vector2(6f, 0.45f), TextAlignmentOptions.Left, 6);
        TMP_Text frames = NovoTexto(linha, "Frames", "5 frames", fMono, 0.2f, Suave, new Vector2(EsqX + EsqLarg - 0.3f, 3.45f), new Vector2(3f, 0.4f), TextAlignmentOptions.Right, 6);
        NovoTexto(linha, "Inicio", "INÍCIO", fMono, 0.17f, Suave, new Vector2(EsqX + 0.3f, 0.6f), new Vector2(2f, 0.3f), TextAlignmentOptions.Left, 6);
        NovoTexto(linha, "Fim", "FIM", fMono, 0.17f, Suave, new Vector2(EsqX + EsqLarg - 0.3f, 0.6f), new Vector2(2f, 0.3f), TextAlignmentOptions.Right, 6);
        var trilho = NovoSprite(linha, "Trilho", sBranco, 1);
        trilho.drawMode = SpriteDrawMode.Sliced;
        trilho.size = new Vector2(EsqLarg - 1.2f, 0.06f);
        trilho.color = Linha;
        trilho.transform.localPosition = new Vector3(EsqX + EsqLarg / 2, EncaixeY, 0);

        float centroEsq = EsqX + EsqLarg / 2;
        var slotsT = new Transform[5];
        var fundosSlot = new SpriteRenderer[5];
        var bordasSlot = new SpriteRenderer[5];
        var travados = new GameObject[5];
        var slotsRaiz = NovoGrupo(puzzle.transform, "Encaixes", Vector3.zero);
        for (int i = 0; i < 5; i++)
        {
            var s = NovoGrupo(slotsRaiz, $"Encaixe_{i + 1}", new Vector3(centroEsq + (i - 2) * PassoEncaixe, EncaixeY, 0));
            fundosSlot[i] = Fatiado(s, "Fundo", sFundo, new Vector2(LadoEncaixe, LadoEncaixe), Superficie, 2);
            bordasSlot[i] = Fatiado(s, "Borda", sBordaGrossa, new Vector2(LadoEncaixe, LadoEncaixe), Linha, 3);
            var selo = NovoSprite(s, "Numero", sCirculo, 4);
            Tamanho(selo, 0.5f);
            selo.color = Dourado;
            selo.transform.localPosition = new Vector3(-LadoEncaixe / 2 + 0.05f, LadoEncaixe / 2 - 0.05f, 0);
            var aro = NovoSprite(selo.transform, "Aro", sCirculo, 3);
            aro.color = Ink;
            aro.transform.localScale = Vector3.one * 1.18f;
            NovoTexto(selo.transform, "Texto", (i + 1).ToString(), fTitulo, 0.3f, Ink, Vector2.zero, new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center, 5, true);
            var trav = NovoTexto(s, "Travado", "travado", fMono, 0.15f, Roxo, new Vector2(LadoEncaixe / 2 - 0.12f, -LadoEncaixe / 2 + 0.2f), new Vector2(1.2f, 0.25f), TextAlignmentOptions.Right, 12);
            trav.gameObject.SetActive(false);
            travados[i] = trav.gameObject;
            slotsT[i] = s;
        }

        var fantasma = NovoSprite(slotsRaiz, "Fantasma", null, 9);
        fantasma.color = new Color(1, 1, 1, 0.28f);
        fantasma.enabled = false;

        // --- monitor de prévia -------------------------------------------
        var monitor = NovoGrupo(cenario, "Monitor", Vector3.zero);
        float cx = DirX + DirLarg / 2;
        Painel(monitor, "Painel", new Vector2(cx, 1.425f), new Vector2(DirLarg, 4.65f), Elevado, Ink, 0);
        NovoTexto(monitor, "Rotulo", "PRÉVIA", fMono, 0.2f, Suave, new Vector2(DirX + 0.3f, 3.45f), new Vector2(2f, 0.4f), TextAlignmentOptions.Left, 6);
        TMP_Text estado = NovoTexto(monitor, "Estado", "• parado", fMono, 0.2f, Suave, new Vector2(DirX + DirLarg - 0.3f, 3.45f), new Vector2(2f, 0.4f), TextAlignmentOptions.Right, 6);
        Painel(monitor, "Tela", new Vector2(cx, 1.6f), new Vector2(DirLarg - 0.5f, 3.2f), Tela, Ink, 2);
        var grade = NovoSprite(monitor, "Grade", sGrade, 3);
        grade.drawMode = SpriteDrawMode.Tiled;
        grade.size = new Vector2(DirLarg - 0.62f, 3.08f);
        grade.transform.localPosition = new Vector3(cx, 1.6f, 0);
        var previa = NovoSprite(monitor, "Previa", null, 8);
        previa.transform.localPosition = new Vector3(cx, 1.6f, 0);

        BotaoMundo(monitor, "BotaoTocar", "Tocar", new Vector2(DirX + 1.05f, -0.42f), new Vector2(1.5f, 0.62f), Azul, AzulHi, Color.white, puzzle.TocarPrevia);
        BotaoMundo(monitor, "BotaoDica", "Dica", new Vector2(DirX + 2.55f, -0.42f), new Vector2(1.3f, 0.62f), Elevado2, Hex("#2E3D5C"), Texto, puzzle.MostrarDica);
        TMP_Text fps = NovoTexto(monitor, "Fps", "8 FPS", fMono, 0.2f, Dourado, new Vector2(DirX + DirLarg - 0.3f, -0.42f), new Vector2(1.2f, 0.4f), TextAlignmentOptions.Right, 6);

        // --- bandeja -------------------------------------------------------
        var bandeja = NovoGrupo(cenario, "Bandeja", Vector3.zero);
        Painel(bandeja, "Painel", new Vector2(centroEsq, -1.2f), new Vector2(EsqLarg, 2.5f), Superficie, Ink, 0);
        NovoTexto(bandeja, "Rotulo", "FRAMES SOLTOS", fMono, 0.2f, Suave, new Vector2(EsqX + 0.3f, -0.25f), new Vector2(3f, 0.4f), TextAlignmentOptions.Left, 6);
        TMP_Text iscas = NovoTexto(bandeja, "Iscas", "1 não faz parte", fMono, 0.2f, Dourado, new Vector2(EsqX + EsqLarg - 0.3f, -0.25f), new Vector2(3.5f, 0.4f), TextAlignmentOptions.Right, 6);
        var pecas = new SequencePiece[6];
        for (int i = 0; i < 6; i++)
        {
            var tile = NovoGrupo(bandeja, $"Lugar_{i + 1}", new Vector3(centroEsq + (i - 2.5f) * PassoPeca, PecaY, 0));
            Fatiado(tile, "Fundo", sFundo, new Vector2(LadoPeca, LadoPeca), Elevado2, 2);
            Fatiado(tile, "Borda", sBordaGrossa, new Vector2(LadoPeca, LadoPeca), Roxo, 3);

            var go = new GameObject($"Frame_{i + 1}");
            go.transform.SetParent(bandeja, false);
            go.transform.position = new Vector3(tile.position.x, PecaY, -0.1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;
            go.AddComponent<BoxCollider2D>();
            pecas[i] = go.AddComponent<SequencePiece>();
        }

        // --- como jogar ----------------------------------------------------
        var como = NovoGrupo(cenario, "ComoJogar", Vector3.zero);
        Painel(como, "Painel", new Vector2(cx, -1.83f), new Vector2(DirLarg, 1.25f), Elevado, Ink, 0);
        TMP_Text comoTexto = NovoTexto(como, "Texto", "<b>Arraste ou clique</b>\nClique num frame encaixado para devolver.", fTexto, 0.2f, Texto, new Vector2(DirX + 0.3f, -1.83f), new Vector2(DirLarg - 0.6f, 1.05f), TextAlignmentOptions.Left, 6);
        comoTexto.textWrappingMode = TextWrappingModes.Normal;

        NovoTexto(cenario, "Rodape", "MOUSE  arrastar     CLIQUE  encaixar ou devolver     ESC  pausa", fMono, 0.17f, Suave, new Vector2(0, -4.62f), new Vector2(16f, 0.3f), TextAlignmentOptions.Center, 6);

        // --- liga o puzzle -------------------------------------------------
        var so = new SerializedObject(puzzle);
        SetArray(so.FindProperty("slots"), slotsT);
        SetArray(so.FindProperty("trayPieces"), pecas);
        so.FindProperty("previewRenderer").objectReferenceValue = previa;
        so.FindProperty("hud").objectReferenceValue = hud;
        so.FindProperty("fantasma").objectReferenceValue = fantasma;
        so.FindProperty("snapRadius").floatValue = 1.15f;
        so.FindProperty("tamanhoPecaBandeja").floatValue = 1.28f;
        so.FindProperty("tamanhoPecaEncaixe").floatValue = 1.62f;
        so.FindProperty("tamanhoPrevia").floatValue = 2.55f;
        so.FindProperty("previewLoopCount").intValue = 2;
        so.FindProperty("nextRoundLinePrefix").stringValue = "Boa! Próxima animação: ";
        SetStrings(so.FindProperty("introDialogue"),
            "Bem-vindo à Oficina de Animação! Uma animação quadro a quadro é uma sequência de imagens (sprites) trocadas bem rápido. Cada imagem é um frame.",
            "Os frames estão soltos na bandeja. Arraste cada um até a linha do tempo, do encaixe 1 ao último, ou só clique nele.",
            "Com a linha do tempo cheia, a animação toca no monitor. Cuidado: sempre tem frame que não faz parte.",
            "Travou? Clique em Dica.");
        SetStrings(so.FindProperty("wrongOrderDialogueStage1"), "Hmm, não foi dessa vez. Olhe a prévia de novo.");
        SetStrings(so.FindProperty("wrongOrderDialogueStage2"), "Quase! Pense no movimento: o que vem primeiro, e o que vem depois?");
        SetStrings(so.FindProperty("wrongOrderDialogueStage3"), "Procure o frame em que nada começou ainda: é por ele que a sequência começa.");
        PreencherRodadas(so.FindProperty("rounds"));
        so.ApplyModifiedPropertiesWithoutUndo();

        var sh = new SerializedObject(hud);
        sh.FindProperty("textoContagem").objectReferenceValue = contagem;
        sh.FindProperty("textoTituloRodada").objectReferenceValue = tituloRodada;
        sh.FindProperty("textoFrames").objectReferenceValue = frames;
        sh.FindProperty("textoIscas").objectReferenceValue = iscas;
        sh.FindProperty("textoEstadoPrevia").objectReferenceValue = estado;
        sh.FindProperty("textoFps").objectReferenceValue = fps;
        sh.FindProperty("textoComoJogar").objectReferenceValue = comoTexto;
        SetArray(sh.FindProperty("fundosEncaixe"), fundosSlot);
        SetArray(sh.FindProperty("bordasEncaixe"), bordasSlot);
        SetArray(sh.FindProperty("marcasTravado"), travados);
        sh.FindProperty("barraRodadas").objectReferenceValue = barra;
        sh.FindProperty("spriteFundo").objectReferenceValue = sFundo;
        sh.FindProperty("spriteBorda").objectReferenceValue = sBorda;
        sh.FindProperty("spriteCheck").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Sprites/icone_check.png");
        sh.FindProperty("fonteRodadas").objectReferenceValue = fTexto;
        sh.FindProperty("tamanhoFonteChip").floatValue = TamanhoFonte(fTexto, 0.24f);
        sh.FindProperty("fonteTituloAjuste").objectReferenceValue = fTitulo;
        sh.FindProperty("fonteTextoAjuste").objectReferenceValue = fTexto;
        sh.FindProperty("spriteArredondadoAjuste").objectReferenceValue = sFundo;
        sh.ApplyModifiedPropertiesWithoutUndo();

        RemoverDialogosSobrando(puzzle, cena);

        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        Debug.Log("[Oficina] Layout da Oficina de Animação montado e cena salva.");
    }

    // ---------------------------------------------------------------------
    // Rodadas
    // ---------------------------------------------------------------------

    private struct Rodada
    {
        public string nome, titulo, dica;
        public string[] certos, iscas;
        public bool travado;
        public int fps;
    }

    private static void PreencherRodadas(SerializedProperty prop)
    {
        const string V = "Assets/Anim/Player/Visor/";
        var lista = new[]
        {
            new Rodada { nome = "Lâmpada", titulo = "Lâmpada acendendo", fps = 8,
                certos = Seq(Pasta + "Lampada/lampada_", 1, 2, 3, 4, 5), iscas = new[] { Pasta + "Lampada/lampada_isca.png" },
                dica = "Repare no brilho da lâmpada em cada frame: ele muda aos poucos." },
            new Rodada { nome = "Porta", titulo = "Porta do laboratório abrindo", fps = 6,
                certos = Seq(Pasta + "Porta/porta_", 1, 2, 4, 5), iscas = new[] { Pasta + "Porta/porta_isca.png", Pasta + "Bateria/bateria_isca.png" },
                dica = "Repare na fresta no meio da porta e na cor da luz em cima dela." },
            new Rodada { nome = "Pulo", titulo = "Pulo do personagem", fps = 8,
                certos = Seq(Pasta + "Pulo/pulo_", 1, 2, 3, 4, 5), iscas = new[] { V + "hurt/player_visor_hurt_02.png" },
                dica = "Olhe a distância entre o personagem e o chão em cada frame, e a pose do corpo." },
            new Rodada { nome = "Corrida", titulo = "Corrida (loop)", fps = 12, travado = true,
                certos = new[] { V + "run/player_visor_run_01.png", V + "run/player_visor_run_02.png", V + "run/player_visor_run_03.png" },
                iscas = new[] { V + "idle/player_visor_idle_01.png", V + "crouch/player_visor_crouch_04.png" },
                dica = "É um loop e o frame 1 já está no lugar. Compare a altura do corpo e a posição dos pés em cada frame." },
            new Rodada { nome = "Alavanca", titulo = "Alavanca de ponta a ponta", fps = 8,
                certos = Seq(Pasta + "Alavanca/alavanca_", 1, 3, 5, 7, 9), iscas = new[] { Pasta + "Alavanca/alavanca_isca.png" },
                dica = "Repare na inclinação do cabo e na cor da luz em cada frame." },
        };

        prop.arraySize = lista.Length;
        for (int i = 0; i < lista.Length; i++)
        {
            SerializedProperty r = prop.GetArrayElementAtIndex(i);
            r.FindPropertyRelative("roundName").stringValue = lista[i].nome;
            r.FindPropertyRelative("titulo").stringValue = lista[i].titulo;
            r.FindPropertyRelative("dica").stringValue = lista[i].dica;
            r.FindPropertyRelative("primeiroFrameTravado").boolValue = lista[i].travado;
            r.FindPropertyRelative("fpsPrevia").intValue = lista[i].fps;
            SetArray(r.FindPropertyRelative("correctFrames"), Carregar(lista[i].certos));
            SetArray(r.FindPropertyRelative("decoyFrames"), Carregar(lista[i].iscas));
        }
    }

    private static string[] Seq(string prefixo, params int[] numeros)
    {
        var l = new string[numeros.Length];
        for (int i = 0; i < numeros.Length; i++)
        {
            l[i] = $"{prefixo}{numeros[i]:00}.png";
        }
        return l;
    }

    private static Sprite[] Carregar(string[] caminhos)
    {
        var l = new List<Sprite>();
        foreach (string c in caminhos)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(c);
            if (s == null)
            {
                Debug.LogError("[Oficina] Sprite não encontrado: " + c);
                continue;
            }
            l.Add(s);
        }
        return l.ToArray();
    }

    // ---------------------------------------------------------------------
    // DialogueBox repetidos
    // ---------------------------------------------------------------------

    private static List<T> EmCena<T>(UnityEngine.SceneManagement.Scene cena) where T : Component
    {
        var lista = new List<T>();
        foreach (GameObject raiz in cena.GetRootGameObjects())
        {
            lista.AddRange(raiz.GetComponentsInChildren<T>(true));
        }
        return lista;
    }

    private static void RemoverDialogosSobrando(AnimationSequencePuzzle puzzle, UnityEngine.SceneManagement.Scene cena)
    {
        var usado = new SerializedObject(puzzle).FindProperty("dialogueManager").objectReferenceValue as DialogueManager;
        if (usado == null)
        {
            return;
        }

        var referenciados = new HashSet<Object>();
        foreach (var mb in EmCena<MonoBehaviour>(cena))
        {
            if (mb == null || mb is DialogueManager)
            {
                continue;
            }
            var it = new SerializedObject(mb).GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue != null)
                {
                    referenciados.Add(it.objectReferenceValue);
                }
            }
        }

        foreach (var dm in EmCena<DialogueManager>(cena))
        {
            if (dm != usado && !referenciados.Contains(dm) && !referenciados.Contains(dm.gameObject) && !PrefabUtility.IsPartOfPrefabInstance(dm))
            {
                Debug.Log("[Oficina] Removendo DialogueBox repetido: " + dm.gameObject.name);
                Object.DestroyImmediate(dm.gameObject);
            }
        }
    }

    // ---------------------------------------------------------------------
    // Ajudantes de criação
    // ---------------------------------------------------------------------

    private static void CarregarAssets()
    {
        sFundo = AssetDatabase.LoadAssetAtPath<Sprite>(Pasta + "UI/oficina_fundo.png");
        sBorda = AssetDatabase.LoadAssetAtPath<Sprite>(Pasta + "UI/oficina_borda.png");
        sBordaGrossa = AssetDatabase.LoadAssetAtPath<Sprite>(Pasta + "UI/oficina_borda_grossa.png");
        sCirculo = AssetDatabase.LoadAssetAtPath<Sprite>(Pasta + "UI/oficina_circulo.png");
        sGrade = AssetDatabase.LoadAssetAtPath<Sprite>(Pasta + "UI/oficina_grade.png");
        sBranco = AssetDatabase.LoadAssetAtPath<Sprite>(Pasta + "UI/oficina_branco.png");
        fTitulo = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/UI/Fonts/Baloo2-ExtraBold SDF.asset");
        fTexto = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/UI/Fonts/Nunito-Bold SDF.asset");
        fMono = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/UI/Fonts/JetBrainsMono-Bold SDF.asset");
    }

    private static Transform NovoGrupo(Transform pai, string nome, Vector3 local)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        go.transform.localPosition = local;
        return go.transform;
    }

    private static SpriteRenderer NovoSprite(Transform pai, string nome, Sprite sprite, int ordem)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = ordem;
        return sr;
    }

    private static SpriteRenderer Fatiado(Transform pai, string nome, Sprite sprite, Vector2 tamanho, Color cor, int ordem)
    {
        var sr = NovoSprite(pai, nome, sprite, ordem);
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = tamanho;
        sr.color = cor;
        return sr;
    }

    private static void Painel(Transform pai, string nome, Vector2 centro, Vector2 tamanho, Color fundo, Color borda, int ordem)
    {
        var g = NovoGrupo(pai, nome, centro);
        Fatiado(g, "Fundo", sFundo, tamanho, fundo, ordem);
        Fatiado(g, "Borda", sBordaGrossa, tamanho, borda, ordem + 1);
    }

    private static void Tamanho(SpriteRenderer sr, float lado)
    {
        float maior = Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y);
        sr.transform.localScale = Vector3.one * (lado / maior);
    }

    private static void Cobrir(SpriteRenderer sr, float largura, float altura)
    {
        Vector2 b = sr.sprite.bounds.size;
        float e = Mathf.Max(largura / b.x, altura / b.y);
        sr.transform.localScale = new Vector3(e, e, 1f);
    }

    // Tamanho de fonte (TMP 3D) pra que a linha tenha "altura" unidades.
    private static float TamanhoFonte(TMP_FontAsset fonte, float altura)
    {
        var go = new GameObject("medida");
        var t = go.AddComponent<TextMeshPro>();
        t.font = fonte;
        t.fontSize = 10f;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        float h = t.GetPreferredValues("Ág").y;
        Object.DestroyImmediate(go);
        return h > 0.0001f ? 10f * altura / h : 2f;
    }

    private static TMP_Text NovoTexto(Transform pai, string nome, string texto, TMP_FontAsset fonte, float altura, Color cor, Vector2 pos, Vector2 caixa, TextAlignmentOptions alinhamento, int ordem, bool centrado = false)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        var t = go.AddComponent<TextMeshPro>();
        t.font = fonte;
        t.fontSize = TamanhoFonte(fonte, altura);
        t.text = texto;
        t.color = cor;
        t.alignment = alinhamento;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.sortingOrder = ordem;
        t.richText = true;
        var rt = t.rectTransform;
        rt.sizeDelta = caixa;
        // pivô no lado do alinhamento, pra "pos" ser a borda do texto
        float px = alinhamento == TextAlignmentOptions.Left || alinhamento == TextAlignmentOptions.TopLeft ? 0f
            : alinhamento == TextAlignmentOptions.Right ? 1f : 0.5f;
        rt.pivot = new Vector2(centrado ? 0.5f : px, 0.5f);
        if (pai.GetComponent<SpriteRenderer>() != null && pai.lossyScale.x != 0)
        {
            // dentro de um selo escalado: compensa a escala do pai
            t.transform.localScale = Vector3.one / pai.lossyScale.x;
        }
        // Sem RectTransform no pai, o que vai pra cena salva é o anchoredPosition.
        rt.anchoredPosition = pos;
        rt.localPosition = new Vector3(pos.x, pos.y, 0f);
        EditorUtility.SetDirty(rt);
        return t;
    }

    private static void BotaoMundo(Transform pai, string nome, string rotulo, Vector2 pos, Vector2 tamanho, Color cor, Color corHover, Color corTexto, UnityAction acao)
    {
        var g = NovoGrupo(pai, nome, pos);
        var fundo = Fatiado(g, "Fundo", sFundo, tamanho, cor, 4);
        Fatiado(g, "Borda", sBordaGrossa, tamanho, Ink, 5);
        NovoTexto(g, "Texto", rotulo, fTitulo, 0.3f, corTexto, Vector2.zero, tamanho, TextAlignmentOptions.Center, 6);
        var col = g.gameObject.AddComponent<BoxCollider2D>();
        col.size = tamanho;
        var botao = g.gameObject.AddComponent<global::BotaoMundo>();
        var sb = new SerializedObject(botao);
        sb.FindProperty("fundo").objectReferenceValue = fundo;
        sb.FindProperty("corNormal").colorValue = cor;
        sb.FindProperty("corHover").colorValue = corHover;
        sb.ApplyModifiedPropertiesWithoutUndo();

        var campo = typeof(global::BotaoMundo).GetField("aoClicar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var evento = (UnityEvent)campo.GetValue(botao);
        UnityEventTools.AddPersistentListener(evento, acao);
        EditorUtility.SetDirty(botao);
    }

    private static void SetArray<T>(SerializedProperty prop, T[] itens) where T : Object
    {
        prop.arraySize = itens.Length;
        for (int i = 0; i < itens.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = itens[i];
        }
    }

    private static void SetStrings(SerializedProperty prop, params string[] linhas)
    {
        prop.arraySize = linhas.Length;
        for (int i = 0; i < linhas.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).stringValue = linhas[i];
        }
    }
}
