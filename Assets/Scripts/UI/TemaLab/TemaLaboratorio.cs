using TMPro;
using UnityEngine;

/// <summary>
/// Sprites e fontes do tema Laboratório para as telas montadas por código
/// (Questionário, mini-lição de HUD, Game Over). O asset fica em
/// Assets/Resources/TemaLaboratorio.asset e é carregado com Resources.Load,
/// então nenhuma cena precisa guardar referência a ele.
///
/// Se o asset não existir, as telas continuam funcionando com Images lisos
/// (ver TemaLabUI), só sem os sprites do kit.
/// </summary>
[CreateAssetMenu(fileName = "TemaLaboratorio", menuName = "UI/Tema Laboratório")]
public class TemaLaboratorio : ScriptableObject
{
    [System.Serializable]
    public class Estados
    {
        public Sprite normal;
        public Sprite hover;
        public Sprite pressed;
        public Sprite focus;
        public Sprite disabled;
    }

    [Header("Painéis (9-slice)")]
    public Sprite painelJanela;
    public Sprite painelPopup;
    public Sprite painelTitulo;
    public Sprite painelLinha;
    public Sprite painelLinhaFoco;
    public Sprite painelCodigo;
    [Tooltip("Retângulo branco arredondado, para caixas tingidas por cor (bordas, chips, tracejado).")]
    public Sprite bordaArredondada;

    [Header("Botões")]
    public Estados botaoPrimario = new Estados();
    public Estados botaoSecundario = new Estados();
    public Estados botaoIcone = new Estados();

    [Header("Selos, pontos e ícones")]
    public Sprite seloNumero;
    public Sprite seloCheck;
    public Sprite paginaPonto;
    public Sprite paginaPontoAtivo;
    public Sprite iconeJogar;
    public Sprite iconeVoltar;
    public Sprite iconeCheck;
    public Sprite iconeInfo;

    [Header("Fundo e vida")]
    public Sprite fundoCalmo;
    public Sprite vidaCheia;
    public Sprite vidaVazia;

    [Header("Fases")]
    [Tooltip("Retrato do Prof Byte (sem fundo), mostrado parado na caixa de diálogo.")]
    public Sprite retratoProfByte;
    [Tooltip("Lâmina da porta de enrolar (ver Door): repetida na vertical em modo Tiled.")]
    public Sprite portaLamina;

    [Header("Moldura dos puzzles (padrão da Física, ver MolduraPuzzleUI)")]
    public Sprite puzzleGradeFundo;
    public Sprite puzzleGizmoEixos;
    [Tooltip("Janela com faixa de cabeçalho (9-slice).")]
    public Sprite puzzleJanela;
    public Sprite puzzleAba;
    [Tooltip("Área escura de dentro das janelas (código, prévia).")]
    public Sprite puzzleArea;
    public Sprite puzzleDivisoria;
    public Sprite puzzleEncaixeVazio;
    public Sprite puzzleContornoSelecao;
    [Tooltip("Moldura neutra da peça já encaixada.")]
    public Sprite puzzlePecaEncaixada;
    public Sprite puzzleLampada;
    [Tooltip("Retângulo branco das caixas de estado e barras de progresso.")]
    public Sprite puzzleCaixa;
    public Sprite puzzleChaoPrevia;

    [Header("Fontes (TextMeshPro)")]
    public TMP_FontAsset display;
    public TMP_FontAsset corpo;
    public TMP_FontAsset corpoForte;
    public TMP_FontAsset mono;
    public TMP_FontAsset monoForte;

    private static TemaLaboratorio atual;
    private static bool procurado;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        atual = null;
        procurado = false;
    }

    /// <summary>O tema de Resources/TemaLaboratorio, ou null se não existir.</summary>
    public static TemaLaboratorio Atual
    {
        get
        {
            if (!procurado || atual == null)
            {
                procurado = true;
                atual = Resources.Load<TemaLaboratorio>("TemaLaboratorio");
            }
            return atual;
        }
    }
}
