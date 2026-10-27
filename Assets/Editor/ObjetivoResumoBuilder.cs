using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Faixa de objetivo e cartão de fim de fase, salvos nas cenas:
/// - faixa de objetivo no topo central (ObjetivoFase) com os textos de
///   cada etapa da fase. Na IA_Aplicacao substitui a placa "Missão".
///   O Tutorial_Anim fica sem a faixa: a barra de rodadas do
///   OficinaAnimacaoHUD já mostra o objetivo.
/// - cartão "Fase Concluída!" no LevelCompleteUI (o que foi montado,
///   Tempo / Erros ou Reinícios / Tentativa, Próxima e [Enter] Continuar).
///   Física e Tutorial_Anim ganham o painel.
/// Idempotente (refaz a faixa e o cartão). Recusa em Play ou com a cena suja.
/// Backup de cada cena em SceneBackups/objetivo_resumo/.
/// </summary>
public static class ObjetivoResumoBuilder
{
    private const string PastaBackup = "SceneBackups/objetivo_resumo_4B_5A_2026-10-16";

    private class Dados
    {
        public string cena;
        public string[,] etapas;          // título, dica (null = sem faixa)
        public bool teclaReiniciar;
        // Borda esquerda das faixas (compacta e grande), em px de 1920x1080.
        // 0 = centralizadas. Na Aplicacao_Fisica começam depois do inventário (x 150 a 690).
        public float inicioX;
    }

    private static readonly Dados[] Cenas =
    {
        new Dados
        {
            cena = "Fisica",
            etapas = new[,]
            {
                { "Monte o código do Movimento", "Arraste cada peça pro espaço certo." },
                { "Ande com o personagem", "Setas ou A/D." },
                { "Monte o script da Câmera", "A câmera precisa ir atrás do personagem." },
                { "Ande mais um pouco", "Agora a câmera vai junto." },
                { "Monte o Rigidbody2D", "Ele flutua sem peso: falta gravidade." },
                { "Monte o Collider2D", "Ele atravessou o chão: falta um corpo sólido." },
                { "Teste andar com física e colisão", "Dê uma volta pela sala." },
                { "Monte o código do Pulo", "Personagem de plataforma precisa pular." },
                { "Pule com Espaço", "Só dá pra pular apoiado no chão." },
            },
        },
        new Dados
        {
            cena = "Aplicacao_Fisica",
            inicioX = 710f,
            etapas = new[,]
            {
                { "Arraste as mecânicas do inventário", "Solte cada ícone em quem vai recebê-lo." },
                { "Dê Movimento e Pulo ao seu personagem", "Movimento e Pulo só funcionam em você." },
                { "Chegue ao objetivo final", "Ficou sem saída? Segure {reiniciar} para recomeçar." },
            },
        },
        new Dados
        {
            cena = "Interacao",
            etapas = new[,]
            {
                { "Configure um objeto: Ação e Consequência", "Clique num objeto da sala." },
                { "Teste a combinação no objeto", "Vá até ele e faça a ação que você escolheu." },
                { "Abra o caminho até a saída", "Combine ações e consequências até liberar a passagem." },
                // A porta de saída da Interacao é o item Objetivo: só abre depois de configurada.
                { "Chegue à porta de saída", "Clique na porta e escolha Passar a fase." },
            },
        },
        new Dados
        {
            cena = "Interacao_Vida",
            etapas = new[,]
            {
                { "Configure um objeto: Ação e Consequência", "Clique num objeto da sala." },
                { "Teste a combinação no objeto", "Vá até ele e faça a ação que você escolheu." },
                { "Abra o caminho até a saída", "Cuidado: cada armadilha tira vida." },
                { "Chegue à porta de saída", "A porta abriu: siga em frente." },
            },
        },
        new Dados
        {
            cena = "Tutorial_Anim",
            etapas = null,
        },
        new Dados
        {
            cena = "Animacao",
            etapas = new[,]
            {
                { "Ande e pule pelo laboratório", "Repare no desenho do personagem." },
                { "Monte as transições do Animator", "Encaixe as condições nas setas." },
                { "Suba a escada e pule a vala de espinhos", "Agora ele anima sozinho." },
                { "Segure agachar perto do túnel", "Veja o corpo e o desenho." },
                { "Monte as setas do agachar no Animator", "O desenho precisa abaixar junto." },
                { "Atravesse o túnel até a porta", "Segure agachar pra passar." },
            },
        },
        new Dados
        {
            cena = "IA",
            etapas = new[,]
            {
                { "Monte os Estados do guarda", "Sem cérebro, ele fica parado." },
                { "Passe na frente do guarda", "Veja se ele percebe você." },
                { "Monte as Transições", "Falta a regra que diz quando trocar de estado." },
                { "Fique atrás da parede", "Veja se o guarda enxerga através dela." },
                { "Troque a regra 1 por Raycast", "Ele precisa ver você, não só estar perto." },
                { "Chegue à saída sem o guarda te ver", "A passarela bloqueia a linha de visão." },
            },
        },
        new Dados
        {
            cena = "IA_Aplicacao",
            teclaReiniciar = true,
            etapas = new[,]
            {
                { "Faça o aliado te seguir", "Clique no aliado para abrir o cérebro dele." },
                { "Faça o aliado distrair o guarda", "Pra distrair, ele precisa ver o guarda primeiro." },
                { "Faça o guarda perseguir o aliado", "Confira a tag no alvo do guarda." },
                { "Esconda-se e corra para a saída", "Suba na plataforma e espere a distração." },
            },
        },
    };

    [MenuItem("Tools/Fases/Faixa de objetivo e fim de fase")]
    public static void MenuTodas()
    {
        foreach (Dados d in Cenas)
        {
            foreach (string l in Aplicar(d.cena)) Debug.Log("[ObjetivoResumo] " + l);
        }
    }

    public static List<string> Aplicar(string nomeCena)
    {
        var log = new List<string>();
        Dados d = Cenas.FirstOrDefault(c => c.cena == nomeCena);
        if (d == null)
        {
            log.Add("ERRO: cena desconhecida " + nomeCena);
            return log;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            log.Add("ERRO: saia do Play Mode antes.");
            return log;
        }

        string path = "Assets/Scenes/" + nomeCena + ".unity";
        Scene cena = SceneManager.GetSceneByPath(path);
        if (cena.isLoaded && cena.isDirty)
        {
            log.Add($"ERRO: {nomeCena} tem mudanças não salvas (outra thread?). Nada feito.");
            return log;
        }

        Directory.CreateDirectory(PastaBackup);
        string backup = Path.Combine(PastaBackup, nomeCena + ".unity");
        if (!File.Exists(backup))
        {
            File.Copy(path, backup);
        }

        bool abriAqui = false;
        if (!cena.isLoaded)
        {
            cena = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            abriAqui = true;
        }

        try
        {
            if (d.etapas != null)
            {
                MontarFaixa(cena, d, log);
            }

            if (nomeCena == "IA_Aplicacao")
            {
                RemoverPlacaMissao(cena, log);
            }

            MontarCartao(cena, log);
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            log.Add(nomeCena + " salva.");
        }
        finally
        {
            if (abriAqui) EditorSceneManager.CloseScene(cena, true);
        }

        return log;
    }

    private static T Achar<T>(Scene cena) where T : Component
    {
        foreach (GameObject raiz in cena.GetRootGameObjects())
        {
            T c = raiz.GetComponentInChildren<T>(true);
            if (c != null) return c;
        }
        return null;
    }

    private static Canvas NovoCanvas(Scene cena, string nome, int ordem)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(go, cena);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = ordem;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        return canvas;
    }

    private static TextMeshProUGUI Txt(Transform p, string nome, string t, float tam, Color cor, TemaLabUI.Fonte f,
        TextAlignmentOptions al = TextAlignmentOptions.MidlineLeft, float minimo = 0f)
    {
        TextMeshProUGUI x = TemaLabUI.Texto(p, nome, t, tam, cor, f, al);
        x.textWrappingMode = TextWrappingModes.NoWrap;
        if (minimo > 0f)
        {
            x.enableAutoSizing = true;
            x.fontSizeMin = minimo;
            x.fontSizeMax = tam;
        }
        return x;
    }

    private static RectTransform Tecla(Transform p, string k, Vector2 anc, Vector2 pos, Vector2 tam)
    {
        RectTransform miolo = TemaLabUI.CaixaArredondada(p, "Tecla", TemaLabUI.Contorno, TemaLabUI.PainelElevado, 3f);
        RectTransform caixa = (RectTransform)miolo.parent;
        TemaLabUI.Posicionar(caixa, anc, pos, tam);
        TextMeshProUGUI t = Txt(miolo, "K", k, 22f, TemaLabUI.CorTexto, TemaLabUI.Fonte.MonoForte, TextAlignmentOptions.Center);
        TemaLabUI.Esticar(t.rectTransform);
        return caixa;
    }

    // ------------------------------------------------------------------ Faixa de objetivo

    // X da âncora (topo central) para a faixa começar em d.inicioX.
    private static float DeslocamentoX(Dados d, float largura)
    {
        return d.inicioX > 0f ? d.inicioX + largura / 2f - 960f : 0f;
    }

    private static void MontarFaixa(Scene cena, Dados d, List<string> log)
    {
        ObjetivoFase velha = Achar<ObjetivoFase>(cena);
        if (velha != null)
        {
            Canvas c = velha.GetComponentInParent<Canvas>(true);
            UnityEngine.Object.DestroyImmediate(c != null && c.name == "ObjetivoCanvas" ? c.gameObject : velha.gameObject);
        }

        TemaLaboratorio tema = TemaLabUI.Tema;
        Canvas canvas = NovoCanvas(cena, "ObjetivoCanvas", 35);
        RectTransform raiz = TemaLabUI.Esticar(TemaLabUI.Novo(canvas.transform, "FaixaObjetivo"));
        CanvasGroup grupo = raiz.gameObject.AddComponent<CanvasGroup>();
        grupo.blocksRaycasts = false;
        grupo.interactable = false;
        ObjetivoFase objetivo = raiz.gameObject.AddComponent<ObjetivoFase>();

        int n = d.etapas.GetLength(0);
        string titulo0 = d.etapas[0, 0];

        // Compacta: sempre visível.
        float largura = d.teclaReiniciar ? 980f : 720f;
        RectTransform miolo = TemaLabUI.CaixaArredondada(raiz, "Compacta", TemaLabUI.Contorno, new Color(0.106f, 0.141f, 0.22f, 0.92f), 3f);
        RectTransform compacta = (RectTransform)miolo.parent;
        TemaLabUI.Posicionar(compacta, new Vector2(0.5f, 1f), new Vector2(DeslocamentoX(d, largura), -22f), new Vector2(largura, 58f));
        CanvasGroup grupoCompacta = compacta.gameObject.AddComponent<CanvasGroup>();
        Image ponto = TemaLabUI.Imagem(miolo, "Ponto", tema.paginaPontoAtivo, TemaLabUI.Dourado);
        ponto.preserveAspect = true;
        TemaLabUI.Posicionar(ponto.rectTransform, new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(22f, 22f));
        float direita = 110f + (d.teclaReiniciar ? 330f : 0f);
        TextMeshProUGUI texto = Txt(miolo, "Texto", titulo0, 26f, TemaLabUI.CorTexto, TemaLabUI.Fonte.CorpoForte, TextAlignmentOptions.MidlineLeft, 18f);
        TemaLabUI.Posicionar(texto.rectTransform, new Vector2(0f, 0.5f), new Vector2(56f, 0f), new Vector2(largura - 56f - direita, 40f));
        TextMeshProUGUI contador = Txt(miolo, "Contador", "1/" + n, 22f, TemaLabUI.TextoSuave, TemaLabUI.Fonte.MonoForte, TextAlignmentOptions.MidlineRight);
        TemaLabUI.Posicionar(contador.rectTransform, new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(80f, 40f));
        if (d.teclaReiniciar)
        {
            Image barra = TemaLabUI.Imagem(miolo, "Divisor", null, TemaLabUI.Contorno);
            TemaLabUI.Posicionar(barra.rectTransform, new Vector2(1f, 0.5f), new Vector2(-112f, 0f), new Vector2(2f, 30f));
            // "Segure [tecla] recomeçar": a tecla acompanha a ação "Reiniciar fase" (aba Controles).
            TextMeshProUGUI segure = Txt(miolo, "Segure", "Segure", 22f, TemaLabUI.TextoSuave, TemaLabUI.Fonte.CorpoForte, TextAlignmentOptions.MidlineRight);
            TemaLabUI.Posicionar(segure.rectTransform, new Vector2(1f, 0.5f), new Vector2(-340f, 0f), new Vector2(90f, 40f));
            RectTransform caixaTecla = Tecla(miolo, "R", new Vector2(1f, 0.5f), new Vector2(-262f, 0f), new Vector2(68f, 40f));
            TextMeshProUGUI k = caixaTecla.GetComponentInChildren<TextMeshProUGUI>(true);
            k.enableAutoSizing = true;
            k.fontSizeMin = 12f;
            k.fontSizeMax = 22f;
            RotuloTeclaUI rotuloTecla = caixaTecla.gameObject.AddComponent<RotuloTeclaUI>();
            var st = new SerializedObject(rotuloTecla);
            st.FindProperty("texto").objectReferenceValue = k;
            st.FindProperty("acao").stringValue = InputManager.RestartActionName;
            st.FindProperty("padrao").stringValue = "R";
            st.ApplyModifiedPropertiesWithoutUndo();
            TextMeshProUGUI acao = Txt(miolo, "Recomecar", "recomeçar", 22f, TemaLabUI.TextoSuave, TemaLabUI.Fonte.CorpoForte);
            TemaLabUI.Posicionar(acao.rectTransform, new Vector2(1f, 0.5f), new Vector2(-126f, 0f), new Vector2(124f, 40f));
        }

        // Grande: aparece ao mudar de etapa (começa invisível também no Editor).
        Image grande = TemaLabUI.Imagem(raiz, "Grande", tema.painelPopup, TemaLabUI.Painel);
        TemaLabUI.Posicionar(grande.rectTransform, new Vector2(0.5f, 1f), new Vector2(DeslocamentoX(d, 1120f), -60f), new Vector2(1120f, 230f));
        CanvasGroup grupoGrande = grande.gameObject.AddComponent<CanvasGroup>();
        grupoGrande.alpha = 0f;
        TextMeshProUGUI rotulo = TemaLabUI.Rotulo(grande.transform, "Rotulo", "Novo objetivo · etapa 1 de " + n, 20f, TemaLabUI.Ciano);
        rotulo.alignment = TextAlignmentOptions.Center;
        TemaLabUI.Posicionar(rotulo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1000f, 30f));
        TextMeshProUGUI tituloGrande = Txt(grande.transform, "Titulo", titulo0, 56f, TemaLabUI.CorTexto, TemaLabUI.Fonte.Display, TextAlignmentOptions.Center, 34f);
        TemaLabUI.Posicionar(tituloGrande.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(1060f, 76f));
        TextMeshProUGUI dica = Txt(grande.transform, "Dica", d.etapas[0, 1], 26f, TemaLabUI.TextoSuave, TemaLabUI.Fonte.Corpo, TextAlignmentOptions.Center, 18f);
        TemaLabUI.Posicionar(dica.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -142f), new Vector2(1000f, 36f));
        RectTransform pontosPai = TemaLabUI.Novo(grande.transform, "Pontos");
        TemaLabUI.Posicionar(pontosPai, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(40f * n, 26f));
        var pontos = new Image[n];
        for (int i = 0; i < n; i++)
        {
            Image p = TemaLabUI.Imagem(pontosPai, "Ponto" + (i + 1), i == 0 ? tema.paginaPontoAtivo : tema.paginaPonto, TemaLabUI.Contorno);
            p.preserveAspect = true;
            TemaLabUI.Posicionar(p.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - (n - 1) / 2f) * 34f, 0f), new Vector2(18f, 18f));
            pontos[i] = p;
        }

        var so = new SerializedObject(objetivo);
        SerializedProperty etapas = so.FindProperty("etapas");
        etapas.arraySize = n;
        for (int i = 0; i < n; i++)
        {
            SerializedProperty e = etapas.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("titulo").stringValue = d.etapas[i, 0];
            e.FindPropertyRelative("dica").stringValue = d.etapas[i, 1];
        }
        so.FindProperty("grupo").objectReferenceValue = grupo;
        so.FindProperty("compacta").objectReferenceValue = grupoCompacta;
        so.FindProperty("textoCompacto").objectReferenceValue = texto;
        so.FindProperty("contador").objectReferenceValue = contador;
        so.FindProperty("grande").objectReferenceValue = grupoGrande;
        so.FindProperty("rotuloGrande").objectReferenceValue = rotulo;
        so.FindProperty("tituloGrande").objectReferenceValue = tituloGrande;
        so.FindProperty("dicaGrande").objectReferenceValue = dica;
        SerializedProperty sp = so.FindProperty("pontos");
        sp.arraySize = n;
        for (int i = 0; i < n; i++) sp.GetArrayElementAtIndex(i).objectReferenceValue = pontos[i];
        so.FindProperty("pontoFeito").objectReferenceValue = tema.seloCheck;
        so.FindProperty("pontoAtual").objectReferenceValue = tema.paginaPontoAtivo;
        so.FindProperty("pontoFalta").objectReferenceValue = tema.paginaPonto;
        so.ApplyModifiedPropertiesWithoutUndo();

        log.Add($"{d.cena}: faixa de objetivo com {n} etapas.");
    }

    private static void RemoverPlacaMissao(Scene cena, List<string> log)
    {
        foreach (GameObject raiz in cena.GetRootGameObjects())
        {
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "PlacaMissao")
                {
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
                    log.Add("IA_Aplicacao: placa Missão removida (a faixa substitui).");
                    return;
                }
            }
        }
    }

    // ------------------------------------------------------------------ Fim de fase

    private static void MontarCartao(Scene cena, List<string> log)
    {
        TemaLaboratorio tema = TemaLabUI.Tema;
        LevelCompleteUI ui = Achar<LevelCompleteUI>(cena);
        GameObject painel;
        if (ui == null)
        {
            Canvas canvas = NovoCanvas(cena, "FimDeFaseCanvas", 90);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            painel = TemaLabUI.Esticar(TemaLabUI.Novo(canvas.transform, "LevelCompletePanel")).gameObject;
            painel.AddComponent<Image>();
            ui = painel.AddComponent<LevelCompleteUI>();
            log.Add(cena.name + ": painel de fim de fase criado (FimDeFaseCanvas).");
        }
        else
        {
            painel = ui.gameObject;
            for (int i = painel.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(painel.transform.GetChild(i).gameObject);
            }
        }

        // Canvas próprio por cima do HUD e das etiquetas (pausa = 100).
        Canvas proprio = painel.GetComponent<Canvas>();
        if (proprio == null) proprio = painel.AddComponent<Canvas>();
        proprio.overrideSorting = true;
        proprio.sortingOrder = 95;
        if (painel.GetComponent<GraphicRaycaster>() == null) painel.AddComponent<GraphicRaycaster>();

        Image veu = painel.GetComponent<Image>();
        if (veu == null) veu = painel.AddComponent<Image>();
        veu.sprite = null;
        veu.type = Image.Type.Simple;
        veu.color = TemaLabUI.CorVeu;
        veu.raycastTarget = true;

        Image jan = TemaLabUI.Imagem(painel.transform, "Janela", tema.painelPopup, TemaLabUI.Painel);
        TemaLabUI.Centro(jan.rectTransform, Vector2.zero, new Vector2(1000f, 780f));
        Transform j = jan.transform;

        Image selo = TemaLabUI.Imagem(j, "Selo", tema.seloCheck, TemaLabUI.Verde);
        selo.preserveAspect = true;
        TemaLabUI.Posicionar(selo.rectTransform, new Vector2(0f, 1f), new Vector2(44f, -40f), new Vector2(84f, 84f));
        TextMeshProUGUI fase = TemaLabUI.Rotulo(j, "Fase", ProgressoJogo.TituloDaCena(cena.name), 18f, TemaLabUI.Ciano);
        TemaLabUI.Posicionar(fase.rectTransform, new Vector2(0f, 1f), new Vector2(148f, -44f), new Vector2(600f, 28f));
        TextMeshProUGUI titulo = Txt(j, "Titulo", "Fase Concluída!", 58f, TemaLabUI.CorTexto, TemaLabUI.Fonte.Display);
        TemaLabUI.Posicionar(titulo.rectTransform, new Vector2(0f, 1f), new Vector2(146f, -66f), new Vector2(800f, 70f));
        TextMeshProUGUI secao = TemaLabUI.Rotulo(j, "Secao", "O que você montou", 18f, TemaLabUI.Dourado);
        TemaLabUI.Posicionar(secao.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -152f), new Vector2(600f, 28f));

        const int maxLinhas = 6;
        const float alturaLinha = 70f, topo = -192f;
        var linhas = new LevelCompleteUI.Linha[maxLinhas];
        for (int i = 0; i < maxLinhas; i++)
        {
            RectTransform l = TemaLabUI.Novo(j, "Linha" + (i + 1));
            TemaLabUI.Posicionar(l, new Vector2(0f, 1f), new Vector2(0f, topo - i * alturaLinha), new Vector2(1000f, alturaLinha));
            Image ck = TemaLabUI.Imagem(l, "Check", tema.seloCheck, TemaLabUI.Verde);
            ck.preserveAspect = true;
            TemaLabUI.Posicionar(ck.rectTransform, new Vector2(0f, 1f), new Vector2(50f, -6f), new Vector2(34f, 34f));
            TextMeshProUGUI nome = Txt(l, "Nome", "Item montado", 28f, TemaLabUI.CorTexto, TemaLabUI.Fonte.CorpoForte, TextAlignmentOptions.MidlineLeft, 20f);
            TemaLabUI.Posicionar(nome.rectTransform, new Vector2(0f, 1f), new Vector2(100f, 0f), new Vector2(850f, 38f));
            TextMeshProUGUI frase = Txt(l, "Frase", "o que ele faz", 22f, TemaLabUI.TextoSuave, TemaLabUI.Fonte.Corpo, TextAlignmentOptions.MidlineLeft, 16f);
            TemaLabUI.Posicionar(frase.rectTransform, new Vector2(0f, 1f), new Vector2(100f, -36f), new Vector2(850f, 28f));
            l.gameObject.SetActive(i < 3);
            linhas[i] = new LevelCompleteUI.Linha { raiz = l.gameObject, nome = nome, frase = frase };
        }

        RectTransform stats = TemaLabUI.Novo(j, "Estatisticas");
        TemaLabUI.Posicionar(stats, new Vector2(0f, 1f), new Vector2(48f, topo - 3 * alturaLinha - 18f), new Vector2(904f, 110f));
        TextMeshProUGUI tempo = Stat(stats, 0f, "Tempo", out _);
        TextMeshProUGUI contagem = Stat(stats, 307f, "Reinícios", out TextMeshProUGUI rotuloContagem);
        TextMeshProUGUI tentativa = Stat(stats, 614f, "Tentativa", out _);

        TextMeshProUGUI prox = TemaLabUI.Rotulo(j, "ProximaRotulo", "Próxima", 18f, TemaLabUI.Ciano);
        TemaLabUI.Posicionar(prox.rectTransform, new Vector2(0f, 0f), new Vector2(48f, 44f), new Vector2(160f, 30f));
        TextMeshProUGUI proxima = Txt(j, "Proxima", ProgressoJogo.TituloDaProxima(cena.name), 32f, TemaLabUI.CorTexto, TemaLabUI.Fonte.Display, TextAlignmentOptions.MidlineLeft, 22f);
        TemaLabUI.Posicionar(proxima.rectTransform, new Vector2(0f, 0f), new Vector2(190f, 36f), new Vector2(520f, 46f));
        Tecla(j, "Enter", new Vector2(1f, 0f), new Vector2(-200f, 38f), new Vector2(92f, 46f));
        TextMeshProUGUI continuar = Txt(j, "Continuar", "Continuar", 24f, TemaLabUI.TextoSuave, TemaLabUI.Fonte.CorpoForte);
        TemaLabUI.Posicionar(continuar.rectTransform, new Vector2(1f, 0f), new Vector2(-48f, 42f), new Vector2(140f, 40f));

        var so = new SerializedObject(ui);
        so.FindProperty("janela").objectReferenceValue = jan.rectTransform;
        so.FindProperty("tituloFase").objectReferenceValue = fase;
        so.FindProperty("rotuloSecao").objectReferenceValue = secao;
        SerializedProperty sl = so.FindProperty("linhas");
        sl.arraySize = maxLinhas;
        for (int i = 0; i < maxLinhas; i++)
        {
            SerializedProperty e = sl.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("raiz").objectReferenceValue = linhas[i].raiz;
            e.FindPropertyRelative("nome").objectReferenceValue = linhas[i].nome;
            e.FindPropertyRelative("frase").objectReferenceValue = linhas[i].frase;
        }
        so.FindProperty("blocoStats").objectReferenceValue = stats;
        so.FindProperty("valorTempo").objectReferenceValue = tempo;
        so.FindProperty("rotuloContagem").objectReferenceValue = rotuloContagem;
        so.FindProperty("valorContagem").objectReferenceValue = contagem;
        so.FindProperty("valorTentativa").objectReferenceValue = tentativa;
        so.FindProperty("proxima").objectReferenceValue = proxima;
        so.FindProperty("alturaBase").floatValue = 430f;
        so.FindProperty("alturaLinha").floatValue = alturaLinha;
        so.FindProperty("topoLinhas").floatValue = topo;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Mesmo tamanho que terá em Play com 3 itens (o Editor mostra um exemplo).
        jan.rectTransform.sizeDelta = new Vector2(1000f, 430f + 3 * alturaLinha);
        painel.SetActive(false);

        // A Física achava o painel sozinha, mas fica ligado no Inspector também.
        GameFlowManager flow = Achar<GameFlowManager>(cena);
        if (flow != null)
        {
            var sf = new SerializedObject(flow);
            sf.FindProperty("completeUI").objectReferenceValue = ui;
            sf.ApplyModifiedPropertiesWithoutUndo();
        }

        log.Add(cena.name + ": cartão 'Fase Concluída!' montado.");
    }

    private static TextMeshProUGUI Stat(RectTransform pai, float x, string rotulo, out TextMeshProUGUI rotuloTexto)
    {
        RectTransform miolo = TemaLabUI.CaixaArredondada(pai, "Stat" + rotulo, TemaLabUI.Contorno, TemaLabUI.Poco, 2f);
        TemaLabUI.Posicionar((RectTransform)miolo.parent, new Vector2(0f, 1f), new Vector2(x, 0f), new Vector2(290f, 110f));
        rotuloTexto = TemaLabUI.Rotulo(miolo, "Rotulo", rotulo, 16f, TemaLabUI.TextoSuave);
        rotuloTexto.alignment = TextAlignmentOptions.Center;
        TemaLabUI.Posicionar(rotuloTexto.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(270f, 24f));
        TextMeshProUGUI v = Txt(miolo, "Valor", "—", 40f, TemaLabUI.CorTexto, TemaLabUI.Fonte.Display, TextAlignmentOptions.Center);
        TemaLabUI.Posicionar(v.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(270f, 54f));
        return v;
    }
}
