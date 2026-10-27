using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Uma peça/condição que o jogador arrasta pro diagrama do cérebro na
/// IA_Aplicacao. Mostra os dois nomes: o do jogo (ex: "Distance(jogador) &gt; 2")
/// e, numa linha menor, o equivalente no Unity (ex: "Vector2.Distance").
/// </summary>
[Serializable]
public class IAOpcao
{
    public string id;
    public string rotulo;
    public string rotuloUnity;
    [TextArea(2, 4)] public string descricao;

    /// <summary>Ícone e cor da peça (ex: "vector2"), os mesmos das peças da Física e da IA. Vazio = cartão sem ícone.</summary>
    public string arte;

    public IAOpcao() { }

    public IAOpcao(string id, string rotulo, string rotuloUnity, string descricao)
    {
        this.id = id;
        this.rotulo = rotulo;
        this.rotuloUnity = rotuloUnity;
        this.descricao = descricao;
    }

    public IAOpcao ComArte(string arte)
    {
        this.arte = arte;
        return this;
    }
}

/// <summary>Caixa de estado no diagrama. Posição normalizada (0 a 1) dentro da área do diagrama, y pra cima.</summary>
public class IADiagramaEstado
{
    public string nome;
    public Vector2 posicao;

    /// <summary>Tamanho da caixa no Canvas; zero = tamanho padrão do painel.</summary>
    public Vector2 tamanho;

    public IADiagramaEstado(string nome, Vector2 posicao, Vector2 tamanho = default)
    {
        this.nome = nome;
        this.posicao = posicao;
        this.tamanho = tamanho;
    }
}

/// <summary>
/// Seta entre dois estados. <see cref="desvio"/> afasta a seta da linha
/// reta, pro lado esquerdo de quem anda nela: ida e volta com o MESMO sinal
/// ficam uma de cada lado (com sinais opostos caem na mesma linha).
/// <see cref="rotuloFixo"/> é uma condição já montada (não editável),
/// desenhada perto do meio da seta.
/// </summary>
public class IADiagramaSeta
{
    public int de;
    public int para;
    public float desvio;
    public string rotuloFixo;
    public Vector2 deslocamentoRotulo;

    public IADiagramaSeta(int de, int para, float desvio = 0f, string rotuloFixo = null, Vector2 deslocamentoRotulo = default)
    {
        this.de = de;
        this.para = para;
        this.desvio = desvio;
        this.rotuloFixo = rotuloFixo;
        this.deslocamentoRotulo = deslocamentoRotulo;
    }
}

/// <summary>Encaixe onde o jogador solta uma condição. Posição normalizada dentro do diagrama.</summary>
public class IADiagramaSlot
{
    public string legenda;
    public Vector2 posicao;

    /// <summary>Legenda embaixo do encaixe (pro encaixe da seta de baixo).</summary>
    public bool legendaEmbaixo;

    public IADiagramaSlot(string legenda, Vector2 posicao, bool legendaEmbaixo = false)
    {
        this.legenda = legenda;
        this.posicao = posicao;
        this.legendaEmbaixo = legendaEmbaixo;
    }
}

/// <summary>Tudo que o painel (IADiagramaUI) precisa pra desenhar o cérebro de um personagem.</summary>
public class IADiagrama
{
    public string titulo;
    public string subtitulo;
    public readonly List<IADiagramaEstado> estados = new List<IADiagramaEstado>();
    public readonly List<IADiagramaSeta> setas = new List<IADiagramaSeta>();
    public readonly List<IADiagramaSlot> slots = new List<IADiagramaSlot>();
    public readonly List<IAOpcao> inventario = new List<IAOpcao>();

    public IAOpcao BuscarOpcao(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        foreach (IAOpcao o in inventario)
        {
            if (o.id == id)
            {
                return o;
            }
        }

        return null;
    }
}

/// <summary>
/// Personagem cujo cérebro pode ser aberto e configurado pelo jogador na
/// IA_Aplicacao (Aliado e Guarda). O painel só conversa com esta interface,
/// então inimigo e aliado usam exatamente a mesma tela.
/// </summary>
public interface ICerebroConfiguravel
{
    /// <summary>Nome curto usado no registro (ex: "Aliado", "Guarda").</summary>
    string IdCerebro { get; }

    IADiagrama Diagrama { get; }

    /// <summary>Índice (em Diagrama.estados) do estado ativo agora — acende no painel. -1 = nenhum.</summary>
    int EstadoAtivoIndice { get; }

    int QuantidadeDeSlots { get; }

    /// <summary>Id da opção encaixada no slot (null/vazio = encaixe vazio).</summary>
    string GetOpcaoDoSlot(int slot);

    void SetOpcaoDoSlot(int slot, string opcaoId);
}
