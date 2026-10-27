using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Coloca a mini-lição de HUD e o estado Game Over na cena "Interacao_Vida":
/// cria (ou reaproveita) o GameObject "HudLicao" com os componentes HudLicao
/// e GameOverUI, liga as referências (HeartHealthUI, PlayerHealth,
/// DialogueManager) e aponta o campo gameOverUI do InteracaoLevelFlow pro
/// painel novo. A UI da lição, do puzzle e do Game Over é montada em runtime
/// por esses componentes, então nada de layout é salvo na cena.
///
/// Uso: Tools > Interação > Configurar Lição de HUD. Se a cena ativa não for
/// a Interacao_Vida, abre ela (só se a cena atual não tiver edições não
/// salvas). Idempotente — pode rodar de novo sem duplicar nada.
/// </summary>
public static class InteracaoVidaHudBuilder
{
    private const string SceneName = "Interacao_Vida";
    private const string ScenePath = "Assets/Scenes/Interacao_Vida.unity";
    private const string RootName = "HudLicao";

    [MenuItem("Tools/Interação/Configurar Lição de HUD")]
    public static void BuildMenu()
    {
        foreach (string line in Build())
        {
            Debug.Log("[InteracaoVidaHudBuilder] " + line);
        }
    }

    public static List<string> Build()
    {
        var log = new List<string>();

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != SceneName)
        {
            if (scene.isDirty)
            {
                Debug.LogError($"[InteracaoVidaHudBuilder] A cena ativa ({scene.name}) tem edições não salvas. Salve ou descarte antes de rodar, ou abra {SceneName} manualmente.");
                log.Add("ABORTADO: cena ativa suja.");
                return log;
            }

            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            log.Add($"Cena {SceneName} aberta.");
        }

        // Referências que a lição usa
        HeartHealthUI barra = Object.FindAnyObjectByType<HeartHealthUI>(FindObjectsInactive.Include);
        PlayerHealth vida = Object.FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
        DialogueManager dialogo = Object.FindAnyObjectByType<DialogueManager>(FindObjectsInactive.Include);
        InteracaoLevelFlow flow = Object.FindAnyObjectByType<InteracaoLevelFlow>(FindObjectsInactive.Include);

        if (barra == null) log.Add("AVISO: nenhum HeartHealthUI na cena (rode antes 'Montar Cena de Interação (Vida)').");
        if (vida == null) log.Add("AVISO: nenhum PlayerHealth na cena.");
        if (dialogo == null) log.Add("AVISO: nenhum DialogueManager na cena — a lição vai direto pro puzzle, sem as falas.");
        if (flow == null) log.Add("AVISO: nenhum InteracaoLevelFlow na cena — o Game Over não vai ser chamado.");

        // GameObject raiz
        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Criar HudLicao");
            log.Add($"GameObject '{RootName}' criado.");
        }
        else
        {
            log.Add($"GameObject '{RootName}' já existia — reaproveitado.");
        }

        HudLicao licao = root.GetComponent<HudLicao>();
        if (licao == null)
        {
            licao = Undo.AddComponent<HudLicao>(root);
            log.Add("Componente HudLicao adicionado (textos e peças com os valores padrão).");
        }

        GameOverUI gameOver = root.GetComponent<GameOverUI>();
        if (gameOver == null)
        {
            gameOver = Undo.AddComponent<GameOverUI>(root);
            log.Add("Componente GameOverUI adicionado.");
        }

        // Liga as referências da lição (campos privados → SerializedObject)
        var so = new SerializedObject(licao);
        so.FindProperty("barraDeVida").objectReferenceValue = barra;
        so.FindProperty("playerHealth").objectReferenceValue = vida;
        so.FindProperty("dialogueManager").objectReferenceValue = dialogo;
        so.ApplyModifiedProperties();
        log.Add($"HudLicao ligado: barra={(barra != null ? barra.name : "nenhuma")}, vida={(vida != null ? vida.name : "nenhuma")}, diálogo={(dialogo != null ? dialogo.name : "nenhum")}.");

        // A barra salva na cena fica conectada (padrão): quem desconecta em
        // runtime é a HudLicao, só enquanto a lição não foi feita.
        if (barra != null)
        {
            var barraSO = new SerializedObject(barra);
            SerializedProperty conectada = barraSO.FindProperty("conectada");
            if (conectada != null && !conectada.boolValue)
            {
                conectada.boolValue = true;
                barraSO.ApplyModifiedProperties();
                log.Add("HeartHealthUI: 'conectada' estava desligado na cena — religado (a lição desconecta em runtime).");
            }
        }

        // InteracaoLevelFlow → GameOverUI
        if (flow != null)
        {
            var flowSO = new SerializedObject(flow);
            flowSO.FindProperty("gameOverUI").objectReferenceValue = gameOver;
            flowSO.ApplyModifiedProperties();
            log.Add("InteracaoLevelFlow.gameOverUI → GameOverUI.");
        }

        // O puzzle é UGUI e precisa de EventSystem.
        if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
        {
            var es = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(es, "Criar EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
            log.Add("EventSystem (InputSystemUIInputModule) criado.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add("Cena salva.");
        return log;
    }
}
