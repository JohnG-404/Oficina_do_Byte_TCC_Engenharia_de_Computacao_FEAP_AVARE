using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Nas prévias "Objetivo" dos puzzles da Física
/// (PuzzlePreview.actor) e da IA (IAPuzzlePreview.boneco) o jogador era o
/// quadrado azul liso. Troca pelo primeiro quadro do Visor_Parado, parado.
/// O corpo do visor (262 px a 256 ppu) fica do mesmo tamanho do quadrado
/// antigo e o pivô dos dois é o centro do corpo, então a posição não muda.
/// O guarda não é tocado. Abre as cenas em modo Additive, salva e fecha as
/// que abriu; backup em SceneBackups/previas_visor/.
/// </summary>
public static class PreviasVisorBuilder
{
    private const string VisorSprite = "Assets/Anim/Player/Visor/idle/player_visor_idle_01.png";
    private const string QuadradoSprite = "Assets/Images/player/player_square_final.png";
    private const float CorpoVisorPx = 262f;
    private const float CorpoQuadradoPx = 261f;
    private const string Backup = "SceneBackups/previas_visor";
    private static readonly string[] Cenas = { "Assets/Scenes/Fisica.unity", "Assets/Scenes/IA.unity" };

    [MenuItem("Tools/Prévias/Visor do jogador nas prévias")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[PreviasVisor] Saia do Play Mode antes.");
            return;
        }

        Sprite visor = AssetDatabase.LoadAssetAtPath<Sprite>(VisorSprite);
        if (visor == null)
        {
            Debug.LogError("[PreviasVisor] Não achei " + VisorSprite);
            return;
        }

        Directory.CreateDirectory(Backup);
        foreach (string caminho in Cenas)
        {
            Scene cena = SceneManager.GetSceneByPath(caminho);
            bool abriu = false;
            if (!cena.IsValid() || !cena.isLoaded)
            {
                cena = EditorSceneManager.OpenScene(caminho, OpenSceneMode.Additive);
                abriu = true;
            }
            else if (cena.isDirty)
            {
                Debug.LogWarning("[PreviasVisor] " + cena.name + " tem mudanças não salvas de outro trabalho; pulei.");
                continue;
            }

            string destino = Path.Combine(Backup, Path.GetFileName(caminho));
            if (!File.Exists(destino)) File.Copy(caminho, destino);

            int n = 0;
            foreach (var p in Object.FindObjectsByType<PuzzlePreview>(FindObjectsInactive.Include))
                if (p.gameObject.scene == cena && Trocar(p, "actor", visor)) n++;
            foreach (var p in Object.FindObjectsByType<IAPuzzlePreview>(FindObjectsInactive.Include))
                if (p.gameObject.scene == cena && Trocar(p, "boneco", visor)) n++;

            if (n > 0) EditorSceneManager.SaveScene(cena);
            Debug.Log("[PreviasVisor] " + cena.name + ": visor em " + n + " prévia(s).");
            if (abriu) EditorSceneManager.CloseScene(cena, true);
        }
    }

    private static bool Trocar(Object previa, string campo, Sprite visor)
    {
        var t = new SerializedObject(previa).FindProperty(campo).objectReferenceValue as Transform;
        var sr = t != null ? t.GetComponent<SpriteRenderer>() : null;
        if (sr == null || sr.sprite == null || AssetDatabase.GetAssetPath(sr.sprite) != QuadradoSprite) return false;

        // Lado do corpo em unidades antes e depois, para manter o tamanho.
        float ladoAntigo = CorpoQuadradoPx / sr.sprite.pixelsPerUnit;
        float ladoVisor = CorpoVisorPx / visor.pixelsPerUnit;

        Undo.RecordObject(sr, "Visor na prévia");
        Undo.RecordObject(t, "Visor na prévia");
        sr.sprite = visor;
        sr.color = Color.white;
        t.localScale = t.localScale * (ladoAntigo / ladoVisor);
        EditorUtility.SetDirty(sr);
        EditorUtility.SetDirty(t);
        return true;
    }
}
