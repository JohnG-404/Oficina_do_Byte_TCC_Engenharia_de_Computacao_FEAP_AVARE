using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marca um painel fixo no alto da tela (placa de VIDA, painel "Animator do
/// Player"...). Quando a caixa de diálogo do Prof precisa subir para o topo
/// (ver DialogueManager.PositionBoxAwayFromPlayer), ela desce o bastante para
/// passar por baixo desses painéis, em vez de ficar escondida atrás deles ou
/// cobrir só um pedaço deles.
///
/// Os painéis são montados em runtime (TemaLabFases.EstilizarVida,
/// AnimatorEstadoHUD), então quem monta acrescenta este componente.
/// </summary>
[DisallowMultipleComponent]
public class AreaFixaNoTopo : MonoBehaviour
{
    private static readonly List<RectTransform> ativas = new List<RectTransform>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => ativas.Clear();

    /// <summary>Painéis marcados que estão na tela agora.</summary>
    public static IReadOnlyList<RectTransform> Ativas => ativas;

    private void OnEnable()
    {
        if (transform is RectTransform rt && !ativas.Contains(rt))
        {
            ativas.Add(rt);
        }
    }

    private void OnDisable()
    {
        ativas.Remove(transform as RectTransform);
    }

    /// <summary>
    /// Retângulo do painel em pixels da tela (origem embaixo à esquerda).
    /// Devolve false se o painel não estiver visível.
    /// </summary>
    public static bool RetanguloNaTela(RectTransform rt, out Rect tela)
    {
        tela = default;
        if (rt == null || !rt.gameObject.activeInHierarchy)
        {
            return false;
        }

        Canvas canvas = rt.GetComponentInParent<Canvas>();
        if (canvas == null || !canvas.enabled)
        {
            return false;
        }

        CanvasGroup grupo = rt.GetComponentInParent<CanvasGroup>();
        if (grupo != null && grupo.alpha < 0.05f)
        {
            return false;
        }

        Camera cam = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
        var cantos = new Vector3[4];
        rt.GetWorldCorners(cantos);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, cantos[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, cantos[2]);
        tela = Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        return true;
    }
}
