using UnityEditor;
using UnityEngine;

/// <summary>
/// Aplica as configurações de import recomendadas pro áudio do jogo:
/// - Músicas (Resources/Audio/Musicas e a pasta antiga Music): Streaming +
///   Vorbis — loop longo, carregado aos poucos.
/// - Efeitos (Resources/Audio/Efeitos): Force To Mono + Decompress On Load —
///   curtos e tocados na hora, sem atraso.
/// - Cliques da máquina de escrever (Resources/Audio/SFX): decodificados/PCM
///   e pré-carregados (sem Preload, a primeira letra de cada diálogo sai atrasada).
///
/// Arquivos novos colocados nessas pastas já entram configurados sozinhos
/// (ver AudioImportPostprocessor abaixo). Pra reaplicar em tudo:
/// Tools > Áudio > Aplicar Configurações de Import Recomendadas (idempotente).
/// </summary>
public static class AudioImportSettings
{
    public const string MusicFolder = "Assets/Resources/Audio/Musicas";
    public const string EffectsFolder = "Assets/Resources/Audio/Efeitos";
    private const string LegacyMusicFolder = "Assets/Resources/Audio/Music";
    private const string SfxFolder = "Assets/Resources/Audio/SFX";

    [MenuItem("Tools/Áudio/Aplicar Configurações de Import Recomendadas")]
    public static void ApplyRecommendedSettings()
    {
        int musicCount = ApplyToFolder(MusicFolder, ConfigureMusicImporter)
            + ApplyToFolder(LegacyMusicFolder, ConfigureMusicImporter);
        int effectsCount = ApplyToFolder(EffectsFolder, ConfigureEffectImporter);
        int sfxCount = ApplyToFolder(SfxFolder, ConfigureSfxImporter);

        Debug.Log($"[AudioImportSettings] Configurado: {musicCount} música(s), {effectsCount} efeito(s), {sfxCount} clique(s) de diálogo.");
    }

    /// <summary>Configura pelo caminho do arquivo. Retorna false se ele não está em nenhuma pasta de áudio conhecida.</summary>
    public static bool ConfigureByPath(AudioImporter importer)
    {
        string path = importer.assetPath;
        if (path.StartsWith(MusicFolder + "/") || path.StartsWith(LegacyMusicFolder + "/"))
        {
            ConfigureMusicImporter(importer);
            return true;
        }
        if (path.StartsWith(EffectsFolder + "/"))
        {
            ConfigureEffectImporter(importer);
            return true;
        }
        if (path.StartsWith(SfxFolder + "/"))
        {
            ConfigureSfxImporter(importer);
            return true;
        }
        return false;
    }

    private static int ApplyToFolder(string folder, System.Action<AudioImporter> configure)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            return 0;
        }

        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                continue;
            }

            configure(importer);
            importer.SaveAndReimport();
        }

        return guids.Length;
    }

    private static void ConfigureMusicImporter(AudioImporter importer)
    {
        importer.forceToMono = false;

        // preloadAudioData mora dentro de SampleSettings (por plataforma) agora
        // — a propriedade direta em AudioImporter foi descontinuada.
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.7f;
        settings.preloadAudioData = false;
        importer.defaultSampleSettings = settings;
    }

    private static void ConfigureEffectImporter(AudioImporter importer)
    {
        importer.forceToMono = true;

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.7f;
        settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings;
    }

    private static void ConfigureSfxImporter(AudioImporter importer)
    {
        importer.forceToMono = true;

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings;
    }
}

/// <summary>Configura sozinho qualquer áudio novo que entrar nas pastas de áudio do jogo.</summary>
public class AudioImportPostprocessor : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        // Só na primeira importação: reimports depois disso respeitam ajustes feitos à mão no Inspector.
        if (!assetImporter.importSettingsMissing)
        {
            return;
        }

        AudioImportSettings.ConfigureByPath((AudioImporter)assetImporter);
    }
}
