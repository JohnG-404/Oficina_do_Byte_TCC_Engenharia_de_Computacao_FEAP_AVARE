using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Leva as três moedas da cena Animacao do chão do túnel de agachar para a
/// escada escura antes dele: uma por degrau, na altura do peito do robô,
/// maiores (0,70) e desenhadas por cima da escuridão das lâmpadas, com um
/// brilho que pulsa até o Animator da moeda ser configurado
/// (ObjetoAnimadoInteracao.brilho). Também deixa o balão da tecla dizer de
/// quem é o Animator ("da moeda" / "da lâmpada").
/// Idempotente. Abre a Animacao em modo Additive se ela não estiver aberta
/// e salva a cena no fim.
/// </summary>
public static class MoedasEscadaBuilder
{
    private const string CenaAnimacao = "Assets/Scenes/Animacao.unity";
    private const string CaminhoBrilho = "Assets/Anim/Moeda/brilho_moeda.png";

    // Um por degrau (pisos em y −3,54 / −2,74 / −1,94), 0,6 acima do piso.
    // A terceira fica longe da borda da vala de espinhos (x 28,5).
    private static readonly Vector3[] Posicoes =
    {
        new Vector3(20.3f, -2.94f, 0f),
        new Vector3(22.7f, -2.14f, 0f),
        new Vector3(25.3f, -1.34f, 0f),
    };

    private const float Tamanho = 0.70f;          // escala no mundo (antes 0,45)
    private const float DiametroBrilho = 1.9f;    // unidades no mundo
    private const int OrdemMoeda = 9;             // acima da lâmpada (7) e da luz (6)
    private const int OrdemBrilho = 8;
    private static readonly Color CorBrilho = new Color(1f, 0.82f, 0.25f, 0.8f);

    private const string DetalheMoeda = "da moeda · ou clique";
    private const string DetalheLampada = "da lâmpada · ou clique";

    [MenuItem("Tools/Animação/Moedas na escada (maiores e brilhando)")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[MoedasEscadaBuilder] Saia do Play antes de rodar.");
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
            Debug.LogError("[MoedasEscadaBuilder] A Animacao tem edições não salvas. Salve ou descarte antes de rodar.");
            return;
        }

        Sprite spriteBrilho = GarantirSpriteBrilho();

        var moedas = new List<MoedaFase>();
        foreach (GameObject raiz in cena.GetRootGameObjects())
        {
            moedas.AddRange(raiz.GetComponentsInChildren<MoedaFase>(true));
        }
        moedas.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

        if (moedas.Count != Posicoes.Length)
        {
            Debug.LogError($"[MoedasEscadaBuilder] Esperava {Posicoes.Length} moedas, achei {moedas.Count}. Nada mudou.");
            if (abriu) EditorSceneManager.CloseScene(cena, true);
            return;
        }

        for (int i = 0; i < moedas.Count; i++)
        {
            ArrumarMoeda(moedas[i], i, spriteBrilho);
        }

        int lampadas = 0;
        foreach (GameObject raiz in cena.GetRootGameObjects())
        {
            foreach (LampadaSensor l in raiz.GetComponentsInChildren<LampadaSensor>(true))
            {
                var so = new SerializedObject(l);
                var texto = so.FindProperty("dicaClique").objectReferenceValue as Component;
                DicaTecla dica = texto != null ? texto.GetComponent<DicaTecla>() : null;
                if (dica != null && DefinirDetalhe(dica, DetalheLampada)) lampadas++;
            }
        }

        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        if (abriu)
        {
            EditorSceneManager.CloseScene(cena, true);
        }
        Debug.Log($"[MoedasEscadaBuilder] {moedas.Count} moedas na escada, {lampadas} balão de lâmpada. Cena salva.");
    }

    private static void ArrumarMoeda(MoedaFase moeda, int indice, Sprite spriteBrilho)
    {
        Transform t = moeda.transform;
        t.name = $"Moeda_Degrau{indice + 1}";
        t.position = Posicoes[indice];
        float escalaPai = t.parent != null ? t.parent.lossyScale.x : 1f;
        t.localScale = Vector3.one * (Tamanho / escalaPai);

        var interacao = moeda.GetComponent<ObjetoAnimadoInteracao>();
        var so = new SerializedObject(interacao);
        var visual = so.FindProperty("visual").objectReferenceValue as SpriteRenderer;
        if (visual == null)
        {
            visual = t.GetComponentInChildren<SpriteRenderer>(true);
        }
        visual.sortingOrder = OrdemMoeda;
        EditorUtility.SetDirty(visual);

        Transform tb = t.Find("Brilho");
        if (tb == null)
        {
            tb = new GameObject("Brilho").transform;
            tb.SetParent(t, false);
            tb.SetAsFirstSibling();
        }
        tb.localPosition = Vector3.zero;
        float tamanhoSprite = spriteBrilho.bounds.size.x;
        tb.localScale = Vector3.one * (DiametroBrilho / tamanhoSprite / Tamanho);

        var sr = tb.GetComponent<SpriteRenderer>();
        if (sr == null) sr = tb.gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = spriteBrilho;
        sr.sharedMaterial = visual.sharedMaterial;
        sr.sortingLayerID = visual.sortingLayerID;
        sr.sortingOrder = OrdemBrilho;
        sr.color = CorBrilho;
        sr.enabled = true;

        so.FindProperty("brilho").objectReferenceValue = sr;
        so.FindProperty("brilhoAlfa").floatValue = CorBrilho.a;
        var dica = so.FindProperty("dica").objectReferenceValue as DicaTecla;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (dica != null) DefinirDetalhe(dica, DetalheMoeda);
        EditorUtility.SetDirty(t.gameObject);
    }

    private static bool DefinirDetalhe(DicaTecla dica, string detalhe)
    {
        var so = new SerializedObject(dica);
        so.FindProperty("detalhe").stringValue = detalhe;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    // Círculo branco com borda suave (a cor vem do SpriteRenderer).
    private static Sprite GarantirSpriteBrilho()
    {
        var existente = AssetDatabase.LoadAssetAtPath<Sprite>(CaminhoBrilho);
        if (existente != null) return existente;

        const int lado = 128;
        var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
        var centro = new Vector2((lado - 1) * 0.5f, (lado - 1) * 0.5f);
        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), centro) / (lado * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }
        tex.Apply();
        File.WriteAllBytes(CaminhoBrilho, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(CaminhoBrilho);

        var imp = (TextureImporter)AssetImporter.GetAtPath(CaminhoBrilho);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 100f;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(CaminhoBrilho);
    }
}
