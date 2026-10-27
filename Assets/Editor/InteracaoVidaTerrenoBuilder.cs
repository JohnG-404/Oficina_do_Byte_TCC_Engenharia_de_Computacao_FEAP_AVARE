using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Reformula o terreno da "Interacao_Vida" em 3 salas (Dano, Cura, Risco),
/// no estilo das outras fases: massa sólida de tiles Tecno em volta das
/// salas, paredes que descem do teto entre elas, fundo lab_background_v3 e
/// uma lâmpada por sala.
///
/// Também reposiciona os objetos que já existem (mantendo nomes e
/// InteractableItem): os 2 lasers viram barreiras em pé (LaserBarreira), os
/// espinhos viram fileiras contínuas (EspinhosContinuos), as plataformas
/// falsas ganham o visual por estado (PlataformaFalsaVisual), e a câmera
/// passa a seguir na vertical, presa à área das salas.
///
/// Uso: Tools > Interação > Reformular Terreno (Vida), com Interacao_Vida
/// aberta. Idempotente — pode rodar de novo.
/// </summary>
public static class InteracaoVidaTerrenoBuilder
{
    private const string Pasta = "Assets/Images/InteracaoVida/";
    private const string Tecno = "Assets/Images/Packs/Tecno/";
    private const string FundoPath = "Assets/Images/Interação/lab_background_v3.png";
    private const string LampadaPath = "Assets/Anim/FaseAnimacao/Lampada/lampada_fase_01.png";
    private const string Camada = "Game";

    // Grade do terreno: células de 0,4 x 0,8 (um tile Tecno), com o canto
    // inferior esquerdo em (-2, -4). O chão das salas fica em y = 0.
    private const float X0 = -2f, Y0 = -4f, W = 0.4f, H = 0.8f;
    private const int Colunas = 100, Linhas = 18;

    private const int OrdemFundo = -3, OrdemBrilho = -2, OrdemTiles = 0, OrdemLampada = 1;

    [MenuItem("Tools/Interação/Reformular Terreno (Vida)")]
    public static void Menu()
    {
        Construir(SceneManager.GetActiveScene());
    }

    public static void Construir(Scene cena)
    {
        ConfigurarSprites();

        GameObject cenario = Raiz(cena, "Cenario");
        if (cenario == null)
        {
            Debug.LogError("[InteracaoVidaTerrenoBuilder] 'Cenario' não encontrado na cena.");
            return;
        }
        Transform traps = cenario.transform.Find("Traps");

        foreach (string velho in new[] { "Ground", "Backgrounds", "Terreno", "Fundo", "Lampadas" })
        {
            Transform t = cenario.transform.Find(velho);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        bool[,] solido = Grade();
        Camera cam = Raiz(cena, "Main Camera")?.GetComponent<Camera>();
        float meiaAltura = cam != null ? cam.orthographicSize : 5f;

        ConstruirFundo(cenario.transform);
        ConstruirTerreno(cenario.transform, solido, 2.2f - meiaAltura - H, 3.4f + meiaAltura + H);
        ConstruirLampadas(cenario.transform);
        foreach (string linha in LampadasVidaBuilder.Montar(cena)) Debug.Log("[LampadasVidaBuilder] " + linha);

        if (traps != null)
        {
            Lasers(traps);
            Espinhos(traps);
            PlataformasFalsas(traps);
            ApoiarNoChao(traps.Find("plus_red_heal"), 15.0f, 1.75f);
            ApoiarNoChao(traps.Find("plus_black_debuff"), 28.0f, 4.15f);
        }

        GameObject porta = Raiz(cena, "PortaSaida");
        if (porta != null)
        {
            var visual = porta.GetComponentInChildren<SpriteRenderer>();
            Vector3 p = porta.transform.position;
            if (visual != null)
            {
                p.x += 34.6f - visual.bounds.center.x;
                p.y += 0.8f - visual.bounds.min.y;
            }
            porta.transform.position = p;
        }

        GameObject player = Raiz(cena, "Player");
        if (player != null) player.transform.position = new Vector3(1.2f, 0.6f, 0f);

        if (cam != null)
        {
            var follow = cam.GetComponent<CameraFollow>();
            if (follow != null)
            {
                var so = new SerializedObject(follow);
                so.FindProperty("followVertical").boolValue = true;
                so.FindProperty("useBounds").boolValue = true;
                so.FindProperty("minPosition").vector2Value = new Vector2(7.7f, 2.2f);
                so.FindProperty("maxPosition").vector2Value = new Vector2(28.3f, 3.4f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            cam.transform.position = new Vector3(7.7f, 2.2f, cam.transform.position.z);
        }

        EditorSceneManager.MarkSceneDirty(cena);
        Debug.Log("[InteracaoVidaTerrenoBuilder] Terreno e objetos da Interacao_Vida reformulados. Confira e salve (Ctrl+S).");
    }

    // ------------------------------------------------------------------
    // Sprites

    private static void ConfigurarSprites()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Pasta.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;

            Vector4 borda = path.EndsWith("espinhos_continuo.png") ? new Vector4(20f, 0f, 20f, 0f) : Vector4.zero;
            var st = new TextureImporterSettings();
            imp.ReadTextureSettings(st);
            bool ok = imp.textureType == TextureImporterType.Sprite
                && imp.spriteImportMode == SpriteImportMode.Single
                && Mathf.Approximately(imp.spritePixelsPerUnit, 100f)
                && st.spriteMeshType == SpriteMeshType.FullRect
                && imp.spriteBorder == borda
                && !imp.mipmapEnabled;
            if (ok) continue;

            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 100f;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect;
            st.spriteAlignment = (int)SpriteAlignment.Center;
            imp.SetTextureSettings(st);
            imp.spriteBorder = borda;
            imp.SaveAndReimport();
        }
    }

    // ------------------------------------------------------------------
    // Terreno

    private static bool[,] Grade()
    {
        var solido = new bool[Colunas, Linhas];
        for (int c = 0; c < Colunas; c++)
            for (int r = 0; r < Linhas; r++)
                solido[c, r] = true;

        // Sala 1 · Dano (teto 6,4), com uma lombada baixa.
        Cavar(solido, 0f, 0f, 10f, 6.4f);
        Cavar(solido, 2.0f, 0f, 3.6f, 0.8f, true);
        // Passagem 1 → 2 (parede descendo do teto até 4,0).
        Cavar(solido, 9.9f, 0f, 10.9f, 4.0f);
        // Sala 2 · Cura (teto 7,2): degraus com a Vida no alto e um fosso de espinhos.
        Cavar(solido, 10.8f, 0f, 21.2f, 7.2f);
        Cavar(solido, 12.8f, 0f, 16.0f, 0.8f, true);
        Cavar(solido, 14.0f, 0.7f, 16.0f, 1.6f, true);
        Cavar(solido, 17.2f, -0.8f, 19.6f, 0.1f);
        // Passagem 2 → 3 (a barreira 2 fica aqui, 4,8 de altura).
        Cavar(solido, 21.1f, 0f, 22.1f, 4.8f);
        // Sala 3 · Risco (teto 8): poço largo e patamar da porta.
        Cavar(solido, 22.0f, 0f, 36.0f, 8.0f);
        Cavar(solido, 23.6f, -2.4f, 31.2f, 0.1f);
        Cavar(solido, 31.2f, 0f, 36.0f, 0.8f, true);
        return solido;
    }

    private static void Cavar(bool[,] solido, float x0, float y0, float x1, float y1, bool valor = false)
    {
        for (int c = 0; c < Colunas; c++)
        {
            for (int r = 0; r < Linhas; r++)
            {
                float cx = X0 + W * c + W * 0.5f, cy = Y0 + H * r + H * 0.5f;
                if (x0 < cx && cx < x1 && y0 < cy && cy < y1) solido[c, r] = valor;
            }
        }
    }

    private static bool S(bool[,] solido, int c, int r)
    {
        if (c < 0 || r < 0 || c >= Colunas || r >= Linhas) return true;
        return solido[c, r];
    }

    // Peça de tile pelo ar em volta (as mesmas regras do mockup).
    private static string Peca(bool[,] s, int c, int r)
    {
        bool cima = !S(s, c, r + 1), baixo = !S(s, c, r - 1), esq = !S(s, c - 1, r), dir = !S(s, c + 1, r);
        if (cima && esq) return "plat_left_T";
        if (cima && dir) return "plat_right_T";
        if (baixo && dir) return "plat_left_B";
        if (baixo && esq) return "plat_right_B";
        if (cima) return "plat_mid_T";
        if (baixo) return "plat_mid_B_none";
        if (esq) return "plat_left_M";
        if (dir) return "plat_right_M";
        if (!S(s, c + 1, r + 1)) return "platform_canto_direita";
        if (!S(s, c - 1, r + 1)) return "platform_canto_esquerda";
        if (!S(s, c - 1, r - 1)) return "platform_canto_direita_cima";
        if (!S(s, c + 1, r - 1)) return "platform_canto_esquerda_cima";
        return "plat_center";
    }

    private static void ConstruirTerreno(Transform pai, bool[,] solido, float yMinVisivel, float yMaxVisivel)
    {
        var terreno = new GameObject("Terreno");
        terreno.transform.SetParent(pai, false);
        terreno.transform.position = Vector3.zero;

        var tiles = new GameObject("Tiles");
        tiles.transform.SetParent(terreno.transform, false);

        int criados = 0;
        for (int c = 0; c < Colunas; c++)
        {
            for (int r = 0; r < Linhas; r++)
            {
                if (!solido[c, r]) continue;
                float y = Y0 + H * r;
                // Só desenha o que a câmera pode mostrar (a área sólida é
                // grande e o resto nunca aparece).
                if (y + H < yMinVisivel || y > yMaxVisivel) continue;

                string peca = Peca(solido, c, r);
                Sprite s = Carregar(Tecno + peca + ".png");
                if (s == null) continue;
                var go = new GameObject(peca);
                go.transform.SetParent(tiles.transform, false);
                go.transform.position = new Vector3(X0 + W * c + W * 0.5f, y + H * 0.5f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = s;
                sr.sortingLayerName = Camada;
                sr.sortingOrder = OrdemTiles;
                criados++;
            }
        }

        // Colisão: um BoxCollider2D por trecho contínuo de cada linha, todos
        // mesclados num CompositeCollider2D (sem emendas pra o jogador
        // enganchar).
        var rb = terreno.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        var comp = terreno.AddComponent<CompositeCollider2D>();
        comp.geometryType = CompositeCollider2D.GeometryType.Polygons;
        comp.generationType = CompositeCollider2D.GenerationType.Synchronous;

        int caixas = 0;
        for (int r = 0; r < Linhas; r++)
        {
            int c = 0;
            while (c < Colunas)
            {
                if (!solido[c, r]) { c++; continue; }
                int inicio = c;
                while (c < Colunas && solido[c, r]) c++;
                var box = terreno.AddComponent<BoxCollider2D>();
                box.size = new Vector2((c - inicio) * W, H);
                box.offset = new Vector2(X0 + W * (inicio + c) * 0.5f, Y0 + H * r + H * 0.5f);
                box.compositeOperation = Collider2D.CompositeOperation.Merge;
                caixas++;
            }
        }
        comp.GenerateGeometry();
        Debug.Log($"[InteracaoVidaTerrenoBuilder] {criados} tiles, {caixas} caixas de colisão mescladas.");
    }

    private static void ConstruirFundo(Transform pai)
    {
        Sprite fundo = Carregar(FundoPath);
        if (fundo == null) return;
        var grupo = new GameObject("Fundo");
        grupo.transform.SetParent(pai, false);
        grupo.transform.position = Vector3.zero;

        foreach (float x in new[] { 9.2f, 28.4f })
        {
            Sprite_(grupo.transform, "BG", fundo, new Vector2(x, 3.7f), OrdemFundo);
        }
        // Embaixo do poço grande: uma cópia espelhada, pra o fundo continuar
        // sem emenda abaixo da borda do primeiro.
        var baixo = Sprite_(grupo.transform, "BG_poco", fundo, new Vector2(28.4f, 3.7f - fundo.bounds.size.y), OrdemFundo);
        baixo.flipY = true;
    }

    private static void ConstruirLampadas(Transform pai)
    {
        Sprite lampada = Carregar(LampadaPath);
        Sprite brilho = Carregar(Pasta + "lampada_brilho.png");
        var grupo = new GameObject("Lampadas");
        grupo.transform.SetParent(pai, false);
        grupo.transform.position = Vector3.zero;

        var tetos = new[] { new Vector2(5f, 6.4f), new Vector2(16f, 7.2f), new Vector2(27.4f, 8f), new Vector2(33.6f, 8f) };
        foreach (Vector2 teto in tetos)
        {
            if (brilho != null)
            {
                Sprite_(grupo.transform, "Brilho", brilho, new Vector2(teto.x, teto.y - 2.4f), OrdemBrilho);
            }
            if (lampada != null)
            {
                // O sprite da lâmpada está de cabeça pra baixo pra quem
                // pendura no teto: espelha e encosta o topo visível no teto.
                var sr = Sprite_(grupo.transform, "Lampada", lampada, Vector2.zero, OrdemLampada);
                sr.flipY = true;
                sr.transform.localScale = Vector3.one * 1.1f;
                Bounds b = sr.bounds;
                sr.transform.position = new Vector3(teto.x, teto.y - b.extents.y, 0f);
            }
        }
    }

    // ------------------------------------------------------------------
    // Objetos

    private static void Lasers(Transform traps)
    {
        var barreiras = new[]
        {
            ("trap_laser_01", new Vector2(6.4f, 0f), 6.4f),
            ("trap_laser_01 (1)", new Vector2(21.6f, 0f), 4.8f),
        };
        foreach (var (nome, pos, distancia) in barreiras)
        {
            Transform t = traps.Find(nome);
            if (t == null) { Debug.LogWarning("[InteracaoVidaTerrenoBuilder] Laser não encontrado: " + nome); continue; }

            var velho = t.GetComponent<SpriteRenderer>();
            if (velho != null) Object.DestroyImmediate(velho);
            var anim = t.GetComponent<Animator>();
            if (anim != null) Object.DestroyImmediate(anim);

            t.position = new Vector3(pos.x, pos.y, 0f);
            t.rotation = Quaternion.identity;
            t.localScale = Vector3.one;

            var col = Comp<BoxCollider2D>(t);
            col.edgeRadius = 0f;
            col.isTrigger = false;

            var laser = Comp<LaserBarreira>(t);
            var so = new SerializedObject(laser);
            so.FindProperty("distancia").floatValue = distancia;
            so.FindProperty("orientacao").enumValueIndex = (int)LaserBarreira.Orientacao.Vertical;
            so.FindProperty("spriteBase").objectReferenceValue = Carregar(Pasta + "laser_base.png");
            so.FindProperty("spriteLed").objectReferenceValue = Carregar(Pasta + "laser_led.png");
            so.FindProperty("feixeContinuo").objectReferenceValue = Carregar(Pasta + "laser_feixe.png");
            so.FindProperty("feixeTracejado").objectReferenceValue = Carregar(Pasta + "laser_feixe_tracejado.png");
            so.ApplyModifiedPropertiesWithoutUndo();
            laser.Montar();
            NaCamada(t);
        }
    }

    private static void Espinhos(Transform traps)
    {
        var fileiras = new[]
        {
            ("trap_spikes_01", new Vector2(18.4f, -0.44f), 2.4f),
            ("trap_spikes_02", new Vector2(27.4f, -2.04f), 7.6f),
        };
        Sprite sprite = Carregar(Pasta + "espinhos_continuo.png");
        foreach (var (nome, pos, largura) in fileiras)
        {
            Transform t = traps.Find(nome);
            if (t == null) { Debug.LogWarning("[InteracaoVidaTerrenoBuilder] Espinhos não encontrados: " + nome); continue; }

            t.position = new Vector3(pos.x, pos.y, 0f);
            t.rotation = Quaternion.identity;
            t.localScale = Vector3.one;
            var sr = t.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = Camada;
            sr.sortingOrder = 2;

            var esp = Comp<EspinhosContinuos>(t);
            var so = new SerializedObject(esp);
            so.FindProperty("largura").floatValue = largura;
            so.ApplyModifiedPropertiesWithoutUndo();
            esp.Montar();
            // Trocar o sprite pode reescalar o objeto pela diferença de
            // tamanho do sprite antigo; a largura vem do Tiled, não da escala.
            t.localScale = Vector3.one;
        }
    }

    private static void PlataformasFalsas(Transform traps)
    {
        // (x do centro, topo) — rota baixa, rota alta e a ponte da sala 2.
        var posicoes = new[]
        {
            new Vector2(24.8f, 0.9f), new Vector2(27.2f, 1.7f), new Vector2(29.6f, 0.9f),
            new Vector2(25.6f, 3.3f), new Vector2(28.0f, 4.1f), new Vector2(30.4f, 3.3f),
            new Vector2(18.4f, 1.6f),
        };
        var plataformas = traps.Cast<Transform>()
            .Where(t => t.name.StartsWith("fake_floor"))
            .OrderBy(t => t.name)
            .ToList();

        Sprite neutra = Carregar(Pasta + "plat_neutra.png");
        for (int i = 0; i < plataformas.Count && i < posicoes.Length; i++)
        {
            Transform t = plataformas[i];
            var sr = t.GetComponent<SpriteRenderer>();
            sr.sprite = neutra;
            sr.sortingLayerName = Camada;
            float meiaAltura = neutra != null ? neutra.bounds.extents.y * t.lossyScale.y : 0.2625f;
            t.position = new Vector3(posicoes[i].x, posicoes[i].y - meiaAltura, 0f);

            var vis = Comp<PlataformaFalsaVisual>(t);
            var so = new SerializedObject(vis);
            so.FindProperty("neutra").objectReferenceValue = neutra;
            so.FindProperty("quebrar").objectReferenceValue = Carregar(Pasta + "plat_quebrar.png");
            so.FindProperty("mover").objectReferenceValue = Carregar(Pasta + "plat_mover.png");
            so.FindProperty("pistoes").objectReferenceValue = Carregar(Pasta + "plat_pistoes.png");
            so.FindProperty("metadeEsquerda").objectReferenceValue = Carregar(Pasta + "plat_quebrar_esq.png");
            so.FindProperty("metadeDireita").objectReferenceValue = Carregar(Pasta + "plat_quebrar_dir.png");
            so.FindProperty("contorno").objectReferenceValue = Carregar(Pasta + "plat_contorno.png");
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        if (plataformas.Count != posicoes.Length)
        {
            Debug.LogWarning($"[InteracaoVidaTerrenoBuilder] {plataformas.Count} plataformas falsas na cena, {posicoes.Length} posições previstas.");
        }
    }

    // Põe o item com o centro em x e a borda de baixo visível em yBase.
    private static void ApoiarNoChao(Transform t, float x, float yBase)
    {
        if (t == null) return;
        var sr = t.GetComponent<SpriteRenderer>();
        Vector3 p = t.position;
        if (sr != null)
        {
            p.x += x - sr.bounds.center.x;
            p.y += yBase - sr.bounds.min.y;
        }
        t.position = p;
    }

    // ------------------------------------------------------------------

    private static void NaCamada(Transform raiz)
    {
        foreach (var sr in raiz.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.sortingLayerName = Camada;
        }
    }

    // GetComponent devolve um "null falso" no Editor, então nada de "??".
    private static T Comp<T>(Transform t) where T : Component
    {
        T c = t.GetComponent<T>();
        return c != null ? c : t.gameObject.AddComponent<T>();
    }

    private static GameObject Raiz(Scene cena, string nome)
    {
        return cena.GetRootGameObjects().FirstOrDefault(g => g.name == nome);
    }

    private static SpriteRenderer Sprite_(Transform pai, string nome, Sprite sprite, Vector2 posicao, int ordem)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        go.transform.position = new Vector3(posicao.x, posicao.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = Camada;
        sr.sortingOrder = ordem;
        return sr;
    }

    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    private static Sprite Carregar(string path)
    {
        if (cache.TryGetValue(path, out Sprite s) && s != null) return s;
        s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null)
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is Sprite sub) { s = sub; break; }
            }
        }
        if (s == null) Debug.LogWarning("[InteracaoVidaTerrenoBuilder] Sprite não encontrado: " + path);
        cache[path] = s;
        return s;
    }
}
