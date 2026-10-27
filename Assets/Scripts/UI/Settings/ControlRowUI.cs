using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Componente de uma linha da lista de controles (ex: "Pular [Space]").
/// Reutilizável: cada instância é configurada em runtime por ControlsTabUI
/// através de Configure(), não possui referências fixas de cena.
///
/// Enquanto espera a nova tecla, a linha fica em destaque com a dica de como
/// cancelar. A troca é cancelada com Esc, com um novo clique na caixa da tecla
/// ou quando a linha some (Voltar, fechar as Configurações, trocar de aba), para
/// a ação nunca ficar desligada esperando uma tecla escondida.
///
/// Se a tecla já era de outra ação, as duas trocam e cada linha mostra um aviso
/// curto ao lado do nome (ex: "trocou com Interagir", "agora usa W").
/// </summary>
[RequireComponent(typeof(Button))]
public class ControlRowUI : MonoBehaviour
{
    private const string WaitingForInputText = "Aperte a tecla";
    private const string CancelHintText = "  <size=73%><color=#9AA8C4>Esc ou clique de novo para cancelar</color></size>";
    private const string AvisoFormato = "  <size=73%><color=#58D68D>{0}</color></size>";
    private const float AvisoDuracao = 2.5f;

    [Header("Referências de UI (arraste no Inspector do prefab)")]
    [SerializeField] private TMP_Text actionLabel;
    [SerializeField] private TMP_Text bindingLabel;
    [SerializeField] private Button rebindButton;

    [Tooltip("Sprite da caixa da tecla enquanto espera a nova tecla (btn_primario_normal).")]
    [SerializeField] private Sprite spriteEsperando;

    private string actionName;
    private string compositePart;
    private string displayName;

    private bool esperandoTecla;
    private int frameInicioTroca = -1;
    private SpriteState spritesOriginais;
    private LinhaFocoUI linhaFoco;
    private bool avisoNaTela;

    private void Awake()
    {
        if (rebindButton == null)
        {
            rebindButton = GetComponent<Button>();
        }

        rebindButton.onClick.AddListener(OnRebindButtonClicked);
        linhaFoco = GetComponent<LinhaFocoUI>();

        // O botão fica desativado durante a troca (assim Enter/Espaço viram a
        // tecla nova em vez de "clicar"), mas um clique do mouse nele ainda
        // chega aqui e cancela a troca.
        var clique = rebindButton.gameObject.AddComponent<CliqueCancelaTroca>();
        clique.linha = this;
    }

    private void OnEnable()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnBindingsChanged += RefreshBindingText;
            InputManager.Instance.OnAvisoLinha += ReceberAviso;
        }
    }

    private void OnDisable()
    {
        // Linha sumiu (Voltar, Esc, outra aba): não deixa a troca esperando.
        if (esperandoTecla)
        {
            InputManager.Instance?.CancelActiveRebind();
        }

        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnBindingsChanged -= RefreshBindingText;
            InputManager.Instance.OnAvisoLinha -= ReceberAviso;
        }

        // A corrotina do aviso para junto com a linha: volta o nome ao normal.
        if (avisoNaTela)
        {
            EsconderAviso();
        }
    }

    /// <summary>Configura esta linha para representar a ação descrita em info.</summary>
    public void Configure(RebindActionInfo info)
    {
        actionName = info.actionName;
        compositePart = info.compositePart;
        displayName = info.displayName;

        if (actionLabel != null)
        {
            actionLabel.text = displayName;
        }

        RefreshBindingText();
    }

    private void OnRebindButtonClicked()
    {
        if (InputManager.Instance == null || string.IsNullOrEmpty(actionName))
        {
            return;
        }

        StopAllCoroutines();
        if (avisoNaTela)
        {
            EsconderAviso();
        }
        MostrarEsperando(true);
        frameInicioTroca = Time.frameCount;

        InputManager.Instance.StartRebind(
            actionName,
            compositePart,
            onComplete: newBindingDisplayName =>
            {
                MostrarEsperando(false);
                if (bindingLabel != null)
                {
                    bindingLabel.text = newBindingDisplayName;
                }
            },
            onCancel: () =>
            {
                MostrarEsperando(false);
                RefreshBindingText();
            });
    }

    /// <summary>Clique do mouse na caixa da tecla enquanto ela espera: cancela.</summary>
    private void CliqueDuranteTroca()
    {
        // O mesmo clique que começou a troca também passa por aqui.
        if (!esperandoTecla || Time.frameCount == frameInicioTroca)
        {
            return;
        }

        InputManager.Instance?.CancelActiveRebind();
    }

    private void MostrarEsperando(bool esperando)
    {
        if (esperando == esperandoTecla)
        {
            return;
        }

        esperandoTecla = esperando;

        if (esperando)
        {
            // A caixa desativada usa o sprite de "disabled": troca por um azul só durante a espera.
            spritesOriginais = rebindButton.spriteState;
            if (spriteEsperando != null)
            {
                SpriteState s = spritesOriginais;
                s.disabledSprite = spriteEsperando;
                rebindButton.spriteState = s;
            }

            rebindButton.interactable = false;

            if (bindingLabel != null)
            {
                bindingLabel.text = WaitingForInputText;
            }
        }
        else
        {
            rebindButton.spriteState = spritesOriginais;
            rebindButton.interactable = true;
        }

        if (actionLabel != null)
        {
            actionLabel.text = esperando ? displayName + CancelHintText : displayName;
        }

        if (linhaFoco != null)
        {
            linhaFoco.ForcarFoco = esperando;
        }
    }

    private void ReceberAviso(string acao, string parte, string texto)
    {
        if (acao != actionName || !string.Equals(parte ?? "", compositePart ?? "") || !isActiveAndEnabled || esperandoTecla)
        {
            return;
        }

        StopAllCoroutines();
        StartCoroutine(MostrarAviso(texto));
    }

    /// <summary>Aviso ao lado do nome, com a linha em destaque, por alguns segundos.</summary>
    private System.Collections.IEnumerator MostrarAviso(string texto)
    {
        avisoNaTela = true;
        if (actionLabel != null)
        {
            actionLabel.text = displayName + string.Format(AvisoFormato, texto);
        }
        if (linhaFoco != null)
        {
            linhaFoco.ForcarFoco = true;
        }

        yield return new WaitForSecondsRealtime(AvisoDuracao);
        EsconderAviso();
    }

    private void EsconderAviso()
    {
        avisoNaTela = false;
        if (actionLabel != null)
        {
            actionLabel.text = displayName;
        }
        if (linhaFoco != null && !esperandoTecla)
        {
            linhaFoco.ForcarFoco = false;
        }
    }

    private void RefreshBindingText()
    {
        if (bindingLabel == null || InputManager.Instance == null || string.IsNullOrEmpty(actionName))
        {
            return;
        }

        bindingLabel.text = InputManager.Instance.GetBindingName(actionName, compositePart);
    }

    /// <summary>Fica no botão da tecla: recebe o clique mesmo com o botão desativado.</summary>
    private class CliqueCancelaTroca : MonoBehaviour, IPointerClickHandler
    {
        public ControlRowUI linha;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (linha != null && eventData.button == PointerEventData.InputButton.Left)
            {
                linha.CliqueDuranteTroca();
            }
        }
    }
}
