/// <summary>
/// Troca marcadores de tecla nas falas e objetivos pela tecla configurada em
/// Controles, pra o texto não ficar errado quando o aluno troca as teclas:
/// "{andar}" vira Mover Esquerda/Mover Direita (ex.: "A/D"; as setas sempre
/// valem, então o texto escreve "setas ou {andar}") e "{pular}" vira a tecla
/// de Pular (ex.: "Espaço"). Chamado no DialogueManager.StartDialogue e na
/// faixa de objetivo (ObjetivoFase). "{tecla}", "{C}" e "{reiniciar}"
/// continuam com ReinicioFase, AnimacaoLevelFlow e ObjetivoFase.
/// </summary>
public static class TextoTeclas
{
    private const string Andar = "{andar}";
    private const string Pular = "{pular}";

    public static string Trocar(string texto)
    {
        if (string.IsNullOrEmpty(texto) || texto.IndexOf('{') < 0)
        {
            return texto;
        }

        if (texto.Contains(Andar))
        {
            texto = texto.Replace(Andar, Nome("Move", "negative", "A") + "/" + Nome("Move", "positive", "D"));
        }

        if (texto.Contains(Pular))
        {
            texto = texto.Replace(Pular, Nome("Jump", "", "Espaço"));
        }

        return texto;
    }

    /// <summary>Nome da tecla atual da ação, já traduzido pelo InputManager (Espaço, ←...).</summary>
    public static string Nome(string acao, string parte, string padrao)
    {
        string nome = InputManager.Instance != null ? InputManager.Instance.GetBindingName(acao, parte) : "";
        return string.IsNullOrEmpty(nome) ? padrao : nome;
    }

    /// <summary>
    /// Nome de tecla do Input System em português (Space → Espaço, Left Shift → Shift,
    /// Left Arrow → ←...). Usado pelo InputManager.GetBindingName, então vale para o
    /// balão, as Configurações, a pausa e as faixas de objetivo.
    /// </summary>
    public static string Traduzir(string nome)
    {
        switch (nome)
        {
            case "Space": return "Espaço";
            case "Left Shift":
            case "Right Shift": return "Shift";
            case "Left Ctrl":
            case "Right Ctrl":
            case "Left Control":
            case "Right Control": return "Ctrl";
            case "Left Arrow":
            case "Left": return "←";
            case "Right Arrow":
            case "Right": return "→";
            case "Up Arrow":
            case "Up": return "↑";
            case "Down Arrow":
            case "Down": return "↓";
            case "Left Alt":
            case "Right Alt": return "Alt";
            case "Escape": return "Esc";
            case "Delete": return "Del";
            case "LMB": return "Clique esq.";
            case "RMB": return "Clique dir.";
            case "MMB": return "Clique meio";
            default: return nome;
        }
    }
}
