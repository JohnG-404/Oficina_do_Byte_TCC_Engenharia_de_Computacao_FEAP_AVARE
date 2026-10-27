using UnityEngine;

/// <summary>
/// Anima o visor do Guarda e do Aliado (cópias vermelha e verde do visor do
/// Player): quadros de "parado" quando está quieto e de "correr" quando anda.
/// Fica no filho Visual; a velocidade vem do quanto ele andou no mundo, então
/// não precisa de Animator nem mexe na lógica do GuardaIA/AliadoIA (que
/// continuam virando o sprite e fazendo o "soco").
/// Sprites ligados pelo Tools > IA > Salvar visor do guarda e do aliado.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class NPCVisorAnimador : MonoBehaviour
{
    [SerializeField] private Sprite[] parado;
    [SerializeField] private Sprite[] correr;
    [SerializeField] private float quadrosPorSegundoParado = 8f;
    [SerializeField] private float quadrosPorSegundoCorrer = 12f;
    [Tooltip("Velocidade horizontal mínima (unidades/s) pra usar os quadros de correr.")]
    [SerializeField] private float limiarCorrida = 0.3f;

    private SpriteRenderer spriteRenderer;
    private float ultimoX;
    private float tempo;
    private bool correndo;

    public void Configurar(Sprite[] quadrosParado, Sprite[] quadrosCorrer)
    {
        parado = quadrosParado;
        correr = quadrosCorrer;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        ultimoX = transform.position.x;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return; // cérebro aberto (jogo pausado): congela o quadro
        }

        float x = transform.position.x;
        bool agoraCorrendo = Mathf.Abs(x - ultimoX) / dt > limiarCorrida;
        ultimoX = x;
        if (agoraCorrendo != correndo)
        {
            correndo = agoraCorrendo;
            tempo = 0f;
        }

        Sprite[] quadros = correndo && correr != null && correr.Length > 0 ? correr : parado;
        if (quadros == null || quadros.Length == 0)
        {
            return;
        }

        tempo += dt;
        float fps = correndo ? quadrosPorSegundoCorrer : quadrosPorSegundoParado;
        int i = Mathf.FloorToInt(tempo * fps) % quadros.Length;
        spriteRenderer.sprite = quadros[i];
    }
}
