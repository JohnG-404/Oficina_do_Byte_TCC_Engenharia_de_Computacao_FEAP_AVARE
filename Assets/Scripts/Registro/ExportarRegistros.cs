using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Exporta os registros deste PC (Ctrl+Shift+E na Seleção de fases, com o
/// modo professor ligado) para uma pasta nova na Área de Trabalho:
///
/// - os CSVs de sessão (cópia, os originais continuam no AppData);
/// - "resumo_jogadores.csv", uma linha por jogador com pré e pós-teste e,
///   por fase, se concluiu, o tempo (sem pausas), erros, quedas e entradas.
///
/// Serve pra juntar os dados da turma sem abrir a pasta escondida do
/// AppData em cada PC, e pra não perder nada em PC de laboratório que apaga
/// os arquivos ao reiniciar.
/// </summary>
public static class ExportarRegistros
{
    private const string PastaRegistros = "Registros";
    private static readonly CultureInfo ptBR = CultureInfo.GetCultureInfo("pt-BR");

    // Mesmo formato do jogadores.json (ver Jogadores.Lista, que é privada).
    [Serializable]
    private class ListaJogadores
    {
        public string codigoPc = "";
        public List<Jogadores.Jogador> jogadores = new List<Jogadores.Jogador>();
    }

    /// <summary>
    /// Copia tudo pra uma pasta nova. Devolve false (com o motivo em
    /// <paramref name="mensagem"/>) se não deu pra criar a pasta.
    /// </summary>
    public static bool Exportar(out string pastaDestino, out int arquivosCsv, out int jogadores, out string mensagem)
    {
        pastaDestino = null;
        arquivosCsv = 0;
        jogadores = 0;
        mensagem = "";

        // Grava o que está em memória antes de copiar.
        ProgressoJogo.Salvar();

        string raiz = PastaDados.Raiz;
        ListaJogadores lista = LerJogadores(raiz);
        string codigoPc = lista != null ? lista.codigoPc : "";

        try
        {
            string nome = "Oficina do Byte - registros" +
                (string.IsNullOrEmpty(codigoPc) ? "" : " PC " + codigoPc) +
                " " + DateTime.Now.ToString("yyyy-MM-dd HH'h'mm", CultureInfo.InvariantCulture);
            pastaDestino = Path.Combine(PastaBase(), nome);
            Directory.CreateDirectory(pastaDestino);

            string registros = Path.Combine(raiz, PastaRegistros);
            if (Directory.Exists(registros))
            {
                foreach (string csv in Directory.GetFiles(registros, "*.csv"))
                {
                    File.Copy(csv, Path.Combine(pastaDestino, Path.GetFileName(csv)), true);
                    arquivosCsv++;
                }
            }

            File.WriteAllText(Path.Combine(pastaDestino, "resumo_jogadores.csv"),
                MontarResumo(raiz, lista, codigoPc, out jogadores), new UTF8Encoding(true));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ExportarRegistros] " + e.Message);
            mensagem = e.Message;
            return false;
        }
    }

    // Área de Trabalho; se não existir (conta sem Desktop), Documentos.
    private static string PastaBase()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (!string.IsNullOrEmpty(desktop) && Directory.Exists(desktop)) return desktop;
        string documentos = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrEmpty(documentos) && Directory.Exists(documentos)) return documentos;
        return PastaDados.Raiz;
    }

    private static ListaJogadores LerJogadores(string raiz)
    {
        try
        {
            string caminho = Path.Combine(raiz, "Jogadores", "jogadores.json");
            if (!File.Exists(caminho)) return null;
            return JsonUtility.FromJson<ListaJogadores>(File.ReadAllText(caminho));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ExportarRegistros] jogadores.json: " + e.Message);
            return null;
        }
    }

    private static ProgressoJogo.Dados LerProgresso(string raiz, Jogadores.Jogador j)
    {
        string pasta = j.id == Jogadores.IdAntigo ? "Save" : Path.Combine("Save", "Jogadores", j.id);
        string caminho = Path.Combine(raiz, pasta, "progresso.json");
        try
        {
            if (!File.Exists(caminho)) return null;
            return JsonUtility.FromJson<ProgressoJogo.Dados>(File.ReadAllText(caminho));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ExportarRegistros] " + caminho + ": " + e.Message);
            return null;
        }
    }

    private static string MontarResumo(string raiz, ListaJogadores lista, string codigoPc, out int quantos)
    {
        quantos = 0;
        var sb = new StringBuilder();
        var cab = new List<string>
        {
            "jogador", "pc", "criado_em", "ultimo_uso", "tempo_de_jogo_min",
            "pre_teste", "pre_acertos", "pre_total", "pos_teste", "pos_acertos", "pos_total",
            "fases_concluidas",
        };
        foreach (ProgressoJogo.Fase f in ProgressoJogo.Fases)
        {
            cab.Add(f.cena + "_concluida");
            cab.Add(f.cena + "_minutos");
            cab.Add(f.cena + "_erros");
            cab.Add(f.cena + "_quedas");
            cab.Add(f.cena + "_entradas");
        }
        sb.Append(string.Join(";", cab)).Append('\n');

        if (lista == null || lista.jogadores == null) return sb.ToString();

        foreach (Jogadores.Jogador j in lista.jogadores)
        {
            ProgressoJogo.Dados d = LerProgresso(raiz, j);
            if (d == null) continue;   // jogador criado que nunca jogou

            var linha = new List<string>
            {
                IdDoJogador(j, codigoPc), codigoPc, j.criadoEm, j.ultimoUso, Minutos(d.segundosDeJogo),
                Situacao(d.preTeste), Nota(d.preTeste), Total(d.preTeste),
                Situacao(d.posTeste), Nota(d.posTeste), Total(d.posTeste),
            };

            int concluidas = 0;
            var colunasFases = new List<string>();
            foreach (ProgressoJogo.Fase f in ProgressoJogo.Fases)
            {
                ProgressoJogo.FaseSalva s = d.fases != null ? d.fases.Find(x => x.cena == f.cena) : null;
                if (s == null)
                {
                    colunasFases.AddRange(new[] { "nao", "", "", "", "0" });
                    continue;
                }
                if (s.concluida) concluidas++;
                colunasFases.Add(s.concluida ? "sim" : "nao");
                colunasFases.Add(Minutos(s.segundosTotal));
                colunasFases.Add(s.errosTotal.ToString(CultureInfo.InvariantCulture));
                colunasFases.Add(s.falhasTotal.ToString(CultureInfo.InvariantCulture));
                colunasFases.Add(s.tentativas.ToString(CultureInfo.InvariantCulture));
            }
            linha.Add(concluidas.ToString(CultureInfo.InvariantCulture));
            linha.AddRange(colunasFases);

            sb.Append(string.Join(";", linha.ConvertAll(Limpar))).Append('\n');
            quantos++;
        }
        return sb.ToString();
    }

    // O mesmo identificador da coluna "jogador" dos CSVs de sessão (Jogadores.IdPesquisa).
    private static string IdDoJogador(Jogadores.Jogador j, string codigoPc)
    {
        if (j.id == Jogadores.IdAntigo) return "antigo";
        return j.anonimo ? $"anonimo-{j.numero:000}-{codigoPc}" : j.apelido;
    }

    private static string Situacao(ProgressoJogo.QuestionarioSalvo q)
    {
        if (q == null || !q.feito) return "nao_feito";
        return q.pulado ? "pulado" : "respondido";
    }

    private static string Nota(ProgressoJogo.QuestionarioSalvo q) =>
        q != null && q.feito && !q.pulado && q.nota >= 0 ? q.nota.ToString(CultureInfo.InvariantCulture) : "";

    private static string Total(ProgressoJogo.QuestionarioSalvo q) =>
        q != null && q.feito && !q.pulado && q.total > 0 ? q.total.ToString(CultureInfo.InvariantCulture) : "";

    private static string Minutos(float segundos) =>
        segundos > 0f ? (segundos / 60f).ToString("0.0", ptBR) : "0";

    // ";" e quebras de linha quebrariam as colunas.
    private static string Limpar(string texto) =>
        (texto ?? "").Replace(";", ",").Replace("\r", " ").Replace("\n", " ");
}
