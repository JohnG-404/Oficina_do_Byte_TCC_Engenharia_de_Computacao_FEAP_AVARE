using UnityEngine;

/// <summary>
/// Prévia do objetivo de um puzzle, mostrada dentro do "painel_previa": um
/// personagem em miniatura repete em loop o que o jogador vai conseguir
/// depois de montar as peças — andar pra frente e pra trás (Movimento), cair
/// (Física/Rigidbody), andar no chão e bater numa parede (Colisão) ou pular
/// (Pulo).
///
/// Só move o "Actor"; o resto da miniatura (chão, parede) é montado na cena
/// pelo builder do Editor (Tools > Física > Aplicar Layout Novo nos Puzzles).
/// Todas as posições ficam no espaço local do PAI do Actor e podem ser
/// ajustadas à mão no Inspector.
///
/// Roda em Update() só enquanto o puzzle está ativo (o grupo inteiro é
/// desativado ao resolver) e recomeça do início toda vez que reaparece.
/// </summary>
public class PuzzlePreview : MonoBehaviour
{
    public enum PreviewType
    {
        MoveBackAndForth,
        Fall,
        CollideWallAndWalk,
        Jump,
        CameraFollow
    }

    [SerializeField] private PreviewType type = PreviewType.MoveBackAndForth;
    [SerializeField] private Transform actor;

    [Tooltip("Ponto de partida (espaço local do pai do Actor). É onde o personagem fica em pé no chão.")]
    [SerializeField] private Vector2 pointA;

    [Tooltip("Ponto de chegada: MoveBackAndForth = outro extremo; Fall = onde termina de cair (abaixo da janela); CollideWallAndWalk = encostado na parede; Jump = não usado.")]
    [SerializeField] private Vector2 pointB;

    [Tooltip("Altura do pulo, em unidades locais (só Jump).")]
    [SerializeField] private float jumpHeight = 1.2f;

    [Tooltip("Velocidade de caminhada, em unidades locais por segundo.")]
    [SerializeField] private float walkSpeed = 1.6f;

    [Tooltip("Duração da queda até o ponto B, em segundos (só Fall).")]
    [SerializeField] private float fallDuration = 1.1f;

    [Header("Só CameraFollow")]
    [Tooltip("Moldura que representa o que a câmera enxerga — segue o Actor suavemente.")]
    [SerializeField] private Transform cameraFrame;
    [Tooltip("Limites (x mínimo e máximo, espaço local) da moldura — a 'câmera' não passa disso.")]
    [SerializeField] private Vector2 frameLimits = new Vector2(-1f, 1f);
    [SerializeField] private float frameSmoothTime = 0.3f;

    private float frameVelocity;
    private Vector3 baseScale = Vector3.one;
    private SpriteRenderer actorRenderer;
    private float startTime;

    private void Awake()
    {
        if (actor != null)
        {
            baseScale = actor.localScale;
            actorRenderer = actor.GetComponent<SpriteRenderer>();
        }
    }

    private void OnEnable()
    {
        startTime = Time.time;
        frameVelocity = 0f;
        if (cameraFrame != null && actor != null)
        {
            Vector3 f = cameraFrame.localPosition;
            cameraFrame.localPosition = new Vector3(Mathf.Clamp(pointA.x, frameLimits.x, frameLimits.y), f.y, f.z);
        }
    }

    private void Update()
    {
        if (actor == null)
        {
            return;
        }

        float t = Time.time - startTime;

        switch (type)
        {
            case PreviewType.MoveBackAndForth: UpdateMoveBackAndForth(t); break;
            case PreviewType.Fall: UpdateFall(t); break;
            case PreviewType.CollideWallAndWalk: UpdateCollide(t); break;
            case PreviewType.Jump: UpdateJump(t); break;
            case PreviewType.CameraFollow: UpdateCameraFollow(t); break;
        }
    }

    private void SetActor(float x, float y)
    {
        Vector3 p = actor.localPosition;
        actor.localPosition = new Vector3(x, y, p.z);
    }

    private void UpdateMoveBackAndForth(float t)
    {
        float distance = Mathf.Max(Mathf.Abs(pointB.x - pointA.x), 0.01f);
        float leg = distance / Mathf.Max(walkSpeed, 0.01f);
        const float pause = 0.35f;
        float cycle = 2f * (leg + pause);
        float p = Mathf.Repeat(t, cycle);

        float u;
        bool goingBack = false;
        if (p < leg) u = p / leg;
        else if (p < leg + pause) u = 1f;
        else if (p < 2f * leg + pause) { u = 1f - (p - leg - pause) / leg; goingBack = true; }
        else { u = 0f; goingBack = true; }

        SetActor(Mathf.Lerp(pointA.x, pointB.x, u), pointA.y);
        if (actorRenderer != null) actorRenderer.flipX = goingBack;
        actor.localScale = baseScale;
    }

    // Cai do ponto A até B (abaixo da janela) com aceleração constante — a
    // "gravidade" do Rigidbody2D — espera um instante e recomeça do topo.
    private void UpdateFall(float t)
    {
        const float hold = 0.7f;
        const float rest = 0.35f;
        float cycle = hold + fallDuration + rest;
        float p = Mathf.Repeat(t, cycle);

        float y;
        if (p < hold)
        {
            y = pointA.y;
        }
        else if (p < hold + fallDuration)
        {
            float k = (p - hold) / fallDuration;
            y = Mathf.Lerp(pointA.y, pointB.y, k * k);
        }
        else
        {
            y = pointB.y;
        }

        SetActor(pointA.x, y);
        actor.localScale = baseScale;
    }

    // Anda até a parede (B), bate (achata um pouco e treme), e volta andando.
    private void UpdateCollide(float t)
    {
        float distance = Mathf.Max(Mathf.Abs(pointB.x - pointA.x), 0.01f);
        float leg = distance / Mathf.Max(walkSpeed, 0.01f);
        const float hit = 0.5f;
        const float idle = 0.5f;
        float cycle = 2f * leg + hit + idle;
        float p = Mathf.Repeat(t, cycle);

        Vector3 scale = baseScale;
        float x;
        bool goingBack = false;

        if (p < leg)
        {
            x = Mathf.Lerp(pointA.x, pointB.x, p / leg);
        }
        else if (p < leg + hit)
        {
            float h = (p - leg) / hit;
            float squash = Mathf.Sin(h * Mathf.PI);
            x = pointB.x - 0.08f * squash;
            scale = new Vector3(baseScale.x * (1f - 0.25f * squash), baseScale.y * (1f + 0.1f * squash), baseScale.z);
        }
        else if (p < 2f * leg + hit)
        {
            x = Mathf.Lerp(pointB.x, pointA.x, (p - leg - hit) / leg);
            goingBack = true;
        }
        else
        {
            x = pointA.x;
            goingBack = true;
        }

        SetActor(x, pointA.y);
        if (actorRenderer != null) actorRenderer.flipX = goingBack;
        actor.localScale = scale;
    }

    // Fica parado, pula em arco (parábola), aterrissa e repete.
    private void UpdateJump(float t)
    {
        const float idle = 0.7f;
        const float air = 0.8f;
        float cycle = idle + air;
        float p = Mathf.Repeat(t, cycle);

        float y = pointA.y;
        Vector3 scale = baseScale;

        if (p >= idle)
        {
            float u = (p - idle) / air;
            y = pointA.y + jumpHeight * 4f * u * (1f - u);
            float stretch = Mathf.Sin(u * Mathf.PI);
            scale = new Vector3(baseScale.x * (1f - 0.08f * stretch), baseScale.y * (1f + 0.12f * stretch), baseScale.z);
        }

        SetActor(pointA.x, y);
        actor.localScale = scale;
    }

    // O personagem anda de um lado ao outro e a moldura (a "câmera") vai
    // atrás dele devagar (SmoothDamp), parando nos limites.
    private void UpdateCameraFollow(float t)
    {
        UpdateMoveBackAndForth(t);

        if (cameraFrame == null)
        {
            return;
        }

        Vector3 f = cameraFrame.localPosition;
        float x = Mathf.SmoothDamp(f.x, actor.localPosition.x, ref frameVelocity, frameSmoothTime);
        cameraFrame.localPosition = new Vector3(Mathf.Clamp(x, frameLimits.x, frameLimits.y), f.y, f.z);
    }
}
