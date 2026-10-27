using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Porta de saída das fases: a mesma porta de duas folhas que abre para os
/// lados da rodada "Porta" do Tutorial_Anim (quadros porta_01..05). Fica
/// fechada; quando o jogador chega perto (e a fase deixa), toca a animação
/// de abrir, e a fase só termina quando ele entra no vão.
///
/// Fica salva em cada cena (objeto "PortaSaida", colocado pelo menu
/// Tools > Fases > Colocar porta de saída, ver PortaSaidaBuilder), então
/// aparece e pode ser movida no Editor. A lógica de conclusão continua a de
/// cada fase (save, CSV, tela "Fase concluída"):
///   - Aplicacao_Fisica: LevelGoal, com o gatilho no vão da porta.
///   - Animacao: AnimacaoObjetivo; só abre com PodeConcluir.
///   - IA e IA_Aplicacao: IASaida; só abre com a saída Aberta.
///   - Interacao: o item "Objetivo"; a consequência "Passar a fase" destranca
///     a porta (Liberar) em vez de concluir na hora.
///   - Interacao_Vida: conclui sozinha ao atravessar (concluirAoAtravessar).
///
/// Enquanto não está toda aberta, o collider sólido (deste objeto) segura o
/// jogador, então o gatilho de conclusão, mais estreito, só é alcançado com a
/// porta aberta.
/// </summary>
public class PortaSaida : MonoBehaviour
{
    public enum Condicao
    {
        [Tooltip("Abre sempre que o jogador chega perto.")]
        Sempre,
        [Tooltip("Animacao: só depois do puzzle do Animator (AnimacaoLevelFlow.PodeConcluir).")]
        AnimatorMontado,
        [Tooltip("IA: só com a saída liberada (IASaida.Aberta).")]
        SaidaIAAberta,
        [Tooltip("Interacao: só depois de o item configurado disparar 'Passar a fase'.")]
        ItemLiberar
    }

    [Tooltip("Quadros da porta, da fechada (0) à aberta.")]
    [SerializeField] private Sprite[] quadros;
    [SerializeField] private SpriteRenderer visual;
    [Tooltip("Collider sólido que segura o jogador até a porta abrir.")]
    [SerializeField] private BoxCollider2D solido;

    [SerializeField] private Condicao condicao = Condicao.Sempre;
    [Tooltip("Usado com SaidaIAAberta.")]
    [SerializeField] private IASaida saidaIA;
    [Tooltip("Usado com ItemLiberar: o item 'Objetivo' que destranca esta porta.")]
    [SerializeField] private InteractableItem item;
    [Tooltip("Marque quando a fase não tem outro gatilho de saída: atravessar a porta aberta chama InteracaoLevelFlow.CompleteLevel (Interacao_Vida).")]
    [SerializeField] private bool concluirAoAtravessar;
    [Tooltip("Animacao: se preenchido, a porta só abre com esta bateria cheia (parâmetro Int carga).")]
    [SerializeField] private BateriaFase bateria;

    [SerializeField] private float quadrosPorSegundo = 12f;
    [Tooltip("Distância (horizontal) do jogador ao centro da porta para ela abrir.")]
    [SerializeField] private float distanciaAbrir = 2.6f;
    [Tooltip("Altura do vão, usada para saber se o jogador está perto/dentro.")]
    [SerializeField] private float altura = 1.6f;
    [Tooltip("Largura do vão que conta como 'atravessou'.")]
    [SerializeField] private float larguraVao = 0.45f;

    private static readonly Dictionary<GameObject, PortaSaida> portasDeItem = new Dictionary<GameObject, PortaSaida>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => portasDeItem.Clear();

    private Action aoPassar;
    private bool liberada;
    private bool passou;
    private float quadro;
    private Collider2D jogador;

    public bool TotalmenteAberta => quadros != null && quadros.Length > 0 && Mathf.RoundToInt(quadro) >= quadros.Length - 1;

    /// <summary>Centro do vão da porta, em coordenadas de mundo.</summary>
    public Vector2 CentroVao => (Vector2)transform.position + new Vector2(0f, altura * 0.5f);

    /// <summary>A porta ligada a um item "Passar a fase" (Interacao), ou null.</summary>
    public static PortaSaida DoItem(GameObject itemGO)
    {
        return itemGO != null && portasDeItem.TryGetValue(itemGO, out PortaSaida p) && p != null ? p : null;
    }

    /// <summary>
    /// Destranca a porta (modo ItemLiberar): ela abre com o jogador perto e
    /// chama <paramref name="aoAtravessar"/> quando ele entra no vão.
    /// </summary>
    public void Liberar(Action aoAtravessar)
    {
        liberada = true;
        aoPassar = aoAtravessar;
    }

    private void Awake()
    {
        liberada = condicao != Condicao.ItemLiberar;
        if (item != null)
        {
            portasDeItem[item.gameObject] = this;
        }
        if (concluirAoAtravessar)
        {
            aoPassar = () => InteracaoLevelFlow.Instance?.CompleteLevel();
        }
        MostrarQuadro(0);
    }

    private void OnDestroy()
    {
        if (item != null && DoItem(item.gameObject) == this)
        {
            portasDeItem.Remove(item.gameObject);
        }
    }

    private void Update()
    {
        if (quadros == null || quadros.Length == 0)
        {
            return;
        }

        if (jogador == null)
        {
            jogador = AcharJogador();
        }

        bool querAberta = passou || (PodeAbrir() && JogadorPerto());
        float alvo = querAberta ? quadros.Length - 1 : 0f;

        if (alvo > 0f && quadro <= 0f)
        {
            AudioManager.TocarEfeito(AudioManager.Efeito.PortaAbrir);
        }

        quadro = Mathf.MoveTowards(quadro, alvo, quadrosPorSegundo * Time.deltaTime);
        MostrarQuadro(Mathf.RoundToInt(quadro));
        if (solido != null)
        {
            solido.enabled = !TotalmenteAberta;
        }

        if (aoPassar != null && !passou && TotalmenteAberta && JogadorNoVao())
        {
            passou = true;
            aoPassar();
        }
    }

    // Interacao: o collider sólido desta porta fica por cima do collider do
    // item "Objetivo", que é trigger (e o projeto tem queriesHitTriggers
    // desligado), então o clique do mouse cai aqui e não no item. Repassa,
    // pra porta abrir o painel "Configurar item" como os outros objetos.
    private void OnMouseDown()
    {
        if (item != null && item.isActiveAndEnabled)
        {
            item.AbrirPainelPeloClique();
        }
    }

    private bool PodeAbrir()
    {
        if (bateria != null && !bateria.Cheia)
        {
            return false;
        }

        switch (condicao)
        {
            case Condicao.AnimatorMontado:
                return AnimacaoLevelFlow.Instance != null && AnimacaoLevelFlow.Instance.PodeConcluir;
            case Condicao.SaidaIAAberta:
                return saidaIA == null || saidaIA.Aberta;
            case Condicao.ItemLiberar:
                return liberada;
            default:
                return true;
        }
    }

    private void MostrarQuadro(int i)
    {
        if (visual != null && quadros != null && quadros.Length > 0)
        {
            visual.sprite = quadros[Mathf.Clamp(i, 0, quadros.Length - 1)];
        }
    }

    private bool JogadorPerto()
    {
        if (jogador == null)
        {
            return false;
        }

        Vector2 d = (Vector2)jogador.bounds.center - CentroVao;
        return Mathf.Abs(d.x) < distanciaAbrir && Mathf.Abs(d.y) < altura + 0.5f;
    }

    private bool JogadorNoVao()
    {
        if (jogador == null)
        {
            return false;
        }

        Vector2 d = (Vector2)jogador.bounds.center - CentroVao;
        return Mathf.Abs(d.x) < larguraVao * 0.5f + jogador.bounds.extents.x * 0.5f
            && Mathf.Abs(d.y) < altura * 0.5f;
    }

    private Collider2D AcharJogador()
    {
        foreach (GameObject raiz in gameObject.scene.GetRootGameObjects())
        {
            MonoBehaviour p = raiz.GetComponentInChildren<InteracaoPlayerController>();
            if (p == null) p = raiz.GetComponentInChildren<AplicacaoPlayerController>();
            if (p == null) p = raiz.GetComponentInChildren<AnimacaoPlayerFisica>();
            if (p != null)
            {
                return p.GetComponent<Collider2D>();
            }
        }
        return null;
    }
}
