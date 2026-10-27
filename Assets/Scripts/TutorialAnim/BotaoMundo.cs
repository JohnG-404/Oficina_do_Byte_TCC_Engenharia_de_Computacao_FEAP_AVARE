using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Botão feito de sprite no espaço de mundo (Tocar e Dica da Oficina de
/// Animação). Usa OnMouse, igual às peças do puzzle, pra que o clique no
/// botão e o arrasto das peças funcionem do mesmo jeito. Escurece um pouco
/// com o mouse em cima e afunda ao apertar. Desligado (Interativo = false),
/// fica apagado e ignora o mouse.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BotaoMundo : MonoBehaviour
{
    [SerializeField] private SpriteRenderer fundo;
    [SerializeField] private Color corNormal = Color.white;
    [SerializeField] private Color corHover = new Color(0.85f, 0.88f, 0.95f, 1f);
    [SerializeField] private UnityEvent aoClicar = new UnityEvent();

    private const float AlfaApagado = 0.4f;

    private Vector3 escalaBase;
    private bool interativo = true;
    private SpriteRenderer[] sprites;
    private TMP_Text[] textos;
    private Color[] coresSprites;
    private Color[] coresTextos;

    public bool Interativo
    {
        get => interativo;
        set
        {
            if (interativo == value)
            {
                return;
            }
            interativo = value;
            transform.localScale = escalaBase;
            Pintar();
        }
    }

    private void Awake()
    {
        escalaBase = transform.localScale;
        if (fundo != null)
        {
            fundo.color = corNormal;
        }
        sprites = GetComponentsInChildren<SpriteRenderer>(true);
        textos = GetComponentsInChildren<TMP_Text>(true);
        coresSprites = new Color[sprites.Length];
        coresTextos = new Color[textos.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            coresSprites[i] = sprites[i] == fundo ? corNormal : sprites[i].color;
        }
        for (int i = 0; i < textos.Length; i++)
        {
            coresTextos[i] = textos[i].color;
        }
        Pintar();
    }

    // Apagado = cores originais em cinza e meio transparentes.
    private void Pintar()
    {
        if (sprites == null)
        {
            return;
        }
        for (int i = 0; i < sprites.Length; i++)
        {
            sprites[i].color = interativo ? coresSprites[i] : Apagar(coresSprites[i]);
        }
        for (int i = 0; i < textos.Length; i++)
        {
            textos[i].color = interativo ? coresTextos[i] : Apagar(coresTextos[i]);
        }
    }

    private static Color Apagar(Color c)
    {
        float cinza = c.grayscale;
        return new Color(cinza, cinza, cinza, c.a * AlfaApagado);
    }

    private void OnMouseEnter()
    {
        if (!interativo)
        {
            return;
        }
        if (fundo != null)
        {
            fundo.color = corHover;
        }
    }

    private void OnMouseExit()
    {
        if (fundo != null && interativo)
        {
            fundo.color = corNormal;
        }
        transform.localScale = escalaBase;
    }

    private void OnMouseDown()
    {
        if (!interativo)
        {
            return;
        }
        transform.localScale = escalaBase * 0.96f;
    }

    private void OnMouseUpAsButton()
    {
        transform.localScale = escalaBase;
        if (interativo)
        {
            aoClicar.Invoke();
        }
    }

    private void OnMouseUp()
    {
        transform.localScale = escalaBase;
    }
}
