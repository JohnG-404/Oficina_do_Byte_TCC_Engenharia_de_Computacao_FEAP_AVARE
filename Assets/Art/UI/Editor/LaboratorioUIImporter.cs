using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Importa os sprites do tema Laboratório (Assets/Art/UI) como Sprite (2D and UI),
// com Mesh Full Rect e as bordas de 9-slice (L, B, R, T) de cada peça.
public class LaboratorioUIImporter : AssetPostprocessor
{
    const string Pasta = "Assets/Art/UI/";

    static readonly Dictionary<string, Vector4> Bordas = new Dictionary<string, Vector4>
    {
        { "btn_primario_normal", new Vector4(36, 36, 36, 36) },
        { "btn_primario_hover", new Vector4(36, 36, 36, 36) },
        { "btn_primario_pressed", new Vector4(36, 36, 36, 36) },
        { "btn_primario_focus", new Vector4(36, 36, 36, 36) },
        { "btn_primario_disabled", new Vector4(36, 36, 36, 36) },
        { "btn_secundario_normal", new Vector4(36, 36, 36, 36) },
        { "btn_secundario_hover", new Vector4(36, 36, 36, 36) },
        { "btn_secundario_pressed", new Vector4(36, 36, 36, 36) },
        { "btn_secundario_focus", new Vector4(36, 36, 36, 36) },
        { "btn_secundario_disabled", new Vector4(36, 36, 36, 36) },
        { "btn_perigo_normal", new Vector4(36, 36, 36, 36) },
        { "btn_perigo_hover", new Vector4(36, 36, 36, 36) },
        { "btn_perigo_pressed", new Vector4(36, 36, 36, 36) },
        { "btn_perigo_focus", new Vector4(36, 36, 36, 36) },
        { "btn_perigo_disabled", new Vector4(36, 36, 36, 36) },
        { "painel_janela", new Vector4(40, 40, 40, 40) },
        { "painel_titulo", new Vector4(30, 30, 30, 30) },
        { "painel_popup", new Vector4(40, 40, 40, 40) },
        { "painel_linha", new Vector4(16, 16, 16, 16) },
        { "painel_linha_foco", new Vector4(16, 16, 16, 16) },
        { "aba_ativa", new Vector4(28, 0, 28, 28) },
        { "aba_inativa", new Vector4(28, 0, 28, 28) },
        { "aba_foco", new Vector4(28, 0, 28, 28) },
        { "slider_trilho", new Vector4(12, 12, 12, 12) },
        { "slider_preenchimento", new Vector4(12, 12, 12, 12) },
        { "seletor_caixa", new Vector4(24, 24, 24, 24) },
        { "dropdown_caixa", new Vector4(24, 24, 52, 24) },
        { "painel_codigo", new Vector4(66, 24, 24, 64) },
        { "moldura_logo", new Vector4(48, 40, 48, 40) },
    };

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Pasta)) return;
        var imp = (TextureImporter)assetImporter;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.filterMode = FilterMode.Bilinear;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.spritePixelsPerUnit = 100;

        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(settings);

        string nome = Path.GetFileNameWithoutExtension(assetPath);
        if (Bordas.TryGetValue(nome, out var b)) imp.spriteBorder = b;
    }

    [MenuItem("Tools/UI Laboratório/Reimportar sprites")]
    static void Reimportar() => AssetDatabase.ImportAsset("Assets/Art/UI", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
}
