using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Deixa as fases iguais em qualquer resolução: as fases foram montadas para
/// 16:9 (câmera ortográfica de tamanho 5), então numa tela 4:3, 16:10 ou
/// ultrawide a câmera mostraria pedaço a menos ou além da borda do cenário.
/// Com "Manter proporção 16:9" ligado (padrão), a área da câmera vira um
/// retângulo 16:9 centralizado e as sobras ficam pretas.
///
/// Funciona sozinho em todas as cenas (criado ao abrir a primeira cena);
/// só mexe no Viewport Rect das câmeras base que desenham na tela.
/// </summary>
public class AjusteProporcaoCamera : MonoBehaviour
{
    public const float Proporcao = 16f / 9f;

    private static AjusteProporcaoCamera instancia;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instancia = null;

    private Camera fundo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (instancia != null) return;

        var go = new GameObject("AjusteProporcaoCamera (Auto)");
        instancia = go.AddComponent<AjusteProporcaoCamera>();
        DontDestroyOnLoad(go);
    }

    /// <summary>Retângulo 16:9 (em fração da tela) onde o jogo é desenhado.</summary>
    public static Rect AreaDoJogo()
    {
        if (!ConfiguracoesJogo.ManterProporcao || Screen.height <= 0) return new Rect(0f, 0f, 1f, 1f);

        float tela = (float)Screen.width / Screen.height;
        if (Mathf.Abs(tela - Proporcao) < 0.01f) return new Rect(0f, 0f, 1f, 1f);

        if (tela > Proporcao)
        {
            float w = Proporcao / tela;          // faixas nas laterais
            return new Rect((1f - w) / 2f, 0f, w, 1f);
        }

        float h = tela / Proporcao;              // faixas em cima e embaixo
        return new Rect(0f, (1f - h) / 2f, 1f, h);
    }

    private void Awake()
    {
        // Câmera que só pinta de preto a tela inteira, por baixo das outras.
        var go = new GameObject("Faixas pretas");
        go.transform.SetParent(transform, false);
        fundo = go.AddComponent<Camera>();
        fundo.clearFlags = CameraClearFlags.SolidColor;
        fundo.backgroundColor = Color.black;
        fundo.cullingMask = 0;
        fundo.depth = -100;
        fundo.orthographic = true;
        fundo.useOcclusionCulling = false;
        var dados = go.AddComponent<UniversalAdditionalCameraData>();
        dados.renderPostProcessing = false;
        dados.renderShadows = false;
        fundo.enabled = false;
    }

    private void LateUpdate()
    {
        Rect area = AreaDoJogo();
        bool cheia = area.width >= 0.999f && area.height >= 0.999f;
        if (fundo.enabled == cheia) fundo.enabled = !cheia;

        foreach (Camera c in Camera.allCameras)
        {
            if (c == fundo || c.targetTexture != null || c.cameraType != CameraType.Game) continue;

            // Câmeras Overlay do URP herdam o retângulo da câmera base.
            var urp = c.GetComponent<UniversalAdditionalCameraData>();
            if (urp != null && urp.renderType == CameraRenderType.Overlay) continue;

            if (c.rect != area) c.rect = area;
        }
    }
}
