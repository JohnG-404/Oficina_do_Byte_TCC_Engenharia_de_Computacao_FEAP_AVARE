using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Cria DO ZERO as cenas das fases de IA:
///
/// - Tools/IA/Criar Cena IA → Assets/Scenes/IA.unity (construção: laboratório
///   com o Guarda, pontos A e B, parede, passarela, saída trancada, jogador
///   com vida, e os 3 puzzles clonados do PuzzleRig da Fisica — ver
///   IAPuzzleRigBuilder).
/// - Tools/IA/Criar Cena IA_Aplicacao → Assets/Scenes/IA_Aplicacao.unity
///   (corredor com Guarda, plataforma pra se esconder, Aliado e saída; o
///   clique nos NPCs abre o diagrama do cérebro).
///
/// "Criar" não mexe numa cena que já existe (só avisa). "Recriar" apaga e
/// monta de novo, guardando antes uma cópia em &lt;projeto&gt;/SceneBackups/.
/// Nada é feito se alguma cena aberta tiver alterações não salvas.
///
/// Não adiciona as cenas ao Build Settings.
/// </summary>
public static class IASceneBuilder
{
    public const string CenaIAPath = "Assets/Scenes/IA.unity";
    public const string CenaAplicacaoPath = "Assets/Scenes/IA_Aplicacao.unity";

    private const string PlayerSpritePath = "Assets/Images/player/player_square_final.png";
    private const string GuardaSpritePath = "Assets/Images/player/player_skin_red.png";
    private const string AliadoSpritePath = "Assets/Images/player/player_skin_green.png";

    private static readonly Color CorFundoCamera = new Color(0.19215687f, 0.3019608f, 0.4745098f, 1f);
    // Mesmo enquadramento das outras fases: o topo do chão (y = 0) fica 0,65
    // acima da borda de baixo da tela, como o chão da Interacao em y = -4,35
    // com a câmera em 0.
    private const float AlturaCamera = 4.35f;
    // A câmera mostra ~0,7 além da borda interna da parede, como nas outras fases.
    private const float RecuoCamera = 8.2f;

    private static List<string> log;

    // ------------------------------------------------------------------
    // Menus
    // ------------------------------------------------------------------

    [MenuItem("Tools/IA/Criar Cena IA")]
    public static void CriarCenaIA() => Executar(CenaIAPath, false, MontarCenaIA);

    [MenuItem("Tools/IA/Recriar Cena IA (sobrescreve)")]
    public static void RecriarCenaIA() => Executar(CenaIAPath, true, MontarCenaIA);

    [MenuItem("Tools/IA/Criar Cena IA_Aplicacao")]
    public static void CriarCenaAplicacao() => Executar(CenaAplicacaoPath, false, MontarCenaAplicacao);

    [MenuItem("Tools/IA/Recriar Cena IA_Aplicacao (sobrescreve)")]
    public static void RecriarCenaAplicacao() => Executar(CenaAplicacaoPath, true, MontarCenaAplicacao);

    private static void Executar(string caminho, bool recriar, Action<Scene> montar)
    {
        log = new List<string>();
        string nome = Path.GetFileNameWithoutExtension(caminho);

        try
        {
            if (!recriar && File.Exists(CaminhoAbsoluto(caminho)))
            {
                log.Add($"AVISO: {caminho} já existe e NÃO foi alterada. Pra montar de novo do zero, use Tools/IA/Recriar Cena {nome} (sobrescreve, com backup).");
                return;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene aberta = SceneManager.GetSceneAt(i);
                if (aberta.isDirty)
                {
                    log.Add($"ERRO: a cena '{(string.IsNullOrEmpty(aberta.name) ? "(sem nome)" : aberta.name)}' tem alterações não salvas. Salve ou descarte antes de rodar — nada foi feito.");
                    return;
                }
            }

            if (recriar && File.Exists(CaminhoAbsoluto(caminho)))
            {
                FazerBackup(caminho);
            }

            Scene cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            montar(cena);

            EditorSceneManager.MarkSceneDirty(cena);
            bool salvou = EditorSceneManager.SaveScene(cena, caminho);
            log.Add(salvou ? $"Cena salva em {caminho}." : $"ERRO: não consegui salvar {caminho}.");
            log.Add("Lembrete: a cena NÃO foi adicionada ao Build Settings.");
        }
        catch (Exception e)
        {
            log.Add("ERRO inesperado: " + e);
        }
        finally
        {
            foreach (string linha in log)
            {
                if (linha.StartsWith("ERRO")) Debug.LogError("[IASceneBuilder] " + linha);
                else if (linha.StartsWith("AVISO")) Debug.LogWarning("[IASceneBuilder] " + linha);
                else Debug.Log("[IASceneBuilder] " + linha);
            }
        }
    }

    private static string CaminhoAbsoluto(string caminhoAsset)
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, caminhoAsset);
    }

    private static void FazerBackup(string caminho)
    {
        string projeto = Directory.GetParent(Application.dataPath).FullName;
        string pasta = Path.Combine(projeto, "SceneBackups");
        Directory.CreateDirectory(pasta);
        string destino = Path.Combine(pasta, $"{Path.GetFileNameWithoutExtension(caminho)}_{DateTime.Now:yyyyMMdd_HHmmss}.unity");
        File.Copy(CaminhoAbsoluto(caminho), destino, true);
        log.Add("Backup da cena antiga: " + destino);
    }

    // ------------------------------------------------------------------
    // Cena IA (construção)
    // ------------------------------------------------------------------

    private static void MontarCenaIA(Scene cena)
    {
        Vector2 posicaoPuzzles = new Vector2(0f, 60f);

        // Sala no padrão das outras fases (kit Tecno em escala 1): paredes
        // nas pontas, uma parede baixa pra se esconder do guarda e uma
        // passarela alta como rota segura até a saída.
        GameObject cenario = new GameObject("Cenario");
        var kit = new IACenarioKit(-9.2f, 35.2f);
        kit.AdicionarBloco("Parede", 2.6f, 3.4f, 2);   // 0,8 x 1,6: esconde o jogador em pé do Raycast (olho em 0,7) e dá pra subir (pulo ~2,5)
        IACenarioKit.Fundo(cenario.transform, kit.BordaEsquerda - 2f, kit.BordaDireita + 2f, AlturaCamera);
        kit.Construir(cenario.transform);
        IACenarioKit.Plataforma(cenario.transform, "Passarela", 5.2f, 22.4f, 3.3f);

        Vector2 camMin = new Vector2(kit.BordaEsquerda + RecuoCamera, AlturaCamera);
        Vector2 camMax = new Vector2(kit.BordaDireita - RecuoCamera, AlturaCamera);

        Transform pontoA = CriarMarcador(cenario.transform, "PontoA", new Vector3(7f, 0.5f, 0f), "A");
        Transform pontoB = CriarMarcador(cenario.transform, "PontoB", new Vector3(19f, 0.5f, 0f), "B");
        Transform pontoInicio = CriarPonto(cenario.transform, "PontoInicio", new Vector3(-3f, 0.6f, 0f));
        Transform atrasDaParede = CriarPonto(cenario.transform, "PontoAtrasDaParede", new Vector3(1.2f, 0.6f, 0f));

        IASaida saida = CriarSaida(cenario.transform, new Vector3(30.5f, 1.2f, 0f), true);

        GameObject jogador = CriarJogador(pontoInicio.position);
        GuardaIA guarda = CriarGuarda(new Vector3(10f, 0.55f, 0f), pontoA, pontoB,
            raioVisao: 7f, raioPerder: 9f, alcance: 1.1f, velPatrulha: 1.6f, velPerseguir: 3.2f,
            estadosMontados: false, transicoesMontadas: false, percepcao: GuardaIA.Percepcao.Distancia, mostrarNome: false);

        Camera cam = CriarCamera(new Vector3(Mathf.Clamp(pontoInicio.position.x, camMin.x, camMax.x), AlturaCamera, -10f));
        CameraFollow follow = ConfigurarCameraFollow(cam, jogador.transform, camMin, camMax);

        CriarEventSystem();
        Canvas canvas = CriarCanvas();
        GameObject hudVida = CriarHudVida(canvas, jogador.GetComponent<PlayerHealth>());
        DialogueManager dialogo = DialogueBoxBuilder.EnsureDialogueBox(canvas);
        LevelCompleteUI completo = CriarPainelConcluido(canvas);

        // O fluxo precisa existir antes dos puzzles (é o listener deles).
        var flowGO = new GameObject("IAFlow");
        IAFlow flow = flowGO.AddComponent<IAFlow>();

        IAPuzzleRigBuilder.Resultado puzzles = IAPuzzleRigBuilder.Construir(cena, posicaoPuzzles, flow, log);
        if (puzzles == null)
        {
            log.Add("AVISO: cena criada SEM os puzzles. Corrija o problema acima e rode Tools/IA/Recriar Cena IA.");
        }

        var so = new SerializedObject(flow);
        so.FindProperty("dialogueManager").objectReferenceValue = dialogo;
        so.FindProperty("guarda").objectReferenceValue = guarda;
        so.FindProperty("jogador").objectReferenceValue = jogador.GetComponent<InteracaoPlayerController>();
        so.FindProperty("vidaJogador").objectReferenceValue = jogador.GetComponent<PlayerHealth>();
        so.FindProperty("cameraFollow").objectReferenceValue = follow;
        so.FindProperty("saida").objectReferenceValue = saida;
        so.FindProperty("completeUI").objectReferenceValue = completo;
        so.FindProperty("hudVida").objectReferenceValue = hudVida;
        so.FindProperty("pontoInicio").objectReferenceValue = pontoInicio;
        so.FindProperty("pontoAtrasDaParede").objectReferenceValue = atrasDaParede;
        so.FindProperty("posicaoDosPuzzles").vector2Value = posicaoPuzzles;
        so.FindProperty("cameraMin").vector2Value = camMin;
        so.FindProperty("cameraMax").vector2Value = camMax;
        so.FindProperty("nextSceneName").stringValue = "IA_Aplicacao";
        if (puzzles != null)
        {
            so.FindProperty("puzzleEstados").objectReferenceValue = puzzles.estados;
            so.FindProperty("puzzleTransicoes").objectReferenceValue = puzzles.transicoes;
            so.FindProperty("puzzleDepuracao").objectReferenceValue = puzzles.depuracao;
            so.FindProperty("regraErrada").objectReferenceValue = puzzles.regraErrada;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        log.Add("Cena IA montada: cenário (chão, parede, passarela, A/B, saída trancada), Player, Guarda sem cérebro, câmera, HUD de vida, diálogo, IAFlow.");
    }

    // ------------------------------------------------------------------
    // Cena IA_Aplicacao
    // ------------------------------------------------------------------

    private static void MontarCenaAplicacao(Scene cena)
    {
        GarantirTag(GuardaCerebroConfig.TagAliado);

        // Corredor no padrão das outras fases; a plataforma é o esconderijo
        // (ela bloqueia o Raycast do guarda pra quem está em cima, no meio).
        GameObject cenario = new GameObject("Cenario");
        var kit = new IACenarioKit(-3.2f, 27.2f);
        IACenarioKit.Fundo(cenario.transform, kit.BordaEsquerda - 2f, kit.BordaDireita + 2f, AlturaCamera);
        kit.Construir(cenario.transform);
        IACenarioKit.Plataforma(cenario.transform, "Plataforma", 8f, 12f, 2.1f);
        float xMin = kit.BordaEsquerda, xMax = kit.BordaDireita;

        Vector2 camMin = new Vector2(kit.BordaEsquerda + RecuoCamera, AlturaCamera);
        Vector2 camMax = new Vector2(kit.BordaDireita - RecuoCamera, AlturaCamera);

        Transform pontoA = CriarMarcador(cenario.transform, "PontoA", new Vector3(12.5f, 0.5f, 0f), "A");
        Transform pontoB = CriarMarcador(cenario.transform, "PontoB", new Vector3(18f, 0.5f, 0f), "B");
        Transform pontoInicio = CriarPonto(cenario.transform, "PontoInicio", new Vector3(1f, 0.6f, 0f));
        Transform pontoAliado = CriarPonto(cenario.transform, "PontoInicioAliado", new Vector3(-0.5f, 0.45f, 0f));

        IASaida saida = CriarSaida(cenario.transform, new Vector3(23.5f, 1.2f, 0f), false);

        GameObject jogador = CriarJogador(pontoInicio.position);
        GuardaIA guarda = CriarGuarda(new Vector3(16f, 0.55f, 0f), pontoA, pontoB,
            raioVisao: 5.5f, raioPerder: 7.5f, alcance: 1.1f, velPatrulha: 1.5f, velPerseguir: 4.5f,
            estadosMontados: true, transicoesMontadas: true, percepcao: GuardaIA.Percepcao.Raycast, mostrarNome: true);
        GuardaCerebroConfig guardaConfig = guarda.gameObject.AddComponent<GuardaCerebroConfig>();
        AliadoIA aliado = CriarAliado(pontoAliado.position, jogador.transform, guarda, new Vector2(xMin + 0.4f, xMax - 0.4f));

        Camera cam = CriarCamera(new Vector3(camMin.x, AlturaCamera, -10f));
        CameraFollow follow = ConfigurarCameraFollow(cam, jogador.transform, camMin, camMax);

        CriarEventSystem();
        Canvas canvas = CriarCanvas();
        CriarHudVida(canvas, jogador.GetComponent<PlayerHealth>());
        CriarTextoHud(canvas, "DicaClique", "Clique no guarda ou no aliado para abrir o cérebro deles.    R recomeça a fase.");
        DialogueManager dialogo = DialogueBoxBuilder.EnsureDialogueBox(canvas);
        LevelCompleteUI completo = CriarPainelConcluido(canvas);

        var flowGO = new GameObject("IAAplicacaoFlow");
        IASeletorNPC seletor = flowGO.AddComponent<IASeletorNPC>();
        var soSel = new SerializedObject(seletor);
        soSel.FindProperty("cameraJogo").objectReferenceValue = cam;
        soSel.ApplyModifiedPropertiesWithoutUndo();

        IAAplicacaoFlow flow = flowGO.AddComponent<IAAplicacaoFlow>();
        var so = new SerializedObject(flow);
        so.FindProperty("dialogueManager").objectReferenceValue = dialogo;
        so.FindProperty("jogador").objectReferenceValue = jogador.GetComponent<InteracaoPlayerController>();
        so.FindProperty("vidaJogador").objectReferenceValue = jogador.GetComponent<PlayerHealth>();
        so.FindProperty("guarda").objectReferenceValue = guarda;
        so.FindProperty("guardaConfig").objectReferenceValue = guardaConfig;
        so.FindProperty("aliado").objectReferenceValue = aliado;
        so.FindProperty("saida").objectReferenceValue = saida;
        so.FindProperty("seletor").objectReferenceValue = seletor;
        so.FindProperty("completeUI").objectReferenceValue = completo;
        so.FindProperty("cameraFollow").objectReferenceValue = follow;
        so.FindProperty("cameraMin").vector2Value = camMin;
        so.FindProperty("cameraMax").vector2Value = camMax;
        so.FindProperty("pontoInicio").objectReferenceValue = pontoInicio;
        so.FindProperty("pontoInicioAliado").objectReferenceValue = pontoAliado;
        so.FindProperty("nextSceneName").stringValue = "Questionario";
        so.ApplyModifiedPropertiesWithoutUndo();

        log.Add("Cena IA_Aplicacao montada: corredor, plataforma, Guarda (Raycast, alvo 'tag Player'), Aliado (cérebro vazio), saída, câmera, HUD, diálogo, IAAplicacaoFlow + IASeletorNPC.");
    }

    // ------------------------------------------------------------------
    // Personagens
    // ------------------------------------------------------------------

    private static GameObject CriarJogador(Vector3 posicao)
    {
        var go = new GameObject("Player");
        go.tag = "Player";
        go.transform.position = posicao;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Carregar(PlayerSpritePath);
        sr.sortingOrder = 6;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = sr.sprite != null ? (Vector2)sr.sprite.bounds.size * 0.95f : new Vector2(0.95f, 0.95f);

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        go.AddComponent<InteracaoPlayerController>();

        PlayerHealth vida = go.AddComponent<PlayerHealth>();
        var so = new SerializedObject(vida);
        so.FindProperty("maxHealth").intValue = 3;
        so.FindProperty("startingHealth").intValue = 3;
        so.ApplyModifiedPropertiesWithoutUndo();

        return go;
    }

    private static GuardaIA CriarGuarda(Vector3 posicao, Transform pontoA, Transform pontoB,
        float raioVisao, float raioPerder, float alcance, float velPatrulha, float velPerseguir,
        bool estadosMontados, bool transicoesMontadas, GuardaIA.Percepcao percepcao, bool mostrarNome)
    {
        var go = new GameObject("Guarda");
        go.transform.position = posicao;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.9f, 0.95f);

        SpriteRenderer visual = CriarVisual(go.transform, GuardaSpritePath, 1f, 5);
        CriarIconeEstado(go.transform, new Vector3(0f, 0.95f, 0f), mostrarNome ? 3.2f : 6f, out TextMeshPro texto);

        GuardaIA guarda = go.AddComponent<GuardaIA>();
        var so = new SerializedObject(guarda);
        so.FindProperty("estadosMontados").boolValue = estadosMontados;
        so.FindProperty("transicoesMontadas").boolValue = transicoesMontadas;
        so.FindProperty("percepcao").enumValueIndex = (int)percepcao;
        so.FindProperty("pontoA").objectReferenceValue = pontoA;
        so.FindProperty("pontoB").objectReferenceValue = pontoB;
        so.FindProperty("velocidadePatrulha").floatValue = velPatrulha;
        so.FindProperty("velocidadePerseguir").floatValue = velPerseguir;
        so.FindProperty("raioVisao").floatValue = raioVisao;
        so.FindProperty("raioPerder").floatValue = raioPerder;
        so.FindProperty("alcance").floatValue = alcance;
        so.FindProperty("obstaculos").intValue = 1 << LayerMask.NameToLayer("Default");
        so.FindProperty("visual").objectReferenceValue = visual.transform;
        SerializedProperty tags = so.FindProperty("tagsAlvo");
        tags.arraySize = 1;
        tags.GetArrayElementAtIndex(0).stringValue = "Player";
        so.ApplyModifiedPropertiesWithoutUndo();

        IAIconeEstado icone = texto.gameObject.AddComponent<IAIconeEstado>();
        var soIcone = new SerializedObject(icone);
        soIcone.FindProperty("guarda").objectReferenceValue = guarda;
        soIcone.FindProperty("texto").objectReferenceValue = texto;
        soIcone.FindProperty("mostrarNome").boolValue = mostrarNome;
        soIcone.ApplyModifiedPropertiesWithoutUndo();

        return guarda;
    }

    private static AliadoIA CriarAliado(Vector3 posicao, Transform jogador, GuardaIA guarda, Vector2 limitesX)
    {
        var go = new GameObject("Aliado");
        go.tag = GuardaCerebroConfig.TagAliado;
        go.transform.position = posicao;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.62f, 0.66f);

        SpriteRenderer visual = CriarVisual(go.transform, AliadoSpritePath, 0.7f, 5);
        CriarIconeEstado(go.transform, new Vector3(0f, 0.75f, 0f), 3.2f, out TextMeshPro texto);

        AliadoIA aliado = go.AddComponent<AliadoIA>();
        var so = new SerializedObject(aliado);
        so.FindProperty("jogador").objectReferenceValue = jogador;
        so.FindProperty("guarda").objectReferenceValue = guarda;
        so.FindProperty("visual").objectReferenceValue = visual.transform;
        so.FindProperty("limitesX").vector2Value = limitesX;
        so.FindProperty("obstaculos").intValue = 1 << LayerMask.NameToLayer("Default");
        so.FindProperty("estadoInicial").enumValueIndex = (int)AliadoIA.Estado.Esperar;
        so.FindProperty("condEsperarParaSeguir").stringValue = "";
        so.FindProperty("condSeguirParaEsperar").stringValue = "";
        so.FindProperty("condParaDistrair").stringValue = "";
        so.ApplyModifiedPropertiesWithoutUndo();

        IAIconeEstado icone = texto.gameObject.AddComponent<IAIconeEstado>();
        var soIcone = new SerializedObject(icone);
        soIcone.FindProperty("aliado").objectReferenceValue = aliado;
        soIcone.FindProperty("texto").objectReferenceValue = texto;
        soIcone.ApplyModifiedPropertiesWithoutUndo();

        return aliado;
    }

    private static SpriteRenderer CriarVisual(Transform pai, string spritePath, float tamanho, int ordem)
    {
        var go = new GameObject("Visual");
        go.transform.SetParent(pai, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Carregar(spritePath);
        sr.sortingOrder = ordem;
        Vector2 s = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        go.transform.localScale = new Vector3(tamanho / Mathf.Max(0.0001f, s.x), tamanho / Mathf.Max(0.0001f, s.y), 1f);
        return sr;
    }

    private static void CriarIconeEstado(Transform pai, Vector3 posicaoLocal, float fonte, out TextMeshPro texto)
    {
        var go = new GameObject("IconeEstado");
        go.transform.SetParent(pai, false);
        go.transform.localPosition = posicaoLocal;
        texto = go.AddComponent<TextMeshPro>();
        texto.text = "...";
        texto.fontSize = fonte;
        texto.fontStyle = FontStyles.Bold;
        texto.alignment = TextAlignmentOptions.Center;
        texto.textWrappingMode = TextWrappingModes.NoWrap;
        texto.richText = false;
        texto.rectTransform.sizeDelta = new Vector2(6f, 1f);
        texto.sortingOrder = 20;
    }

    // ------------------------------------------------------------------
    // Cenário
    // ------------------------------------------------------------------

    private static Transform CriarPonto(Transform pai, string nome, Vector3 posicao)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        go.transform.position = posicao;
        return go.transform;
    }

    // Ponto de patrulha com uma letra no chão (só visual).
    private static Transform CriarMarcador(Transform pai, string nome, Vector3 posicao, string letra)
    {
        Transform ponto = CriarPonto(pai, nome, posicao);
        // Placa no poste em vez da letra solta no chão.
        IAMarcadorPatrulhaBuilder.Criar(ponto, letra, IAMarcadorPatrulhaBuilder.GarantirSprite());
        return ponto;
    }

    private static IASaida CriarSaida(Transform pai, Vector3 posicao, bool comBarreira)
    {
        var go = new GameObject("Saida");
        go.transform.SetParent(pai, false);
        go.transform.position = posicao;

        BoxCollider2D gatilho = go.AddComponent<BoxCollider2D>();
        gatilho.isTrigger = true;
        gatilho.size = new Vector2(1.2f, 2.4f);

        // Mesma porta da Interacao (lift_panel na proporção original).
        SpriteRenderer porta = IACenarioKit.Porta(go.transform, posicao.x);
        float topoPorta = porta != null ? porta.bounds.max.y : 1.5f;

        TextoMundo(go.transform, "Placa", "SAÍDA", new Vector3(posicao.x, topoPorta + 0.4f, 0f), 4f, new Color(0.31f, 0.78f, 0.45f, 1f));

        // Trancada: uma cerca de laser em pé (os lasers da Interacao_Vida) do
        // chão até acima do alcance do pulo.
        GameObject barreira = null;
        if (comBarreira)
        {
            barreira = new GameObject("Barreira");
            barreira.transform.SetParent(pai, false);
            float xBarreira = posicao.x - 1.4f;
            BoxCollider2D colBarreira = barreira.AddComponent<BoxCollider2D>();
            colBarreira.offset = new Vector2(xBarreira, 5.6f);
            colBarreira.size = new Vector2(0.4f, 11.2f);
            IACenarioKit.Lasers(barreira.transform, xBarreira, 11.2f);
        }

        IASaida saida = go.AddComponent<IASaida>();
        var so = new SerializedObject(saida);
        so.FindProperty("aberta").boolValue = !comBarreira;
        so.FindProperty("barreira").objectReferenceValue = barreira;
        so.ApplyModifiedPropertiesWithoutUndo();
        return saida;
    }

    private static SpriteRenderer SpriteMundo(Transform pai, string nome, Sprite sprite, Vector3 posicao, Vector2 tamanhoMundo, int ordem, Color cor)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        go.transform.position = posicao;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = ordem;
        sr.color = cor;

        Vector2 s = sprite.bounds.size;
        Vector3 pl = pai.lossyScale;
        go.transform.localScale = new Vector3(
            tamanhoMundo.x / Mathf.Max(0.0001f, s.x * Mathf.Abs(pl.x)),
            tamanhoMundo.y / Mathf.Max(0.0001f, s.y * Mathf.Abs(pl.y)),
            1f);
        return sr;
    }

    private static TextMeshPro TextoMundo(Transform pai, string nome, string texto, Vector3 posicao, float fonte, Color cor)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        go.transform.position = posicao;
        TextMeshPro t = go.AddComponent<TextMeshPro>();
        t.text = texto;
        t.fontSize = fonte;
        t.fontStyle = FontStyles.Bold;
        t.color = cor;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.rectTransform.sizeDelta = new Vector2(4f, 1f);
        t.sortingOrder = 3;
        return t;
    }

    // ------------------------------------------------------------------
    // Câmera, UI
    // ------------------------------------------------------------------

    private static Camera CriarCamera(Vector3 posicao)
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        go.transform.position = posicao;

        Camera cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = CorFundoCamera;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1000f;

        go.AddComponent<AudioListener>();
        return cam;
    }

    private static CameraFollow ConfigurarCameraFollow(Camera cam, Transform alvo, Vector2 min, Vector2 max)
    {
        CameraFollow follow = cam.gameObject.AddComponent<CameraFollow>();
        var so = new SerializedObject(follow);
        so.FindProperty("target").objectReferenceValue = alvo;
        so.FindProperty("useBounds").boolValue = true;
        so.FindProperty("minPosition").vector2Value = min;
        so.FindProperty("maxPosition").vector2Value = max;
        so.ApplyModifiedPropertiesWithoutUndo();
        return follow;
    }

    private static void CriarEventSystem()
    {
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    private static Canvas CriarCanvas()
    {
        var go = new GameObject("Canvas");
        go.layer = LayerMask.NameToLayer("UI");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // Três quadrados de vida no canto (HeartHealthUI), como na Interacao_Vida.
    private static GameObject CriarHudVida(Canvas canvas, PlayerHealth vida)
    {
        var life = new GameObject("Life", typeof(RectTransform));
        RectTransform rt = (RectTransform)life.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(40f, -36f);
        rt.sizeDelta = new Vector2(240f, 64f);

        var coracoes = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            var q = new GameObject(i == 0 ? "Image" : $"Image ({i})", typeof(RectTransform));
            RectTransform qrt = (RectTransform)q.transform;
            qrt.SetParent(rt, false);
            qrt.anchorMin = qrt.anchorMax = new Vector2(0f, 1f);
            qrt.pivot = new Vector2(0f, 1f);
            qrt.sizeDelta = new Vector2(56f, 56f);
            qrt.anchoredPosition = new Vector2(i * 72f, 0f);
            coracoes[i] = q.AddComponent<Image>();
            coracoes[i].color = new Color(0.9f, 0.2f, 0.25f);
            coracoes[i].raycastTarget = false;
        }

        HeartHealthUI hud = life.AddComponent<HeartHealthUI>();
        var so = new SerializedObject(hud);
        SerializedProperty hearts = so.FindProperty("hearts");
        hearts.arraySize = coracoes.Length;
        for (int i = 0; i < coracoes.Length; i++)
        {
            hearts.GetArrayElementAtIndex(i).objectReferenceValue = coracoes[i];
        }
        so.FindProperty("playerHealth").objectReferenceValue = vida;
        so.ApplyModifiedPropertiesWithoutUndo();
        return life;
    }

    private static void CriarTextoHud(Canvas canvas, string nome, string texto)
    {
        GameObject go = UIBuildUtils.CreateText(canvas.transform, nome, texto, 26f, TextAlignmentOptions.Center, new Color(0.86f, 0.9f, 0.96f, 1f));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(1300f, 50f);
        rt.anchoredPosition = new Vector2(0f, -30f);
    }

    private static LevelCompleteUI CriarPainelConcluido(Canvas canvas)
    {
        var panelGO = new GameObject("LevelCompletePanel", typeof(RectTransform));
        RectTransform rect = (RectTransform)panelGO.transform;
        rect.SetParent(canvas.transform, false);
        UIBuildUtils.StretchToParent(rect);

        Image bg = panelGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.75f);

        GameObject textGO = UIBuildUtils.CreateText(rect, "Text", "Fase Concluída!", 64, TextAlignmentOptions.Center);
        UIBuildUtils.StretchToParent(textGO.GetComponent<RectTransform>());

        LevelCompleteUI completeUI = panelGO.AddComponent<LevelCompleteUI>();
        panelGO.SetActive(false);
        return completeUI;
    }

    private static Sprite Carregar(string path)
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        if (sprite == null) log.Add("AVISO: sprite não encontrado em " + path);
        return sprite;
    }

    private static void GarantirTag(string tag)
    {
        if (InternalEditorUtility.tags.Contains(tag))
        {
            return;
        }

        InternalEditorUtility.AddTag(tag);
        log.Add($"Tag '{tag}' cadastrada em Project Settings > Tags and Layers.");
    }
}
