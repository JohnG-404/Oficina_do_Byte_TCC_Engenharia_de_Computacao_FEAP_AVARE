using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Monta (ou localiza, se já existir) a estrutura interna do painel "Info"
/// da cena Inicio, deixando "Title" e "Description" salvos na cena e
/// visíveis na Hierarchy — prontos pra você arrastar/redimensionar cada um
/// no Editor pra decidir onde o título e a descrição aparecem.
///
/// Sem essa ferramenta, InfoPanelUI só monta essa estrutura em Play Mode, e
/// o Unity descarta tudo que foi criado em runtime ao sair do Play — ou
/// seja, não haveria como salvar um reposicionamento manual.
///
/// Uso: selecione o GameObject "Info" na Hierarchy (ou deixe sem selecionar
/// pra ele procurar por nome) e rode Tools > Inicio > Montar Painel de Info.
/// </summary>
public static class InfoPanelBuilder
{
    [MenuItem("Tools/Inicio/Montar Painel de Info")]
    public static void BuildInfoPanel()
    {
        InfoPanelUI panel = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<InfoPanelUI>()
            : null;

        if (panel == null)
        {
            GameObject infoGO = GameObject.Find("Info");
            if (infoGO == null)
            {
                Debug.LogError("[InfoPanelBuilder] Não encontrei um GameObject \"Info\" na cena aberta. Selecione o painel na Hierarchy (ou abra a cena Inicio) e rode de novo.");
                return;
            }

            panel = infoGO.GetComponent<InfoPanelUI>();
            if (panel == null)
            {
                if (infoGO.GetComponent<SpriteRenderer>() == null)
                {
                    Debug.LogError("[InfoPanelBuilder] \"Info\" precisa ter um SpriteRenderer (o fundo do painel) antes de adicionar o InfoPanelUI.");
                    return;
                }

                panel = Undo.AddComponent<InfoPanelUI>(infoGO);
            }
        }

        panel.EnsureBuilt();

        Selection.activeGameObject = panel.gameObject;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[InfoPanelBuilder] Painel de Info montado — expanda \"Info > InfoCanvas > Content\" na Hierarchy pra mover/redimensionar \"Title\" e \"Description\" à vontade, depois salve a cena (Ctrl+S).");
    }
}
