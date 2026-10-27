using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Capturas de conferência das telas montadas por código da Interacao_Vida
/// (puzzle do HUD e Game Over), feitas numa cena de pré-visualização: nada é
/// salvo na cena e nada é escrito no registro de aprendizagem.
/// </summary>
public static class TemaLabCapturas
{
    private const BindingFlags Privado = BindingFlags.NonPublic | BindingFlags.Instance;

    public static void CapturarVida(string pastaSaida)
    {
        Scene s = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Interacao_Vida.unity");
        try
        {
            GameObject apoio = new GameObject("Capturas");
            SceneManager.MoveGameObjectToScene(apoio, s);

            // Puzzle do HUD, com uma peça já encaixada no primeiro encaixe.
            HudLicao hl = Achar<HudLicao>(s);
            // Em jogo o painel de configurar item começa fechado (Awake).
            InteractionConfigUI painel = Achar<InteractionConfigUI>(s);
            if (painel != null) painel.gameObject.SetActive(false);

            var canvasGO = new GameObject("HudPuzzleUI", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            canvasGO.transform.SetParent(apoio.transform, false);
            canvasGO.GetComponent<Canvas>().sortingOrder = 70;
            var puzzle = canvasGO.AddComponent<HudPuzzleUI>();
            var tipo = typeof(HudPuzzleUI);
            tipo.GetField("textos", Privado).SetValue(puzzle, new HudPuzzleUI.Textos());
            var tipoLicao = typeof(HudLicao);
            var encaixes = (HudPuzzleUI.Encaixe[])tipoLicao.GetField("encaixes", Privado).GetValue(hl);
            var pecas = (HudPuzzleUI.Peca[])tipoLicao.GetField("pecas", Privado).GetValue(hl);
            tipo.GetMethod("Montar", Privado).Invoke(puzzle, new object[] { (RectTransform)canvasGO.transform, encaixes, pecas });
            Render(s, pastaSaida + "/novo_vida_puzzle.png");
            Object.DestroyImmediate(canvasGO);

            // Game Over.
            GameOverUI g = Achar<GameOverUI>(s);
            typeof(GameOverUI).GetMethod("Montar", Privado).Invoke(g, null);
            Render(s, pastaSaida + "/novo_vida_gameover.png");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(s);
        }
    }

    private static T Achar<T>(Scene s) where T : Component
    {
        foreach (var r in s.GetRootGameObjects())
        {
            T c = r.GetComponentInChildren<T>(true);
            if (c != null) return c;
        }
        return null;
    }

    public static void Render(Scene s, string arquivo)
    {
        Camera cam = null;
        foreach (var r in s.GetRootGameObjects())
        {
            var c = r.GetComponentInChildren<Camera>();
            if (c != null) { cam = c; break; }
        }

        cam.aspect = 1920f / 1080f;
        var camadas = SortingLayer.layers;
        foreach (var r in s.GetRootGameObjects())
        {
            foreach (var cv in r.GetComponentsInChildren<Canvas>(true))
            {
                if (!cv.isRootCanvas) continue;
                int ordem = cv.sortingOrder >= 10000 ? cv.sortingOrder - 10000 : cv.sortingOrder;
                cv.renderMode = RenderMode.ScreenSpaceCamera;
                cv.worldCamera = cam;
                cv.planeDistance = 1f;
                cv.sortingLayerID = camadas[camadas.Length - 1].id;
                cv.sortingOrder = 10000 + ordem;
                var rt = (RectTransform)cv.transform;
                rt.sizeDelta = new Vector2(1920, 1080);
                rt.localScale = Vector3.one * (2f * cam.orthographicSize / 1080f);
                rt.position = cam.transform.position + cam.transform.forward;
                rt.rotation = cam.transform.rotation;
            }
        }

        var alvo = new RenderTexture(1920, 1080, 24);
        cam.scene = s;
        cam.targetTexture = alvo;
        Canvas.ForceUpdateCanvases();
        cam.Render();
        Canvas.ForceUpdateCanvases();
        cam.Render();
        RenderTexture.active = alvo;
        var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes(arquivo, tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(alvo);
        Object.DestroyImmediate(tex);
    }
}
