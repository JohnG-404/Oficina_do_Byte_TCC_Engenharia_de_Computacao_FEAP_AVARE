using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Aplica na cena Inicio o mesmo visual de fundo da caixa de diálogo usado
/// na cena Aplicacao (sprite "dialog_panel_black_gradient" tingido de
/// branco, em vez do sprite de UI genérico da Unity tingido de azul que a
/// cena Inicio usava). Só troca a APARÊNCIA do fundo — não mexe em
/// posição/tamanho da caixa, que já foram ajustados à mão pra caber no
/// layout da cena Inicio.
///
/// Uso: com a cena Inicio aberta, Tools > Inicio > Aplicar Design de Diálogo (igual Aplicacao).
/// </summary>
public static class InicioDialogueDesignTool
{
    private const string PanelSpritePath = "Assets/Images/dialog_panel_black_gradient.png";

    [MenuItem("Tools/Inicio/Aplicar Design de Diálogo (igual Aplicacao)")]
    public static void ApplyDialogueDesign()
    {
        GameObject dialogueBox = GameObject.Find("DialogueBox");
        if (dialogueBox == null)
        {
            Debug.LogError("[InicioDialogueDesignTool] Não encontrei um GameObject \"DialogueBox\" na cena aberta. Abra a cena Inicio e rode de novo.");
            return;
        }

        Image panelImage = dialogueBox.GetComponent<Image>();
        if (panelImage == null)
        {
            Debug.LogError("[InicioDialogueDesignTool] \"DialogueBox\" não tem um componente Image — não há fundo pra trocar.");
            return;
        }

        Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath);
        if (panelSprite == null)
        {
            Debug.LogError($"[InicioDialogueDesignTool] Não encontrei o sprite em {PanelSpritePath}.");
            return;
        }

        Undo.RecordObject(panelImage, "Aplicar design de diálogo");
        panelImage.sprite = panelSprite;
        panelImage.type = Image.Type.Simple;
        panelImage.color = Color.white;
        EditorUtility.SetDirty(panelImage);

        Debug.Log("[InicioDialogueDesignTool] Fundo da caixa de diálogo atualizado — salve a cena (Ctrl+S).");
    }
}
