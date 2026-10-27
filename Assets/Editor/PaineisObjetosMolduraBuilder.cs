using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Preenche o cartão (ícone,
/// moldura, categoria e linha de baixo) das peças dos painéis do Animator dos
/// objetos da cena Animacao (moeda, jumppad, bateria, alavanca e lâmpada),
/// pra elas aparecerem como as peças do puzzle do Animator do Player.
/// Só mexe nesses campos; textos, setas e descrições ficam como estão.
/// Idempotente. Abre a Animacao em modo Additive se ela não estiver aberta
/// e salva a cena no fim.
/// </summary>
public static class PaineisObjetosMolduraBuilder
{
    private const string CenaAnimacao = "Assets/Scenes/Animacao.unity";

    [MenuItem("Tools/Animação/Painéis dos objetos na moldura (item 5)")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[PaineisObjetosMolduraBuilder] Saia do Play antes de rodar.");
            return;
        }

        Scene cena = SceneManager.GetSceneByPath(CenaAnimacao);
        bool abriu = false;
        if (!cena.IsValid() || !cena.isLoaded)
        {
            cena = EditorSceneManager.OpenScene(CenaAnimacao, OpenSceneMode.Additive);
            abriu = true;
        }
        else if (cena.isDirty)
        {
            Debug.LogError("[PaineisObjetosMolduraBuilder] A Animacao tem edições não salvas. Salve ou descarte antes de rodar.");
            return;
        }

        int pecas = 0;
        foreach (GameObject raiz in cena.GetRootGameObjects())
        {
            foreach (ObjetoAnimatorUI ui in raiz.GetComponentsInChildren<ObjetoAnimatorUI>(true))
            {
                pecas += Preencher(ui);
            }
            foreach (LampadaAnimatorUI ui in raiz.GetComponentsInChildren<LampadaAnimatorUI>(true))
            {
                pecas += Preencher(ui);
            }
        }

        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        if (abriu)
        {
            EditorSceneManager.CloseScene(cena, true);
        }
        Debug.Log($"[PaineisObjetosMolduraBuilder] {pecas} peças com cartão. Cena salva.");
    }

    private static int Preencher(Component ui)
    {
        var so = new SerializedObject(ui);
        SerializedProperty lista = so.FindProperty("pecas");
        for (int i = 0; i < lista.arraySize; i++)
        {
            SerializedProperty e = lista.GetArrayElementAtIndex(i);
            var p = new AnimatorPuzzleUI.Peca { texto = e.FindPropertyRelative("texto").stringValue };
            ObjetoAnimatorModelos.PreencherCartao(p);
            e.FindPropertyRelative("icone").objectReferenceValue = p.icone;
            e.FindPropertyRelative("moldura").objectReferenceValue = p.moldura;
            e.FindPropertyRelative("categoria").stringValue = p.categoria;
            e.FindPropertyRelative("corCategoria").colorValue = p.corCategoria;
            e.FindPropertyRelative("subtitulo").stringValue = p.subtitulo;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return lista.arraySize;
    }
}
