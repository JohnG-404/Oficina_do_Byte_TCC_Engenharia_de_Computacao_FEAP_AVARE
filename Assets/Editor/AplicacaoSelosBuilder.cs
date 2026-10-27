using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Liga os selos de componente (SelosComponentes) na cena Aplicacao_Fisica:
/// - Gera o fundo e a borda do selo (quadrado arredondado).
/// - Põe um SelosComponentes em cada PhysicsTarget e no Player, com os
///   mesmos ícones dos cartões do inventário.
///
/// Pode rodar de novo: só reconfigura.
/// </summary>
public static class AplicacaoSelosBuilder
{
    private const string ScenePath = "Assets/Scenes/Aplicacao_Fisica.unity";
    // Fora de Art/UI: lá o LaboratorioUIImporter muda o import de tudo.
    private const string Dir = "Assets/Art/Aplicacao/";
    private const string FundoPng = Dir + "selo_fundo.png";
    private const string BordaPng = Dir + "selo_borda.png";
    private const string IconeDir = "Assets/Images/Scene_Col_Rig/";

    [MenuItem("Tools/Aplicação/Montar Selos dos Componentes")]
    public static void BuildMenu()
    {
        foreach (string line in Build()) Debug.Log("[AplicacaoSelosBuilder] " + line);
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

        if (!AssetDatabase.IsValidFolder("Assets/Art/Aplicacao"))
        {
            AssetDatabase.CreateFolder("Assets/Art", "Aplicacao");
        }

        Sprite fundo = CriarQuadrado(FundoPng, 0);
        Sprite borda = CriarQuadrado(BordaPng, 5);
        Sprite colisao = AssetDatabase.LoadAssetAtPath<Sprite>(IconeDir + "icon_collider2d_v2.png");
        Sprite fisica = AssetDatabase.LoadAssetAtPath<Sprite>(IconeDir + "icon_rigidbody2d_v2.png");
        Sprite movimento = AssetDatabase.LoadAssetAtPath<Sprite>(IconeDir + "icon_movement_v2.png");
        Sprite pulo = AssetDatabase.LoadAssetAtPath<Sprite>(IconeDir + "icon_jump.png");
        log.Add("fundo: " + (fundo != null) + ", borda: " + (borda != null) + ", ícones: " + (colisao != null) + (fisica != null) + (movimento != null) + (pulo != null));

        var alvos = new List<GameObject>();
        foreach (GameObject raiz in scene.GetRootGameObjects())
        {
            foreach (PhysicsTarget pt in raiz.GetComponentsInChildren<PhysicsTarget>(true)) alvos.Add(pt.gameObject);
            foreach (AplicacaoPlayerController p in raiz.GetComponentsInChildren<AplicacaoPlayerController>(true)) alvos.Add(p.gameObject);
        }

        foreach (GameObject go in alvos)
        {
            var selos = go.GetComponent<SelosComponentes>();
            if (selos == null)
            {
                selos = Undo.AddComponent<SelosComponentes>(go);
            }

            var so = new SerializedObject(selos);
            so.FindProperty("spriteFundo").objectReferenceValue = fundo;
            so.FindProperty("spriteBorda").objectReferenceValue = borda;
            so.FindProperty("iconeColisao").objectReferenceValue = colisao;
            so.FindProperty("iconeFisica").objectReferenceValue = fisica;
            so.FindProperty("iconeMovimento").objectReferenceValue = movimento;
            so.FindProperty("iconePulo").objectReferenceValue = pulo;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add("selos: " + go.name);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        return log;
    }

    /// <summary>Quadrado arredondado branco de 64 px (128 px por unidade). espessura 0 = cheio.</summary>
    private static Sprite CriarQuadrado(string caminho, int espessura)
    {
        const int N = 64;
        const float raio = 14f;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float dFora = DistanciaArredondada(x + 0.5f, y + 0.5f, N, raio);
                float a = Mathf.Clamp01(0.5f - dFora);
                if (espessura > 0)
                {
                    a *= Mathf.Clamp01(dFora + espessura + 0.5f);
                }

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        File.WriteAllBytes(caminho, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(caminho);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 128f;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.filterMode = FilterMode.Bilinear;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
    }

    /// <summary>Distância com sinal até a borda do quadrado arredondado (negativa dentro).</summary>
    private static float DistanciaArredondada(float x, float y, int n, float r)
    {
        float h = n / 2f;
        float qx = Mathf.Abs(x - h) - (h - r);
        float qy = Mathf.Abs(y - h) - (h - r);
        float fora = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
        return fora + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }
}
