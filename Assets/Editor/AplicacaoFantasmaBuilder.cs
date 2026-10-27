using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Liga o modelo visual "fantasma + mini Inspector" na cena Aplicacao_Fisica:
/// - Gera o tracejado (textura + material) e a seta do Rigidbody2D.
/// - Põe um ColisaoFantasma em cada PhysicsTarget.
/// - Monta o MiniInspector no Canvas, no mesmo estilo da dica do inventário.
///
/// Funciona com a cena aberta sozinha ou junto com outras (modo aditivo).
/// Pode rodar de novo: refaz o MiniInspector e reconfigura os fantasmas.
/// </summary>
public static class AplicacaoFantasmaBuilder
{
    private const string ScenePath = "Assets/Scenes/Aplicacao_Fisica.unity";
    private const string SpriteDir = "Assets/Art/UI/Aplicacao/";
    private const string FontDir = "Assets/Art/UI/Fonts/";
    // Fora de Art/UI: o LaboratorioUIImporter força tudo de lá a virar Sprite
    // com Clamp, e o tracejado precisa repetir (Repeat) ao longo da linha.
    private const string LinhaDir = "Assets/Art/Aplicacao/";
    private const string TracejadoPng = LinhaDir + "tracejado.png";
    private const string TracejadoMat = LinhaDir + "Tracejado.mat";
    private const string SetaPng = SpriteDir + "seta_rigidbody2d.png";

    private static readonly Color Texto = Hex("E6E9EF");
    private static readonly Color TextoApagado = Hex("8A93A6");
    private static readonly Color Fundo = Hex("0B0F18", 0.97f);
    private static readonly Color Borda = Hex("2E323C");

    [MenuItem("Tools/Aplicação/Montar Fantasma e Mini Inspector")]
    public static void BuildMenu()
    {
        foreach (string line in Build()) Debug.Log("[AplicacaoFantasmaBuilder] " + line);
    }

    public static List<string> Build()
    {
        var log = new List<string>();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            log.Add("ERRO: abra a cena Aplicacao_Fisica antes de rodar.");
            return log;
        }

        Material mat = CriarTracejado();
        Sprite seta = CriarSeta();
        log.Add("tracejado: " + (mat != null) + ", seta: " + (seta != null));

        foreach (GameObject raiz in scene.GetRootGameObjects())
        {
            foreach (PhysicsTarget pt in raiz.GetComponentsInChildren<PhysicsTarget>(true))
            {
                var f = pt.GetComponent<ColisaoFantasma>();
                if (f == null)
                {
                    f = Undo.AddComponent<ColisaoFantasma>(pt.gameObject);
                }

                var so = new SerializedObject(f);
                so.FindProperty("materialTracejado").objectReferenceValue = mat;
                so.FindProperty("spriteSeta").objectReferenceValue = seta;
                so.ApplyModifiedPropertiesWithoutUndo();
                log.Add("fantasma: " + pt.name + (new SerializedObject(pt).FindProperty("comecaComColisao").boolValue ? " (já sólido)" : ""));
            }
        }

        log.AddRange(MontarMiniInspector(scene));
        EditorSceneManager.MarkSceneDirty(scene);
        return log;
    }

    // --------------------------------------------------------------- assets

    private static Material CriarTracejado()
    {
        // 32x4: metade branca (traço), metade transparente (espaço).
        if (!AssetDatabase.IsValidFolder("Assets/Art/Aplicacao"))
        {
            AssetDatabase.CreateFolder("Assets/Art", "Aplicacao");
        }

        var tex = new Texture2D(32, 4, TextureFormat.RGBA32, false);
        for (int x = 0; x < 32; x++)
            for (int y = 0; y < 4; y++)
                tex.SetPixel(x, y, x < 18 ? Color.white : new Color(1f, 1f, 1f, 0f));
        File.WriteAllBytes(TracejadoPng, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(TracejadoPng, ImportAssetOptions.ForceUpdate);

        var imp = (TextureImporter)AssetImporter.GetAtPath(TracejadoPng);
        imp.textureType = TextureImporterType.Default;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapModeU = TextureWrapMode.Repeat;
        imp.wrapModeV = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(TracejadoPng);

        var mat = AssetDatabase.LoadAssetAtPath<Material>(TracejadoMat);
        if (mat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            mat = new Material(sh);
            AssetDatabase.CreateAsset(mat, TracejadoMat);
        }

        mat.mainTexture = t;
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", t);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    private static Sprite CriarSeta()
    {
        // Seta branca para baixo (tingida de azul pelo componente), 32x40, 100 px por unidade.
        const int W = 32, H = 40;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float cx = x + 0.5f - W / 2f;
                bool haste = y >= 16 && Mathf.Abs(cx) <= 4.5f;
                bool ponta = y < 18 && Mathf.Abs(cx) <= (y + 0.5f) * (W / 2f) / 18f;
                tex.SetPixel(x, y, haste || ponta ? Color.white : new Color(1f, 1f, 1f, 0f));
            }
        }

        File.WriteAllBytes(SetaPng, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(SetaPng, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(SetaPng);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 100f;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.filterMode = FilterMode.Bilinear;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(SetaPng);
    }

    // -------------------------------------------------------- mini Inspector

    private static List<string> MontarMiniInspector(Scene scene)
    {
        var log = new List<string>();
        Canvas canvas = scene.GetRootGameObjects().Select(g => g.GetComponent<Canvas>()).FirstOrDefault(c => c != null);
        if (canvas == null)
        {
            log.Add("ERRO: Canvas não encontrado.");
            return log;
        }

        Transform velho = canvas.transform.Find("MiniInspector");
        if (velho != null)
        {
            Undo.DestroyObjectImmediate(velho.gameObject);
        }

        RectTransform raiz = Novo("MiniInspector", canvas.transform);
        Undo.RegisterCreatedObjectUndo(raiz.gameObject, "Mini Inspector");
        Esticar(raiz);
        raiz.SetAsLastSibling();

        RectTransform caixa = Novo("Caixa", raiz);
        caixa.anchorMin = caixa.anchorMax = new Vector2(0.5f, 0.5f);
        caixa.pivot = new Vector2(0f, 1f);
        caixa.sizeDelta = new Vector2(330f, 130f);
        Img(caixa, S("inv_arredondado.png"), Fundo, true);
        RectTransform bordaRt = Novo("Borda", caixa);
        Esticar(bordaRt);
        Img(bordaRt, S("inv_borda.png"), Borda, true);

        TMP_Text titulo = Rotulo(caixa, "Titulo", "Plataforma4", "JetBrainsMono-Bold SDF", 18, Texto, new Vector2(16f, -10f), new Vector2(300f, 26f), TextAlignmentOptions.MidlineLeft);

        var tipos = new[] { ToolType.Colisao, ToolType.Fisica, ToolType.Movimento, ToolType.Pulo };
        var linhas = new List<(ToolType, GameObject, TMP_Text, TMP_Text)>();
        for (int i = 0; i < tipos.Length; i++)
        {
            RectTransform linha = Novo("Linha_" + DraggableToolIcon.NomeNoUnity(tipos[i]), caixa);
            Canto(linha, new Vector2(16f, -44f - i * 28f), new Vector2(300f, 26f));
            TMP_Text nome = Rotulo(linha, "Nome", DraggableToolIcon.NomeNoUnity(tipos[i]), "JetBrainsMono-Bold SDF", 16, ToolColors.For(tipos[i]), Vector2.zero, new Vector2(160f, 26f), TextAlignmentOptions.MidlineLeft);
            TMP_Text estado = Rotulo(linha, "Estado", "falta", "Nunito-Bold SDF", 16, TextoApagado, new Vector2(160f, 0f), new Vector2(140f, 26f), TextAlignmentOptions.MidlineLeft);
            linhas.Add((tipos[i], linha.gameObject, nome, estado));
        }

        TMP_Text rodape = Rotulo(caixa, "Rodape", "arraste do inventário", "Nunito-Bold SDF", 14, TextoApagado, new Vector2(16f, -100f), new Vector2(300f, 22f), TextAlignmentOptions.MidlineLeft);
        caixa.gameObject.SetActive(false);

        var mi = raiz.gameObject.AddComponent<MiniInspector>();
        var so = new SerializedObject(mi);
        so.FindProperty("caixa").objectReferenceValue = caixa;
        so.FindProperty("titulo").objectReferenceValue = titulo;
        so.FindProperty("rodape").objectReferenceValue = rodape;
        SerializedProperty arr = so.FindProperty("linhas");
        arr.arraySize = linhas.Count;
        for (int i = 0; i < linhas.Count; i++)
        {
            SerializedProperty e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("ferramenta").intValue = (int)linhas[i].Item1;
            e.FindPropertyRelative("raiz").objectReferenceValue = linhas[i].Item2;
            e.FindPropertyRelative("nome").objectReferenceValue = linhas[i].Item3;
            e.FindPropertyRelative("estado").objectReferenceValue = linhas[i].Item4;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        log.Add("MiniInspector montado no Canvas " + canvas.name);
        return log;
    }

    // ---------------------------------------------------------------- helpers

    private static Sprite S(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + file);

    private static RectTransform Novo(string nome, Transform pai)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(pai, false);
        return rt;
    }

    private static void Canto(RectTransform rt, Vector2 pos, Vector2 tam)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
    }

    private static void Esticar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Image Img(RectTransform rt, Sprite sprite, Color cor, bool fatiado = false)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = cor;
        img.raycastTarget = false;
        if (fatiado && sprite != null)
        {
            img.type = Image.Type.Sliced;
        }
        return img;
    }

    private static TMP_Text Rotulo(RectTransform pai, string nome, string texto, string fonte, float tamanho, Color cor, Vector2 pos, Vector2 tam, TextAlignmentOptions alinhamento)
    {
        RectTransform rt = Novo(nome, pai);
        Canto(rt, pos, tam);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDir + fonte + ".asset");
        tmp.text = texto;
        tmp.fontSize = tamanho;
        tmp.color = cor;
        tmp.alignment = alinhamento;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Color Hex(string hex, float a = 1f)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        c.a = a;
        return c;
    }
}
