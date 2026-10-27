using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 22/10: nas prévias "Objetivo" da Física e da IA o chão começava colado
/// na borda esquerda da janela e parava antes da direita (blocos de 0.4
/// a partir de xMin + 0.2 até passar de xMax). Recoloca os blocos "Chao"
/// com a mesma margem de 0.2 dos dois lados (a das barras de progresso),
/// ajustando a largura do bloco para fechar a conta. Só mexe no chão; a
/// altura, o sprite e a máscara de cada bloco ficam como estão. Os
/// builders (FisicaCodigoBuilder e IAPuzzleLayoutBuilder) já montam assim.
/// Backup em SceneBackups/chao_previa_2026-10-22/.
/// </summary>
public static class PreviasChaoBuilder
{
    private const float Margem = 0.2f;
    private const float BlocoBase = 0.4f;
    private const string Backup = "SceneBackups/chao_previa_2026-10-22";
    private static readonly string[] Cenas = { "Assets/Scenes/Fisica.unity", "Assets/Scenes/IA.unity" };

    [MenuItem("Tools/Prévias/Centralizar chão das prévias")]
    public static void Aplicar() => Rodar(true);

    /// <summary>Ajusta o chão nas cenas abertas (ou abre em Additive). Com <paramref name="salvar"/> salva e fecha as que abriu.</summary>
    public static void Rodar(bool salvar)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[PreviasChao] Saia do Play Mode antes.");
            return;
        }

        Directory.CreateDirectory(Backup);
        foreach (string caminho in Cenas)
        {
            Scene cena = SceneManager.GetSceneByPath(caminho);
            bool abriu = false;
            if (!cena.IsValid() || !cena.isLoaded)
            {
                cena = EditorSceneManager.OpenScene(caminho, OpenSceneMode.Additive);
                abriu = true;
            }

            string destino = Path.Combine(Backup, Path.GetFileName(caminho));
            if (!File.Exists(destino)) File.Copy(caminho, destino);

            int n = 0;
            foreach (GameObject raiz in cena.GetRootGameObjects())
                foreach (Transform painel in raiz.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Painel_Previa").ToList())
                    if (Centralizar(painel)) n++;

            Debug.Log("[PreviasChao] " + cena.name + ": chão centralizado em " + n + " prévia(s).");
            if (salvar && n > 0) EditorSceneManager.SaveScene(cena);
            if (salvar && abriu) EditorSceneManager.CloseScene(cena, true);
        }
    }

    private static bool Centralizar(Transform painel)
    {
        Transform janelaT = painel.Find("janela_previa");
        Transform cont = painel.Find("Preview");
        if (janelaT == null || cont == null) return false;
        var janela = janelaT.GetComponent<SpriteRenderer>();
        var blocos = cont.Cast<Transform>().Where(t => t.name == "Chao").Select(t => t.GetComponent<SpriteRenderer>()).Where(s => s != null && s.sprite != null).ToList();
        if (janela == null || janela.sprite == null || blocos.Count == 0) return false;

        // Bordas da janela em mundo (renderers inativos não têm bounds).
        float xMin = Esquerda(janela), xMax = Direita(janela);
        float ini = xMin + Margem, larg = (xMax - xMin) - 2f * Margem;
        int n = Mathf.Max(1, Mathf.RoundToInt(larg / BlocoBase));
        float bloco = larg / n;

        // Sobra ou falta de bloco: remove os últimos ou duplica o primeiro.
        blocos = blocos.OrderBy(b => b.transform.position.x).ToList();
        while (blocos.Count > n)
        {
            Undo.DestroyObjectImmediate(blocos[blocos.Count - 1].gameObject);
            blocos.RemoveAt(blocos.Count - 1);
        }
        while (blocos.Count < n)
        {
            var copia = Object.Instantiate(blocos[0].gameObject, cont);
            copia.name = "Chao";
            Undo.RegisterCreatedObjectUndo(copia, "Chão da prévia");
            blocos.Add(copia.GetComponent<SpriteRenderer>());
        }

        for (int i = 0; i < n; i++)
        {
            Transform t = blocos[i].transform;
            Undo.RecordObject(t, "Chão da prévia");
            float nativo = blocos[i].sprite.bounds.size.x;
            float paiEsc = t.parent.lossyScale.x;
            t.localScale = new Vector3(bloco / nativo / (Mathf.Abs(paiEsc) < 0.0001f ? 1f : paiEsc), t.localScale.y, t.localScale.z);
            t.position = new Vector3(ini + bloco * (i + 0.5f), t.position.y, t.position.z);
            EditorUtility.SetDirty(t);
        }
        return true;
    }

    private static float Esquerda(SpriteRenderer s) => s.transform.position.x + s.sprite.bounds.min.x * s.transform.lossyScale.x;
    private static float Direita(SpriteRenderer s) => s.transform.position.x + s.sprite.bounds.max.x * s.transform.lossyScale.x;
}
