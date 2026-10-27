using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuControl : MonoBehaviour
{
    // Nome da cena (configure exatamente como está no Build Settings). O
    // Questionario roda o pré-teste e depois carrega a Fisica.
    [SerializeField] private string nomeFase1 = "Questionario";

    [Tooltip("Texto do botão Jogar — vira \"Continuar\" quando já existe progresso salvo. Achado sozinho no botão 'Jogar' se ficar vazio.")]
    [SerializeField] private TMP_Text rotuloJogar;

    private void Start()
    {
        if (rotuloJogar == null)
        {
            GameObject botao = GameObject.Find("Jogar");
            if (botao != null)
            {
                rotuloJogar = botao.GetComponentInChildren<TMP_Text>(true);
            }
        }

        AtualizarRotuloJogar();
        ProgressoJogo.OnMudou += AtualizarRotuloJogar;
        Jogadores.OnMudou += AtualizarRotuloJogar;
    }

    private void OnDestroy()
    {
        ProgressoJogo.OnMudou -= AtualizarRotuloJogar;
        Jogadores.OnMudou -= AtualizarRotuloJogar;
    }

    private void AtualizarRotuloJogar()
    {
        if (rotuloJogar != null)
        {
            rotuloJogar.text = Jogadores.Escolhido && ProgressoJogo.TemProgresso && ProgressoJogo.ProximaCena() != null ? "Continuar" : "Jogar";
        }
    }

    // Botão Jogar / Continuar: sem progresso, começa pelo questionário
    // inicial; com progresso, segue da próxima fase não concluída; com tudo
    // concluído, abre a tela de fases pra rejogar.
    // Ninguém escolhido ainda (o jogo acabou de abrir): primeiro "Quem vai
    // jogar agora?" (ver Jogadores), depois segue daqui.
    public void Jogar()
    {
        if (!Jogadores.Escolhido)
        {
            NovoJogadorUI.Abrir(Jogar);
            return;
        }

        if (!ProgressoJogo.TemProgresso)
        {
            TrocaDeCena.Carregar(nomeFase1);
            return;
        }

        string proxima = ProgressoJogo.ProximaCena();
        if (string.IsNullOrEmpty(proxima))
        {
            SelecionarFases();
            return;
        }

        RegistroAprendizagem.Registrar("continuar", proxima);
        TrocaDeCena.Carregar(proxima);
    }

    // Botão Fases: lista as fases, com as bloqueadas travadas (ver SeletorFasesUI).
    public void SelecionarFases()
    {
        if (!Jogadores.Escolhido)
        {
            NovoJogadorUI.Abrir(SeletorFasesUI.Abrir);
            return;
        }

        SeletorFasesUI.Abrir();
    }

    // Botão Configurações: abre a tela de Configurações (com abas), sem depender de uma cena dedicada
    public void Configuracoes()
    {
        SettingsPanelUI.Instance?.Show(cameFromPauseMenu: false);
    }

    // Botão Sair
    public void Sair()
    {
        RegistroAprendizagem.MarcarSaidaPeloMenu();
        Application.Quit();
    }
}
