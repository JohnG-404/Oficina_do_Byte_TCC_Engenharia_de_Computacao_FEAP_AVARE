using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ajusta a cena "Aplicacao_Fisica" para o trecho do meio virar uma escolha:
/// - As 9 plataformas do Grupo6 deixam de ser um bloco só. Cada uma vira um
///   bloco próprio em Cenario/Ground (Rigidbody2D Kinematic,
///   CompositeCollider2D e PhysicsTarget), então o Collider2D e o Rigidbody2D
///   são aplicados plataforma por plataforma e o jogador escolhe quais ficam
///   sólidas e quais caem.
/// - O Rigidbody2D do inventário fica sem limite de usos, como os outros.
///
/// Os blocos novos copiam a configuração do PhysicsTarget, do Rigidbody2D e
/// do CompositeCollider2D do Grupo6, que é apagado no fim (já vazio).
/// Pode rodar de novo: sem Grupo6 na cena, só confere o inventário.
/// </summary>
public static class AplicacaoPlataformasSeparadasBuilder
{
    private const string ScenePath = "Assets/Scenes/Aplicacao_Fisica.unity";
    private const string GrupoNome = "Grupo6";

    [MenuItem("Tools/Aplicação/Separar plataformas do meio e Rigidbody2D sem limite")]
    public static void BuildMenu()
    {
        foreach (string line in Build()) Debug.Log("[AplicacaoPlataformasSeparadasBuilder] " + line);
    }

    public static List<string> Build()
    {
        var log = new List<string>();
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            log.Add("ERRO: saia do Play antes de rodar.");
            return log;
        }

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            log.Add("ERRO: abra a cena Aplicacao_Fisica antes de rodar.");
            return log;
        }

        GameObject cenario = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Cenario");
        Transform ground = cenario != null ? cenario.transform.Find("Ground") : null;
        if (ground == null)
        {
            log.Add("ERRO: Cenario/Ground não encontrado.");
            return log;
        }

        Transform grupo = ground.Find(GrupoNome);
        if (grupo == null)
        {
            log.Add(GrupoNome + ": já separado, nada a fazer");
        }
        else
        {
            SepararGrupo(ground, grupo, log);
        }

        RigidbodySemLimite(scene, log);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add("Cena salva.");
        return log;
    }

    private static void SepararGrupo(Transform ground, Transform grupo, List<string> log)
    {
        var ptGrupo = grupo.GetComponent<PhysicsTarget>();
        var rbGrupo = grupo.GetComponent<Rigidbody2D>();
        var ccGrupo = grupo.GetComponent<CompositeCollider2D>();

        // Da esquerda para a direita e de cima para baixo, como o jogador lê a cena.
        List<Transform> plataformas = grupo.Cast<Transform>()
            .OrderBy(t => Mathf.Round(Centro(t).x))
            .ThenByDescending(t => Centro(t).y)
            .ToList();

        int indice = grupo.GetSiblingIndex();
        foreach (Transform p in plataformas)
        {
            Undo.SetTransformParent(p, ground, "Separar plataformas do meio");
            p.SetSiblingIndex(indice++);
            GameObject go = p.gameObject;

            var rb = go.GetComponent<Rigidbody2D>();
            if (rb == null) rb = Undo.AddComponent<Rigidbody2D>(go);
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.constraints = rbGrupo != null ? rbGrupo.constraints : RigidbodyConstraints2D.FreezeRotation;

            var cc = go.GetComponent<CompositeCollider2D>();
            if (cc == null) cc = Undo.AddComponent<CompositeCollider2D>(go);
            if (ccGrupo != null)
            {
                cc.geometryType = ccGrupo.geometryType;
                cc.generationType = ccGrupo.generationType;
                cc.offsetDistance = ccGrupo.offsetDistance;
            }
            cc.isTrigger = true;

            foreach (BoxCollider2D box in go.GetComponentsInChildren<BoxCollider2D>(true))
            {
                box.compositeOperation = Collider2D.CompositeOperation.Merge;
            }
            cc.GenerateGeometry();

            var pt = go.GetComponent<PhysicsTarget>();
            if (pt == null) pt = Undo.AddComponent<PhysicsTarget>(go);
            if (ptGrupo != null)
            {
                EditorUtility.CopySerialized(ptGrupo, pt);
            }

            EditorUtility.SetDirty(go);
            Bounds b = cc.bounds;
            log.Add($"{go.name}: bloco próprio, {cc.pathCount} contorno(s), área {b.min.x:F2},{b.min.y:F2} → {b.max.x:F2},{b.max.y:F2}");
        }

        Undo.DestroyObjectImmediate(grupo.gameObject);
        log.Add($"{GrupoNome}: {plataformas.Count} plataformas separadas, grupo vazio apagado");
    }

    private static void RigidbodySemLimite(Scene scene, List<string> log)
    {
        foreach (GameObject raiz in scene.GetRootGameObjects())
        {
            foreach (DraggableToolIcon icon in raiz.GetComponentsInChildren<DraggableToolIcon>(true))
            {
                if (icon.Tool != ToolType.Fisica)
                {
                    continue;
                }

                var so = new SerializedObject(icon);
                SerializedProperty usos = so.FindProperty("usosDisponiveis");
                int antes = usos.intValue;
                usos.intValue = -1;
                so.ApplyModifiedProperties();

                // O selo "x2" só aparece com limite; desligado também no Editor.
                var selo = so.FindProperty("seloUsos").objectReferenceValue as Component;
                if (selo != null)
                {
                    Undo.RecordObject(selo.gameObject, "Selo de usos");
                    selo.gameObject.SetActive(false);
                }

                log.Add($"Rigidbody2D: usos {(antes < 0 ? "já sem limite" : antes + " → sem limite")}");
            }
        }
    }

    private static Vector3 Centro(Transform t)
    {
        Bounds b = new Bounds(t.position, Vector3.zero);
        bool primeiro = true;
        foreach (SpriteRenderer sr in t.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (primeiro) { b = sr.bounds; primeiro = false; }
            else b.Encapsulate(sr.bounds);
        }
        return b.center;
    }
}
