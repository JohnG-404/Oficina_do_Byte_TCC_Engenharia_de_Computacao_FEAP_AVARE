using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Garante que sempre exista um item selecionado quando uma tela está aberta,
/// pra o anel de foco (sprite "_focus") aparecer ao navegar por teclado ou
/// controle. Coloque na raiz de cada tela (menu, pausa, configurações,
/// seletor de fases) e arraste em "Candidatos" os itens que podem receber o
/// foco ao abrir — o primeiro ativo e clicável é escolhido.
///
/// As telas formam uma pilha: só a última aberta cuida do foco. Ao fechar,
/// a tela de baixo volta a selecionar o item que estava selecionado quando a
/// de cima abriu (ex: fechar Configurações devolve o foco ao botão
/// "Configurações" do menu).
///
/// Só visual: não muda nenhuma ação dos botões.
/// </summary>
public class FocoInicialUI : MonoBehaviour
{
    [Tooltip("Itens que podem receber o foco ao abrir, em ordem de preferência.")]
    [SerializeField] private Selectable[] candidatos;

    private static readonly List<FocoInicialUI> pilha = new List<FocoInicialUI>();

    private GameObject selecaoAoAbrir;
    private GameObject pedido;
    private bool focarAgora;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => pilha.Clear();

    private bool EhTopo => pilha.Count > 0 && pilha[pilha.Count - 1] == this;

    private void OnEnable()
    {
        EventSystem es = EventSystem.current;
        selecaoAoAbrir = es != null ? es.currentSelectedGameObject : null;
        pilha.Remove(this);
        pilha.Add(this);
        focarAgora = true;
    }

    private void OnDisable()
    {
        bool eraTopo = EhTopo;
        pilha.Remove(this);
        if (eraTopo && pilha.Count > 0)
        {
            pilha[pilha.Count - 1].pedido = selecaoAoAbrir;
            pilha[pilha.Count - 1].focarAgora = true;
        }
    }

    private void Update()
    {
        if (!EhTopo)
        {
            return;
        }

        EventSystem es = EventSystem.current;
        if (es == null)
        {
            return;
        }

        GameObject atual = es.currentSelectedGameObject;
        if (!focarAgora && Valido(atual))
        {
            return;
        }

        focarAgora = false;

        if (Valido(pedido))
        {
            es.SetSelectedGameObject(pedido);
            pedido = null;
            return;
        }

        pedido = null;

        // A própria tela pode já ter escolhido o item (ex: o seletor de fases
        // seleciona a próxima fase): respeita.
        if (Valido(atual))
        {
            return;
        }

        if (candidatos == null)
        {
            return;
        }

        foreach (Selectable s in candidatos)
        {
            if (s != null && s.IsActive() && s.IsInteractable())
            {
                es.SetSelectedGameObject(s.gameObject);
                return;
            }
        }
    }

    private bool Valido(GameObject go)
    {
        if (go == null || !go.activeInHierarchy || !go.transform.IsChildOf(transform))
        {
            return false;
        }

        Selectable s = go.GetComponent<Selectable>();
        return s == null || s.IsInteractable();
    }
}
