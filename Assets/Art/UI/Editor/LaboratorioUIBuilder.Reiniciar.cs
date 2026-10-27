using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Reiniciar fase" no Resources/GameUICanvas: botão na
/// pausa entre Continuar e Configurações (com o chip da tecla), barra de dicas
/// da pausa, confirmação "Reiniciar a fase?" e o anel "Segure para
/// reiniciar…" do canto inferior esquerdo. Lógica em ReinicioFase,
/// PauseMenuUI, ConfirmarReinicioUI e ReinicioSeguraUI.
///
/// Backup do prefab antes da primeira montagem: SceneBackups/reiniciar_fase_*.
/// </summary>
public static partial class LaboratorioUIBuilder
{
    [MenuItem("Tools/UI Laboratório/5. Reiniciar fase (pausa, confirmação e anel)")]
    public static void AplicarReiniciarFase()
    {
        Preparar();
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabGameUI);
        try
        {
            MontarPausa((RectTransform)root.transform.Find("PausePanel"));
            MontarConfirmacaoReinicio(root.transform);
            MontarAnelReinicio(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabGameUI);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log("[LaboratorioUI] Reiniciar fase montado na pausa.");
    }

    static void GarantirIconeReiniciar()
    {
        foreach (string nome in new[] { "icone_reiniciar", "anel_reiniciar" })
        {
            string caminho = Pasta + "Sprites/" + nome + ".png";
            var imp = AssetImporter.GetAtPath(caminho) as TextureImporter;
            if (imp == null || (imp.textureType == TextureImporterType.Sprite && imp.spriteImportMode == SpriteImportMode.Single && imp.mipmapEnabled == false))
            {
                continue;
            }

            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
    }

    // Segundo botão da pausa: secundário, ícone de seta circular e chip com a tecla atual.
    static Button MontarBotaoReiniciar(RectTransform pausa)
    {
        GarantirIconeReiniciar();

        Transform t = pausa.Find("ReiniciarButton");
        if (t == null)
        {
            Transform modelo = pausa.Find("ConfiguracoesButton");
            GameObject copia = Object.Instantiate(modelo.gameObject, pausa);
            copia.name = "ReiniciarButton";
            t = copia.transform;
            // Cópia sem os cliques do modelo (o PauseMenuUI liga pelo nome).
            t.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();
        }
        t.SetSiblingIndex(pausa.Find("ContinuarButton").GetSiblingIndex() + 1);

        Button b = BotaoPainel(pausa, "ReiniciarButton", "secundario", "Reiniciar fase", "icone_reiniciar", 96);
        var label = (RectTransform)t.Find("Label");
        label.offsetMax = new Vector2(-96, 0); // espaço pro chip da tecla

        RectTransform chip = ChipTecla(t, "Tecla", "R", 26);
        Pos(chip, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(56, 52));
        return b;
    }

    // Chip de tecla no estilo da legenda do kit; o texto acompanha a ação Restart (RotuloTeclaUI).
    static RectTransform ChipTecla(Transform pai, string nome, string tecla, float tamanho, bool seguirAcao = true)
    {
        RectTransform k = Filho(pai, nome);
        Img(k.gameObject, S("btn_secundario_normal"), Color.white, true).pixelsPerUnitMultiplier = 2.6f;
        k.GetComponent<Image>().raycastTarget = false;
        var kh = Garantir<HorizontalLayoutGroup>(k.gameObject);
        kh.padding = new RectOffset(12, 12, 4, 6);
        kh.childControlWidth = kh.childControlHeight = true;
        kh.childForceExpandWidth = kh.childForceExpandHeight = false;
        kh.childAlignment = TextAnchor.MiddleCenter;
        // Solto num botão, o chip cresce sozinho com a tecla; dentro de uma dica, o layout dela cuida disso.
        bool emLayout = pai.GetComponent<HorizontalLayoutGroup>() != null;
        var fit = k.GetComponent<ContentSizeFitter>();
        if (emLayout && fit != null) Object.DestroyImmediate(fit);
        if (!emLayout)
        {
            fit = Garantir<ContentSizeFitter>(k.gameObject);
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        }
        var kle = Garantir<LayoutElement>(k.gameObject);
        kle.minHeight = 44;
        kle.minWidth = 48;
        var txt = Txt(k, "Texto", tecla, fCodigoBold, tamanho, Texto, TextAlignmentOptions.Center);
        txt.textWrappingMode = TextWrappingModes.NoWrap;

        if (seguirAcao)
        {
            var rot = Garantir<RotuloTeclaUI>(k.gameObject);
            var so = new SerializedObject(rot);
            so.FindProperty("texto").objectReferenceValue = txt;
            so.FindProperty("acao").stringValue = InputManager.RestartActionName;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return k;
    }

    // Barra de dicas da pausa ("Esc Continuar   R Reiniciar") no rodapé da tela,
    // sobre a mesma faixa escura das Configurações e dos Créditos: a faixa cobre
    // a barra de teclas da tela de trás. Fica no Canvas (a janela da pausa tem
    // tamanho fixo) e o PauseMenuUI liga e desliga junto com a pausa.
    static void MontarDicasPausa(RectTransform pausa)
    {
        Transform antiga = pausa.Find("BarraDicas");
        if (antiga != null) Object.DestroyImmediate(antiga.gameObject);

        RectTransform faixa = Filho(pausa.parent, "RodapePausa");
        faixa.SetSiblingIndex(pausa.GetSiblingIndex() + 1);
        Pos(faixa, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 96));
        Img(faixa.gameObject, null, TemaLabUI.Fundo).raycastTarget = false;
        faixa.gameObject.SetActive(false);

        RectTransform barra = Filho(faixa, "BarraDicas");
        Pos(barra, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(-128, 52));
        var h = Garantir<HorizontalLayoutGroup>(barra.gameObject);
        h.childAlignment = TextAnchor.MiddleRight;
        h.spacing = 36;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        Dica(barra, "Dica_Continuar", "Esc", "Continuar", false);
        Dica(barra, "Dica_Reiniciar", "R", "Reiniciar", true);
    }

    static void Dica(RectTransform barra, string nome, string tecla, string rotulo, bool seguirAcao)
    {
        RectTransform d = Filho(barra, nome);
        var h = Garantir<HorizontalLayoutGroup>(d.gameObject);
        h.childAlignment = TextAnchor.MiddleCenter;
        h.spacing = 12;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        ChipTecla(d, "Tecla", tecla, 22, seguirAcao);
        var r = Txt(d, "Rotulo", rotulo, fTexto, 24, TextoSuave, TextAlignmentOptions.MidlineLeft);
        r.textWrappingMode = TextWrappingModes.NoWrap;
    }

    // Popup curto no estilo da confirmação do seletor de fases.
    static void MontarConfirmacaoReinicio(Transform root)
    {
        GarantirIconeReiniciar();

        RectTransform conf = Filho(root, "ConfirmarReinicio");
        conf.SetAsLastSibling();
        Esticar(conf);
        var veu = Img(conf.gameObject, S("veu_popup"), Color.white);
        veu.raycastTarget = true; // bloqueia a pausa por trás

        RectTransform caixa = Filho(conf, "Caixa");
        Centro(caixa, new Vector2(0, 0), new Vector2(860, 380));
        Img(caixa.gameObject, S("painel_popup"), Color.white, true);

        var titulo = Txt(caixa, "Titulo", "Reiniciar a fase?", fTitulo, 44, Texto, TextAlignmentOptions.Center);
        Pos(titulo.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(780, 64));

        var texto = Txt(caixa, "Texto", "Você volta ao começo desta fase. O que você montou nela será desfeito.", fTextoBold, 28, TextoSuave, TextAlignmentOptions.Top);
        texto.textWrappingMode = TextWrappingModes.Normal;
        Pos(texto.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -122), new Vector2(720, 90));

        Button cancelar = BotaoPopup(caixa, "CancelarButton", "secundario", "Cancelar", "Esc", -180f);
        Button reiniciar = BotaoPopup(caixa, "ReiniciarButton", "primario", "Reiniciar", "Enter", 180f);
        cancelar.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = reiniciar };
        reiniciar.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = cancelar };

        var ui = Garantir<ConfirmarReinicioUI>(conf.gameObject);
        var so = new SerializedObject(ui);
        so.FindProperty("cancelar").objectReferenceValue = cancelar;
        so.FindProperty("reiniciar").objectReferenceValue = reiniciar;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Reiniciar já em foco (Enter confirma); ao fechar, o foco volta pro botão da pausa.
        Foco(conf.gameObject, reiniciar, cancelar);
        conf.gameObject.SetActive(false);
    }

    static Button BotaoPopup(RectTransform caixa, string nome, string tipo, string rotulo, string tecla, float x)
    {
        Transform t = caixa.Find(nome);
        if (t == null)
        {
            t = UIBuildUtils.CreateButtonWithLabel(caixa, nome, rotulo, out _).transform;
        }

        var r = (RectTransform)t;
        Pos(r, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(x, 84), new Vector2(320, 96));
        Button b = t.GetComponent<Button>();
        EstiloBotao(b, tipo);

        var l = t.Find("Label").GetComponent<TMP_Text>();
        l.text = rotulo;
        Estilo(l, fTitulo, 34, tipo == "primario" ? Color.white : Texto, TextAlignmentOptions.Center);
        l.textWrappingMode = TextWrappingModes.NoWrap;
        var lr = (RectTransform)l.transform;
        Esticar(lr);
        lr.offsetMin = new Vector2(20, 4);
        lr.offsetMax = new Vector2(tecla.Length > 3 ? -116 : -92, 0);

        RectTransform chip = ChipTecla(t, "Tecla", tecla, 22, false);
        Pos(chip, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(56, 48));
        return b;
    }

    // Anel "Segure para reiniciar…" no canto inferior esquerdo (escondido até a tecla ser segurada).
    static void MontarAnelReinicio(Transform root)
    {
        GarantirIconeReiniciar();

        RectTransform anel = Filho(root, "ReinicioAnel");
        Pos(anel, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(40, 40), new Vector2(420, 96));
        var grupo = Garantir<CanvasGroup>(anel.gameObject);
        grupo.alpha = 0f;
        grupo.blocksRaycasts = false;
        grupo.interactable = false;

        RectTransform fundo = Filho(anel, "Fundo");
        Esticar(fundo);
        Img(fundo.gameObject, S("painel_popup"), Color.white, true).raycastTarget = false;
        fundo.GetComponent<Image>().pixelsPerUnitMultiplier = 2f;

        RectTransform trilho = Filho(anel, "Trilho");
        Pos(trilho, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(52, 0), new Vector2(64, 64));
        Img(trilho.gameObject, S("anel_reiniciar"), Linha).raycastTarget = false;

        RectTransform enche = Filho(anel, "Preenchimento");
        Pos(enche, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(52, 0), new Vector2(64, 64));
        var img = Img(enche.gameObject, S("anel_reiniciar"), Ciano);
        img.raycastTarget = false;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillOrigin = (int)Image.Origin360.Top;
        img.fillClockwise = true;
        img.fillAmount = 0f;

        RectTransform icone = Filho(anel, "Icone");
        Pos(icone, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(52, 0), new Vector2(30, 30));
        Img(icone.gameObject, S("icone_reiniciar"), Texto).raycastTarget = false;

        var rotulo = Txt(anel, "Rotulo", "Segure para reiniciar…", fTexto, 28, Texto, TextAlignmentOptions.MidlineLeft);
        rotulo.textWrappingMode = TextWrappingModes.NoWrap;
        var rr = rotulo.rectTransform;
        Esticar(rr);
        rr.offsetMin = new Vector2(100, 0);
        rr.offsetMax = new Vector2(-20, 0);

        var ui = Garantir<ReinicioSeguraUI>(anel.gameObject);
        var so = new SerializedObject(ui);
        so.FindProperty("grupo").objectReferenceValue = grupo;
        so.FindProperty("preenchimento").objectReferenceValue = img;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
