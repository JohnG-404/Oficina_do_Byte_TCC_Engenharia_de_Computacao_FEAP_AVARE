using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vida do jogador mostrada como uma fileira de quadrados (um por ponto de
/// vida) em vez de uma barra contínua — usado na cena "Interacao_Vida".
/// Cada quadrado é um Image comum (sem sprite): cheio = colorido, vazio =
/// apagado.
///
/// "Conectada" (padrão) = redesenha toda vez que o PlayerHealth avisa que a
/// vida mudou (evento OnHealthChanged). A mini-lição de HUD da
/// Interacao_Vida (ver HudLicao) desconecta a barra de propósito no começo:
/// ela desenha só o valor inicial e fica parada até o aluno montar o aviso.
/// </summary>
public class HeartHealthUI : MonoBehaviour
{
    [SerializeField] private Image[] hearts;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Color fullColor = new Color(0.9f, 0.2f, 0.25f);
    [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.15f);

    [Tooltip("Desligado = a barra desenha só o valor inicial e ignora as mudanças de vida. Usado pela mini-lição de HUD (ver HudLicao), que liga de volta com Conectar(). Padrão: ligado.")]
    [SerializeField] private bool conectada = true;

    /// <summary>Se a barra está ouvindo o OnHealthChanged do PlayerHealth.</summary>
    public bool Conectada => conectada;

    /// <summary>Vida que os quadrados estão mostrando agora (pode estar desatualizada se a barra estiver desconectada).</summary>
    public int ValorMostrado { get; private set; }

    // Quadrados antigos (Images lisos, sem sprite) ganham a placa VIDA e os
    // sprites do tema Laboratório (ver TemaLabFases). A Interacao_Vida já
    // vem com sprites e fica como está. Salvo na cena quando a placa já foi
    // montada no Editor (Tools > Tema Lab > Salvar tema das fases nas cenas).
    [SerializeField, HideInInspector] private bool usarSpritesDoTema;

    private void Awake()
    {
        if (playerHealth == null)
        {
            playerHealth = FindAnyObjectByType<PlayerHealth>();
        }

        if (hearts != null && hearts.Length > 0 && hearts[0] != null && hearts[0].sprite == null)
        {
            usarSpritesDoTema = TemaLabFases.EstilizarVida(transform as RectTransform, hearts);
        }
    }

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += Refresh;
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= Refresh;
        }
    }

    private void Start()
    {
        // O valor inicial é desenhado mesmo desconectada: a barra "começa
        // certa" e só depois deixa de acompanhar.
        if (playerHealth != null)
        {
            Draw(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }
    }

    /// <summary>Para de acompanhar a vida: os quadrados ficam como estão.</summary>
    public void Desconectar()
    {
        conectada = false;
    }

    /// <summary>Volta a acompanhar a vida e já redesenha com o valor real.</summary>
    public void Conectar()
    {
        conectada = true;
        if (playerHealth != null)
        {
            Draw(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }
    }

    // "Aumentar a vida máxima": se o máximo passar do número de quadrados,
    // cria cópias do último, no mesmo espaçamento entre os dois últimos.
    private void GarantirQuadrados(int max)
    {
        if (hearts == null || hearts.Length == 0 || max <= hearts.Length || hearts[hearts.Length - 1] == null)
        {
            return;
        }

        var lista = new System.Collections.Generic.List<Image>(hearts);
        RectTransform ultimo = hearts[hearts.Length - 1].rectTransform;
        Vector2 passo = hearts.Length >= 2 && hearts[hearts.Length - 2] != null
            ? ultimo.anchoredPosition - hearts[hearts.Length - 2].rectTransform.anchoredPosition
            : new Vector2(ultimo.rect.width + 8f, 0f);
        bool temLayout = ultimo.parent != null && ultimo.parent.GetComponent<LayoutGroup>() != null;

        while (lista.Count < max)
        {
            Image novo = Instantiate(lista[lista.Count - 1], ultimo.parent);
            novo.name = "Vida" + (lista.Count + 1);
            if (!temLayout)
            {
                novo.rectTransform.anchoredPosition = lista[lista.Count - 1].rectTransform.anchoredPosition + passo;
            }
            lista.Add(novo);
        }

        int adicionados = lista.Count - hearts.Length;
        hearts = lista.ToArray();

        // A placa escura do HUD (PlacaVida) estica com o pai: alarga o pai um
        // passo por quadrado novo, se ele não tiver layout próprio.
        RectTransform pai = ultimo.parent as RectTransform;
        if (!temLayout && pai != null && pai.GetComponent<ContentSizeFitter>() == null && pai.rect.width > 0f)
        {
            pai.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, pai.rect.width + passo.x * adicionados);
        }
    }

    private void Refresh(int current, int max)
    {
        if (!conectada)
        {
            return;
        }

        Draw(current, max);
    }

    private void Draw(int current, int max)
    {
        ValorMostrado = current;
        GarantirQuadrados(max);
        for (int i = 0; i < hearts.Length; i++)
        {
            if (hearts[i] == null)
            {
                continue;
            }

            if (usarSpritesDoTema)
            {
                TemaLaboratorio tema = TemaLabUI.Tema;
                hearts[i].sprite = i < current ? tema.vidaCheia : (tema.vidaVazia != null ? tema.vidaVazia : tema.vidaCheia);
                hearts[i].color = i < current || tema.vidaVazia != null ? Color.white : emptyColor;
                continue;
            }

            hearts[i].color = i < current ? fullColor : emptyColor;
        }
    }
}
