using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Utilitário de Editor: monta (ou localiza, se já existir na cena) o painel
/// de Pausa e a tela de Configurações com abas, deixando tudo visível e
/// editável na Hierarchy/Inspector — cores, tamanhos, fontes, imagens.
///
/// Chama os mesmos métodos EnsureBuilt() usados em runtime, então o
/// resultado é idêntico ao que o jogo criaria sozinho; a diferença é só que
/// aqui fica salvo na cena, pronto pra ajustar visualmente.
///
/// Uso: Tools > UI do Jogo > Montar UI de Pausa e Configurações.
/// Rode na cena onde quiser deixar a UI salva (ex: Menu.unity) — como o
/// painel já é DontDestroyOnLoad em runtime, uma cópia é suficiente para
/// o jogo inteiro.
/// </summary>
public static class GameUIBuilder
{
    [MenuItem("Tools/UI do Jogo/Montar UI de Pausa e Configurações")]
    public static void BuildGameUI()
    {
        PauseMenuUI existing = Object.FindAnyObjectByType<PauseMenuUI>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("[GameUIBuilder] Já existe uma UI de Pausa/Configurações nesta cena — selecionada na Hierarchy.");
            return;
        }

        var canvasRoot = new GameObject("GameUICanvas");
        Undo.RegisterCreatedObjectUndo(canvasRoot, "Montar UI de Pausa e Configurações");

        var canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasRoot.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasRoot.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var pauseMenu = canvasRoot.AddComponent<PauseMenuUI>();
        pauseMenu.EnsureBuilt();

        Selection.activeGameObject = canvasRoot;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[GameUIBuilder] UI de Pausa e Configurações criada. Ajuste cores/tamanhos/fontes à vontade e salve a cena (Ctrl+S).");
    }
}
