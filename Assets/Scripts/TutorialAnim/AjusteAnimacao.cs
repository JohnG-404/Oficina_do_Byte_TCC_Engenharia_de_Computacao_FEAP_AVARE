using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dados de um passo "Ajuste a animação" da cena Tutorial_Anim: depois que o
/// jogador monta a ordem certa de uma rodada, ele escolhe a velocidade
/// (Sample Rate, em quadros por segundo) e o Loop Time do clipe, que são as
/// duas coisas que se ajustam de verdade num Animation Clip do Unity.
///
/// Fica numa classe separada (e não solta no AnimationSequencePuzzle) pra
/// que todos os textos do passo sejam editáveis no Inspector, e pra que dê
/// pra ter mais de um ajuste no futuro (ex: a Alavanca com loop desligado).
/// A avaliação da resposta também mora aqui (ver Avaliar), assim a UI
/// (AjusteAnimacaoUI) só cuida de mostrar e o puzzle só cuida do fluxo.
/// </summary>
[Serializable]
public class AjusteAnimacao
{
    public enum Resultado
    {
        Certo,
        CertoComObservacao,
        Errado
    }

    [Tooltip("Nome da rodada (RoundData.roundName) depois da qual este ajuste aparece. Precisa ser igual ao nome da rodada.")]
    public string roundName = "Corrida";

    [Header("Abertura (Prof Byte)")]
    [Tooltip("Falado quando o painel de ajuste aparece. Os botões ficam travados até o jogador terminar de ler.")]
    [TextArea(2, 4)]
    public string[] introDialogue =
    {
        "Boa, a corrida está na ordem certa! Agora falta um detalhe.",
        "No Unity, cada Animation Clip tem uma velocidade (Sample Rate, em quadros por segundo) e a opção Loop Time. Ajuste os dois.",
        "Teste os botões, repare na prévia e clique em Confirmar quando achar que ficou certo."
    };

    [Header("Painel")]
    public string titulo = "Ajuste a animação";

    [Tooltip("O caso pedido, mostrado no topo do painel. A resposta certa (fpsCerto/loopCerto) precisa combinar com ele.")]
    [TextArea(2, 3)]
    public string pedido = "Essa é a corrida: ela precisa repetir enquanto o personagem corre e ficar fluida.";

    public string rotuloFps = "Sample Rate (quadros por segundo)";
    public string rotuloLoop = "Loop Time";
    public string textoBotaoLoopLigado = "Loop ligado";
    public string textoBotaoLoopDesligado = "Loop desligado";
    public string textoConfirmar = "Confirmar";

    [Tooltip("Velocidades oferecidas como botões, na ordem em que aparecem.")]
    public int[] fpsOpcoes = { 6, 12, 24 };

    [Tooltip("Explicação curta de cada velocidade, na mesma ordem de fpsOpcoes. Aceita rich text (<b>...</b>).")]
    [TextArea(2, 3)]
    public string[] textoPorFps =
    {
        "<b>6 FPS:</b> dá pra ver cada quadro. A corrida parece travada, bom para estilo retrô ou movimento lento.",
        "<b>12 FPS:</b> o padrão comum de pixel art. Fica fluido sem precisar de muitos desenhos.",
        "<b>24 FPS:</b> mais rápido e liso. Com poucos quadros, a corrida fica apressada; mais FPS pede mais desenhos."
    };

    [TextArea(2, 3)]
    public string textoLoopLigado = "<b>Loop ligado:</b> ao chegar no último quadro, volta pro primeiro e repete sem parar.";

    [TextArea(2, 3)]
    public string textoLoopDesligado = "<b>Loop desligado:</b> toca uma vez e para no último quadro, como uma porta abrindo. A prévia espera um pouco e mostra de novo.";

    [Tooltip("Velocidade já marcada quando o painel abre. Não use a resposta certa aqui, senão o jogador acerta sem pensar.")]
    public int fpsInicial = 6;

    [Tooltip("Loop já marcado quando o painel abre.")]
    public bool loopInicial = false;

    [Tooltip("Com loop desligado, quanto tempo (segundos) a prévia fica parada no último quadro antes de tocar de novo.")]
    public float pausaSemLoop = 1.2f;

    [Header("Speed (tocar de trás pra frente)")]
    [Tooltip("Mostra a linha \"Speed\" no painel (1 = normal, -1 = de trás pra frente).")]
    public bool usarSpeed = false;

    [Tooltip("O painel recebe os frames ao contrário da linha do tempo. Ex: na Porta fechando, o painel mostra o clipe da porta ABRINDO e o jogador descobre que Speed -1 fecha a porta sem desenhar nada novo.")]
    public bool inverterQuadros = false;

    public string rotuloSpeed = "Speed (no Animator)";

    [TextArea(2, 3)]
    public string textoSpeedNormal = "<b>Speed 1:</b> toca o clipe do primeiro ao último quadro.";

    [TextArea(2, 3)]
    public string textoSpeedInvertido = "<b>Speed -1:</b> toca o MESMO clipe do último quadro pro primeiro, sem desenhar nada novo.";

    public int speedInicial = 1;

    [Header("Resposta certa")]
    public int fpsCerto = 12;
    public bool loopCerto = true;

    [Tooltip("Só vale com usarSpeed.")]
    public int speedCerto = 1;

    [Tooltip("Velocidade que 'quase' serve. Por padrão NÃO é aceita: com só 5 ou 6 desenhos, a corrida em 24 FPS dá a volta inteira em uns 0,2 s e fica apressada, que é justamente o que o texto do botão de 24 FPS explica.")]
    public int fpsQuase = 24;

    [Tooltip("Marque pra aceitar fpsQuase como resposta certa, com a observação de falasQuaseAceito.")]
    public bool aceitarFpsQuase = false;

    [Header("Falas de retorno (Prof Byte)")]
    [TextArea(2, 4)]
    public string[] falasAcerto =
    {
        "Isso! 12 quadros por segundo e Loop Time ligado.",
        "No Unity, o Sample Rate fica na janela Animation, e o Loop Time é uma caixinha no Inspector do próprio Animation Clip.",
        "Corrida e personagem parado respirando usam loop. Pulo, porta abrindo e baú costumam tocar uma vez só."
    };

    [Tooltip("Só usado se aceitarFpsQuase estiver marcado.")]
    [TextArea(2, 4)]
    public string[] falasQuaseAceito =
    {
        "Funciona! Só repara que em 24 FPS a corrida ficou bem apressada. Com poucos desenhos, 12 FPS costuma ficar mais natural.",
        "No Unity, o Sample Rate fica na janela Animation, e o Loop Time é uma caixinha no Inspector do próprio Animation Clip."
    };

    [Tooltip("Quando o loop escolhido está errado.")]
    [TextArea(2, 4)]
    public string[] falasErroLoop =
    {
        "Com o Loop Time desligado, a corrida toca uma vez e o personagem fica congelado no último quadro, mesmo continuando a andar pela fase.",
        "Loop desligado serve pra animação que acontece uma vez só, tipo uma porta abrindo. A corrida precisa repetir."
    };

    [Tooltip("Quando a velocidade escolhida é MENOR que a certa.")]
    [TextArea(2, 4)]
    public string[] falasErroFpsLento =
    {
        "Em 6 FPS cada quadro fica tempo demais na tela. Dá pra contar os desenhos e a corrida parece travada, quase em câmera lenta.",
        "Pra ficar fluida, ela precisa de mais quadros por segundo."
    };

    [Tooltip("Quando a velocidade escolhida é MAIOR que a certa.")]
    [TextArea(2, 4)]
    public string[] falasErroFpsRapido =
    {
        "Em 24 FPS a volta inteira da corrida passa num piscar de olhos. Com poucos desenhos, as pernas giram rápido demais e parece que o personagem está patinando.",
        "24 FPS fica bonito quando o artista desenha muitos quadros. Com poucos desenhos, um valor menor funciona melhor."
    };

    [Tooltip("Quando o Speed escolhido está errado (só com usarSpeed).")]
    [TextArea(2, 4)]
    public string[] falasErroSpeed =
    {
        "Com Speed 1 o clipe toca do jeito que foi montado: a porta abre em vez de fechar.",
        "Pra reaproveitar o clipe ao contrário, o Speed precisa ser negativo."
    };

    [Tooltip("Dica extra, somada às falas de erro a partir do erro seguinte a errosAntesDaDica.")]
    [TextArea(2, 3)]
    public string[] dicaAposErros =
    {
        "Uma dica: a maioria dos jogos em pixel art usa por volta de 12 quadros por segundo nas animações de movimento."
    };

    [Tooltip("Quantos erros o jogador pode cometer antes de a dica extra aparecer junto das falas de erro.")]
    public int errosAntesDaDica = 2;

    /// <summary>Texto de apoio do painel pra combinação escolhida (velocidade + loop).</summary>
    public string TextoExplicacao(int fps, bool loop) => TextoExplicacao(fps, loop, 1);

    /// <summary>Texto de apoio do painel pra combinação escolhida (velocidade + loop + speed).</summary>
    public string TextoExplicacao(int fps, bool loop, int speed)
    {
        string textoFps = "";
        if (fpsOpcoes != null && textoPorFps != null)
        {
            int indice = Array.IndexOf(fpsOpcoes, fps);
            if (indice >= 0 && indice < textoPorFps.Length)
            {
                textoFps = textoPorFps[indice];
            }
        }

        string textoLoop = loop ? textoLoopLigado : textoLoopDesligado;
        string texto = string.IsNullOrEmpty(textoFps) ? textoLoop : textoFps + "\n" + textoLoop;
        if (usarSpeed)
        {
            texto += "\n" + (speed < 0 ? textoSpeedInvertido : textoSpeedNormal);
        }
        return texto;
    }

    /// <summary>
    /// Confere a combinação escolhida e devolve as falas do Prof pra ela.
    /// No erro, as falas explicam SÓ o que está errado (loop, velocidade ou
    /// os dois), e depois de alguns erros ganham uma dica mais concreta.
    /// </summary>
    public Resultado Avaliar(int fps, bool loop, int errosAnteriores, out string[] falas) => Avaliar(fps, loop, 1, errosAnteriores, out falas);

    public Resultado Avaliar(int fps, bool loop, int speed, int errosAnteriores, out string[] falas)
    {
        bool speedOk = !usarSpeed || Math.Sign(speed) == Math.Sign(speedCerto);
        bool loopESpeedOk = loop == loopCerto && speedOk;
        bool fpsExato = fps == fpsCerto;
        bool fpsQuaseOk = aceitarFpsQuase && fps == fpsQuase;

        if (loopESpeedOk && fpsExato)
        {
            falas = falasAcerto ?? Array.Empty<string>();
            return Resultado.Certo;
        }

        if (loopESpeedOk && fpsQuaseOk)
        {
            falas = falasQuaseAceito ?? Array.Empty<string>();
            return Resultado.CertoComObservacao;
        }

        var lista = new List<string>();
        if (!speedOk)
        {
            Somar(lista, falasErroSpeed);
        }
        if (loop != loopCerto)
        {
            Somar(lista, falasErroLoop);
        }

        if (!fpsExato && !fpsQuaseOk)
        {
            Somar(lista, fps < fpsCerto ? falasErroFpsLento : falasErroFpsRapido);
        }

        if (errosAnteriores + 1 > errosAntesDaDica)
        {
            Somar(lista, dicaAposErros);
        }

        falas = lista.ToArray();
        return Resultado.Errado;
    }

    private static void Somar(List<string> lista, string[] falas)
    {
        if (falas != null)
        {
            lista.AddRange(falas);
        }
    }
}
