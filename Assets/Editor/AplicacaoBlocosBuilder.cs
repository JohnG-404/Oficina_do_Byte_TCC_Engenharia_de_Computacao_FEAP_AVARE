using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Liga as mecânicas nos blocos que o autor montou à mão na cena
/// "Aplicacao_Fisica": cada filho direto de Cenario/Ground é UM bloco
/// (Plataforma1, Plataforma2, Escada1, Plataforma6 1...), não importa quantos tiles
/// ele tenha.
///
/// Pra cada bloco:
/// - Cada tile (SpriteRenderer filho) ganha um BoxCollider2D do tamanho exato
///   do sprite, em modo Merge;
/// - O bloco ganha Rigidbody2D (Kinematic — parado até receber a Física),
///   CompositeCollider2D (junta todos os tiles num collider só, sem emendas
///   que travem o Player) e PhysicsTarget (recebe Rigidbody2D/Collider2D do
///   inventário e liga/desliga tudo de uma vez).
///
/// Também cria o objetivo da fase (LevelGoal, invisível) em cima do bloco
/// "Fim" e ajusta os limites da câmera à largura do fundo (BG).
///
/// Só ADICIONA componentes: não muda posição, escala, sprite nem ordem de
/// desenho de nada. Idempotente — pode rodar de novo depois de mexer nos
/// blocos (tiles novos ganham collider; os já configurados ficam como estão).
/// </summary>
public static class AplicacaoBlocosBuilder
{
    private const string SceneName = "Aplicacao_Fisica";
    private const string GroundPath = "Cenario/Ground";
    private const string GoalName = "Objetivo";

    [MenuItem("Tools/Aplicação/Configurar Blocos do Cenário")]
    public static void BuildMenu()
    {
        foreach (string line in Build()) Debug.Log("[AplicacaoBlocosBuilder] " + line);
    }

    public static List<string> Build()
    {
        var log = new List<string>();
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != SceneName)
        {
            log.Add($"ERRO: abra a cena {SceneName} antes de rodar (cena ativa: {scene.name}).");
            return log;
        }

        GameObject ground = GameObject.Find(GroundPath);
        if (ground == null)
        {
            log.Add($"ERRO: '{GroundPath}' não encontrado.");
            return log;
        }

        foreach (Transform block in ground.transform)
        {
            log.Add(ConfigureBlock(block));
        }

        log.Add(EnsureGoal(ground.transform));
        log.Add(FitCameraBounds());

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add("Cena salva.");
        return log;
    }

    private static string ConfigureBlock(Transform block)
    {
        int created = 0;
        SpriteRenderer[] tiles = block.GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer tile in tiles)
        {
            if (tile.sprite == null)
            {
                continue;
            }

            var box = tile.GetComponent<BoxCollider2D>();
            if (box == null)
            {
                box = Undo.AddComponent<BoxCollider2D>(tile.gameObject);
                created++;
            }

            // Tamanho exato do sprite (em espaço local do tile — a escala do
            // próprio tile já é aplicada pelo Unity).
            Bounds spriteBounds = tile.sprite.bounds;
            box.size = spriteBounds.size;
            box.offset = spriteBounds.center;
            box.compositeOperation = Collider2D.CompositeOperation.Merge;
        }

        GameObject go = block.gameObject;

        var rb = go.GetComponent<Rigidbody2D>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody2D>(go);
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        var composite = go.GetComponent<CompositeCollider2D>();
        if (composite == null) composite = Undo.AddComponent<CompositeCollider2D>(go);
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
        // Funde frestas mínimas entre tiles posicionados à mão (ex: 0,006 na
        // Caixa) — sem isso a fresta vira uma emenda no meio do bloco.
        composite.offsetDistance = 0.01f;
        composite.isTrigger = true;
        composite.GenerateGeometry();

        if (go.GetComponent<PhysicsTarget>() == null)
        {
            Undo.AddComponent<PhysicsTarget>(go);
        }

        EditorUtility.SetDirty(go);
        Bounds b = composite.bounds;
        return $"{block.name}: {tiles.Length} tiles ({created} colliders novos), {composite.pathCount} contorno(s), área {b.min.x:F2},{b.min.y:F2} → {b.max.x:F2},{b.max.y:F2}";
    }

    // Objetivo invisível em cima da borda esquerda do bloco "Fim" — o autor
    // pode colocar um sprite (bandeira, portal) no mesmo lugar depois.
    private static string EnsureGoal(Transform ground)
    {
        if (Object.FindAnyObjectByType<LevelGoal>() != null)
        {
            return "Objetivo: já existe um LevelGoal na cena — mantido.";
        }

        Transform fim = ground.Find("Fim");
        if (fim == null)
        {
            return "AVISO: bloco 'Fim' não encontrado — objetivo não criado. Coloque um LevelGoal (com Collider2D) na chegada.";
        }

        // Topo da coluna mais à esquerda do Fim = o chão onde o jogador pousa.
        Bounds fimBounds = BoundsOf(fim);
        float ledgeTop = float.MinValue;
        foreach (SpriteRenderer sr in fim.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.bounds.min.x < fimBounds.min.x + 0.5f)
            {
                ledgeTop = Mathf.Max(ledgeTop, sr.bounds.max.y);
            }
        }

        var goal = new GameObject(GoalName);
        Undo.RegisterCreatedObjectUndo(goal, "Criar objetivo");
        goal.transform.SetParent(ground.parent, true);
        goal.transform.position = new Vector3(fimBounds.min.x + 1.0f, ledgeTop + 0.8f, 0f);

        var box = goal.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(1.2f, 1.6f);
        goal.AddComponent<LevelGoal>();

        return $"Objetivo: criado em ({goal.transform.position.x:F2}, {goal.transform.position.y:F2}), sobre a borda do 'Fim' (topo {ledgeTop:F2}).";
    }

    // A câmera vai do começo ao fim do fundo (todos os "BG*" do Cenario), sem
    // mostrar a área vazia de fora. Altura continua travada.
    private static string FitCameraBounds()
    {
        Camera cam = Camera.main;
        var follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
        GameObject cenario = GameObject.Find("Cenario");
        if (follow == null || cenario == null)
        {
            return "AVISO: câmera com CameraFollow ou 'Cenario' não encontrados — limites não ajustados.";
        }

        bool found = false;
        Bounds bg = new Bounds();
        foreach (Transform child in cenario.transform)
        {
            var sr = child.GetComponent<SpriteRenderer>();
            if (sr == null || !child.name.StartsWith("BG")) continue;
            if (!found) { bg = sr.bounds; found = true; }
            else bg.Encapsulate(sr.bounds);
        }

        if (!found)
        {
            return "AVISO: nenhum BG encontrado — limites da câmera não ajustados.";
        }

        // Proporção de referência 16:9 (a do jogo) — no Editor a aspect da
        // câmera depende do tamanho da aba Game.
        float halfWidth = cam.orthographicSize * (16f / 9f);
        var so = new SerializedObject(follow);
        so.FindProperty("useBounds").boolValue = true;
        so.FindProperty("followVertical").boolValue = false;
        so.FindProperty("minPosition").vector2Value = new Vector2(bg.min.x + halfWidth, 0f);
        so.FindProperty("maxPosition").vector2Value = new Vector2(bg.max.x - halfWidth, 0f);
        so.ApplyModifiedProperties();

        return $"Câmera: segue de x={bg.min.x + halfWidth:F2} a x={bg.max.x - halfWidth:F2} (fundo de {bg.min.x:F2} a {bg.max.x:F2}).";
    }

    private static Bounds BoundsOf(Transform root)
    {
        bool first = true;
        Bounds b = new Bounds(root.position, Vector3.zero);
        foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (first) { b = sr.bounds; first = false; }
            else b.Encapsulate(sr.bounds);
        }

        return b;
    }
}
