using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ajuste de cena pro painel de configuração das fases Interacao e
/// Interacao_Vida: o painel agora diz "Quem ativa: tag Player", e os itens
/// (InteractableItem, ItemProximityZone, ItemTopZone) passaram a checar
/// CompareTag("Player"). Nas cenas salvas o Player estava "Untagged", então
/// este menu marca o GameObject do jogador (quem tem InteracaoPlayerController)
/// com a tag Player nas duas cenas e salva.
///
/// O resto das novidades do painel (linha do equivalente no Unity, linhas
/// "Quem ativa" e "Repetir") é montado em tempo de execução pelo
/// InteractionConfigUI, sem precisar mexer na cena nem no prefab.
///
/// Uso: Tools > Interação > Marcar Player com tag Player. Idempotente. Se a
/// cena aberta for uma das duas, é ajustada e salva no lugar; a outra só é
/// aberta se nenhuma cena carregada tiver alterações não salvas (nunca
/// descarta edições do usuário). No fim reabre a cena que estava aberta.
/// </summary>
public static class InteracaoPainelBuilder
{
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/Interacao.unity",
        "Assets/Scenes/Interacao_Vida.unity",
    };

    [MenuItem("Tools/Interação/Marcar Player com tag Player")]
    public static void TagPlayerInInteracaoScenes()
    {
        var log = new StringBuilder("[InteracaoPainelBuilder] Tag Player nas cenas de Interação:\n");
        string originalPath = SceneManager.GetActiveScene().path;
        var pending = new List<string>(ScenePaths);

        // 1) Cena(s) já abertas que são alvo: ajusta no lugar.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loaded = SceneManager.GetSceneAt(i);
            if (loaded.isLoaded && pending.Contains(loaded.path))
            {
                ProcessScene(loaded, log);
                pending.Remove(loaded.path);
            }
        }

        // 2) As outras: só abre se nada carregado tiver alterações não salvas.
        bool openedOtherScene = false;
        foreach (string path in pending)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                log.AppendLine($"  - {path}: cena não encontrada, pulada.");
                continue;
            }

            if (AnyLoadedSceneDirty())
            {
                Debug.LogError($"[InteracaoPainelBuilder] Há cena aberta com alterações não salvas. Salve (Ctrl+S) e rode o menu de novo pra ajustar {path}.");
                log.AppendLine($"  - {path}: NÃO ajustada (cena aberta com alterações não salvas).");
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            openedOtherScene = true;
            ProcessScene(scene, log);
        }

        // 3) Volta pra cena em que o usuário estava.
        if (openedOtherScene && !string.IsNullOrEmpty(originalPath) &&
            SceneManager.GetActiveScene().path != originalPath && !AnyLoadedSceneDirty())
        {
            EditorSceneManager.OpenScene(originalPath, OpenSceneMode.Single);
        }

        Debug.Log(log.ToString());
    }

    private static void ProcessScene(Scene scene, StringBuilder log)
    {
        int found = 0;
        int changed = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (InteracaoPlayerController player in root.GetComponentsInChildren<InteracaoPlayerController>(true))
            {
                found++;
                GameObject go = player.gameObject;

                if (go.CompareTag(InteractableItem.PlayerTag))
                {
                    log.AppendLine($"  - {scene.name}: \"{go.name}\" já tinha a tag {InteractableItem.PlayerTag}.");
                    continue;
                }

                Undo.RecordObject(go, "Tag Player");
                string oldTag = go.tag;
                go.tag = InteractableItem.PlayerTag;
                changed++;
                log.AppendLine($"  - {scene.name}: \"{go.name}\" {oldTag} -> {InteractableItem.PlayerTag}.");
            }
        }

        if (found == 0)
        {
            log.AppendLine($"  - {scene.name}: nenhum InteracaoPlayerController encontrado (nada mudou).");
            return;
        }

        if (changed > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"  - {scene.name}: salva.");
        }
    }

    private static bool AnyLoadedSceneDirty()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                return true;
            }
        }

        return false;
    }
}
