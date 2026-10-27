using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Exporta ícones internos do Editor da Unity (Rigidbody, Collider2D, etc.)
/// como PNGs dentro do projeto, já importados como Sprite — prontos pra usar
/// em UI de jogo (ex: ícones do inventário na cena Aplicacao).
///
/// Uso: Tools > Ícones do Editor > Exportar Ícones (Rigidbody/Collider).
/// Idempotente: rodar de novo só re-exporta os mesmos arquivos.
/// </summary>
public static class EditorIconExporter
{
    private const string OutputFolder = "Assets/Images/EditorIcons";

    // Nomes internos dos ícones do Editor da Unity. Pra descobrir outros,
    // selecione qualquer componente na Hierarchy e olhe o ícone ao lado do
    // nome dele no Inspector — ou use a janela "Ícones do Editor > Listar
    // Todos" abaixo pra procurar por um pedaço do nome.
    private static readonly string[] IconNames =
    {
        "Rigidbody Icon",
        "Rigidbody2D Icon",
        "BoxCollider Icon",
        "BoxCollider2D Icon",
        "CircleCollider2D Icon",
        "CapsuleCollider2D Icon",
        "PolygonCollider2D Icon",
        "EdgeCollider2D Icon",
    };

    [MenuItem("Tools/Ícones do Editor/Exportar Ícones (Rigidbody-Collider)")]
    public static void ExportKnownIcons()
    {
        Directory.CreateDirectory(OutputFolder);

        int exported = 0;
        foreach (string iconName in IconNames)
        {
            if (ExportIcon(iconName))
            {
                exported++;
            }
        }

        AssetDatabase.Refresh();
        ConfigureAsSprites();

        Debug.Log($"[EditorIconExporter] {exported}/{IconNames.Length} ícones exportados em {OutputFolder}.");
    }

    private static bool ExportIcon(string iconName)
    {
        GUIContent content = EditorGUIUtility.IconContent(iconName);
        Texture2D source = content != null ? content.image as Texture2D : null;
        if (source == null)
        {
            Debug.LogWarning($"[EditorIconExporter] Ícone não encontrado nesta versão do Unity: \"{iconName}\".");
            return false;
        }

        Texture2D readable = MakeReadable(source);
        byte[] png = readable.EncodeToPNG();
        Object.DestroyImmediate(readable);

        string fileName = iconName.Replace(" ", "_") + ".png";
        File.WriteAllBytes(Path.Combine(OutputFolder, fileName), png);
        return true;
    }

    // Texturas de ícone do Editor normalmente não têm "Read/Write" habilitado,
    // então EncodeToPNG() direto falha. Contorna desenhando a textura numa
    // RenderTexture temporária e lendo os pixels de volta dali.
    private static Texture2D MakeReadable(Texture2D source)
    {
        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;

        Graphics.Blit(source, rt);
        RenderTexture.active = rt;

        var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        readable.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        return readable;
    }

    // Garante que os PNGs exportados já entrem configurados como Sprite (2D e UI),
    // em vez de "Default", pra dar pra arrastar direto num Image sem passo manual.
    private static void ConfigureAsSprites()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { OutputFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType == TextureImporterType.Sprite)
            {
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }
}
