using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Liga os degraus do terreno da cena Fisica (blocos sólidos apoiados no
/// chão e mais altos que ele, dentro da sala) ao ObstaculoPulo e ao
/// GameFlowManager, junto com as falas e as etapas da faixa de objetivo que
/// falam dos degraus.
///
/// Os degraus em si são montados à mão com os tiles do terreno, em qualquer
/// lugar da hierarquia. Este builder não cria nem move blocos: só cria o
/// objeto "ObstaculoPulo" em "Jogo" apontando pros colliders deles.
///
/// Idempotente (refaz o "ObstaculoPulo"). Recusa em Play ou com a Fisica
/// suja. Se a Fisica não estiver aberta, abre por cima (aditiva), salva e
/// fecha. Backup em SceneBackups/.
/// </summary>
public static class ObstaculoPuloBuilder
{
    private const string CaminhoCena = "Assets/Scenes/Fisica.unity";
    private const string PastaBackup = "SceneBackups";

    // Topo do chão da sala. Um bloco apoiado nele (ou em outro bloco) e mais
    // alto que ele faz parte dos degraus.
    private const float TopoDoChao = -4.29f;

    // Entre as paredes da sala (as paredes também são tiles com collider).
    private const float SalaMinX = -8.5f;
    private const float SalaMaxX = 26.7f;

    // Mais alto que isso já não é degrau (as peças dos puzzles ficam lá em cima).
    private const float AlturaMaxima = 3f;

    private static readonly string[] FalaDepoisDaColisao =
    {
        "Agora sim! Ele pisa no chão e não atravessa mais nada.",
        "Um detalhe: com Rigidbody2D, quem move o corpo é a física. Por isso o Movimento troca o transform.position por rb.linearVelocity, a velocidade do corpo.",
        "Agora segue pela sala, lá pra direita.",
    };

    private static readonly string[] FalaIntroPulo =
    {
        "Empacou! Com o Collider2D, esse degrau ficou sólido pra ele também.",
        "Andando pros lados não tem como passar. Falta o que todo personagem de plataforma tem: pulo!",
        "O próximo puzzle é o do Pulo. Bora montar?",
    };

    private static readonly string[] FalaDepoisDoPulo =
    {
        "Pronto! Aperta {pular} e passa pelos degraus. No mais alto, segura a tecla pra subir mais.",
        "Só dá pra pular apoiado no chão. O isGrounded no if barra o pulo no ar. Sem ele, dava pra pular sem parar.",
    };

    private const string FalaFinalDegrau = "Passou! Sem o pulo, esses degraus iam barrar ele pra sempre.";

    // Falas que este builder já pôs antes no começo do fechamento.
    private static readonly string[] FalasFinaisAntigas =
    {
        "Passou! Sem o pulo, essas caixas iam barrar ele pra sempre.",
        "Passou! Sem o pulo, esse degrau ia barrar ele pra sempre.",
    };

    // Etapas 6 (EtapaTestar) e 8 (EtapaPular) do ObjetivoFase da Fisica.
    private static readonly (int indice, string titulo, string dica)[] Etapas =
    {
        (6, "Siga pela sala", "Ande pra direita e veja o que mudou com a colisão."),
        (8, "Pule os degraus com {pular}", "Segure {pular} para pular mais alto. Ele só pula pisando no chão."),
    };

    [MenuItem("Tools/Fases/Obstáculo do pulo (Física)")]
    public static void Montar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[ObstaculoPuloBuilder] Saia do Play antes.");
            return;
        }

        Scene cena = SceneManager.GetSceneByPath(CaminhoCena);
        bool abriuAgora = !cena.isLoaded;
        if (!abriuAgora && cena.isDirty)
        {
            Debug.LogError("[ObstaculoPuloBuilder] A Fisica tem alterações não salvas. Salve antes.");
            return;
        }

        Directory.CreateDirectory(PastaBackup);
        File.Copy(CaminhoCena, Path.Combine(PastaBackup, "Fisica_antes_obstaculo_pulo.unity.bak"), true);

        if (abriuAgora)
        {
            cena = EditorSceneManager.OpenScene(CaminhoCena, OpenSceneMode.Additive);
        }

        try
        {
            if (Aplicar(cena))
            {
                EditorSceneManager.SaveScene(cena);
                Debug.Log("[ObstaculoPuloBuilder] Degrau do pulo ligado na Fisica.");
            }
        }
        finally
        {
            if (abriuAgora)
            {
                EditorSceneManager.CloseScene(cena, true);
            }
        }
    }

    private static bool Aplicar(Scene cena)
    {
        Transform jogo = null;
        GameFlowManager flow = null;
        ObjetivoFase objetivo = null;
        foreach (GameObject raiz in cena.GetRootGameObjects())
        {
            if (raiz.name == "Jogo")
            {
                jogo = raiz.transform;
            }
            flow = flow != null ? flow : raiz.GetComponentInChildren<GameFlowManager>(true);
            objetivo = objetivo != null ? objetivo : raiz.GetComponentInChildren<ObjetivoFase>(true);
        }

        if (jogo == null || flow == null)
        {
            Debug.LogError("[ObstaculoPuloBuilder] Fisica sem 'Jogo' ou sem GameFlowManager.");
            return false;
        }

        var partes = new List<Collider2D>();
        foreach (GameObject raiz in cena.GetRootGameObjects())
        {
            foreach (Collider2D col in raiz.GetComponentsInChildren<Collider2D>(false))
            {
                if (EhDegrau(col))
                {
                    partes.Add(col);
                }
            }
        }
        if (partes.Count == 0)
        {
            Debug.LogError("[ObstaculoPuloBuilder] Nenhum bloco acima do chão dentro da sala. Monte os degraus primeiro.");
            return false;
        }

        Transform antigo = jogo.Find("ObstaculoPulo");
        if (antigo != null)
        {
            Object.DestroyImmediate(antigo.gameObject);
        }

        Bounds area = partes[0].bounds;
        foreach (Collider2D col in partes)
        {
            area.Encapsulate(col.bounds);
        }

        var obstaculo = new GameObject("ObstaculoPulo");
        obstaculo.transform.SetParent(jogo, false);
        obstaculo.transform.position = new Vector3(area.center.x, area.center.y, 0f);

        var componente = obstaculo.AddComponent<ObstaculoPulo>();
        var so = new SerializedObject(componente);
        so.FindProperty("player").objectReferenceValue = jogo.GetComponentInChildren<Player>(true);
        SerializedProperty lista = so.FindProperty("partes");
        lista.arraySize = partes.Count;
        for (int i = 0; i < partes.Count; i++)
        {
            lista.GetArrayElementAtIndex(i).objectReferenceValue = partes[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        var flowSo = new SerializedObject(flow);
        flowSo.FindProperty("obstaculoPulo").objectReferenceValue = componente;
        // Depois da colisão só a última fala muda ("dá uma voltinha" vira
        // "segue pra direita"); no fim, a fala do degrau entra na frente.
        TrocarUltima(flowSo.FindProperty("afterCollisionDialogue"), FalaDepoisDaColisao);
        Preencher(flowSo.FindProperty("jumpIntroDialogue"), FalaIntroPulo);
        Preencher(flowSo.FindProperty("afterJumpDialogue"), FalaDepoisDoPulo);
        ColocarNaFrente(flowSo.FindProperty("finalDialogue"), FalaFinalDegrau);
        flowSo.ApplyModifiedPropertiesWithoutUndo();

        if (objetivo != null)
        {
            var objSo = new SerializedObject(objetivo);
            SerializedProperty etapas = objSo.FindProperty("etapas");
            foreach (var (indice, titulo, dica) in Etapas)
            {
                if (indice < etapas.arraySize)
                {
                    SerializedProperty etapa = etapas.GetArrayElementAtIndex(indice);
                    etapa.FindPropertyRelative("titulo").stringValue = titulo;
                    etapa.FindPropertyRelative("dica").stringValue = dica;
                }
            }
            objSo.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(cena);
        Debug.Log($"[ObstaculoPuloBuilder] Degraus com {partes.Count} blocos, de x {area.min.x:0.00} a {area.max.x:0.00}, altura máxima {area.max.y - TopoDoChao:0.00}.");
        return true;
    }

    // Bloco fixo do terreno, entre as paredes, apoiado no chão ou em outro
    // bloco e mais alto que o chão. O Player (que só ganha Rigidbody2D no
    // Play) e as peças dos puzzles ficam de fora.
    private static bool EhDegrau(Collider2D col)
    {
        if (!col.enabled || col.isTrigger ||
            col.GetComponentInParent<Rigidbody2D>(true) != null ||
            col.GetComponentInParent<Player>(true) != null ||
            col.GetComponentInParent<DragPiece>(true) != null)
        {
            return false;
        }

        Bounds b = col.bounds;
        return b.size.x > 0.01f && b.min.x > SalaMinX && b.max.x < SalaMaxX &&
            b.min.y > TopoDoChao - 0.05f &&
            b.max.y > TopoDoChao + 0.1f && b.max.y < TopoDoChao + AlturaMaxima;
    }

    private static void TrocarUltima(SerializedProperty lista, string[] padrao)
    {
        if (lista.arraySize == 0)
        {
            Preencher(lista, padrao);
            return;
        }
        lista.GetArrayElementAtIndex(lista.arraySize - 1).stringValue = padrao[padrao.Length - 1];
    }

    private static void ColocarNaFrente(SerializedProperty lista, string texto)
    {
        if (lista.arraySize > 0)
        {
            string primeira = lista.GetArrayElementAtIndex(0).stringValue;
            if (primeira == texto)
            {
                return;
            }
            if (System.Array.IndexOf(FalasFinaisAntigas, primeira) >= 0)
            {
                lista.GetArrayElementAtIndex(0).stringValue = texto;
                return;
            }
        }
        lista.InsertArrayElementAtIndex(0);
        lista.GetArrayElementAtIndex(0).stringValue = texto;
    }

    private static void Preencher(SerializedProperty lista, string[] textos)
    {
        lista.arraySize = textos.Length;
        for (int i = 0; i < textos.Length; i++)
        {
            lista.GetArrayElementAtIndex(i).stringValue = textos[i];
        }
    }
}
