#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using E = ObjetoAnimatorUI.Estado;
using S = ObjetoAnimatorUI.Seta;
using V = ObjetoAnimatorUI.Evento;
using C = ObjetoAnimatorUI.Condicao;
using P = AnimatorPuzzleUI.Peca;

/// <summary>
/// Editor: conteúdo dos painéis ObjetoAnimatorUI da fase Animacao (moeda,
/// jumppad, alavanca, bateria), espelhando os Animator Controllers de
/// Assets/Anim/FaseAnimacao. Os quadros da prévia vêm dos clipes de cada
/// estado. Depois de aplicado, tudo fica salvo na cena e pode ser editado no
/// Inspector.
/// </summary>
public static class ObjetoAnimatorModelos
{
    private const string Cinza = "<color=#8B95A7>";
    private const string Amar = "<color=#F5C542>";
    private const string Fim = "</color>";

    public static void Aplicar(ObjetoAnimatorUI ui, string tipo)
    {
        switch (tipo)
        {
            case "moeda": Moeda(ui); break;
            case "jumppad": Jumppad(ui); break;
            case "alavanca": Alavanca(ui); break;
            case "bateria": Bateria(ui); break;
            default: Debug.LogWarning("[ObjetoAnimatorModelos] tipo desconhecido: " + tipo); return;
        }

        EditorUtility.SetDirty(ui);
    }

    private static P Peca(string texto, int seta, string descricao) => PreencherCartao(new P { texto = texto, setaCorreta = seta, descricao = descricao });

    private const string Kit = "Assets/Images/EditorKit/";
    private static readonly Color Verde = new Color(0.204f, 0.788f, 0.549f);
    private static readonly Color Rosa = new Color(0.957f, 0.447f, 0.714f);
    private static readonly Color Lima = new Color(0.639f, 0.902f, 0.208f);
    private static readonly Color Laranja = new Color(0.984f, 0.573f, 0.235f);
    private static readonly Color Anil = new Color(0.506f, 0.549f, 0.973f);

    /// <summary>
    /// Cartão da peça na moldura do puzzle do Animator:
    /// ícone, moldura, categoria e linha de baixo, pelo texto da condição.
    /// Mesmas cores do puzzle do Animator do Player (parâmetro laranja,
    /// condição verde, entrada rosa, método lima/rosa).
    /// </summary>
    public static P PreencherCartao(P p)
    {
        string t = p.texto ?? "";
        if (t.Contains("(Trigger)") || t.StartsWith("pular ==") || t.StartsWith("puxar =="))
            Cartao(p, "icone_istrigger_laranja", "peca_bodytype_dynamic", "PARÂMETRO", Laranja, "Trigger do Animator");
        else if (t.StartsWith("carga"))
            Cartao(p, "icone_istrigger_laranja", "peca_bodytype_dynamic", "PARÂMETRO", Laranja, "Int do Animator");
        else if (t.StartsWith("ligada"))
            Cartao(p, "icone_istrigger_laranja", "peca_bodytype_dynamic", "PARÂMETRO", Laranja, "Bool do Animator");
        else if (t.StartsWith("Has Exit"))
            Cartao(p, "icone_fixedupdate_lima", "peca_update", "TRANSIÇÃO", Lima, "espera o clipe acabar");
        else if (t.StartsWith("OnTrigger"))
            Cartao(p, "icone_ontriggerenter2d_rosa", "peca_input", "MÉTODO", Rosa, "MonoBehaviour");
        else if (t.StartsWith("isGrounded"))
            Cartao(p, "icone_isgrounded_verde", "peca_collider2d", "CONDIÇÃO", Verde, "Collider2D · chão");
        else if (t.StartsWith("velocidade"))
            Cartao(p, "icone_velocity_anil", "peca_velocity", "PARÂMETRO", Anil, "Float do Player");
        else if (t.StartsWith("Input.GetMouse"))
            Cartao(p, "icone_input_rosa", "peca_input", "ENTRADA", Rosa, "mouse");
        else if (t.StartsWith("Input"))
            Cartao(p, "icone_input_rosa", "peca_input", "ENTRADA", Rosa, "teclado");
        else
            Cartao(p, "icone_start_lima", "peca_update", "MÉTODO", Lima, "MonoBehaviour");
        return p;
    }

    private static void Cartao(P p, string icone, string moldura, string categoria, Color cor, string sub)
    {
        p.icone = AssetDatabase.LoadAssetAtPath<Sprite>(Kit + "icones/categorias/" + icone + ".png");
        p.moldura = AssetDatabase.LoadAssetAtPath<Sprite>(Kit + "pecas/" + moldura + ".png");
        p.categoria = categoria;
        p.corCategoria = cor;
        p.subtitulo = sub;
    }

    // Quadros de um estado, lidos do clipe no Animator Controller.
    public static void PreencherQuadros(ObjetoAnimatorUI ui, RuntimeAnimatorController ctrl)
    {
        var porNome = new Dictionary<string, AnimationClip>();
        var ac = ctrl as UnityEditor.Animations.AnimatorController;
        if (ac == null) return;
        foreach (var st in ac.layers[0].stateMachine.states)
        {
            if (st.state.motion is AnimationClip clip) porNome[st.state.name] = clip;
        }

        foreach (E e in ui.Estados)
        {
            if (!porNome.TryGetValue(e.nome, out AnimationClip clip)) continue;
            var quadros = new List<Sprite>();
            foreach (EditorCurveBinding b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (b.type != typeof(SpriteRenderer) || b.propertyName != "m_Sprite" || !string.IsNullOrEmpty(b.path)) continue;
                foreach (ObjectReferenceKeyframe k in AnimationUtility.GetObjectReferenceCurve(clip, b))
                {
                    if (k.value is Sprite sp && (quadros.Count == 0 || quadros[quadros.Count - 1] != sp)) quadros.Add(sp);
                }
            }

            e.quadros = quadros.ToArray();
            float dur = clip.length > 0.01f ? clip.length : 0.4f;
            e.fps = quadros.Count > 0 ? Mathf.Max(2f, quadros.Count / dur) : 10f;
            e.loop = clip.isLooping;
        }

        EditorUtility.SetDirty(ui);
    }

    private static void Moeda(ObjetoAnimatorUI ui)
    {
        ui.DefinirTudo("moeda", "coletada", "Trigger", "(o script dispara ao encostar)",
            new[] { new E { nome = "Girando" }, new E { nome = "Coletada", someNoFim = true } }, 0,
            new[] { new S { de = 0, para = 1, condicao = C.Trigger } },
            new[]
            {
                Peca("ligada == true", -1, "Parâmetro Bool do Animator da lâmpada."),
                Peca("coletada (Trigger)", 0, "O Trigger coletada é disparado pelo script quando o personagem encosta na moeda. A seta segue uma vez e o Trigger volta sozinho."),
                Peca("Has Exit Time", -1, "Sem condição: a seta segue quando o clipe chega ao fim."),
                Peca("OnTriggerEnter2D()", -1, "Método do script, chamado quando outro collider entra no collider Trigger deste objeto."),
                Peca("isGrounded == true", -1, "Parâmetro do Animator do Player: verdadeiro quando ele está no chão."),
                Peca("velocidadeX > 0.1", -1, "Parâmetro do Animator do Player: a velocidade horizontal dele."),
            },
            "Configure o Animator da Moeda", "Animator da Moeda (Base Layer)", "Uma moeda que gira e some",
            "O clipe Girando tem Loop Time: a moeda gira sem parar. Quando o personagem encosta, o script dispara o Trigger coletada. Sem condição na seta, a moeda gira pra sempre e não pode ser pega.",
            "O Animator da moeda foi zerado. Pense: o que avisa o Animator que a moeda foi pega?",
            "Moeda configurada!",
            "Na prévia, a moeda gira até o Trigger coletada disparar, toca o clipe Coletada uma vez e some.",
            3.4f, true,
            new[] { new V { tempo = 1.4f, leitura = "o personagem encosta: coletada disparado" } },
            "a moeda gira esperando o personagem",
            new[]
            {
                "Essa moeda está parada: o Animator dela ainda não foi configurado, então ela nem gira nem pode ser pega.",
                "Clique numa moeda ou use a tecla de interagir perto dela pra abrir o Animator.",
            },
            "Animator da moeda",
            Cinza + "// MoedaFase: o personagem encostou" + Fim + "\n" +
            "void OnTriggerEnter2D(Collider2D outro) {\n" +
            "    animator.SetTrigger(" + Amar + "\"coletada\"" + Fim + ");\n}\n" +
            Cinza + "// Transição (a seta que você montou)" + Fim + "\n" +
            "Girando -> Coletada:  " + Amar + "coletada" + Fim + " (Trigger)\n" +
            Cinza + "// o clipe Girando tem Loop Time; o Coletada toca uma vez" + Fim);
        ui.DefinirPrevia(false, false, new Vector2(110f, 110f));
    }

    private static void Jumppad(ObjetoAnimatorUI ui)
    {
        ui.DefinirTudo("jumppad", "pular", "Trigger", "(o script dispara ao pisar)",
            new[] { new E { nome = "Repouso" }, new E { nome = "Impulso", loop = false } }, 0,
            new[] { new S { de = 0, para = 1, condicao = C.Trigger }, new S { de = 1, para = 0, condicao = C.ExitTime } },
            new[]
            {
                Peca("pular (Trigger)", 0, "O Trigger pular é disparado pelo script quando o personagem cai em cima do jumppad. A seta segue uma vez e o Trigger volta sozinho."),
                Peca("isGrounded == false", -1, "Parâmetro do Animator do Player: verdadeiro quando ele está no ar."),
                Peca("Has Exit Time", 1, "Sem condição: a seta segue quando o clipe chega ao fim."),
                Peca("pular == false", -1, "Um Trigger não tem valor true ou false pra testar: ele só dispara e volta sozinho."),
                Peca("Input.GetKey(Space)", -1, "Lê o teclado dentro de um script: verdadeiro enquanto a barra de espaço está apertada."),
                Peca("ligada == true", -1, "Parâmetro Bool do Animator da lâmpada."),
            },
            "Configure o Animator do Jumppad", "Animator do Jumppad (Base Layer)", "Um jumppad que dá impulso",
            "Quando o personagem cai em cima, o script dispara o Trigger pular. Uma seta leva para Impulso; a outra precisa trazer o jumppad de volta para Repouso. Sem as setas, o jumppad não funciona.",
            "O Animator do jumppad foi zerado. Pense: o que faz ele ir para Impulso? E como ele sabe a hora de voltar?",
            "Jumppad configurado!",
            "Na prévia, pular leva para Impulso, e o Has Exit Time volta para Repouso quando o clipe acaba.",
            2.6f, false,
            new[] { new V { tempo = 0.9f, leitura = "o personagem cai em cima: pular disparado" } },
            "esperando alguém pisar",
            new[]
            {
                "Esse jumppad não reage: o Animator dele ainda não foi configurado.",
                "Clique nele ou use a tecla de interagir perto dele pra abrir o Animator.",
            },
            "Animator do jumppad",
            Cinza + "// JumppadFase: o personagem caiu em cima" + Fim + "\n" +
            "animator.SetTrigger(" + Amar + "\"pular\"" + Fim + ");\n" +
            "player.Impulsionar(altura);\n" +
            Cinza + "// Transições (as setas que você montou)" + Fim + "\n" +
            "Repouso -> Impulso:  " + Amar + "pular" + Fim + " (Trigger)\n" +
            "Impulso -> Repouso:  " + Amar + "Has Exit Time" + Fim + " (sem condição)");
        ui.DefinirPrevia(false, false, new Vector2(150f, 150f));
    }

    private static void Alavanca(ObjetoAnimatorUI ui)
    {
        ui.DefinirTudo("alavanca", "puxar", "Trigger", "(o script dispara na tecla de interagir)",
            new[] { new E { nome = "Parada" }, new E { nome = "Puxando", loop = false }, new E { nome = "Puxada" } }, 0,
            new[] { new S { de = 0, para = 1, condicao = C.Trigger }, new S { de = 1, para = 2, condicao = C.ExitTime } },
            new[]
            {
                Peca("Input.GetKeyDown(E)", -1, "Lê o teclado dentro de um script: verdadeiro no quadro em que a tecla E é apertada."),
                Peca("puxar (Trigger)", 0, "O Trigger puxar é disparado pelo script quando o jogador aperta a tecla de interagir perto da alavanca."),
                Peca("carga == 1", -1, "Parâmetro Int do Animator da bateria."),
                Peca("Has Exit Time", 1, "Sem condição: a seta segue quando o clipe chega ao fim."),
                Peca("velocidadeX > 0.1", -1, "Parâmetro do Animator do Player: a velocidade horizontal dele."),
                Peca("puxar == false", -1, "Um Trigger não tem valor true ou false pra testar: ele só dispara e volta sozinho."),
            },
            "Configure o Animator da Alavanca", "Animator da Alavanca (Base Layer)", "Uma alavanca de três estados",
            "A tecla de interagir faz o script disparar o Trigger puxar. A alavanca vai de Parada para Puxando e, quando o clipe acaba, fica em Puxada. Sem as setas, ela não sai do lugar.",
            "O Animator da alavanca foi zerado. Pense: o que tira a alavanca de Parada? E o que leva de Puxando para Puxada?",
            "Alavanca configurada!",
            "Na prévia, puxar leva para Puxando, e o Has Exit Time termina em Puxada.",
            3.6f, true,
            new[] { new V { tempo = 1.0f, leitura = "tecla de interagir: puxar disparado" } },
            "esperando alguém puxar",
            new[]
            {
                "Essa alavanca está travada: o Animator dela ainda não foi configurado.",
                "Clique nela ou use a tecla de interagir perto dela pra abrir o Animator.",
            },
            "Animator da alavanca",
            Cinza + "// AlavancaFase: tecla de interagir perto dela" + Fim + "\n" +
            "animator.SetTrigger(" + Amar + "\"puxar\"" + Fim + ");\n" +
            "bateria.Carregar();\n" +
            Cinza + "// Transições (as setas que você montou)" + Fim + "\n" +
            "Parada -> Puxando:  " + Amar + "puxar" + Fim + " (Trigger)\n" +
            "Puxando -> Puxada:  " + Amar + "Has Exit Time" + Fim + " (sem condição)");
        ui.DefinirPrevia(false, false, new Vector2(170f, 170f));
    }

    private static void Bateria(ObjetoAnimatorUI ui)
    {
        ui.DefinirTudo("bateria", "carga", "Int", "(cada alavanca soma 1)",
            new[] { new E { nome = "Vazia" }, new E { nome = "Metade" }, new E { nome = "Cheia" } }, 0,
            new[]
            {
                new S { de = -1, para = 0, condicao = C.IntIgual, valor = 0 },
                new S { de = -1, para = 1, condicao = C.IntIgual, valor = 1 },
                new S { de = -1, para = 2, condicao = C.IntIgual, valor = 2 },
            },
            new[]
            {
                Peca("carga == 2", 2, "O parâmetro Int carga vale 2: as duas alavancas foram puxadas."),
                Peca("carga > 0", -1, "Verdadeiro com qualquer carga acima de zero: 1 ou 2."),
                Peca("carga == 0", 0, "O parâmetro Int carga vale 0: nenhuma alavanca foi puxada ainda."),
                Peca("puxar (Trigger)", -1, "Trigger do Animator da alavanca."),
                Peca("carga == 1", 1, "O parâmetro Int carga vale 1: uma alavanca foi puxada."),
                Peca("Has Exit Time", -1, "Sem condição: a seta segue quando o clipe chega ao fim."),
            },
            "Configure o Animator da Bateria", "Animator da Bateria (Base Layer)", "Uma bateria com parâmetro Int",
            "A bateria tem um parâmetro Int chamado carga: cada alavanca puxada soma 1. Do Any State sai uma seta pra cada estado, e cada seta testa um valor. Sem as setas, a bateria não mostra a carga e a porta não destranca.",
            "O Animator da bateria foi zerado. Pense: qual valor de carga combina com cada desenho da bateria?",
            "Bateria configurada!",
            "Na prévia, carga vai de 0 a 2 e o Any State leva direto para o estado com o valor certo.",
            5.4f, false,
            new[]
            {
                new V { tempo = 0.05f, valor = 0, leitura = "nenhuma alavanca puxada: carga = 0" },
                new V { tempo = 1.8f, valor = 1, leitura = "uma alavanca puxada: carga = 1" },
                new V { tempo = 3.6f, valor = 2, leitura = "duas alavancas puxadas: carga = 2" },
            },
            "",
            new[]
            {
                "Essa bateria não mostra a carga: o Animator dela ainda não foi configurado. Sem ele, a porta não destranca.",
                "Clique nela ou use a tecla de interagir perto dela pra abrir o Animator.",
            },
            "Animator da bateria",
            Cinza + "// BateriaFase: cada alavanca puxada soma 1" + Fim + "\n" +
            "carga++;\n" +
            "animator.SetInteger(" + Amar + "\"carga\"" + Fim + ", carga);\n" +
            Cinza + "// Transições (as setas que você montou)" + Fim + "\n" +
            "Any State -> Vazia:   " + Amar + "carga == 0" + Fim + "\n" +
            "Any State -> Metade:  " + Amar + "carga == 1" + Fim + "\n" +
            "Any State -> Cheia:   " + Amar + "carga == 2" + Fim);
        ui.DefinirPrevia(false, false, new Vector2(150f, 150f));
    }
}
#endif
