using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Monta o cenário das fases de IA com o mesmo kit e a mesma estrutura das
/// fases Interacao/Interacao_Vida/Aplicacao_Fisica: tiles do pack Tecno em
/// escala 1 (0,4 x 0,8), sala fechada por colunas de parede, chão com as
/// peças de canto nas junções, blocos que saem do chão com as peças
/// plat_*_T/M, plataformas flutuantes com as pontas arredondadas e o fundo
/// lab_background_v3 repetido lado a lado.
///
/// Os tiles são só visuais; cada peça sólida ganha UM BoxCollider2D do
/// tamanho dela (chão, paredes, blocos, plataformas), com o topo alinhado ao
/// topo visual dos tiles.
/// </summary>
public class IACenarioKit
{
    private const string Pasta = "Assets/Images/Packs/Tecno/";
    private const string FundoPath = "Assets/Images/Interação/lab_background_v3.png";
    private const string LaserPath = "Assets/Anim/Laser/trap_laser_04.png";
    private const string PortaPath = "Assets/Images/Packs/Tecno/lift_panel.png";

    public const float Tile = 0.4f;      // largura de um tile de chão/plataforma
    public const float Linha = 0.8f;     // altura de um tile (e lado de um tile de parede)
    private const int OrdemTiles = 1;
    private const int OrdemFundo = -20;

    private readonly float a;            // borda interna da parede esquerda
    private readonly float b;            // borda interna da parede direita
    private readonly int linhasParede;
    private readonly List<(float x0, float x1, int linhas, string nome)> blocos = new List<(float, float, int, string)>();

    /// <summary>
    /// Sala com o chão com topo em y = 0, entre as paredes internas
    /// <paramref name="bordaEsquerda"/> e <paramref name="bordaDireita"/>
    /// (a distância é arredondada pra múltiplos de 0,4).
    /// </summary>
    public IACenarioKit(float bordaEsquerda, float bordaDireita, int linhasDeParede = 14)
    {
        a = bordaEsquerda;
        b = a + Mathf.Round((bordaDireita - bordaEsquerda) / Tile) * Tile;
        linhasParede = linhasDeParede;
    }

    public float BordaEsquerda => a;
    public float BordaDireita => b;

    /// <summary>Bloco sólido saindo do chão (ex: uma parede baixa), com <paramref name="linhas"/> x 0,8 de altura.</summary>
    public void AdicionarBloco(string nome, float x0, float x1, int linhas)
    {
        blocos.Add((Snap(x0), Snap(x1), Mathf.Max(1, linhas), nome));
    }

    /// <summary>Cria chão, paredes e blocos embaixo de <paramref name="pai"/>. Devolve os GameObjects dos blocos, pelo nome.</summary>
    public Dictionary<string, GameObject> Construir(Transform pai)
    {
        var criados = new Dictionary<string, GameObject>();
        float yChao = -Linha * 0.5f;

        // Chão -----------------------------------------------------------
        var chao = Grupo(pai, "Chao");
        Colisor(chao, Rect.MinMaxRect(a - Linha, -Linha, b + Linha, 0f));
        // Embaixo da parede esquerda: canto externo + junção com a face interna.
        Tile_(chao.transform, "platform_mid_right", new Vector2(a - Linha + Tile * 0.5f, yChao));
        Tile_(chao.transform, "platform_mid_complement", new Vector2(a - Tile * 0.5f, yChao));
        for (float x = a + Tile * 0.5f; x < b; x += Tile)
        {
            Tile_(chao.transform, PecaDoChao(x), new Vector2(x, yChao));
        }
        Tile_(chao.transform, "platform_mid_complement_left", new Vector2(b + Tile * 0.5f, yChao));
        Tile_(chao.transform, "platform_mid_left", new Vector2(b + Linha - Tile * 0.5f, yChao));

        // Paredes --------------------------------------------------------
        float alturaParede = linhasParede * Linha;
        var esquerda = Grupo(pai, "ParedeEsquerda");
        Colisor(esquerda, Rect.MinMaxRect(a - Linha, 0f, a, alturaParede));
        var direita = Grupo(pai, "ParedeDireita");
        Colisor(direita, Rect.MinMaxRect(b, 0f, b + Linha, alturaParede));
        for (int i = 0; i < linhasParede; i++)
        {
            float y = Linha * 0.5f + i * Linha;
            Tile_(esquerda.transform, "wall_tile_right", new Vector2(a - Linha * 0.5f, y));
            Tile_(direita.transform, "wall_tile_left", new Vector2(b + Linha * 0.5f, y));
        }

        // Blocos saindo do chão -------------------------------------------
        foreach (var bl in blocos)
        {
            var go = Grupo(pai, bl.nome);
            Colisor(go, Rect.MinMaxRect(bl.x0, 0f, bl.x1, bl.linhas * Linha));
            int colunas = Mathf.RoundToInt((bl.x1 - bl.x0) / Tile);
            for (int linha = 0; linha < bl.linhas; linha++)
            {
                bool topo = linha == bl.linhas - 1;
                float y = Linha * 0.5f + linha * Linha;
                for (int c = 0; c < colunas; c++)
                {
                    bool esq = c == 0, dir = c == colunas - 1;
                    string peca = topo
                        ? (esq ? "plat_left_T" : dir ? "plat_right_T" : "plat_mid_T")
                        : (esq ? "plat_left_M" : dir ? "plat_right_M" : "plat_center");
                    Tile_(go.transform, peca, new Vector2(bl.x0 + Tile * 0.5f + c * Tile, y));
                }
            }
            criados[bl.nome] = go;
        }

        return criados;
    }

    // No chão, embaixo de um bloco: a coluna da esquerda continua a face clara
    // do bloco (complement_left), a da direita idem (complement) e o miolo
    // fica liso (plat_mid_B) — igual à Interacao_Vida.
    private string PecaDoChao(float x)
    {
        foreach (var bl in blocos)
        {
            if (x < bl.x0 || x > bl.x1) continue;
            if (x < bl.x0 + Tile) return "platform_mid_complement_left";
            if (x > bl.x1 - Tile) return "platform_mid_complement";
            return "plat_mid_B";
        }
        return "platform_tile_mid";
    }

    /// <summary>Plataforma flutuante (pontas arredondadas) de x0 a x1, com o topo em <paramref name="yTopo"/>.</summary>
    public static GameObject Plataforma(Transform pai, string nome, float x0, float x1, float yTopo)
    {
        x0 = Snap(x0);
        x1 = Snap(x1);
        var go = Grupo(pai, nome);
        Colisor(go, Rect.MinMaxRect(x0, yTopo - Linha, x1, yTopo));

        int colunas = Mathf.Max(2, Mathf.RoundToInt((x1 - x0) / Tile));
        float y = yTopo - Linha * 0.5f;
        for (int c = 0; c < colunas; c++)
        {
            float x = x0 + Tile * 0.5f + c * Tile;
            // As pontas arredondadas (recorte de ~0,41) entram 0,02 no tile
            // vizinho, pra não sobrar uma fresta clara na emenda.
            if (c == 0) Ponta(go.transform, "platform_final_left", x + Tile * 0.5f + 0.02f, y, true);
            else if (c == colunas - 1) Ponta(go.transform, "platform_final_right", x - Tile * 0.5f - 0.02f, y, false);
            else Tile_(go.transform, "platform_tile_mid", new Vector2(x, y));
        }
        return go;
    }

    /// <summary>Fundo lab_background_v3 (19,2 x 10,8, escala 1) repetido pra cobrir de xMin a xMax, centrado na altura da câmera.</summary>
    public static void Fundo(Transform pai, float xMin, float xMax, float yCamera)
    {
        Sprite fundo = Carregar(FundoPath);
        if (fundo == null) return;

        var grupo = Grupo(pai, "Fundo");
        float w = fundo.bounds.size.x;
        for (float x = xMin + w * 0.5f; x - w * 0.5f < xMax; x += w)
        {
            var sr = Sprite_(grupo.transform, "BG", fundo, new Vector2(x, yCamera + 0.12f));
            sr.sortingOrder = OrdemFundo;
        }
    }

    /// <summary>Porta de saída: lift_panel na proporção original (escala 1,5, como na Interacao), apoiada no chão.</summary>
    public static SpriteRenderer Porta(Transform pai, float x)
    {
        Sprite porta = Carregar(PortaPath);
        if (porta == null) return null;
        var sr = Sprite_(pai, "Porta", porta, Vector2.zero);
        sr.transform.localScale = Vector3.one * 1.5f;
        sr.transform.position = new Vector3(x, porta.bounds.size.y * 1.5f * 0.5f, 0f);
        return sr;
    }

    /// <summary>
    /// Barreira de laser vertical (os mesmos lasers da Interacao_Vida, em pé),
    /// empilhados de y = 0 até <paramref name="altura"/>. O collider é do
    /// objeto que recebe os lasers.
    /// </summary>
    public static void Lasers(Transform pai, float x, float altura)
    {
        Sprite laser = Carregar(LaserPath);
        if (laser == null) return;
        float comprimento = laser.bounds.size.x;
        // Os segmentos se sobrepõem pelo tamanho de um poste, pra cada emenda
        // mostrar um poste só.
        float passo = comprimento * 0.88f;
        for (float y = comprimento * 0.5f; y - comprimento * 0.5f < altura; y += passo)
        {
            var sr = Sprite_(pai, "Laser", laser, new Vector2(x, y));
            sr.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            sr.sortingOrder = OrdemTiles + 1;
        }
    }

    // ------------------------------------------------------------------

    private static float Snap(float v) => Mathf.Round(v / Tile) * Tile;

    private static GameObject Grupo(Transform pai, string nome)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        return go;
    }

    private static void Colisor(GameObject go, Rect r)
    {
        var col = go.AddComponent<BoxCollider2D>();
        col.offset = r.center - (Vector2)go.transform.position;
        col.size = r.size;
    }

    // Ponta de plataforma posicionada pela borda que encosta no miolo
    // (bordaInterna), usando a largura real do sprite.
    private static void Ponta(Transform pai, string peca, float bordaInterna, float y, bool ehEsquerda)
    {
        Sprite s = Carregar(Pasta + peca + ".png");
        if (s == null) return;
        float w = s.bounds.size.x;
        float x = ehEsquerda ? bordaInterna - w * 0.5f : bordaInterna + w * 0.5f;
        var sr = Sprite_(pai, peca, s, new Vector2(x, y));
        sr.sortingOrder = OrdemTiles;
    }

    private static void Tile_(Transform pai, string peca, Vector2 posicao)
    {
        Sprite s = Carregar(Pasta + peca + ".png");
        if (s == null) return;
        var sr = Sprite_(pai, peca, s, posicao);
        sr.sortingOrder = OrdemTiles;
    }

    private static SpriteRenderer Sprite_(Transform pai, string nome, Sprite sprite, Vector2 posicao)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        go.transform.position = new Vector3(posicao.x, posicao.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        return sr;
    }

    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    private static Sprite Carregar(string path)
    {
        if (cache.TryGetValue(path, out Sprite s) && s != null) return s;
        s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null)
        {
            // Sprites em modo Multiple: pega o primeiro sub-sprite.
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is Sprite sub) { s = sub; break; }
            }
        }
        if (s == null) Debug.LogWarning("[IACenarioKit] Sprite não encontrado: " + path);
        cache[path] = s;
        return s;
    }
}
