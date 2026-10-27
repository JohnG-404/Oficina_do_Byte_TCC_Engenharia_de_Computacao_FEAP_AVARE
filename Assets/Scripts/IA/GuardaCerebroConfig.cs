using UnityEngine;

/// <summary>
/// Deixa o cérebro do Guarda ser aberto no painel da IA_Aplicacao. As
/// transições já vêm montadas (é o guarda da fase IA, com Raycast); o único
/// encaixe editável é o ALVO: quem ele persegue.
///
/// "tag Player" = só o jogador. "tag Player ou Aliado" = qualquer um dos
/// dois que ele vir primeiro. É a segunda configuração que faz o aliado
/// conseguir distrair o guarda: o alvo também é uma regra.
/// </summary>
[RequireComponent(typeof(GuardaIA))]
public class GuardaCerebroConfig : MonoBehaviour, ICerebroConfiguravel
{
    public const string AlvoSoPlayer = "tag_player";
    public const string AlvoPlayerOuAliado = "tag_player_ou_aliado";

    public const string TagPlayer = "Player";
    public const string TagAliado = "Aliado";

    [Tooltip("Configuração inicial do alvo (o jogador precisa trocar pra 'tag Player ou Aliado').")]
    [SerializeField] private string opcaoAlvo = AlvoSoPlayer;

    private GuardaIA guarda;
    private IADiagrama diagrama;

    public bool IncluiAliado => opcaoAlvo == AlvoPlayerOuAliado;

    private void Awake()
    {
        guarda = GetComponent<GuardaIA>();
        Aplicar();
    }

    private void Aplicar()
    {
        if (guarda == null)
        {
            guarda = GetComponent<GuardaIA>();
        }

        if (opcaoAlvo == AlvoPlayerOuAliado)
        {
            guarda.SetTagsAlvo(TagPlayer, TagAliado);
        }
        else
        {
            opcaoAlvo = AlvoSoPlayer;
            guarda.SetTagsAlvo(TagPlayer);
        }
    }

    // ------------------------------------------------------------------
    // ICerebroConfiguravel
    // ------------------------------------------------------------------

    public string IdCerebro => "Guarda";

    public IADiagrama Diagrama => diagrama ?? (diagrama = CriarDiagrama());

    public int EstadoAtivoIndice
    {
        get
        {
            if (guarda == null) return -1;
            switch (guarda.EstadoAtual)
            {
                case GuardaIA.Estado.Patrulhar: return 0;
                case GuardaIA.Estado.Perseguir: return 1;
                case GuardaIA.Estado.Atacar: return 2;
                default: return -1;
            }
        }
    }

    public int QuantidadeDeSlots => 1;

    public string GetOpcaoDoSlot(int slot)
    {
        return slot == 0 ? opcaoAlvo : null;
    }

    public void SetOpcaoDoSlot(int slot, string opcaoId)
    {
        if (slot != 0 || string.IsNullOrEmpty(opcaoId))
        {
            return;
        }

        opcaoAlvo = opcaoId;
        Aplicar();
    }

    private static IADiagrama CriarDiagrama()
    {
        var d = new IADiagrama
        {
            titulo = "Cérebro do Guarda",
            subtitulo = "As setas já vêm montadas (é o guarda da fase anterior). Confira quem ele persegue."
        };

        d.estados.Add(new IADiagramaEstado("Patrulhar", new Vector2(0.13f, 0.62f)));
        d.estados.Add(new IADiagramaEstado("Perseguir", new Vector2(0.5f, 0.62f)));
        d.estados.Add(new IADiagramaEstado("Atacar", new Vector2(0.87f, 0.62f)));

        d.setas.Add(new IADiagramaSeta(0, 1, 0.045f, "Raycast vê o alvo", new Vector2(0f, 0.07f)));
        d.setas.Add(new IADiagramaSeta(1, 0, 0.045f, "Distance > raioPerder\nou parede no meio", new Vector2(0f, -0.1f)));
        d.setas.Add(new IADiagramaSeta(1, 2, 0.045f, "Distance < alcance", new Vector2(0f, 0.07f)));
        d.setas.Add(new IADiagramaSeta(2, 1, 0.045f, "Distance > alcance", new Vector2(0f, -0.07f)));

        d.slots.Add(new IADiagramaSlot("Alvo: persegue quem tem", new Vector2(0.5f, 0.14f)));

        d.inventario.Add(new IAOpcao(AlvoSoPlayer, "tag Player", "CompareTag",
            "Persegue só quem tem a tag Player.").ComArte("tag"));
        d.inventario.Add(new IAOpcao(AlvoPlayerOuAliado, "tag Player ou Aliado", "CompareTag",
            "Quem tiver a tag Player ou a tag Aliado. Ele vai atrás do primeiro que vir.").ComArte("tag"));

        return d;
    }
}
