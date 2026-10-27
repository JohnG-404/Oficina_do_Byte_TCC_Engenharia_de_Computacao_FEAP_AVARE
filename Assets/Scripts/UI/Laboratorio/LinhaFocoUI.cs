using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Fundo de uma linha de opção (tema Laboratório): troca para o sprite de
/// foco (painel_linha_foco) enquanto algum item da linha está selecionado.
/// Por padrão observa os filhos desta linha; preencha "Observar" quando os
/// controles da linha não forem filhos dela (ex: botões que precisam ficar
/// soltos no pai porque o script da aba os encontra pelo nome).
/// </summary>
[RequireComponent(typeof(Image))]
public class LinhaFocoUI : MonoBehaviour
{
    [SerializeField] private Sprite spriteNormal;
    [SerializeField] private Sprite spriteFoco;
    [SerializeField] private Transform[] observar;

    private Image imagem;
    private int estado = -1;

    /// <summary>Mantém o destaque mesmo sem foco (ex: linha de controle esperando a tecla nova).</summary>
    public bool ForcarFoco { get; set; }

    private void Awake()
    {
        imagem = GetComponent<Image>();
    }

    private void OnEnable()
    {
        estado = -1;
    }

    private void Update()
    {
        EventSystem es = EventSystem.current;
        GameObject sel = es != null ? es.currentSelectedGameObject : null;
        int focado = ForcarFoco || (sel != null && Contem(sel.transform)) ? 1 : 0;
        if (focado == estado)
        {
            return;
        }

        estado = focado;
        Sprite s = focado == 1 ? spriteFoco : spriteNormal;
        if (s != null)
        {
            imagem.sprite = s;
        }
    }

    private bool Contem(Transform t)
    {
        if (observar == null || observar.Length == 0)
        {
            return t.IsChildOf(transform);
        }

        foreach (Transform o in observar)
        {
            if (o != null && t.IsChildOf(o)) return true;
        }
        return false;
    }
}
