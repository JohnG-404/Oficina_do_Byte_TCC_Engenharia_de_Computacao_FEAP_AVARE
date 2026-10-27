using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Textos, barra de teclas e faixa "como jogar" da Oficina de Animação, na cena
/// Tutorial_Anim já montada:
/// - rótulos da linha do tempo, da prévia e da bandeja a 20 px (INÍCIO/FIM a 18 px);
/// - rodapé de texto solto trocado pela barra de teclas do tema
///   (tecla desenhada + ação, como TemaLabUI.LegendaTeclas, no canto de baixo à direita);
/// - quadro "como jogar" vira uma faixa de largura inteira embaixo da bandeja,
///   com a lâmpada, no formato do painel Dica dos puzzles;
/// - monitor de prévia desce até o fim da bandeja, com a tela maior;
/// - faixa e barra somem enquanto o Prof fala (EsconderDuranteFala no Cenario).
///
/// Não refaz o resto da cena (o OficinaAnimacaoBuilder apagaria as 15 rodadas
/// atuais). Pode rodar de novo: as posições são absolutas e a barra é recriada.
/// Salva a cena; faça backup antes (SceneBackups/).
/// </summary>
public static class OficinaTextosBuilder
{
    private const string Pasta = "Assets/Anim/TutorialAnim/";

    // Mesmo layout do OficinaAnimacaoBuilder (unidades de mundo; 1 unidade = 108 px em 1080p).
    private const float EsqX = -8.33f, DirX = 3.95f, DirLarg = 4.38f;
    private const float PxPorUnidade = 108f;

    private static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out Color c); return c; }
    private static readonly Color Ink = Hex("#0A0E17"), Elevado = Hex("#1B2438"), Contorno = Hex("#33415F");
    private static readonly Color Texto = Hex("#E8EEF9"), Suave = Hex("#9AA8C4"), AzulTitulo = Hex("#9FC2FF");

    [MenuItem("Tools/Tutorial Animação/Textos, barra de teclas e faixa Como jogar")]
    public static void Aplicar()
    {
        var cena = EditorSceneManager.GetActiveScene();
        if (cena.name != "Tutorial_Anim")
        {
            Debug.LogError("[OficinaTextos] Abra a cena Tutorial_Anim antes.");
            return;
        }

        Transform cenario = null;
        foreach (GameObject raiz in cena.GetRootGameObjects())
        {
            if (raiz.name == "Cenario")
            {
                cenario = raiz.transform;
            }
        }
        if (cenario == null)
        {
            Debug.LogError("[OficinaTextos] Objeto Cenario não encontrado. Rode antes Tools > Tutorial Animação > Montar Oficina de Animação.");
            return;
        }

        var sFundo = AssetDatabase.LoadAssetAtPath<Sprite>(Pasta + "UI/oficina_fundo.png");
        var sBordaGrossa = AssetDatabase.LoadAssetAtPath<Sprite>(Pasta + "UI/oficina_borda_grossa.png");
        var fTexto = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/UI/Fonts/Nunito-Bold SDF.asset");
        var fMono = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/UI/Fonts/JetBrainsMono-Bold SDF.asset");
        TemaLaboratorio tema = TemaLaboratorio.Atual;
        Sprite sLampada = tema != null ? tema.puzzleLampada : null;

        // --- textos de ajuda a 20 px ------------------------------------
        foreach (string n in new[] { "LinhaDoTempo/Rotulo", "LinhaDoTempo/Frames", "Monitor/Rotulo", "Monitor/Estado", "Monitor/Fps", "Bandeja/Rotulo", "Bandeja/Iscas" })
        {
            Tamanho(cenario, n, 20f);
        }
        Tamanho(cenario, "LinhaDoTempo/Inicio", 18f);
        Tamanho(cenario, "LinhaDoTempo/Fim", 18f);

        // --- monitor de prévia até o fim da bandeja ------------------------
        Transform monitor = cenario.Find("Monitor");
        float cx = DirX + DirLarg / 2f;
        const float topo = 3.75f, baixo = -2.45f;
        const float telaTopo = 3.2f, telaBaixo = -1.25f;
        float telaY = (telaTopo + telaBaixo) / 2f;
        Painel(monitor.Find("Painel"), new Vector2(cx, (topo + baixo) / 2f), new Vector2(DirLarg, topo - baixo));
        Painel(monitor.Find("Tela"), new Vector2(cx, telaY), new Vector2(DirLarg - 0.5f, telaTopo - telaBaixo));
        var grade = monitor.Find("Grade").GetComponent<SpriteRenderer>();
        grade.transform.localPosition = new Vector3(cx, telaY, 0f);
        grade.size = new Vector2(DirLarg - 0.62f, telaTopo - telaBaixo - 0.12f);
        EditorUtility.SetDirty(grade);
        Transform previa = monitor.Find("Previa");
        previa.localPosition = new Vector3(cx, telaY, previa.localPosition.z);
        foreach (string n in new[] { "BotaoTocar", "BotaoDica", "Fps" })
        {
            Transform b = monitor.Find(n);
            b.localPosition = new Vector3(b.localPosition.x, -1.85f, b.localPosition.z);
            if (b.GetComponent<TMP_Text>() is TMP_Text t)
            {
                t.rectTransform.anchoredPosition = b.localPosition;
            }
        }

        var puzzle = cenario.GetComponentInChildren<AnimationSequencePuzzle>(true);
        if (puzzle != null)
        {
            var so = new SerializedObject(puzzle);
            so.FindProperty("tamanhoPrevia").floatValue = 3.2f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- faixa "como jogar" embaixo da bandeja --------------------------
        Transform como = cenario.Find("ComoJogar");
        const float fTopo = -2.72f, fBaixo = -4.02f;
        float fY = (fTopo + fBaixo) / 2f;
        float largura = 2f * -EsqX;
        Painel(como.Find("Painel"), new Vector2(0f, fY), new Vector2(largura, fTopo - fBaixo));

        Transform lamp = como.Find("Lampada");
        if (lamp == null)
        {
            lamp = new GameObject("Lampada", typeof(SpriteRenderer)).transform;
            lamp.SetParent(como, false);
        }
        var srLamp = lamp.GetComponent<SpriteRenderer>();
        srLamp.sprite = sLampada;
        srLamp.sortingOrder = 4;
        if (sLampada != null)
        {
            float maior = Mathf.Max(sLampada.bounds.size.x, sLampada.bounds.size.y);
            lamp.localScale = Vector3.one * (0.78f / maior);
        }
        lamp.localPosition = new Vector3(EsqX + 0.75f, fY, 0f);
        EditorUtility.SetDirty(srLamp);

        var comoTexto = como.Find("Texto").GetComponent<TMP_Text>();
        comoTexto.fontSize = 20f / 10.8f;
        comoTexto.color = Texto;
        comoTexto.alignment = TextAlignmentOptions.Left;
        comoTexto.textWrappingMode = TextWrappingModes.Normal;
        comoTexto.lineSpacing = 18f;
        var rt = comoTexto.rectTransform;
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(-EsqX - 0.3f - (EsqX + 1.45f), fTopo - fBaixo - 0.2f);
        rt.anchoredPosition = new Vector2(EsqX + 1.45f, fY);
        rt.localPosition = new Vector3(EsqX + 1.45f, fY, 0f);
        comoTexto.text = OficinaAnimacaoHUD.ComTituloColorido(
            "<b>Arraste ou clique</b>\nArraste cada frame da bandeja até a linha do tempo, ou só clique nele. Clique num frame encaixado para devolver.",
            AzulTitulo);
        EditorUtility.SetDirty(comoTexto);

        var hud = cenario.GetComponentInChildren<OficinaAnimacaoHUD>(true);
        if (hud != null)
        {
            var sh = new SerializedObject(hud);
            sh.FindProperty("corTituloComoJogar").colorValue = AzulTitulo;
            sh.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- rodapé -> barra de teclas ----------------------------------------
        Transform rodape = cenario.Find("Rodape");
        if (rodape != null)
        {
            Object.DestroyImmediate(rodape.gameObject);
        }
        Transform velha = cenario.Find("BarraTeclas");
        if (velha != null)
        {
            Object.DestroyImmediate(velha.gameObject);
        }
        BarraTeclas(cenario, sFundo, fTexto, fMono, -EsqX, -4.53f,
            "Mouse", "Arrastar", "Clique", "Encaixar ou devolver", "Esc", "Pausa");

        // A caixa do Prof fica no canto de baixo, em cima da faixa e da barra:
        // as duas somem enquanto ele fala.
        var esconder = cenario.GetComponent<EsconderDuranteFala>() ?? cenario.gameObject.AddComponent<EsconderDuranteFala>();
        var se = new SerializedObject(esconder);
        var alvos = se.FindProperty("alvos");
        alvos.arraySize = 2;
        alvos.GetArrayElementAtIndex(0).objectReferenceValue = como.gameObject;
        alvos.GetArrayElementAtIndex(1).objectReferenceValue = cenario.Find("BarraTeclas").gameObject;
        se.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        Debug.Log("[OficinaTextos] Textos, barra de teclas, faixa Como jogar e monitor aplicados; cena salva.");
    }

    private static void Tamanho(Transform cenario, string caminho, float px)
    {
        Transform t = cenario.Find(caminho);
        if (t == null || !(t.GetComponent<TMP_Text>() is TMP_Text texto))
        {
            Debug.LogWarning("[OficinaTextos] Texto não encontrado: " + caminho);
            return;
        }
        texto.fontSize = px / 10.8f;
        EditorUtility.SetDirty(texto);
    }

    // Painel = grupo com Fundo e Borda fatiados (do OficinaAnimacaoBuilder).
    private static void Painel(Transform grupo, Vector2 centro, Vector2 tamanho)
    {
        grupo.localPosition = new Vector3(centro.x, centro.y, grupo.localPosition.z);
        foreach (var sr in grupo.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.size = tamanho;
            EditorUtility.SetDirty(sr);
        }
    }

    /// <summary>Teclas desenhadas (JetBrains 22 px num quadro arredondado) + ação (Nunito 26 px), da direita pra esquerda.</summary>
    private static void BarraTeclas(Transform pai, Sprite sFundo, TMP_FontAsset fTexto, TMP_FontAsset fMono, float direita, float y, params string[] teclaAcao)
    {
        var raiz = new GameObject("BarraTeclas").transform;
        raiz.SetParent(pai, false);
        float x = direita - 18f / PxPorUnidade;
        for (int i = teclaAcao.Length - 2; i >= 0; i -= 2)
        {
            var grupo = new GameObject("Tecla_" + teclaAcao[i]).transform;
            grupo.SetParent(raiz, false);
            grupo.SetAsFirstSibling();

            TMP_Text acao = NovoTexto(grupo, "Acao", teclaAcao[i + 1], fTexto, 26f, Suave, 1f, 7);
            Posicionar(acao, new Vector2(x, y));
            x -= acao.GetPreferredValues(teclaAcao[i + 1]).x + 12f / PxPorUnidade;

            TMP_Text k = NovoTexto(grupo, "Tecla", teclaAcao[i], fMono, 22f, Texto, 0.5f, 8);
            float w = k.GetPreferredValues(teclaAcao[i]).x + 24f / PxPorUnidade;
            float cx = x - w / 2f;
            Posicionar(k, new Vector2(cx, y + 0.01f));

            var tam = new Vector2(w, 0.40f);
            SpriteRenderer contorno = Fatiado(grupo, "Contorno", sFundo, tam, Contorno, 5);
            contorno.transform.localPosition = new Vector3(cx, y, 0f);
            Fatiado(contorno.transform, "Fundo", sFundo, tam - Vector2.one * (6f / PxPorUnidade), Elevado, 6);
            x -= w + 30f / PxPorUnidade;
        }
    }

    // Sem RectTransform no pai, o que vai pra cena salva é o anchoredPosition (ver OficinaAnimacaoBuilder.NovoTexto).
    private static void Posicionar(TMP_Text t, Vector2 pos)
    {
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.localPosition = new Vector3(pos.x, pos.y, 0f);
    }

    private static SpriteRenderer Fatiado(Transform pai, string nome, Sprite sprite, Vector2 tamanho, Color cor, int ordem)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = tamanho;
        sr.color = cor;
        sr.sortingOrder = ordem;
        return sr;
    }

    private static TMP_Text NovoTexto(Transform pai, string nome, string texto, TMP_FontAsset fonte, float px, Color cor, float pivoX, int ordem)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        var t = go.AddComponent<TextMeshPro>();
        t.font = fonte;
        t.fontSize = px / 10.8f;
        t.text = texto;
        t.color = cor;
        t.alignment = pivoX > 0.9f ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.Midline;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.sortingOrder = ordem;
        t.rectTransform.sizeDelta = new Vector2(6f, 0.6f);
        t.rectTransform.pivot = new Vector2(pivoX, 0.5f);
        return t;
    }
}
