using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Código com lacunas nos 5 puzzles da cena Fisica: a janela da esquerda vira o
/// script (ou o Inspector) do objeto, com as lacunas no meio das linhas; a
/// prévia do Objetivo fica do lado, da mesma altura; as peças à direita são
/// coloridas por tipo e levam uma etiqueta; a Dica fala da lacuna atual.
///
/// Tudo é montado e SALVO na cena (nada é criado só em Play). Rodar de novo
/// refaz o layout do zero (idempotente). Backup antes da primeira vez:
/// SceneBackups/fisica_codigo/.
///
/// Tools > Física > Aplicar código com lacunas
/// </summary>
public static class FisicaCodigoBuilder
{
    private const string ScenePath = "Assets/Scenes/Fisica.unity";
    private const string Kit = "Assets/Images/EditorKit/";
    private const string Categorias = Kit + "icones/categorias/";
    private const string SortingLayer = "Puzzle";

    // Cores do código (mesmas do mockup aprovado).
    private const string CorKw = "#7AA2F7";
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
        { "laranja", new Color32(255, 138, 61, 255) },
        { "anil", new Color32(129, 140, 248, 255) },
        { "azul", new Color32(61, 165, 255, 255) },
        { "verde", new Color32(43, 209, 138, 255) },
        { "lima", new Color32(163, 230, 53, 255) },
    };

    // Moldura da peça com a borda na cor do tipo (as artes do EditorKit já
    // vêm com essas cores).
    private static readonly Dictionary<string, string> MolduraTipo = new Dictionary<string, string>
    {
        { "rosa", "peca_input" },
        { "laranja", "peca_rigidbody2d" },
        { "anil", "peca_vector2" },
        { "azul", "peca_transform" },
        { "verde", "peca_boxcollider" },
        { "lima", "peca_update" },
    };

    // ------------------------------------------------------------------
    // Layout (unidades de mundo, relativo ao centro do fundo do puzzle)
    // ------------------------------------------------------------------
    private static readonly Rect JanelaEsq = Rect.MinMaxRect(-8.55f, -2.63f, 3.35f, 4.27f);
    private static readonly Rect JanelaPecas = Rect.MinMaxRect(3.6f, -2.63f, 8.65f, 4.27f);
    private static readonly Rect AreaCodigo = Rect.MinMaxRect(-8.25f, -2.35f, -0.95f, 3.72f);
    private static readonly Rect AreaPrevia = Rect.MinMaxRect(-0.7f, -2.35f, 3.05f, 3.72f);
    private const float LarguraPeca = 4.1f;
    private const float AlturaPeca = 0.64f;
    private const float PassoPeca = 0.76f;
    private static readonly Vector2 FundoReferencia = new Vector2(0.21f, 0.18f);

    // ------------------------------------------------------------------
    // Conteúdo
    // ------------------------------------------------------------------
    private class Peca
    {
        public int id;
        public string rotulo, etiqueta, cor, icone, descricao;
        public Peca(int id, string rotulo, string etiqueta, string cor, string icone, string descricao)
        {
            this.id = id; this.rotulo = rotulo; this.etiqueta = etiqueta; this.cor = cor; this.icone = icone; this.descricao = descricao;
        }
    }

    private class Lac
    {
        public int id;
        public string dica;
        public Lac(int id, string dica) { this.id = id; this.dica = dica; }
    }

    private class Linha
    {
        public string tipo;   // "obj", "cab", "campo"
        public string rotulo, valor, icone;
        public Linha(string tipo, string rotulo, string valor = "", string icone = null)
        {
            this.tipo = tipo; this.rotulo = rotulo; this.valor = valor; this.icone = icone;
        }
    }

    private class Def
    {
        public string grupo, controle, nome, objeto, script, objetivo, objetoIcone;
        public int ordem;
        public PuzzlePreview.PreviewType previa;
        public Linha[] inspector;   // null = só script
        public string[] codigo;     // null = só Inspector
        public Lac[] lacunas;
        public Peca[] pecas;
    }

    private static Def[] Defs => new[]
    {
        new Def
        {
            grupo = "Puzzle", controle = "PuzzleControl", nome = "Movimento", ordem = 1,
            objeto = "Player", objetoIcone = "Assets/Images/player/player_square_final.png", script = "SCRIPT · PLAYER.CS",
            objetivo = "o Player anda pros lados", previa = PuzzlePreview.PreviewType.MoveBackAndForth,
            codigo = new[]
            {
                "<kw>void</kw> Update() {",
                "    <kw>float</kw> h = §0.GetAxis(§1);",
                "    <ty>Vector2</ty> direcao = <kw>new</kw> §2(h, 0);",
                "    §3 += (<ty>Vector3</ty>)direcao * velocidade * Time.deltaTime;",
                "}",
            },
            lacunas = new[]
            {
                new Lac(1, "Linha 2: quem escuta o teclado?"),
                new Lac(2, "Linha 2: qual eixo o personagem usa pra andar pros lados?"),
                new Lac(3, "Linha 3: em que tipo a direção fica guardada?"),
                new Lac(4, "Linha 4: o que muda de lugar a cada frame?"),
            },
            pecas = new[]
            {
                new Peca(4, "transform.position", "prop.", "azul", "transform", "A posição do objeto no mundo, com x, y e z. Mudar esse valor leva o objeto pra outro lugar."),
                new Peca(5, "\"Vertical\"", "eixo", "laranja", "vertical", "Nome do eixo cima/baixo. Input.GetAxis(\"Vertical\") devolve -1 pra baixo, 0 parado e 1 pra cima."),
                new Peca(1, "Input", "classe", "rosa", "input", "Classe do Unity que lê teclado, mouse e controle. Sem ela, o jogo não sabe o que você apertou."),
                new Peca(3, "Vector2", "tipo", "anil", "vector2", "Dois números, x e y, que guardam uma direção ou um ponto."),
                new Peca(6, "Vector3", "tipo", "anil", "vector3", "Três números, x, y e z, que guardam uma direção ou um ponto. É como o Vector2, com um número a mais (z)."),
                new Peca(2, "\"Horizontal\"", "eixo", "laranja", "horizontal", "Nome do eixo esquerda/direita. Input.GetAxis(\"Horizontal\") devolve -1 pra esquerda, 0 parado e 1 pra direita."),
                new Peca(7, "transform.rotation", "prop.", "azul", "transform", "A rotação do objeto. Mudar esse valor gira o objeto."),
                new Peca(8, "\"Fire1\"", "botão", "laranja", "input", "Nome de um botão do Input Manager (Ctrl ou clique, por padrão). Input.GetButton(\"Fire1\") diz se ele está apertado."),
            },
        },
        new Def
        {
            grupo = "PuzzleCam", controle = "PuzzleControlCam", nome = "Câmera", ordem = 2,
            objeto = "Main Camera", objetoIcone = Kit + "icones/componentes/icone_transform.png", script = "SCRIPT · CAMERAFOLLOW.CS",
            objetivo = "a câmera segue o Player e para na borda", previa = PuzzlePreview.PreviewType.CameraFollow,
            codigo = new[]
            {
                "<kw>public</kw> <ty>Transform</ty> §0;",
                "<kw>void</kw> §1() {",
                "    <ty>Vector3</ty> pos = <ty>Vector3</ty>.§2(transform.position,",
                "            <mu>target.position, ref vel, 0.25f);</mu>",
                "    pos.x = <ty>Mathf</ty>.Clamp(pos.x, §3);",
                "    transform.position = <kw>new</kw> <ty>Vector3</ty>(pos.x, pos.y, -10);",
                "}",
            },
            lacunas = new[]
            {
                new Lac(1, "Linha 1: quem a câmera segue?"),
                new Lac(2, "Linha 2: em que momento do frame a câmera deve andar, pra não ficar um passo atrás do Player?"),
                new Lac(3, "Linha 3: como a câmera chega até o alvo aos poucos, sem dar tranco?"),
                new Lac(4, "Linha 5: até onde a câmera pode ir sem mostrar o vazio fora da sala?"),
            },
            pecas = new[]
            {
                new Peca(5, "Update", "método", "lima", "update", "Roda uma vez a cada frame, junto com o Update dos outros objetos, sem ordem garantida entre eles."),
                new Peca(1, "target", "campo", "azul", "transform", "Campo público do tipo Transform. Aparece no Inspector, onde dá pra arrastar um objeto pra ele."),
                new Peca(4, "minX, maxX", "limites", "laranja", "boxcollider", "Dois números, um mínimo e um máximo. Mathf.Clamp(valor, min, max) mantém o valor entre os dois."),
                new Peca(6, "position = alvo", "atalho", "azul", "transform", "transform.position = alvo copia a posição do alvo de uma vez, no mesmo frame."),
                new Peca(3, "SmoothDamp", "método", "anil", "velocity", "Leva um valor até o alvo aos poucos, acelerando e freando no caminho."),
                new Peca(2, "LateUpdate", "método", "lima", "update", "Roda uma vez a cada frame, depois que o Update de todos os objetos já rodou."),
                new Peca(7, "FixedUpdate", "método", "lima", "fixedupdate", "Roda no ritmo da física, em intervalos fixos que não batem com os frames da tela."),
                new Peca(8, "Start", "método", "lima", "start", "Roda uma vez só, quando o objeto nasce."),
            },
        },
        new Def
        {
            grupo = "PuzzleRig", controle = "PuzzleControlRig", nome = "Rigidbody2D", ordem = 3,
            objeto = "Player", objetoIcone = "Assets/Images/player/player_square_final.png",
            objetivo = "o Player cai e não tomba", previa = PuzzlePreview.PreviewType.Fall,
            inspector = new[]
            {
                new Linha("obj", "Player", "Tag: Player · Layer: Default"),
                new Linha("cab", "Transform", "X 0   Y 3   Z 0", "transform"),
                new Linha("cab", "Add Component ›", "§0"),
                new Linha("campo", "Body Type", "§1"),
                new Linha("campo", "Mass", "1"),
                new Linha("campo", "Gravity Scale", "§2"),
                new Linha("campo", "Constraints", "Freeze Position: X não, Y não"),
                new Linha("campo", "Freeze Rotation", "§3"),
            },
            lacunas = new[]
            {
                new Lac(1, "Add Component: qual componente coloca o Player sob a física 2D do Unity?"),
                new Lac(2, "Body Type: qual tipo de corpo reage à gravidade?"),
                new Lac(3, "Gravity Scale: qual valor faz o Player cair firme?"),
                new Lac(4, "Freeze Rotation: o que impede o Player de tombar ao esbarrar em algo?"),
            },
            pecas = new[]
            {
                new Peca(6, "Collider2D", "comp.", "anil", "collider2d", "Dá ao objeto uma forma pra colisões."),
                new Peca(5, "Static", "corpo", "laranja", "bodytype_dynamic", "Corpo que nunca se move, nem com gravidade nem com forças."),
                new Peca(7, "0", "número", "azul", "gravityscale", "Gravity Scale 0: a gravidade não age neste objeto."),
                new Peca(4, "Z travado", "trava", "verde", "freezerotation", "Trava a rotação no eixo Z: o objeto não gira, mesmo quando algo bate nele."),
                new Peca(2, "Dynamic", "corpo", "laranja", "bodytype_dynamic", "Corpo que reage à gravidade, a forças e a colisões."),
                new Peca(3, "4", "número", "azul", "gravityscale", "Multiplica a gravidade neste objeto: 1 é a normal, 4 é quatro vezes a normal."),
                new Peca(8, "Kinematic", "corpo", "laranja", "rigidbody2d", "Corpo que só se move pelo código. A gravidade e as forças não agem nele."),
                new Peca(1, "Rigidbody2D", "comp.", "anil", "rigidbody2d", "Componente que coloca o objeto sob a física 2D do Unity. Sem ele, gravidade e forças não fazem efeito."),
            },
        },
        new Def
        {
            grupo = "PuzzleCol", controle = "PuzzleControlCol", nome = "Collider2D", ordem = 4,
            objeto = "Player", objetoIcone = "Assets/Images/player/player_square_final.png", script = "SCRIPT · PLAYER.CS",
            objetivo = "pisa no chão e bate na parede", previa = PuzzlePreview.PreviewType.CollideWallAndWalk,
            inspector = new[]
            {
                new Linha("obj", "Player", "Tag: Player · Layer: Default"),
                new Linha("cab", "Rigidbody2D", "(do puzzle anterior)", "rigidbody2d"),
                new Linha("cab", "Add Component ›", "§0"),
                new Linha("campo", "Is Trigger", "§1"),
                new Linha("campo", "Size", "X 1   Y 1"),
            },
            codigo = new[]
            {
                "<kw>void</kw> §2(<ty>Collision2D</ty> c)",
                "{",
                "    <mu>isGrounded = true;   // encostou em algo sólido</mu>",
                "}",
            },
            lacunas = new[]
            {
                new Lac(1, "Add Component: qual componente dá ao Player uma forma que encosta nas coisas?"),
                new Lac(2, "Is Trigger: o chão precisa segurar o Player. O collider deve barrar quem encosta ou deixar passar?"),
                new Lac(3, "Linha 1 do script: qual método o Unity chama quando dois colliders sólidos se encostam?"),
            },
            pecas = new[]
            {
                new Peca(5, "OnTriggerEnter2D", "método", "rosa", "ontriggerenter2d", "Método chamado quando algo entra num collider marcado como Trigger."),
                new Peca(4, "true", "bool", "laranja", "istrigger", "Is Trigger ligado: o collider vira uma área que avisa quando algo entra e deixa passar."),
                new Peca(6, "Rigidbody2D", "comp.", "anil", "rigidbody2d", "Componente que coloca o objeto sob a física 2D: gravidade e forças passam a agir nele."),
                new Peca(3, "OnCollisionEnter2D", "método", "rosa", "oncollisionenter2d", "Método chamado no instante em que dois colliders sólidos se encostam."),
                new Peca(2, "false", "bool", "laranja", "istrigger_false", "Is Trigger desligado: o collider é sólido e barra quem encosta."),
                new Peca(1, "BoxCollider2D", "comp.", "anil", "boxcollider", "Uma caixa invisível ao redor do objeto, que o Unity usa pra saber onde ele encosta. Só há colisão quando os dois objetos têm collider."),
                new Peca(7, "SpriteRenderer", "comp.", "anil", "spriterenderer", "Desenha o sprite na tela. Não tem forma física."),
                new Peca(8, "OnCollisionExit2D", "método", "rosa", "oncollisionenter2d", "Método chamado quando dois colliders sólidos param de se encostar."),
            },
        },
        new Def
        {
            grupo = "PuzzleJump", controle = "PuzzleControlJump", nome = "Pulo", ordem = 5,
            objeto = "Player", objetoIcone = "Assets/Images/player/player_square_final.png", script = "SCRIPT · PLAYER.CS",
            objetivo = "o Player pula só quando está no chão", previa = PuzzlePreview.PreviewType.Jump,
            codigo = new[]
            {
                "<kw>void</kw> Update() {",
                "    <kw>if</kw> (§0.GetButtonDown(§1)",
                "        && §2)",
                "        rb.§3 = <kw>new</kw> <ty>Vector2</ty>(rb.linearVelocity.x, pulo);",
                "}",
            },
            lacunas = new[]
            {
                new Lac(1, "Linha 2: quem escuta o jogador apertando um botão?"),
                new Lac(2, "Linha 2: qual botão do Input Manager é o pulo? Por padrão é a barra de espaço."),
                new Lac(3, "Linha 3: qual condição impede de pular no ar?"),
                new Lac(4, "Linha 4: o que joga o personagem pra cima usando a física?"),
            },
            pecas = new[]
            {
                new Peca(4, "linearVelocity", "prop.", "azul", "velocity", "Velocidade atual do Rigidbody2D, com x e y. Trocar o valor muda a velocidade do corpo na hora."),
                new Peca(6, "Start", "método", "lima", "start", "Roda uma vez, quando o objeto nasce."),
                new Peca(1, "Input", "classe", "rosa", "input", "Classe do Unity que lê teclado, mouse e controle."),
                new Peca(3, "isGrounded", "variável", "verde", "isgrounded", "Variável que diz se o personagem está pisando em algo: true no chão, false no ar."),
                new Peca(5, "Translate", "método", "lima", "translate", "Move o objeto mudando a posição direto, sem passar pela física."),
                new Peca(2, "\"Jump\"", "botão", "laranja", "jump", "Nome do botão de pulo no Input Manager (barra de espaço, por padrão). Input.GetButtonDown(\"Jump\") vale verdadeiro só no frame em que o botão é apertado."),
                new Peca(7, "gravityScale", "prop.", "azul", "gravityscale", "Quanto a gravidade puxa este objeto: 1 é a normal, 0 desliga."),
                new Peca(8, "\"Horizontal\"", "eixo", "laranja", "horizontal", "Nome do eixo esquerda/direita. Input.GetAxis(\"Horizontal\") devolve -1, 0 ou 1."),
            },
        },
    };

    private static readonly string[] NomesOrdem = { "Movimento", "Câmera", "Rigidbody2D", "Collider2D", "Pulo" };

    // ------------------------------------------------------------------
    // Estado da montagem
    // ------------------------------------------------------------------
    private static List<string> log;
    private static TMP_FontAsset fonteMono, fonteMonoForte, fonteCorpo, fonteCorpoForte;
    private static float larguraCharPorTamanho;   // largura de 1 caractere mono com fontSize 1
    private static int ordemBase;

    [MenuItem("Tools/Física/Aplicar código com lacunas")]
    public static void Menu()
    {
        Aplicar();
        foreach (string l in log) Debug.Log("[FisicaCodigoBuilder] " + l);
    }

    public static List<string> Aplicar() => Aplicar(true);

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
            log.Add("ERRO: a cena Fisica tem mudanças não salvas. Salve ou descarte antes de rodar.");
            return log;
        }

        TemaLaboratorio tema = TemaLaboratorio.Atual;
        fonteMono = tema != null ? tema.mono : null;
        fonteMonoForte = tema != null && tema.monoForte != null ? tema.monoForte : fonteMono;
        fonteCorpo = tema != null ? tema.corpo : null;
        fonteCorpoForte = tema != null && tema.corpoForte != null ? tema.corpoForte : fonteCorpo;
        larguraCharPorTamanho = MedirCharMono();

        GameFlowManager gfm = null;
        foreach (GameObject r in cena.GetRootGameObjects())
        {
            if (gfm == null) gfm = r.GetComponentInChildren<GameFlowManager>(true);
        }

        foreach (Def def in Defs)
        {
            GameObject grupo = cena.GetRootGameObjects().FirstOrDefault(g => g.name == def.grupo);
            if (grupo == null)
            {
                log.Add($"AVISO: grupo '{def.grupo}' não encontrado.");
                continue;
            }

            bool ativo = grupo.activeSelf;
            grupo.SetActive(true);
            Montar(cena, grupo, def);
            grupo.SetActive(ativo);
        }

        EditorSceneManager.MarkSceneDirty(cena);
        if (!salvar)
        {
            log.Add("Cena Fisica montada sem salvar (prévia).");
            return log;
        }
        EditorSceneManager.SaveScene(cena);
        log.Add("Cena Fisica salva.");
        if (abriAqui) EditorSceneManager.CloseScene(cena, true);
        return log;
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
    private static void Montar(Scene cena, GameObject grupo, Def def)
    {
        Transform cenario = grupo.transform.Find("CenarioPuzzle");
        SpriteRenderer fundo = cenario.Find("Fundo").GetComponent<SpriteRenderer>();
        Vector2 o = (Vector2)fundo.bounds.center - FundoReferencia;

        // Modelos (componentes, física, camada) antes de apagar o antigo.
        GameObject modeloZona = Object.Instantiate(grupo.GetComponentInChildren<SnapZone>(true).gameObject, grupo.transform);
        GameObject modeloPeca = Object.Instantiate(grupo.GetComponentInChildren<DragPiece>(true).gameObject, grupo.transform);
        modeloZona.name = "_modeloZona";
        modeloPeca.name = "_modeloPeca";
        LimparFilhos(modeloZona.transform);
        LimparFilhos(modeloPeca.transform);
        foreach (var t in modeloZona.GetComponentsInChildren<TMP_Text>(true)) Object.DestroyImmediate(t.gameObject);

        foreach (string velho in new[] { "Encaixar", "Peças", "Codigo", "Pecas", "Cabecalho" })
        {
            Transform t = grupo.transform.Find(velho);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        Transform card = cenario.Find("Card Encaixe");
        if (card != null) card.gameObject.SetActive(false);

        AjustarJanelas(cenario, def, o);
        Cabecalho(grupo.transform, def, o);

        // --- Código / Inspector com lacunas ---
        var raiz = NovoFilho(grupo.transform, "Codigo");
        var lacunas = new List<CodigoEncaixeUI.Lacuna>();
        var linhas = new List<CodigoEncaixeUI.Linha>();
        var zonas = new SnapZone[def.lacunas.Length];

        int maxRotulo = def.pecas.Max(p => p.rotulo.Length);
        Rect area = Desloca(AreaCodigo, o);

        if (def.inspector != null && def.codigo != null)
        {
            float meio = area.yMin + area.height * 0.55f;
            Rect cima = Rect.MinMaxRect(area.xMin, meio + 0.08f, area.xMax, area.yMax);
            Rect baixo = Rect.MinMaxRect(area.xMin, area.yMin, area.xMax, meio - 0.08f);
            Inspector(raiz, def, cima, maxRotulo, modeloZona, zonas, lacunas, linhas);
            Script(raiz, def, baixo, maxRotulo, modeloZona, zonas, lacunas, linhas, completar: false);
        }
        else if (def.inspector != null)
        {
            Inspector(raiz, def, area, maxRotulo, modeloZona, zonas, lacunas, linhas);
        }
        else
        {
            Script(raiz, def, area, maxRotulo, modeloZona, zonas, lacunas, linhas, completar: true);
        }

        // --- Peças ---
        Pecas(grupo.transform, def, o, modeloPeca);

        Object.DestroyImmediate(modeloZona);
        Object.DestroyImmediate(modeloPeca);

        // --- Prévia e progresso ---
        var (pontos, textoProg) = Previa(cenario, def, o);

        // --- Dica ---
        InfoPanelUI painel = cenario.Find("Descrição")?.GetComponent<InfoPanelUI>();
        string primeiraDica = def.lacunas[0].dica;
        if (painel != null) Dica(painel, primeiraDica);

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

        // --- PuzzleControl ---
        PuzzleControl controle = Object.FindObjectsByType<PuzzleControl>(FindObjectsInactive.Include)
            .FirstOrDefault(c => c.name == def.controle && c.gameObject.scene == cena);
        if (controle != null)
        {
            var cso = new SerializedObject(controle);
            cso.FindProperty("totalPieces").intValue = def.lacunas.Length;
            cso.FindProperty("segundosCodigo").floatValue = 3f;
            cso.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            log.Add($"AVISO: {def.controle} não encontrado.");
        }

        log.Add($"{def.grupo} ({def.nome}): {def.lacunas.Length} lacunas, {def.pecas.Length} peças, {linhas.Count} linhas.");
    }

    // Painel de Dica: os textos antigos tinham escala torta e outra fonte;
    // ficam desligados e o InfoPanelUI passa a usar dois textos novos.
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
    // Janelas, abas e cabeçalho
    // ------------------------------------------------------------------
    private static void AjustarJanelas(Transform cenario, Def def, Vector2 o)
    {
        Transform painel = cenario.Find("Painel");
        RedimensionarSprite(painel.Find("painel_1").GetComponent<SpriteRenderer>(), Desloca(JanelaEsq, o));
        Aba(painel.Find("aba_ativa"), def.objeto, new Vector2(JanelaEsq.xMin + 0.2f, JanelaEsq.yMax) + o, def.objeto.Length > 7 ? 1.9f : 1.4f);

        Transform painel2 = cenario.Find("Painel_2");
        SpriteRenderer janela2 = painel2.GetComponentsInChildren<SpriteRenderer>(true).First(s => s.name.StartsWith("painel_1"));
        RedimensionarSprite(janela2, Desloca(JanelaPecas, o));
        Aba(painel2.Find("aba_ativa"), "Peças", new Vector2(JanelaPecas.xMin + 0.2f, JanelaPecas.yMax) + o, 1.4f);

        Transform desc = cenario.Find("Descrição");
        Transform abaDesc = desc != null ? desc.Find("aba_ativa") : null;
        if (abaDesc != null) RotuloAba(abaDesc, "Dica");
    }

    // Aba de janela: encosta no topo da janela, à esquerda, e ganha o nome.
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
        // A aba é fina (só a borda de cima): o nome fica logo abaixo dela.
        var t = Texto(aba, "Rotulo", texto, new Vector2(b.min.x + 0.14f, b.center.y), 0f, fonteCorpoForte, Color.white, TextAlignmentOptions.Left, sr.sortingOrder + 1);
        AjustarAltura(t, 0.19f);
        ManterEscalaUm(t.transform);
    }

    private static void Cabecalho(Transform grupo, Def def, Vector2 o)
    {
        var raiz = NovoFilho(grupo, "Cabecalho");
        float y = 4.62f + o.y;
        var titulo = Texto(raiz, "Titulo", $"<b>Fase Física</b>  <color=#8A97AD>Puzzle {def.ordem} de 5 · {def.nome}</color>",
            new Vector2(-7.45f + o.x, y), 0f, fonteCorpo, Color.white, TextAlignmentOptions.Left, 10);
        AjustarAltura(titulo, 0.3f);

        var partes = new List<string>();
        for (int i = 0; i < NomesOrdem.Length; i++)
        {
            string n = $"{i + 1}. {NomesOrdem[i]}";
            if (i + 1 < def.ordem) partes.Add($"<color=#7EE2A8>{n}</color>");
            else if (i + 1 == def.ordem) partes.Add($"<color=#FFFFFF><b>{n}</b></color>");
            else partes.Add($"<color=#5D6B84>{n}</color>");
        }
        var passos = Texto(raiz, "Passos", string.Join("   ", partes), new Vector2(8.6f + o.x, y), 0f, fonteCorpo, Color.white, TextAlignmentOptions.Right, 10);
        AjustarAltura(passos, 0.22f);
    }

    // ------------------------------------------------------------------
    // Script
    // ------------------------------------------------------------------
    private static void Script(Transform raiz, Def def, Rect area, int maxRotulo, GameObject modeloZona, SnapZone[] zonas,
        List<CodigoEncaixeUI.Lacuna> lacunas, List<CodigoEncaixeUI.Linha> linhas, bool completar)
    {
        var sec = NovoFilho(raiz, "Script");
        Sprite painelSpr = Carregar(Kit + "cenario/janela_previa.png");
        Spr(sec, "Fundo", painelSpr, area.center, area.size, 1, Color.white);
        Legenda(sec, def.script, area);

        const float topo = 0.42f;
        float margemNum = 0.62f;
        float xCodigo = area.xMin + margemNum + 0.18f;
        float larguraUtil = area.xMax - 0.15f - xCodigo;

        // Tamanho da letra: a linha mais longa (lacuna contada pela maior
        // peça) tem que caber na largura.
        int maiorLinha = def.codigo.Max(l => ContarLinha(l, maxRotulo + 3));
        float cw = Mathf.Min(0.115f, larguraUtil / Mathf.Max(1, maiorLinha));
        float alturaLinha = Mathf.Clamp(cw * 5.2f, 0.42f, 0.56f);
        int totalLinhas = completar ? Mathf.FloorToInt((area.height - topo - 0.1f) / alturaLinha) : def.codigo.Length;
        totalLinhas = Mathf.Max(totalLinhas, def.codigo.Length);
        float fonte = cw / larguraCharPorTamanho;
        float lacW = (maxRotulo + 3) * cw;
        float lacH = alturaLinha * 0.8f;

        // Margem dos números, estilo Visual Studio.
        Spr(sec, "Margem", Carregar(Kit + "cenario/pixel_branco.png"),
            new Vector2(area.xMin + margemNum, area.center.y - topo * 0.5f + 0.05f), new Vector2(0.015f, area.height - topo - 0.2f), 2, new Color32(0x1E, 0x2A, 0x3E, 0xFF));

        for (int i = 0; i < totalLinhas; i++)
        {
            float y = area.yMax - topo - alturaLinha * (i + 0.5f);
            var linhaT = NovoFilho(sec, $"Linha {i + 1}");
            var num = Texto(linhaT, "Numero", (i + 1).ToString(), new Vector2(area.xMin + margemNum - 0.1f, y), fonte * 0.85f, fonteMono, CorNumero, TextAlignmentOptions.Right, 4);
            num.rectTransform.pivot = new Vector2(1f, 0.5f);
            num.transform.position = new Vector3(area.xMin + margemNum - 0.1f, y, 0f);

            int indiceLinha = linhas.Count;
            var info = new CodigoEncaixeUI.Linha();
            info.faixaAtual = Faixa(linhaT, new Rect(area.xMin + 0.05f, y - alturaLinha * 0.5f, area.width - 0.1f, alturaLinha));
            info.marcaOk = MarcaOk(linhaT, new Vector2(area.xMin + 0.22f, y), alturaLinha * 0.5f);
            linhas.Add(info);

            if (i < def.codigo.Length)
            {
                EscreverLinha(linhaT, def, def.codigo[i], xCodigo, y, cw, fonte, lacW, lacH, maxRotulo, modeloZona, zonas, lacunas, indiceLinha);
            }
        }
    }

    // ------------------------------------------------------------------
    // Inspector
    // ------------------------------------------------------------------
    private static void Inspector(Transform raiz, Def def, Rect area, int maxRotulo, GameObject modeloZona, SnapZone[] zonas,
        List<CodigoEncaixeUI.Lacuna> lacunas, List<CodigoEncaixeUI.Linha> linhas)
    {
        var sec = NovoFilho(raiz, "Inspector");
        Spr(sec, "Fundo", Carregar(Kit + "cenario/cartao_gameobject.png"), area.center, area.size, 1, Color.white);
        Legenda(sec, "INSPECTOR", area);

        const float topo = 0.42f;
        int n = def.inspector.Length;
        float alturaLinha = Mathf.Min(0.62f, (area.height - topo - 0.12f) / n);
        float cw = 0.105f;
        float fonte = cw / larguraCharPorTamanho;
        float lacW = (maxRotulo + 3) * cw;
        float lacH = Mathf.Min(alturaLinha * 0.8f, 0.5f);
        float xRotulo = area.xMin + 0.5f;
        float xValor = area.xMin + 2.85f;
        Sprite branco = Carregar(Kit + "cenario/pixel_branco.png");

        for (int i = 0; i < n; i++)
        {
            Linha l = def.inspector[i];
            float y = area.yMax - topo - alturaLinha * (i + 0.5f);
            var linhaT = NovoFilho(sec, $"Linha {i + 1} ({l.rotulo})");
            var linhaRect = new Rect(area.xMin + 0.08f, y - alturaLinha * 0.5f + 0.02f, area.width - 0.16f, alturaLinha - 0.04f);

            if (l.tipo == "cab" || l.tipo == "obj")
            {
                Spr(linhaT, "FundoLinha", branco, linhaRect.center, linhaRect.size, 2, l.tipo == "obj" ? new Color32(0x1A, 0x1F, 0x29, 0xFF) : new Color32(0x2A, 0x2F, 0x39, 0xFF));
            }

            int indiceLinha = linhas.Count;
            var info = new CodigoEncaixeUI.Linha();
            info.faixaAtual = Faixa(linhaT, linhaRect);
            info.marcaOk = MarcaOk(linhaT, new Vector2(area.xMin + 0.26f, y), alturaLinha * 0.42f);
            linhas.Add(info);

            float xTexto = xRotulo;
            if (l.tipo == "obj" || !string.IsNullOrEmpty(l.icone))
            {
                Sprite ic = l.tipo == "obj" ? Carregar(def.objetoIcone) : Carregar(Kit + "icones/componentes/icone_" + l.icone + ".png");
                float s = alturaLinha * 0.5f;
                Spr(linhaT, "Icone", ic, new Vector2(xRotulo + s * 0.5f, y), new Vector2(s, s), 4, Color.white);
                xTexto += s + 0.14f;
            }

            bool forte = l.tipo != "campo";
            var rot = Texto(linhaT, "Rotulo", forte ? $"<b>{l.rotulo}</b>" : l.rotulo, new Vector2(xTexto, y), 0f, forte ? fonteCorpoForte : fonteCorpo,
                forte ? Color.white : CorRotulo, TextAlignmentOptions.Left, 4);
            AjustarAltura(rot, l.tipo == "obj" ? 0.3f : 0.25f);

            if (string.IsNullOrEmpty(l.valor)) continue;

            if (l.valor.Contains("§"))
            {
                float x = l.tipo == "cab" ? xTexto + rot.preferredWidth + 0.15f : xValor;
                EscreverLinha(linhaT, def, l.valor, x, y, cw, fonte, lacW, lacH, maxRotulo, modeloZona, zonas, lacunas, indiceLinha);
            }
            else if (l.tipo == "campo")
            {
                Texto(linhaT, "Valor", l.valor, new Vector2(xValor, y), fonte, fonteMono, CorTexto, TextAlignmentOptions.Left, 4);
            }
            else
            {
                var v = Texto(linhaT, "Valor", l.valor, new Vector2(linhaRect.xMax - 0.15f, y), 0f, fonteCorpo,
                    l.valor.StartsWith("(") ? new Color32(0x7E, 0xE2, 0xA8, 0xFF) : CorRotulo, TextAlignmentOptions.Right, 4);
                v.rectTransform.pivot = new Vector2(1f, 0.5f);
                v.transform.position = new Vector3(linhaRect.xMax - 0.15f, y, 0f);
                AjustarAltura(v, 0.2f);
            }
        }
    }

    // ------------------------------------------------------------------
    // Uma linha com trechos de texto e lacunas (§n)
    // ------------------------------------------------------------------
    private static void EscreverLinha(Transform linhaT, Def def, string markup, float x, float y, float cw, float fonte, float lacW, float lacH,
        int maxRotulo, GameObject modeloZona, SnapZone[] zonas, List<CodigoEncaixeUI.Lacuna> lacunas, int indiceLinha)
    {
        int trecho = 0;
        foreach (string parte in Regex.Split(markup, @"(§\d)"))
        {
            if (string.IsNullOrEmpty(parte)) continue;

            if (parte.StartsWith("§"))
            {
                int idx = parte[1] - '0';
                Lac lac = def.lacunas[idx];
                Peca certa = def.pecas.First(p => p.id == lac.id);
                SnapZone zona = Lacuna(linhaT, modeloZona, $"Lacuna {idx + 1}", new Vector2(x + lacW * 0.5f, y), new Vector2(lacW, lacH), lac.id, certa, cw, fonte);
                zonas[idx] = zona;
                lacunas.Add(new CodigoEncaixeUI.Lacuna
                {
                    zona = zona,
                    destaque = zona.transform.Find("Destaque").gameObject,
                    linha = indiceLinha,
                    dica = lac.dica,
                });
                x += lacW + cw * 0.5f;
                continue;
            }

            string rich = parte.Replace("<kw>", $"<color={CorKw}>").Replace("</kw>", "</color>")
                .Replace("<ty>", $"<color={CorTy}>").Replace("</ty>", "</color>")
                .Replace("<mu>", $"<color={CorMu}>").Replace("</mu>", "</color>");
            string visivel = Regex.Replace(rich, "<[^>]+>", "");
            int espacos = visivel.Length - visivel.TrimStart(' ').Length;
            rich = rich.TrimStart(' ');
            string semEspacoInicio = visivel.TrimStart(' ');
            float xIni = x + espacos * cw;
            if (semEspacoInicio.Length > 0)
            {
                Texto(linhaT, $"Trecho {++trecho}", rich, new Vector2(xIni, y), fonte, fonteMono, CorTexto, TextAlignmentOptions.Left, 4);
            }
            x += visivel.Length * cw;
        }
    }

    // Conta os caracteres visíveis de uma linha, com cada lacuna valendo
    // "larguraLacuna" caracteres.
    private static int ContarLinha(string markup, int larguraLacuna)
    {
        string semTags = Regex.Replace(markup, "<[^>]+>", "");
        int lacunasNaLinha = Regex.Matches(semTags, @"§\d").Count;
        return Regex.Replace(semTags, @"§\d", "").Length + lacunasNaLinha * (larguraLacuna + 1);
    }

    private static SnapZone Lacuna(Transform pai, GameObject modelo, string nome, Vector2 centro, Vector2 tamanho, int id, Peca certa, float cw, float fonte)
    {
        GameObject go = Object.Instantiate(modelo, pai);
        go.name = nome;
        go.transform.localScale = Vector3.one;
        go.transform.rotation = Quaternion.identity;
        go.transform.position = new Vector3(centro.x, centro.y, 0f);
        ManterEscalaUm(go.transform);

        // 1º filho = visual (é o que a SnapZone usa como âncora).
        Spr(go.transform, "Fundo", Carregar(Kit + "pecas/encaixe_vazio.png"), centro, tamanho, 3, new Color(0.75f, 0.8f, 0.9f, 0.9f));
        var destaque = Spr(go.transform, "Destaque", Carregar(Kit + "pecas/peca_contorno_selecao.png"), centro, tamanho + new Vector2(0.12f, 0.1f), 4, new Color(1f, 1f, 1f, 0.95f));
        destaque.gameObject.SetActive(false);

        // A palavra certa, já escrita (aparece no lugar da lacuna ao encaixar).
        var pre = NovoFilho(go.transform, "Preenchido");
        Spr(pre, "Fundo", Carregar(Kit + "pecas/peca_velocity.png"), centro, tamanho, 5, Color.white);
        float ic = tamanho.y * 0.62f;
        Spr(pre, "icone", Carregar(Categorias + $"icone_{certa.icone}_{certa.cor}.png"), new Vector2(centro.x - tamanho.x * 0.5f + 0.16f + ic * 0.5f, centro.y), new Vector2(ic, ic), 6, Color.white);
        var t = Texto(pre, "Texto", $"<b>{certa.rotulo}</b>", new Vector2(centro.x - tamanho.x * 0.5f + 0.28f + ic, centro.y), fonte, fonteMonoForte, CorDourado, TextAlignmentOptions.Left, 6);
        float livre = tamanho.x - (0.4f + ic);
        if (t.preferredWidth > livre) t.fontSize *= livre / t.preferredWidth;
        pre.gameObject.SetActive(false);

        var zona = go.GetComponent<SnapZone>();
        var so = new SerializedObject(zona);
        so.FindProperty("correctPieceID").intValue = id;
        so.FindProperty("preenchido").objectReferenceValue = pre.gameObject;
        so.FindProperty("ajustarAosVizinhos").boolValue = false;
        so.FindProperty("folgaGatilho").vector2Value = new Vector2(0.35f, 0.18f);
        so.ApplyModifiedPropertiesWithoutUndo();

        var box = go.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.isTrigger = true;
            box.offset = Vector2.zero;
            box.size = tamanho + new Vector2(0.7f, 0.36f);
        }
        return zona;
    }

    // ------------------------------------------------------------------
    // Peças
    // ------------------------------------------------------------------
    private static void Pecas(Transform grupo, Def def, Vector2 o, GameObject modelo)
    {
        var raiz = NovoFilho(grupo, "Pecas");
        Rect janela = Desloca(JanelaPecas, o);
        float centroY = janela.center.y - 0.15f;
        int n = def.pecas.Length;
        float topoY = centroY + PassoPeca * (n - 1) * 0.5f;
        float xCentro = janela.center.x + 0.12f;
        float cwPeca = 0.112f;
        float fontePeca = cwPeca / larguraCharPorTamanho;

        for (int i = 0; i < n; i++)
        {
            Peca p = def.pecas[i];
            Vector2 c = new Vector2(xCentro, topoY - PassoPeca * i);

            GameObject go = Object.Instantiate(modelo, raiz);
            go.name = "peca_" + Regex.Replace(p.rotulo.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
            go.transform.localScale = Vector3.one;
            go.transform.rotation = Quaternion.identity;
            go.transform.position = new Vector3(c.x, c.y, 0f);
            ManterEscalaUm(go.transform);

            Color cor = CoresTipo[p.cor];
            Spr(go.transform, "Fundo", Carregar(Kit + "pecas/" + MolduraTipo[p.cor] + ".png"), c, new Vector2(LarguraPeca, AlturaPeca), 8, Color.white);
            float ic = AlturaPeca * 0.66f;
            float xIc = c.x - LarguraPeca * 0.5f + 0.32f + ic * 0.5f;
            Sprite icone = Carregar(Categorias + $"icone_{p.icone}_{p.cor}.png");
            Spr(go.transform, "icone_" + p.icone, icone, new Vector2(xIc, c.y), new Vector2(ic, ic), 9, Color.white);

            var rot = Texto(go.transform, "Texto", $"<b>{p.rotulo}</b>", new Vector2(xIc + ic * 0.5f + 0.16f, c.y), fontePeca, fonteMonoForte, Color.white, TextAlignmentOptions.Left, 10);

            var et = Texto(go.transform, "Etiqueta", p.etiqueta.ToUpperInvariant(), new Vector2(c.x + LarguraPeca * 0.5f - 0.18f, c.y), 0f, fonteCorpoForte, cor, TextAlignmentOptions.Right, 10);
            et.rectTransform.pivot = new Vector2(1f, 0.5f);
            et.transform.position = new Vector3(c.x + LarguraPeca * 0.5f - 0.18f, c.y, 0f);
            AjustarAltura(et, 0.15f);
            et.characterSpacing = 4f;

            float livre = (c.x + LarguraPeca * 0.5f - 0.3f - et.preferredWidth) - (xIc + ic * 0.5f + 0.16f) - 0.1f;
            rot.ForceMeshUpdate();
            if (rot.preferredWidth > livre) rot.fontSize *= livre / rot.preferredWidth;

            var drag = go.GetComponent<DragPiece>();
            var dso = new SerializedObject(drag);
            dso.FindProperty("pieceID").intValue = p.id;
            dso.ApplyModifiedPropertiesWithoutUndo();

            var hover = go.GetComponent<PieceHoverInfo>();
            if (hover != null)
            {
                var hso = new SerializedObject(hover);
                hso.FindProperty("title").stringValue = p.rotulo;
                hso.FindProperty("description").stringValue = p.descricao;
                hso.FindProperty("icon").objectReferenceValue = icone;
                hso.ApplyModifiedPropertiesWithoutUndo();
            }

            var box = go.GetComponent<BoxCollider2D>();
            if (box != null)
            {
                box.offset = Vector2.zero;
                box.size = new Vector2(LarguraPeca, AlturaPeca);
            }
        }
    }

    // ------------------------------------------------------------------
    // Prévia (janela do Objetivo) + progresso
    // ------------------------------------------------------------------
    private static (List<SpriteRenderer>, TMP_Text) Previa(Transform cenario, Def def, Vector2 o)
    {
        Transform painel = cenario.Find("Painel_Previa");
        Rect area = Desloca(AreaPrevia, o);
        SpriteRenderer janela = painel.Find("janela_previa").GetComponent<SpriteRenderer>();
        RedimensionarSprite(janela, area);

        // Título "Objetivo" + frase do objetivo.
        // O texto antigo tinha escala torta; é trocado por dois novos.
        foreach (var velhoT in painel.GetComponentsInChildren<TMP_Text>(true).Where(x => x.transform.parent == painel).ToList())
        {
            Object.DestroyImmediate(velhoT.gameObject);
        }
        var titulo = Texto(painel, "Titulo", "<b>Objetivo</b>", new Vector2(area.xMin + 0.2f, area.yMax - 0.3f), 0f, fonteCorpoForte, CorTitulo, TextAlignmentOptions.Left, 3);
        AjustarAltura(titulo, 0.26f);
        var frase = Texto(painel, "Frase", def.objetivo, new Vector2(area.xMin + 0.2f, area.yMax - 0.68f), 0f, fonteCorpo, new Color32(0xC9, 0xD6, 0xEA, 0xFF), TextAlignmentOptions.Left, 3);
        AjustarAltura(frase, 0.21f);
        if (frase.preferredWidth > area.width - 0.4f) frase.fontSize *= (area.width - 0.4f) / frase.preferredWidth;
        Transform div = painel.Find("divisoria");
        if (div != null) RedimensionarSprite(div.GetComponent<SpriteRenderer>(), new Rect(area.xMin + 0.18f, area.yMax - 0.98f, area.width - 0.36f, 0.02f));

        // Palco: entre o título e a faixa de progresso.
        Rect palco = Rect.MinMaxRect(area.xMin, area.yMin + 1.05f, area.xMax, area.yMax - 1.0f);
        MontarPalco(painel, janela, palco, def.previa);

        // Progresso.
        Transform velho = painel.Find("Progresso");
        if (velho != null) Object.DestroyImmediate(velho.gameObject);
        var prog = NovoFilho(painel, "Progresso");
        var pontos = new List<SpriteRenderer>();
        int n = def.lacunas.Length;
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

    private static void MontarPalco(Transform painel, SpriteRenderer janela, Rect palco, PuzzlePreview.PreviewType tipo)
    {
        var mask = janela.GetComponent<SpriteMask>();
        if (mask == null) mask = janela.gameObject.AddComponent<SpriteMask>();
        mask.sprite = janela.sprite;

        Transform velho = painel.Find("Preview");
        if (velho != null) Object.DestroyImmediate(velho.gameObject);
        var cont = NovoFilho(painel, "Preview");

        Sprite chao = Carregar("Assets/Images/Packs/Tecno/platform_tile_mid.png");
        Sprite parede = Carregar("Assets/Images/Packs/Tecno/wall_tile_left.png");
        Sprite player = Carregar("Assets/Images/player/player_square_final.png");

        const float ator = 0.45f;
        float chaoTopo = palco.yMin + 0.4f;
        float yAtor = chaoTopo + ator * 0.5f;

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

        float paredeX = palco.xMax;
        if (tipo == PuzzlePreview.PreviewType.CollideWallAndWalk)
        {
            float cx = palco.xMax - 0.4f;
            paredeX = cx - 0.2f;
            for (int k = 0; k < 4; k++)
            {
                var w = Spr(cont, "Parede", parede, new Vector2(cx, chaoTopo + 0.2f + 0.4f * k), new Vector2(0.4f, 0.4f), 2, Color.white);
                w.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }
        }

        var atorSr = Spr(cont, "Ator", player, new Vector2(palco.xMin + 0.7f, yAtor), new Vector2(ator, ator), 4, Color.white);
        atorSr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        Transform atorT = atorSr.transform;

        Vector2 a, b;
        switch (tipo)
        {
            case PuzzlePreview.PreviewType.MoveBackAndForth: a = new Vector2(palco.xMin + 0.6f, yAtor); b = new Vector2(palco.xMax - 0.6f, yAtor); break;
            case PuzzlePreview.PreviewType.Fall: a = new Vector2(palco.center.x, palco.yMax - 0.3f); b = new Vector2(palco.center.x, yAtor); break;
            case PuzzlePreview.PreviewType.CollideWallAndWalk: a = new Vector2(palco.xMin + 0.5f, yAtor); b = new Vector2(paredeX - ator * 0.5f, yAtor); break;
            case PuzzlePreview.PreviewType.CameraFollow: a = new Vector2(palco.xMin + 0.35f, yAtor); b = new Vector2(palco.xMax - 0.35f, yAtor); break;
            default: a = new Vector2(palco.center.x, yAtor); b = a; break;
        }

        Vector2 aL = cont.InverseTransformPoint(a);
        Vector2 bL = cont.InverseTransformPoint(b);
        atorT.localPosition = new Vector3(aL.x, aL.y, 0f);

        var prev = painel.GetComponent<PuzzlePreview>();
        if (prev == null) prev = painel.gameObject.AddComponent<PuzzlePreview>();
        var so = new SerializedObject(prev);
        so.FindProperty("type").enumValueIndex = (int)tipo;
        so.FindProperty("actor").objectReferenceValue = atorT;
        so.FindProperty("pointA").vector2Value = aL;
        so.FindProperty("pointB").vector2Value = bL;
        so.FindProperty("jumpHeight").floatValue = 1.4f;
        so.FindProperty("cameraFrame").objectReferenceValue = null;

        if (tipo == PuzzlePreview.PreviewType.CameraFollow)
        {
            // Colunas no fundo pra dar a ideia de sala maior que a tela.
            for (int k = 0; k < 4; k++)
            {
                float h = 0.5f + 0.35f * (k % 3);
                var col = Spr(cont, "Coluna", Carregar(Kit + "pecas/caixa_icone.png"), new Vector2(palco.xMin + 0.35f + k * 1.0f, chaoTopo + h * 0.5f), new Vector2(0.16f, h), 1, new Color32(0x2A, 0x33, 0x44, 0xFF));
                col.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }
            float meia = 0.8f;
            var molduraSr = Spr(cont, "MolduraCamera", Carregar(Kit + "cenario/moldura_selecao.png"), new Vector2(palco.xMin + meia + 0.08f, yAtor + 0.25f), new Vector2(1.6f, 1.25f), 5, new Color(0.98f, 0.45f, 0.09f, 1f));
            molduraSr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            Vector2 minL = cont.InverseTransformPoint(new Vector3(palco.xMin + meia + 0.08f, 0f, 0f));
            Vector2 maxL = cont.InverseTransformPoint(new Vector3(palco.xMax - meia - 0.08f, 0f, 0f));
            so.FindProperty("cameraFrame").objectReferenceValue = molduraSr.transform;
            so.FindProperty("frameLimits").vector2Value = new Vector2(minL.x, maxL.x);
            so.FindProperty("walkSpeed").floatValue = 1.2f;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------
    // Utilitários de desenho
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

    // Deixa a escala de MUNDO do objeto em 1 (o pai pode ter escala).
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
        sr.sortingLayerName = SortingLayer;
        sr.sortingOrder = ordem;
        go.transform.position = new Vector3(centro.x, centro.y, 0f);
        Vector2 nativo = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
        Vector3 paiEsc = pai.lossyScale;
        go.transform.localScale = new Vector3(tamanho.x / nativo.x / NaoZero(paiEsc.x), tamanho.y / nativo.y / NaoZero(paiEsc.y), 1f);
        return sr;
    }

    // Faz um sprite existente ocupar exatamente o retângulo (mundo).
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
        mr.sortingLayerName = SortingLayer;
        mr.sortingOrder = ordem;
        t.ForceMeshUpdate();
        return t;
    }

    // Ajusta o fontSize pra altura de letra maiúscula ≈ "altura" (mundo).
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
        var t = Texto(sec, "Legenda", texto, new Vector2(area.xMin + 0.2f, area.yMax - 0.2f), 0f, fonteCorpoForte, new Color32(0x6F, 0x7D, 0x95, 0xFF), TextAlignmentOptions.Left, 3);
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
