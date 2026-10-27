using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Gera o executável do jogo para Windows (64 bits) com as cenas marcadas
/// em Build Profiles. Sai em testTCC/Builds/&lt;nome do jogo&gt;/&lt;nome do jogo&gt;.exe,
/// com o nome, a empresa e o ícone das Player Settings.
///
/// Uso: Tools > Build > Gerar executável (Windows).
/// Rodar de novo substitui o build anterior na mesma pasta.
/// </summary>
public static class GerarExecutavel
{
    [MenuItem("Tools/Build/Gerar executável (Windows)")]
    public static void Gerar()
    {
        string nome = NomeDeArquivo(PlayerSettings.productName);
        string raiz = Directory.GetParent(Application.dataPath).FullName;
        string pasta = Path.Combine(raiz, "Builds", nome);
        string exe = Path.Combine(pasta, nome + ".exe");

        string[] cenas = EditorBuildSettings.scenes.Where(c => c.enabled).Select(c => c.path).ToArray();
        if (cenas.Length == 0)
        {
            EditorUtility.DisplayDialog("Gerar executável", "Nenhuma cena marcada em Build Profiles.", "OK");
            return;
        }

        var opcoes = new BuildPlayerOptions
        {
            scenes = cenas,
            locationPathName = exe,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
        };

        BuildReport relatorio = BuildPipeline.BuildPlayer(opcoes);
        BuildSummary resumo = relatorio.summary;

        if (resumo.result == BuildResult.Succeeded)
        {
            float mb = resumo.totalSize / (1024f * 1024f);
            Debug.Log($"[GerarExecutavel] Build pronto: {exe} ({mb:0.0} MB, {cenas.Length} cenas, {resumo.totalTime.TotalSeconds:0}s)");
            EditorUtility.RevealInFinder(exe);
        }
        else
        {
            Debug.LogError($"[GerarExecutavel] Build terminou como {resumo.result} com {resumo.totalErrors} erro(s). Veja o Console.");
        }
    }

    // O Windows não aceita alguns caracteres nem nome terminado em ponto ou espaço
    // (ex.: "Compilando..." vira "Compilando").
    private static string NomeDeArquivo(string nome)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            nome = nome.Replace(c.ToString(), "");
        nome = nome.TrimEnd('.', ' ');
        return string.IsNullOrEmpty(nome) ? "Jogo" : nome;
    }
}
