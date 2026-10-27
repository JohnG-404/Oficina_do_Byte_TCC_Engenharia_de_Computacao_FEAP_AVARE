using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Liga a linguagem "holograma vira matéria" (Materializacao) na cena
/// Aplicacao_Fisica:
/// - Cria o material do shader Aplicacao/Materializacao.
/// - Põe um Materializacao em cada PhysicsTarget e no Player.
/// - Tira o que sobrou das ideias anteriores (fantasma, selos, mini
///   Inspector), inclusive componentes cujo script já foi apagado.
/// - Explica o holograma na fala de abertura do Prof Byte.
///
/// Pode rodar de novo: só reconfigura.
/// </summary>
public static class AplicacaoMaterializacaoBuilder
{
    private const string ScenePath = "Assets/Scenes/Aplicacao_Fisica.unity";
    private const string ShaderNome = "Aplicacao/Materializacao";
    private const string MaterialPath = "Assets/Art/Aplicacao/Materializacao.mat";
    private const string PoeiraPath = "Assets/Art/UI/Aplicacao/inv_circulo.png";
    private const string FalaHolograma = "Tudo que aparece como holograma, azul e listrado, ainda não tem Collider2D: dá pra atravessar.";

    private static readonly string[] ComponentesAntigos = { "ColisaoFantasma", "SelosComponentes" };

    [MenuItem("Tools/Aplicação/Montar Holograma e Matéria")]
    public static void BuildMenu()
    {
        foreach (string line in Build()) Debug.Log("[AplicacaoMaterializacaoBuilder] " + line);
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

        Material mat = CriarMaterial();
        Sprite poeira = AssetDatabase.LoadAssetAtPath<Sprite>(PoeiraPath);
        log.Add("material: " + (mat != null) + ", poeira: " + (poeira != null));

        var alvos = new List<GameObject>();
        foreach (GameObject raiz in scene.GetRootGameObjects())
        {
            foreach (PhysicsTarget pt in raiz.GetComponentsInChildren<PhysicsTarget>(true)) alvos.Add(pt.gameObject);
            foreach (AplicacaoPlayerController p in raiz.GetComponentsInChildren<AplicacaoPlayerController>(true)) alvos.Add(p.gameObject);
            log.AddRange(LimparAntigos(raiz));
        }

        foreach (GameObject go in alvos)
        {
            var m = go.GetComponent<Materializacao>();
            if (m == null)
            {
                m = Undo.AddComponent<Materializacao>(go);
            }

            var so = new SerializedObject(m);
            so.FindProperty("material").objectReferenceValue = mat;
            so.FindProperty("spritePoeira").objectReferenceValue = poeira;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add("materialização: " + go.name);
        }

        log.Add(ExplicarNaAbertura(scene));
        EditorSceneManager.MarkSceneDirty(scene);
        return log;
    }

    private static Material CriarMaterial()
    {
        Shader shader = Shader.Find(ShaderNome);
        if (shader == null)
        {
            return null;
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }

        mat.shader = shader;
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    private static List<string> LimparAntigos(GameObject raiz)
    {
        var log = new List<string>();
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            GameObject go = t.gameObject;
            int faltando = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            if (faltando > 0)
            {
                log.Add("script apagado removido: " + go.name);
            }

            foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb != null && System.Array.IndexOf(ComponentesAntigos, mb.GetType().Name) >= 0)
                {
                    log.Add("removido " + mb.GetType().Name + ": " + go.name);
                    Undo.DestroyObjectImmediate(mb);
                }
            }
        }

        Transform mini = raiz.transform.Find("MiniInspector");
        if (mini != null && raiz.GetComponent<Canvas>() != null)
        {
            Undo.DestroyObjectImmediate(mini.gameObject);
            log.Add("MiniInspector removido do Canvas");
        }

        return log;
    }

    private static string ExplicarNaAbertura(Scene scene)
    {
        foreach (GameObject raiz in scene.GetRootGameObjects())
        {
            foreach (AplicacaoLevelFlow flow in raiz.GetComponentsInChildren<AplicacaoLevelFlow>(true))
            {
                var so = new SerializedObject(flow);
                SerializedProperty falas = so.FindProperty("introDialogue");
                for (int i = 0; i < falas.arraySize; i++)
                {
                    if (falas.GetArrayElementAtIndex(i).stringValue == FalaHolograma)
                    {
                        return "fala do holograma já estava na abertura";
                    }
                }

                // Logo depois de "nada tem Rigidbody2D nem Collider2D" (ou no fim).
                int pos = falas.arraySize;
                for (int i = 0; i < falas.arraySize; i++)
                {
                    if (falas.GetArrayElementAtIndex(i).stringValue.Contains("Collider2D"))
                    {
                        pos = i + 1;
                        break;
                    }
                }

                if (pos >= falas.arraySize)
                {
                    falas.arraySize++;
                    pos = falas.arraySize - 1;
                }
                else
                {
                    falas.InsertArrayElementAtIndex(pos);
                }

                falas.GetArrayElementAtIndex(pos).stringValue = FalaHolograma;
                so.ApplyModifiedProperties();
                return "fala do holograma incluída na abertura (posição " + pos + ")";
            }
        }

        return "AVISO: AplicacaoLevelFlow não encontrado";
    }
}
