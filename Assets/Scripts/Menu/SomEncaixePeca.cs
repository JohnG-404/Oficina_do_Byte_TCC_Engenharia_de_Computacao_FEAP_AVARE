using UnityEngine;

/// <summary>
/// Som de "encaixe" das peças da oficina da Tutorial_Anim (SequencePiece),
/// sem mexer na cena nem no puzzle: adicionado em runtime pelo AudioManager
/// e só observa a peça. Quando ela para num lugar novo fora da bandeja (solta
/// num encaixe ou mandada pra ele com um clique), toca o encaixe.
/// </summary>
[DisallowMultipleComponent]
public class SomEncaixePeca : MonoBehaviour
{
    private const float Tolerancia = 0.01f;

    private SequencePiece peca;
    private Vector3 posicaoNaBandeja;
    private Vector3 ultimaPosicao;

    public static void Garantir(SequencePiece peca)
    {
        if (!peca.TryGetComponent(out SomEncaixePeca _))
        {
            peca.gameObject.AddComponent<SomEncaixePeca>();
        }
    }

    private void Awake()
    {
        peca = GetComponent<SequencePiece>();
        posicaoNaBandeja = transform.position;
        ultimaPosicao = posicaoNaBandeja;
    }

    private void LateUpdate()
    {
        if (peca == null || peca.IsDragging)
        {
            return;
        }

        Vector3 atual = transform.position;
        if ((atual - ultimaPosicao).sqrMagnitude < Tolerancia * Tolerancia)
        {
            return;
        }

        ultimaPosicao = atual;
        if ((atual - posicaoNaBandeja).sqrMagnitude > Tolerancia * Tolerancia)
        {
            AudioManager.TocarEfeito(AudioManager.Efeito.Encaixe);
        }
    }
}
