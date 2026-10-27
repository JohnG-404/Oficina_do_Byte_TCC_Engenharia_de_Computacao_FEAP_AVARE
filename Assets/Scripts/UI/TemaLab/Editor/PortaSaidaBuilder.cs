using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Coloca a porta de saída (PortaSaida, a porta de correr do Tutorial_Anim)
/// no fim de cada fase e salva a cena, para ela aparecer no Editor:
/// apoia a porta no chão logo abaixo da saída antiga, afasta da parede,
/// leva o gatilho de conclusão para o vão e esconde o desenho antigo
/// (cartão, moeda, laser). Rodar de novo refaz a porta no mesmo lugar.
///
/// Cada cena só é mexida se não tiver mudanças não salvas (outras sessões
/// usam o mesmo Editor); antes de salvar, uma cópia vai para
/// SceneBackups/PortaFinal_2026-10-05/.
/// Menu: Tools > Fases > Colocar porta de saída.
/// </summary>
public static class PortaSaidaBuilder
{
    private const string PastaQuadros = "Assets/Anim/TutorialAnim/Porta/porta_0";
    private const string PastaBackup = "SceneBackups/PortaFinal_2026-10-05/";
    private const string NomePorta = "PortaSaida";

    // porta_01..05: 320 px a 256 PPU. Na escala 1,28 a porta tem 1,6 u; a
    // linha de baixo da arte fica a 20 px (0,1 u) da borda do sprite, por
    // isso o desenho desce 0,1 u para a base encostar no chão.
    private const float Escala = 1.28f;
    private const float Altura = 1.6f;
    private const float RespiroBase = 0.1f;
    private const float MeiaLargura = 0.64f;
    private static readonly Vector2 TamanhoSolido = new Vector2(1.1f, 1.45f);
    private static readonly Vector2 TamanhoVao = new Vector2(0.45f, 1.1f);

    private static readonly string[] Cenas =
    {
        "Aplicacao_Fisica", "Interacao", "Interacao_Vida", "Animacao", "IA", "IA_Aplicacao"
    };

    [MenuItem("Tools/Fases/Colocar porta de saída")]
    private static void Menu()
    {
        Debug.Log(ColocarEmTodas());
    }

    public static string ColocarEmTodas(params string[] soEstas)
    {
        var log = new StringBuilder();
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return "[PortaSaida] Saia do Play Mode antes.";
        }

        foreach (string nome in soEstas != null && soEstas.Length > 0 ? soEstas : Cenas)
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
            if (!File.Exists(backup))
            {
                File.Copy(caminho, backup);
            }

            log.AppendLine(nome + ": " + Colocar(cena));
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);

            if (abriAqui)
            {
                EditorSceneManager.CloseScene(cena, true);
            }
        }
        return log.ToString();
    }

    private static string Colocar(Scene cena)
    {
        foreach (GameObject antiga in cena.GetRootGameObjects().Where(g => g.name == NomePorta).ToArray())
        {
            Object.DestroyImmediate(antiga);
        }
        Physics2D.SyncTransforms();

        IEnumerable<T> Todos<T>() where T : Component =>
            cena.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true));

        LevelGoal goal = Todos<LevelGoal>().FirstOrDefault();
        if (goal != null)
        {
            var porta = Criar(cena, Centro(goal.gameObject), Colliders(goal.gameObject), out string info);
            LevarGatilhoAoVao(goal.gameObject, porta);
            return info;
        }

        AnimacaoObjetivo obj = Todos<AnimacaoObjetivo>().FirstOrDefault();
        if (obj != null)
        {
            Vector2 ponto = Centro(obj.gameObject);
            EsconderDesenho(obj.gameObject);
            var porta = Criar(cena, ponto, Colliders(obj.gameObject), out string info);
            Configurar(porta, PortaSaida.Condicao.AnimatorMontado);
            LevarGatilhoAoVao(obj.gameObject, porta);
            return info + " (cartão escondido)";
        }

        IASaida saida = Todos<IASaida>().FirstOrDefault();
        if (saida != null)
        {
            var ignorar = Colliders(saida.gameObject);
            Vector2 ponto = Centro(saida.gameObject);
            if (saida.Barreira != null)
            {
                // A porta fica no lugar do laser; o collider da barreira
                // continua segurando o jogador enquanto a saída está fechada.
                foreach (Collider2D c in saida.Barreira.GetComponentsInChildren<Collider2D>(true))
                {
                    ignorar.Add(c);
                    if (!c.isTrigger) ponto.x = Caixa(c).center.x;
                }
                foreach (Renderer r in saida.Barreira.GetComponentsInChildren<Renderer>(true))
                {
                    r.enabled = false;
                    EditorUtility.SetDirty(r);
                }
            }

            Transform vazia = saida.transform.Find("Porta");
            if (vazia != null) vazia.gameObject.SetActive(false);

            var porta = Criar(cena, ponto, ignorar, out string info);
            Configurar(porta, PortaSaida.Condicao.SaidaIAAberta, saidaIA: saida);
            LevarGatilhoAoVao(saida.gameObject, porta);

            Transform placa = saida.transform.Find("Placa");
            if (placa != null)
            {
                placa.position = new Vector3(porta.transform.position.x, porta.transform.position.y + Altura + 0.3f, placa.position.z);
            }
            return info + (saida.Barreira != null ? " (laser escondido)" : "");
        }

        InteractableItem item = Todos<InteractableItem>().FirstOrDefault(i => i.AllowedConsequences.Contains(ConsequenceType.CompleteLevel));
        if (item != null)
        {
            Vector2 ponto = Centro(item.gameObject);
            EsconderDesenho(item.gameObject);
            var porta = Criar(cena, ponto, Colliders(item.gameObject), out string info);
            Configurar(porta, PortaSaida.Condicao.ItemLiberar, item: item);

            // Alvo do clique do item, do tamanho da porta. É gatilho: quem
            // bloqueia é o collider sólido da porta. Com ele salvo, o
            // InteractableItem não cria o collider do tamanho da moeda.
            var box = item.GetComponent<BoxCollider2D>();
            if (box == null) box = item.gameObject.AddComponent<BoxCollider2D>();
            Vector3 esc = item.transform.lossyScale;
            box.offset = item.transform.InverseTransformPoint(porta.CentroVao);
            box.size = new Vector2(TamanhoSolido.x / Mathf.Abs(esc.x), TamanhoSolido.y / Mathf.Abs(esc.y));
            box.isTrigger = true;
            return info + " (moeda escondida, item Objetivo vira a porta)";
        }

        if (cena.name == "Interacao_Vida")
        {
            // A fase não tinha saída: porta no fim do chão, depois do último
            // espinho, encostada na parede da direita.
            var porta = Criar(cena, new Vector2(24f, -3.5f), new HashSet<Collider2D>(), out string info);
            Configurar(porta, PortaSaida.Condicao.Sempre, concluir: true);
            return info + " (saída nova)";
        }

        return "nenhuma saída encontrada";
    }

    private static Vector2 Centro(GameObject go)
    {
        var col = go.GetComponent<Collider2D>();
        if (col != null) return Caixa(col).center;
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) return sr.bounds.center;
        return go.transform.position;
    }

    private static HashSet<Collider2D> Colliders(GameObject go) =>
        new HashSet<Collider2D>(go.GetComponentsInChildren<Collider2D>(true));

    private static void EsconderDesenho(GameObject go)
    {
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;
        var anim = go.GetComponent<Animator>();
        if (anim != null) anim.enabled = false;
    }

    private static void Configurar(PortaSaida porta, PortaSaida.Condicao condicao, IASaida saidaIA = null, InteractableItem item = null, bool concluir = false)
    {
        var so = new SerializedObject(porta);
        so.FindProperty("condicao").enumValueIndex = (int)condicao;
        so.FindProperty("saidaIA").objectReferenceValue = saidaIA;
        so.FindProperty("item").objectReferenceValue = item;
        so.FindProperty("concluirAoAtravessar").boolValue = concluir;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Move o collider-gatilho de conclusão para o meio do vão da porta.</summary>
    private static void LevarGatilhoAoVao(GameObject gatilho, PortaSaida porta)
    {
        Vector3 esc = gatilho.transform.lossyScale;
        Vector2 local = gatilho.transform.InverseTransformPoint(porta.transform.position + new Vector3(0f, TamanhoVao.y * 0.5f + 0.1f, 0f));
        switch (gatilho.GetComponent<Collider2D>())
        {
            case BoxCollider2D box:
                box.offset = local;
                box.size = new Vector2(TamanhoVao.x / Mathf.Abs(esc.x), TamanhoVao.y / Mathf.Abs(esc.y));
                break;
            case CircleCollider2D circ:
                circ.offset = local;
                circ.radius = TamanhoVao.x * 0.5f / Mathf.Abs(esc.x);
                break;
        }
    }

    private static PortaSaida Criar(Scene cena, Vector2 ponto, HashSet<Collider2D> ignorar, out string info)
    {
        Collider2D jogador = AcharJogador(cena);
        if (jogador != null)
        {
            foreach (Collider2D c in jogador.GetComponentsInChildren<Collider2D>(true)) ignorar.Add(c);
        }

        var solidos = cena.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<Collider2D>())
            // O CompositeCollider2D (Animacao) é a soma dos blocos, que já
            // entram um por um; a caixa dele cobre a fase inteira.
            .Where(c => c.enabled && !c.isTrigger && !ignorar.Contains(c) && !(c is CompositeCollider2D))
            .ToList();

        // Chão: o topo mais alto logo abaixo do ponto.
        float chao = ponto.y - Altura * 0.5f;
        Collider2D colChao = null;
        foreach (Collider2D c in solidos)
        {
            Bounds b = Caixa(c);
            if (ponto.x >= b.min.x && ponto.x <= b.max.x && b.max.y <= ponto.y + 0.05f && (colChao == null || b.max.y > chao))
            {
                chao = b.max.y;
                colChao = c;
            }
        }

        // Paredes: empurra a porta para fora de qualquer coisa sólida na
        // altura dela.
        float x = ponto.x;
        for (int i = 0; i < 2; i++)
        {
            foreach (Collider2D c in solidos)
            {
                Bounds b = Caixa(c);
                bool naAltura = b.max.y > chao + 0.15f && b.min.y < chao + Altura - 0.15f;
                bool encosta = b.min.x < x + MeiaLargura && b.max.x > x - MeiaLargura;
                if (!naAltura || !encosta) continue;
                if (b.center.x >= x) x = b.min.x - MeiaLargura;
                else x = b.max.x + MeiaLargura;
            }
        }

        var go = new GameObject(NomePorta);
        SceneManager.MoveGameObjectToScene(go, cena);
        go.layer = colChao != null ? colChao.gameObject.layer : 0;
        go.transform.position = new Vector3(x, chao, 0f);

        var visualGO = new GameObject("Visual");
        visualGO.layer = go.layer;
        visualGO.transform.SetParent(go.transform, false);
        visualGO.transform.localPosition = new Vector3(0f, Altura * 0.5f - RespiroBase, 0f);
        visualGO.transform.localScale = Vector3.one * Escala;
        var sr = visualGO.AddComponent<SpriteRenderer>();

        SpriteRenderer srJogador = jogador != null ? jogador.GetComponentInChildren<SpriteRenderer>() : null;
        if (srJogador != null)
        {
            sr.sortingLayerID = srJogador.sortingLayerID;
            sr.sortingOrder = srJogador.sortingOrder - 1;
        }

        var solido = go.AddComponent<BoxCollider2D>();
        solido.size = TamanhoSolido;
        solido.offset = new Vector2(0f, TamanhoSolido.y * 0.5f);

        var porta = go.AddComponent<PortaSaida>();
        var so = new SerializedObject(porta);
        var quadros = so.FindProperty("quadros");
        quadros.arraySize = 5;
        for (int i = 0; i < 5; i++)
        {
            quadros.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(PastaQuadros + (i + 1) + ".png");
        }
        so.FindProperty("visual").objectReferenceValue = sr;
        so.FindProperty("solido").objectReferenceValue = solido;
        so.FindProperty("altura").floatValue = Altura - RespiroBase;
        so.ApplyModifiedPropertiesWithoutUndo();
        sr.sprite = (Sprite)quadros.GetArrayElementAtIndex(0).objectReferenceValue;

        info = $"porta em ({x:0.00}, {chao:0.00}), chão = {(colChao != null ? colChao.name : "nenhum (estimado)")}";
        return porta;
    }

    // Fora do Play, colliders de cenas abertas por código podem vir com
    // bounds vazios (ainda sem forma na física), então a caixa de um
    // BoxCollider2D é calculada direto do tamanho/offset.
    private static Bounds Caixa(Collider2D c)
    {
        if (c is BoxCollider2D box)
        {
            Vector3 esc = box.transform.lossyScale;
            Vector3 centro = box.transform.TransformPoint(box.offset);
            Vector2 tam = new Vector2(Mathf.Abs(box.size.x * esc.x), Mathf.Abs(box.size.y * esc.y)) + Vector2.one * box.edgeRadius * 2f;
            return new Bounds(centro, tam);
        }
        if (c.bounds.size.sqrMagnitude > 0f)
        {
            return c.bounds;
        }
        var sr = c.GetComponent<SpriteRenderer>();
        return sr != null ? sr.bounds : c.bounds;
    }

    private static Collider2D AcharJogador(Scene cena)
    {
        foreach (GameObject raiz in cena.GetRootGameObjects())
        {
            MonoBehaviour p = raiz.GetComponentInChildren<InteracaoPlayerController>(true);
            if (p == null) p = raiz.GetComponentInChildren<AplicacaoPlayerController>(true);
            if (p == null) p = raiz.GetComponentInChildren<AnimacaoPlayerFisica>(true);
            if (p != null) return p.GetComponent<Collider2D>();
        }
        return null;
    }
}
