using UnityEngine;

/// <summary>
/// Fileira de espinhos de largura ajustável: o sprite espinhos_continuo fica
/// em Draw Mode Tiled (bordas de 9-slice nas pontas), então só o miolo se
/// repete quando a largura cresce. O collider e as zonas do InteractableItem
/// (ProximityZone/TopZone) acompanham a largura.
///
/// A raiz fica no centro do sprite. Só cuida do tamanho; a lógica de
/// Ação/Consequência continua toda no InteractableItem.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class EspinhosContinuos : MonoBehaviour
{
    [SerializeField, Min(0.8f)] private float largura = 2.4f;
    [Tooltip("Altura do sprite (espinhos_continuo.png: 72 px a 100 PPU).")]
    [SerializeField] private float altura = 0.72f;
    [Tooltip("Altura do collider sólido, a partir da base.")]
    [SerializeField] private float alturaColisor = 0.45f;
    [Tooltip("Quanto a zona de 'Chegar perto' sobe acima dos espinhos.")]
    [SerializeField] private float alcanceProximidade = 0.6f;

    public float Largura
    {
        get => largura;
        set { largura = Mathf.Max(0.8f, value); Montar(); }
    }

    private void OnEnable()
    {
        Montar();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) Montar(); };
#endif
    }

    public void Montar()
    {
        var sr = GetComponent<SpriteRenderer>();
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = new Vector2(largura, altura);

        float baseY = -altura * 0.5f;
        var colisor = GetComponent<BoxCollider2D>();
        colisor.edgeRadius = 0f;
        colisor.size = new Vector2(Mathf.Max(0.2f, largura - 0.1f), alturaColisor);
        colisor.offset = new Vector2(0f, baseY + alturaColisor * 0.5f);

        Zona("ProximityZone", new Vector2(largura, altura + alcanceProximidade),
            new Vector2(0f, baseY + (altura + alcanceProximidade) * 0.5f));
        Zona("TopZone", new Vector2(largura * 0.9f, 0.2f), new Vector2(0f, altura * 0.5f + 0.1f));
    }

    private void Zona(string nome, Vector2 tamanho, Vector2 centro)
    {
        Transform t = transform.Find(nome);
        if (t == null)
        {
            t = new GameObject(nome).transform;
            t.SetParent(transform, false);
        }
        t.localPosition = Vector3.zero;
        t.localScale = Vector3.one;
        var box = t.GetComponent<BoxCollider2D>();
        if (box == null) box = t.gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = tamanho;
        box.offset = centro;
    }
}

