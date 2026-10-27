using System.Collections;
using UnityEngine;

/// <summary>
/// Queda no vão do chão (Interacao e Interacao_Vida): quando o jogador passa
/// abaixo de alturaLimite (linha vermelha na Scene, um pouco abaixo do chão
/// mais baixo da fase), a tela escurece rápido, ele volta ao último chão
/// firme (InteracaoPlayerController.VoltarAoPontoSeguro, o mesmo dos
/// espinhos) e aparece o aviso. Registra "queda" no CSV, como a Aplicacao.
///
/// Na Interacao_Vida (fase com Game Over), a queda segue o fluxo dos espinhos
/// daquela fase: tira 1 de vida; se zerar, o Game Over assume.
/// </summary>
public class QuedaNoVao : MonoBehaviour
{
    [Tooltip("Abaixo desta altura (Y do mundo) o jogador conta como caído. Linha vermelha na Scene.")]
    [SerializeField] private float alturaLimite = -8f;
    [SerializeField] private InteracaoPlayerController jogador;

    [Header("Véu escuro (filho VeuQueda)")]
    [SerializeField] private CanvasGroup veu;
    [SerializeField] private float escurecer = 0.15f;
    [SerializeField] private float segurarEscuro = 0.12f;
    [SerializeField] private float clarear = 0.3f;

    [SerializeField] private string aviso = "Você caiu! Voltando ao ponto seguro…";

    [Tooltip("Tira vida na queda. Vazio/false = decide sozinho: só onde há Game Over (Interacao_Vida).")]
    [SerializeField] private bool tirarVida;
    [SerializeField] private int dano = 1;

    private bool emQueda;
    private int quedas;

    public float AlturaLimite => alturaLimite;

    private void Awake()
    {
        if (jogador == null) jogador = FindAnyObjectByType<InteracaoPlayerController>();
        if (veu != null)
        {
            veu.alpha = 0f;
            veu.blocksRaycasts = false;
        }
        if (!tirarVida) tirarVida = FindAnyObjectByType<GameOverUI>() != null;
    }

    private void Update()
    {
        if (!emQueda && jogador != null && jogador.transform.position.y < alturaLimite)
        {
            StartCoroutine(Queda());
        }
    }

    private IEnumerator Queda()
    {
        emQueda = true;
        quedas++;
        RegistroAprendizagem.RegistrarFalha("queda", "jogador | " + quedas + "ª");

        var rb = jogador.GetComponent<Rigidbody2D>();
        yield return Fade(0f, 1f, escurecer);

        // Parado no escuro enquanto troca de lugar (sem continuar caindo).
        if (rb != null) rb.linearVelocity = Vector2.zero;
        jogador.VoltarAoPontoSeguro();

        if (tirarVida)
        {
            var vida = jogador.GetComponent<PlayerHealth>();
            if (vida != null) vida.TakeDamage(dano);
        }

        InteractionConfigUI.Instance?.ShowHint(aviso);
        yield return new WaitForSecondsRealtime(segurarEscuro);
        yield return Fade(1f, 0f, clarear);
        emQueda = false;
    }

    private IEnumerator Fade(float de, float para, float duracao)
    {
        if (veu == null) yield break;
        float t = 0f;
        while (t < duracao)
        {
            t += Time.unscaledDeltaTime;
            veu.alpha = Mathf.Lerp(de, para, t / duracao);
            yield return null;
        }
        veu.alpha = para;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.9f);
        Gizmos.DrawLine(new Vector3(-200f, alturaLimite, 0f), new Vector3(200f, alturaLimite, 0f));
    }
}
