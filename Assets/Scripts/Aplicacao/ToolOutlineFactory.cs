using UnityEngine;

/// <summary>
/// Cria o "anel" visual — uma cópia do sprite de um objeto, maior e por
/// trás dele, numa cor lisa — que confirma pro jogador qual ferramenta foi
/// aplicada ali. Começa desligado (<see cref="SpriteRenderer.enabled"/> =
/// false); quem chama liga o anel certo quando a ferramenta correspondente
/// é aplicada.
///
/// Compartilhado entre PhysicsTarget (objetos do cenário) e
/// AplicacaoPlayerController (o próprio Player, que recebe as 4
/// ferramentas) — sem isso, cada um reimplementaria a mesma conta de
/// escala/padding separadamente.
/// </summary>
public static class ToolOutlineFactory
{
    // worldPadding é a espessura da borda em unidades de MUNDO, não um
    // multiplicador de escala — objetos da fase costumam ter escala
    // não-uniforme (ex: uma plataforma comprida e baixa), e multiplicar a
    // escala igual nos dois eixos faz a borda ficar grossa nas pontas e fina
    // em cima/embaixo. Compensando pela lossyScale do próprio objeto, a
    // borda fica com a mesma espessura visual em qualquer proporção.
    public static SpriteRenderer Create(SpriteRenderer source, string outlineName, float worldPadding, Color color, int orderOffset)
    {
        if (source == null)
        {
            return null;
        }

        var go = new GameObject(outlineName);
        go.transform.SetParent(source.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        Vector3 lossyScale = source.transform.lossyScale;
        float scaleX = Mathf.Approximately(lossyScale.x, 0f) ? 1f : Mathf.Abs(lossyScale.x);
        float scaleY = Mathf.Approximately(lossyScale.y, 0f) ? 1f : Mathf.Abs(lossyScale.y);
        go.transform.localScale = new Vector3(
            (scaleX + worldPadding * 2f) / scaleX,
            (scaleY + worldPadding * 2f) / scaleY,
            1f);

        var outlineRenderer = go.AddComponent<SpriteRenderer>();
        outlineRenderer.sprite = source.sprite;
        outlineRenderer.color = color;
        outlineRenderer.sortingLayerID = source.sortingLayerID;
        outlineRenderer.sortingOrder = source.sortingOrder + orderOffset;
        outlineRenderer.enabled = false;
        return outlineRenderer;
    }
}
