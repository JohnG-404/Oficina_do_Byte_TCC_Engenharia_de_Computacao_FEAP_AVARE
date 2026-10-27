using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Acrescenta as Ações e Consequências novas (13/10) aos objetos que já
/// estão nas cenas Interacao e Interacao_Vida, reconhecidos pelo nome
/// mostrado no assistente (displayName). Só adiciona o que falta: as
/// opções antigas e a ordem delas ficam como estão.
///
/// Tools > Interação > Adicionar novas interações às cenas
/// </summary>
public static class NovasInteracoesTool
{
    private class Extras
    {
        public ActionType[] acoes = new ActionType[0];
        public ConsequenceType[] consequencias = new ConsequenceType[0];
    }

    private static readonly Dictionary<string, Extras> PorNome = new Dictionary<string, Extras>
    {
        { "Laser", new Extras {
            acoes = new[] { ActionType.LeaveArea },
            consequencias = new[] { ConsequenceType.DisableForSeconds, ConsequenceType.TurnOnBehindPlayer } } },
        { "Espinhos", new Extras {
            consequencias = new[] { ConsequenceType.ReturnToSafePoint, ConsequenceType.SinkIntoGround } } },
        { "Vida", new Extras {
            acoes = new[] { ActionType.RequiresMissingHealth },
            consequencias = new[] { ConsequenceType.ShieldForSeconds, ConsequenceType.IncreaseMaxHealth } } },
        { "Vida Falsa", new Extras {
            acoes = new[] { ActionType.RequiresMissingHealth },
            consequencias = new[] { ConsequenceType.SlowPlayer, ConsequenceType.InvertControls } } },
        { "Plataforma Falsa", new Extras {
            consequencias = new[] { ConsequenceType.FallAfterDelay, ConsequenceType.MakeSlippery, ConsequenceType.OneWayFromBelow } } },
        { "Plataforma de Lançamento", new Extras {
            consequencias = new[] { ConsequenceType.LaunchSideways } } },
        { "Porta", new Extras {
            acoes = new[] { ActionType.PressButtonThreeTimes },
            consequencias = new[] { ConsequenceType.OpenDoorForSeconds } } },
        { "Parede", new Extras {
            consequencias = new[] { ConsequenceType.PassableWhileNear } } },
        { "Plataforma de Movimento", new Extras {
            acoes = new[] { ActionType.StayOnTop } } },
    };

    private static readonly string[] Cenas = { "Assets/Scenes/Interacao.unity", "Assets/Scenes/Interacao_Vida.unity" };

    [MenuItem("Tools/Interação/Adicionar novas interações às cenas")]
    public static void AdicionarEmTodasAsCenas()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        string cenaAberta = EditorSceneManager.GetActiveScene().path;
        foreach (string cena in Cenas)
        {
            EditorSceneManager.OpenScene(cena, OpenSceneMode.Single);
            int alterados = AdicionarNaCenaAberta();
            if (alterados > 0)
            {
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            }
            Debug.Log($"[NovasInteracoes] {cena}: {alterados} objeto(s) atualizado(s).");
        }

        if (!string.IsNullOrEmpty(cenaAberta))
        {
            EditorSceneManager.OpenScene(cenaAberta, OpenSceneMode.Single);
        }
    }

    /// <summary>Acrescenta as opções novas aos itens da cena aberta. Devolve quantos itens mudaram (a cena fica marcada como alterada).</summary>
    public static int AdicionarNaCenaAberta()
    {
        int alterados = 0;
        foreach (var item in Object.FindObjectsByType<InteractableItem>(FindObjectsInactive.Include))
        {
            if (!PorNome.TryGetValue(item.DisplayName, out Extras extras))
            {
                continue;
            }

            var so = new SerializedObject(item);
            bool mudou = Acrescentar(so.FindProperty("allowedActions"), extras.acoes.Select(a => (int)a));
            mudou |= Acrescentar(so.FindProperty("allowedConsequences"), extras.consequencias.Select(c => (int)c));
            if (mudou)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(item);
                alterados++;
            }
        }

        if (alterados > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
        return alterados;
    }

    // Os valores do enum não são sequenciais (ActionType pula o 4), então
    // compara pelo valor real com intValue, não pelo índice.
    private static bool Acrescentar(SerializedProperty lista, IEnumerable<int> valores)
    {
        var atuais = new HashSet<int>();
        for (int i = 0; i < lista.arraySize; i++)
        {
            atuais.Add(lista.GetArrayElementAtIndex(i).intValue);
        }

        bool mudou = false;
        foreach (int v in valores)
        {
            if (atuais.Contains(v))
            {
                continue;
            }
            lista.arraySize++;
            lista.GetArrayElementAtIndex(lista.arraySize - 1).intValue = v;
            atuais.Add(v);
            mudou = true;
        }
        return mudou;
    }
}
