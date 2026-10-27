using UnityEngine;

/// <summary>
/// Faz a câmera seguir suavemente um alvo (normalmente o Player) nos eixos
/// X e Y, sem mexer na profundidade (Z) da própria câmera — funciona com
/// qualquer configuração ortográfica sem precisar reposicionar a câmera na
/// cena.
///
/// Usa SmoothDamp em vez de Lerp puro: SmoothDamp tem uma velocidade real
/// por trás (não só "anda uma fração da distância por frame"), então a
/// câmera acelera e desacelera de forma mais natural ao alcançar o alvo,
/// em vez do "tremor"/atraso constante que Lerp costuma dar quando o alvo
/// se move a velocidade constante.
///
/// Roda em LateUpdate (depois de todo Update()) — importante pra câmera
/// reagir à posição do Player já atualizada no mesmo frame, sem ficar um
/// frame atrasada.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Tooltip("Transform que a câmera persegue — normalmente o Player.")]
    [SerializeField] private Transform target;

    [Tooltip("Tempo aproximado (em segundos) que a câmera leva pra alcançar o alvo. Maior = câmera mais 'preguiçosa'/suave; menor = mais colada no Player.")]
    [SerializeField] private float smoothTime = 0.25f;

    [Tooltip("Velocidade máxima de deslocamento da câmera, em unidades/segundo. Evita que ela 'dispare' atrás do alvo depois de um teleporte grande (ex: reinício de fase).")]
    [SerializeField] private float maxSpeed = 40f;

    [Tooltip("Deslocamento somado à posição do alvo antes de mirar a câmera nele (ex: olhar um pouco acima da cabeça do personagem).")]
    [SerializeField] private Vector2 offset = new Vector2(0f, 0.5f);

    [Tooltip("Se desmarcado, a câmera nunca se move no eixo Y — fica travada na altura em que já estava posicionada na cena ao iniciar, só acompanhando o alvo na horizontal.")]
    [SerializeField] private bool followVertical = true;

    [Header("Limites do cenário (opcional)")]
    [Tooltip("Se marcado, a posição final da câmera é travada dentro de minPosition/maxPosition — útil pra não mostrar além da borda do cenário.")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Vector2 minPosition;
    [SerializeField] private Vector2 maxPosition;

    private Vector3 velocity;
    private float lockedY;

    private void Awake()
    {
        // Guardado antes de qualquer movimento — é a altura que a câmera já
        // estava posicionada na cena, usada como trava quando followVertical
        // estiver desmarcado (em vez de "correr atrás" da altura do alvo).
        lockedY = transform.position.y;

        if (target == null)
        {
            var player = FindAnyObjectByType<AplicacaoPlayerController>();
            if (player != null)
            {
                target = player.transform;
            }
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        float targetY = followVertical ? target.position.y + offset.y : lockedY;
        Vector3 desired = new Vector3(target.position.x + offset.x, targetY, transform.position.z);
        Vector3 smoothed = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime, maxSpeed);

        if (useBounds)
        {
            smoothed.x = Mathf.Clamp(smoothed.x, minPosition.x, maxPosition.x);
            smoothed.y = Mathf.Clamp(smoothed.y, minPosition.y, maxPosition.y);
        }

        transform.position = smoothed;
    }

    /// <summary>Define o alvo em runtime — útil se o Player só existir depois da câmera (ex: instanciado por outro sistema).</summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    /// <summary>
    /// Para de seguir e posiciona a câmera num ponto fixo (mantém o Z) — usado
    /// na Fisica pra mostrar os puzzles, que ficam montados no centro da sala.
    /// </summary>
    public void FocarEm(Vector2 position)
    {
        enabled = false;
        velocity = Vector3.zero;
        transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    /// <summary>Volta a seguir o alvo (depois de <see cref="FocarEm"/>).</summary>
    public void Retomar()
    {
        velocity = Vector3.zero;
        enabled = true;
    }
}
