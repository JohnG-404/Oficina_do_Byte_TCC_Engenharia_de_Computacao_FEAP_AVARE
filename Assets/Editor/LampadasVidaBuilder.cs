using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Transforma as lâmpadas do teto da Interacao_Vida (Cenario/Lampadas) em
/// itens configuráveis (23/10/2026):
/// - encosta o suporte da lâmpada no teto (o sprite tem borda transparente,
///   então antes sobrava um vão);
/// - põe o Animator das lâmpadas da sala escura da Animacao
///   (LampadaFase.controller) e o brilho vira o filho LuzLampada, que o clip
///   Lampada_Acender clareia. No Editor a lâmpada fica apagada, como no Play;
/// - põe o InteractableItem "Lâmpada" (painel Configurar item), a
///   LampadaInteracao e uma ProximityZone que vai do chão ao teto, pra
///   "Passar perto" e "Pressionar um botão" funcionarem com o jogador
///   embaixo da lâmpada.
/// Depois remonta o destaque dos itens (etiqueta embaixo da lâmpada).
///
/// Uso: Tools > Interação > Lâmpadas configuráveis (Vida), com a
/// Interacao_Vida aberta; salvar com Ctrl+S. Pode rodar de novo.
/// O InteracaoVidaTerrenoBuilder também chama este no fim.
/// </summary>
public static class LampadasVidaBuilder
{
    private const string VidaPath = "Assets/Scenes/Interacao_Vida.unity";
    private const string ControllerPath = "Assets/Anim/FaseAnimacao/LampadaFase.controller";
    private const string NomeLuz = "LuzLampada"; // caminho animado pelo clip Lampada_Acender

    // Grade do terreno (ver InteracaoVidaTerrenoBuilder): os tetos caem em
    // y = -4 + 0,8·n.
    private const float GradeY0 = -4f, GradeH = 0.8f;

    // Parte desenhada de lampada_fase_01 (320x320 px, 256 px/unidade, pivô no
    // centro): x 72..247 e y 22..237 na textura. Com flipY o suporte (y = 22)
    // fica em cima.
    private const float Ppu = 256f, Pivo = 160f;
    private const float TexelSuporte = 22f, TexelFundo = 237f, TexelEsq = 72f, TexelDir = 247f;
    private const float Sobreposicao = 0.02f; // o suporte entra um tiquinho no teto

    private const float LarguraZona = 4f;
    private const float ChaoZona = -2.6f; // abaixo do chão mais fundo (poço da sala 3, -2,4)

    [MenuItem("Tools/Interação/Lâmpadas configuráveis (Vida)")]
    public static void Menu()
    {
        Scene cena = SceneManager.GetSceneByPath(VidaPath);
        if (!cena.IsValid() || !cena.isLoaded)
        {
            Debug.LogError("[LampadasVidaBuilder] Abra a cena Interacao_Vida antes de rodar.");
            return;
        }

        foreach (string linha in Montar(cena)) Debug.Log("[LampadasVidaBuilder] " + linha);
        foreach (string linha in DestaqueInteracaoBuilder.Build(VidaPath)) Debug.Log("[DestaqueInteracaoBuilder] " + linha);
    }

    public static List<string> Montar(Scene cena)
    {
        var log = new List<string>();
        Transform grupo = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Cenario")?.transform.Find("Lampadas");
        if (grupo == null)
        {
            log.Add("ERRO: Cenario/Lampadas não encontrado.");
            return log;
        }

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (controller == null)
        {
            log.Add("ERRO: " + ControllerPath + " não encontrado.");
            return log;
        }

        var lampadas = new List<Transform>();
        var brilhos = new List<Transform>();
        foreach (Transform filho in grupo)
        {
            if (filho.name == "Lampada") lampadas.Add(filho);
            else if (filho.name == "Brilho") brilhos.Add(filho);
        }

        foreach (Transform lampada in lampadas)
        {
            var sr = lampada.GetComponent<SpriteRenderer>();
            if (sr == null) continue;
            Undo.RegisterFullObjectHierarchyUndo(lampada.gameObject, "Lâmpadas configuráveis");

            // O Animator começa no estado Apagada (quadro 01).
            sr.sprite = PrimeiroQuadro(controller) ?? sr.sprite;
            sr.flipY = true;
            float escala = lampada.localScale.y;

            // Suporte encostado no teto.
            float topo = sr.bounds.max.y;
            float teto = GradeY0 + Mathf.Round((topo - GradeY0) / GradeH) * GradeH;
            float suporteAcimaDoCentro = (Pivo - TexelSuporte) / Ppu * escala;
            Vector3 p = lampada.position;
            p.y = teto + Sobreposicao - suporteAcimaDoCentro;
            lampada.position = p;

            // Brilho da mesma lâmpada vira o filho LuzLampada (apagado).
            Transform luz = lampada.Find(NomeLuz);
            Transform brilho = brilhos.OrderBy(b => Mathf.Abs(b.position.x - p.x)).FirstOrDefault();
            if (luz == null && brilho != null && Mathf.Abs(brilho.position.x - p.x) < 1f)
            {
                brilhos.Remove(brilho);
                Undo.SetTransformParent(brilho, lampada, "Lâmpadas configuráveis");
                brilho.name = NomeLuz;
                luz = brilho;
            }
            if (luz != null)
            {
                luz.localScale = Vector3.one / escala;
                var luzSr = luz.GetComponent<SpriteRenderer>();
                if (luzSr != null)
                {
                    Undo.RecordObject(luzSr, "Lâmpadas configuráveis");
                    Color c = luzSr.color;
                    c.a = 0f;
                    luzSr.color = c;
                }
            }

            var animator = Comp<Animator>(lampada);
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            Comp<LampadaInteracao>(lampada);

            // Collider de clique só na parte desenhada (sólido: com
            // queriesHitTriggers desligado, trigger não recebe o clique).
            var box = Comp<BoxCollider2D>(lampada);
            box.isTrigger = false;
            box.size = new Vector2((TexelDir - TexelEsq + 1f) / Ppu, (TexelFundo - TexelSuporte + 1f) / Ppu);
            // flipY não vira o collider: o centro da parte desenhada fica
            // acima do pivô depois do espelho.
            box.offset = new Vector2(((TexelEsq + TexelDir + 1f) * 0.5f - Pivo) / Ppu,
                                     (Pivo - (TexelSuporte + TexelFundo + 1f) * 0.5f) / Ppu);

            // Área de "perto": do chão ao teto, no trecho da lâmpada.
            Transform zona = lampada.Find("ProximityZone");
            if (zona == null)
            {
                zona = new GameObject("ProximityZone").transform;
                Undo.RegisterCreatedObjectUndo(zona.gameObject, "Lâmpadas configuráveis");
                zona.SetParent(lampada, false);
            }
            zona.localPosition = Vector3.zero;
            zona.localScale = Vector3.one / escala;
            foreach (CircleCollider2D velho in zona.GetComponents<CircleCollider2D>()) Object.DestroyImmediate(velho);
            var area = Comp<BoxCollider2D>(zona);
            area.isTrigger = true;
            area.size = new Vector2(LarguraZona, teto - ChaoZona);
            area.offset = new Vector2(0f, (teto + ChaoZona) * 0.5f - p.y);
            Comp<ItemProximityZone>(zona);

            var item = Comp<InteractableItem>(lampada);
            var so = new SerializedObject(item);
            so.FindProperty("displayName").stringValue = "Lâmpada";
            so.FindProperty("description").stringValue = "Lâmpada do teto, ainda apagada. Escolha como ela acende.";
            Enum(so.FindProperty("allowedActions"), (int)ActionType.PressButton, (int)ActionType.WalkNear,
                 (int)ActionType.LeaveArea, (int)ActionType.PressButtonThreeTimes);
            Enum(so.FindProperty("allowedConsequences"), (int)ConsequenceType.TurnOnLamp, (int)ConsequenceType.TurnOnLampForSeconds);
            so.ApplyModifiedPropertiesWithoutUndo();

            log.Add($"lâmpada em x={p.x:0.0}: teto {teto:0.0}, suporte em {p.y + suporteAcimaDoCentro:0.00}, " +
                    $"brilho {(luz != null ? "ok" : "não achado")}");
        }

        foreach (Transform sobra in brilhos) log.Add("AVISO: brilho sem lâmpada em x=" + sobra.position.x.ToString("0.0"));

        EditorSceneManager.MarkSceneDirty(cena);
        return log;
    }

    private static Sprite PrimeiroQuadro(RuntimeAnimatorController controller)
    {
        var ac = controller as AnimatorController;
        AnimatorState inicial = ac != null && ac.layers.Length > 0 ? ac.layers[0].stateMachine.defaultState : null;
        var clip = inicial != null ? inicial.motion as AnimationClip : null;
        if (clip == null) return null;
        foreach (EditorCurveBinding b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (b.path == "" && b.propertyName == "m_Sprite")
            {
                ObjectReferenceKeyframe[] k = AnimationUtility.GetObjectReferenceCurve(clip, b);
                if (k.Length > 0) return k[0].value as Sprite;
            }
        }
        return null;
    }

    private static T Comp<T>(Transform t) where T : Component
    {
        T c = t.GetComponent<T>();
        return c != null ? c : Undo.AddComponent<T>(t.gameObject);
    }

    private static void Enum(SerializedProperty arr, params int[] valores)
    {
        arr.arraySize = valores.Length;
        for (int i = 0; i < valores.Length; i++) arr.GetArrayElementAtIndex(i).intValue = valores[i];
    }
}
