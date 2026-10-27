using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Linha "Você tem:" do inventário: acende o chip de cada ferramenta que já
/// foi aplicada no jogador (lê HasPhysics/HasCollision/HasMovement/HasJump do
/// AplicacaoPlayerController). Só leitura — não muda nada no jogador.
/// </summary>
public class InventarioStatusJogador : MonoBehaviour
{
    [System.Serializable]
    public class Chip
    {
        public ToolType ferramenta;
        public Image fundo;
        public Image borda;
        public Image ponto;
        public TMP_Text nome;
    }

    [SerializeField] private AplicacaoPlayerController jogador;
    [SerializeField] private Chip[] chips;

    [Header("Cores do chip apagado")]
    [SerializeField] private Color fundoApagado = new Color(0.118f, 0.125f, 0.149f, 1f);
    [SerializeField] private Color bordaApagada = new Color(0.180f, 0.196f, 0.235f, 1f);
    [SerializeField] private Color pontoApagado = new Color(0.227f, 0.247f, 0.290f, 1f);
    [SerializeField] private Color textoApagado = new Color(0.42f, 0.45f, 0.50f, 1f);
    [SerializeField] private Color textoAceso = new Color(0.90f, 0.91f, 0.94f, 1f);

    private bool[] estado;

    private void Awake()
    {
        if (jogador == null)
        {
            jogador = FindAnyObjectByType<AplicacaoPlayerController>();
        }

        estado = new bool[chips != null ? chips.Length : 0];
        for (int i = 0; i < estado.Length; i++)
        {
            Pintar(chips[i], false);
        }
    }

    private void Update()
    {
        if (jogador == null || chips == null)
        {
            return;
        }

        for (int i = 0; i < chips.Length; i++)
        {
            bool tem = jogador.HasTool(chips[i].ferramenta);
            if (tem != estado[i])
            {
                estado[i] = tem;
                Pintar(chips[i], tem);
            }
        }
    }

    private void Pintar(Chip chip, bool aceso)
    {
        Color cor = ToolColors.For(chip.ferramenta);
        if (chip.fundo != null) chip.fundo.color = aceso ? new Color(cor.r, cor.g, cor.b, 0.25f) : fundoApagado;
        if (chip.borda != null) chip.borda.color = aceso ? cor : bordaApagada;
        if (chip.ponto != null) chip.ponto.color = aceso ? cor : pontoApagado;
        if (chip.nome != null) chip.nome.color = aceso ? textoAceso : textoApagado;
    }
}
