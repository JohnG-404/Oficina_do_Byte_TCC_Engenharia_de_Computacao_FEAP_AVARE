using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Coloca o botão "Fases" no Menu (clone do botão "Jogar", mesmo visual) ligado
/// a MenuControl.SelecionarFases, e reorganiza a coluna de botões pra caber os
/// quatro: Jogar/Continuar, Fases, Configurações, Sair. Idempotente.
///
/// Uso: Tools > Menu > Adicionar Botão de Fases (abre a cena Menu se a cena
/// ativa não tiver edições não salvas).
/// </summary>
public static class MenuFasesBuilder
{
    private const string MenuScenePath = "Assets/Scenes/Menu.unity";

    [MenuItem("Tools/Menu/Adicionar Botão de Fases")]
    public static void BuildMenu()
    {
        foreach (string linha in Build()) Debug.Log("[MenuFasesBuilder] " + linha);
    }

    public static List<string> Build()
    {
        var log = new List<string>();
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Menu")
        {
            if (scene.isDirty)
            {
                log.Add("ERRO: a cena ativa (" + scene.name + ") tem edições não salvas. Salve antes de rodar.");
                return log;
            }
            scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            log.Add("Cena Menu aberta.");
        }

        GameObject jogar = GameObject.Find("Canvas/PainelMenu/Jogar");
        GameObject config = GameObject.Find("Canvas/PainelMenu/Configuracoes");
        GameObject sair = GameObject.Find("Canvas/PainelMenu/Sair");
        var menuControl = Object.FindAnyObjectByType<MenuControl>();
        if (jogar == null || menuControl == null)
        {
            log.Add("ERRO: não achei Canvas/PainelMenu/Jogar ou o MenuControl.");
            return log;
        }

        Transform existente = jogar.transform.parent.Find("Fases");
        GameObject fases = existente != null ? existente.gameObject : Object.Instantiate(jogar, jogar.transform.parent);
        fases.name = "Fases";
        fases.transform.SetSiblingIndex(jogar.transform.GetSiblingIndex() + 1);

        var label = fases.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "Fases";

        // Troca o clique (o clone veio com o Jogar).
        var botao = fases.GetComponent<Button>();
        for (int i = botao.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            UnityEventTools.RemovePersistentListener(botao.onClick, i);
        }
        UnityEventTools.AddPersistentListener(botao.onClick, menuControl.SelecionarFases);
        log.Add(existente != null ? "Botão 'Fases' já existia: clique religado." : "Botão 'Fases' criado (clone do Jogar).");

        // Coluna com 4 botões, 150 de distância entre eles.
        float x = ((RectTransform)jogar.transform).anchoredPosition.x;
        SetY(jogar, x, 150f);
        SetY(fases, x, 0f);
        if (config != null) SetY(config, x, -150f);
        if (sair != null) SetY(sair, x, -300f);
        log.Add("Botões reposicionados: Jogar 150, Fases 0, Configurações -150, Sair -300.");

        // O Sair estava sem clique ligado na cena: liga no MenuControl.Sair.
        if (sair != null)
        {
            var botaoSair = sair.GetComponent<Button>();
            if (botaoSair != null && botaoSair.onClick.GetPersistentEventCount() == 0)
            {
                UnityEventTools.AddPersistentListener(botaoSair.onClick, menuControl.Sair);
                log.Add("Botão 'Sair' estava sem clique: ligado a MenuControl.Sair.");
            }
        }

        // Texto do Jogar vira "Continuar" quando há progresso.
        var so = new SerializedObject(menuControl);
        so.FindProperty("rotuloJogar").objectReferenceValue = jogar.GetComponentInChildren<TMP_Text>(true);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add("Cena salva.");
        return log;
    }

    private static void SetY(GameObject go, float x, float y)
    {
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = new Vector2(x, y);
    }
}
