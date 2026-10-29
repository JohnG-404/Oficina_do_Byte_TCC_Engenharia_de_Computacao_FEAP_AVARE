using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Painel "Cérebro do X" da IA_Aplicacao: ao clicar no Aliado ou no Guarda
/// (IASeletorNPC), mostra o diagrama DELE — caixas de estado, setas e
/// encaixes — e um inventário de condições. O jogador arrasta uma condição
/// (IADiagramaChipUI) até um encaixe (IADiagramaSlotUI) e a regra passa a
/// valer na hora, no próprio personagem (ICerebroConfiguravel).
///
/// Enquanto o painel está aberto o jogo fica pausado (Time.timeScale = 0).
/// Condição errada não reseta nada: o personagem só passa a se comportar
/// do jeito errado, e o jogador vê o resultado ao fechar.
///
/// Montado em runtime (Canvas próprio), como o CodigoMontadoUI — a cena só
/// precisa de um EventSystem. Fecha pelo botão "Fechar", pelo Esc (a pausa
/// não abre junto, ver PauseMenuUI.EscDeOutraJanela) ou clicando fora da
/// janela.
/// </summary>
public class IADiagramaUI : MonoBehaviour
{
    public static IADiagramaUI Instancia { get; private set; }
    public static bool EstaAberto => Instancia != null && Instancia.aberto;

    /// <summary>Frame em que o painel fechou (evita reabrir com o mesmo clique).</summary>
    public static int FrameFechado { get; private set; } = -1;

    /// <summary>True com o painel aberto e no frame em que fechou — pra o Esc não abrir a pausa junto.</summary>
    public static bool SegurandoEsc => EstaAberto || Time.frameCount == FrameFechado;

    /// <summary>(cérebro, encaixe, id da opção) — disparado a cada condição encaixada.</summary>
    public static event Action<ICerebroConfiguravel, int, string> OnConfigAlterada;

    /// <summary>Disparado quando o painel fecha, com o cérebro que estava aberto.</summary>
    public static event Action<ICerebroConfiguravel> OnFechou;

    // Mesma paleta do CodigoMontadoUI / mockups.
    // Cores do tema Laboratório (ver TemaLabUI).
    private static readonly Color CorFundoEscuro = TemaLabUI.CorVeu;
    private static readonly Color CorBorda = TemaLabUI.Dourado;
    private static readonly Color CorPainel = TemaLabUI.Painel;
    private static readonly Color CorArea = TemaLabUI.Poco;
    private static readonly Color CorTexto = TemaLabUI.CorTexto;
    private static readonly Color CorTextoFraco = TemaLabUI.TextoSuave;
    private static readonly Color CorTextoEscuro = new Color(0.05f, 0.08f, 0.13f, 1f);
    private static readonly Color CorCaixa = TemaLabUI.PainelElevado;
    private static readonly Color CorSeta = new Color32(0x9A, 0xA8, 0xC4, 0xFF);
    private static readonly Color CorSlotVazio = new Color32(0x16, 0x20, 0x33, 0xFF);
    private static readonly Color CorSlotAlvo = new Color32(0x1C, 0x33, 0x60, 0xFF);
    private static readonly Color CorChip = TemaLabUI.Poco;
    private static readonly Color CorAmarelo = new Color(0.96f, 0.77f, 0.26f, 1f);
    private static readonly Color CorCartao = new Color32(0x10, 0x18, 0x26, 0xFF);
    private static readonly Color CorPegador = new Color32(0x5A, 0x66, 0x7C, 0xFF);

    // Ícones das peças e o contorno tracejado do encaixe (cópias do EditorKit em Resources).
    private const string PastaRecursos = "IACerebro/";
    private static readonly Dictionary<string, Sprite> Icones = new Dictionary<string, Sprite>();

    // Tamanhos fixos (unidades do Canvas de referência 1920x1080).
    private static readonly Vector2 TamanhoJanela = new Vector2(1560f, 860f);
    private static readonly Vector2 TamanhoArea = new Vector2(1040f, 540f);
    private static readonly Vector2 CentroArea = new Vector2(-220f, 16f);
    private static readonly Vector2 TamanhoInventario = new Vector2(420f, 540f);
    private static readonly Vector2 CentroInventario = new Vector2(534f, 16f);
    private static readonly Vector2 TamanhoCaixa = new Vector2(210f, 72f);
    private static readonly Vector2 TamanhoSlot = new Vector2(360f, 62f);

    private RectTransform raiz;
    private RectTransform conteudo;
    private ICerebroConfiguravel cerebro;
    private bool aberto;
    private float timeScaleAnterior = 1f;

    private readonly List<Image> caixas = new List<Image>();
    private readonly List<TMP_Text> textosCaixas = new List<TMP_Text>();
    private readonly List<IADiagramaSlotUI> slots = new List<IADiagramaSlotUI>();
    private TMP_Text descricaoTitulo;
    private TMP_Text descricaoTexto;

    public RectTransform Raiz => raiz;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ZerarEstaticos()
    {
        Instancia = null;
        FrameFechado = -1;
        OnConfigAlterada = null;
        OnFechou = null;
    }

    /// <summary>Abre o painel com o diagrama do personagem (e pausa o jogo).</summary>
    public static void Abrir(ICerebroConfiguravel alvo)
    {
        if (alvo == null)
        {
            return;
        }

        if (Instancia == null)
        {
            Criar();
        }

        Instancia.Mostrar(alvo);
    }

    public static void Fechar()
    {
        if (Instancia != null)
        {
            Instancia.FecharPainel();
        }
    }

    private static void Criar()
    {
        var go = new GameObject("IADiagramaUI");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // cabe inteira em qualquer proporção
        go.AddComponent<GraphicRaycaster>();

        Instancia = go.AddComponent<IADiagramaUI>();
        Instancia.raiz = (RectTransform)go.transform;
    }

    private void OnDestroy()
    {
        if (Instancia == this)
        {
            if (aberto)
            {
                Time.timeScale = timeScaleAnterior;
            }
            Instancia = null;
        }
    }

    private void Update()
    {
        if (!aberto)
        {
            return;
        }

        // Esc fecha só o painel (com a pausa aberta por cima, o Esc é dela).
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame
            && (PauseMenuUI.Instance == null || !PauseMenuUI.Instance.PausaAberta))
        {
            FecharPainel();
            return;
        }

        // Mantém pausado mesmo se outro sistema (ex: menu de pausa) mexer no timeScale.
        Time.timeScale = 0f;

        int ativo = cerebro != null ? cerebro.EstadoAtivoIndice : -1;
        for (int i = 0; i < caixas.Count; i++)
        {
            bool aceso = i == ativo;
            caixas[i].color = aceso ? CorAmarelo : CorCaixa;
            textosCaixas[i].color = aceso ? CorTextoEscuro : CorTexto;
        }
    }

    // ------------------------------------------------------------------
    // Abrir / fechar
    // ------------------------------------------------------------------

    private void Mostrar(ICerebroConfiguravel alvo)
    {
        cerebro = alvo;

        if (!aberto)
        {
            timeScaleAnterior = Time.timeScale > 0f ? Time.timeScale : 1f;
            AudioManager.AvisarPainel(aberto: true);
        }

        aberto = true;
        Time.timeScale = 0f;
        Reconstruir();
        RegistroAprendizagem.Registrar("ia_cerebro_aberto", alvo.IdCerebro);
    }

    private void FecharPainel()
    {
        if (!aberto)
        {
            return;
        }

        aberto = false;
        Time.timeScale = timeScaleAnterior;
        FrameFechado = Time.frameCount;
        AudioManager.AvisarPainel(aberto: false);

        if (conteudo != null)
        {
            Destroy(conteudo.gameObject);
            conteudo = null;
        }

        ICerebroConfiguravel fechado = cerebro;
        cerebro = null;
        OnFechou?.Invoke(fechado);
    }

    // ------------------------------------------------------------------
    // Chamados pelos encaixes e peças
    // ------------------------------------------------------------------

    internal void AoSoltar(int indiceSlot, IAOpcao opcao)
    {
        if (cerebro == null || opcao == null)
        {
            return;
        }

        cerebro.SetOpcaoDoSlot(indiceSlot, opcao.id);

        IADiagrama d = cerebro.Diagrama;
        string legenda = indiceSlot < d.slots.Count ? d.slots[indiceSlot].legenda : indiceSlot.ToString();
        RegistroAprendizagem.Registrar("ia_config", $"{cerebro.IdCerebro} | {legenda} = {opcao.rotulo}");

        AtualizarSlots();
        MostrarDescricao(opcao);
        OnConfigAlterada?.Invoke(cerebro, indiceSlot, opcao.id);
    }

    internal void MostrarDescricao(IAOpcao opcao)
    {
        if (opcao == null || descricaoTitulo == null)
        {
            return;
        }

        descricaoTitulo.text = string.IsNullOrEmpty(opcao.rotuloUnity) ? opcao.rotulo : $"{opcao.rotulo}   ({opcao.rotuloUnity})";
        descricaoTexto.text = opcao.descricao;
    }

    internal void LimparDescricao()
    {
        if (descricaoTitulo == null)
        {
            return;
        }

        descricaoTitulo.text = "Descrição";
        descricaoTexto.text = "Passe o mouse numa condição pra ver o que ela faz. Arraste até um encaixe pra trocar a regra.";
    }

    internal IAOpcao OpcaoDoSlot(int indice)
    {
        return cerebro != null ? cerebro.Diagrama.BuscarOpcao(cerebro.GetOpcaoDoSlot(indice)) : null;
    }

    // ------------------------------------------------------------------
    // Montagem da tela
    // ------------------------------------------------------------------

    private void Reconstruir()
    {
        if (conteudo != null)
        {
            Destroy(conteudo.gameObject);
        }

        caixas.Clear();
        textosCaixas.Clear();
        slots.Clear();

        IADiagrama d = cerebro.Diagrama;

        conteudo = NovoRect("Conteudo", raiz);
        Esticar(conteudo, 0f);

        // Fundo escuro: clicar fora da janela fecha.
        Image fundo = NovaImagem(conteudo, "FundoEscuro", CorFundoEscuro);
        Esticar(fundo.rectTransform, 0f);
        Button fecharFora = fundo.gameObject.AddComponent<Button>();
        fecharFora.transition = Selectable.Transition.None;
        fecharFora.onClick.AddListener(FecharPainel);

        TemaLaboratorio tema = TemaLabUI.Tema;
        Image borda = NovaImagem(conteudo, "Janela", CorPainel);
        TemaLabUI.DefinirSprite(borda, tema != null ? tema.painelJanela : null, CorPainel);
        Posicionar(borda.rectTransform, Vector2.zero, TamanhoJanela);
        Image painel = NovaImagem(borda.rectTransform, "Painel", Color.clear);
        Esticar(painel.rectTransform, 4f);
        RectTransform p = painel.rectTransform;

        TMP_Text titulo = NovoTexto(p, "Titulo", d.titulo, 40f, FontStyles.Bold, CorBorda, TextAlignmentOptions.TopLeft);
        Posicionar(titulo.rectTransform, new Vector2(-190f, 376f), new Vector2(1100f, 56f));

        TMP_Text sub = NovoTexto(p, "Subtitulo", d.subtitulo, 22f, FontStyles.Italic, CorTextoFraco, TextAlignmentOptions.TopLeft);
        Posicionar(sub.rectTransform, new Vector2(-190f, 326f), new Vector2(1100f, 40f));

        CriarBotaoFechar(p);

        // Área do diagrama.
        Image area = NovaImagem(p, "Diagrama", CorArea);
        Posicionar(area.rectTransform, CentroArea, TamanhoArea);
        area.raycastTarget = false;
        RectTransform ar = area.rectTransform;

        foreach (IADiagramaSeta seta in d.setas)
        {
            DesenharSeta(ar, d, seta);
        }

        for (int i = 0; i < d.estados.Count; i++)
        {
            CriarCaixaDeEstado(ar, d.estados[i]);
        }

        for (int i = 0; i < d.slots.Count; i++)
        {
            CriarSlot(ar, d.slots[i], i);
        }

        CriarInventario(p, d);
        CriarDescricao(p);

        AtualizarSlots();
        LimparDescricao();
    }

    private void CriarBotaoFechar(RectTransform pai)
    {
        Image img = NovaImagem(pai, "Fechar", CorChip);
        Posicionar(img.rectTransform, new Vector2(640f, 372f), new Vector2(200f, 58f));
        Button b = img.gameObject.AddComponent<Button>();
        ColorBlock cores = b.colors;
        cores.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
        cores.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        b.colors = cores;
        b.onClick.AddListener(FecharPainel);

        TMP_Text t = NovoTexto(img.rectTransform, "Texto", "Fechar  <size=80%><color=#8A97AD>Esc</color></size>", 26f, FontStyles.Bold, CorTexto, TextAlignmentOptions.Center);
        Esticar(t.rectTransform, 0f);
    }

    private static Vector2 NoDiagrama(Vector2 normalizada)
    {
        return new Vector2((normalizada.x - 0.5f) * TamanhoArea.x, (normalizada.y - 0.5f) * TamanhoArea.y);
    }

    private static Vector2 TamanhoDe(IADiagramaEstado estado)
    {
        return estado.tamanho.sqrMagnitude > 0f ? estado.tamanho : TamanhoCaixa;
    }

    private void CriarCaixaDeEstado(RectTransform area, IADiagramaEstado estado)
    {
        Image img = NovaImagem(area, "Estado_" + estado.nome, CorCaixa);
        Posicionar(img.rectTransform, NoDiagrama(estado.posicao), TamanhoDe(estado));
        img.raycastTarget = false;

        TMP_Text t = NovoTexto(img.rectTransform, "Nome", estado.nome, 30f, FontStyles.Bold, CorTexto, TextAlignmentOptions.Center);
        Esticar(t.rectTransform, 4f);

        caixas.Add(img);
        textosCaixas.Add(t);
    }

    // Seta de uma caixa até a outra, cortada na borda das caixas, com ponta em "V".
    private void DesenharSeta(RectTransform area, IADiagrama d, IADiagramaSeta seta)
    {
        if (seta.de < 0 || seta.de >= d.estados.Count || seta.para < 0 || seta.para >= d.estados.Count)
        {
            return;
        }

        Vector2 ca = NoDiagrama(d.estados[seta.de].posicao);
        Vector2 cb = NoDiagrama(d.estados[seta.para].posicao);
        Vector2 dir = (cb - ca).normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        float deslocamento = Mathf.Clamp(seta.desvio * TamanhoArea.y, -60f, 60f);

        Vector2 s = ca + perp * deslocamento;
        Vector2 e = cb + perp * deslocamento;
        Vector2 meiaA = TamanhoDe(d.estados[seta.de]) * 0.5f + new Vector2(8f, 8f);
        Vector2 meiaB = TamanhoDe(d.estados[seta.para]) * 0.5f + new Vector2(8f, 8f);
        Vector2 inicio = s + dir * SaidaDaCaixa(s, dir, ca, meiaA);
        Vector2 fim = e - dir * SaidaDaCaixa(e, -dir, cb, meiaB);

        NovaLinha(area, "Seta", inicio, fim, 4f, CorSeta);

        float angulo = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        foreach (float lado in new[] { 150f, -150f })
        {
            float a = (angulo + lado) * Mathf.Deg2Rad;
            NovaLinha(area, "Ponta", fim, fim + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 20f, 4f, CorSeta);
        }

        if (!string.IsNullOrEmpty(seta.rotuloFixo))
        {
            Vector2 meio = (inicio + fim) * 0.5f + new Vector2(seta.deslocamentoRotulo.x * TamanhoArea.x, seta.deslocamentoRotulo.y * TamanhoArea.y);
            TMP_Text rotulo = NovoTexto(area, "RegraFixa", seta.rotuloFixo, 20f, FontStyles.Normal, CorTextoFraco, TextAlignmentOptions.Center);
            rotulo.richText = false;
            Posicionar(rotulo.rectTransform, meio, new Vector2(320f, 56f));
        }
    }

    // Distância, a partir de um ponto dentro da caixa, até a borda na direção dir.
    private static float SaidaDaCaixa(Vector2 ponto, Vector2 dir, Vector2 centro, Vector2 meia)
    {
        float tx = float.MaxValue, ty = float.MaxValue;
        if (dir.x > 0.0001f) tx = (centro.x + meia.x - ponto.x) / dir.x;
        else if (dir.x < -0.0001f) tx = (centro.x - meia.x - ponto.x) / dir.x;
        if (dir.y > 0.0001f) ty = (centro.y + meia.y - ponto.y) / dir.y;
        else if (dir.y < -0.0001f) ty = (centro.y - meia.y - ponto.y) / dir.y;
        float t = Mathf.Min(tx, ty);
        return t == float.MaxValue ? 0f : Mathf.Max(0f, t);
    }

    private void CriarSlot(RectTransform area, IADiagramaSlot def, int indice)
    {
        Vector2 pos = NoDiagrama(def.posicao);

        // Legenda colada no encaixe: em cima, ou embaixo no encaixe da seta de baixo.
        float lado = def.legendaEmbaixo ? -1f : 1f;
        TMP_Text legenda = NovoTexto(area, "Legenda", def.legenda, 19f, FontStyles.Normal, CorTextoFraco,
            def.legendaEmbaixo ? TextAlignmentOptions.Top : TextAlignmentOptions.Bottom);
        legenda.richText = false;
        Posicionar(legenda.rectTransform, pos + new Vector2(0f, lado * (TamanhoSlot.y * 0.5f + 16f)), new Vector2(TamanhoSlot.x + 60f, 28f));

        // Fundo opaco (a seta passa por trás) com o contorno tracejado do encaixe vazio da Física.
        Sprite redondo = TemaLabUI.Tema != null ? TemaLabUI.Tema.bordaArredondada : null;
        Image img = NovaImagem(area, "Encaixe_" + indice, CorSlotVazio);
        TemaLabUI.DefinirSprite(img, redondo, CorSlotVazio);
        img.color = CorSlotVazio;
        Posicionar(img.rectTransform, pos, TamanhoSlot);

        Image tracejado = NovaImagem(img.rectTransform, "Tracejado", Color.white);
        tracejado.sprite = Resources.Load<Sprite>(PastaRecursos + "encaixe_vazio");
        tracejado.raycastTarget = false;
        tracejado.enabled = tracejado.sprite != null;
        Esticar(tracejado.rectTransform, 0f);
        tracejado.rectTransform.offsetMin = new Vector2(-10f, 0f);  // o sprite tem o dente à esquerda

        TMP_Text vazio = NovoTexto(img.rectTransform, "Rotulo", "arraste aqui", 22f, FontStyles.Normal, CorTextoFraco, TextAlignmentOptions.Center);
        vazio.richText = false;
        Esticar(vazio.rectTransform, 6f);

        IADiagramaSlotUI slot = img.gameObject.AddComponent<IADiagramaSlotUI>();
        slot.Configurar(this, indice, img, tracejado, vazio, CorSlotVazio, CorSlotAlvo);
        slots.Add(slot);
    }

    private void AtualizarSlots()
    {
        foreach (IADiagramaSlotUI s in slots)
        {
            s.Atualizar(OpcaoDoSlot(s.Indice));
        }
    }

    private void CriarInventario(RectTransform pai, IADiagrama d)
    {
        Image fundo = NovaImagem(pai, "Inventario", CorArea);
        Posicionar(fundo.rectTransform, CentroInventario, TamanhoInventario);
        fundo.raycastTarget = false;
        RectTransform inv = fundo.rectTransform;

        TMP_Text cab = NovoTexto(inv, "Titulo", "Inventário de condições", 24f, FontStyles.Bold, CorBorda, TextAlignmentOptions.Center);
        Posicionar(cab.rectTransform, new Vector2(0f, TamanhoInventario.y * 0.5f - 30f), new Vector2(TamanhoInventario.x - 20f, 40f));

        float alturaChip = 62f;
        float espaco = 13f;
        float y = TamanhoInventario.y * 0.5f - 64f - alturaChip * 0.5f;
        foreach (IAOpcao opcao in d.inventario)
        {
            RectTransform chip = CriarVisualChip(inv, opcao, new Vector2(TamanhoInventario.x - 38f, alturaChip));
            chip.anchoredPosition = new Vector2(0f, y);
            IADiagramaChipUI c = chip.gameObject.AddComponent<IADiagramaChipUI>();
            c.Configurar(this, opcao);
            y -= alturaChip + espaco;
        }
    }

    /// <summary>
    /// Visual de uma condição (no inventário, no encaixe e no "fantasma" do
    /// arrasto): cartão como as peças da Física e da IA, com borda na cor da
    /// categoria, dente de encaixe à esquerda, ícone, os dois nomes e o
    /// pegador à direita.
    /// </summary>
    internal RectTransform CriarVisualChip(RectTransform pai, IAOpcao opcao, Vector2 tamanho)
    {
        Color cor = CorDaArte(opcao.arte);
        Sprite redondo = TemaLabUI.Tema != null ? TemaLabUI.Tema.bordaArredondada : null;

        Image img = NovaImagem(pai, "Condicao_" + opcao.id, cor);
        TemaLabUI.DefinirSprite(img, redondo, cor);
        img.color = cor;
        Posicionar(img.rectTransform, Vector2.zero, tamanho);

        Image dente = NovaImagem(img.rectTransform, "Dente", cor);
        TemaLabUI.DefinirSprite(dente, redondo, cor);
        dente.color = cor;
        dente.raycastTarget = false;
        Posicionar(dente.rectTransform, new Vector2(-tamanho.x * 0.5f - 3f, 0f), new Vector2(14f, 20f));

        Image miolo = NovaImagem(img.rectTransform, "Miolo", CorCartao);
        TemaLabUI.DefinirSprite(miolo, redondo, CorCartao);
        miolo.color = CorCartao;
        miolo.raycastTarget = false;
        Esticar(miolo.rectTransform, 3f);

        float ladoIcone = Mathf.Max(24f, tamanho.y - 22f);
        float textoX = 16f;
        Sprite icone = IconeDe(opcao.arte);
        if (icone != null)
        {
            Image ic = NovaImagem(img.rectTransform, "Icone", Color.white);
            ic.sprite = icone;
            ic.preserveAspect = true;
            ic.raycastTarget = false;
            Posicionar(ic.rectTransform, new Vector2(-tamanho.x * 0.5f + 14f + ladoIcone * 0.5f, 0f), new Vector2(ladoIcone, ladoIcone));
            textoX = 14f + ladoIcone + 12f;
        }

        bool temUnity = !string.IsNullOrEmpty(opcao.rotuloUnity);
        float meio = tamanho.y * 0.5f;

        TMP_Text principal = NovoTexto(img.rectTransform, "Rotulo", opcao.rotulo, 21f, FontStyles.Bold, CorTexto,
            temUnity ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.Left);
        principal.richText = false;
        Esticar(principal.rectTransform, 0f);
        principal.rectTransform.offsetMin = new Vector2(textoX, temUnity ? meio - 3f : 4f);
        principal.rectTransform.offsetMax = new Vector2(-34f, -4f);

        if (temUnity)
        {
            TMP_Text unity = NovoTexto(img.rectTransform, "Unity", opcao.rotuloUnity, 15f, FontStyles.Normal, cor, TextAlignmentOptions.TopLeft);
            unity.richText = false;
            Esticar(unity.rectTransform, 0f);
            unity.rectTransform.offsetMin = new Vector2(textoX, 4f);
            unity.rectTransform.offsetMax = new Vector2(-34f, -(meio + 3f));
        }

        // Pegador (2x3 pontinhos): mostra que a peça dá pra arrastar.
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                Image ponto = NovaImagem(img.rectTransform, "Pegador", CorPegador);
                ponto.raycastTarget = false;
                Posicionar(ponto.rectTransform, new Vector2(tamanho.x * 0.5f - 22f + j * 8f, 8f - i * 8f), new Vector2(4f, 4f));
            }
        }

        return img.rectTransform;
    }

    // Mesmas cores dos ícones das peças (EditorKit/icones/componentes).
    private static Color CorDaArte(string arte)
    {
        switch (arte)
        {
            case "vector2": return new Color32(0x81, 0x8C, 0xF8, 0xFF);
            case "isgrounded": return new Color32(0xD9, 0x46, 0xEF, 0xFF);
            case "oncollisionenter2d":
            case "ontriggerenter2d": return new Color32(0xEF, 0x44, 0x44, 0xFF);
            case "input": return new Color32(0xF4, 0x72, 0xB6, 0xFF);
            case "start": return new Color32(0xA3, 0xE6, 0x35, 0xFF);
            case "tag": return new Color32(0xCB, 0xD5, 0xE1, 0xFF);
            default: return TemaLabUI.Contorno;
        }
    }

    private static Sprite IconeDe(string arte)
    {
        if (string.IsNullOrEmpty(arte))
        {
            return null;
        }

        if (!Icones.TryGetValue(arte, out Sprite s))
        {
            s = Resources.Load<Sprite>(PastaRecursos + "icone_" + arte);
            Icones[arte] = s;
        }

        return s;
    }

    private void CriarDescricao(RectTransform pai)
    {
        Image fundo = NovaImagem(pai, "Descricao", CorArea);
        Posicionar(fundo.rectTransform, new Vector2(0f, -338f), new Vector2(1480f, 120f));
        fundo.raycastTarget = false;

        descricaoTitulo = NovoTexto(fundo.rectTransform, "Titulo", "", 24f, FontStyles.Bold, CorBorda, TextAlignmentOptions.TopLeft);
        descricaoTitulo.richText = false;
        Esticar(descricaoTitulo.rectTransform, 14f);

        descricaoTexto = NovoTexto(fundo.rectTransform, "Texto", "", 22f, FontStyles.Normal, CorTexto, TextAlignmentOptions.TopLeft);
        descricaoTexto.richText = false;
        Esticar(descricaoTexto.rectTransform, 14f);
        descricaoTexto.rectTransform.offsetMax = new Vector2(-14f, -46f);
    }

    // ------------------------------------------------------------------
    // Utilitários de UI
    // ------------------------------------------------------------------

    private static RectTransform NovoRect(string nome, Transform pai)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);
        return (RectTransform)go.transform;
    }

    private static Image NovaImagem(Transform pai, string nome, Color cor)
    {
        RectTransform rt = NovoRect(nome, pai);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = cor;
        return img;
    }

    internal static TMP_Text NovoTexto(Transform pai, string nome, string texto, float tamanho, FontStyles estilo, Color cor, TextAlignmentOptions alinhamento)
    {
        RectTransform rt = NovoRect(nome, pai);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.fontSize = tamanho;
        // Fontes do kit: títulos em Baloo, condições em JetBrains Mono, o resto em Nunito.
        tmp.font = TemaLabUI.FonteDe(tamanho >= 36f ? TemaLabUI.Fonte.Display : TemaLabFases.FonteParaTexto(texto, estilo == FontStyles.Bold));
        tmp.fontStyle = estilo == FontStyles.Italic ? FontStyles.Normal : FontStyles.Normal;
        tmp.color = cor;
        tmp.alignment = alinhamento;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax = tamanho;
        tmp.fontSizeMin = Mathf.Max(10f, tamanho * 0.55f);
        return tmp;
    }

    private static void NovaLinha(RectTransform pai, string nome, Vector2 de, Vector2 ate, float espessura, Color cor)
    {
        Image img = NovaImagem(pai, nome, cor);
        img.raycastTarget = false;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        Vector2 delta = ate - de;
        rt.sizeDelta = new Vector2(delta.magnitude, espessura);
        rt.anchoredPosition = de;
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    private static void Posicionar(RectTransform rt, Vector2 posicao, Vector2 tamanho)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = tamanho;
        rt.anchoredPosition = posicao;
    }

    private static void Esticar(RectTransform rt, float margem)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(margem, margem);
        rt.offsetMax = new Vector2(-margem, -margem);
    }
}
