using System.IO;
using UnityEngine;

/// <summary>
/// Pasta onde o jogo guarda jogadores, progresso e registros CSV.
///
/// Na build é o Application.persistentDataPath de sempre
/// (AppData\LocalLow\FEAP Avare\Oficina do Byte), a pasta que o professor
/// abre pra pegar os CSVs. No Editor é a pasta vizinha
/// "Oficina do Byte (Editor)", pra os testes do Play não se misturarem com os
/// jogadores, o progresso e os registros dos alunos quando a build roda no
/// mesmo PC.
///
/// O Editor começa com a pasta vazia (sem jogadores e com outro código de PC
/// nos anônimos). Nada da pasta da build é apagado, movido ou copiado.
/// </summary>
public static class PastaDados
{
#if UNITY_EDITOR
    private static string raiz;

    public static string Raiz
    {
        get
        {
            if (raiz == null)
            {
                string pastaBuild = Application.persistentDataPath;
                raiz = Path.Combine(Path.GetDirectoryName(pastaBuild), Path.GetFileName(pastaBuild) + " (Editor)");
            }
            return raiz;
        }
    }
#else
    public static string Raiz => Application.persistentDataPath;
#endif
}
