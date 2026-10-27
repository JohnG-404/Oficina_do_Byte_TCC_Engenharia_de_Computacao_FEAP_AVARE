using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ponto único de troca de cena do jogo. Todo lugar que antes chamava
/// SceneManager.LoadScene chama TrocaDeCena.Carregar: a cena atual vira a
/// grade do editor a partir da porta de saída, aparece o cartão com a
/// próxima fase e a grade se desfaz a partir do jogador na cena nova (ver
/// TransicaoCompilar e o prefab Assets/Resources/TransicaoCompilar.prefab).
///
/// Recarregar a mesma cena (morte, reinício) usa a versão curta, sem cartão.
/// Sem o prefab, cai no LoadScene seco de antes.
/// </summary>
public static class TrocaDeCena
{
    /// <summary>True enquanto uma transição roda; novos pedidos são ignorados.</summary>
    public static bool EmAndamento => TransicaoCompilar.EmAndamento;

    public static void Carregar(string cena)
    {
        if (string.IsNullOrEmpty(cena) || EmAndamento)
        {
            return;
        }

        TransicaoCompilar transicao = TransicaoCompilar.Garantir();
        if (transicao == null)
        {
            SceneManager.LoadScene(cena);
            return;
        }

        transicao.Iniciar(cena, cena == SceneManager.GetActiveScene().name);
    }

    public static void Recarregar() => Carregar(SceneManager.GetActiveScene().name);
}
