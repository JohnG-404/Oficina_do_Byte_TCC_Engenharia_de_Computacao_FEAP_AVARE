using TMPro;
using UnityEngine;

/// <summary>
/// Dica de tecla de uma ação (agachar, puxar a alavanca, abrir a lâmpada...).
/// Mostra sempre a tecla atual e atualiza sozinha se o jogador trocar a
/// tecla nas configurações.
///
/// Balão (padrão): o texto solto no cenário fica escondido e quem aparece é
/// o balão compartilhado (<see cref="BalaoTecla"/>) sobre a cabeça do
/// jogador, com a tecla, a ação e o detalhe.
/// - Zona <see cref="Zona.AntesDoObstaculo"/>: o balão aparece de
///   <see cref="distanciaAntes"/> antes da borda esquerda do obstáculo (os
///   renderers do objeto pai) até essa borda, e some quando o jogador entra.
/// - Zona <see cref="Zona.PeloObjeto"/>: o objeto dono (alavanca, lâmpada)
///   decide quando mostrar, chamando <see cref="Mostrar"/>.
///
/// Com o balão desligado, volta ao texto fixo no cenário ("Aperte C para agachar").
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class DicaTecla : MonoBehaviour
{
    public enum Zona
    {
        AntesDoObstaculo,
        PeloObjeto,
    }

    [Tooltip("Nome da ação no InputSystem_Actions (mapa Player), ex.: Crouch.")]
    [SerializeField] private string acao = "Crouch";
    [Tooltip("Texto com {0} no lugar da tecla (usado só sem o balão).")]
    [SerializeField] private string formato = "Aperte {0} para agachar";

    [Header("Balão perto do jogador")]
    [SerializeField] private bool usarBalao = true;
    [SerializeField] private string rotulo = "agachar";
    [SerializeField] private string detalhe = "segure para passar";
    [Tooltip("AntesDoObstaculo: zona antes da borda do obstáculo pai. PeloObjeto: o dono (alavanca, lâmpada) chama Mostrar.")]
    [SerializeField] private Zona zona = Zona.AntesDoObstaculo;
    [Tooltip("Quantas unidades antes da borda do obstáculo o balão começa a aparecer.")]
    [SerializeField] private float distanciaAntes = 3.2f;
    [Tooltip("O balão some um pouco antes de o jogador entrar no obstáculo (o teto é baixo).")]
    [SerializeField] private float folgaNaBorda = 0.15f;
    [Tooltip("Diferença de altura máxima entre o jogador e a dica para o balão aparecer.")]
    [SerializeField] private float alturaMaxima = 2.5f;

    private TMP_Text texto;
    private Transform jogador;
    private float bordaObstaculo;
    private bool pedidoPeloDono;

    public string Acao => acao;

    private void Awake()
    {
        texto = GetComponent<TMP_Text>();
        if (usarBalao)
        {
            texto.enabled = false;
            bordaObstaculo = CalcularBordaObstaculo();
        }
    }

    private void OnEnable()
    {
        if (InputManager.Instance != null) InputManager.Instance.OnBindingsChanged += Atualizar;
        Atualizar();
    }

    private void Start()
    {
        if (usarBalao && zona == Zona.AntesDoObstaculo)
        {
            var agachar = FindAnyObjectByType<PlayerAgachar>();
            GameObject p = agachar != null ? agachar.gameObject : GameObject.FindGameObjectWithTag("Player");
            if (p != null) jogador = p.transform;
        }
        Atualizar();
    }

    private void OnDisable()
    {
        if (InputManager.Instance != null) InputManager.Instance.OnBindingsChanged -= Atualizar;
        pedidoPeloDono = false;
        BalaoTecla.Soltar(this);
    }

    /// <summary>Zona PeloObjeto: o dono liga/desliga o balão (ex.: jogador perto da alavanca).</summary>
    public void Mostrar(bool mostrar)
    {
        pedidoPeloDono = mostrar;
    }

    private string Tecla()
    {
        string tecla = InputManager.Instance != null ? InputManager.Instance.GetBindingName(acao) : "";
        return string.IsNullOrEmpty(tecla) ? "?" : tecla;
    }

    private void Atualizar()
    {
        if (texto == null) texto = GetComponent<TMP_Text>();
        texto.text = string.Format(formato, Tecla());
    }

    // Borda esquerda do obstáculo: os renderers do objeto pai (ex.: PassagemAgachar).
    private float CalcularBordaObstaculo()
    {
        float borda = transform.position.x;
        bool achou = false;
        if (transform.parent != null)
        {
            foreach (Renderer r in transform.parent.GetComponentsInChildren<Renderer>(true))
            {
                if (r.transform == transform) continue;
                borda = achou ? Mathf.Min(borda, r.bounds.min.x) : r.bounds.min.x;
                achou = true;
            }
        }
        return achou ? borda : transform.position.x - 1f;
    }

    private void LateUpdate()
    {
        if (!usarBalao)
        {
            return;
        }

        bool mostrar;
        if (zona == Zona.PeloObjeto)
        {
            mostrar = pedidoPeloDono;
        }
        else if (jogador == null)
        {
            mostrar = false;
        }
        else
        {
            Vector3 pos = jogador.position;
            mostrar = pos.x >= bordaObstaculo - distanciaAntes
                && pos.x <= bordaObstaculo - folgaNaBorda
                && Mathf.Abs(pos.y - transform.position.y) <= alturaMaxima;
        }

        BalaoTecla.Definir(this, mostrar, acao, rotulo, detalhe);
    }
}
