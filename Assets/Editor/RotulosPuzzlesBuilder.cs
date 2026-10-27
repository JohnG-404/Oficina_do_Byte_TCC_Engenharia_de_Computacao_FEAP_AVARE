using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Rótulos maiores nos puzzles
/// da Física e da IA, salvos nas cenas sem rodar os builders de novo.
/// - Legendas dos quadros ("SCRIPT · PLAYER.CS", "INSPECTOR", "CÉREBRO DO GUARDA · ...")
///   de ~10 para ~16 px em 1080p (altura 0.19 nos builders).
/// - Estados da prévia da IA (Patrulhar, Perseguir, Atacar) de ~11 para ~18 px
///   (altura 0.2 nos builders), encolhendo só se não couberem na caixa.
/// Os tamanhos vão fixos porque a medida dos builders (textBounds) dá zero em
/// objetos desativados, e a maioria dos puzzles fica desativada na cena. Os puzzles de UI (Animator, objetos,
/// Interação e Vida) mudam por código em MolduraPuzzleUI e AnimatorPuzzleUI.
/// Idempotente. Recusa em Play ou com a cena suja. Backup em
/// SceneBackups/rotulos_puzzles/.
/// </summary>
public static class RotulosPuzzlesBuilder
{
    private const string CenaFisica = "Assets/Scenes/Fisica.unity";
    private const string CenaIA = "Assets/Scenes/IA.unity";
    // fontSize que os builders chegam com AjustarAltura(0.19) e AjustarAltura(0.2).
    private const float FonteLegenda = 1.393f;
    private const float FonteEstado = 1.466f;
    private static readonly string[] Estados = { "Patrulhar", "Perseguir", "Atacar" };

    [MenuItem("Tools/Fases/Rótulos dos puzzles da Física e da IA")]
    public static void Menu()
    {
        foreach (string l in Aplicar()) Debug.Log("[RotulosPuzzles] " + l);
    }

    public static List<string> Aplicar()
    {
        var log = new List<string>();
        foreach (string path in new[] { CenaFisica, CenaIA })
        {
            NaCena(path, log, cena =>
            {
                foreach (GameObject raiz in cena.GetRootGameObjects())
                {
                    foreach (TextMeshPro t in raiz.GetComponentsInChildren<TextMeshPro>(true))
                    {
                        if (t.name == "Legenda" && EhLegendaDeQuadro(t.text))
                        {
                            Undo.RecordObject(t, "Rótulos dos puzzles");
                            t.fontSize = FonteLegenda;
                            log.Add($"{cena.name}: legenda \"{t.text}\" fontSize {t.fontSize:0.###}");
                        }
                        else if (t.transform.parent != null && t.transform.parent.name == "Preview" && Array.IndexOf(Estados, t.text) >= 0 && t.name == "Texto_" + t.text)
                        {
                            Undo.RecordObject(t, "Rótulos dos puzzles");
                            t.fontSize = FonteEstado;
                            Transform caixa = t.transform.parent.Find("Estado_" + t.text);
                            SpriteRenderer sr = caixa != null ? caixa.GetComponent<SpriteRenderer>() : null;
                            if (sr != null)
                            {
                                float larg = sr.bounds.size.x;
                                if (t.preferredWidth > larg - 0.1f) t.fontSize *= (larg - 0.1f) / t.preferredWidth;
                            }
                            log.Add($"{cena.name}: estado {t.text} fontSize {t.fontSize:0.###}");
                        }
                    }
                }
            });
        }
        return log;
    }

    private static bool EhLegendaDeQuadro(string texto)
    {
        return texto.StartsWith("SCRIPT") || texto == "INSPECTOR" || texto.StartsWith("CÉREBRO");
    }

    private static void NaCena(string path, List<string> log, Action<Scene> montar)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            log.Add("ERRO: saia do Play Mode antes.");
            return;
        }

        Scene cena = SceneManager.GetSceneByPath(path);
        bool abriAqui = false;
        if (!cena.isLoaded)
        {
            cena = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            abriAqui = true;
        }
        else if (cena.isDirty)
        {
            log.Add($"ERRO: {cena.name} tem mudanças não salvas. Salve ou descarte antes.");
            return;
        }

        montar(cena);
        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        log.Add($"{cena.name} salva.");
        if (abriAqui) EditorSceneManager.CloseScene(cena, true);
    }
}
