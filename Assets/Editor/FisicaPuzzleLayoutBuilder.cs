using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Aplica o layout novo do puzzle de Física ("PuzzleRig", feito à mão com o
/// EditorKit) nos demais puzzles da cena Fisica e cria o 4º puzzle (Pulo):
///
/// - PuzzleRig (Física): layout do próprio usuário — só liga as mecânicas
///   (IDs dos slots/peças, painel "Descrição" com hover, prévia do objetivo).
/// - Puzzle (Movimento), PuzzleCol (Colisão), PuzzleJump (Pulo): clones do
///   layout do PuzzleRig, re-skinados com as peças/ícones do EditorKit, com
///   os mesmos IDs de encaixe e descrições de hover que cada puzzle já tinha.
///   Os grupos antigos não são apagados: viram "<nome>_Antigo" (desativados).
///
/// Uso: Tools > Física > Aplicar Layout Novo nos Puzzles, com Fisica.unity
/// aberta. Idempotente — grupos que já estão no layout novo só são
/// reconfigurados, nunca clonados de novo.
/// </summary>
public static class FisicaPuzzleLayoutBuilder
{
    private const string PecasFolder = "Assets/Images/EditorKit/pecas/";
    private const string IconesFolder = "Assets/Images/EditorKit/icones/componentes/";
    private const string HintIconPath = "Assets/Images/EditorKit/icones/interface/icone_dica@2x.png";
    // Visor parado: corpo de 262 px a 256 ppu, pivô no centro do corpo.
    private const string PlayerSpritePath = "Assets/Anim/Player/Visor/idle/player_visor_idle_01.png";
    private const string GroundSpritePath = "Assets/Images/Packs/Tecno/platform_tile_mid.png";
    private const string WallSpritePath = "Assets/Images/Packs/Tecno/wall_tile_left.png";
    private const string FrameSpritePath = "Assets/Images/EditorKit/cenario/moldura_selecao.png";

    private class PieceDef
    {
        public string key;
        public string label;
        public int id;
        public string description;
        public string art;   // arte da peça/ícone (peca_<art>.png, icone_<art>.png); se vazio, usa key

        public string Art => string.IsNullOrEmpty(art) ? key : art;
    }

    private class PuzzleDef
    {
        public string groupName;
        public string controlName;
        public int[] zoneIds;
        public PieceDef[] pieces;
        public int[] existingPieceIds;   // só Física: mantém o conteúdo do usuário, ajusta apenas IDs
        public PuzzlePreview.PreviewType preview;
        public string hint;
        public string[] slotHints;   // texto dentro de cada encaixe vazio (mesma ordem de zoneIds)
        public string codeTitle;     // painel "código montado" ao concluir
        public string code;
        public string cardTitle;     // título do card (padrão: "Player")
    }

    private static List<string> log;

    private static PuzzleDef[] Definitions => new[]
    {
        // Os 4 puzzles montam, juntos, um único PlayerController: Movimento e
        // Pulo são linhas de código; Rigidbody2D e Collider2D são o Inspector.
        // A ordem dos encaixes (IDs 1..N) segue a ordem de leitura do código;
        // a ordem das peças abaixo é a ordem na bandeja (embaralhada).
        new PuzzleDef
        {
            // float h = Input.GetAxis("Horizontal");  Vector2 direcao = new Vector2(h, 0);
            // transform.position += direcao * velocidade * Time.deltaTime;
            groupName = "Puzzle", controlName = "PuzzleControl",
            zoneIds = new[] { 1, 2, 3, 4 },
            preview = PuzzlePreview.PreviewType.MoveBackAndForth,
            hint = "Dica: siga a ordem do código. Quem escuta o teclado, qual eixo ele lê, onde a direção fica guardada e o que muda de lugar a cada frame.",
            slotHints = new[] { "float h = ___.GetAxis(…);", "GetAxis(\"___\")", "direcao = new ___(h, 0);", "___ += direcao * vel * Time.deltaTime;" },
            codeTitle = "Código montado · Movimento",
            // Com o campo e o Update declarados: o aluno pode copiar pro Unity e compila.
            code = "public float velocidade = 5f;\n" +
                   "void Update()\n" +
                   "{\n" +
                   "      float h = <color=#F5C542>Input</color>.GetAxis(\"<color=#F5C542>Horizontal</color>\");\n" +
                   "      <color=#F5C542>Vector2</color> direcao = new Vector2(h, 0);\n" +
                   "      <color=#F5C542>transform.position</color> += (Vector3)direcao * velocidade * Time.deltaTime;\n" +
                   "}",
            pieces = new[]
            {
                new PieceDef { key = "transform", label = "transform.position", id = 4, description = "A posição do objeto no mundo. Somar a direção a ela, a cada frame, é o que faz o personagem andar." },
                new PieceDef { key = "vertical", label = "Vertical", id = 5, description = "Nome do eixo cima/baixo. Input.GetAxis(\"Vertical\") devolve -1 pra baixo, 0 parado e 1 pra cima." },
                new PieceDef { key = "input", label = "Input", id = 1, description = "Classe do Unity que lê teclado, mouse e controle. Sem ela, o jogo não sabe o que você apertou." },
                new PieceDef { key = "vector2", label = "Vector2", id = 3, description = "Dois números, x e y, que guardam uma direção ou um ponto." },
                new PieceDef { key = "vector3", label = "Vector3", id = 6, description = "Igual ao Vector2, mas com um terceiro número (z). É pra 3D; num jogo 2D, sobra." },
                new PieceDef { key = "horizontal", label = "Horizontal", id = 2, description = "Nome do eixo esquerda/direita. Input.GetAxis(\"Horizontal\") devolve -1 pra esquerda, 0 parado e 1 pra direita." },
            },
        },
        new PuzzleDef
        {
            // Player > Add Component > Rigidbody2D | Body Type: Dynamic | Gravity Scale | Constraints: Freeze Rotation
            groupName = "PuzzleRig", controlName = "PuzzleControlRig",
            zoneIds = new[] { 1, 2, 3, 4 },
            preview = PuzzlePreview.PreviewType.Fall,
            hint = "Dica: primeiro o componente que traz a física, depois o tipo de corpo que reage à gravidade, o quanto ela puxa e, por fim, o que impede o personagem de tombar.",
            slotHints = new[] { "Add Component → ___", "Tipo de corpo: ___", "Quanto a gravidade puxa: ___", "Constraints → ___ (Z)" },
            codeTitle = "Inspector montado · Rigidbody2D",
            code = "Player  ›  Add Component  ›  <color=#F5C542>Rigidbody2D</color>\n" +
                   "      <color=#F5C542>Body Type</color>: Dynamic\n" +
                   "      <color=#F5C542>Gravity Scale</color>: 4\n" +
                   "      Constraints  ›  <color=#F5C542>Freeze Rotation</color> Z\n" +
                   "<color=#8B95A7>// com Rigidbody2D, o Movimento passa a usar a física:</color>\n" +
                   "rb.<color=#F5C542>linearVelocity</color> = new Vector2(h * velocidade, rb.linearVelocity.y);",
            pieces = new[]
            {
                new PieceDef { key = "gravityscale", label = "Gravity Scale", id = 3, description = "Multiplica a gravidade neste objeto: 0 flutua, 1 é a gravidade normal, 4 cai bem mais rápido." },
                new PieceDef { key = "rigidbody2d", label = "Rigidbody2D", id = 1, description = "Componente que coloca o objeto sob a física 2D do Unity. Sem ele, gravidade e forças não fazem efeito." },
                new PieceDef { key = "mass", label = "Mass", id = 5, description = "Peso do objeto. Conta em empurrões e colisões, mas não muda a velocidade da queda: no Unity, tudo cai igual." },
                new PieceDef { key = "freezerotation", art = "transform", label = "Freeze Rotation", id = 4, description = "Trava o giro do personagem. Sem isso ele tombaria como um dado ao esbarrar em algo." },
                new PieceDef { key = "collider2d", label = "Collider2D", id = 6, description = "Dá ao objeto uma forma pra colisões." },
                new PieceDef { key = "bodytype", art = "rigidbody2d", label = "Body Type: Dynamic", id = 2, description = "Tipo de corpo que reage à gravidade e a forças. Static nunca se move; Kinematic só se move pelo código." },
            },
        },
        new PuzzleDef
        {
            // Player > BoxCollider2D | Is Trigger = false | OnCollisionEnter2D (avisa que pisou no chão)
            groupName = "PuzzleCol", controlName = "PuzzleControlCol",
            zoneIds = new[] { 1, 2, 3 },
            preview = PuzzlePreview.PreviewType.CollideWallAndWalk,
            hint = "Dica: primeiro a forma que sente o toque, depois se ela barra ou deixa passar e, por fim, o método que avisa quando dois colliders se encostam.",
            slotHints = new[] { "Add Component → ___", "Sólido ou só área? ___", "void ___(Collision2D c)" },
            codeTitle = "Montado · Collider2D",
            code = "Player  ›  Add Component  ›  <color=#F5C542>BoxCollider2D</color>\n" +
                   "      <color=#F5C542>Is Trigger</color> = false      (sólido, barra quem encosta)\n" +
                   "void <color=#F5C542>OnCollisionEnter2D</color>(Collision2D c)  →  encostou em algo sólido",
            pieces = new[]
            {
                new PieceDef { key = "ontriggerenter2d", label = "OnTriggerEnter2D", id = 5, description = "Método chamado quando algo entra num collider marcado como Trigger." },
                new PieceDef { key = "istrigger_true", art = "istrigger", label = "Is Trigger = true", id = 4, description = "Ligado, o collider vira só uma área que avisa quando alguém entra. Serve pra moedas e sensores, mas o personagem atravessaria o chão." },
                new PieceDef { key = "rigidbody2d", label = "Rigidbody2D", id = 6, description = "Componente que coloca o objeto sob a física 2D: gravidade e forças passam a agir nele." },
                new PieceDef { key = "oncollisionenter2d", label = "OnCollisionEnter2D", id = 3, description = "Método chamado no instante em que dois colliders sólidos se encostam. É por ele que o personagem sabe que pisou no chão." },
                new PieceDef { key = "boxcollider", label = "BoxCollider2D", id = 1, description = "Uma caixa invisível ao redor do objeto, que o Unity usa pra saber onde ele encosta. Só há colisão quando os dois objetos têm collider." },
                new PieceDef { key = "istrigger_false", art = "istrigger", label = "Is Trigger = false", id = 2, description = "Desligado, o collider é sólido e barra quem encosta. É assim que o chão segura o personagem." },
            },
        },
        new PuzzleDef
        {
            // if (Input.GetButtonDown("Jump") && isGrounded)
            //     rb.linearVelocity = new Vector2(rb.linearVelocity.x, pulo);
            groupName = "PuzzleJump", controlName = "PuzzleControlJump",
            zoneIds = new[] { 1, 2, 3, 4 },
            preview = PuzzlePreview.PreviewType.Jump,
            hint = "Dica: quem escuta o botão, qual botão é, a condição pra poder pular e o que joga o personagem pra cima.",
            slotHints = new[] { "if (___.GetButtonDown(…)", "GetButtonDown(\"___\")", "&& ___)", "rb.___ = new Vector2(…, pulo);" },
            codeTitle = "Código montado · Pulo",
            code = "public float pulo = 7f;\n" +
                   "public bool isGrounded;   // fica true enquanto pisa no chão (Colisão)\n" +
                   "Rigidbody2D rb;\n" +
                   "void Start() { rb = GetComponent<Rigidbody2D>(); }\n" +
                   "void Update()\n" +
                   "{\n" +
                   "      if (<color=#F5C542>Input</color>.GetButtonDown(\"<color=#F5C542>Jump</color>\") && <color=#F5C542>isGrounded</color>)\n" +
                   "            rb.<color=#F5C542>linearVelocity</color> = new Vector2(rb.linearVelocity.x, pulo);\n" +
                   "}",
            pieces = new[]
            {
                new PieceDef { key = "velocity", label = "linearVelocity", id = 4, description = "Velocidade atual do Rigidbody2D, com x e y. Trocar o valor muda a velocidade do corpo na hora." },
                new PieceDef { key = "start", label = "Start", id = 6, description = "Roda uma vez, quando o objeto nasce." },
                new PieceDef { key = "input", label = "Input", id = 1, description = "Classe do Unity que lê teclado, mouse e controle." },
                new PieceDef { key = "isgrounded", art = "collider2d", label = "isGrounded", id = 3, description = "Variável que diz se o personagem está pisando em algo: true no chão, false no ar." },
                new PieceDef { key = "translate", label = "Translate", id = 5, description = "Move o objeto mudando a posição direto, sem passar pela física." },
                new PieceDef { key = "jump", label = "Jump", id = 2, description = "Nome do botão de pulo no Input Manager (barra de espaço, por padrão). Input.GetButtonDown(\"Jump\") vale verdadeiro só no frame em que o botão é apertado." },
            },
        },
        CameraDef,
    };

    // Puzzle da câmera (CameraFollow na Main Camera). Aberto quando o
    // personagem sai da tela logo depois do puzzle de Movimento.
    private static PuzzleDef CameraDef => new PuzzleDef
    {
        // public Transform target;  void LateUpdate() { SmoothDamp(...); Mathf.Clamp(x, min, max); }
        groupName = "PuzzleCam", controlName = "PuzzleControlCam",
        zoneIds = new[] { 1, 2, 3, 4 },
        preview = PuzzlePreview.PreviewType.CameraFollow,
        cardTitle = "Main Camera",
        hint = "Dica: primeiro quem a câmera segue, depois em que momento do frame ela anda, como ela chega até o alvo e até onde ela pode ir.",
        slotHints = new[] { "public Transform ___;", "void ___()", "Vector3.___(pos, alvo, …)", "Mathf.Clamp(x, ___)" },
        codeTitle = "Código montado · CameraFollow",
        code = "public Transform <color=#F5C542>target</color>;             // arraste o Player aqui\n" +
               "public float minX = 0f, maxX = 20f;  // bordas da sala\n" +
               "Vector3 vel;                         // o SmoothDamp guarda a velocidade aqui\n" +
               "void <color=#F5C542>LateUpdate</color>()\n" +
               "{\n" +
               "      Vector3 pos = Vector3.<color=#F5C542>SmoothDamp</color>(transform.position, target.position, ref vel, 0.25f);\n" +
               "      pos.x = Mathf.Clamp(pos.x, <color=#F5C542>minX, maxX</color>);\n" +
               "      transform.position = new Vector3(pos.x, pos.y, -10);\n" +
               "}",
        pieces = new[]
        {
            new PieceDef { key = "update", label = "Update", id = 5, description = "Roda uma vez a cada frame, junto com o Update dos outros objetos, sem ordem garantida entre eles." },
            new PieceDef { key = "target", art = "transform", label = "target", id = 1, description = "Campo público do tipo Transform. Aparece no Inspector, onde dá pra arrastar um objeto pra ele." },
            new PieceDef { key = "limites", art = "boxcollider", label = "Limites (min/max)", id = 4, description = "Dois números, um mínimo e um máximo. Mathf.Clamp(valor, min, max) mantém o valor entre os dois." },
            new PieceDef { key = "grudar", art = "transform", label = "position = alvo", id = 6, description = "transform.position = alvo copia a posição do alvo de uma vez, no mesmo frame." },
            new PieceDef { key = "smoothdamp", art = "velocity", label = "SmoothDamp", id = 3, description = "Leva um valor até o alvo aos poucos, acelerando e freando no caminho." },
            new PieceDef { key = "lateupdate", art = "update", label = "LateUpdate", id = 2, description = "Roda uma vez a cada frame, depois que o Update de todos os objetos já rodou." },
        },
    };

    [MenuItem("Tools/Física/Atualizar Conteúdo dos Puzzles (peças, encaixes e dicas)")]
    public static void UpdateContentMenu()
    {
        UpdateContent();
        foreach (string line in log) Debug.Log("[FisicaPuzzleLayoutBuilder] " + line);
    }

    /// <summary>
    /// Só o conteúdo: IDs dos encaixes, rótulo/arte/ID/descrição das peças e
    /// a dica do painel Descrição. Não clona, não refaz a prévia nem mexe em
    /// posição, tamanho ou ativo/desativado de nada — seguro pra rodar depois
    /// de ajustes visuais feitos à mão.
    /// </summary>
    public static List<string> UpdateContent()
    {
        log = new List<string>();
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Fisica")
        {
            log.Add("ERRO: abra a cena Fisica antes de rodar (cena ativa: " + scene.name + ").");
            return log;
        }

        foreach (PuzzleDef def in Definitions)
        {
            GameObject group = FindRoot(def.groupName);
            if (group == null)
            {
                log.Add($"AVISO: grupo '{def.groupName}' não encontrado.");
                continue;
            }

            // TMP só mede texto direito com o objeto ativo (ver FitLabel).
            bool wasActive = group.activeSelf;
            group.SetActive(true);

            ConfigureZones(group, def);
            ConfigurePieces(group, def, reskin: true);
            ConfigureInfoPanel(group, def);
            ConfigureCard(group, def);
            WireCode(def);

            group.SetActive(wasActive);
            log.Add($"{def.groupName}: conteúdo atualizado ({def.zoneIds.Length} encaixes, {def.pieces.Length} peças).");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add("Cena salva.");
        return log;
    }

    /// <summary>
    /// Só cria o puzzle da câmera (clone do PuzzleRig), sem refazer a prévia
    /// nem o layout dos outros quatro. Depois atualiza o conteúdo de todos
    /// (dicas dentro dos encaixes e código montado). Idempotente.
    /// </summary>
    [MenuItem("Tools/Física/Criar Puzzle da Câmera")]
    public static void CreateCameraPuzzleMenu()
    {
        CreateCameraPuzzle();
        foreach (string line in log) Debug.Log("[FisicaPuzzleLayoutBuilder] " + line);
    }

    public static List<string> CreateCameraPuzzle()
    {
        var output = new List<string>();
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Fisica")
        {
            output.Add("ERRO: abra a cena Fisica antes de rodar (cena ativa: " + scene.name + ").");
            log = output;
            return log;
        }

        log = output;
        GameObject rig = FindRoot("PuzzleRig");
        if (rig == null)
        {
            log.Add("ERRO: GameObject 'PuzzleRig' (o layout-modelo) não foi encontrado na cena.");
            return log;
        }

        PuzzleDef def = CameraDef;
        bool rigWasActive = rig.activeSelf;
        rig.SetActive(true);
        GameObject group = EnsureGroupFromTemplate(rig, def);
        rig.SetActive(rigWasActive);
        group.SetActive(true);
        ApplyDefinition(group, def);
        WireControl(group, def);
        WireCode(def);
        group.SetActive(false);
        WireFlowManager();

        // UpdateContent troca o "log" por uma lista nova — junta as duas.
        List<string> content = UpdateContent();
        output.AddRange(content);
        log = output;
        return log;
    }

    [MenuItem("Tools/Física/Aplicar Layout Novo nos Puzzles")]
    public static void BuildMenu()
    {
        BuildAll();
        foreach (string line in log) Debug.Log("[FisicaPuzzleLayoutBuilder] " + line);
    }

    public static List<string> BuildAll()
    {
        log = new List<string>();
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Fisica")
        {
            log.Add("ERRO: abra a cena Fisica antes de rodar (cena ativa: " + scene.name + ").");
            return log;
        }

        GameObject rig = FindRoot("PuzzleRig");
        if (rig == null)
        {
            log.Add("ERRO: GameObject 'PuzzleRig' (o layout-modelo) não foi encontrado na cena.");
            return log;
        }

        var defs = Definitions;
        PuzzleDef rigDef = defs.First(d => d.groupName == "PuzzleRig");

        // 1) Liga as mecânicas no próprio PuzzleRig — ele é o modelo dos clones.
        ApplyDefinition(rig, rigDef);

        // 2) Clona o layout pros demais e re-skina.
        foreach (PuzzleDef def in defs)
        {
            if (def == rigDef)
            {
                continue;
            }

            GameObject group = EnsureGroupFromTemplate(rig, def);
            ApplyDefinition(group, def);
            WireControl(group, def);
            WireCode(def);

            // Como os grupos originais (Puzzle/PuzzleCol): começam desligados
            // na cena salva — o PuzzleControl liga cada um na sua hora.
            group.SetActive(false);
        }

        WireControl(rig, rigDef);
        WireCode(rigDef);
        WireFlowManager();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add("Cena salva.");
        return log;
    }

    // ------------------------------------------------------------------
    // Clonagem
    // ------------------------------------------------------------------

    private static GameObject EnsureGroupFromTemplate(GameObject rig, PuzzleDef def)
    {
        GameObject group = FindRoot(def.groupName);
        bool alreadyNewLayout = group != null && group.transform.Find("CenarioPuzzle/Painel_Previa") != null;
        if (alreadyNewLayout)
        {
            log.Add($"{def.groupName}: já está no layout novo — só reconfigurando.");
            return group;
        }

        if (group != null)
        {
            group.name = def.groupName + "_Antigo";
            group.SetActive(false);
            log.Add($"{def.groupName}: layout antigo preservado como '{group.name}' (desativado).");
        }

        // Fica ativo durante a configuração (medir texto do TMP num objeto
        // inativo é pouco confiável) — BuildAll desativa no final.
        GameObject clone = Object.Instantiate(rig);
        clone.name = def.groupName;
        Undo.RegisterCreatedObjectUndo(clone, "Clonar layout " + def.groupName);
        log.Add($"{def.groupName}: clonado do layout do PuzzleRig.");
        return clone;
    }

    // ------------------------------------------------------------------
    // Configuração de um grupo (peças, slots, painel de descrição, prévia)
    // ------------------------------------------------------------------

    private static void ApplyDefinition(GameObject group, PuzzleDef def)
    {
        bool reskin = def.pieces != null && def.pieces.Length > 0;

        ConfigureZones(group, def);
        ConfigurePieces(group, def, reskin);
        ConfigureInfoPanel(group, def);
        ConfigureCard(group, def);
        BuildPreview(group, def.preview);
        log.Add($"{def.groupName}: configurado ({def.zoneIds.Length} slots, prévia {def.preview}).");
    }

    private static void ConfigureZones(GameObject group, PuzzleDef def)
    {
        Transform encaixar = group.transform.Find("Encaixar");
        var zones = encaixar.GetComponentsInChildren<SnapZone>(true)
            .OrderBy(z => z.transform.GetSiblingIndex())
            .ToList();

        for (int i = 0; i < zones.Count; i++)
        {
            if (i >= def.zoneIds.Length)
            {
                Object.DestroyImmediate(zones[i].gameObject);
                continue;
            }

            var so = new SerializedObject(zones[i]);
            so.FindProperty("correctPieceID").intValue = def.zoneIds[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            string slotHint = def.slotHints != null && i < def.slotHints.Length ? def.slotHints[i] : null;
            ConfigureSlotHint(zones[i], slotHint);
        }
    }

    // Texto dentro do encaixe vazio mostrando onde a peça entra na linha de
    // código. Fica na layer Puzzle entre o encaixe (ordem 2) e as peças (6),
    // então some por baixo de uma peça arrastada por cima; a SnapZone
    // esconde quando encaixa.
    private static void ConfigureSlotHint(SnapZone zone, string text)
    {
        Transform existing = zone.transform.Find("DicaCodigo");
        if (string.IsNullOrEmpty(text))
        {
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            return;
        }

        SpriteRenderer slotSprite = zone.GetComponentInChildren<SpriteRenderer>();
        GameObject go = existing != null ? existing.gameObject : new GameObject("DicaCodigo", typeof(RectTransform));
        go.transform.SetParent(zone.transform, false);

        var tmp = go.GetComponent<TextMeshPro>();
        if (tmp == null) tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 5f;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 2f;
        tmp.fontSizeMax = 5f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.color = new Color(0.78f, 0.84f, 0.93f, 0.75f);
        tmp.fontStyle = FontStyles.Italic;

        var rt = (RectTransform)go.transform;
        if (slotSprite != null)
        {
            Bounds b = slotSprite.bounds;
            rt.position = new Vector3(b.center.x, b.center.y, zone.transform.position.z);
            // A SnapZone costuma ter escala desigual (ex: 1,65 x 0,73): anula
            // a escala herdada pra o texto não sair esticado, e mede o
            // retângulo direto em unidades de mundo.
            Vector3 lossy = zone.transform.lossyScale;
            rt.localScale = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)), 1f / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)), 1f);
            rt.sizeDelta = new Vector2(b.size.x - 0.5f, b.size.y - 0.25f);
        }

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null && slotSprite != null)
        {
            mr.sortingLayerID = slotSprite.sortingLayerID;
            mr.sortingOrder = slotSprite.sortingOrder + 1;
        }
    }

    private static void ConfigurePieces(GameObject group, PuzzleDef def, bool reskin)
    {
        Transform pecas = group.transform.Find("Peças");
        int count = pecas.childCount;

        for (int i = 0; i < count; i++)
        {
            Transform piece = pecas.GetChild(i);
            var drag = piece.GetComponent<DragPiece>();

            if (!reskin)
            {
                // Física: layout e textos do usuário — só corrige o ID.
                var so0 = new SerializedObject(drag);
                so0.FindProperty("pieceID").intValue = def.existingPieceIds[i];
                so0.ApplyModifiedPropertiesWithoutUndo();
                continue;
            }

            PieceDef p = def.pieces[i];
            piece.name = "peca_" + p.key;

            var so = new SerializedObject(drag);
            so.FindProperty("pieceID").intValue = p.id;
            so.ApplyModifiedPropertiesWithoutUndo();

            SpriteRenderer fundo = null, icone = null;
            TMP_Text label = null;
            foreach (Transform child in piece)
            {
                var csr = child.GetComponent<SpriteRenderer>();
                if (csr != null)
                {
                    if (child.name.StartsWith("icone")) icone = csr;
                    else fundo = csr;
                }

                var ctmp = child.GetComponent<TMP_Text>();
                if (ctmp != null) label = ctmp;
            }

            Sprite pecaSprite = LoadSprite(PecasFolder + "peca_" + p.Art + ".png");
            Sprite iconeSprite = LoadSprite(IconesFolder + "icone_" + p.Art + ".png");
            if (fundo != null && pecaSprite != null) fundo.sprite = pecaSprite;
            if (icone != null)
            {
                if (iconeSprite != null) icone.sprite = iconeSprite;
                icone.gameObject.name = "icone_" + p.Art;
            }
            if (label != null) FitLabel(label, p.label);

            var hover = piece.GetComponent<PieceHoverInfo>();
            var hso = new SerializedObject(hover);
            hso.FindProperty("title").stringValue = p.label;
            hso.FindProperty("description").stringValue = p.description;
            hso.FindProperty("icon").objectReferenceValue = iconeSprite;
            hso.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // Texto maior que a largura do rótulo estoura a peça — encolhe só o
    // necessário (o tamanho-base, 8, é o das peças originais).
    private static void FitLabel(TMP_Text tmp, string text)
    {
        tmp.text = text;
        tmp.fontSize = 8f;
        tmp.ForceMeshUpdate();

        float available = tmp.rectTransform.rect.width;
        float preferred = tmp.preferredWidth;
        if (preferred > available && preferred > 0f)
        {
            tmp.fontSize = Mathf.Max(4f, 8f * available / preferred * 0.97f);
            log.Add($"  rótulo '{text}': largura {preferred:F2} > {available:F2} — fonte reduzida para {tmp.fontSize:F1}.");
        }
    }

    // Título do card do objeto que recebe as peças ("Player" nos puzzles do
    // personagem, "Main Camera" no da câmera).
    private static void ConfigureCard(GameObject group, PuzzleDef def)
    {
        Transform card = group.transform.Find("CenarioPuzzle/Card Encaixe");
        if (card == null) return;

        string title = string.IsNullOrEmpty(def.cardTitle) ? "Player" : def.cardTitle;
        foreach (Transform child in card)
        {
            var t = child.GetComponent<TMP_Text>();
            if (t != null) t.text = title;
            if (child.name == "player_parado") child.gameObject.SetActive(title == "Player");
        }
    }

    // Título e trecho do painel "código montado" no PuzzleControl do grupo.
    private static void WireCode(PuzzleDef def)
    {
        PuzzleControl control = FindControl(def.controlName);
        if (control == null) return;

        var so = new SerializedObject(control);
        so.FindProperty("tituloCodigo").stringValue = def.codeTitle ?? "";
        so.FindProperty("codigoMontado").stringValue = def.code ?? "";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static PuzzleControl FindControl(string controlName)
    {
        foreach (var c in Object.FindObjectsByType<PuzzleControl>(FindObjectsInactive.Include))
        {
            if (c.name == controlName) return c;
        }
        return null;
    }

    private static void ConfigureInfoPanel(GameObject group, PuzzleDef def)
    {
        Transform descricao = group.transform.Find("CenarioPuzzle/Descrição");
        if (descricao == null)
        {
            log.Add($"AVISO: {def.groupName} não tem 'CenarioPuzzle/Descrição'.");
            return;
        }

        Transform textRoot = descricao.Find("Text");
        TMP_Text title = textRoot != null && textRoot.childCount > 0 ? textRoot.GetChild(0).GetComponent<TMP_Text>() : null;
        TMP_Text body = textRoot != null && textRoot.childCount > 1 ? textRoot.GetChild(1).GetComponent<TMP_Text>() : null;

        SpriteRenderer icon = null;
        foreach (Transform child in descricao)
        {
            if (child.name.StartsWith("icone"))
            {
                icon = child.GetComponent<SpriteRenderer>();
                break;
            }
        }

        var panel = descricao.GetComponent<InfoPanelUI>();
        if (panel == null) panel = descricao.gameObject.AddComponent<InfoPanelUI>();

        Sprite hintIcon = LoadSprite(HintIconPath);

        var so = new SerializedObject(panel);
        so.FindProperty("externalTitle").objectReferenceValue = title;
        so.FindProperty("externalDescription").objectReferenceValue = body;
        so.FindProperty("externalIcon").objectReferenceValue = icon;
        so.FindProperty("defaultIcon").objectReferenceValue = hintIcon;
        so.FindProperty("defaultTitle").stringValue = "Dica";
        so.FindProperty("defaultDescription").stringValue = def.hint;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Mostra a dica já no estado salvo (o Editor não roda Clear()).
        if (title != null) title.text = "Dica";
        if (body != null)
        {
            body.text = def.hint;
            body.ForceMeshUpdate();
            float needed = body.preferredHeight;
            float available = body.rectTransform.rect.height;
            if (needed > available)
            {
                log.Add($"  AVISO: dica de {def.groupName} precisa de altura {needed:F2} > {available:F2} disponível na caixa de descrição — aumente a caixa ou reduza o texto.");
            }
        }
        if (icon != null)
        {
            icon.sprite = hintIcon;
            icon.enabled = hintIcon != null;
        }
    }

    // ------------------------------------------------------------------
    // Prévia do objetivo (painel_previa)
    // ------------------------------------------------------------------

    private static void BuildPreview(GameObject group, PuzzlePreview.PreviewType type)
    {
        Transform painel = group.transform.Find("CenarioPuzzle/Painel_Previa");
        Transform janela = painel.Find("janela_previa");
        var windowSr = janela.GetComponent<SpriteRenderer>();

        // Máscara: tudo da prévia só aparece dentro da janela (o personagem
        // "cai" pra fora dela sem invadir os outros painéis).
        var mask = janela.GetComponent<SpriteMask>();
        if (mask == null) mask = janela.gameObject.AddComponent<SpriteMask>();
        mask.sprite = windowSr.sprite;

        Transform old = painel.Find("Preview");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var containerGO = new GameObject("Preview");
        containerGO.transform.SetParent(painel, false);
        containerGO.transform.localPosition = Vector3.zero;
        Transform container = containerGO.transform;

        Bounds b = windowSr.bounds;
        const float actorScale = 0.45f;
        float halfActor = actorScale * 0.5f;
        float groundTop = b.min.y + 0.8f;
        float restY = groundTop + halfActor;

        bool hasGround = type != PuzzlePreview.PreviewType.Fall;
        bool hasFrame = type == PuzzlePreview.PreviewType.CameraFollow;
        bool hasWall = type == PuzzlePreview.PreviewType.CollideWallAndWalk;

        Sprite groundSprite = LoadSprite(GroundSpritePath);
        Sprite wallSprite = LoadSprite(WallSpritePath);
        Sprite playerSprite = LoadSprite(PlayerSpritePath);

        if (hasGround)
        {
            for (float x = b.min.x + 0.2f; x < b.max.x; x += 0.4f)
            {
                CreateSprite(container, "Chao", groundSprite, new Vector3(x, groundTop - 0.4f, 0f), Vector3.one, windowSr, 2);
            }
        }

        float wallFaceX = b.max.x;
        if (hasWall)
        {
            float wallCenterX = b.max.x - 0.45f;
            wallFaceX = wallCenterX - 0.4f;
            for (int k = 0; k < 4; k++)
            {
                CreateSprite(container, "Parede", wallSprite, new Vector3(wallCenterX, groundTop + 0.4f + 0.8f * k, 0f), Vector3.one, windowSr, 2);
            }
        }

        Vector3 actorStart = new Vector3(b.min.x + 0.9f, restY, 0f);
        Transform actor = CreateSprite(container, "Ator", playerSprite, actorStart, Vector3.one * (actorScale / 1.0234f), windowSr, 4);

        Vector2 a, bPoint;
        switch (type)
        {
            case PuzzlePreview.PreviewType.MoveBackAndForth:
                a = new Vector2(b.min.x + 0.9f, restY);
                bPoint = new Vector2(b.max.x - 0.9f, restY);
                break;
            case PuzzlePreview.PreviewType.Fall:
                a = new Vector2(b.center.x, b.max.y - 1.1f);
                bPoint = new Vector2(b.center.x, b.min.y - 0.7f);
                break;
            case PuzzlePreview.PreviewType.CollideWallAndWalk:
                a = new Vector2(b.min.x + 0.7f, restY);
                bPoint = new Vector2(wallFaceX - halfActor, restY);
                break;
            case PuzzlePreview.PreviewType.CameraFollow:
                a = new Vector2(b.min.x + 0.4f, restY);
                bPoint = new Vector2(b.max.x - 0.4f, restY);
                break;
            default: // Jump
                a = new Vector2(b.center.x, restY);
                bPoint = a;
                break;
        }

        // Pontos no espaço local do contêiner (= pai do Ator).
        Vector2 aLocal = container.InverseTransformPoint(new Vector3(a.x, a.y, 0f));
        Vector2 bLocal = container.InverseTransformPoint(new Vector3(bPoint.x, bPoint.y, 0f));
        actor.localPosition = new Vector3(aLocal.x, aLocal.y, 0f);

        var preview = painel.GetComponent<PuzzlePreview>();
        if (preview == null) preview = painel.gameObject.AddComponent<PuzzlePreview>();

        var so = new SerializedObject(preview);
        so.FindProperty("type").enumValueIndex = (int)type;
        so.FindProperty("actor").objectReferenceValue = actor;
        so.FindProperty("pointA").vector2Value = aLocal;
        so.FindProperty("pointB").vector2Value = bLocal;
        so.FindProperty("jumpHeight").floatValue = 1.5f;

        // Câmera: uma moldura (o que a câmera enxerga) que segue o ator e
        // para perto das bordas da janela.
        if (hasFrame)
        {
            Sprite frameSprite = LoadSprite(FrameSpritePath);
            Transform frame = CreateSprite(container, "MolduraCamera", frameSprite, new Vector3(a.x, restY + 0.1f, 0f), Vector3.one, windowSr, 5);
            var fsr = frame.GetComponent<SpriteRenderer>();
            fsr.drawMode = SpriteDrawMode.Sliced;
            fsr.size = new Vector2(1.9f, 1.3f);
            fsr.color = new Color(0.96f, 0.77f, 0.26f, 1f);

            float halfFrame = 0.95f;
            Vector2 minLocal = container.InverseTransformPoint(new Vector3(b.min.x + halfFrame + 0.1f, 0f, 0f));
            Vector2 maxLocal = container.InverseTransformPoint(new Vector3(b.max.x - halfFrame - 0.1f, 0f, 0f));
            so.FindProperty("cameraFrame").objectReferenceValue = frame;
            so.FindProperty("frameLimits").vector2Value = new Vector2(minLocal.x, maxLocal.x);
            so.FindProperty("walkSpeed").floatValue = 1.2f;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform CreateSprite(Transform parent, string name, Sprite sprite, Vector3 worldPos, Vector3 scale, SpriteRenderer sortingReference, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos;
        go.transform.localScale = scale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerID = sortingReference.sortingLayerID;
        sr.sortingOrder = order;
        sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        return go.transform;
    }

    // ------------------------------------------------------------------
    // Ligações (PuzzleControl / GameFlowManager)
    // ------------------------------------------------------------------

    private static void WireControl(GameObject group, PuzzleDef def)
    {
        PuzzleControl control = null;
        foreach (var c in Object.FindObjectsByType<PuzzleControl>(FindObjectsInactive.Include))
        {
            if (c.name == def.controlName) control = c;
        }

        var gfm = Object.FindAnyObjectByType<GameFlowManager>();
        if (control == null)
        {
            var go = new GameObject(def.controlName);
            Undo.RegisterCreatedObjectUndo(go, "Criar " + def.controlName);
            go.transform.position = gfm != null ? gfm.transform.position : Vector3.zero;
            control = go.AddComponent<PuzzleControl>();
            log.Add($"{def.controlName}: criado.");
        }

        var so = new SerializedObject(control);
        so.FindProperty("puzzle").objectReferenceValue = group;
        so.FindProperty("totalPieces").intValue = def.zoneIds.Length;
        so.FindProperty("flowManager").objectReferenceValue = gfm;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireFlowManager()
    {
        var gfm = Object.FindAnyObjectByType<GameFlowManager>();
        PuzzleControl jump = null;
        foreach (var c in Object.FindObjectsByType<PuzzleControl>(FindObjectsInactive.Include))
        {
            if (c.name == "PuzzleControlJump") jump = c;
        }

        var so = new SerializedObject(gfm);
        so.FindProperty("jumpPuzzle").objectReferenceValue = jump;
        PuzzleControl cam = FindControl("PuzzleControlCam");
        if (cam != null) so.FindProperty("cameraPuzzle").objectReferenceValue = cam;
        so.ApplyModifiedPropertiesWithoutUndo();
        log.Add("GameFlowManager.jumpPuzzle = " + (jump != null ? jump.name : "NULL") + ", cameraPuzzle = " + (cam != null ? cam.name : "NULL"));
    }

    // ------------------------------------------------------------------
    // Utilitários
    // ------------------------------------------------------------------

    private static GameObject FindRoot(string name)
    {
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.name == name) return go;
        }
        return null;
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) log.Add("AVISO: sprite não encontrado em " + path);
        return sprite;
    }
}
