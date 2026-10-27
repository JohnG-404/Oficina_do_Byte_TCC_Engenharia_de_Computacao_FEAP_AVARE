using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Vários alunos no mesmo PC. Cada jogador tem o próprio progresso
/// (Save/Jogadores/&lt;id&gt;/progresso.json) e aparece nos registros CSV da
/// pesquisa (coluna "jogador" e nome do arquivo).
///
/// Dois jeitos de entrar (tela Novo jogador, ver NovoJogadorUI):
/// - Com apelido: até 12 letras, números, "_", "." ou "-". Não pode repetir
///   um apelido já salvo neste PC.
/// - Anônimo: o jogo dá o próximo número livre ("Jogador 007"). No CSV vira
///   "anonimo-007-K3", onde K3 é um código sorteado uma vez por PC, pra o
///   mesmo número em PCs diferentes não se misturar quando os dados forem
///   juntados.
///
/// Ninguém fica escolhido ao abrir o jogo: o Menu pede "Quem vai jogar
/// agora?" antes de Jogar/Continuar, pra um aluno não seguir o progresso do
/// outro sem querer. Sem jogador escolhido (ex.: fase aberta direto no
/// Editor), o progresso usa o arquivo antigo Save/progresso.json, que também
/// aparece na lista como "Sem nome (antigo)" quando tem progresso de verdade.
/// Fora de uma fase, nada é salvo sem jogador (ver ProgressoJogo.Salvar).
/// Nada é apagado nem movido.
///
/// Arquivo da lista: PastaDados.Raiz/Jogadores/jogadores.json.
/// </summary>
public static class Jogadores
{
    public const int TamanhoMaximoApelido = 12;
    public const int TamanhoMinimoApelido = 2;
    public const string IdAntigo = "_antigo";

    [Serializable]
    public class Jogador
    {
        public string id = "";
        public string apelido = "";
        public bool anonimo;
        public int numero;
        public string criadoEm = "";
        public string ultimoUso = "";

        /// <summary>Como aparece na tela: o apelido ou "Jogador 007".</summary>
        public string Nome
        {
            get
            {
                if (id == IdAntigo) return "Sem nome (antigo)";
                return anonimo ? $"Jogador {numero:000}" : apelido;
            }
        }
    }

    [Serializable]
    private class Lista
    {
        public int versao = 1;
        public string codigoPc = "";
        public int ultimoNumeroAnonimo;
        public List<Jogador> jogadores = new List<Jogador>();
    }

    private const string Pasta = "Jogadores";
    private const string Arquivo = "jogadores.json";
    private const string LetrasCodigoPc = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"; // sem 0/O, 1/I/L

    private static Lista lista;
    private static Jogador atual;

    /// <summary>Disparado quando o jogador escolhido muda (Menu, crachá, CSV).</summary>
    public static event Action OnMudou;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        lista = null;
        atual = null;
        OnMudou = null;
    }

    private static string CaminhoLista => Path.Combine(PastaDados.Raiz, Pasta, Arquivo);

    // ------------------------------------------------------------------
    // Consultas
    // ------------------------------------------------------------------

    /// <summary>Jogador escolhido nesta abertura do jogo (null = ninguém ainda).</summary>
    public static Jogador Atual => atual;

    public static bool Escolhido => atual != null;

    /// <summary>Nome na tela do jogador escolhido ("" se ninguém).</summary>
    public static string NomeAtual => atual != null ? atual.Nome : "";

    /// <summary>
    /// Identificador que vai no CSV: o apelido, "anonimo-007-K3" ou "antigo".
    /// Vazio quando ninguém foi escolhido.
    /// </summary>
    public static string IdPesquisa
    {
        get
        {
            if (atual == null) return "";
            if (atual.id == IdAntigo) return "antigo";
            return atual.anonimo ? $"anonimo-{atual.numero:000}-{Carregada.codigoPc}" : atual.apelido;
        }
    }

    /// <summary>Pasta do progresso (relativa à PastaDados.Raiz) do jogador escolhido.</summary>
    public static string PastaSave
    {
        get
        {
            if (atual == null || atual.id == IdAntigo) return "Save";
            return Path.Combine("Save", Pasta, atual.id);
        }
    }

    /// <summary>Jogadores salvos neste PC, do usado mais recente pro mais antigo.</summary>
    public static List<Jogador> Todos()
    {
        GarantirAntigo();
        var copia = new List<Jogador>();
        foreach (Jogador j in Carregada.jogadores)
        {
            // Save antigo vazio (o jogo abriu, mas ninguém jogou nele) não vira chip.
            if (j.id == IdAntigo && !ProgressoJogo.SaveAntigoTemProgresso()) continue;
            copia.Add(j);
        }
        copia.Sort((a, b) => string.CompareOrdinal(b.ultimoUso, a.ultimoUso));
        return copia;
    }

    /// <summary>Número que o próximo anônimo vai receber.</summary>
    public static int ProximoNumeroAnonimo => Carregada.ultimoNumeroAnonimo + 1;

    public static string NomeDoProximoAnonimo => $"Jogador {ProximoNumeroAnonimo:000}";

    /// <summary>
    /// Confere o apelido. Devolve null se estiver ok, ou a mensagem pra mostrar.
    /// </summary>
    public static string ValidarApelido(string texto)
    {
        string apelido = (texto ?? "").Trim();
        if (apelido.Length < TamanhoMinimoApelido) return $"O apelido precisa de pelo menos {TamanhoMinimoApelido} letras.";
        if (apelido.Length > TamanhoMaximoApelido) return $"O apelido pode ter até {TamanhoMaximoApelido} letras.";
        foreach (char c in apelido)
        {
            if (!CaractereValido(c)) return "Use só letras, números, _ . ou -";
        }
        if (Buscar(IdDoApelido(apelido)) != null) return "Esse apelido já existe neste PC. Escolha ele na lista ou use outro.";
        if (apelido.StartsWith("jogador", StringComparison.OrdinalIgnoreCase) || apelido.Equals("anonimo", StringComparison.OrdinalIgnoreCase))
            return "Esse nome é reservado pros anônimos. Use outro apelido.";
        return null;
    }

    /// <summary>Letras (com acento), números, "_", "." e "-". Usado também pra filtrar a digitação.</summary>
    public static bool CaractereValido(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '-';

    // ------------------------------------------------------------------
    // Alterações
    // ------------------------------------------------------------------

    public static Jogador CriarComApelido(string texto)
    {
        string apelido = (texto ?? "").Trim();
        if (ValidarApelido(apelido) != null) return null;

        var j = new Jogador { id = IdDoApelido(apelido), apelido = apelido, criadoEm = Agora() };
        Carregada.jogadores.Add(j);
        Escolher(j, "novo");
        return j;
    }

    public static Jogador CriarAnonimo()
    {
        Lista l = Carregada;
        l.ultimoNumeroAnonimo++;
        var j = new Jogador
        {
            id = $"jogador_{l.ultimoNumeroAnonimo:000}",
            anonimo = true,
            numero = l.ultimoNumeroAnonimo,
            criadoEm = Agora(),
        };
        l.jogadores.Add(j);
        Escolher(j, "novo");
        return j;
    }

    /// <summary>Volta a jogar com alguém da lista.</summary>
    public static void Continuar(Jogador j)
    {
        if (j == null) return;
        Jogador salvo = Buscar(j.id);
        if (salvo != null) Escolher(salvo, "continuar");
    }

    private static void Escolher(Jogador j, string como)
    {
        ProgressoJogo.SalvarAntesDeTrocar();
        atual = j;
        j.ultimoUso = Agora();
        SalvarLista();

        // O progresso e o CSV passam a ser deste jogador.
        ProgressoJogo.Recarregar();
        RegistroAprendizagem.TrocouJogador();
        if (como == "continuar")
        {
            // Jogador novo não registra aqui: o arquivo dele começa no pré-teste.
            RegistroAprendizagem.Registrar("jogador_continuou");
        }

        OnMudou?.Invoke();
    }

    // ------------------------------------------------------------------
    // Disco
    // ------------------------------------------------------------------

    private static Lista Carregada
    {
        get
        {
            if (lista == null)
            {
                lista = Ler() ?? new Lista();
                if (lista.jogadores == null) lista.jogadores = new List<Jogador>();
                if (string.IsNullOrEmpty(lista.codigoPc))
                {
                    lista.codigoPc = SortearCodigoPc();
                    SalvarLista();
                }
            }
            return lista;
        }
    }

    // O save de antes desta tela (Save/progresso.json) vira um jogador da
    // lista, pra poder continuar dele. O arquivo fica onde está.
    private static void GarantirAntigo()
    {
        if (Buscar(IdAntigo) != null) return;

        string antigo = Path.Combine(PastaDados.Raiz, "Save", "progresso.json");
        if (!File.Exists(antigo)) return;

        string data = File.GetLastWriteTime(antigo).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        Carregada.jogadores.Add(new Jogador { id = IdAntigo, criadoEm = data, ultimoUso = data });
        SalvarLista();
    }

    private static Jogador Buscar(string id)
    {
        foreach (Jogador j in Carregada.jogadores)
        {
            if (string.Equals(j.id, id, StringComparison.OrdinalIgnoreCase)) return j;
        }
        return null;
    }

    // Nome de pasta seguro e sem diferença de maiúsculas/acentos: "Lía_Gamer" -> "lia_gamer".
    private static string IdDoApelido(string apelido)
    {
        string semAcento = apelido.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (char c in semAcento)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            char m = char.ToLowerInvariant(c);
            sb.Append((m >= 'a' && m <= 'z') || (m >= '0' && m <= '9') || m == '_' || m == '-' ? m : '_');
        }
        return "a_" + sb;
    }

    private static string SortearCodigoPc()
    {
        var r = new System.Random();
        return new string(new[] { LetrasCodigoPc[r.Next(LetrasCodigoPc.Length)], LetrasCodigoPc[r.Next(LetrasCodigoPc.Length)] });
    }

    private static Lista Ler()
    {
        try
        {
            if (!File.Exists(CaminhoLista)) return null;
            string json = File.ReadAllText(CaminhoLista);
            return string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<Lista>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Jogadores] Lista de jogadores ilegível: " + e.Message);
            return null;
        }
    }

    private static void SalvarLista()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CaminhoLista));
            string tmp = CaminhoLista + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(lista, true));
            if (File.Exists(CaminhoLista))
            {
                File.Copy(CaminhoLista, CaminhoLista + ".bak", true);
                File.Delete(CaminhoLista);
            }
            File.Move(tmp, CaminhoLista);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Jogadores] Não consegui salvar a lista de jogadores: " + e.Message);
        }
    }

    private static string Agora() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}
