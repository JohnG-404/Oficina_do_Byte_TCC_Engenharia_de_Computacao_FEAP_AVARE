using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Tela de créditos do Menu: uma
/// janela "Creditos.cs" no estilo do painel de código do Menu, com a música
/// final tocando. Aberta pelo botão Créditos (linha do Sair).
///
/// O texto vem do campo "Modelo" (código C# comum, editável no Inspector):
/// {desenvolvimento}, {orientacao} e {instituicao} viram os nomes dos campos
/// abaixo, e as cores de sintaxe são aplicadas sozinhas. Mudou algo no
/// Inspector, o texto da cena se atualiza no Editor.
///
/// Visual montado por Tools > Menu > Criar botão e tela de Créditos
/// (MenuCreditosBuilder), salvo na cena Menu.
/// </summary>
public class CreditosUI : MonoBehaviour
{
    [Header("Nomes")]
    [SerializeField] private string desenvolvimento = "(seu nome)";
    [SerializeField] private string orientacao = "(nome do orientador)";
    [SerializeField] private string instituicao = "(curso · instituição)";

    [Header("Texto")]
    [TextArea(12, 40)]
    [SerializeField] private string modelo =
@"// Oficina do Byte · TCC 2026
public class Creditos : MonoBehaviour
{
    // Quem fez
    string desenvolvimento = ""{desenvolvimento}"";
    string orientacao      = ""{orientacao}"";
    string instituicao     = ""{instituicao}"";

    // O que você aprende jogando
    string[] fisica    = { ""Rigidbody2D"", ""Collider2D"", ""Pulo"" };
    string[] interacao = { ""OnTriggerEnter2D"", ""Is Trigger"", ""Vida"" };
    string[] animacao  = { ""Animation Clip"", ""FPS"", ""Animator"" };
    string[] ia        = { ""Máquina de estados"", ""Raycast"" };

    // Feito com
    string motor  = ""Unity 6 · URP 2D"";
    string fontes = ""Baloo 2 · Nunito · JetBrains Mono"";

    void Start() => Debug.Log(""Obrigado por jogar!"");
}";

    [Header("Partes (ligadas pelo builder)")]
    [SerializeField] private GameObject painel;
    [SerializeField] private TMP_Text codigo;
    [SerializeField] private TMP_Text numeros;
    [SerializeField] private ScrollRect rolagem;
    [SerializeField] private Button botaoFechar;
    [Tooltip("Quanto ↑/↓ rolam por segundo (fração do texto).")]
    [SerializeField] private float velocidadeTeclas = 0.9f;

    private static CreditosUI instancia;
    private static int frameFechou = -1;

    public static bool Aberto => instancia != null && instancia.painel != null && instancia.painel.activeSelf;

    /// <summary>True também no frame em que fechou com Esc — pra o Esc não abrir as Configurações junto.</summary>
    public static bool SegurandoEsc => Aberto || Time.frameCount == frameFechou;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instancia = null;
        frameFechou = -1;
    }

    private void Awake()
    {
        instancia = this;
        if (botaoFechar != null) botaoFechar.onClick.AddListener(Fechar);
        MontarTexto();
        if (painel != null) painel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }

    /// <summary>Botão Créditos do Menu.</summary>
    public void Abrir()
    {
        if (painel == null || painel.activeSelf)
        {
            return;
        }

        MontarTexto();
        painel.SetActive(true);
        if (rolagem != null) rolagem.verticalNormalizedPosition = 1f;
        AudioManager.Instance?.PlayMusic(AudioManager.MusicTrack.Final);
    }

    public void Fechar()
    {
        if (painel == null || !painel.activeSelf)
        {
            return;
        }

        painel.SetActive(false);
        frameFechou = Time.frameCount;
        AudioManager.Instance?.PlayMusic(AudioManager.MusicTrack.Menu);
    }

    private void Update()
    {
        if (!Aberto)
        {
            return;
        }

        Keyboard teclado = Keyboard.current;
        if (teclado == null)
        {
            return;
        }

        if (teclado.escapeKey.wasPressedThisFrame || teclado.backspaceKey.wasPressedThisFrame)
        {
            Fechar();
            return;
        }

        if (rolagem != null)
        {
            float dir = (teclado.downArrowKey.isPressed || teclado.sKey.isPressed ? -1f : 0f)
                      + (teclado.upArrowKey.isPressed || teclado.wKey.isPressed ? 1f : 0f);
            if (dir != 0f)
            {
                rolagem.verticalNormalizedPosition = Mathf.Clamp01(rolagem.verticalNormalizedPosition + dir * velocidadeTeclas * Time.unscaledDeltaTime);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Atualiza o texto da cena ao mexer nos nomes/modelo no Inspector.
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && !Application.isPlaying)
            {
                MontarTexto();
                if (codigo != null) UnityEditor.EditorUtility.SetDirty(codigo);
                if (numeros != null) UnityEditor.EditorUtility.SetDirty(numeros);
            }
        };
    }
#endif

    // ------------------------------------------------------------ texto

    private const string CorComentario = "#5D6B8A";
    private const string CorPalavra = "#C792EA";
    private const string CorTipo = "#5FD4E8";
    private const string CorTexto = "#F5C542";
    private const string CorMetodo = "#82AAFF";

    private static readonly Regex Palavras = new Regex(@"\b(public|private|class|string|void|int|float|bool|new|return)\b");
    private static readonly Regex Tipos = new Regex(@"\b(Creditos|MonoBehaviour|Debug)\b");
    private static readonly Regex Metodos = new Regex(@"\b([A-Z]\w*)(?=\()");

    public void MontarTexto()
    {
        if (codigo == null)
        {
            return;
        }

        string texto = (modelo ?? "")
            .Replace("{desenvolvimento}", desenvolvimento)
            .Replace("{orientacao}", orientacao)
            .Replace("{instituicao}", instituicao)
            .Replace("\r", "");

        string[] linhas = texto.Split('\n');
        var sb = new StringBuilder();
        var nums = new StringBuilder();
        for (int i = 0; i < linhas.Length; i++)
        {
            sb.Append(Colorir(linhas[i]));
            nums.Append(i + 1);
            if (i < linhas.Length - 1)
            {
                sb.Append('\n');
                nums.Append('\n');
            }
        }

        codigo.text = sb.ToString();
        if (numeros != null) numeros.text = nums.ToString();
    }

    // Comentário inteiro numa cor; fora das aspas: palavras-chave, tipos e métodos.
    private static string Colorir(string linha)
    {
        string semEspaco = linha.TrimStart();
        if (semEspaco.StartsWith("//"))
        {
            return $"<color={CorComentario}>{Escapar(linha)}</color>";
        }

        var sb = new StringBuilder();
        string[] partes = linha.Split('"');
        for (int i = 0; i < partes.Length; i++)
        {
            string p = Escapar(partes[i]);
            if (i % 2 == 1)
            {
                sb.Append($"<color={CorTexto}>\"{p}\"</color>");
            }
            else
            {
                p = Palavras.Replace(p, $"<color={CorPalavra}>$1</color>");
                p = Metodos.Replace(p, m => Tipos.IsMatch(m.Value) ? m.Value : $"<color={CorMetodo}>{m.Value}</color>");
                p = Tipos.Replace(p, $"<color={CorTipo}>$1</color>");
                sb.Append(p);
            }
        }
        return sb.ToString();
    }

    private static string Escapar(string s) => s.Replace("<", "<noparse><</noparse>");
}
