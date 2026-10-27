using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Salva nas cenas o visual que antes só era montado em Play, para o Editor
/// mostrar os mesmos sprites do jogo:
/// - tema Laboratório das fases (TemaLabFases): caixa de diálogo, placa de
///   vida, "Fase Concluída!", cartões do EditorKit tingidos e fontes do kit;
/// - porta de enrolar (Door): lâminas e as duas luzes da porta_cima;
/// - objetos com Animator: o sprite salvo vira o 1º quadro do estado inicial;
/// - bandeja do Tutorial_Anim: as peças da 1ª rodada.
/// Os scripts continuam fazendo o mesmo em Play, mas reaproveitam o que já
/// está salvo. Cenas fechadas são abertas em modo Additive, salvas e fechadas;
/// backup em SceneBackups/VisualDoPlay_&lt;data&gt;/.
/// </summary>
public static class VisualDoPlayNasCenas
{
    private const string LuzPath = "Assets/Art/UI/Sprites/porta_luz.png";
    private const BindingFlags Campo = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    [MenuItem("Tools/Cenas/Salvar visual do Play nas cenas")]
    public static void Salvar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[VisualDoPlay] Saia do Play Mode antes.");
            return;
        }

        Sprite luz = GarantirSpriteLuz();
        string backup = "SceneBackups/VisualDoPlay_" + System.DateTime.Now.ToString("yyyy-MM-dd_HHmm");
        Directory.CreateDirectory(backup);

        foreach (var cena in EditorBuildSettings.scenes.Where(c => c.enabled))
        {
            Scene s = SceneManager.GetSceneByPath(cena.path);
            bool abriu = false;
            if (!s.isLoaded)
            {
                s = EditorSceneManager.OpenScene(cena.path, OpenSceneMode.Additive);
                abriu = true;
            }
            else if (s.isDirty)
            {
                Debug.LogWarning("[VisualDoPlay] " + s.name + " tem mudanças não salvas; pulei.");
                continue;
            }

            File.Copy(cena.path, Path.Combine(backup, Path.GetFileName(cena.path)), true);

            TemaLabFases.AplicarNaCena(s);
            foreach (var raiz in s.GetRootGameObjects())
            {
                foreach (var dm in raiz.GetComponentsInChildren<DialogueManager>(true))
                {
                    TemaLabFases.EstilizarDialogo(dm.dialoguePanel, dm.dialogueText);
                }
                foreach (var fim in raiz.GetComponentsInChildren<LevelCompleteUI>(true))
                {
                    TemaLabFases.EstilizarFimDeFase(fim.gameObject);
                }
                foreach (var vida in raiz.GetComponentsInChildren<HeartHealthUI>(true))
                {
                    Vida(vida);
                }
                foreach (var porta in raiz.GetComponentsInChildren<Door>(true))
                {
                    Porta(porta, luz);
                }
                foreach (var anim in raiz.GetComponentsInChildren<Animator>(true))
                {
                    PrimeiroQuadro(anim);
                }
                foreach (var puzzle in raiz.GetComponentsInChildren<AnimationSequencePuzzle>(true))
                {
                    Bandeja(puzzle);
                }
            }

            // Mudanças feitas por código em instâncias de prefab (ex.: o DialogueBox da
            // Aplicacao_Fisica) só são salvas se virarem override.
            foreach (var raiz in s.GetRootGameObjects())
            {
                foreach (var c in raiz.GetComponentsInChildren<Component>(true))
                {
                    if (c != null && PrefabUtility.IsPartOfPrefabInstance(c))
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(c);
                    }
                }
                foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(s);
            EditorSceneManager.SaveScene(s);
            Debug.Log("[VisualDoPlay] " + s.name + " salva.");
            if (abriu)
            {
                EditorSceneManager.CloseScene(s, true);
            }
        }
    }

    // ---------------------------------------------------------------- vida

    private static void Vida(HeartHealthUI vida)
    {
        var so = new SerializedObject(vida);
        SerializedProperty hearts = so.FindProperty("hearts");
        Image[] quadrados = Enumerable.Range(0, hearts.arraySize)
            .Select(i => hearts.GetArrayElementAtIndex(i).objectReferenceValue as Image).ToArray();
        if (quadrados.Length == 0 || quadrados[0] == null)
        {
            return;
        }

        if (quadrados[0].sprite == null)
        {
            // Mesmo que o Awake fazia em Play; a vida começa cheia (sprite do kit, branco).
            if (!TemaLabFases.EstilizarVida(vida.transform as RectTransform, quadrados))
            {
                return;
            }
            so.Update();
            so.FindProperty("usarSpritesDoTema").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach (Image q in quadrados.Where(q => q != null))
            {
                q.color = Color.white;
            }
        }
        else if (!so.FindProperty("usarSpritesDoTema").boolValue)
        {
            // Quadrados com sprite próprio (Interacao_Vida) são pintados com a cor de "cheio".
            Color cheio = so.FindProperty("fullColor").colorValue;
            foreach (Image q in quadrados.Where(q => q != null))
            {
                q.color = cheio;
            }
        }
    }

    // ---------------------------------------------------------------- porta

    private static void Porta(Door porta, Sprite luz)
    {
        var so = new SerializedObject(porta);
        if (!so.FindProperty("portaDeEnrolar").boolValue)
        {
            return;
        }
        so.FindProperty("luzSprite").objectReferenceValue = luz;
        so.ApplyModifiedPropertiesWithoutUndo();
        porta.PrepararPortaDeEnrolar();
    }

    private static Sprite GarantirSpriteLuz()
    {
        if (!File.Exists(LuzPath))
        {
            Texture2D tex = Door.CriarSpriteCirculo(32).texture;
            File.WriteAllBytes(LuzPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(LuzPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(LuzPath);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 100f;
            imp.filterMode = FilterMode.Bilinear;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(LuzPath);
    }

    // ---------------------------------------------------------------- animator

    private static void PrimeiroQuadro(Animator anim)
    {
        var sr = anim.GetComponent<SpriteRenderer>();
        var controller = anim.runtimeAnimatorController as AnimatorController;
        if (sr == null || controller == null || controller.layers.Length == 0)
        {
            return;
        }

        var clip = controller.layers[0].stateMachine.defaultState?.motion as AnimationClip;
        if (clip == null)
        {
            return;
        }

        EditorCurveBinding binding = AnimationUtility.GetObjectReferenceCurveBindings(clip)
            .FirstOrDefault(b => b.type == typeof(SpriteRenderer) && b.path == "" && b.propertyName == "m_Sprite");
        ObjectReferenceKeyframe[] quadros = binding.type != null ? AnimationUtility.GetObjectReferenceCurve(clip, binding) : null;
        if (quadros != null && quadros.Length > 0 && quadros[0].value is Sprite primeiro && sr.sprite != primeiro)
        {
            sr.sprite = primeiro;
        }
    }

    // ---------------------------------------------------------------- tutorial

    private static void Bandeja(AnimationSequencePuzzle puzzle)
    {
        var pecas = Ler(puzzle, "trayPieces") as SequencePiece[];
        var rodadas = Ler(puzzle, "rounds") as IList;
        if (pecas == null || rodadas == null || rodadas.Count == 0)
        {
            return;
        }

        object rodada = rodadas[0];
        var certos = Ler(rodada, "correctFrames") as Sprite[] ?? new Sprite[0];
        var iscas = Ler(rodada, "decoyFrames") as Sprite[] ?? new Sprite[0];
        var pool = certos.Select((s, i) => (s, i + 1)).Concat(iscas.Where(s => s != null).Select(s => (s, 0))).ToList();
        float tamanho = (float)Ler(puzzle, "tamanhoPecaBandeja");

        for (int i = 0; i < pecas.Length; i++)
        {
            if (pecas[i] == null || i >= pool.Count || pool[i].Item1 == null)
            {
                continue;
            }
            // O Configure usa o SpriteRenderer/collider que o Awake guardaria.
            Escrever(pecas[i], "sr", pecas[i].GetComponent<SpriteRenderer>());
            Escrever(pecas[i], "col", pecas[i].GetComponent<Collider2D>());
            pecas[i].Configure(pool[i].Item1, pool[i].Item2, tamanho);
            EditorUtility.SetDirty(pecas[i]);
        }
    }

    private static object Ler(object alvo, string nome)
    {
        for (System.Type t = alvo.GetType(); t != null; t = t.BaseType)
        {
            FieldInfo f = t.GetField(nome, Campo);
            if (f != null) return f.GetValue(alvo);
        }
        return null;
    }

    private static void Escrever(object alvo, string nome, object valor)
    {
        for (System.Type t = alvo.GetType(); t != null; t = t.BaseType)
        {
            FieldInfo f = t.GetField(nome, Campo);
            if (f != null && (valor == null || f.FieldType.IsInstanceOfType(valor)))
            {
                f.SetValue(alvo, valor);
                return;
            }
        }
    }
}
