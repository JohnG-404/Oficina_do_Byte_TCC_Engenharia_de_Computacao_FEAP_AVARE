using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Aplica o tema Laboratório no Canvas das cenas Interacao e Interacao_Vida:
/// painel de configurar item (janela, placa com o nome do item, colunas Ação
/// e Consequência lado a lado, linhas do kit, check verde), aviso rápido no
/// rodapé, painel "Fase Concluída!" e, se existir, a placa do HUD de vida.
///
/// Só mexe no visual e nas referências de UI do InteractionConfigUI; a lógica
/// continua no script. Rodar de novo numa cena já convertida não faz nada.
/// Menu: Tools > UI Laboratório > Aplicar na cena de Interação aberta.
/// </summary>
public static class InteracaoLabBuilder
{
    private const string PastaSprites = "Assets/Art/UI/Sprites/";

    [MenuItem("Tools/UI Laboratório/Aplicar na cena de Interação aberta")]
    private static void AplicarNaCenaAberta()
    {
        var ui = Object.FindAnyObjectByType<InteractionConfigUI>(FindObjectsInactive.Include);
        if (ui == null)
        {
            Debug.LogWarning("[InteracaoLabBuilder] Nenhum InteractionConfigUI na cena aberta.");
            return;
        }

        Aplicar(ui);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
    }

    /// <summary>Converte o Canvas que contém este InteractionConfigUI. Devolve false se já estava convertido.</summary>
    public static bool Aplicar(InteractionConfigUI ui)
    {
        var root = (RectTransform)ui.transform;
        if (root.Find("Tela") != null)
        {
            return false;
        }

        TemaLaboratorio tema = TemaLaboratorio.Atual;
        if (tema == null)
        {
            Debug.LogError("[InteracaoLabBuilder] Resources/TemaLaboratorio não encontrado.");
            return false;
        }

        // O painel da Interacao_Vida é instância de Prefabs/InteractionConfigPanel
        // (só essa cena usa): desvincula na cena para poder reorganizar os
        // filhos. O arquivo do prefab não é alterado.
        Desempacotar(ui.gameObject);

        // ---------------- raiz: tela cheia, sem fundo próprio
        Esticar(root);
        var rootImg = root.GetComponent<Image>();
        if (rootImg != null)
        {
            rootImg.enabled = false;
        }

        // Tela (panelRoot): véu + janela. A raiz fica sempre ativa, para o
        // aviso rápido conseguir sumir sozinho mesmo com o painel fechado.
        RectTransform tela = Novo(root, "Tela");
        Esticar(tela);
        tela.SetAsFirstSibling();
        Image veu = tela.gameObject.AddComponent<Image>();
        veu.color = TemaLabUI.CorVeu;
        veu.raycastTarget = true;

        Image janela = Img(tela, "Janela", tema.painelJanela, true);
        Centro((RectTransform)janela.transform, new Vector2(0f, -10f), new Vector2(1400f, 860f));
        RectTransform j = (RectTransform)janela.transform;

        foreach (string nome in new[] { "Header", "ActionColumn", "ConsequenceColumn", "ActionButtonTemplate", "ConsequenceButtonTemplate", "ConfirmButton", "CancelButton" })
        {
            Transform t = root.Find(nome);
            if (t != null) t.SetParent(j, false);
        }

        // ---------------- placa com o nome do item
        var header = (RectTransform)j.Find("Header");
        if (header != null)
        {
            Sprite(header.GetComponent<Image>(), tema.painelTitulo);
            Pos(header, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(420f, 104f));
            var title = header.Find("Title")?.GetComponent<TextMeshProUGUI>();
            if (title != null)
            {
                Esticar(title.rectTransform, new Vector2(28f, 12f), new Vector2(-28f, -4f));
                Fonte(title, tema.display, 50f, Color.white, TextAlignmentOptions.Center);
                title.enableAutoSizing = true;
                title.fontSizeMin = 24f;
                title.fontSizeMax = 50f;
                title.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }

        TextMeshProUGUI rotulo = Texto(j, "Rotulo", "Configurar item", tema.monoForte, 22f, TemaLabUI.Ciano, TextAlignmentOptions.MidlineLeft);
        rotulo.characterSpacing = 8f;
        rotulo.fontStyle = FontStyles.UpperCase;
        Pos(rotulo.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(80f, -80f), new Vector2(600f, 32f));

        // ---------------- colunas
        Coluna(j.Find("ActionColumn") as RectTransform, tema, 80f, "Ação", "como interagir");
        Coluna(j.Find("ConsequenceColumn") as RectTransform, tema, 720f, "Consequência", "o que acontece");

        RectTransform aguardando = CaixaBorda(j, "AguardandoAcao", tema, new Color32(0x3A, 0x48, 0x68, 0xFF), new Color32(0x1A, 0x23, 0x37, 0xFF), 3f);
        Pos(aguardando, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(720f, -214f), new Vector2(600f, 306f));
        TextMeshProUGUI aguardandoTxt = Texto(aguardando, "Texto", "Escolha uma ação\npara ver as consequências", tema.corpoForte, 27f, TemaLabUI.TextoSuave, TextAlignmentOptions.Center);
        Esticar(aguardandoTxt.rectTransform, new Vector2(24f, 0f), new Vector2(-24f, 0f));

        Template(j.Find("ActionButtonTemplate") as RectTransform, tema, 98f);
        Template(j.Find("ConsequenceButtonTemplate") as RectTransform, tema, 84f);

        // ---------------- botões
        var confirm = j.Find("ConfirmButton") as RectTransform;
        if (confirm != null)
        {
            Pos(confirm, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-80f, 44f), new Vector2(340f, 96f));
            Botao(confirm, tema.botaoPrimario, tema.display, 32f, Color.white, "Salvar e aplicar");
        }

        var fechar = j.Find("CancelButton") as RectTransform;
        if (fechar != null)
        {
            Pos(fechar, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -36f), new Vector2(96f, 96f));
            Image f = fechar.GetComponent<Image>();
            f.sprite = tema.botaoIcone.normal;
            f.type = Image.Type.Simple;
            f.color = Color.white;
            TemaLabUI.AplicarEstados(fechar.GetComponent<Button>(), tema.botaoIcone);
            var icone = fechar.Find("Image") as RectTransform;
            if (icone != null)
            {
                Centro(icone, new Vector2(0f, 3f), new Vector2(40f, 40f));
                icone.GetComponent<Image>().color = TemaLabUI.CorTexto;
            }
        }

        Image cancelarImg = Img(j, "CancelarButton", tema.botaoSecundario.normal, true);
        var cancelar = cancelarImg.gameObject.AddComponent<Button>();
        cancelar.targetGraphic = cancelarImg;
        TextMeshProUGUI cancelarLbl = Texto(cancelarImg.transform, "Label", "Cancelar", tema.display, 30f, TemaLabUI.CorTexto, TextAlignmentOptions.Center);
        Esticar(cancelarLbl.rectTransform, new Vector2(20f, 6f), new Vector2(-20f, 0f));
        TemaLabUI.AplicarEstados(cancelar, tema.botaoSecundario);
        Pos((RectTransform)cancelar.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-440f, 44f), new Vector2(260f, 96f));

        // "Quem ativa" e "Repetir" ao lado dos botões Cancelar/Salvar, para
        // as colunas terem altura para 4 opções sem passar por cima delas.
        RectTransform regras = Novo(j, "RegrasExtras");
        Pos(regras, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(80f, 32f), new Vector2(600f, 116f));

        // ---------------- aviso rápido (fora da Tela: aparece com o painel fechado)
        Image aviso = Img(root, "Aviso", tema.painelPopup, false);
        Pos((RectTransform)aviso.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(860f, 108f));
        Image avisoIcone = Img(aviso.transform, "Icone", tema.iconeInfo, false);
        avisoIcone.type = Image.Type.Simple;
        avisoIcone.preserveAspect = true;
        Pos((RectTransform)avisoIcone.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 3f), new Vector2(52f, 52f));
        TextMeshProUGUI avisoTxt = Texto(aviso.transform, "Texto", "", tema.corpoForte, 30f, TemaLabUI.CorTexto, TextAlignmentOptions.MidlineLeft);
        avisoTxt.enableAutoSizing = true;
        avisoTxt.fontSizeMin = 20f;
        avisoTxt.fontSizeMax = 30f;
        Esticar(avisoTxt.rectTransform, new Vector2(112f, 6f), new Vector2(-36f, 0f));
        aviso.gameObject.SetActive(false);

        // ---------------- referências do script
        var so = new SerializedObject(ui);
        so.FindProperty("panelRoot").objectReferenceValue = tela.gameObject;
        so.FindProperty("cancelButtonSecundario").objectReferenceValue = cancelar;
        so.FindProperty("consequencePlaceholder").objectReferenceValue = aguardando.gameObject;
        so.FindProperty("hintRoot").objectReferenceValue = aviso.gameObject;
        so.FindProperty("hintText").objectReferenceValue = avisoTxt;
        so.FindProperty("rulesRowsRoot").objectReferenceValue = regras;
        so.FindProperty("codeFont").objectReferenceValue = tema.mono;
        so.FindProperty("unityLineColor").colorValue = TemaLabUI.Ciano;
        so.FindProperty("unityLineFontSize").floatValue = 19f;
        so.FindProperty("unityLineMonoSpacing").floatValue = 0f;
        so.FindProperty("unityLineRightMargin").floatValue = 90f;
        so.FindProperty("rowsFontSize").floatValue = 24f;
        so.FindProperty("chipFontSize").floatValue = 20f;
        so.FindProperty("rowsHintFontSize").floatValue = 19f;
        so.FindProperty("rowsLabelColor").colorValue = TemaLabUI.TextoSuave;
        so.FindProperty("rowsHintColor").colorValue = new Color32(0x76, 0x83, 0x9F, 0xFF);
        so.FindProperty("chipTextColor").colorValue = TemaLabUI.CorTexto;
        so.FindProperty("chipSelectedColor").colorValue = TemaLabUI.Dourado;
        so.FindProperty("chipBackgroundColor").colorValue = TemaLabUI.Poco;
        so.FindProperty("chipBorderColor").colorValue = TemaLabUI.Contorno;
        so.ApplyModifiedPropertiesWithoutUndo();

        // ---------------- outros painéis do mesmo Canvas
        Transform canvas = root.parent;
        FaseConcluida(canvas.Find("LevelCompletePanel") as RectTransform, tema);
        PlacaVida(canvas.GetComponentInChildren<HeartHealthUI>(true), tema);
        return true;
    }

    // ------------------------------------------------------------ partes

    private static void Coluna(RectTransform coluna, TemaLaboratorio tema, float x, string titulo, string subtitulo)
    {
        if (coluna == null) return;

        Pos(coluna, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -136f), new Vector2(600f, 560f));

        TextMeshProUGUI header = null;
        RectTransform opcoes = null;
        foreach (Transform filho in coluna)
        {
            if (filho.name.StartsWith("Header")) header = filho.GetComponent<TextMeshProUGUI>();
            if (filho.name == "OptionsContainer") opcoes = (RectTransform)filho;
        }

        if (header != null)
        {
            var hr = header.rectTransform;
            hr.anchorMin = new Vector2(0f, 1f);
            hr.anchorMax = new Vector2(1f, 1f);
            hr.pivot = new Vector2(0.5f, 1f);
            hr.offsetMin = new Vector2(0f, -64f);
            hr.offsetMax = Vector2.zero;
            Fonte(header, tema.display, 38f, TemaLabUI.CorTexto, TextAlignmentOptions.MidlineLeft);
            header.text = $"{titulo}  <size=63%><color=#9AA8C4>{subtitulo}</color></size>";
            header.margin = new Vector4(78f, 0f, 0f, 0f);
            header.textWrappingMode = TextWrappingModes.NoWrap;

            var selo = header.transform.Find("Image") as RectTransform;
            if (selo != null)
            {
                Pos(selo, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
                Image si = selo.GetComponent<Image>();
                si.sprite = tema.seloNumero;
                si.type = Image.Type.Simple;
                si.preserveAspect = true;
                si.color = Color.white;
                var n = selo.GetComponentInChildren<TextMeshProUGUI>(true);
                if (n != null)
                {
                    Esticar(n.rectTransform, new Vector2(0f, 3f), Vector2.zero);
                    Fonte(n, tema.display, 32f, TemaLabUI.TextoSeloNumero, TextAlignmentOptions.Center);
                }
            }
        }

        if (opcoes != null)
        {
            opcoes.anchorMin = Vector2.zero;
            opcoes.anchorMax = Vector2.one;
            opcoes.pivot = new Vector2(0.5f, 1f);
            opcoes.offsetMin = Vector2.zero;
            opcoes.offsetMax = new Vector2(0f, -78f);
            var vlg = opcoes.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.padding = new RectOffset(0, 0, 0, 0);
                vlg.spacing = 14f;
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlWidth = true;
                vlg.childForceExpandWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandHeight = false;
            }
        }
    }

    private static void Template(RectTransform t, TemaLaboratorio tema, float altura)
    {
        if (t == null) return;

        t.sizeDelta = new Vector2(600f, altura);
        var le = t.GetComponent<LayoutElement>();
        if (le != null)
        {
            le.preferredHeight = altura;
            le.minHeight = altura;
        }

        Image fundo = t.GetComponent<Image>();
        Sprite(fundo, tema.painelLinha);
        TemaLabUI.AplicarEstados(t.GetComponent<Button>(), new TemaLaboratorio.Estados
        {
            normal = tema.painelLinha,
            hover = tema.painelLinhaFoco,
            pressed = tema.painelLinhaFoco,
            focus = tema.painelLinhaFoco,
            disabled = tema.painelLinha,
        });

        // Destaque de selecionado: tinta azul + faixa à esquerda (ligado pelo script).
        Image sel = Img(t, "Selecionado", tema.bordaArredondada, false);
        sel.color = new Color(0.29f, 0.55f, 0.97f, 0.12f);
        Esticar((RectTransform)sel.transform, new Vector2(6f, 6f), new Vector2(-6f, -6f));
        sel.transform.SetAsFirstSibling();
        Image faixa = Img(sel.transform, "Faixa", null, false);
        faixa.color = TemaLabUI.Azul;
        var fr = (RectTransform)faixa.transform;
        fr.anchorMin = new Vector2(0f, 0f);
        fr.anchorMax = new Vector2(0f, 1f);
        fr.pivot = new Vector2(0f, 0.5f);
        fr.offsetMin = new Vector2(2f, 12f);
        fr.offsetMax = new Vector2(8f, -12f);
        sel.gameObject.SetActive(false);

        var label = t.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if (label != null)
        {
            Esticar(label.rectTransform, new Vector2(28f, 0f), new Vector2(-90f, 0f));
            Fonte(label, tema.corpoForte, 29f, TemaLabUI.CorTexto, TextAlignmentOptions.MidlineLeft);
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }

        var check = t.Find("Check") as RectTransform;
        if (check != null)
        {
            Pos(check, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(48f, 48f));
            Image ci = check.GetComponent<Image>();
            Sprite(ci, tema.bordaArredondada);
            ci.color = new Color32(0x26, 0x32, 0x4B, 0xFF);
            var ok = check.Find("CheckOK") as RectTransform;
            if (ok != null)
            {
                Centro(ok, Vector2.zero, new Vector2(60f, 60f));
                Image oi = ok.GetComponent<Image>();
                oi.sprite = tema.seloCheck;
                oi.type = Image.Type.Simple;
                oi.preserveAspect = true;
                oi.color = Color.white;
            }
        }

        var icone = t.Find("Image");
        if (icone != null)
        {
            icone.gameObject.SetActive(false);
        }
    }

    private static void FaseConcluida(RectTransform painel, TemaLaboratorio tema)
    {
        if (painel == null || painel.Find("Janela") != null) return;
        Desempacotar(painel.gameObject);

        Esticar(painel);
        Image veu = painel.GetComponent<Image>();
        if (veu != null)
        {
            veu.sprite = null;
            veu.type = Image.Type.Simple;
            veu.color = TemaLabUI.CorVeu;
            veu.raycastTarget = true;
        }

        Image janela = Img(painel, "Janela", tema.painelPopup, true);
        Centro((RectTransform)janela.transform, Vector2.zero, new Vector2(800f, 440f));
        Image selo = Img(janela.transform, "Selo", tema.seloCheck, false);
        selo.type = Image.Type.Simple;
        selo.preserveAspect = true;
        Centro((RectTransform)selo.transform, new Vector2(0f, 70f), new Vector2(140f, 140f));

        var texto = painel.Find("Text")?.GetComponent<TextMeshProUGUI>();
        if (texto != null)
        {
            texto.transform.SetParent(janela.transform, false);
            Centro(texto.rectTransform, new Vector2(0f, -86f), new Vector2(740f, 110f));
            Fonte(texto, tema.display, 84f, TemaLabUI.CorTexto, TextAlignmentOptions.Center);
            texto.enableAutoSizing = false;
            texto.textWrappingMode = TextWrappingModes.NoWrap;
        }
    }

    // HUD de vida: a barra vai para o canto de cima à esquerda, dentro de uma
    // placa do kit com o rótulo VIDA. Os quadrados (e a lógica do
    // HeartHealthUI) continuam os mesmos, só mudam de posição.
    private static void PlacaVida(HeartHealthUI vida, TemaLaboratorio tema)
    {
        if (vida == null || vida.transform.Find("PlacaVida") != null) return;
        Desempacotar(vida.gameObject);

        var rt = (RectTransform)vida.transform;
        var quadrados = new System.Collections.Generic.List<RectTransform>();
        foreach (Transform filho in rt)
        {
            if (filho.GetComponent<Image>() != null) quadrados.Add((RectTransform)filho);
        }
        quadrados.Sort((x, y) => x.anchoredPosition.x.CompareTo(y.anchoredPosition.x));

        float largura = 150f + quadrados.Count * 58f + 20f;
        Pos(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -32f), new Vector2(largura, 96f));

        for (int i = 0; i < quadrados.Count; i++)
        {
            Pos(quadrados[i], new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(150f + i * 58f, 0f), new Vector2(100f, 100f));
        }

        Image placa = Img(rt, "PlacaVida", tema.painelLinha, false);
        placa.transform.SetAsFirstSibling();
        Esticar((RectTransform)placa.transform);

        TextMeshProUGUI rotulo = Texto(placa.transform, "Rotulo", "VIDA", tema.monoForte, 20f, TemaLabUI.Ciano, TextAlignmentOptions.MidlineLeft);
        rotulo.characterSpacing = 8f;
        rotulo.rectTransform.anchorMin = new Vector2(0f, 0f);
        rotulo.rectTransform.anchorMax = new Vector2(0f, 1f);
        rotulo.rectTransform.pivot = new Vector2(0f, 0.5f);
        rotulo.rectTransform.anchoredPosition = new Vector2(26f, 0f);
        rotulo.rectTransform.sizeDelta = new Vector2(90f, 0f);
    }

    // ------------------------------------------------------------ utilitários

    private static void Desempacotar(GameObject go)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(go)) return;
        GameObject raiz = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
        PrefabUtility.UnpackPrefabInstance(raiz, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
    }

    private static RectTransform Novo(Transform parent, string nome)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static Image Img(Transform parent, string nome, Sprite sprite, bool raycast)
    {
        Image img = Novo(parent, nome).gameObject.AddComponent<Image>();
        Sprite(img, sprite);
        img.raycastTarget = raycast;
        return img;
    }

    private static void Sprite(Image img, Sprite sprite)
    {
        if (img == null) return;
        img.sprite = sprite;
        img.color = Color.white;
        img.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        img.pixelsPerUnitMultiplier = 1f;
        img.preserveAspect = false;
    }

    private static RectTransform CaixaBorda(Transform parent, string nome, TemaLaboratorio tema, Color borda, Color miolo, float espessura)
    {
        Image fora = Img(parent, nome, tema.bordaArredondada, false);
        fora.color = borda;
        Image dentro = Img(fora.transform, "Fundo", tema.bordaArredondada, false);
        dentro.color = miolo;
        Esticar((RectTransform)dentro.transform, new Vector2(espessura, espessura), new Vector2(-espessura, -espessura));
        return (RectTransform)fora.transform;
    }

    private static TextMeshProUGUI Texto(Transform parent, string nome, string texto, TMP_FontAsset fonte, float tamanho, Color cor, TextAlignmentOptions al)
    {
        var tmp = Novo(parent, nome).gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        Fonte(tmp, fonte, tamanho, cor, al);
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void Fonte(TextMeshProUGUI tmp, TMP_FontAsset fonte, float tamanho, Color cor, TextAlignmentOptions al)
    {
        if (fonte != null)
        {
            tmp.font = fonte;
            tmp.fontSharedMaterial = fonte.material;
        }
        tmp.fontSize = tamanho;
        tmp.color = cor;
        tmp.alignment = al;
        tmp.fontStyle = FontStyles.Normal;
        tmp.characterSpacing = 0f;
        tmp.richText = true;
    }

    private static void Botao(RectTransform rt, TemaLaboratorio.Estados estados, TMP_FontAsset fonte, float tamanho, Color cor, string texto)
    {
        Image img = rt.GetComponent<Image>();
        Sprite(img, estados.normal);
        TemaLabUI.AplicarEstados(rt.GetComponent<Button>(), estados);
        var label = rt.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            Esticar(label.rectTransform, new Vector2(20f, 6f), new Vector2(-20f, 0f));
            Fonte(label, fonte, tamanho, cor, TextAlignmentOptions.Center);
            label.text = texto;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }
    }

    private static void Esticar(RectTransform rt)
    {
        Esticar(rt, Vector2.zero, Vector2.zero);
    }

    private static void Esticar(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = min;
        rt.offsetMax = max;
    }

    private static void Centro(RectTransform rt, Vector2 pos, Vector2 tamanho)
    {
        Pos(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, tamanho);
    }

    private static void Pos(RectTransform rt, Vector2 ancora, Vector2 pivo, Vector2 pos, Vector2 tamanho)
    {
        rt.anchorMin = rt.anchorMax = ancora;
        rt.pivot = pivo;
        rt.anchoredPosition = pos;
        rt.sizeDelta = tamanho;
    }
}
