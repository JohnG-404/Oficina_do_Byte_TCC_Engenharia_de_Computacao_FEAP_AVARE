using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cartão de dica amarelo (opção A aprovada em 29/10) nos puzzles salvos na
/// cena: troca o painel "Dica" de cada InfoPanelUI das cenas Fisica e IA
/// pelo CartaoDica (fundo escuro, borda e brilho na cor do modo, círculo com
/// a lâmpada, rótulo "DICA", etiqueta do lugar e texto grande) e pinta de
/// âmbar o contorno da lacuna da vez, para combinar com a etiqueta.
///
/// Também acerta a importação dos sprites de Assets/Art/UI/Dica (9-slice) e
/// liga esses sprites no TemaLaboratorio, que o CartaoDicaUI usa nos puzzles
/// montados em runtime.
///
/// Tudo é salvo na cena. Rodar de novo refaz o cartão do zero (idempotente).
/// Se o FisicaCodigoBuilder for rodado depois, rode este de novo.
///
/// Tools > Dicas > Aplicar cartão de dica
/// </summary>
public static class CartaoDicaBuilder
{
    private const string Pasta = "Assets/Art/UI/Dica/";
    private static readonly string[] Cenas = { "Assets/Scenes/Fisica.unity", "Assets/Scenes/IA.unity" };
    private const string Prefixo = "Cartao_";

    private static List<string> log;

    [MenuItem("Tools/Dicas/Aplicar cartão de dica")]
    public static void Menu()
    {
        Aplicar();
        foreach (string l in log) Debug.Log("[CartaoDicaBuilder] " + l);
    }

    public static List<string> Aplicar()
    {
        log = new List<string>();
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            log.Add("ERRO: saia do Play Mode antes.");
            return log;
        }

        Sprite fundo = Importar("dica_fundo", 32);
        Sprite borda = Importar("dica_borda", 32);
        Sprite brilho = Importar("dica_brilho", 64);
        Sprite circulo = Importar("dica_circulo", 0);
        Sprite anel = Importar("dica_anel", 0);

        TemaLaboratorio tema = TemaLaboratorio.Atual;
        if (tema != null)
        {
            tema.dicaFundo = fundo;
            tema.dicaBorda = borda;
            tema.dicaBrilho = brilho;
            tema.dicaCirculo = circulo;
            tema.dicaAnel = anel;
            EditorUtility.SetDirty(tema);
            AssetDatabase.SaveAssets();
            log.Add("TemaLaboratorio: sprites do cartão ligados.");
        }
        else
        {
            log.Add("AVISO: Resources/TemaLaboratorio não encontrado; os puzzles UGUI ficam com o painel antigo.");
        }

        foreach (string caminho in Cenas)
        {
            Scene cena = SceneManager.GetSceneByPath(caminho);
            bool abriAqui = false;
            if (!cena.isLoaded)
            {
                cena = EditorSceneManager.OpenScene(caminho, OpenSceneMode.Additive);
                abriAqui = true;
            }
            else if (cena.isDirty)
            {
                log.Add($"ERRO: {caminho} tem mudanças não salvas. Salve ou descarte antes de rodar.");
                continue;
            }

            int n = 0;
            foreach (GameObject raiz in cena.GetRootGameObjects())
            {
                foreach (InfoPanelUI painel in raiz.GetComponentsInChildren<InfoPanelUI>(true))
                {
                    if (Montar(painel, tema, fundo, borda, brilho, circulo, anel)) n++;
                }
                foreach (CodigoEncaixeUI codigo in raiz.GetComponentsInChildren<CodigoEncaixeUI>(true))
                {
                    PintarDestaques(codigo);
                }
            }

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            log.Add($"{caminho}: {n} cartão(ões) montado(s).");
            if (abriAqui) EditorSceneManager.CloseScene(cena, true);
        }

        return log;
    }

    private static Sprite Importar(string nome, int bordaPx)
    {
        string caminho = Pasta + nome + ".png";
        var imp = AssetImporter.GetAtPath(caminho) as TextureImporter;
        if (imp == null)
        {
            log.Add("ERRO: não achei " + caminho);
            return null;
        }

        var cfg = new TextureImporterSettings();
        imp.ReadTextureSettings(cfg);
        cfg.textureType = TextureImporterType.Sprite;
        cfg.spriteMode = (int)SpriteImportMode.Single;
        cfg.spritePixelsPerUnit = 100f;
        cfg.spriteMeshType = SpriteMeshType.FullRect;
        cfg.spriteBorder = new Vector4(bordaPx, bordaPx, bordaPx, bordaPx);
        cfg.mipmapEnabled = false;
        cfg.filterMode = FilterMode.Bilinear;
        cfg.wrapMode = TextureWrapMode.Clamp;
        cfg.alphaIsTransparency = true;
        imp.SetTextureSettings(cfg);
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
    }

    private static bool Montar(InfoPanelUI painel, TemaLaboratorio tema, Sprite sFundo, Sprite sBorda, Sprite sBrilho, Sprite sCirculo, Sprite sAnel)
    {
        Transform p = painel.transform;
        var so = new SerializedObject(painel);
        var titulo = so.FindProperty("externalTitle").objectReferenceValue as TMP_Text;
        var texto = so.FindProperty("externalDescription").objectReferenceValue as TMP_Text;
        var icone = so.FindProperty("externalIcon").objectReferenceValue as SpriteRenderer;
        if (titulo == null || texto == null)
        {
            log.Add($"AVISO: {Caminho(p)} sem título/descrição ligados; pulei.");
            return false;
        }

        // Refaz do zero: apaga o que uma rodada anterior criou e reacende o
        // fundo e a aba antigos para medir.
        for (int i = p.childCount - 1; i >= 0; i--)
        {
            Transform c = p.GetChild(i);
            if (c.name.StartsWith(Prefixo)) Object.DestroyImmediate(c.gameObject);
        }

        SpriteRenderer fundoVelho = null;
        float maior = 0f;
        var outros = new List<SpriteRenderer>();
        foreach (Transform c in p)
        {
            var sr = c.GetComponent<SpriteRenderer>();
            if (sr == null || sr == icone) continue;
            c.gameObject.SetActive(true);
            outros.Add(sr);
            float area = sr.bounds.size.x * sr.bounds.size.y;
            if (area > maior)
            {
                maior = area;
                fundoVelho = sr;
            }
        }
        if (fundoVelho == null)
        {
            log.Add($"AVISO: {Caminho(p)} sem fundo de painel; pulei.");
            return false;
        }

        Vector3 a = p.InverseTransformPoint(fundoVelho.bounds.min);
        Vector3 b = p.InverseTransformPoint(fundoVelho.bounds.max);
        float xMin = a.x, xMax = b.x, yMin = a.y - 0.1f, yMax = b.y + 0.1f;
        float largura = xMax - xMin, altura = yMax - yMin;
        var centro = new Vector2((xMin + xMax) / 2f, (yMin + yMax) / 2f);

        int camada = fundoVelho.sortingLayerID;
        int ordem = fundoVelho.sortingOrder;

        // O fundo e a aba "Dica" antigos ficam desligados (o cartão tem o rótulo).
        foreach (SpriteRenderer sr in outros) sr.gameObject.SetActive(false);

        SpriteRenderer brilho = Fatia(p, "Brilho", sBrilho, centro, new Vector2(largura + 0.64f, altura + 0.64f), camada, ordem, Color.clear);
        Fatia(p, "Fundo", sFundo, centro, new Vector2(largura, altura), camada, ordem + 1, CartaoDica.Fundo);
        SpriteRenderer borda = Fatia(p, "Borda", sBorda, centro, new Vector2(largura, altura), camada, ordem + 2, CartaoDica.Ambar);

        const float diametro = 1.16f;
        var centroCirculo = new Vector2(xMin + 0.34f + diametro / 2f, centro.y);
        SpriteRenderer circulo = Simples(p, "Circulo", sCirculo, centroCirculo, diametro, camada, ordem + 3, CartaoDica.Ambar);
        SpriteRenderer anel = Simples(p, "Anel", sAnel, centroCirculo, diametro + 0.16f, camada, ordem + 3, Color.clear);

        Sprite iconeDica = null;
        if (icone != null)
        {
            iconeDica = icone.sprite;
            icone.gameObject.SetActive(true);
            icone.transform.localPosition = new Vector3(centroCirculo.x, centroCirculo.y, icone.transform.localPosition.z);
            float lado = icone.sprite != null ? icone.sprite.bounds.size.x : 1.28f;
            float escala = 0.66f / Mathf.Max(0.01f, lado);
            icone.transform.localScale = new Vector3(escala, escala, 1f);
            icone.sortingLayerID = camada;
            icone.sortingOrder = ordem + 4;
            icone.color = CartaoDica.IconeEscuro;
        }

        float xTexto = xMin + 0.34f + diametro + 0.36f;
        TMP_FontAsset display = tema != null && tema.display != null ? tema.display : titulo.font;
        TMP_FontAsset monoForte = tema != null && tema.monoForte != null ? tema.monoForte : titulo.font;
        TMP_FontAsset corpo = tema != null && tema.corpo != null ? tema.corpo : texto.font;
        TMP_FontAsset corpoForte = tema != null && tema.corpoForte != null ? tema.corpoForte : texto.font;

        float yRotulo = yMax - 0.36f;
        Linha(titulo, "DICA", new Vector2(xTexto, yRotulo), new Vector2(4f, 0.5f), display, 2.6f, CartaoDica.Ambar, camada, ordem + 5);
        titulo.characterSpacing = 8f;

        TMP_Text chip = NovoTexto(p, "Chip", new Vector2(xTexto + 2f, yRotulo), new Vector2(10f, 0.4f), monoForte, 2.0f, CartaoDica.Ambar, camada, ordem + 5);
        SpriteRenderer chipFundo = Fatia(p, "ChipFundo", sFundo, new Vector2(xTexto + 3f, yRotulo), new Vector2(2f, 0.72f), camada, ordem + 3, Color.clear);
        SpriteRenderer chipBorda = Fatia(p, "ChipBorda", sBorda, new Vector2(xTexto + 3f, yRotulo), new Vector2(2f, 0.72f), camada, ordem + 4, CartaoDica.Ambar);
        chipFundo.transform.localScale = chipBorda.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

        // Texto grande: até duas linhas, com tamanho automático.
        texto.font = corpoForte;
        texto.color = Color.white;
        texto.alignment = TextAlignmentOptions.TopLeft;
        texto.textWrappingMode = TextWrappingModes.Normal;
        texto.overflowMode = TextOverflowModes.Overflow;
        texto.enableAutoSizing = true;
        texto.fontSizeMin = 2.2f;
        texto.fontSizeMax = 3.3f;
        texto.rectTransform.pivot = new Vector2(0f, 1f);
        texto.rectTransform.sizeDelta = new Vector2(xMax - xTexto - 0.4f, altura - 0.66f - 0.42f);
        texto.transform.localPosition = new Vector3(xTexto, yMax - 0.66f, texto.transform.localPosition.z);
        texto.transform.localScale = Vector3.one;
        Ordenar(texto, camada, ordem + 5);

        TMP_Text rodape = NovoTexto(p, "Rodape", new Vector2(xTexto, yMin + 0.26f), new Vector2(14f, 0.3f), corpo, 1.7f, CartaoDica.Apagado, camada, ordem + 5);
        rodape.text = CartaoDica.RodapePadrao;

        CartaoDica cartao = p.GetComponent<CartaoDica>();
        if (cartao == null) cartao = p.gameObject.AddComponent<CartaoDica>();
        var sc = new SerializedObject(cartao);
        sc.FindProperty("brilho").objectReferenceValue = brilho;
        sc.FindProperty("borda").objectReferenceValue = borda;
        sc.FindProperty("circulo").objectReferenceValue = circulo;
        sc.FindProperty("anel").objectReferenceValue = anel;
        sc.FindProperty("icone").objectReferenceValue = icone;
        sc.FindProperty("chipFundo").objectReferenceValue = chipFundo;
        sc.FindProperty("chipBorda").objectReferenceValue = chipBorda;
        sc.FindProperty("rotulo").objectReferenceValue = titulo;
        sc.FindProperty("chip").objectReferenceValue = chip;
        sc.FindProperty("texto").objectReferenceValue = texto;
        sc.FindProperty("rodape").objectReferenceValue = rodape;
        sc.FindProperty("iconeDica").objectReferenceValue = iconeDica;
        sc.FindProperty("escalaAnel").floatValue = (diametro + 0.16f) / Mathf.Max(0.01f, sAnel != null ? sAnel.bounds.size.x : 2.56f);
        sc.ApplyModifiedPropertiesWithoutUndo();

        so.FindProperty("cartao").objectReferenceValue = cartao;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Estado inicial salvo (o mesmo que o InfoPanelUI mostra ao abrir).
        string dica = so.FindProperty("defaultDescription").stringValue;
        CartaoDica.Separar(dica, out string local, out string corpoDica);
        string tituloPadrao = so.FindProperty("defaultTitle").stringValue;
        if (string.IsNullOrEmpty(local) && tituloPadrao != "Dica") local = tituloPadrao;
        cartao.Mostrar(CartaoDica.Modo.Dica, local, corpoDica);
        EditorUtility.SetDirty(cartao);
        return true;
    }

    // Contorno da lacuna da vez em âmbar, igual à etiqueta do cartão.
    private static void PintarDestaques(CodigoEncaixeUI codigo)
    {
        var so = new SerializedObject(codigo);
        SerializedProperty lacunas = so.FindProperty("lacunas");
        for (int i = 0; i < lacunas.arraySize; i++)
        {
            var destaque = lacunas.GetArrayElementAtIndex(i).FindPropertyRelative("destaque").objectReferenceValue as GameObject;
            if (destaque == null) continue;
            foreach (SpriteRenderer sr in destaque.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Color c = CartaoDica.Ambar;
                c.a = sr.color.a;
                sr.color = c;
                EditorUtility.SetDirty(sr);
            }
        }
    }

    private static SpriteRenderer Fatia(Transform p, string nome, Sprite s, Vector2 centro, Vector2 tamanho, int camada, int ordem, Color cor)
    {
        SpriteRenderer sr = Simples(p, nome, s, centro, 0f, camada, ordem, cor);
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = tamanho;
        sr.transform.localScale = Vector3.one;
        return sr;
    }

    private static SpriteRenderer Simples(Transform p, string nome, Sprite s, Vector2 centro, float diametro, int camada, int ordem, Color cor)
    {
        var go = new GameObject(Prefixo + nome);
        go.transform.SetParent(p, false);
        go.transform.localPosition = new Vector3(centro.x, centro.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.color = cor;
        sr.sortingLayerID = camada;
        sr.sortingOrder = ordem;
        if (diametro > 0f && s != null)
        {
            float e = diametro / s.bounds.size.x;
            go.transform.localScale = new Vector3(e, e, 1f);
        }
        return sr;
    }

    private static TMP_Text NovoTexto(Transform p, string nome, Vector2 pos, Vector2 tamanho, TMP_FontAsset fonte, float corpo, Color cor, int camada, int ordem)
    {
        var go = new GameObject(Prefixo + nome, typeof(RectTransform));
        go.transform.SetParent(p, false);
        var t = go.AddComponent<TextMeshPro>();
        Linha(t, "", pos, tamanho, fonte, corpo, cor, camada, ordem);
        return t;
    }

    private static void Linha(TMP_Text t, string texto, Vector2 pos, Vector2 tamanho, TMP_FontAsset fonte, float corpo, Color cor, int camada, int ordem)
    {
        t.text = texto;
        if (fonte != null) t.font = fonte;
        t.fontSize = corpo;
        t.enableAutoSizing = false;
        t.fontStyle = FontStyles.Normal;
        t.color = cor;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.rectTransform.pivot = new Vector2(0f, 0.5f);
        t.rectTransform.sizeDelta = tamanho;
        t.transform.localPosition = new Vector3(pos.x, pos.y, t.transform.localPosition.z);
        t.transform.localScale = Vector3.one;
        Ordenar(t, camada, ordem);
    }

    private static void Ordenar(TMP_Text t, int camada, int ordem)
    {
        var r = t.GetComponent<MeshRenderer>();
        if (r == null) return;
        r.sortingLayerID = camada;
        r.sortingOrder = ordem;
    }

    private static string Caminho(Transform t)
    {
        string s = t.name;
        for (Transform q = t.parent; q != null; q = q.parent) s = q.name + "/" + s;
        return s;
    }
}
