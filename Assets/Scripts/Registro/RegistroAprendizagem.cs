using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Registro automático do que o jogador faz, usado como evidência de
/// aprendizagem (TCC). Cada evento vira uma linha de um CSV em
/// PastaDados.Raiz/Registros/ — gravada na hora, então nada se
/// perde se o jogo fechar no meio.
///
/// Se o arquivo estiver travado (o professor abriu o CSV no Excel com o jogo
/// rodando), as linhas ficam guardadas na ordem e entram assim que der: no
/// próximo evento, a cada 5 segundos ou ao fechar o jogo. Se ao fechar ainda
/// estiver travado, o que faltou vai para "<mesmo nome>_continuacao.csv".
///
/// Fechar o jogo (X, Alt+F4 ou Sair do Menu) encerra a cena e a sessão no
/// arquivo: "cena_saida", "cena_tempo_ativo" (numa fase) e "sessao_fim" com
/// detalhe "jogo_fechado" ou "botao_sair". O "sessao_fim" do fim do
/// questionário final continua sem detalhe.
///
/// Colunas (separadas por ";", abre direto no Excel em português):
/// sessao; data_hora; segundos_desde_o_inicio; cena; evento; detalhe; jogador
///
/// "jogador" é o apelido ou o código anônimo escolhido no Menu (ver
/// Jogadores), também no nome do arquivo. Fica no fim pra não mudar a posição
/// das colunas dos registros antigos.
///
/// Além do arquivo, guarda um resumo em memória (erros de puzzle, falhas,
/// notas do questionário) que a tela final do Questionario mostra.
///
/// Entradas e saídas de cena são registradas sozinhas; as fases só chamam
/// Registrar/RegistrarErro/RegistrarFalha nos momentos que importam.
///
/// Pausas nas fases (menu de pausa, Configurações ou janela fora de foco)
/// também são sozinhas: "pausa_inicio" (detalhe: menu, configuracoes ou
/// janela_fora_de_foco) e "pausa_fim" (detalhe: duração). Na saída de uma
/// fase, depois do "cena_saida" (tempo total, como sempre foi), vem o
/// "cena_tempo_ativo": o mesmo tempo sem as pausas. A coluna "segundos"
/// continua contando tudo.
/// </summary>
public static class RegistroAprendizagem
{
    private const string Pasta = "Registros";

    public static string CaminhoArquivo { get; private set; }
    public static string CenaAtual { get; private set; } = "";
    public static string CenaAnterior { get; private set; } = "";

    /// <summary>Peças/sequências erradas (Fisica, Tutorial_Anim).</summary>
    public static int ErrosDePuzzle { get; private set; }

    /// <summary>Quedas, mortes e reinícios por erro (Aplicacao_Fisica, Interacao, Animacao).</summary>
    public static int Falhas { get; private set; }

    public static float SegundosDeJogo => sessaoIniciada ? Time.realtimeSinceStartup - inicioSessao : 0f;

    /// <summary>Acertos por bloco no pré e no pós-teste (-1 = não respondido).</summary>
    public static readonly Dictionary<string, int> AcertosPre = new Dictionary<string, int>();
    public static readonly Dictionary<string, int> AcertosPos = new Dictionary<string, int>();
    public static int NotaPre { get; private set; } = -1;
    public static int NotaPos { get; private set; } = -1;

    private static string idSessao;
    private static Scene cenaRegistrada;
    private static bool sessaoIniciada;
    private static float inicioSessao;
    private static float inicioCena;
    private static bool avisouFalhaDeEscrita;

    // Linhas que ainda não entraram no arquivo (caminho, linha), na ordem.
    private static readonly List<KeyValuePair<string, string>> pendentes = new List<KeyValuePair<string, string>>();
    private const float IntervaloNovaTentativa = 5f;
    private static float proximaTentativa;
    private static bool gravacaoTravada;
    private static bool sessaoEncerrada;
    private static bool jogoFechando;
    private static string motivoSaida;

    // Pausa em andamento e soma das pausas desde o começo do Play (ver RelogioPausa).
    private static bool emPausa;
    private static float inicioPausa;
    private static float pausaAcumulada;
    private static float pausaNoInicioCena;
    private static bool janelaEmFoco = true;
    private static RelogioPausa relogio;

    /// <summary>Segundos parados em pausa desde o começo do Play, contando a pausa aberta agora.</summary>
    public static float SegundosEmPausa => pausaAcumulada + (emPausa ? Time.realtimeSinceStartup - inicioPausa : 0f);

    // SubsystemRegistration: zera o estado estático a cada Play, mesmo com
    // "Enter Play Mode Options" (sem domain reload) ligado no Editor.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Inicializar()
    {
        CaminhoArquivo = null;
        CenaAtual = "";
        CenaAnterior = "";
        cenaRegistrada = default;
        ErrosDePuzzle = 0;
        Falhas = 0;
        AcertosPre.Clear();
        AcertosPos.Clear();
        NotaPre = -1;
        NotaPos = -1;
        sessaoIniciada = false;
        avisouFalhaDeEscrita = false;
        pendentes.Clear();
        gravacaoTravada = false;
        sessaoEncerrada = false;
        jogoFechando = false;
        motivoSaida = null;
        emPausa = false;
        pausaAcumulada = 0f;
        pausaNoInicioCena = 0f;
        janelaEmFoco = true;
        relogio = null;

        SceneManager.sceneLoaded -= AoCarregarCena;
        SceneManager.sceneLoaded += AoCarregarCena;
    }

    // A primeira cena do Play nem sempre chega pelo sceneLoaded (depende de
    // quando a inscrição acontece) — garante que ela também seja conhecida.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegistrarCenaInicial()
    {
        AoCarregarCena(SceneManager.GetActiveScene(), LoadSceneMode.Single);

        if (relogio == null)
        {
            var go = new GameObject("RelogioPausa") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(go);
            relogio = go.AddComponent<RelogioPausa>();
            janelaEmFoco = Application.isFocused;
        }
    }

    /// <summary>Chamado pelo RelogioPausa quando a janela do jogo ganha ou perde o foco.</summary>
    public static void MudouFoco(bool emFoco)
    {
        janelaEmFoco = emFoco;
        AtualizarPausa();
    }

    /// <summary>Chamado a cada quadro pelo RelogioPausa: abre ou fecha a pausa da fase.</summary>
    public static void AtualizarPausa()
    {
        string motivo = MotivoDaPausa();
        if (motivo != null && !emPausa)
        {
            emPausa = true;
            inicioPausa = Time.realtimeSinceStartup;
            Registrar("pausa_inicio", motivo);
        }
        else if (motivo == null && emPausa)
        {
            FecharPausa();
        }
    }

    /// <summary>Encerra a pausa aberta (se houver) e grava quanto ela durou.</summary>
    public static void FecharPausa()
    {
        if (!emPausa)
        {
            return;
        }

        float duracao = Time.realtimeSinceStartup - inicioPausa;
        pausaAcumulada += duracao;
        emPausa = false;
        Registrar("pausa_fim", Segundos(duracao) + "s");
    }

    // Só nas fases: puzzles, diálogos e Game Over continuam contando (fazem parte da fase).
    private static string MotivoDaPausa()
    {
        if (!ProgressoJogo.EhFase(CenaAtual)) return null;
        if (!janelaEmFoco) return "janela_fora_de_foco";
        if (SettingsPanelUI.Instance != null && SettingsPanelUI.Instance.IsVisible) return "configuracoes";
        if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.PausaAberta) return "menu";
        return null;
    }

    /// <summary>Chamado a cada quadro pelo RelogioPausa: tenta de novo as linhas guardadas.</summary>
    public static void TentarGravarPendentes()
    {
        if (pendentes.Count > 0 && Time.realtimeSinceStartup >= proximaTentativa)
        {
            GravarPendentes();
        }
    }

    /// <summary>O botão Sair do Menu vai fechar o jogo (vira o detalhe do "sessao_fim").</summary>
    public static void MarcarSaidaPeloMenu()
    {
        motivoSaida = "botao_sair";
    }

    /// <summary>
    /// Chamado pelo RelogioPausa quando o jogo fecha: encerra a pausa, a cena
    /// e a sessão no arquivo e grava o que estava guardado.
    /// </summary>
    public static void AoFecharJogo()
    {
        if (jogoFechando)
        {
            return;
        }

        jogoFechando = true;

        // Sem sessão (só abriu o Menu e saiu) não cria arquivo, como antes.
        if (sessaoIniciada && !sessaoEncerrada)
        {
            FecharPausa();
            if (!string.IsNullOrEmpty(CenaAtual))
            {
                RegistrarSaidaDaCena();
            }
            Registrar("sessao_fim", motivoSaida ?? "jogo_fechado");
        }

        GravarPendentes();
        GravarContinuacao();
    }

    /// <summary>
    /// Outro jogador foi escolhido no Menu: o próximo evento abre um arquivo
    /// novo, com o nome dele. Os contadores do resumo também recomeçam.
    /// </summary>
    public static void TrocouJogador()
    {
        sessaoIniciada = false;
        sessaoEncerrada = false;
        CaminhoArquivo = null;
        ErrosDePuzzle = 0;
        Falhas = 0;
        AcertosPre.Clear();
        AcertosPos.Clear();
        NotaPre = -1;
        NotaPos = -1;
    }

    /// <summary>
    /// Chamado pelo pré-teste, que marca o início de uma partida. A sessão já
    /// aberta no Menu (ex: "fase_escolhida") continua: o pré-teste entra no
    /// mesmo arquivo. Só começa um arquivo novo se a anterior já teve
    /// "sessao_fim". Se ninguém chamar (ex: testando uma fase direto no
    /// Editor), a sessão começa sozinha no primeiro evento.
    /// </summary>
    public static void NovaSessao()
    {
        if (sessaoEncerrada)
        {
            sessaoIniciada = false;
            sessaoEncerrada = false;
        }
        ErrosDePuzzle = 0;
        Falhas = 0;
        AcertosPre.Clear();
        AcertosPos.Clear();
        NotaPre = -1;
        NotaPos = -1;
        GarantirSessao();
    }

    public static void Registrar(string evento, string detalhe = "")
    {
        GarantirSessao();
        Escrever(evento, detalhe);

        if (evento == "sessao_fim")
        {
            sessaoEncerrada = true;
        }

        // Salvamento do progresso (ver ProgressoJogo): conclusão de fase e
        // questionário pulado contam pra liberar as próximas fases.
        switch (evento)
        {
            case "fase_concluida": ProgressoJogo.RegistrarConclusao(CenaAtual); break;
            case "questionario_pre_pulado": ProgressoJogo.RegistrarQuestionarioPulado(false); break;
            case "questionario_pos_pulado": ProgressoJogo.RegistrarQuestionarioPulado(true); break;
        }

        OnEvento?.Invoke(evento, detalhe);
    }

    /// <summary>
    /// Disparado a cada evento registrado (evento, detalhe) — usado pelo
    /// AudioManager pra tocar os sons de erro, puzzle/fase concluída etc.
    /// </summary>
    public static event Action<string, string> OnEvento;

    /// <summary>Peça errada / sequência errada — conta no resumo como erro de puzzle.</summary>
    public static void RegistrarErro(string evento, string detalhe = "")
    {
        ErrosDePuzzle++;
        Registrar(evento, detalhe);
    }

    /// <summary>Queda, morte ou reinício por erro — conta no resumo como falha.</summary>
    public static void RegistrarFalha(string evento, string detalhe = "")
    {
        Falhas++;
        Registrar(evento, detalhe);
    }

    /// <summary>
    /// Devolve a nota do pré-teste salva em disco (ver ProgressoJogo) quando o
    /// jogo foi fechado entre o começo e o fim — sem isso a tela de resultado
    /// do pós-teste mostraria o "antes" como não respondido.
    /// </summary>
    public static void RestaurarPreTeste(int nota, Dictionary<string, int> acertosPorBloco)
    {
        if (NotaPre >= 0 || nota < 0)
        {
            return;
        }

        NotaPre = nota;
        AcertosPre.Clear();
        foreach (KeyValuePair<string, int> par in acertosPorBloco)
        {
            AcertosPre[par.Key] = par.Value;
        }
    }

    /// <summary>
    /// Guarda a nota de um questionário (pré ou pós) e os acertos por bloco.
    /// <paramref name="marca"/> vai junto no detalhe do CSV (ex.: pós-teste do modo professor).
    /// </summary>
    public static void RegistrarNota(bool posTeste, int acertos, int total, Dictionary<string, int> acertosPorBloco, string marca = null)
    {
        Dictionary<string, int> destino = posTeste ? AcertosPos : AcertosPre;
        destino.Clear();
        foreach (KeyValuePair<string, int> par in acertosPorBloco)
        {
            destino[par.Key] = par.Value;
        }

        if (posTeste) NotaPos = acertos;
        else NotaPre = acertos;

        ProgressoJogo.RegistrarQuestionario(posTeste, acertos, total, acertosPorBloco);

        string detalhe = string.IsNullOrEmpty(marca) ? $"{acertos}/{total}" : $"{acertos}/{total} · {marca}";
        Registrar(posTeste ? "questionario_pos_nota" : "questionario_pre_nota", detalhe);
    }

    private static void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        // Mesma cena avisada duas vezes (sceneLoaded + RegistrarCenaInicial).
        if (modo != LoadSceneMode.Single || cena == cenaRegistrada)
        {
            return;
        }

        cenaRegistrada = cena;

        // Menu principal e Reiniciar saem pela pausa: ela termina na troca de cena.
        FecharPausa();

        if (cena.name == CenaAtual)
        {
            // A própria fase recarregou (queda, morte, reinício) — quem causou
            // já registrou o motivo; aqui só marca que recomeçou.
            Registrar("cena_recarregada");
            return;
        }

        if (!string.IsNullOrEmpty(CenaAtual))
        {
            RegistrarSaidaDaCena();
        }

        CenaAnterior = CenaAtual;
        CenaAtual = cena.name;
        inicioCena = Time.realtimeSinceStartup;
        pausaNoInicioCena = pausaAcumulada;

        // Entrar numa cena não abre arquivo sozinho (o Menu, por exemplo, não
        // é uma partida) — a sessão começa no pré-teste ou no primeiro evento.
        if (sessaoIniciada)
        {
            Registrar("cena_entrada");
        }
    }

    // "cena_saida" (tempo total) e, numa fase, "cena_tempo_ativo" (sem as pausas).
    private static void RegistrarSaidaDaCena()
    {
        float total = Time.realtimeSinceStartup - inicioCena;
        Registrar("cena_saida", Segundos(total) + "s");
        if (ProgressoJogo.EhFase(CenaAtual))
        {
            Registrar("cena_tempo_ativo", Segundos(Mathf.Max(0f, total - (pausaAcumulada - pausaNoInicioCena))) + "s");
        }
    }

    private static void GarantirSessao()
    {
        if (sessaoIniciada)
        {
            return;
        }

        sessaoIniciada = true;
        sessaoEncerrada = false;
        inicioSessao = Time.realtimeSinceStartup;
        idSessao = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

        try
        {
            string pasta = Path.Combine(PastaDados.Raiz, Pasta);
            Directory.CreateDirectory(pasta);
            string jogador = NomeDeArquivo(Jogadores.IdPesquisa);
            string nome = string.IsNullOrEmpty(jogador) ? "sessao_" + idSessao : "sessao_" + jogador + "_" + idSessao;
            CaminhoArquivo = Path.GetFullPath(Path.Combine(pasta, nome + ".csv"));

            // Com BOM: sem ele, o Excel abre os acentos quebrados.
            File.WriteAllText(CaminhoArquivo, "sessao;data_hora;segundos;cena;evento;detalhe;jogador\n", new UTF8Encoding(true));
        }
        catch (Exception e)
        {
            AvisarFalha(e);
        }

        Escrever("sessao_inicio", Application.version);
    }

    private static void Escrever(string evento, string detalhe)
    {
        if (string.IsNullOrEmpty(CaminhoArquivo))
        {
            return;
        }

        string linha = string.Join(";",
            idSessao,
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            Segundos(SegundosDeJogo),
            Limpar(CenaAtual),
            Limpar(evento),
            Limpar(detalhe),
            Limpar(Jogadores.IdPesquisa)) + "\n";

        pendentes.Add(new KeyValuePair<string, string>(CaminhoArquivo, linha));
        GravarPendentes();
    }

    // Grava as linhas guardadas em ordem, juntando as do mesmo arquivo numa
    // escrita só. Se o arquivo estiver travado, para e tenta de novo depois.
    private static void GravarPendentes()
    {
        while (pendentes.Count > 0)
        {
            string caminho = pendentes[0].Key;
            int quantas = 0;
            var bloco = new StringBuilder();
            while (quantas < pendentes.Count && pendentes[quantas].Key == caminho)
            {
                bloco.Append(pendentes[quantas].Value);
                quantas++;
            }

            try
            {
                File.AppendAllText(caminho, bloco.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                proximaTentativa = Time.realtimeSinceStartup + IntervaloNovaTentativa;
                if (!gravacaoTravada)
                {
                    gravacaoTravada = true;
                    Debug.LogWarning("[RegistroAprendizagem] O registro está travado (o CSV pode estar aberto no Excel). As linhas ficam guardadas e entram quando der: " + e.Message);
                }
                return;
            }

            pendentes.RemoveRange(0, quantas);
        }

        if (gravacaoTravada)
        {
            gravacaoTravada = false;
            Debug.Log("[RegistroAprendizagem] Registro em dia: as linhas guardadas foram gravadas.");
        }
    }

    // Último recurso ao fechar o jogo com o arquivo ainda travado: o que faltou
    // vai para um arquivo ao lado, com o mesmo cabeçalho.
    private static void GravarContinuacao()
    {
        while (pendentes.Count > 0)
        {
            string caminho = pendentes[0].Key;
            string continuacao = Path.Combine(Path.GetDirectoryName(caminho), Path.GetFileNameWithoutExtension(caminho) + "_continuacao.csv");
            var bloco = new StringBuilder();
            int quantas = 0;
            while (quantas < pendentes.Count && pendentes[quantas].Key == caminho)
            {
                bloco.Append(pendentes[quantas].Value);
                quantas++;
            }

            try
            {
                if (!File.Exists(continuacao))
                {
                    File.WriteAllText(continuacao, "sessao;data_hora;segundos;cena;evento;detalhe;jogador\n", new UTF8Encoding(true));
                }
                File.AppendAllText(continuacao, bloco.ToString(), new UTF8Encoding(false));
                Debug.LogWarning($"[RegistroAprendizagem] {quantas} linhas gravadas em {continuacao} (o registro principal estava travado).");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RegistroAprendizagem] {quantas} linhas do registro não puderam ser gravadas: " + e.Message);
            }

            pendentes.RemoveRange(0, quantas);
        }
    }

    // Apelidos podem ter acento e "."; no nome do arquivo fica só o seguro.
    private static string NomeDeArquivo(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return "";
        var sb = new StringBuilder();
        foreach (char c in texto)
        {
            sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
        }
        return sb.ToString();
    }

    private static string Segundos(float valor)
    {
        return valor.ToString("0.0", CultureInfo.GetCultureInfo("pt-BR"));
    }

    // ";" e quebras de linha quebrariam as colunas do CSV.
    private static string Limpar(string texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return "";
        }

        return texto.Replace(";", ",").Replace("\r", " ").Replace("\n", " ");
    }

    private static void AvisarFalha(Exception e)
    {
        if (avisouFalhaDeEscrita)
        {
            return;
        }

        avisouFalhaDeEscrita = true;
        Debug.LogWarning("[RegistroAprendizagem] Não foi possível gravar o registro: " + e.Message);
    }
}
