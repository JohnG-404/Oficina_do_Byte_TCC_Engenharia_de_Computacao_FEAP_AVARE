// Configura automaticamente os sprites do EditorKit ao importar no Unity.
// - Todo PNG dentro de uma pasta chamada "EditorKit" vira Sprite (2D e UI).
// - Se o nome estiver em sprites_config.json, aplica as bordas de 9-slice.
// Coloque a pasta EditorKit inteira em Assets/ (este script precisa ficar numa pasta "Editor").
using System.IO;
using UnityEditor;
using UnityEngine;

public class EditorKitSpriteImporter : AssetPostprocessor
{
    [System.Serializable] class Entry { public string name; public int l, b, r, t; }
    [System.Serializable] class Config { public Entry[] sprites; }

    void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        int idx = path.IndexOf("/EditorKit/");
        if (idx < 0) return;

        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear;
        ti.textureCompression = TextureImporterCompression.Uncompressed;

        string cfgPath = path.Substring(0, idx) + "/EditorKit/sprites_config.json";
        if (!File.Exists(cfgPath)) return;
        var cfg = JsonUtility.FromJson<Config>(File.ReadAllText(cfgPath));
        string spriteName = Path.GetFileNameWithoutExtension(path);

        foreach (var e in cfg.sprites)
        {
            if (e.name != spriteName) continue;
            // Vector4: x = esquerda, y = baixo, z = direita, w = topo
            ti.spriteBorder = new Vector4(e.l, e.b, e.r, e.t);
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect; // necessário para Image Type = Sliced/Tiled
            ti.SetTextureSettings(s);
            break;
        }
    }
}
