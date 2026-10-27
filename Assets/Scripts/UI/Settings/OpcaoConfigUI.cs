using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Uma linha das Configurações ligada a uma opção do GameManager ou do
/// ConfiguracoesJogo. A linha é um Button (foco/realce do tema Laboratório):
///  - opção de lista (seletor): ◀ valor ▶ — setas do teclado ←→, clique nas
///    setas, ou Enter/clique na linha para avançar;
///  - opção liga/desliga (interruptor): Enter/clique alterna, ← desliga, → liga.
///
/// Montada no Editor por Tools > UI Laboratório > 5. Opções de qualidade de vida.
/// </summary>
[RequireComponent(typeof(Button))]
public class OpcaoConfigUI : MonoBehaviour, IMoveHandler
{
    public enum Opcao
    {
        Resolucao,
        ModoTela,
        TaxaQuadros,
        ManterProporcao,
        SilenciarSemFoco,
        VelocidadeTexto,
        DicasTecla,
        MostrarFPS,
        PrenderMouse,
    }

    [SerializeField] private Opcao opcao;

    [Header("Seletor")]
    [SerializeField] private TMP_Text valor;
    [SerializeField] private Button setaEsquerda;
    [SerializeField] private Button setaDireita;

    [Header("Interruptor")]
    [SerializeField] private Image interruptor;
    [SerializeField] private Sprite spriteLigado;
    [SerializeField] private Sprite spriteDesligado;

    private Button linha;

    public bool EhInterruptor =>
        opcao == Opcao.ManterProporcao || opcao == Opcao.SilenciarSemFoco ||
        opcao == Opcao.DicasTecla || opcao == Opcao.MostrarFPS || opcao == Opcao.PrenderMouse;

    private void Awake()
    {
        linha = GetComponent<Button>();
        linha.onClick.AddListener(AoClicarLinha);
        if (setaEsquerda != null) setaEsquerda.onClick.AddListener(() => AoClicarSeta(-1));
        if (setaDireita != null) setaDireita.onClick.AddListener(() => AoClicarSeta(+1));
    }

    private void OnEnable()
    {
        ConfiguracoesJogo.OnMudou += Atualizar;
        GameManager.OnTelaMudou += Atualizar;
        Atualizar();
    }

    private void OnDisable()
    {
        ConfiguracoesJogo.OnMudou -= Atualizar;
        GameManager.OnTelaMudou -= Atualizar;
    }

    private void AoClicarLinha()
    {
        if (EhInterruptor) DefinirLigado(!Ligado());
        else Passo(+1, circular: true);
    }

    private void AoClicarSeta(int direcao)
    {
        Passo(direcao, circular: false);
        // O foco volta pra linha, senão as setas do teclado ficariam presas no botão da seta.
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnMove(AxisEventData e)
    {
        if (e.moveDir != MoveDirection.Left && e.moveDir != MoveDirection.Right) return;
        int d = e.moveDir == MoveDirection.Left ? -1 : +1;

        if (EhInterruptor) DefinirLigado(d > 0);
        else Passo(d, circular: false);
    }

    // ------------------------------------------------------------------

    private void Passo(int direcao, bool circular)
    {
        int total = Quantidade();
        if (total <= 0) return;

        int i = Indice() + direcao;
        if (circular) i = (i % total + total) % total;
        else i = Mathf.Clamp(i, 0, total - 1);

        if (i != Indice()) DefinirIndice(i);
    }

    private int Quantidade()
    {
        switch (opcao)
        {
            case Opcao.Resolucao: return GameManager.Instance != null ? GameManager.Instance.Resolucoes.Count : 0;
            case Opcao.ModoTela: return GameManager.NomesModoTela.Length;
            case Opcao.TaxaQuadros: return ConfiguracoesJogo.NomesTaxaQuadros.Length;
            case Opcao.VelocidadeTexto: return ConfiguracoesJogo.NomesVelocidadeTexto.Length;
            default: return 0;
        }
    }

    private int Indice()
    {
        switch (opcao)
        {
            case Opcao.Resolucao: return GameManager.Instance != null ? GameManager.Instance.indiceResolucao : 0;
            case Opcao.ModoTela: return GameManager.Instance != null ? GameManager.Instance.indiceModoTela : 0;
            case Opcao.TaxaQuadros: return ConfiguracoesJogo.TaxaQuadros;
            case Opcao.VelocidadeTexto: return ConfiguracoesJogo.VelocidadeTexto;
            default: return 0;
        }
    }

    private void DefinirIndice(int i)
    {
        switch (opcao)
        {
            case Opcao.Resolucao: GameManager.Instance?.DefinirIndiceResolucao(i); break;
            case Opcao.ModoTela: GameManager.Instance?.DefinirModoTela(i); break;
            case Opcao.TaxaQuadros: ConfiguracoesJogo.TaxaQuadros = i; break;
            case Opcao.VelocidadeTexto: ConfiguracoesJogo.VelocidadeTexto = i; break;
        }
        Atualizar();
    }

    private string TextoDoValor()
    {
        switch (opcao)
        {
            case Opcao.Resolucao:
                if (GameManager.Instance == null) return "—";
                Vector2Int r = GameManager.Instance.ResolucaoAtual;
                return $"{r.x} × {r.y}";
            case Opcao.ModoTela:
                return GameManager.NomesModoTela[GameManager.Instance != null ? GameManager.Instance.indiceModoTela : 0];
            case Opcao.TaxaQuadros: return ConfiguracoesJogo.NomesTaxaQuadros[ConfiguracoesJogo.TaxaQuadros];
            case Opcao.VelocidadeTexto: return ConfiguracoesJogo.NomesVelocidadeTexto[ConfiguracoesJogo.VelocidadeTexto];
            default: return "";
        }
    }

    private bool Ligado()
    {
        switch (opcao)
        {
            case Opcao.ManterProporcao: return ConfiguracoesJogo.ManterProporcao;
            case Opcao.SilenciarSemFoco: return ConfiguracoesJogo.SilenciarSemFoco;
            case Opcao.DicasTecla: return ConfiguracoesJogo.DicasTecla;
            case Opcao.MostrarFPS: return ConfiguracoesJogo.MostrarFPS;
            case Opcao.PrenderMouse: return ConfiguracoesJogo.PrenderMouse;
            default: return false;
        }
    }

    private void DefinirLigado(bool v)
    {
        if (v == Ligado()) return;
        switch (opcao)
        {
            case Opcao.ManterProporcao: ConfiguracoesJogo.ManterProporcao = v; break;
            case Opcao.SilenciarSemFoco: ConfiguracoesJogo.SilenciarSemFoco = v; break;
            case Opcao.DicasTecla: ConfiguracoesJogo.DicasTecla = v; break;
            case Opcao.MostrarFPS: ConfiguracoesJogo.MostrarFPS = v; break;
            case Opcao.PrenderMouse: ConfiguracoesJogo.PrenderMouse = v; break;
        }
        Atualizar();
    }

    private void Atualizar()
    {
        if (EhInterruptor)
        {
            if (interruptor != null)
            {
                Sprite s = Ligado() ? spriteLigado : spriteDesligado;
                if (s != null) interruptor.sprite = s;
            }
            return;
        }

        if (valor != null) valor.text = TextoDoValor();

        // Setas apagadas nas pontas da lista.
        int total = Quantidade(), i = Indice();
        Apagar(setaEsquerda, i <= 0);
        Apagar(setaDireita, i >= total - 1);
    }

    private static void Apagar(Button seta, bool apagada)
    {
        if (seta == null || seta.targetGraphic == null) return;
        seta.targetGraphic.color = apagada ? new Color(1f, 1f, 1f, 0.3f) : Color.white;
    }
}
