using UnityEngine;

/// <summary>
/// Mantém a Oficina de Animação inteira na tela em qualquer proporção: o
/// layout foi montado pra 16:9 (17,8 x 10 unidades). Em telas mais
/// estreitas (4:3, 16:10) a câmera abre o tamanho ortográfico até caber a
/// largura toda, sobrando espaço em cima e embaixo em vez de cortar as laterais.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraOficinaAjuste : MonoBehaviour
{
    [SerializeField] private float larguraMinima = 17.8f;
    [SerializeField] private float alturaMinima = 10f;

    private Camera cam;
    private float ultimoAspecto = -1f;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        Ajustar();
    }

    private void LateUpdate()
    {
        if (!Mathf.Approximately(cam.aspect, ultimoAspecto))
        {
            Ajustar();
        }
    }

    private void Ajustar()
    {
        ultimoAspecto = cam.aspect;
        float metadeParaLargura = larguraMinima * 0.5f / Mathf.Max(0.01f, cam.aspect);
        cam.orthographicSize = Mathf.Max(alturaMinima * 0.5f, metadeParaLargura);
    }
}
