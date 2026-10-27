using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Salva o Animator do visor (PlayerVisorAnimator) no Player de cada fase, com o
/// primeiro quadro do Visor_Parado no SpriteRenderer. Assim o editor mostra o
/// mesmo sprite que o Play. Cenas fechadas são abertas em modo Additive, salvas e
/// fechadas; backup em SceneBackups/PlayerVisor_&lt;data&gt;/.
/// </summary>
public static class PlayerVisorSceneInstaller
{
    [MenuItem("Tools/Player/Salvar visor nas cenas")]
    public static void SalvarNasCenas()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[PlayerVisor] Saia do Play Mode antes.");
            return;
        }
        var controller = Resources.Load<RuntimeAnimatorController>(PlayerVisorAnimator.ControllerResource);
        var parado = PrimeiroQuadro(controller, PlayerVisorAnimator.EstadoParado);
        if (controller == null || parado == null)
        {
            Debug.LogError("[PlayerVisor] Não achei o controller ou o clip Visor_Parado.");
            return;
        }

        string backup = "SceneBackups/PlayerVisor_" + System.DateTime.Now.ToString("yyyy-MM-dd_HHmm");
        Directory.CreateDirectory(backup);

        foreach (var cena in EditorBuildSettings.scenes.Where(c => c.enabled))
        {
            var s = SceneManager.GetSceneByPath(cena.path);
            bool abriu = false;
            if (!s.isLoaded)
            {
                s = EditorSceneManager.OpenScene(cena.path, OpenSceneMode.Additive);
                abriu = true;
            }
            else if (s.isDirty)
            {
                Debug.LogWarning("[PlayerVisor] " + s.name + " tem mudanças não salvas; pulei.");
                continue;
            }

            int n = 0;
            foreach (var go in Players(s))
            {
                // Player que vem de prefab: muda o prefab, as instâncias herdam.
                if (PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                    File.Copy(path, Path.Combine(backup, Path.GetFileName(path)), true);
                    var raiz = PrefabUtility.LoadPrefabContents(path);
                    foreach (var pgo in raiz.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject).Where(EhPlayer))
                        Instalar(pgo, controller, parado);
                    PrefabUtility.SaveAsPrefabAsset(raiz, path);
                    PrefabUtility.UnloadPrefabContents(raiz);
                    // Tira overrides de sprite da instância para ela pegar o do prefab.
                    var sr = go.GetComponent<SpriteRenderer>();
                    if (sr != null) PrefabUtility.RevertObjectOverride(sr, InteractionMode.AutomatedAction);
                }
                else Instalar(go, controller, parado);
                n++;
            }

            if (n > 0)
            {
                File.Copy(cena.path, Path.Combine(backup, Path.GetFileName(cena.path)), true);
                EditorSceneManager.MarkSceneDirty(s);
                EditorSceneManager.SaveScene(s);
                Debug.Log("[PlayerVisor] " + s.name + ": visor salvo em " + n + " player(s).");
            }
            if (abriu) EditorSceneManager.CloseScene(s, true);
        }
    }

    private static bool EhPlayer(GameObject go) =>
        go.GetComponent<AnimacaoPlayerFisica>() == null &&
        (go.GetComponent<Player>() != null || go.GetComponent<InteracaoPlayerController>() != null ||
         go.GetComponent<AplicacaoPlayerController>() != null);

    private static GameObject[] Players(Scene s) =>
        s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .Select(t => t.gameObject).Where(EhPlayer).ToArray();

    private static void Instalar(GameObject go, RuntimeAnimatorController controller, Sprite parado)
    {
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        var anim = go.GetComponent<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null && anim.runtimeAnimatorController != controller) return;
        if (anim == null) anim = Undo.AddComponent<Animator>(go);
        Undo.RecordObject(anim, "Visor");
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        Undo.RecordObject(sr, "Visor");
        sr.drawMode = SpriteDrawMode.Simple;
        sr.sprite = parado;
        if (go.GetComponent<PlayerVisorAnimator>() == null) Undo.AddComponent<PlayerVisorAnimator>(go);
    }

    private static Sprite PrimeiroQuadro(RuntimeAnimatorController controller, string clip)
    {
        var c = controller != null ? controller.animationClips.FirstOrDefault(a => a.name == clip) : null;
        if (c == null) return null;
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(c).FirstOrDefault(b => b.type == typeof(SpriteRenderer));
        var quadros = AnimationUtility.GetObjectReferenceCurve(c, binding);
        return quadros != null && quadros.Length > 0 ? quadros[0].value as Sprite : null;
    }
}
