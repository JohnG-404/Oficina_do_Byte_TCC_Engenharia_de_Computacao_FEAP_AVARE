using TMPro;
using UnityEngine;

/// <summary>
/// Prévia dos puzzles da fase IA, dentro do "painel_previa": um guarda em
/// miniatura e um boneco que anda sozinho, com a caixa do estado ativo
/// acesa (Patrulhar / Perseguir / Atacar). Liga "o que eu vejo" com "em
/// que estado ele está".
///
/// Três modos, um por puzzle:
/// - Estados: mostra cada estado com seu comportamento, em sequência
///   (anda entre A e B, vai atrás do boneco, ataca).
/// - Transicoes: a distância decide as trocas (boneco chega perto, o guarda
///   persegue e ataca; boneco se afasta, o guarda desiste).
/// - Raycast: com uma parede no meio. A linha do guarda até o boneco fica
///   vermelha quando bate na parede e verde quando está livre.
///
/// É uma simulação só visual (sem física), em coordenadas locais do pai dos
/// atores — montada pelo IAPuzzleRigBuilder (Tools/IA). Roda enquanto o
/// puzzle está ativo e recomeça toda vez que ele reaparece.
/// </summary>
public class IAPuzzlePreview : MonoBehaviour
{
    public enum Modo
    {
        Estados,
        Transicoes,
        Raycast
    }

    private struct Chave
    {
        public float t;
        public float u;
        public float y;
        public float arco;

        public Chave(float t, float u, float y = 0f, float arco = 0f)
        {
            this.t = t;
            this.u = u;
            this.y = y;
            this.arco = arco;
        }
    }

    [SerializeField] private Modo modo = Modo.Estados;

    [Header("Atores (filhos do mesmo pai)")]
    [SerializeField] private Transform guarda;
    [SerializeField] private Transform boneco;
    [SerializeField] private TMP_Text icone;
    [SerializeField] private Vector2 offsetIcone = new Vector2(0f, 0.42f);

    [Header("Caixas de estado (Patrulhar, Perseguir, Atacar)")]
    [SerializeField] private SpriteRenderer[] caixas = new SpriteRenderer[3];
    [SerializeField] private TMP_Text[] textosCaixas = new TMP_Text[3];

    [Header("Só Raycast")]
    [SerializeField] private SpriteRenderer parede;
    [Tooltip("Sprite fino esticado entre o olho do guarda e o boneco.")]
    [SerializeField] private SpriteRenderer linhaVisao;

    [Header("Área (espaço local do pai dos atores)")]
    [SerializeField] private float xMin = -1.5f;
    [SerializeField] private float xMax = 1.5f;
    [Tooltip("Y local do centro dos atores quando estão no chão.")]
    [SerializeField] private float chaoY;

    [Header("Cores")]
    [SerializeField] private Color corCaixaInativa = new Color(0.17f, 0.22f, 0.32f, 1f);
    [SerializeField] private Color corCaixaPatrulha = new Color(0.96f, 0.77f, 0.26f, 1f);
    [SerializeField] private Color corCaixaAlerta = new Color(0.94f, 0.29f, 0.29f, 1f);
    [SerializeField] private Color corTextoInativo = new Color(0.55f, 0.6f, 0.71f, 1f);
    [SerializeField] private Color corTextoAtivo = new Color(0.05f, 0.08f, 0.13f, 1f);
    [SerializeField] private Color corLinhaLivre = new Color(0.31f, 0.78f, 0.45f, 1f);
    [SerializeField] private Color corLinhaBloqueada = new Color(0.94f, 0.29f, 0.29f, 1f);

    // Estado da simulação.
    private int estado;          // 0 Patrulhar, 1 Perseguir, 2 Atacar
    private float gx;
    private int gdir = 1;
    private float t;
    private float timerAtaque;
    private float soco;
    private float tempoNoEstado;

    private float largura;
    private float pontoA, pontoB;
    private float raioVisao, raioPerder, alcance;
    private float velPatrulha, velPerseguir;
    private float limiteGuarda;
    private Chave[] chaves;
    private Rect rectParede;

    private Vector3 escalaGuarda = Vector3.one;
    private SpriteRenderer guardaRenderer;
    private SpriteRenderer bonecoRenderer;
    private Color corBoneco = Color.white;

    private void Awake()
    {
        if (guarda != null)
        {
            escalaGuarda = guarda.localScale;
            guardaRenderer = guarda.GetComponent<SpriteRenderer>();
        }

        if (boneco != null)
        {
            bonecoRenderer = boneco.GetComponent<SpriteRenderer>();
            if (bonecoRenderer != null) corBoneco = bonecoRenderer.color;
        }

        Configurar();
    }

    private void OnEnable()
    {
        Reiniciar();
    }

    // Tudo proporcional à largura da janela, pra funcionar em qualquer tamanho.
    private void Configurar()
    {
        largura = Mathf.Max(0.5f, xMax - xMin);

        if (parede != null && parede.transform.parent != null)
        {
            Transform pai = guarda != null ? guarda.parent : parede.transform.parent;
            Bounds b = parede.bounds;
            Vector3 min = pai.InverseTransformPoint(b.min);
            Vector3 max = pai.InverseTransformPoint(b.max);
            rectParede = Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        float meioAtor = guarda != null ? Mathf.Abs(guarda.localScale.x) * 0.5f : 0.2f;

        switch (modo)
        {
            case Modo.Estados:
                pontoA = X(0.1f); pontoB = X(0.55f);
                alcance = 0.1f * largura;
                velPatrulha = 0.16f * largura; velPerseguir = 0.3f * largura;
                limiteGuarda = xMax;
                chaves = new[] { new Chave(0f, 0.9f), new Chave(1f, 0.9f) };
                break;

            case Modo.Transicoes:
                pontoA = X(0.1f); pontoB = X(0.45f);
                raioVisao = 0.3f * largura; raioPerder = 0.38f * largura; alcance = 0.1f * largura;
                velPatrulha = 0.13f * largura; velPerseguir = 0.2f * largura;
                limiteGuarda = X(0.55f);
                chaves = new[]
                {
                    new Chave(0f, 0.95f), new Chave(1.5f, 0.95f), new Chave(3.5f, 0.52f),
                    new Chave(5.5f, 0.52f), new Chave(6.7f, 0.97f), new Chave(10f, 0.97f)
                };
                break;

            default: // Raycast
                pontoA = X(0.08f); pontoB = X(0.45f);
                raioVisao = 0.42f * largura; raioPerder = 0.5f * largura; alcance = 0.1f * largura;
                velPatrulha = 0.13f * largura; velPerseguir = 0.22f * largura;
                float bonecoY = chaoY - meioAtor;   // base dos atores
                float topo = parede != null ? (rectParede.yMax - bonecoY) : 0.5f;
                float uParede = parede != null ? (rectParede.center.x - xMin) / largura : 0.66f;
                float uAtras = parede != null ? (rectParede.xMax + 0.35f - xMin) / largura : 0.8f;
                float uFrente = parede != null ? (rectParede.xMin - 0.45f - xMin) / largura : 0.5f;
                limiteGuarda = parede != null ? rectParede.xMin - meioAtor - 0.02f : xMax;
                chaves = new[]
                {
                    new Chave(0f, 0.95f), new Chave(1.5f, uAtras), new Chave(5f, uAtras),
                    new Chave(5.6f, uParede, topo, 0.3f), new Chave(7.5f, uParede, topo),
                    new Chave(8.1f, uFrente, 0f, 0.25f), new Chave(9.2f, uFrente, 0f),
                    new Chave(9.8f, uParede, topo, 0.3f), new Chave(10.4f, uAtras, 0f, 0.2f),
                    new Chave(12f, 0.95f)
                };
                break;
        }
    }

    private float X(float u) => xMin + u * largura;

    private void Reiniciar()
    {
        t = 0f;
        gx = pontoA;
        gdir = 1;
        estado = 0;
        tempoNoEstado = 0f;
        timerAtaque = 0f;
        soco = 0f;
    }

    private void Update()
    {
        if (guarda == null || boneco == null || chaves == null)
        {
            return;
        }

        float dt = Time.deltaTime;
        t += dt;
        tempoNoEstado += dt;

        Vector2 pb = PosicaoBoneco(t);

        switch (modo)
        {
            case Modo.Estados: SimularEstados(pb, dt); break;
            case Modo.Transicoes: SimularTransicoes(pb, dt); break;
            default: SimularRaycast(pb, dt); break;
        }

        Desenhar(pb, dt);
    }

    // ------------------------------------------------------------------
    // Modos
    // ------------------------------------------------------------------

    // Um estado de cada vez, pra mostrar o comportamento de cada um.
    private void SimularEstados(Vector2 pb, float dt)
    {
        if (estado == 0)
        {
            Patrulhar(dt);
            if (tempoNoEstado > 4f) MudarEstado(1);
        }
        else if (estado == 1)
        {
            Perseguir(pb.x, dt);
            if (Mathf.Abs(pb.x - gx) < alcance) MudarEstado(2);
        }
        else
        {
            Atacar(dt);
            if (tempoNoEstado > 1.8f)
            {
                gx = pontoA;
                gdir = 1;
                MudarEstado(0);
            }
        }
    }

    private void SimularTransicoes(Vector2 pb, float dt)
    {
        float d = Vector2.Distance(new Vector2(gx, chaoY), pb);
        Transicoes(d, d < raioVisao, d > raioPerder);
        Agir(pb, dt);
    }

    private void SimularRaycast(Vector2 pb, float dt)
    {
        float d = Vector2.Distance(new Vector2(gx, chaoY), pb);
        bool bloqueado = LinhaBloqueada(Olho(), pb, out _);
        Transicoes(d, d < raioVisao && !bloqueado, d > raioPerder || bloqueado);
        Agir(pb, dt);
    }

    private void Transicoes(float d, bool ve, bool perde)
    {
        if (estado == 0 && ve) MudarEstado(1);
        else if (estado == 1 && perde) MudarEstado(0);
        else if (estado == 1 && d < alcance) MudarEstado(2);
        else if (estado == 2 && d > alcance) MudarEstado(1);
    }

    private void Agir(Vector2 pb, float dt)
    {
        if (estado == 0) Patrulhar(dt);
        else if (estado == 1) Perseguir(pb.x, dt);
        else Atacar(dt);
    }

    private void Patrulhar(float dt)
    {
        float destino = gdir > 0 ? pontoB : pontoA;
        gx = Mathf.MoveTowards(gx, destino, velPatrulha * dt);
        if (Mathf.Abs(destino - gx) < 0.01f) gdir = -gdir;
        gx = Mathf.Min(gx, limiteGuarda);
    }

    private void Perseguir(float alvoX, float dt)
    {
        float dx = alvoX - gx;
        if (Mathf.Abs(dx) > 0.02f)
        {
            gdir = dx > 0f ? 1 : -1;
            gx += Mathf.Sign(dx) * velPerseguir * dt;
        }

        gx = Mathf.Clamp(gx, xMin, limiteGuarda);
    }

    private void Atacar(float dt)
    {
        timerAtaque -= dt;
        if (timerAtaque <= 0f)
        {
            timerAtaque = 0.6f;
            soco = 0.2f;
        }
    }

    private void MudarEstado(int novo)
    {
        if (novo == estado) return;
        estado = novo;
        tempoNoEstado = 0f;
        if (novo == 2) timerAtaque = 0.15f;
    }

    // ------------------------------------------------------------------
    // Boneco (anda sozinho por quadros-chave) e linha de visão
    // ------------------------------------------------------------------

    private Vector2 PosicaoBoneco(float tempo)
    {
        float periodo = chaves[chaves.Length - 1].t;
        float x = periodo > 0f ? Mathf.Repeat(tempo, periodo) : 0f;

        for (int i = 1; i < chaves.Length; i++)
        {
            if (x <= chaves[i].t)
            {
                Chave a = chaves[i - 1];
                Chave b = chaves[i];
                float k = b.t > a.t ? (x - a.t) / (b.t - a.t) : 1f;
                float u = Mathf.Lerp(a.u, b.u, k);
                float y = Mathf.Lerp(a.y, b.y, k) + b.arco * 4f * k * (1f - k);
                return new Vector2(X(u), chaoY + y);
            }
        }

        Chave ultima = chaves[chaves.Length - 1];
        return new Vector2(X(ultima.u), chaoY + ultima.y);
    }

    private Vector2 Olho() => new Vector2(gx, chaoY + 0.08f);

    // Liang-Barsky: o segmento olho→boneco cruza o retângulo da parede?
    private bool LinhaBloqueada(Vector2 a, Vector2 b, out Vector2 ponto)
    {
        ponto = b;
        if (parede == null)
        {
            return false;
        }

        float t0 = 0f, t1 = 1f;
        float dx = b.x - a.x, dy = b.y - a.y;
        float[] p = { -dx, dx, -dy, dy };
        float[] q = { a.x - rectParede.xMin, rectParede.xMax - a.x, a.y - rectParede.yMin, rectParede.yMax - a.y };

        for (int i = 0; i < 4; i++)
        {
            if (Mathf.Approximately(p[i], 0f))
            {
                if (q[i] < 0f) return false;
            }
            else
            {
                float r = q[i] / p[i];
                if (p[i] < 0f)
                {
                    if (r > t1) return false;
                    if (r > t0) t0 = r;
                }
                else
                {
                    if (r < t0) return false;
                    if (r < t1) t1 = r;
                }
            }
        }

        ponto = a + (b - a) * t0;
        return true;
    }

    // ------------------------------------------------------------------
    // Desenho
    // ------------------------------------------------------------------

    private void Desenhar(Vector2 pb, float dt)
    {
        boneco.localPosition = new Vector3(pb.x, pb.y, boneco.localPosition.z);
        guarda.localPosition = new Vector3(gx, chaoY, guarda.localPosition.z);

        if (guardaRenderer != null) guardaRenderer.flipX = gdir < 0;

        // "Soco" ao atacar + boneco pisca em vermelho.
        soco = Mathf.Max(0f, soco - dt);
        float k = soco > 0f ? Mathf.Sin(soco / 0.2f * Mathf.PI) : 0f;
        guarda.localScale = new Vector3(escalaGuarda.x * (1f + 0.25f * k), escalaGuarda.y * (1f - 0.1f * k), escalaGuarda.z);
        if (bonecoRenderer != null) bonecoRenderer.color = soco > 0f ? Color.Lerp(corBoneco, corCaixaAlerta, 0.7f) : corBoneco;

        if (icone != null)
        {
            icone.text = estado == 0 ? "?" : "!";
            icone.color = estado == 0 ? corCaixaPatrulha : corCaixaAlerta;
            icone.transform.localPosition = new Vector3(gx + offsetIcone.x, chaoY + offsetIcone.y, icone.transform.localPosition.z);
        }

        for (int i = 0; i < caixas.Length; i++)
        {
            bool ativa = i == estado;
            Color cor = !ativa ? corCaixaInativa : (i == 0 ? corCaixaPatrulha : corCaixaAlerta);
            if (caixas[i] != null) caixas[i].color = cor;
            if (textosCaixas != null && i < textosCaixas.Length && textosCaixas[i] != null)
            {
                textosCaixas[i].color = ativa ? corTextoAtivo : corTextoInativo;
            }
        }

        DesenharLinhaDeVisao(pb);
    }

    private void DesenharLinhaDeVisao(Vector2 pb)
    {
        if (linhaVisao == null)
        {
            return;
        }

        Vector2 olho = Olho();
        float d = Vector2.Distance(new Vector2(gx, chaoY), pb);
        bool mostrar = modo == Modo.Raycast && d < raioVisao;
        linhaVisao.enabled = mostrar;
        if (!mostrar)
        {
            return;
        }

        bool bloqueada = LinhaBloqueada(olho, pb, out Vector2 fim);
        Vector2 delta = fim - olho;
        float comprimento = delta.magnitude;
        float larguraSprite = linhaVisao.sprite != null ? linhaVisao.sprite.bounds.size.x : 1f;

        Transform lt = linhaVisao.transform;
        lt.localPosition = new Vector3((olho.x + fim.x) * 0.5f, (olho.y + fim.y) * 0.5f, lt.localPosition.z);
        lt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        lt.localScale = new Vector3(comprimento / Mathf.Max(0.0001f, larguraSprite), lt.localScale.y, 1f);
        linhaVisao.color = bloqueada ? corLinhaBloqueada : corLinhaLivre;
    }
}
