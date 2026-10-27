using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Os pontos de patrulha A e B da IA_Aplicacao viram uma placa num poste, com a
/// borda amarela da IA, a letra grande e "patrulha" embaixo.
///
/// Tools > IA > Placas dos pontos de patrulha
///   Gera Assets/Art/IA/placa_patrulha.png (se ainda não existir), troca o
///   filho "Letra" de PontoA/PontoB pelo "Marcador" e salva a IA_Aplicacao.
///   Pode rodar de novo: refaz o marcador sem duplicar.
/// </summary>
public static class IAMarcadorPatrulhaBuilder
{
    private const string ScenePath = "Assets/Scenes/IA_Aplicacao.unity";
    private const string SpritePath = "Assets/Art/IA/placa_patrulha.png";

    // Textura 160 x 260 px a 160 px por unidade: placa de 1 x 0,75 em cima
    // de um poste que desce até o chão (1,625 de altura no total).
    private const int Largura = 160, Altura = 260, AlturaPlaca = 120, Raio = 20, Borda = 6, LarguraPoste = 12;
    private const float PixelsPorUnidade = 160f;

    private static readonly Color Amarelo = new Color32(0xF5, 0xC5, 0x42, 0xFF);
    private static readonly Color Fundo = new Color32(0x1B, 0x22, 0x32, 0xF2);
    private static readonly Color Poste = new Color32(0x3A, 0x41, 0x52, 0xFF);
    private static readonly Color TextoSuave = new Color32(0xC9, 0xD1, 0xE0, 0xFF);

    [MenuItem("Tools/IA/Placas dos pontos de patrulha")]
    public static void AplicarMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[IAMarcadorPatrulhaBuilder] Saia do Play antes.");
            return;
        }

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool abriu = false;
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            abriu = true;
        }

        Sprite placa = GarantirSprite();
        // Junta os pontos antes: Criar apaga a letra antiga, que também está na lista dos filhos.
        var pontos = new System.Collections.Generic.List<Transform>();
        foreach (GameObject raiz in scene.GetRootGameObjects())
        {
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "PontoA" || t.name == "PontoB") pontos.Add(t);
            }
        }
        foreach (Transform ponto in pontos)
        {
            Criar(ponto, ponto.name.Substring(5), placa);
        }
        int feitos = pontos.Count;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (abriu) EditorSceneManager.CloseScene(scene, true);
        Debug.Log($"[IAMarcadorPatrulhaBuilder] {feitos} placas de patrulha na IA_Aplicacao.");
    }

    /// <summary>Troca a letra solta do ponto pela placa no poste, com o pé no chão (y = 0).</summary>
    public static void Criar(Transform ponto, string letra, Sprite placa)
    {
        foreach (string velho in new[] { "Letra", "Marcador" })
        {
            Transform antigo = ponto.Find(velho);
            if (antigo != null) Undo.DestroyObjectImmediate(antigo.gameObject);
        }

        var marcador = new GameObject("Marcador");
        Undo.RegisterCreatedObjectUndo(marcador, "Placa de patrulha");
        marcador.transform.SetParent(ponto, false);
        marcador.transform.position = new Vector3(ponto.position.x, 0f, 0f);

        var sr = marcador.AddComponent<SpriteRenderer>();
        sr.sprite = placa;
        sr.sortingOrder = 1; // atrás do guarda e do jogador

        float centroPlaca = (Altura - AlturaPlaca * 0.5f) / PixelsPorUnidade;
        Texto(marcador.transform, "Letra", letra, new Vector3(0f, centroPlaca + 0.08f, 0f), 4.2f, Amarelo, FontStyles.Bold);
        Texto(marcador.transform, "Rotulo", "patrulha", new Vector3(0f, centroPlaca - 0.22f, 0f), 1.5f, TextoSuave, FontStyles.Normal);
    }

    private static void Texto(Transform pai, string nome, string texto, Vector3 local, float fonte, Color cor, FontStyles estilo)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        go.transform.localPosition = local;
        TextMeshPro t = go.AddComponent<TextMeshPro>();
        t.text = texto;
        t.fontSize = fonte;
        t.fontStyle = estilo;
        t.color = cor;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.rectTransform.sizeDelta = new Vector2(1f, 0.5f);
        t.sortingOrder = 2;
    }

    public static Sprite GarantirSprite()
    {
        if (!File.Exists(SpritePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SpritePath));
            var tex = new Texture2D(Largura, Altura, TextureFormat.RGBA32, false);
            int yPlaca = Altura - AlturaPlaca;
            for (int y = 0; y < Altura; y++)
            {
                for (int x = 0; x < Largura; x++)
                {
                    Color c = Color.clear;
                    if (y < yPlaca)
                    {
                        if (Mathf.Abs(x + 0.5f - Largura * 0.5f) <= LarguraPoste * 0.5f) c = Poste;
                    }
                    else
                    {
                        // Distância até a borda do retângulo arredondado da placa.
                        float px = x + 0.5f, py = y + 0.5f - yPlaca;
                        float cx = Mathf.Clamp(px, Raio, Largura - Raio);
                        float cy = Mathf.Clamp(py, Raio, AlturaPlaca - Raio);
                        float d = Raio - Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                        if (d > 0f)
                        {
                            c = d <= Borda ? Amarelo : Fundo;
                            c.a *= Mathf.Clamp01(d);
                        }
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SpritePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPorUnidade;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }
}
