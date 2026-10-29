using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Monta na cena Fisica a pilha de caixas que só dá pra passar pulando
/// (ObstaculoPulo) e liga ela no GameFlowManager, junto com as falas e as
/// etapas da faixa de objetivo que falam das caixas.
///
/// Idempotente (apaga e refaz o "ObstaculoPulo" dentro de "Jogo"). Recusa em
/// Play ou com a Fisica suja. Se a Fisica não estiver aberta, abre por cima
/// (aditiva), salva e fecha. Backup em SceneBackups/.
/// </summary>
public static class ObstaculoPuloBuilder
{
    private const string CaminhoCena = "Assets/Scenes/Fisica.unity";
    private const string SpriteCaixa = "Assets/Images/Packs/Metal/metal_block.png";
    private const string PastaBackup = "SceneBackups";

    // Caixas de 0.83 (metal_block, 80 PPU): 2 x 2 dá 1.66 de altura. O pulo
    // segurando sobe ~2.5 e o batido ~1.25, então só passa segurando a tecla.
    private const int Colunas = 2;
    private const int Linhas = 2;
    private const float CentroX = 5.5f;
    private const float TopoDoChao = -4.29f;

    private static readonly string[] FalaDepoisDaColisao =
    {
        "Agora sim! Ele pisa no chão e não atravessa mais nada.",
        "Um detalhe: com Rigidbody2D, quem move o corpo é a física. Por isso o Movimento troca o transform.position por rb.linearVelocity, a velocidade do corpo.",
        "Agora segue pela sala, lá pra direita.",
    };

    private static readonly string[] FalaIntroPulo =
    {
        "Empacou! Com o Collider2D, essas caixas ficaram sólidas pra ele também.",
        "Andando pros lados não tem como passar. Falta o que todo personagem de plataforma tem: pulo!",
        "O próximo puzzle é o do Pulo. Bora montar?",
    };

    private static readonly string[] FalaDepoisDoPulo =
    {
        "Pronto! Aperta {pular} e passa por cima das caixas. Segurando a tecla, ele sobe mais alto.",
        "Só dá pra pular apoiado no chão. O isGrounded no if barra o pulo no ar. Sem ele, dava pra pular sem parar.",
    };

    private const string FalaFinalCaixas = "Passou! Sem o pulo, essas caixas iam barrar ele pra sempre.";

    // Etapas 6 (EtapaTestar) e 8 (EtapaPular) do ObjetivoFase da Fisica.
    private static readonly (int indice, string titulo, string dica)[] Etapas =
    {
        (6, "Siga pela sala", "Ande pra direita e veja o que mudou com a colisão."),
        (8, "Pule as caixas com {pular}", "Segure {pular} para subir mais alto. Ele só pula pisando no chão."),
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
            Aplicar(cena);
            EditorSceneManager.SaveScene(cena);
            Debug.Log("[ObstaculoPuloBuilder] Caixas do pulo montadas na Fisica.");
        }
        finally
        {
            if (abriuAgora)
            {
                EditorSceneManager.CloseScene(cena, true);
            }
        }
    }

    private static void Aplicar(Scene cena)
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
            throw new System.InvalidOperationException("Fisica sem 'Jogo' ou sem GameFlowManager.");
        }

        Transform antigo = jogo.Find("ObstaculoPulo");
        if (antigo != null)
        {
            Object.DestroyImmediate(antigo.gameObject);
        }

        Player player = jogo.GetComponentInChildren<Player>(true);
        Sprite caixa = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteCaixa);
        float lado = caixa.bounds.size.x;

        var obstaculo = new GameObject("ObstaculoPulo");
        obstaculo.transform.SetParent(jogo, false);
        obstaculo.transform.position = new Vector3(CentroX, TopoDoChao + lado * Linhas / 2f, 0f);

        for (int linha = 0; linha < Linhas; linha++)
        {
            for (int coluna = 0; coluna < Colunas; coluna++)
            {
                var go = new GameObject($"Caixa {linha * Colunas + coluna + 1}");
                go.transform.SetParent(obstaculo.transform, false);
                go.transform.localPosition = new Vector3(
                    (coluna - (Colunas - 1) / 2f) * lado,
                    (linha - (Linhas - 1) / 2f) * lado,
                    0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = caixa;
                sr.sortingLayerName = "Game";
                sr.sortingOrder = 2;
            }
        }

        // Um collider só pra pilha toda: sem emendas entre as caixas pro
        // personagem enganchar ao subir ou descer.
        var col = obstaculo.AddComponent<BoxCollider2D>();
        col.size = new Vector2(lado * Colunas, lado * Linhas);

        var componente = obstaculo.AddComponent<ObstaculoPulo>();
        var so = new SerializedObject(componente);
        so.FindProperty("player").objectReferenceValue = player;
        so.ApplyModifiedPropertiesWithoutUndo();

        var flowSo = new SerializedObject(flow);
        flowSo.FindProperty("obstaculoPulo").objectReferenceValue = componente;
        // Depois da colisão só a última fala muda ("dá uma voltinha" vira
        // "segue pra direita"); no fim, a fala das caixas entra na frente.
        TrocarUltima(flowSo.FindProperty("afterCollisionDialogue"), FalaDepoisDaColisao);
        Preencher(flowSo.FindProperty("jumpIntroDialogue"), FalaIntroPulo);
        Preencher(flowSo.FindProperty("afterJumpDialogue"), FalaDepoisDoPulo);
        ColocarNaFrente(flowSo.FindProperty("finalDialogue"), FalaFinalCaixas);
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
        if (lista.arraySize > 0 && lista.GetArrayElementAtIndex(0).stringValue == texto)
        {
            return;
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
