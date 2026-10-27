using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Opções de qualidade de vida nas Configurações (Resources/GameUICanvas):
///  - Gráficos: Resolução (lista do monitor), Modo de tela, Limite de quadros,
///    Manter proporção 16:9;
///  - Áudio: Silenciar quando a janela perde o foco;
///  - aba nova "Jogo": Velocidade do texto, Dicas de teclas, Mostrar FPS, Prender o mouse;
///  - contador de FPS no canto do GameUICanvas.
/// E um ajuste de resolução: todos os Canvas Scalers das cenas e prefabs em
/// modo Expand (a área 1920x1080 sempre cabe inteira na tela).
///
/// Pode rodar de novo sem duplicar nada. Backups: SceneBackups/resolucoes_config_2026-10-13/.
/// </summary>
public static partial class LaboratorioUIBuilder
{
    [MenuItem("Tools/UI Laboratório/5. Opções de qualidade de vida (Configurações)")]
    public static void AplicarQualidadeVida()
    {
        Preparar();
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabGameUI);
        try
        {
            Escalar(root.GetComponent<CanvasScaler>());
            var painel = (RectTransform)root.transform.Find("SettingsPanel");
            MontarOpcoesQualidadeVida(painel);

            Transform area = painel.Find("ContentArea"), abas = painel.Find("TabBar");
            Foco(painel.gameObject,
                area.Find("AudioContent/VolumeSlider").GetComponent<Selectable>(),
                area.Find("GraphicsContent/LinhaResolucao").GetComponent<Selectable>(),
                abas.Find("ControlsTabButton").GetComponent<Selectable>(),
                abas.Find("AudioTabButton").GetComponent<Selectable>());

            MontarContadorFPS(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabGameUI);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log("[LaboratorioUI] Opções de qualidade de vida aplicadas no GameUICanvas.");
    }

    [MenuItem("Tools/Resolução/Canvas Scalers em modo Expand (cenas e prefabs)")]
    public static void AjustarCanvasScalers()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Resolução] Saia do Play Mode antes.");
            return;
        }

        var log = new List<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string caminho = AssetDatabase.GUIDToAssetPath(guid);
            Scene cena = SceneManager.GetSceneByPath(caminho);
            bool jaAberta = cena.IsValid() && cena.isLoaded;

            if (jaAberta && cena.isDirty)
            {
                log.Add($"PULADA (aberta com mudanças não salvas): {caminho}");
                continue;
            }

            if (!jaAberta) cena = EditorSceneManager.OpenScene(caminho, OpenSceneMode.Additive);

            int n = 0;
            foreach (GameObject raiz in cena.GetRootGameObjects())
            {
                foreach (CanvasScaler s in raiz.GetComponentsInChildren<CanvasScaler>(true))
                {
                    if (AjustarScaler(s)) n++;
                }
            }

            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(cena);
                EditorSceneManager.SaveScene(cena);
            }
            log.Add($"{caminho}: {n} Canvas Scaler(s) ajustado(s)");

            if (!jaAberta) EditorSceneManager.CloseScene(cena, true);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources", "Assets/Prefabs" }))
        {
            string caminho = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
            if (asset == null || asset.GetComponentInChildren<CanvasScaler>(true) == null) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(caminho);
            try
            {
                int n = 0;
                foreach (CanvasScaler s in root.GetComponentsInChildren<CanvasScaler>(true))
                {
                    if (AjustarScaler(s)) n++;
                }
                if (n > 0) PrefabUtility.SaveAsPrefabAsset(root, caminho);
                log.Add($"{caminho}: {n} Canvas Scaler(s) ajustado(s)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        Debug.Log("[Resolução] Canvas Scalers:\n" + string.Join("\n", log));
    }

    static bool AjustarScaler(CanvasScaler s)
    {
        if (s.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return false;
        if (s.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand) return false;
        s.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        EditorUtility.SetDirty(s);
        return true;
    }

    // ------------------------------------------------------------------

    static void MontarOpcoesQualidadeVida(RectTransform painel)
    {
        var abas = (RectTransform)painel.Find("TabBar");
        var area = (RectTransform)painel.Find("ContentArea");
        Selectable voltar = painel.Find("VoltarButton").GetComponent<Selectable>();

        // ---------------- Gráficos ----------------
        var graf = (RectTransform)area.Find("GraphicsContent");
        var layout = graf.GetComponent<VerticalLayoutGroup>();
        if (layout != null) layout.enabled = false;

        foreach (string velho in new[] { "Resolucao720Button", "Resolucao1080Button", "FullscreenButton", "LinhaResolucao" })
        {
            Transform t = graf.Find(velho);
            if (t != null && t.GetComponent<OpcaoConfigUI>() == null) Object.DestroyImmediate(t.gameObject);
        }
        var visualAntigo = graf.GetComponent<GraficosVisualUI>();
        if (visualAntigo != null) Object.DestroyImmediate(visualAntigo);

        Selectable abaGraf = abas.Find("GraphicsTabButton").GetComponent<Selectable>();
        Coluna(abaGraf, voltar,
            LinhaSeletor(graf, "LinhaResolucao", "Resolução", 0, OpcaoConfigUI.Opcao.Resolucao, "1920 × 1080"),
            LinhaSeletor(graf, "LinhaModoTela", "Modo de tela", 112, OpcaoConfigUI.Opcao.ModoTela, "Tela cheia"),
            LinhaSeletor(graf, "LinhaTaxaQuadros", "Limite de quadros", 224, OpcaoConfigUI.Opcao.TaxaQuadros, "VSync"),
            LinhaInterruptor(graf, "LinhaProporcao", "Manter proporção 16:9 (faixas pretas nas sobras)", 336, OpcaoConfigUI.Opcao.ManterProporcao, true));

        // ---------------- Áudio: linha extra ----------------
        var audio = (RectTransform)area.Find("AudioContent");
        Button silenciar = LinhaInterruptor(audio, "LinhaSilenciarSemFoco", "Silenciar quando a janela perde o foco", 336, OpcaoConfigUI.Opcao.SilenciarSemFoco, false);
        Transform efeitosT = audio.Find("LinhaEfeitos/EfeitosSlider");
        if (efeitosT != null)
        {
            Selectable efeitos = efeitosT.GetComponent<Selectable>();
            var ne = efeitos.navigation;
            ne.mode = Navigation.Mode.Explicit;
            ne.selectOnDown = silenciar;
            efeitos.navigation = ne;
            silenciar.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = efeitos, selectOnDown = voltar };
        }

        // ---------------- Aba Jogo ----------------
        Transform abaJogoT = abas.Find("GameTabButton");
        if (abaJogoT == null)
        {
            Transform ctrl = abas.Find("ControlsTabButton");
            abaJogoT = Object.Instantiate(ctrl.gameObject, abas).transform;
            abaJogoT.name = "GameTabButton";
        }
        abaJogoT.SetSiblingIndex(3);
        Button abaJogo = Aba(abas, "GameTabButton", "Jogo", "icone_acessibilidade");

        RectTransform jogo = Filho(area, "GameContent");
        Esticar(jogo);
        Button[] linhasJogo =
        {
            LinhaSeletor(jogo, "LinhaVelocidadeTexto", "Velocidade do texto dos diálogos", 0, OpcaoConfigUI.Opcao.VelocidadeTexto, "Normal"),
            LinhaInterruptor(jogo, "LinhaDicasTecla", "Dicas de teclas sobre o personagem", 112, OpcaoConfigUI.Opcao.DicasTecla, true),
            LinhaInterruptor(jogo, "LinhaMostrarFPS", "Mostrar FPS no canto da tela", 224, OpcaoConfigUI.Opcao.MostrarFPS, false),
            LinhaInterruptor(jogo, "LinhaPrenderMouse", "Prender o mouse dentro da janela do jogo", 336, OpcaoConfigUI.Opcao.PrenderMouse, false),
        };
        Coluna(abaJogo, voltar, linhasJogo);
        jogo.gameObject.SetActive(false);

        // Abas: ←→ entre elas, ↓ desce pro conteúdo da aba.
        Selectable abaCtrl = abas.Find("ControlsTabButton").GetComponent<Selectable>();
        var nc = abaCtrl.navigation;
        if (nc.mode == Navigation.Mode.Explicit)
        {
            nc.selectOnRight = abaJogo;
            abaCtrl.navigation = nc;
            abaJogo.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = abaCtrl, selectOnDown = linhasJogo[0] };
        }
        var ng = abaGraf.navigation;
        if (ng.mode == Navigation.Mode.Explicit)
        {
            ng.selectOnDown = graf.Find("LinhaResolucao").GetComponent<Selectable>();
            abaGraf.navigation = ng;
        }
    }

    static void Coluna(Selectable aba, Selectable voltar, params Button[] linhas)
    {
        Encadear(linhas);
        var n0 = linhas[0].navigation;
        n0.selectOnUp = aba;
        linhas[0].navigation = n0;
        var nf = linhas[linhas.Length - 1].navigation;
        nf.selectOnDown = voltar;
        linhas[linhas.Length - 1].navigation = nf;
    }

    static Button LinhaBase(RectTransform pai, string nome, float y, OpcaoConfigUI.Opcao opcao, out SerializedObject so)
    {
        RectTransform linha = Filho(pai, nome);
        Pos(linha, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(0, 96));
        Image img = Img(linha.gameObject, S("painel_linha"), Color.white, true);

        Button b = Garantir<Button>(linha.gameObject);
        b.targetGraphic = img;
        b.transition = Selectable.Transition.SpriteSwap;
        b.spriteState = new SpriteState { highlightedSprite = S("painel_linha_foco"), pressedSprite = S("painel_linha_foco"), selectedSprite = S("painel_linha_foco") };

        var op = Garantir<OpcaoConfigUI>(linha.gameObject);
        so = new SerializedObject(op);
        so.FindProperty("opcao").enumValueIndex = (int)opcao;
        return b;
    }

    static Button LinhaSeletor(RectTransform pai, string nome, string rotulo, float y, OpcaoConfigUI.Opcao opcao, string exemplo)
    {
        Button b = LinhaBase(pai, nome, y, opcao, out SerializedObject so);
        Transform linha = b.transform;

        var rot = Txt(linha, "Rotulo", rotulo, fTexto, 30, Texto, TextAlignmentOptions.MidlineLeft);
        rot.textWrappingMode = TextWrappingModes.NoWrap;
        var rr = (RectTransform)rot.transform;
        rr.anchorMin = new Vector2(0, 0);
        rr.anchorMax = new Vector2(1, 1);
        rr.pivot = new Vector2(0, 0.5f);
        rr.offsetMin = new Vector2(32, 0);
        rr.offsetMax = new Vector2(-500, 0);

        Button dir = Seta(linha, "SetaDireita", "seta_dir", -60);

        RectTransform caixa = Filho(linha, "Caixa");
        Pos(caixa, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-256, 0), new Vector2(320, 68));
        Img(caixa.gameObject, S("seletor_caixa"), Color.white, true).raycastTarget = false;
        var val = Txt(caixa, "Valor", exemplo, fCodigoBold, 24, Texto, TextAlignmentOptions.Center);
        val.textWrappingMode = TextWrappingModes.NoWrap;
        val.overflowMode = TextOverflowModes.Ellipsis;
        var vr = (RectTransform)val.transform;
        Esticar(vr);
        vr.offsetMin = new Vector2(12, 2);
        vr.offsetMax = new Vector2(-12, 0);

        Button esq = Seta(linha, "SetaEsquerda", "seta_esq", -452);

        so.FindProperty("valor").objectReferenceValue = val;
        so.FindProperty("setaEsquerda").objectReferenceValue = esq;
        so.FindProperty("setaDireita").objectReferenceValue = dir;
        so.ApplyModifiedPropertiesWithoutUndo();
        return b;
    }

    static Button Seta(Transform linha, string nome, string sprite, float x)
    {
        RectTransform r = Filho(linha, nome);
        Pos(r, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(56, 56));
        Image img = Img(r.gameObject, S(sprite), Color.white);
        img.preserveAspect = true;
        Button b = Garantir<Button>(r.gameObject);
        b.targetGraphic = img;
        b.transition = Selectable.Transition.ColorTint;
        b.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = Ciano,
            pressedColor = Azul,
            selectedColor = Color.white,
            disabledColor = new Color(1, 1, 1, 0.3f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f,
        };
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        return b;
    }

    static Button LinhaInterruptor(RectTransform pai, string nome, string rotulo, float y, OpcaoConfigUI.Opcao opcao, bool ligadoNoExemplo)
    {
        Button b = LinhaBase(pai, nome, y, opcao, out SerializedObject so);
        Transform linha = b.transform;

        var rot = Txt(linha, "Rotulo", rotulo, fTexto, 30, Texto, TextAlignmentOptions.MidlineLeft);
        rot.textWrappingMode = TextWrappingModes.NoWrap;
        var rr = (RectTransform)rot.transform;
        rr.anchorMin = new Vector2(0, 0);
        rr.anchorMax = new Vector2(1, 1);
        rr.pivot = new Vector2(0, 0.5f);
        rr.offsetMin = new Vector2(32, 0);
        rr.offsetMax = new Vector2(-180, 0);

        RectTransform chave = Filho(linha, "Interruptor");
        Pos(chave, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-32, 0), new Vector2(98, 63));
        Image chaveImg = Img(chave.gameObject, S(ligadoNoExemplo ? "interruptor_ligado" : "interruptor_desligado"), Color.white);
        chaveImg.raycastTarget = false;
        chaveImg.preserveAspect = true;

        so.FindProperty("interruptor").objectReferenceValue = chaveImg;
        so.FindProperty("spriteLigado").objectReferenceValue = S("interruptor_ligado");
        so.FindProperty("spriteDesligado").objectReferenceValue = S("interruptor_desligado");
        so.ApplyModifiedPropertiesWithoutUndo();
        return b;
    }

    static void MontarContadorFPS(Transform canvas)
    {
        RectTransform r = Filho(canvas, "ContadorFPS");
        r.SetAsLastSibling();
        Pos(r, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -16), new Vector2(240, 48));
        var t = Txt(r.parent, "ContadorFPS", "60 FPS", fCodigoBold, 26, Verde, TextAlignmentOptions.TopRight);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enabled = false;

        var c = Garantir<ContadorFPSUI>(r.gameObject);
        var so = new SerializedObject(c);
        so.FindProperty("texto").objectReferenceValue = t;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
