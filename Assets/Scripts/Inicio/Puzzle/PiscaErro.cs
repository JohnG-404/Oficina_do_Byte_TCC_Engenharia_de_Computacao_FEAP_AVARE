using System.Collections;
using UnityEngine;

/// <summary>
/// Ritmo e cor do pisca vermelho de "peça errada", o mesmo dos puzzles do
/// Animator e da Vida (AnimatorPuzzleUI.PiscarErro / HudPuzzleUI.PiscarErro):
/// VidaUI.Vermelho com alfa 0,45, 2 vezes, 0,15 s aceso e 0,1 s apagado, em
/// tempo sem escala.
///
/// Também gera, por código, os sprites do aviso de erro dos puzzles de
/// PuzzleControl + SnapZone (moldura arredondada e o ✕), pra não precisar de
/// asset novo nem de mexer nas cenas.
/// </summary>
public static class PiscaErro
{
    public const int Vezes = 2;
    public const float SegundosAceso = 0.15f;
    public const float SegundosApagado = 0.1f;
    public const float Alfa = 0.45f;

    public static Color Cor => new Color(VidaUI.Vermelho.r, VidaUI.Vermelho.g, VidaUI.Vermelho.b, Alfa);

    /// <summary>Chama aceso(true)/aceso(false) no ritmo do pisca de erro.</summary>
    public static IEnumerator Piscar(System.Action<bool> aceso)
    {
        for (int i = 0; i < Vezes; i++)
        {
            aceso(true);
            yield return new WaitForSecondsRealtime(SegundosAceso);
            aceso(false);
            yield return new WaitForSecondsRealtime(SegundosApagado);
        }
    }

    /// <summary>
    /// Quantas unidades de mundo cabem em 1 pixel de uma tela 1080p — usado
    /// pra medir tremida, borda e margem "em pixels" em qualquer resolução.
    /// </summary>
    public static float MundoPorPixel()
    {
        Camera cam = Camera.main;
        float altura = cam != null && cam.orthographic ? cam.orthographicSize * 2f : 10f;
        return altura / 1080f;
    }

    // ------------------------------------------------------------------
    // Sprites gerados por código
    // ------------------------------------------------------------------

    private const float PixelsPorUnidade = 100f;

    private static Sprite molduraLacuna;
    private static Sprite molduraAviso;
    private static Sprite molduraEncaixe;
    private static Sprite xis;

    /// <summary>Lacuna recusada: preenchimento vermelho translúcido + borda vermelha.</summary>
    public static Sprite MolduraLacuna => molduraLacuna != null ? molduraLacuna
        : molduraLacuna = CriarMoldura(new Color(1f, 1f, 1f, 0.45f), Color.white, 10, 3);

    /// <summary>"Vai encaixar aqui" (SnapZone): mesma moldura, preenchimento mais leve. Pinte de ciano.</summary>
    public static Sprite MolduraEncaixe => molduraEncaixe != null ? molduraEncaixe
        : molduraEncaixe = CriarMoldura(new Color(1f, 1f, 1f, 0.28f), Color.white, 10, 3);

    /// <summary>Painel da Dica no aviso: fundo vermelho escuro + borda vermelha.</summary>
    public static Sprite MolduraAviso => molduraAviso != null ? molduraAviso
        : molduraAviso = CriarMoldura(new Color32(0x3A, 0x14, 0x19, 0xF5), VidaUI.Vermelho, 8, 2);

    /// <summary>✕ branco (pinte pelo SpriteRenderer), 128 px como o ícone da dica.</summary>
    public static Sprite Xis => xis != null ? xis : xis = CriarXis();

    /// <summary>
    /// Ajusta um SpriteRenderer fatiado (filho de qualquer coisa, mesmo com
    /// escala não uniforme) pra cobrir "tamanhoMundo" com bordas medidas em
    /// pixels 1080p.
    /// </summary>
    public static void Cobrir(SpriteRenderer sr, Vector3 centroMundo, Vector2 tamanhoMundo)
    {
        float k = MundoPorPixel() * PixelsPorUnidade;
        Vector3 pai = sr.transform.parent != null ? sr.transform.parent.lossyScale : Vector3.one;
        sr.transform.position = new Vector3(centroMundo.x, centroMundo.y, sr.transform.position.z);
        sr.transform.rotation = Quaternion.identity;
        sr.transform.localScale = new Vector3(
            k / Mathf.Max(Mathf.Abs(pai.x), 0.0001f),
            k / Mathf.Max(Mathf.Abs(pai.y), 0.0001f),
            1f);
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = tamanhoMundo / k;
    }

    private static Sprite CriarMoldura(Color preenchimento, Color borda, int raio, int espessura)
    {
        int lado = raio * 2 + 8;
        var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        float meio = lado * 0.5f;
        var px = new Color[lado * lado];
        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                // Distância com sinal até a borda de um retângulo arredondado
                // que ocupa a textura toda (negativa = dentro).
                float qx = Mathf.Abs(x + 0.5f - meio) - (meio - raio);
                float qy = Mathf.Abs(y + 0.5f - meio) - (meio - raio);
                float fora = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                float d = fora + Mathf.Min(Mathf.Max(qx, qy), 0f) - raio;

                float cobertura = Mathf.Clamp01(0.5f - d);
                float naBorda = Mathf.Clamp01(d + espessura + 0.5f);
                Color c = Color.Lerp(preenchimento, borda, naBorda);
                c.a *= cobertura;
                px[y * lado + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply(false, true);

        float b = raio + 2;
        return Sprite.Create(tex, new Rect(0, 0, lado, lado), new Vector2(0.5f, 0.5f),
            PixelsPorUnidade, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    private static Sprite CriarXis()
    {
        const int lado = 128;
        const float meiaLinha = 7f;
        var a1 = new Vector2(34, 34);
        var b1 = new Vector2(94, 94);
        var a2 = new Vector2(34, 94);
        var b2 = new Vector2(94, 34);

        var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        var px = new Color[lado * lado];
        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Mathf.Min(DistanciaAoSegmento(p, a1, b1), DistanciaAoSegmento(p, a2, b2));
                px[y * lado + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(meiaLinha - d + 0.5f));
            }
        }

        tex.SetPixels(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, lado, lado), new Vector2(0.5f, 0.5f), PixelsPorUnidade);
    }

    private static float DistanciaAoSegmento(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }
}
