using UnityEngine;

/// <summary>
/// Cursor do mouse do tema Laboratório: seta em pixel art (contorno escuro,
/// preenchimento claro e sombra) apontando para cima e para a esquerda, com o
/// clique exatamente na ponta da seta. Aplicado sozinho ao iniciar o jogo,
/// em qualquer cena — não precisa de nada na cena nem nas Player Settings.
///
/// Os PNGs ficam em Resources como .bytes (cursor_laboratorio = 32 px,
/// cursor_laboratorio_64 = 64 px para telas de 1440p ou mais) e são lidos
/// com LoadImage e copiados para uma textura legível em RGBA32, o formato
/// que o Cursor.SetCursor exige — assim não depende do tipo de importação.
/// </summary>
public static class CursorLaboratorio
{
    // Ponta da seta, em pixels a partir do canto superior esquerdo (no PNG de 32 px).
    private static readonly Vector2 PontaEm32 = Vector2.zero;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Aplicar()
    {
        bool grande = Screen.height >= 1440;
        var bytes = Resources.Load<TextAsset>(grande ? "cursor_laboratorio_64" : "cursor_laboratorio");
        if (bytes == null) return;

        var lida = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!lida.LoadImage(bytes.bytes)) return;

        // LoadImage pode trocar o formato (ARGB32); o cursor exige RGBA32 sem mipmaps.
        var textura = new Texture2D(lida.width, lida.height, TextureFormat.RGBA32, false) { name = "CursorLaboratorio" };
        textura.SetPixels32(lida.GetPixels32());
        textura.Apply(false, false);
        Object.Destroy(lida);

        Vector2 ponta = PontaEm32 * (textura.width / 32f);
        Cursor.SetCursor(textura, ponta, CursorMode.Auto);
    }
}
