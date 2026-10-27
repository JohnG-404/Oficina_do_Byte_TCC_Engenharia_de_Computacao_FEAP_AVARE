using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Crachá do Menu: mostra quem está jogando ("Jogando como Lia") e abre a
/// janela Novo jogador pra trocar. Antes de alguém ser escolhido, mostra
/// "Quem vai jogar?". Ver Jogadores e NovoJogadorUI.
/// </summary>
[RequireComponent(typeof(Button))]
public class CrachaJogadorUI : MonoBehaviour
{
    [SerializeField] private TMP_Text rotulo;
    [SerializeField] private TMP_Text nome;
    [SerializeField] private TMP_Text acao;
    [SerializeField] private TMP_Text selo;

    [Header("Textos")]
    [SerializeField] private string rotuloComJogador = "Jogando como";
    [SerializeField] private string rotuloSemJogador = "Quem vai jogar?";
    [SerializeField] private string nomeSemJogador = "Ninguém escolhido";
    [SerializeField] private string acaoComJogador = "Trocar";
    [SerializeField] private string acaoSemJogador = "Escolher";

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() => NovoJogadorUI.Abrir());
    }

    private void OnEnable()
    {
        Atualizar();
        Jogadores.OnMudou += Atualizar;
    }

    private void OnDisable()
    {
        Jogadores.OnMudou -= Atualizar;
    }

    private void Atualizar()
    {
        Jogadores.Jogador j = Jogadores.Atual;
        if (rotulo != null) rotulo.text = j != null ? rotuloComJogador : rotuloSemJogador;
        if (nome != null) nome.text = j != null ? j.Nome : nomeSemJogador;
        if (acao != null) acao.text = j != null ? acaoComJogador : acaoSemJogador;
        if (selo != null)
        {
            if (j == null) selo.text = "?";
            else if (j.anonimo) selo.text = j.numero.ToString("00");
            else selo.text = j.Nome.Substring(0, 1).ToUpperInvariant();
        }
    }
}
