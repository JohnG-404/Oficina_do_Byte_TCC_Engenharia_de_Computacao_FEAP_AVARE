using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// IA_Aplicacao: clicar no Guarda ou no Aliado abre o painel com o
/// diagrama do cérebro dele (IADiagramaUI). Qualquer personagem com um
/// componente <see cref="ICerebroConfiguravel"/> no próprio objeto (ou num
/// pai) pode ser clicado.
///
/// Não abre durante diálogos, com o painel já aberto, nem com o mouse em
/// cima de outra UI.
/// </summary>
public class IASeletorNPC : MonoBehaviour
{
    [Tooltip("Câmera usada pra converter o clique em posição no mundo (vazio = Camera.main).")]
    [SerializeField] private Camera cameraJogo;

    [Tooltip("Folga, em unidades de mundo, em volta do ponto clicado (personagens são pequenos).")]
    [SerializeField] private float raioDoClique = 0.35f;

    /// <summary>Liga/desliga o clique (o fluxo da fase desliga no fim, por exemplo).</summary>
    public bool Ativo { get; set; } = true;

    private void Update()
    {
        if (!Ativo || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (IADiagramaUI.EstaAberto || IADiagramaUI.FrameFechado == Time.frameCount || DialogueManager.IsAnyDialogueActive)
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Camera cam = cameraJogo != null ? cameraJogo : Camera.main;
        if (cam == null)
        {
            return;
        }

        Vector2 tela = Mouse.current.position.ReadValue();
        Vector3 mundo = cam.ScreenToWorldPoint(new Vector3(tela.x, tela.y, -cam.transform.position.z));

        ICerebroConfiguravel escolhido = null;
        float melhor = float.MaxValue;
        foreach (Collider2D col in Physics2D.OverlapCircleAll(mundo, raioDoClique))
        {
            ICerebroConfiguravel cerebro = col.GetComponentInParent<ICerebroConfiguravel>();
            if (cerebro == null)
            {
                continue;
            }

            float d = Vector2.Distance(col.bounds.center, mundo);
            if (d < melhor)
            {
                melhor = d;
                escolhido = cerebro;
            }
        }

        if (escolhido != null)
        {
            IADiagramaUI.Abrir(escolhido);
        }
    }
}
