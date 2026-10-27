using TMPro;
using UnityEngine;

/// <summary>
/// Ícone acima da cabeça de um personagem com IA mostrando o estado atual:
/// "..." sem cérebro, "?" patrulhando, "!" perseguindo ou atacando. É uma
/// ferramenta de depuração de verdade (jogos usam isso pra testar IA) e
/// ajuda a ligar "o que eu vejo" com "em que estado ele está".
///
/// Funciona com o Guarda (GuardaIA) ou com o Aliado (AliadoIA, mostra o
/// nome do estado). Com <see cref="mostrarNome"/>, o guarda também mostra o
/// nome do estado e quem ele está perseguindo.
/// </summary>
public class IAIconeEstado : MonoBehaviour
{
    [SerializeField] private GuardaIA guarda;
    [SerializeField] private AliadoIA aliado;
    [SerializeField] private TMP_Text texto;

    [Tooltip("Mostra o nome do estado junto do ícone (ex: '! Perseguir → Aliado').")]
    [SerializeField] private bool mostrarNome;

    [Header("Cores")]
    [SerializeField] private Color corSemCerebro = new Color32(0x9A, 0xA8, 0xC4, 0xFF);
    [SerializeField] private Color corPatrulha = new Color32(0xF5, 0xC5, 0x42, 0xFF);
    [SerializeField] private Color corAlerta = new Color32(0xE5, 0x64, 0x6A, 0xFF);
    [SerializeField] private Color corAliado = new Color32(0x52, 0xC9, 0x8A, 0xFF);

    [Header("Balanço")]
    [SerializeField] private float amplitude = 0.06f;
    [SerializeField] private float frequencia = 3f;

    private Vector3 posicaoBase;

    private void Awake()
    {
        if (texto == null)
        {
            texto = GetComponent<TMP_Text>();
        }

        posicaoBase = transform.localPosition;

        // Cores do tema Laboratório (as salvas nas cenas eram as antigas).
        corSemCerebro = TemaLabUI.TextoSuave;
        corPatrulha = TemaLabUI.Dourado;
        corAlerta = TemaLabUI.Perigo;
        corAliado = TemaLabUI.Verde;
    }

    private void LateUpdate()
    {
        if (texto == null)
        {
            return;
        }

        if (guarda != null)
        {
            AtualizarGuarda();
        }
        else if (aliado != null)
        {
            texto.text = aliado.EstadoAtual.ToString();
            texto.color = corAliado;
        }

        transform.localPosition = posicaoBase + Vector3.up * (Mathf.Sin(Time.time * frequencia) * amplitude);
    }

    private void AtualizarGuarda()
    {
        GuardaIA.Estado estado = guarda.EstadoAtual;
        string icone;
        Color cor;

        switch (estado)
        {
            case GuardaIA.Estado.Patrulhar:
                icone = "?";
                cor = corPatrulha;
                break;
            case GuardaIA.Estado.Perseguir:
            case GuardaIA.Estado.Atacar:
                icone = "!";
                cor = corAlerta;
                break;
            default:
                icone = "...";
                cor = corSemCerebro;
                break;
        }

        if (mostrarNome && estado != GuardaIA.Estado.SemCerebro)
        {
            string quem = "";
            if (guarda.AlvoAtual != null && estado != GuardaIA.Estado.Patrulhar)
            {
                quem = guarda.AlvoAtual.CompareTag("Player") ? " -> Player" : " -> " + guarda.AlvoAtual.gameObject.tag;
            }

            icone = $"{icone} {estado}{quem}";
        }

        texto.text = icone;
        texto.color = cor;
    }
}
