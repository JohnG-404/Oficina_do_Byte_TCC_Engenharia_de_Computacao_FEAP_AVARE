using TMPro;
using UnityEngine;

/// <summary>
/// Puzzles da cena Fisica no modelo "código com lacunas": acompanha quais
/// lacunas (SnapZone) já foram preenchidas e atualiza o que é só visual —
/// o brilho da próxima lacuna, a faixa da linha atual, o ✔ das linhas
/// completas, os pontos de progresso da prévia e a dica do painel.
///
/// Não decide nada do puzzle (isso continua no SnapZone/PuzzleControl).
/// Todos os objetos que ele liga/desliga são montados e salvos na cena pelo
/// FisicaCodigoBuilder (Tools > Física > Aplicar código com lacunas).
/// </summary>
public class CodigoEncaixeUI : MonoBehaviour
{
    [System.Serializable]
    public class Lacuna
    {
        public SnapZone zona;
        [Tooltip("Contorno que brilha quando esta é a próxima lacuna.")]
        public GameObject destaque;
        [Tooltip("Índice da linha (em Linhas) onde a lacuna está.")]
        public int linha;
        [Tooltip("Dica mostrada no painel enquanto esta é a próxima lacuna.")]
        [TextArea(1, 3)] public string dica;
    }

    [System.Serializable]
    public class Linha
    {
        [Tooltip("Faixa clara atrás da linha enquanto ela tem a próxima lacuna.")]
        public GameObject faixaAtual;
        [Tooltip("✔ na margem quando todas as lacunas da linha estão preenchidas.")]
        public GameObject marcaOk;
    }

    [SerializeField] private Lacuna[] lacunas = new Lacuna[0];
    [SerializeField] private Linha[] linhas = new Linha[0];

    [Header("Prévia")]
    [SerializeField] private SpriteRenderer[] pontos = new SpriteRenderer[0];
    [SerializeField] private TMP_Text textoProgresso;
    [SerializeField] private Color corPontoFeito = new Color32(0xF5, 0xC5, 0x42, 0xFF);
    [SerializeField] private Color corPontoVazio = new Color32(0x22, 0x30, 0x4A, 0xFF);

    [Header("Dica")]
    [SerializeField] private InfoPanelUI painel;
    [SerializeField] private string tituloDica = "Dica";
    [Tooltip("Mostrado no painel quando todas as lacunas estão encaixadas, no lugar da dica da última.")]
    [SerializeField] private string tituloPronto = "Pronto";
    [TextArea(1, 3)]
    [SerializeField] private string textoPronto = "Todas as lacunas encaixadas. O código está montado.";

    private int ultimoEstado = -2;

    private void OnEnable()
    {
        ultimoEstado = -2;
        Atualizar();
    }

    private void Update()
    {
        Atualizar();
    }

    private void Atualizar()
    {
        int feitas = 0;
        int proxima = -1;
        for (int i = 0; i < lacunas.Length; i++)
        {
            bool ok = lacunas[i].zona != null && lacunas[i].zona.Ocupado;
            if (ok) feitas++;
            else if (proxima < 0) proxima = i;
        }

        // Só redesenha quando muda alguma coisa (estado = quantas + qual).
        int estado = feitas * 100 + proxima;
        if (estado == ultimoEstado) return;
        ultimoEstado = estado;

        for (int i = 0; i < lacunas.Length; i++)
        {
            if (lacunas[i].destaque != null) lacunas[i].destaque.SetActive(i == proxima);
        }

        int linhaAtual = proxima >= 0 ? lacunas[proxima].linha : -1;
        for (int l = 0; l < linhas.Length; l++)
        {
            bool temLacuna = false, completa = true;
            foreach (Lacuna lac in lacunas)
            {
                if (lac.linha != l) continue;
                temLacuna = true;
                if (lac.zona == null || !lac.zona.Ocupado) completa = false;
            }

            if (linhas[l].faixaAtual != null) linhas[l].faixaAtual.SetActive(l == linhaAtual);
            if (linhas[l].marcaOk != null) linhas[l].marcaOk.SetActive(temLacuna && completa);
        }

        for (int p = 0; p < pontos.Length; p++)
        {
            if (pontos[p] != null) pontos[p].color = p < feitas ? corPontoFeito : corPontoVazio;
        }

        if (textoProgresso != null)
        {
            textoProgresso.text = $"<b>{feitas} de {lacunas.Length} encaixe{(lacunas.Length == 1 ? "" : "s")}.</b>";
        }

        if (painel != null && proxima >= 0 && !string.IsNullOrEmpty(lacunas[proxima].dica))
        {
            painel.DefinirPadrao(tituloDica, lacunas[proxima].dica);
        }
        else if (painel != null && proxima < 0 && lacunas.Length > 0)
        {
            painel.DefinirPadrao(tituloPronto, textoPronto);
        }
    }
}
