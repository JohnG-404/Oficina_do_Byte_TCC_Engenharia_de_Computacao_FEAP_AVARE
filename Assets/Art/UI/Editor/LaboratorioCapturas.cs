using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tira capturas do Game view das telas do tema Laboratório (menu,
/// configurações com as 3 abas, pausa e seletor de fases) em 1920x1080 e
/// 1440x1080. Abre o Menu, entra em Play, captura e sai do Play sozinho.
/// Arquivos em Capturas_UI/ (na raiz do projeto, fora de Assets).
/// </summary>
public static class LaboratorioCapturas
{
    const string Pasta = "Capturas_UI";
    const string ChaveAtivo = "LaboratorioCapturas.ativo";
    static readonly Vector2Int[] Tamanhos = { new Vector2Int(1920, 1080), new Vector2Int(1440, 1080) };

    static List<Action> passos;
    static int passo;
    static double proximo;

    [MenuItem("Tools/UI Laboratório/Capturar telas (Play)")]
    public static void Capturar()
    {
        if (EditorApplication.isPlaying) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/Scenes/Menu.unity", OpenSceneMode.Single);
        Directory.CreateDirectory(Pasta);
        SessionState.SetBool(ChaveAtivo, true);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Registrar()
    {
        EditorApplication.playModeStateChanged += estado =>
        {
            if (estado == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ChaveAtivo, false))
            {
                SessionState.SetBool(ChaveAtivo, false);
                Montar();
                passo = 0;
                proximo = EditorApplication.timeSinceStartup + 2.5;
                EditorApplication.update += Tick;
            }
        };
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= Tick;
            return;
        }

        if (EditorApplication.timeSinceStartup < proximo) return;

        if (passo >= passos.Count)
        {
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
            Debug.Log("[LaboratorioCapturas] Capturas salvas em " + Path.GetFullPath(Pasta));
            return;
        }

        try { passos[passo](); }
        catch (Exception e) { Debug.LogException(e); }
        passo++;
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        proximo = EditorApplication.timeSinceStartup + 1.5;
    }

    static void Montar()
    {
        passos = new List<Action>();
        if (SessionState.GetBool(ChaveAudio, false))
        {
            MontarAudio();
            MontarSoMenu();
            return;
        }
        if (SessionState.GetBool(ChaveSoMenu, false))
        {
            MontarSoMenu();
            return;
        }
        foreach (Vector2Int t in Tamanhos)
        {
            string sufixo = $"{t.x}x{t.y}";
            passos.Add(() => DefinirTamanho(t.x, t.y));
            passos.Add(() => Salvar($"menu_{sufixo}"));
            passos.Add(() =>
            {
                SettingsPanelUI.Instance.Show(cameFromPauseMenu: false);
                Debug.Log($"[LaboratorioCapturas] Configurações visível: {SettingsPanelUI.Instance.IsVisible}");
            });
            passos.Add(() => Salvar($"config_audio_{sufixo}"));
            passos.Add(() => Aba("GraphicsTabButton"));
            passos.Add(() => Salvar($"config_graficos_{sufixo}"));
            passos.Add(() => Aba("ControlsTabButton"));
            passos.Add(() => Salvar($"config_controles_{sufixo}"));
            passos.Add(() => { Aba("AudioTabButton"); SettingsPanelUI.Instance.Hide(); PauseMenuUI.Instance.ShowPausePanelOnly(); });
            passos.Add(() => Salvar($"pausa_{sufixo}"));
            passos.Add(() => { PauseMenuUI.Instance.HidePausePanelOnly(); SeletorFasesUI.Abrir(); });
            passos.Add(() => Salvar($"fases_{sufixo}"));
            passos.Add(SeletorFasesUI.Fechar);
        }

        // Progresso simulado só na memória (faça backup do save antes: ao sair do Play o jogo grava).
        if (SimularProgresso)
        {
            passos.Add(() => DefinirTamanho(1920, 1080));
            passos.Add(() =>
            {
                var d = ProgressoJogo.Atual;
                d.preTeste.feito = true;
                d.preTeste.nota = 6;
                d.preTeste.total = 10;
                foreach (string cena in new[] { "Fisica", "Aplicacao_Fisica", "Interacao" })
                    d.fases.Add(new ProgressoJogo.FaseSalva { cena = cena, concluida = true, vezesConcluida = 1, tentativas = 2, melhorTempo = 95f });
                d.fases.Add(new ProgressoJogo.FaseSalva { cena = "Interacao_Vida", tentativas = 1 });
                var painel = GameObject.Find("Canvas/PainelMenu");
                painel.SetActive(false);
                painel.SetActive(true);
            });
            passos.Add(() => Salvar("menu_com_progresso_1920x1080"));
            passos.Add(SeletorFasesUI.Abrir);
            passos.Add(() => Salvar("fases_com_progresso_1920x1080"));
        }
    }

    const string ChaveAudio = "LaboratorioCapturas.audio";
    static float volGeral, volMusica, volEfeitos;

    // Aba Áudio: mexe nos 3 sliders, confere AudioManager/PlayerPrefs/AudioSources,
    // reabre o painel pra ver se o valor volta, captura e devolve os volumes originais.
    static void MontarAudio()
    {
        passos.Add(() =>
        {
            var am = AudioManager.Instance;
            volGeral = am.MasterVolume; volMusica = am.MusicVolume; volEfeitos = am.SfxVolume;
            Debug.Log($"[LaboratorioCapturas] Áudio original: geral={volGeral:0.00} música={volMusica:0.00} efeitos={volEfeitos:0.00} faixa={am.CurrentTrack} fontes={Fontes(am)}");
            SettingsPanelUI.Instance.Show(cameFromPauseMenu: false);
        });
        passos.Add(() =>
        {
            SliderAudio("VolumeSlider").value = 0.8f;
            SliderAudio("LinhaMusica/MusicaSlider").value = 0.4f;
            SliderAudio("LinhaEfeitos/EfeitosSlider").value = 0.65f;
        });
        passos.Add(() =>
        {
            var am = AudioManager.Instance;
            Debug.Log($"[LaboratorioCapturas] Depois dos sliders: geral={am.MasterVolume:0.00} música={am.MusicVolume:0.00} efeitos={am.SfxVolume:0.00} " +
                      $"prefs=({PlayerPrefs.GetFloat(AudioManager.MasterVolumeKey):0.00},{PlayerPrefs.GetFloat(AudioManager.MusicVolumeKey):0.00},{PlayerPrefs.GetFloat(AudioManager.SfxVolumeKey):0.00}) " +
                      $"listener={AudioListener.volume:0.00} fontes={Fontes(am)}");
            SettingsPanelUI.Instance.Hide();
        });
        passos.Add(() =>
        {
            SettingsPanelUI.Instance.Show(cameFromPauseMenu: false);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(SliderAudio("LinhaMusica/MusicaSlider").gameObject);
        });
        passos.Add(() =>
        {
            Debug.Log($"[LaboratorioCapturas] Reaberto: sliders=({SliderAudio("VolumeSlider").value:0.00},{SliderAudio("LinhaMusica/MusicaSlider").value:0.00},{SliderAudio("LinhaEfeitos/EfeitosSlider").value:0.00}) " +
                      $"textos=({Texto("VolumeValueLabel")},{Texto("LinhaMusica/Valor")},{Texto("LinhaEfeitos/Valor")})");
        });
        foreach (Vector2Int t in Tamanhos)
        {
            passos.Add(() => PrepararRender(PauseMenuUI.Instance.GetComponent<Canvas>(), t.x, t.y));
            passos.Add(() => { });
            passos.Add(() => SalvarRender($"config_audio_{t.x}x{t.y}"));
        }
        passos.Add(() =>
        {
            // Devolve os volumes que o jogador tinha (também gravados em PlayerPrefs).
            var am = AudioManager.Instance;
            am.SetMasterVolume(volGeral);
            am.SetMusicVolume(volMusica);
            am.SetSfxVolume(volEfeitos);
            SettingsPanelUI.Instance.Hide();
            Debug.Log($"[LaboratorioCapturas] Volumes restaurados: geral={am.MasterVolume:0.00} música={am.MusicVolume:0.00} efeitos={am.SfxVolume:0.00}");
        });
    }

    static Slider SliderAudio(string caminho) =>
        SettingsPanelUI.Instance.transform.Find("ContentArea/AudioContent/" + caminho).GetComponent<Slider>();

    static string Texto(string caminho) =>
        SettingsPanelUI.Instance.transform.Find("ContentArea/AudioContent/" + caminho).GetComponent<TMPro.TMP_Text>().text;

    static string Fontes(AudioManager am)
    {
        var partes = new List<string>();
        foreach (var src in am.GetComponentsInChildren<AudioSource>(true))
            partes.Add($"{src.name}:{src.volume:0.00}{(src.isPlaying ? "▶" : "")}");
        return string.Join(" ", partes);
    }

    const string ChaveSoMenu = "LaboratorioCapturas.soMenu";

    // Só o botão Jogar/Continuar do menu: sem progresso e com progresso simulado
    // em memória (faça backup do save antes: ao sair do Play o jogo grava).
    static void MontarSoMenu()
    {
        foreach (Vector2Int t in Tamanhos)
        {
            passos.Add(() => PrepararRender("Canvas", t.x, t.y));
            passos.Add(() => { }); // dá tempo do Canvas refazer o layout no tamanho novo
            passos.Add(() => SalvarRender($"menu_sem_progresso_{t.x}x{t.y}"));
        }

        passos.Add(() =>
        {
            var d = ProgressoJogo.Atual;
            d.preTeste.feito = true;
            foreach (string cena in new[] { "Fisica", "Aplicacao_Fisica", "Interacao" })
                d.fases.Add(new ProgressoJogo.FaseSalva { cena = cena, concluida = true, vezesConcluida = 1, tentativas = 1 });
            // O MenuControl troca pra "Continuar" quando o save muda; aqui o save é só de memória.
            var rotulo = GameObject.Find("Canvas/PainelMenu/Botoes/Jogar/Text (TMP)");
            if (rotulo != null) rotulo.GetComponent<TMPro.TMP_Text>().text = "Continuar";
            var painel = GameObject.Find("Canvas/PainelMenu");
            painel.SetActive(false);
            painel.SetActive(true);
        });

        foreach (Vector2Int t in Tamanhos)
        {
            passos.Add(() => PrepararRender("Canvas", t.x, t.y));
            passos.Add(() => { }); // dá tempo do Canvas refazer o layout no tamanho novo
            passos.Add(() => SalvarRender($"menu_com_progresso_{t.x}x{t.y}"));
        }
    }

    // Captura sem depender do Game view: o Canvas passa a desenhar numa câmera
    // própria com RenderTexture do tamanho pedido (o CanvasScaler usa esse tamanho).
    static Camera camCaptura;
    static RenderTexture rtCaptura;
    static Canvas canvasCaptura;
    static RenderMode modoOriginal;

    static void PrepararRender(string canvas, int w, int h) => PrepararRender(GameObject.Find(canvas).GetComponent<Canvas>(), w, h);

    static void PrepararRender(Canvas canvas, int w, int h)
    {
        canvasCaptura = canvas;
        modoOriginal = canvasCaptura.renderMode;
        rtCaptura = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        var go = new GameObject("CameraCaptura (temporária)");
        camCaptura = go.AddComponent<Camera>();
        camCaptura.orthographic = true;
        camCaptura.clearFlags = CameraClearFlags.SolidColor;
        camCaptura.backgroundColor = Color.black;
        camCaptura.targetTexture = rtCaptura;
        camCaptura.transform.position = new Vector3(0, 0, -5000);
        canvasCaptura.renderMode = RenderMode.ScreenSpaceCamera;
        canvasCaptura.worldCamera = camCaptura;
        canvasCaptura.planeDistance = 10;
    }

    static void SalvarRender(string nome)
    {
        // Com o Editor em segundo plano a câmera não desenha sozinha: força o
        // layout do Canvas e um render direto na RenderTexture.
        Canvas.ForceUpdateCanvases();
        var pedido = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = rtCaptura };
        if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camCaptura, pedido))
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camCaptura, pedido);
        else
            camCaptura.Render();

        var anterior = RenderTexture.active;
        RenderTexture.active = rtCaptura;
        var tex = new Texture2D(rtCaptura.width, rtCaptura.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rtCaptura.width, rtCaptura.height), 0, 0);
        tex.Apply();
        RenderTexture.active = anterior;
        File.WriteAllBytes(Path.Combine(Pasta, nome + ".png"), tex.EncodeToPNG());

        canvasCaptura.renderMode = modoOriginal;
        canvasCaptura.worldCamera = null;
        UnityEngine.Object.Destroy(camCaptura.gameObject);
        UnityEngine.Object.Destroy(tex);
        rtCaptura.Release();
    }

    const string ChaveSimular = "LaboratorioCapturas.simular";
    static bool SimularProgresso => SessionState.GetBool(ChaveSimular, false);

    static void Aba(string nome)
    {
        var t = SettingsPanelUI.Instance.transform.Find("TabBar/" + nome);
        t.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
    }

    static void Salvar(string nome)
    {
        string caminho = Path.Combine(Pasta, nome + ".png");
        ScreenCapture.CaptureScreenshot(caminho);
        // A captura sai no fim do próximo quadro do Game view: força o redesenho
        // (com o Editor sem foco, o Game view às vezes não redesenha sozinho).
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    // Game view com resolução fixa (API interna do Editor, via reflexão).
    static void DefinirTamanho(int w, int h)
    {
        Assembly ed = typeof(Editor).Assembly;
        Type tSizes = ed.GetType("UnityEditor.GameViewSizes");
        Type tSingleton = typeof(ScriptableSingleton<>).MakeGenericType(tSizes);
        object sizes = tSingleton.GetProperty("instance").GetValue(null);
        object grupo = tSizes.GetMethod("GetGroup").Invoke(sizes, new object[] { (int)GameViewSizeGroupType.Standalone });
        Type tGrupo = grupo.GetType();

        string nome = $"Lab {w}x{h}";
        int indice = -1;
        string[] textos = (string[])tGrupo.GetMethod("GetDisplayTexts").Invoke(grupo, null);
        for (int i = 0; i < textos.Length; i++)
        {
            if (textos[i].StartsWith(nome)) { indice = i; break; }
        }

        if (indice < 0)
        {
            Type tSize = ed.GetType("UnityEditor.GameViewSize");
            Type tTipo = ed.GetType("UnityEditor.GameViewSizeType");
            ConstructorInfo ctor = tSize.GetConstructor(new[] { tTipo, typeof(int), typeof(int), typeof(string) });
            object novo = ctor.Invoke(new object[] { Enum.ToObject(tTipo, 1), w, h, nome });
            tGrupo.GetMethod("AddCustomSize").Invoke(grupo, new[] { novo });
            indice = (int)tGrupo.GetMethod("GetTotalCount").Invoke(grupo, null) - 1;
        }

        Type tGameView = ed.GetType("UnityEditor.GameView");
        EditorWindow gv = EditorWindow.GetWindow(tGameView);
        tGameView.GetMethod("SizeSelectionCallback", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(gv, new object[] { indice, null });
    }
}
