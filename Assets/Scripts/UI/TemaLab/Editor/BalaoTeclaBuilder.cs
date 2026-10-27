using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Monta o balão de tecla (o mesmo do "agachar" da fase Animacao) como o
/// prefab Resources/BalaoTecla e coloca uma instância salva em cada cena com
/// interação, para ele aparecer no Editor. Na Animacao também liga as dicas
/// das alavancas e da lâmpada ao balão (DicaTecla na zona PeloObjeto).
///
/// Cada cena só é mexida se não tiver mudanças não salvas; antes de salvar,
/// uma cópia vai para SceneBackups/BalaoTecla_2026-10-08/.
/// Menu: Tools > Fases > Balão de tecla.
/// </summary>
public static class BalaoTeclaBuilder
{
    public const string CaminhoPrefab = "Assets/Resources/BalaoTecla.prefab";
    private const string PastaBackup = "SceneBackups/BalaoTecla_2026-10-08/Scenes/";

    private static readonly string[] Cenas = { "Animacao", "Interacao", "Interacao_Vida" };

    [MenuItem("Tools/Fases/Balão de tecla")]
    private static void Menu()
    {
        MontarPrefab();
        Debug.Log(ColocarEmTodas());
    }

    public static GameObject MontarPrefab()
    {
        var raizGO = new GameObject("BalaoTecla", typeof(RectTransform));
        try
        {
            var canvas = raizGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = raizGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // cabe inteira em qualquer proporção
            var raiz = (RectTransform)raizGO.transform;

            TemaLaboratorio tema = TemaLabUI.Tema;

            // O balão: ponto de apoio embaixo, no meio (a ponta encosta na cabeça do jogador).
            RectTransform balao = TemaLabUI.Novo(raiz, "Balao");
            balao.anchorMin = balao.anchorMax = new Vector2(0.5f, 0.5f);
            balao.pivot = new Vector2(0.5f, 0f);
            balao.sizeDelta = new Vector2(340f, 170f);
            var grupo = balao.gameObject.AddComponent<CanvasGroup>();
            grupo.alpha = 0f;
            grupo.blocksRaycasts = false;
            grupo.interactable = false;

            // Ponta (losango girado, metade escondida atrás do painel).
            Image ponta = TemaLabUI.Imagem(balao, "Ponta", tema != null ? tema.bordaArredondada : null, TemaLabUI.Painel);
            ponta.color = TemaLabUI.Painel;
            var pr = ponta.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(30f, 30f);
            pr.anchoredPosition = new Vector2(0f, 34f);
            pr.localRotation = Quaternion.Euler(0f, 0f, 45f);

            Image painel = TemaLabUI.Imagem(balao, "Painel", tema != null ? tema.painelLinha : null, TemaLabUI.Painel);
            var pp = painel.rectTransform;
            pp.anchorMin = new Vector2(0f, 0f);
            pp.anchorMax = new Vector2(1f, 1f);
            pp.offsetMin = new Vector2(0f, 30f);
            pp.offsetMax = Vector2.zero;

            // Tecla desenhada.
            RectTransform tecla = TemaLabUI.CaixaArredondada(painel.transform, "Tecla", TemaLabUI.Contorno, TemaLabUI.PainelElevado, 4f);
            TemaLabUI.Posicionar((RectTransform)tecla.parent, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(76f, 76f));
            TextMeshProUGUI letra = TemaLabUI.Texto(tecla, "Letra", "E", 38f, TemaLabUI.CorTexto, TemaLabUI.Fonte.MonoForte, TextAlignmentOptions.Center);
            letra.enableAutoSizing = true;
            letra.fontSizeMin = 16f;
            letra.fontSizeMax = 38f;
            letra.textWrappingMode = TextWrappingModes.NoWrap;
            TemaLabUI.Esticar(letra.rectTransform, 4f);

            TextMeshProUGUI acao = TemaLabUI.Texto(painel.transform, "Acao", "interagir", 36f, TemaLabUI.CorTexto, TemaLabUI.Fonte.CorpoForte);
            acao.textWrappingMode = TextWrappingModes.NoWrap;
            acao.enableAutoSizing = true;
            acao.fontSizeMin = 20f;
            acao.fontSizeMax = 36f;
            TemaLabUI.Posicionar(acao.rectTransform, new Vector2(0f, 0.5f), new Vector2(118f, 18f), new Vector2(200f, 46f));

            TextMeshProUGUI detalhe = TemaLabUI.Texto(painel.transform, "Detalhe", "aperte para usar", 22f, TemaLabUI.TextoSuave, TemaLabUI.Fonte.CorpoForte);
            detalhe.textWrappingMode = TextWrappingModes.NoWrap;
            detalhe.enableAutoSizing = true;
            detalhe.fontSizeMin = 14f;
            detalhe.fontSizeMax = 22f;
            TemaLabUI.Posicionar(detalhe.rectTransform, new Vector2(0f, 0.5f), new Vector2(118f, -24f), new Vector2(200f, 32f));

            // Sem alvo de clique: o balão nunca bloqueia o mouse.
            foreach (Graphic g in raizGO.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;

            var comp = raizGO.AddComponent<BalaoTecla>();
            var so = new SerializedObject(comp);
            so.FindProperty("balao").objectReferenceValue = balao;
            so.FindProperty("grupo").objectReferenceValue = grupo;
            so.FindProperty("letraTecla").objectReferenceValue = letra;
            so.FindProperty("acaoTxt").objectReferenceValue = acao;
            so.FindProperty("detalheTxt").objectReferenceValue = detalhe;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(CaminhoPrefab));
            return PrefabUtility.SaveAsPrefabAsset(raizGO, CaminhoPrefab);
        }
        finally
        {
            Object.DestroyImmediate(raizGO);
        }
    }

    public static string ColocarEmTodas()
    {
        var log = new StringBuilder();
        if (EditorApplication.isPlayingOrWillChangePlaymode) return "[BalaoTecla] Saia do Play Mode antes.";

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CaminhoPrefab);
        if (prefab == null) return "[BalaoTecla] Prefab não encontrado: " + CaminhoPrefab;

        foreach (string nome in Cenas)
        {
            string caminho = "Assets/Scenes/" + nome + ".unity";
            Scene cena = SceneManager.GetSceneByPath(caminho);
            bool abriAqui = false;
            if (cena.IsValid() && cena.isLoaded)
            {
                if (cena.isDirty)
                {
                    log.AppendLine($"{nome}: PULADA, a cena aberta tem mudanças não salvas.");
                    continue;
                }
            }
            else
            {
                cena = EditorSceneManager.OpenScene(caminho, OpenSceneMode.Additive);
                abriAqui = true;
            }

            Directory.CreateDirectory(PastaBackup);
            string backup = PastaBackup + nome + ".unity";
            if (!File.Exists(backup)) File.Copy(caminho, backup);

            // Uma instância do prefab na raiz da cena.
            GameObject inst = null;
            foreach (GameObject go in cena.GetRootGameObjects())
            {
                if (go.GetComponent<BalaoTecla>() != null)
                {
                    if (inst == null) inst = go;
                    else Object.DestroyImmediate(go);
                }
            }
            if (inst == null)
            {
                inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cena);
                inst.name = "BalaoTecla";
            }
            log.AppendLine($"{nome}: balão na cena.");

            if (nome == "Animacao") log.Append(LigarDicasAnimacao(cena));

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            if (abriAqui) EditorSceneManager.CloseScene(cena, true);
        }

        return log.ToString();
    }

    private static string LigarDicasAnimacao(Scene cena)
    {
        var log = new StringBuilder();
        foreach (GameObject raiz in cena.GetRootGameObjects())
        {
            // Alavancas: o balão aparece com o personagem perto (a alavanca decide).
            foreach (AlavancaFase alavanca in raiz.GetComponentsInChildren<AlavancaFase>(true))
            {
                var so = new SerializedObject(alavanca);
                var dica = so.FindProperty("dica").objectReferenceValue as GameObject;
                DicaTecla d = dica != null ? dica.GetComponent<DicaTecla>() : null;
                if (d == null) continue;
                DefinirDica(d, "Interact", null, null, null);
                log.AppendLine($"  {alavanca.name}: dica no balão.");
            }

            // Lâmpada: o texto "clique na lâmpada" vira o balão da tecla de interagir.
            foreach (LampadaSensor lampada in raiz.GetComponentsInChildren<LampadaSensor>(true))
            {
                var so = new SerializedObject(lampada);
                var texto = so.FindProperty("dicaClique").objectReferenceValue as TMP_Text;
                if (texto != null)
                {
                    DicaTecla d = texto.GetComponent<DicaTecla>();
                    if (d == null) d = Undo.AddComponent<DicaTecla>(texto.gameObject);
                    DefinirDica(d, "Interact", "Aperte {0} ou clique na lâmpada", "abrir Animator", "ou clique na lâmpada");
                    log.AppendLine($"  {lampada.name}: dica no balão.");
                }

                var falas = so.FindProperty("falasEscuro");
                for (int i = 0; i < falas.arraySize; i++)
                {
                    var f = falas.GetArrayElementAtIndex(i);
                    if (f.stringValue.Contains("Clique na lâmpada pra abrir"))
                    {
                        f.stringValue = f.stringValue.Replace("Clique na lâmpada pra abrir",
                            "Clique na lâmpada ou use a tecla de interagir pra abrir");
                    }
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        return log.ToString();
    }

    private static void DefinirDica(DicaTecla d, string acao, string formato, string rotulo, string detalhe)
    {
        var so = new SerializedObject(d);
        so.FindProperty("acao").stringValue = acao;
        if (formato != null) so.FindProperty("formato").stringValue = formato;
        so.FindProperty("usarBalao").boolValue = true;
        if (rotulo != null) so.FindProperty("rotulo").stringValue = rotulo;
        if (detalhe != null) so.FindProperty("detalhe").stringValue = detalhe;
        so.FindProperty("zona").enumValueIndex = (int)DicaTecla.Zona.PeloObjeto;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
