using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Salvamento do progresso do jogador: quais fases foram concluídas (e com
/// que tempo, erros e quedas), se os questionários de antes e depois já foram
/// feitos, e o tempo total de jogo. É o que libera as fases no seletor do
/// Menu (uma fase só abre depois que a anterior foi concluída) e o que o botão
/// "Continuar" usa pra saber de onde seguir.
///
/// Nada precisa ser ligado na cena: a classe se inscreve sozinha na troca de
/// cenas e é avisada pelo RegistroAprendizagem quando uma fase termina
/// ("fase_concluida") ou quando um questionário é respondido/pulado.
///
/// Arquivo: PastaDados.Raiz/Save/Jogadores/<jogador>/progresso.json
/// (ou Save/progresso.json sem jogador escolhido, ver Jogadores), gravado de
/// forma segura (escreve num .tmp e só então troca; o anterior vira .bak e é
/// usado se o principal estiver corrompido).
/// </summary>
public static class ProgressoJogo
{
    /// <summary>Uma fase do caminho, na ordem em que precisa ser jogada.</summary>
    public class Fase
    {
        public readonly string cena;
        public readonly string titulo;
        public readonly string conceito;

        public Fase(string cena, string titulo, string conceito)
        {
            this.cena = cena;
            this.titulo = titulo;
            this.conceito = conceito;
        }
    }

    public const string CenaQuestionario = "Questionario";
    public const string CenaMenu = "Menu";

    /// <summary>O caminho das fases, na ordem. Mudou a ordem no Build Settings? Mude aqui também.</summary>
    public static readonly Fase[] Fases =
    {
        new Fase("Fisica", "Física", "Movimento, Rigidbody2D, Collider2D, pulo e câmera"),
        new Fase("Aplicacao_Fisica", "Aplicação da Física", "Usar os componentes no cenário"),
        new Fase("Interacao", "Interação", "Ação e consequência: triggers e colisões"),
        new Fase("Interacao_Vida", "Interação e Vida", "Dano, cura, HUD e Game Over"),
        new Fase("Tutorial_Anim", "Animação quadro a quadro", "Ordem dos quadros, FPS e loop"),
        new Fase("Animacao", "Animator", "Estados e transições da animação"),
        new Fase("IA", "IA: o cérebro do guarda", "Estados, transições e Raycast"),
        new Fase("IA_Aplicacao", "IA: guarda e aliado", "Configurar inimigo e aliado"),
    };

    [Serializable]
    public class FaseSalva
    {
        public string cena;
        public bool concluida;
        public int vezesConcluida;
        public int tentativas;          // quantas vezes o jogador entrou na fase
        public float ultimoTempo = -1f; // segundos jogando (sem as pausas, ver RegistroAprendizagem)
        public float melhorTempo = -1f;
        public int menosErros = -1;
        public int menosFalhas = -1;
        public int ultimosErros = -1;   // da última conclusão (os "menos" acima são o recorde)
        public int ultimasFalhas = -1;
        // Soma de todas as vezes que o jogador esteve na fase, concluindo ou
        // não (aula que acabou no meio, jogo fechado, fase jogada de novo).
        // Tempo sem as pausas. A tela de resultado do pós-teste soma esses.
        public float segundosTotal;
        public int errosTotal;
        public int falhasTotal;
        public string primeiraConclusao = "";
        public string ultimaConclusao = "";
    }

    [Serializable]
    public class BlocoNota
    {
        public string bloco;
        public int acertos;
    }

    [Serializable]
    public class QuestionarioSalvo
    {
        public bool feito;
        public bool pulado;
        public int nota = -1;
        public int total;
        public string data = "";
        public List<BlocoNota> blocos = new List<BlocoNota>();
    }

    [Serializable]
    public class Dados
    {
        public int versao = 1;
        public string criadoEm = "";
        public string atualizadoEm = "";
        public float segundosDeJogo;
        public string ultimaFaseJogada = "";
        public bool todasDesbloqueadas;   // modo professor (ver DesbloquearTodas)
        public QuestionarioSalvo preTeste = new QuestionarioSalvo();
        public QuestionarioSalvo posTeste = new QuestionarioSalvo();
        public List<FaseSalva> fases = new List<FaseSalva>();
    }

    /// <summary>Números da conclusão mais recente (o painel "Fase Concluída!" mostra). -1 = não medido.</summary>
    public class Resultado
    {
        public string cena;
        public float segundos = -1f;
        public int erros = -1;
        public int falhas = -1;
        public int tentativa = -1;
    }

    /// <summary>Última fase concluída nesta sessão de jogo (null antes da primeira).</summary>
    public static Resultado UltimaConclusao { get; private set; }

    private const string Arquivo = "progresso.json";

    private static Dados dados;

    // Fase em andamento: pra medir tempo, erros e quedas só dela (os
    // contadores do RegistroAprendizagem são da sessão inteira).
    private static string faseEmAndamento = "";
    private static float inicioDaFase;
    private static float pausaNoInicioDaFase;
    private static int errosNoInicio;
    private static int falhasNoInicio;
    private static float ultimoAcumulo;

    // Começo do trecho da fase em andamento que ainda não entrou nos totais
    // (ver FecharTrecho): avança a cada conclusão, saída da fase e fechamento.
    private static float inicioDoTrecho;
    private static float pausaNoInicioDoTrecho;
    private static int errosNoTrecho;
    private static int falhasNoTrecho;

    /// <summary>Disparado sempre que o progresso muda (o seletor de fases usa pra se atualizar).</summary>
    public static event Action OnMudou;

    // Cada jogador tem a sua pasta (ver Jogadores); sem jogador escolhido, a pasta antiga "Save".
    private static string PastaCompleta => Path.Combine(PastaDados.Raiz, Jogadores.PastaSave);
    public static string CaminhoArquivo => Path.Combine(PastaCompleta, Arquivo);
    private static string CaminhoBackup => CaminhoArquivo + ".bak";
    private static string CaminhoTemp => CaminhoArquivo + ".tmp";

    public static Dados Atual
    {
        get
        {
            if (dados == null)
            {
                dados = Carregar();
            }
            return dados;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        dados = null;
        faseEmAndamento = "";
        UltimaConclusao = null;
        OnMudou = null;
        SceneManager.sceneLoaded -= AoCarregarCena;
        SceneManager.sceneLoaded += AoCarregarCena;
        Application.quitting -= AoSair;
        Application.quitting += AoSair;
    }

    // A primeira cena do Play nem sempre chega pelo sceneLoaded (ex.: testando
    // uma fase direto no Editor): sem isso o tempo e os erros dela não seriam medidos.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegistrarCenaInicial()
    {
        Scene ativa = SceneManager.GetActiveScene();
        if (EhFase(ativa.name) && ativa.name != faseEmAndamento)
        {
            AoCarregarCena(ativa, LoadSceneMode.Single);
        }
    }

    // ------------------------------------------------------------------
    // Consultas
    // ------------------------------------------------------------------

    /// <summary>True se já existe algum progresso (questionário feito ou fase jogada).</summary>
    public static bool TemProgresso
    {
        get
        {
            Dados d = Atual;
            if (d.preTeste.feito)
            {
                return true;
            }

            foreach (FaseSalva f in d.fases)
            {
                if (f.concluida || f.tentativas > 0) return true;
            }
            return false;
        }
    }

    public static bool PreTesteFeito => Atual.preTeste.feito;

    /// <summary>
    /// True se o save antigo (Save/progresso.json, sem jogador) tem questionário
    /// feito ou fase jogada. Vazio, ele não aparece na lista de jogadores.
    /// </summary>
    public static bool SaveAntigoTemProgresso()
    {
        string caminho = Path.Combine(PastaDados.Raiz, "Save", Arquivo);
        Dados d = Ler(caminho) ?? Ler(caminho + ".bak");
        if (d == null) return false;
        if ((d.preTeste != null && d.preTeste.feito) || (d.posTeste != null && d.posTeste.feito)) return true;
        if (d.fases == null) return false;

        foreach (FaseSalva f in d.fases)
        {
            if (f.concluida || f.tentativas > 0) return true;
        }
        return false;
    }
    public static bool PosTesteFeito => Atual.posTeste.feito;
    public static bool TodasDesbloqueadas => Atual.todasDesbloqueadas;

    public static bool TodasConcluidas
    {
        get
        {
            foreach (Fase f in Fases)
            {
                if (!Concluida(f.cena)) return false;
            }
            return true;
        }
    }

    public static int QuantidadeConcluidas
    {
        get
        {
            int n = 0;
            foreach (Fase f in Fases)
            {
                if (Concluida(f.cena)) n++;
            }
            return n;
        }
    }

    public static int IndiceDaFase(string cena)
    {
        for (int i = 0; i < Fases.Length; i++)
        {
            if (Fases[i].cena == cena) return i;
        }
        return -1;
    }

    public static bool EhFase(string cena) => IndiceDaFase(cena) >= 0;

    public static bool Concluida(string cena)
    {
        FaseSalva f = Buscar(cena, false);
        return f != null && f.concluida;
    }

    /// <summary>
    /// Uma fase abre quando a anterior foi concluída. A primeira abre depois do
    /// questionário inicial (respondido ou pulado), pra ele sempre vir antes
    /// do jogo — é a linha de base da pesquisa.
    /// </summary>
    public static bool Desbloqueada(string cena)
    {
        if (Atual.todasDesbloqueadas)
        {
            return true;
        }

        int i = IndiceDaFase(cena);
        if (i < 0) return false;
        if (i == 0) return PreTesteFeito;
        return Concluida(Fases[i - 1].cena);
    }

    /// <summary>Questionário final: só depois de todas as fases.</summary>
    public static bool PosTesteDesbloqueado => TodasConcluidas || Atual.todasDesbloqueadas;

    /// <summary>
    /// Soma, nas 8 fases, do tempo (sem pausas), dos erros e das quedas de
    /// todas as vezes que o jogador esteve nelas. Saves de antes dos totais
    /// usam os números da última conclusão no lugar.
    /// </summary>
    public static void Totais(out float segundos, out int erros, out int falhas)
    {
        segundos = 0f;
        erros = 0;
        falhas = 0;
        foreach (FaseSalva f in Atual.fases)
        {
            if (!EhFase(f.cena)) continue;
            segundos += Mathf.Max(f.segundosTotal, f.ultimoTempo);
            erros += Mathf.Max(f.errosTotal, f.ultimosErros);
            falhas += Mathf.Max(f.falhasTotal, f.ultimasFalhas);
        }
    }

    /// <summary>Dados salvos de uma fase (null se ela nunca foi jogada).</summary>
    public static FaseSalva Info(string cena) => Buscar(cena, false);

    /// <summary>
    /// Pra onde o "Continuar" leva: questionário inicial se ainda não foi
    /// feito; senão a primeira fase não concluída; se todas foram concluídas e
    /// falta o questionário final, ele; e null quando está tudo feito.
    /// </summary>
    public static string ProximaCena()
    {
        if (!PreTesteFeito) return CenaQuestionario;

        foreach (Fase f in Fases)
        {
            if (!Concluida(f.cena)) return f.cena;
        }

        return PosTesteFeito ? null : CenaQuestionario;
    }

    public static string TituloDaCena(string cena)
    {
        if (cena == CenaQuestionario) return PreTesteFeito ? "Questionário final" : "Questionário inicial";
        int i = IndiceDaFase(cena);
        return i >= 0 ? Fases[i].titulo : cena;
    }

    /// <summary>Título do que vem depois de <paramref name="cena"/> no caminho (a última fase leva ao questionário final).</summary>
    public static string TituloDaProxima(string cena)
    {
        int i = IndiceDaFase(cena);
        if (i >= 0 && i + 1 < Fases.Length) return Fases[i + 1].titulo;
        return "Questionário final";
    }

    // ------------------------------------------------------------------
    // Alterações (chamadas pelo RegistroAprendizagem e pelo Menu)
    // ------------------------------------------------------------------

    /// <summary>Chamado quando uma fase registra "fase_concluida".</summary>
    public static void RegistrarConclusao(string cena)
    {
        if (!EhFase(cena))
        {
            return;
        }

        FaseSalva f = Buscar(cena, true);
        bool emAndamento = faseEmAndamento == cena;
        float pausas = RegistroAprendizagem.SegundosEmPausa - pausaNoInicioDaFase;
        float tempo = emAndamento ? Mathf.Max(0f, Time.realtimeSinceStartup - inicioDaFase - pausas) : -1f;
        int erros = emAndamento ? RegistroAprendizagem.ErrosDePuzzle - errosNoInicio : -1;
        int falhas = emAndamento ? RegistroAprendizagem.Falhas - falhasNoInicio : -1;

        string agora = Agora();
        if (!f.concluida)
        {
            f.primeiraConclusao = agora;
        }

        f.concluida = true;
        f.vezesConcluida++;
        f.ultimaConclusao = agora;

        if (tempo >= 0f)
        {
            f.ultimoTempo = tempo;
            if (f.melhorTempo < 0f || tempo < f.melhorTempo) f.melhorTempo = tempo;
        }
        if (erros >= 0 && (f.menosErros < 0 || erros < f.menosErros)) f.menosErros = erros;
        if (falhas >= 0 && (f.menosFalhas < 0 || falhas < f.menosFalhas)) f.menosFalhas = falhas;
        f.ultimosErros = erros;
        f.ultimasFalhas = falhas;
        if (emAndamento) FecharTrecho();

        UltimaConclusao = new Resultado
        {
            cena = cena,
            segundos = tempo,
            erros = erros,
            falhas = falhas,
            tentativa = f.tentativas,
        };

        // Se a mesma cena for jogada de novo sem sair dela, conta do zero.
        faseEmAndamento = "";
        Salvar();
    }

    /// <summary>Chamado quando um questionário termina (respondido).</summary>
    public static void RegistrarQuestionario(bool pos, int nota, int total, Dictionary<string, int> blocos)
    {
        QuestionarioSalvo q = pos ? Atual.posTeste : Atual.preTeste;
        q.feito = true;
        q.pulado = false;
        q.nota = nota;
        q.total = total;
        q.data = Agora();
        q.blocos.Clear();
        if (blocos != null)
        {
            foreach (KeyValuePair<string, int> par in blocos)
            {
                q.blocos.Add(new BlocoNota { bloco = par.Key, acertos = par.Value });
            }
        }
        Salvar();
    }

    /// <summary>Chamado quando um questionário é pulado — conta como feito (libera o caminho), mas sem nota.</summary>
    public static void RegistrarQuestionarioPulado(bool pos)
    {
        QuestionarioSalvo q = pos ? Atual.posTeste : Atual.preTeste;
        if (q.feito && !q.pulado)
        {
            return; // já tinha nota: pular de novo não apaga
        }

        q.feito = true;
        q.pulado = true;
        q.data = Agora();
        Salvar();
    }

    /// <summary>
    /// Nota do pré-teste salva, pra devolver ao RegistroAprendizagem quando o
    /// jogo foi fechado e reaberto entre o começo e o fim (a tela de resultado
    /// compara antes e depois).
    /// </summary>
    public static Dictionary<string, int> BlocosDoPreTeste()
    {
        var d = new Dictionary<string, int>();
        foreach (BlocoNota b in Atual.preTeste.blocos)
        {
            d[b.bloco] = b.acertos;
        }
        return d;
    }

    /// <summary>Troca de jogador, antes de mudar a pasta: guarda o tempo de jogo de quem sai.</summary>
    public static void SalvarAntesDeTrocar()
    {
        if (dados == null) return;
        AcumularTempo();
        Salvar();
    }

    /// <summary>Troca de jogador, depois de mudar a pasta: lê o progresso do novo jogador.</summary>
    public static void Recarregar()
    {
        dados = null;
        faseEmAndamento = "";
        OnMudou?.Invoke();
    }

    /// <summary>Apaga todo o progresso (Novo jogo). As configurações (volume, teclas, tela) não são afetadas.</summary>
    public static void ApagarTudo()
    {
        dados = NovosDados();
        faseEmAndamento = "";
        Salvar();
        Debug.Log("[ProgressoJogo] Progresso apagado.");
    }

    /// <summary>
    /// Libera todas as fases sem precisar jogar (pra apresentação ou pra o
    /// professor testar uma fase específica). Fica salvo até o professor
    /// desligar (Ctrl+Shift+U de novo) ou "Apagar progresso".
    /// </summary>
    public static void DesbloquearTodas()
    {
        Atual.todasDesbloqueadas = true;
        Salvar();
    }

    /// <summary>Desliga o modo professor: as fases voltam a seguir a ordem, sem apagar o progresso.</summary>
    public static void DesligarModoProfessor()
    {
        Atual.todasDesbloqueadas = false;
        Salvar();
    }

    // ------------------------------------------------------------------
    // Troca de cena: tentativas, fase em andamento, tempo total
    // ------------------------------------------------------------------

    private static void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        if (modo != LoadSceneMode.Single)
        {
            return;
        }

        AcumularTempo();

        // Saiu da fase sem concluir (ou foi pra outra): o que fez nela entra nos totais.
        if (!string.IsNullOrEmpty(faseEmAndamento) && cena.name != faseEmAndamento)
        {
            FecharTrecho();
        }

        // Recarregar a mesma fase (morte, queda, R) continua a mesma tentativa.
        if (EhFase(cena.name) && cena.name != faseEmAndamento)
        {
            faseEmAndamento = cena.name;
            inicioDaFase = Time.realtimeSinceStartup;
            pausaNoInicioDaFase = RegistroAprendizagem.SegundosEmPausa;
            errosNoInicio = RegistroAprendizagem.ErrosDePuzzle;
            falhasNoInicio = RegistroAprendizagem.Falhas;
            inicioDoTrecho = inicioDaFase;
            pausaNoInicioDoTrecho = pausaNoInicioDaFase;
            errosNoTrecho = errosNoInicio;
            falhasNoTrecho = falhasNoInicio;

            FaseSalva f = Buscar(cena.name, true);
            f.tentativas++;
            Atual.ultimaFaseJogada = cena.name;
            Salvar();
        }
        else if (!EhFase(cena.name))
        {
            faseEmAndamento = "";
            Salvar();
        }
    }

    private static void AoSair()
    {
        FecharTrecho();
        AcumularTempo();
        Salvar();
    }

    /// <summary>
    /// Soma nos totais da fase em andamento o tempo (sem pausas), os erros e
    /// as quedas desde o último trecho fechado. Não salva: quem chama salva.
    /// </summary>
    private static void FecharTrecho()
    {
        if (string.IsNullOrEmpty(faseEmAndamento))
        {
            return;
        }

        FaseSalva f = Buscar(faseEmAndamento, true);
        float agora = Time.realtimeSinceStartup;
        float pausaAgora = RegistroAprendizagem.SegundosEmPausa;
        f.segundosTotal += Mathf.Max(0f, agora - inicioDoTrecho - (pausaAgora - pausaNoInicioDoTrecho));
        // Os contadores do RegistroAprendizagem zeram na troca de jogador: nunca subtrai.
        f.errosTotal += Mathf.Max(0, RegistroAprendizagem.ErrosDePuzzle - errosNoTrecho);
        f.falhasTotal += Mathf.Max(0, RegistroAprendizagem.Falhas - falhasNoTrecho);

        inicioDoTrecho = agora;
        pausaNoInicioDoTrecho = pausaAgora;
        errosNoTrecho = RegistroAprendizagem.ErrosDePuzzle;
        falhasNoTrecho = RegistroAprendizagem.Falhas;
    }

    private static void AcumularTempo()
    {
        float agora = Time.realtimeSinceStartup;
        if (ultimoAcumulo > 0f && agora > ultimoAcumulo)
        {
            Atual.segundosDeJogo += agora - ultimoAcumulo;
        }
        ultimoAcumulo = agora;
    }

    // ------------------------------------------------------------------
    // Disco
    // ------------------------------------------------------------------

    private static FaseSalva Buscar(string cena, bool criar)
    {
        foreach (FaseSalva f in Atual.fases)
        {
            if (f.cena == cena) return f;
        }

        if (!criar) return null;

        var nova = new FaseSalva { cena = cena };
        Atual.fases.Add(nova);
        return nova;
    }

    private static Dados NovosDados()
    {
        string agora = Agora();
        return new Dados { criadoEm = agora, atualizadoEm = agora };
    }

    public static void Salvar()
    {
        // Sem jogador escolhido só grava numa fase aberta direto no Editor. Senão
        // abrir o Menu e fechar o jogo criariam um save vazio na pasta antiga,
        // que na abertura seguinte vira o "Sem nome (antigo)" (ver Jogadores).
        if (!Jogadores.Escolhido && !EhFase(SceneManager.GetActiveScene().name))
        {
            OnMudou?.Invoke();
            return;
        }

        Dados d = Atual;
        d.atualizadoEm = Agora();

        try
        {
            Directory.CreateDirectory(PastaCompleta);
            File.WriteAllText(CaminhoTemp, JsonUtility.ToJson(d, true));

            if (File.Exists(CaminhoArquivo))
            {
                File.Copy(CaminhoArquivo, CaminhoBackup, true);
                File.Delete(CaminhoArquivo);
            }
            File.Move(CaminhoTemp, CaminhoArquivo);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ProgressoJogo] Não consegui salvar o progresso: " + e.Message);
        }

        OnMudou?.Invoke();
    }

    private static Dados Carregar()
    {
        Dados d = Ler(CaminhoArquivo) ?? Ler(CaminhoBackup);
        if (d == null)
        {
            return NovosDados();
        }

        if (d.fases == null) d.fases = new List<FaseSalva>();
        if (d.preTeste == null) d.preTeste = new QuestionarioSalvo();
        if (d.posTeste == null) d.posTeste = new QuestionarioSalvo();
        if (d.preTeste.blocos == null) d.preTeste.blocos = new List<BlocoNota>();
        if (d.posTeste.blocos == null) d.posTeste.blocos = new List<BlocoNota>();
        return d;
    }

    private static Dados Ler(string caminho)
    {
        try
        {
            if (!File.Exists(caminho)) return null;
            string json = File.ReadAllText(caminho);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonUtility.FromJson<Dados>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ProgressoJogo] Arquivo de progresso ilegível ({caminho}): {e.Message}");
            return null;
        }
    }

    private static string Agora() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}
