using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Um cartão do seletor de fases. O visual é todo do prefab (edite o
/// "CartaoFase" dentro de Assets/Resources/SeletorFases.prefab): este script
/// só preenche os textos e aplica o estilo do estado — Bloqueada, Disponível,
/// Atual (a próxima a jogar) ou Concluída — configurado no Inspector.
///
/// Cada estado tem cores e, se quiser, um sprite de fundo próprio. Deixe o
/// sprite vazio pra manter o que estiver na Image. Os ícones/selos de cada
/// estado são ligados/desligados sozinhos, e o "Foco" (anel ciano) aparece
/// enquanto o cartão está selecionado. Passar o mouse por cima também
/// seleciona o cartão, pra os detalhes da fase acompanharem o mouse.
/// </summary>
public class CartaoFaseUI : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    public enum Estado { Bloqueada, Disponivel, Concluida, Atual }

    [Serializable]
    public class Estilo
    {
        [Tooltip("Cor (tint) do fundo do cartão.")]
        public Color fundo = Color.white;
        [Tooltip("Opcional: sprite de fundo só deste estado. Vazio = mantém o sprite da Image.")]
        public Sprite spriteFundo;
        [Tooltip("Cor da borda (componente Outline), se houver.")]
        public Color borda = Color.white;
        public Color numero = Color.white;
        public Color titulo = Color.white;
        public Color conceito = Color.gray;
        public Color status = Color.white;
    }

    [Header("Partes do cartão")]
    [SerializeField] private Image fundo;
    [Tooltip("Opcional — borda colorida pelo estado.")]
    [SerializeField] private Outline borda;
    [SerializeField] private Button botao;
    [SerializeField] private TMP_Text numero;
    [SerializeField] private TMP_Text titulo;
    [SerializeField] private TMP_Text conceito;
    [SerializeField] private TMP_Text status;
    [Tooltip("Opcional — aparece só quando a fase está bloqueada.")]
    [SerializeField] private GameObject iconeBloqueada;
    [Tooltip("Opcional — aparece só quando a fase está concluída.")]
    [SerializeField] private GameObject iconeConcluida;
    [Tooltip("Opcional — selo com o número, aparece só na fase atual (a próxima a jogar).")]
    [SerializeField] private GameObject seloAtual;
    [SerializeField] private TMP_Text seloAtualNumero;
    [Tooltip("Opcional — mostrado enquanto o cartão está selecionado (anel de foco).")]
    [SerializeField] private GameObject foco;
    [Tooltip("Opcional — o número pequeno fica escondido nos estados com selo (Concluída e Atual).")]
    [SerializeField] private bool esconderNumeroComSelo;

    [Header("Estilo de cada estado")]
    [SerializeField] private Estilo estiloBloqueada = new Estilo();
    [SerializeField] private Estilo estiloDisponivel = new Estilo();
    [SerializeField] private Estilo estiloConcluida = new Estilo();
    [SerializeField] private Estilo estiloAtual = new Estilo();

    private Action aoClicar;

    public Button Botao => botao;
    public Estado EstadoAtual { get; private set; }

    /// <summary>Disparado quando o cartão recebe o foco (teclado, controle ou mouse).</summary>
    public event Action<CartaoFaseUI> AoSelecionar;

    private void Awake()
    {
        if (botao != null)
        {
            botao.onClick.AddListener(() => aoClicar?.Invoke());
        }

        if (foco != null) foco.SetActive(false);
    }

    public void Preencher(string textoNumero, string textoTitulo, string textoConceito, string textoStatus, Estado estado, Action acao)
    {
        aoClicar = acao;
        EstadoAtual = estado;

        if (numero != null) numero.text = textoNumero;
        if (seloAtualNumero != null) seloAtualNumero.text = textoNumero;
        if (titulo != null) titulo.text = textoTitulo;
        if (conceito != null) conceito.text = textoConceito;
        if (status != null) status.text = textoStatus;

        Estilo e = estado switch
        {
            Estado.Concluida => estiloConcluida,
            Estado.Disponivel => estiloDisponivel,
            Estado.Atual => estiloAtual,
            _ => estiloBloqueada,
        };

        if (fundo != null)
        {
            fundo.color = e.fundo;
            if (e.spriteFundo != null) fundo.sprite = e.spriteFundo;
        }

        if (borda != null) borda.effectColor = e.borda;
        if (numero != null) numero.color = e.numero;
        if (titulo != null) titulo.color = e.titulo;
        if (conceito != null) conceito.color = e.conceito;
        if (status != null) status.color = e.status;

        if (iconeBloqueada != null) iconeBloqueada.SetActive(estado == Estado.Bloqueada);
        if (iconeConcluida != null) iconeConcluida.SetActive(estado == Estado.Concluida);
        if (seloAtual != null) seloAtual.SetActive(estado == Estado.Atual);

        if (numero != null && esconderNumeroComSelo)
        {
            numero.gameObject.SetActive(estado != Estado.Concluida && estado != Estado.Atual);
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (foco != null) foco.SetActive(true);
        AoSelecionar?.Invoke(this);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (foco != null) foco.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (botao != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(botao.gameObject);
        }
    }
}
