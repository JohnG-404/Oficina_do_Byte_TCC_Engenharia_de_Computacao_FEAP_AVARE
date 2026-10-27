using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Monta os 3 puzzles da fase IA com o MESMO layout dos puzzles da Fisica:
/// abre Fisica.unity de forma aditiva, clona o grupo "PuzzleRig" pra dentro
/// da cena IA (SceneManager.MoveGameObjectToScene), fecha a Fisica e
/// re-skina cada clone como o FisicaPuzzleLayoutBuilder faz:
///
/// - cartão "Player" vira "Cérebro do Guarda";
/// - encaixes ganham um texto "DicaCodigo" (ex: "Patrulhar: ___");
/// - peças mostram os DOIS nomes (o método do jogo e, numa linha menor, o
///   equivalente no Unity), com descrição no hover;
/// - painel Descrição com a dica do puzzle;
/// - prévia (IAPuzzlePreview) com um mini-guarda e o estado ativo aceso;
/// - puzzle de Depuração: regras 2-4 fixas (IAPecaFixa) e a regra 1 errada
///   ("Distance &lt; raioVisao") dentro do encaixe, que o jogador troca por
///   Physics2D.Raycast.
///
/// Usado pelo IASceneBuilder (Tools/IA/Criar Cena IA). A Fisica nunca é
/// modificada nem salva.
/// </summary>
public static class IAPuzzleRigBuilder
{
    public const string FisicaScenePath = "Assets/Scenes/Fisica.unity";
    private const string PecasFolder = "Assets/Images/EditorKit/pecas/";
    private const string IconesFolder = "Assets/Images/EditorKit/icones/componentes/";
    private const string HintIconPath = "Assets/Images/EditorKit/icones/interface/icone_dica@2x.png";
    private const string GuardaSpritePath = "Assets/Images/player/player_skin_red.png";
    private const string PlayerSpritePath = "Assets/Images/player/player_square_final.png";
    private const string GroundSpritePath = "Assets/Images/Packs/Tecno/platform_tile_mid.png";
    private const string WallSpritePath = "Assets/Images/Packs/Tecno/wall_tile_left.png";
    private const string CaixaSpritePath = "Assets/Images/EditorKit/pecas/peca_base_branca.png";
    private const string LinhaSpritePath = "Assets/Images/EditorKit/cenario/divisoria.png";

    // Mesma escala "mundo" dos textos do layout da Fisica (ex: "Objetivo").
    private static readonly Vector3 EscalaTexto = new Vector3(0.42019f, 0.64645f, 0.64645f);

    private const string Amarelo = "#F5C542";
    private const string Cinza = "#8C9AB5";

    public class PecaDef
    {
        public string key;
        public string rotulo;
        public string rotuloUnity;
        public int id;
        public string descricao;
        public string art;
    }

    public class PuzzleDef
    {
        public string groupName;
        public string controlName;
        public string[] rotulosEncaixes;
        public int[] zoneIds;
        public PecaDef[] pecas;
        public string dica;
        public IAPuzzlePreview.Modo preview;
        public string tituloCodigo;
        public string codigo;
        public bool depuracao;
        public PecaDef[] fixas;          // só depuração: [0] regra errada (encaixe 1), [1..3] regras 2-4
        public string descricaoRegraErrada;
    }

    public class Resultado
    {
        public PuzzleControl estados;
        public PuzzleControl transicoes;
        public PuzzleControl depuracao;
        public IAPecaFixa regraErrada;
    }

    private static List<string> log;

    private static string N(string s) => "<noparse>" + s + "</noparse>";
    private static string C(string s) => $"<color={Amarelo}><noparse>{s}</noparse></color>";
    private static string Com(string s) => $"<color={Cinza}>// {s}</color>";

    public static PuzzleDef[] Definicoes => new[]
    {
        new PuzzleDef
        {
            groupName = "IA_Estados", controlName = "PuzzleControlEstados",
            rotulosEncaixes = new[] { "Patrulhar: ___", "Perseguir: ___", "Atacar: ___" },
            zoneIds = new[] { 1, 2, 3 },
            preview = IAPuzzlePreview.Modo.Estados,
            dica = "Dica: cada estado é uma coisa que o guarda faz. Patrulhar é ir e voltar entre dois pontos, Perseguir é ir atrás de alguém e Atacar é tirar vida.",
            pecas = new[]
            {
                new PecaDef { key = "moverate", rotulo = "MoverAte(alvo)", rotuloUnity = "linearVelocity", id = 2, art = "velocity",
                    descricao = "Anda na direção do jogador: se ele está à direita, velocidade positiva; à esquerda, negativa." },
                new PecaDef { key = "inputgetaxis", rotulo = "Input.GetAxis", rotuloUnity = "", id = 4, art = "input",
                    descricao = "Lê o teclado: devolve -1, 0 ou 1 conforme as setas apertadas." },
                new PecaDef { key = "causardano", rotulo = "CausarDano()", rotuloUnity = "TakeDamage", id = 3, art = "addforce",
                    descricao = "Tira vida do jogador, o mesmo TakeDamage que as armadilhas usam. Com intervalo, pra não tirar tudo de uma vez." },
                new PecaDef { key = "pular", rotulo = "Pular()", rotuloUnity = "linearVelocity.y", id = 5, art = "jump",
                    descricao = "Dá um impulso pra cima, mudando linearVelocity.y." },
                new PecaDef { key = "irentre", rotulo = "IrEntre()", rotuloUnity = "linearVelocity", id = 1, art = "horizontal",
                    descricao = "Anda de um ponto ao outro e volta, usando linearVelocity, igual ao seu personagem. Quando chega num ponto, vira." },
                new PecaDef { key = "destroy", rotulo = "Destroy(alvo)", rotuloUnity = "", id = 6, art = "spriterenderer",
                    descricao = "Apaga o objeto alvo da cena na hora." },
            },
            tituloCodigo = "Código montado · Estados",
            // O mesmo Agir() do GuardaIA.
            codigo =
                "switch (estado)\n" +
                "{\n" +
                "    case Estado.Patrulhar: velocidadeDesejadaX = " + C("IrEntre()") + "; break;\n" +
                "    case Estado.Perseguir: velocidadeDesejadaX = " + C("MoverAte(alvo)") + "; break;\n" +
                "    case Estado.Atacar: velocidadeDesejadaX = 0; " + C("CausarDano()") + "; break;   " + Com("TakeDamage") + "\n" +
                "}\n" +
                "rb.linearVelocity = new Vector2(velocidadeDesejadaX, rb.linearVelocity.y);",
        },
        new PuzzleDef
        {
            groupName = "IA_Transicoes", controlName = "PuzzleControlTransicoes",
            rotulosEncaixes = new[]
            {
                "1  Patrulhar -> Perseguir: ___",
                "2  Perseguir -> Patrulhar: ___",
                "3  Perseguir -> Atacar: ___",
                "4  Atacar -> Perseguir: ___"
            },
            zoneIds = new[] { 1, 2, 3, 4 },
            preview = IAPuzzlePreview.Modo.Transicoes,
            dica = "Dica: cada seta é uma pergunta feita a cada frame. Chegou perto o bastante pra ver? Ficou longe demais pra continuar? Está perto o bastante pra atacar?",
            pecas = new[]
            {
                new PecaDef { key = "dist_maior_raioperder", rotulo = "Distance > raioPerder", rotuloUnity = "Vector2.Distance", id = 2, art = "vector2",
                    descricao = "Verdadeiro quando a distância até o jogador é maior que o raio de perder. O raio de perder é maior que o de visão." },
                new PecaDef { key = "dist_menor_alcance", rotulo = "Distance < alcance", rotuloUnity = "Vector2.Distance", id = 3, art = "vector2",
                    descricao = "Verdadeiro quando a distância até o jogador é menor que o alcance do ataque." },
                new PecaDef { key = "oncollisionenter2d", rotulo = "OnCollisionEnter2D", rotuloUnity = "", id = 5, art = "oncollisionenter2d",
                    descricao = "Método chamado quando dois colliders sólidos se encostam." },
                new PecaDef { key = "dist_maior_alcance", rotulo = "Distance > alcance", rotuloUnity = "Vector2.Distance", id = 4, art = "vector2",
                    descricao = "Verdadeiro quando a distância até o jogador é maior que o alcance do ataque." },
                new PecaDef { key = "start", rotulo = "Start", rotuloUnity = "", id = 6, art = "start",
                    descricao = "Roda uma vez, quando o guarda nasce." },
                new PecaDef { key = "dist_menor_raiovisao", rotulo = "Distance < raioVisao", rotuloUnity = "Vector2.Distance", id = 1, art = "vector2",
                    descricao = "Vector2.Distance mede a distância até o jogador. Verdadeiro quando ela é menor que o raio de visão." },
            },
            tituloCodigo = "Código montado · Transições",
            // O mesmo Decidir() do GuardaIA (d = Vector2.Distance até o alvo).
            codigo =
                "float d = Vector2.Distance(transform.position, alvo.position);\n" +
                "switch (estado)\n" +
                "{\n" +
                "    case Estado.Patrulhar:\n" +
                "        if (" + C("d < raioVisao") + ") MudarEstado(Estado.Perseguir);\n" +
                "        break;\n" +
                "    case Estado.Perseguir:\n" +
                "        if (" + C("d > raioPerder") + ") MudarEstado(Estado.Patrulhar);\n" +
                "        else if (" + C("d < alcance") + ") MudarEstado(Estado.Atacar);\n" +
                "        break;\n" +
                "    case Estado.Atacar:\n" +
                "        if (" + C("d > alcance") + ") MudarEstado(Estado.Perseguir);\n" +
                "        break;\n" +
                "}",
        },
        new PuzzleDef
        {
            groupName = "IA_Depuracao", controlName = "PuzzleControlDepuracao",
            depuracao = true,
            zoneIds = new[] { 1 },
            preview = IAPuzzlePreview.Modo.Raycast,
            dica = "Dica: lembre o que o guarda fez quando você estava atrás da parede. Qual das quatro regras deixou ele te ver? Troque a peça dessa regra.",
            pecas = new[]
            {
                new PecaDef { key = "raycast", rotulo = "Physics2D.Raycast", rotuloUnity = "", id = 1, art = "isgrounded",
                    descricao = "Lança uma linha invisível do guarda até o jogador e diz qual é o primeiro collider que ela acerta no caminho." },
                new PecaDef { key = "ontriggerenter2d", rotulo = "OnTriggerEnter2D", rotuloUnity = "", id = 2, art = "ontriggerenter2d",
                    descricao = "Método chamado quando alguém entra numa área (collider Trigger) em volta do guarda." },
                new PecaDef { key = "raiovisao_maior", rotulo = "raioVisao maior", rotuloUnity = "Vector2.Distance", id = 3, art = "vector2",
                    descricao = "Aumenta o raio de visão: o guarda passa a notar o jogador de mais longe." },
            },
            fixas = new[]
            {
                new PecaDef { key = "fixa_regra1", rotulo = "Distance < raioVisao", rotuloUnity = "Vector2.Distance", art = "vector2",
                    descricao = "Regra 1 (Patrulhar -> Perseguir): o jogador chegou mais perto que o raio de visão." },
                new PecaDef { key = "fixa_regra2", rotulo = "Distance > raioPerder", rotuloUnity = "Vector2.Distance", art = "vector2",
                    descricao = "Regra 2 (Perseguir -> Patrulhar): o jogador se afastou e o guarda desiste." },
                new PecaDef { key = "fixa_regra3", rotulo = "Distance < alcance", rotuloUnity = "Vector2.Distance", art = "vector2",
                    descricao = "Regra 3 (Perseguir -> Atacar): perto o bastante pra atacar." },
                new PecaDef { key = "fixa_regra4", rotulo = "Distance > alcance", rotuloUnity = "Vector2.Distance", art = "vector2",
                    descricao = "Regra 4 (Atacar -> Perseguir): saiu do alcance, volta a perseguir." },
            },
            descricaoRegraErrada = "Regra 1 (Patrulhar -> Perseguir): a distância mede só o espaço entre os dois, sem olhar o que tem no meio.",
            tituloCodigo = "Código montado · Linha de visão",
            // Ve() do GuardaIA no modo Raycast: distância E linha de visão livre.
            codigo =
                "float d = Vector2.Distance(transform.position, alvo.position);\n" +
                "Vector2 direcao = (Vector2)alvo.position - olho;\n" +
                "RaycastHit2D hit = " + C("Physics2D.Raycast") + "(olho, direcao.normalized, direcao.magnitude, obstaculos);\n" +
                "bool ve = " + N("d < raioVisao") + " && hit.collider == null;   " + Com("nada no meio do caminho") + "\n\n" +
                "if (estado == Estado.Patrulhar && ve) MudarEstado(Estado.Perseguir);",
        },
    };

    /// <summary>
    /// Clona o PuzzleRig da Fisica 3 vezes pra dentro de <paramref name="destino"/>,
    /// re-skina e cria os PuzzleControl (com <paramref name="listener"/> no campo listener).
    /// Devolve null se a Fisica ou o PuzzleRig não forem encontrados.
    /// </summary>
    public static Resultado Construir(Scene destino, Vector2 posicaoDosPuzzles, MonoBehaviour listener, List<string> logExterno)
    {
        log = logExterno ?? new List<string>();

        Scene fisica = SceneManager.GetSceneByPath(FisicaScenePath);
        bool jaAberta = fisica.IsValid() && fisica.isLoaded;
        if (jaAberta && fisica.isDirty)
        {
            log.Add("ERRO: Fisica.unity está aberta com alterações não salvas — salve-a antes (os puzzles não foram criados).");
            return null;
        }

        if (!jaAberta)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FisicaScenePath) == null)
            {
                log.Add("ERRO: " + FisicaScenePath + " não existe — os puzzles não foram criados.");
                return null;
            }

            fisica = EditorSceneManager.OpenScene(FisicaScenePath, OpenSceneMode.Additive);
        }

        var resultado = new Resultado();
        try
        {
            GameObject rig = fisica.GetRootGameObjects().FirstOrDefault(g => g.name == "PuzzleRig");
            if (rig == null)
            {
                log.Add("ERRO: 'PuzzleRig' não encontrado na raiz da Fisica — os puzzles não foram criados.");
                return null;
            }

            Vector2 focoFisica = FocoDosPuzzlesNaFisica(fisica);
            Vector3 deslocamento = (Vector3)(posicaoDosPuzzles - focoFisica);
            log.Add($"PuzzleRig da Fisica encontrado (foco da câmera lá: {focoFisica}). Clonando para {posicaoDosPuzzles}.");

            foreach (PuzzleDef def in Definicoes)
            {
                GameObject grupo = Object.Instantiate(rig);
                SceneManager.MoveGameObjectToScene(grupo, destino);
                grupo.name = def.groupName;
                grupo.transform.position = rig.transform.position + deslocamento;

                // TMP só mede texto direito com o objeto ativo.
                grupo.SetActive(true);
                IAPecaFixa errada = Configurar(grupo, def);
                grupo.SetActive(false);

                PuzzleControl control = CriarControle(destino, grupo, def, listener);
                switch (def.preview)
                {
                    case IAPuzzlePreview.Modo.Estados: resultado.estados = control; break;
                    case IAPuzzlePreview.Modo.Transicoes: resultado.transicoes = control; break;
                    default: resultado.depuracao = control; resultado.regraErrada = errada; break;
                }

                log.Add($"{def.groupName}: pronto ({(def.depuracao ? 1 : def.zoneIds.Length)} encaixe(s), {def.pecas.Length} peças, prévia {def.preview}).");
            }
        }
        finally
        {
            if (!jaAberta && fisica.IsValid() && fisica.isLoaded)
            {
                EditorSceneManager.CloseScene(fisica, true);
            }
        }

        return resultado;
    }

    private static Vector2 FocoDosPuzzlesNaFisica(Scene fisica)
    {
        foreach (GameObject root in fisica.GetRootGameObjects())
        {
            GameFlowManager gfm = root.GetComponentInChildren<GameFlowManager>(true);
            if (gfm == null) continue;

            SerializedProperty prop = new SerializedObject(gfm).FindProperty("posicaoDosPuzzles");
            if (prop != null && prop.propertyType == SerializedPropertyType.Vector2)
            {
                return prop.vector2Value;
            }
        }

        return Vector2.zero;
    }

    // ------------------------------------------------------------------
    // Um grupo (clone do PuzzleRig)
    // ------------------------------------------------------------------

    private static IAPecaFixa Configurar(GameObject grupo, PuzzleDef def)
    {
        TMP_Text modeloTexto = AcharModeloDeTexto(grupo);

        ConfigurarCartao(grupo);

        Transform pecas = grupo.transform.Find("Peças");
        GameObject modeloPeca = pecas != null && pecas.childCount > 0 ? pecas.GetChild(0).gameObject : null;

        IAPecaFixa errada = ConfigurarEncaixes(grupo, def, modeloTexto, modeloPeca);
        ConfigurarPecas(pecas, def);
        ConfigurarDescricao(grupo, def);
        ConstruirPrevia(grupo, def, modeloTexto);
        return errada;
    }

    private static TMP_Text AcharModeloDeTexto(GameObject grupo)
    {
        Transform previa = grupo.transform.Find("CenarioPuzzle/Painel_Previa");
        TMP_Text modelo = previa != null ? previa.GetComponentInChildren<TMP_Text>(true) : null;
        if (modelo == null)
        {
            modelo = grupo.GetComponentInChildren<TMP_Text>(true);
        }

        if (modelo == null)
        {
            log.Add("AVISO: nenhum texto TMP encontrado no PuzzleRig pra servir de modelo.");
        }

        return modelo;
    }

    // Cartão "Player" → "Cérebro do Guarda", com o guarda no lugar do ícone do player.
    private static void ConfigurarCartao(GameObject grupo)
    {
        Transform cartao = grupo.transform.Find("CenarioPuzzle/Card Encaixe");
        if (cartao == null)
        {
            log.Add("AVISO: 'CenarioPuzzle/Card Encaixe' não encontrado — cartão não re-skinado.");
            return;
        }

        foreach (Transform filho in cartao)
        {
            TMP_Text t = filho.GetComponent<TMP_Text>();
            if (t != null)
            {
                t.text = "Cérebro do Guarda";
                t.textWrappingMode = TextWrappingModes.NoWrap;
                AjustarFonte(t, t.rectTransform.rect.size, Mathf.Max(24f, t.fontSize));
            }
        }

        Transform icone = cartao.Find("player_parado");
        Sprite guarda = LoadSprite(GuardaSpritePath);
        if (icone != null && guarda != null)
        {
            SpriteRenderer sr = icone.GetComponent<SpriteRenderer>();
            Vector2 antes = sr.bounds.size;
            sr.sprite = guarda;
            Vector2 depois = sr.bounds.size;
            if (depois.x > 0.0001f && depois.y > 0.0001f)
            {
                Vector3 s = icone.localScale;
                icone.localScale = new Vector3(s.x * antes.x / depois.x, s.y * antes.y / depois.y, s.z);
            }
            icone.name = "guarda_parado";
        }
    }

    private static IAPecaFixa ConfigurarEncaixes(GameObject grupo, PuzzleDef def, TMP_Text modeloTexto, GameObject modeloPeca)
    {
        Transform encaixar = grupo.transform.Find("Encaixar");
        if (encaixar == null)
        {
            log.Add($"ERRO: {def.groupName} não tem 'Encaixar'.");
            return null;
        }

        List<SnapZone> zonas = encaixar.GetComponentsInChildren<SnapZone>(true)
            .OrderBy(z => z.transform.GetSiblingIndex())
            .ToList();

        int quantidade = def.depuracao ? 4 : def.zoneIds.Length;
        if (zonas.Count < quantidade)
        {
            log.Add($"AVISO: {def.groupName} tem {zonas.Count} encaixes no layout, precisa de {quantidade}.");
        }

        for (int i = zonas.Count - 1; i >= quantidade; i--)
        {
            Object.DestroyImmediate(zonas[i].gameObject);
            zonas.RemoveAt(i);
        }

        IAPecaFixa errada = null;

        for (int i = 0; i < zonas.Count; i++)
        {
            SnapZone zona = zonas[i];

            // Idempotente: tira qualquer "DicaCodigo" que tenha vindo no clone.
            for (int c = zona.transform.childCount - 1; c >= 0; c--)
            {
                Transform filho = zona.transform.GetChild(c);
                if (filho.name == "DicaCodigo") Object.DestroyImmediate(filho.gameObject);
            }

            SpriteRenderer visual = zona.GetComponentInChildren<SpriteRenderer>(true);

            if (!def.depuracao)
            {
                SetInt(zona, "correctPieceID", def.zoneIds[i]);
                CriarDicaCodigo(zona.transform, visual, modeloTexto, def.rotulosEncaixes[i]);
                continue;
            }

            if (modeloPeca == null || visual == null || def.fixas == null || i >= def.fixas.Length)
            {
                log.Add($"AVISO: {def.groupName}: não deu pra montar a regra fixa {i + 1}.");
                continue;
            }

            if (i == 0)
            {
                // Regra 1: continua sendo um encaixe (Physics2D.Raycast entra
                // aqui); a peça errada fica dentro dele como "DicaCodigo" e
                // some quando a certa encaixa.
                SetInt(zona, "correctPieceID", def.zoneIds[0]);
                errada = CriarPecaFixa(modeloPeca, zona.transform, visual, def.fixas[0], "DicaCodigo", def.descricaoRegraErrada);
            }
            else
            {
                // Regras 2-4: já montadas, sem encaixe.
                GameObject go = zona.gameObject;
                Object.DestroyImmediate(zona);
                BoxCollider2D gatilho = go.GetComponent<BoxCollider2D>();
                if (gatilho != null) Object.DestroyImmediate(gatilho);
                go.name = "RegraFixa" + (i + 1);
                CriarPecaFixa(modeloPeca, go.transform, visual, def.fixas[i], "Peca_" + def.fixas[i].key, null);
            }
        }

        return errada;
    }

    private static void CriarDicaCodigo(Transform zona, SpriteRenderer visual, TMP_Text modelo, string texto)
    {
        if (modelo == null || visual == null)
        {
            return;
        }

        Bounds b = visual.bounds;
        TMP_Text t = ClonarTexto(modelo, zona, "DicaCodigo", texto, b.center, new Vector2(b.size.x * 0.92f, b.size.y * 0.78f), 30f);
        t.color = new Color(0.62f, 0.68f, 0.8f, 1f);
        t.fontStyle = FontStyles.Italic;
    }

    private static void ConfigurarPecas(Transform pecas, PuzzleDef def)
    {
        if (pecas == null)
        {
            log.Add($"ERRO: {def.groupName} não tem 'Peças'.");
            return;
        }

        int total = pecas.childCount;
        if (total < def.pecas.Length)
        {
            log.Add($"AVISO: {def.groupName}: o layout tem {total} peças, a definição pede {def.pecas.Length}.");
        }

        for (int i = total - 1; i >= 0; i--)
        {
            Transform peca = pecas.GetChild(i);
            if (i >= def.pecas.Length)
            {
                Object.DestroyImmediate(peca.gameObject);
                continue;
            }

            PecaDef p = def.pecas[i];
            peca.name = "peca_" + p.key;

            DragPiece drag = peca.GetComponent<DragPiece>();
            if (drag != null) SetInt(drag, "pieceID", p.id);

            Sprite icone = ReskinPeca(peca.gameObject, p);

            PieceHoverInfo hover = peca.GetComponent<PieceHoverInfo>();
            if (hover != null)
            {
                var so = new SerializedObject(hover);
                so.FindProperty("title").stringValue = Titulo(p);
                so.FindProperty("description").stringValue = p.descricao;
                so.FindProperty("icon").objectReferenceValue = icone;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    private static string Titulo(PecaDef p)
    {
        string t = string.IsNullOrEmpty(p.rotuloUnity) ? p.rotulo : $"{p.rotulo}  ({p.rotuloUnity})";
        return N(t);
    }

    // Troca sprite da peça, ícone e rótulo (com os dois nomes).
    private static Sprite ReskinPeca(GameObject peca, PecaDef p)
    {
        SpriteRenderer fundo = null, icone = null;
        TMP_Text rotulo = null;
        foreach (Transform filho in peca.transform)
        {
            SpriteRenderer sr = filho.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (filho.name.StartsWith("icone")) icone = sr;
                else fundo = sr;
            }

            TMP_Text tmp = filho.GetComponent<TMP_Text>();
            if (tmp != null) rotulo = tmp;
        }

        Sprite spritePeca = LoadSprite(PecasFolder + "peca_" + p.art + ".png");
        Sprite spriteIcone = LoadSprite(IconesFolder + "icone_" + p.art + ".png");
        if (fundo != null && spritePeca != null) fundo.sprite = spritePeca;
        if (icone != null)
        {
            if (spriteIcone != null) icone.sprite = spriteIcone;
            icone.gameObject.name = "icone_" + p.art;
        }

        if (rotulo != null)
        {
            string principal = N(p.rotulo);
            rotulo.richText = true;
            rotulo.text = string.IsNullOrEmpty(p.rotuloUnity)
                ? principal
                : principal + "\n<size=62%><color=#D5DCEA>" + p.rotuloUnity + "</color></size>";
            float max = Mathf.Max(4f, rotulo.fontSize);
            rotulo.enableAutoSizing = true;
            rotulo.fontSizeMax = Mathf.Min(8f, max);
            rotulo.fontSizeMin = 2.5f;
            rotulo.textWrappingMode = TextWrappingModes.NoWrap;
            rotulo.ForceMeshUpdate();
        }

        return spriteIcone;
    }

    // Peça "já encaixada" (sem DragPiece): cópia de uma peça, ajustada ao encaixe.
    private static IAPecaFixa CriarPecaFixa(GameObject modelo, Transform pai, SpriteRenderer visualEncaixe, PecaDef p, string nome, string descricaoDestacada)
    {
        GameObject chip = Object.Instantiate(modelo);
        SceneManager.MoveGameObjectToScene(chip, pai.gameObject.scene);
        chip.name = nome;
        chip.transform.SetParent(pai, true);
        chip.transform.rotation = Quaternion.identity;

        PieceHoverInfo hover = chip.GetComponent<PieceHoverInfo>();
        if (hover != null) Object.DestroyImmediate(hover);
        DragPiece drag = chip.GetComponent<DragPiece>();
        if (drag != null) Object.DestroyImmediate(drag);
        Rigidbody2D rb = chip.GetComponent<Rigidbody2D>();
        if (rb != null) Object.DestroyImmediate(rb);

        Sprite icone = ReskinPeca(chip, p);

        SpriteRenderer fundo = null;
        foreach (SpriteRenderer sr in chip.GetComponentsInChildren<SpriteRenderer>(true))
        {
            // Fica um pouco "atrás" das peças arrastáveis (que usam 6/7).
            sr.sortingOrder = Mathf.Max(3, sr.sortingOrder - 2);
            if (fundo == null && !sr.gameObject.name.StartsWith("icone")) fundo = sr;
        }

        if (fundo != null)
        {
            Bounds alvo = visualEncaixe.bounds;
            Vector2 atual = fundo.bounds.size;
            if (atual.x > 0.0001f && atual.y > 0.0001f)
            {
                Vector3 lossy = chip.transform.lossyScale;
                SetLossyScale(chip.transform, new Vector3(lossy.x * alvo.size.x / atual.x, lossy.y * alvo.size.y / atual.y, lossy.z));
            }

            Vector3 delta = alvo.center - fundo.bounds.center;
            delta.z = 0f;
            chip.transform.position += delta;

            // Collider do hover alinhado ao que aparece na tela (sólido: o
            // hover do Unity ignora triggers com queriesHitTriggers desligado).
            BoxCollider2D box = chip.GetComponent<BoxCollider2D>();
            if (box == null) box = chip.AddComponent<BoxCollider2D>();
            box.isTrigger = false;
            box.offset = chip.transform.InverseTransformPoint(fundo.bounds.center);
            Vector3 escala = chip.transform.lossyScale;
            Vector2 tamanho = fundo.bounds.size;
            box.size = new Vector2(
                Mathf.Abs(escala.x) > 0.0001f ? tamanho.x / Mathf.Abs(escala.x) : tamanho.x,
                Mathf.Abs(escala.y) > 0.0001f ? tamanho.y / Mathf.Abs(escala.y) : tamanho.y);
        }

        IAPecaFixa fixa = chip.AddComponent<IAPecaFixa>();
        var so = new SerializedObject(fixa);
        so.FindProperty("titulo").stringValue = Titulo(p);
        so.FindProperty("descricao").stringValue = p.descricao;
        so.FindProperty("descricaoDestacada").stringValue = descricaoDestacada ?? "";
        so.FindProperty("icone").objectReferenceValue = icone;
        so.FindProperty("fundo").objectReferenceValue = fundo;
        so.ApplyModifiedPropertiesWithoutUndo();
        return fixa;
    }

    // Painel "Descrição": mesmo esquema do FisicaPuzzleLayoutBuilder.
    private static void ConfigurarDescricao(GameObject grupo, PuzzleDef def)
    {
        Transform descricao = grupo.transform.Find("CenarioPuzzle/Descrição");
        if (descricao == null)
        {
            log.Add($"AVISO: {def.groupName} não tem 'CenarioPuzzle/Descrição'.");
            return;
        }

        Transform textos = descricao.Find("Text");
        TMP_Text titulo = textos != null && textos.childCount > 0 ? textos.GetChild(0).GetComponent<TMP_Text>() : null;
        TMP_Text corpo = textos != null && textos.childCount > 1 ? textos.GetChild(1).GetComponent<TMP_Text>() : null;

        SpriteRenderer icone = null;
        foreach (Transform filho in descricao)
        {
            if (filho.name.StartsWith("icone"))
            {
                icone = filho.GetComponent<SpriteRenderer>();
                break;
            }
        }

        InfoPanelUI painel = descricao.GetComponent<InfoPanelUI>();
        if (painel == null) painel = descricao.gameObject.AddComponent<InfoPanelUI>();

        Sprite iconeDica = LoadSprite(HintIconPath);
        var so = new SerializedObject(painel);
        so.FindProperty("externalTitle").objectReferenceValue = titulo;
        so.FindProperty("externalDescription").objectReferenceValue = corpo;
        so.FindProperty("externalIcon").objectReferenceValue = icone;
        so.FindProperty("defaultIcon").objectReferenceValue = iconeDica;
        so.FindProperty("defaultTitle").stringValue = "Dica";
        so.FindProperty("defaultDescription").stringValue = def.dica;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (titulo != null) titulo.text = "Dica";
        if (corpo != null) corpo.text = def.dica;
        if (icone != null)
        {
            icone.sprite = iconeDica;
            icone.enabled = iconeDica != null;
        }
    }

    // ------------------------------------------------------------------
    // Prévia (painel_previa): mini-guarda, boneco e caixas de estado
    // ------------------------------------------------------------------

    private static void ConstruirPrevia(GameObject grupo, PuzzleDef def, TMP_Text modeloTexto)
    {
        Transform painel = grupo.transform.Find("CenarioPuzzle/Painel_Previa");
        Transform janela = painel != null ? painel.Find("janela_previa") : null;
        SpriteRenderer janelaSr = janela != null ? janela.GetComponent<SpriteRenderer>() : null;
        if (janelaSr == null)
        {
            log.Add($"AVISO: {def.groupName} sem 'Painel_Previa/janela_previa' — prévia não criada.");
            return;
        }

        PuzzlePreview antiga = painel.GetComponent<PuzzlePreview>();
        if (antiga != null) Object.DestroyImmediate(antiga);

        SpriteMask mascara = janela.GetComponent<SpriteMask>();
        if (mascara == null) mascara = janela.gameObject.AddComponent<SpriteMask>();
        mascara.sprite = janelaSr.sprite;

        Transform velha = painel.Find("Preview");
        if (velha != null) Object.DestroyImmediate(velha.gameObject);

        var containerGO = new GameObject("Preview");
        containerGO.transform.SetParent(painel, false);
        containerGO.transform.localPosition = Vector3.zero;
        Transform container = containerGO.transform;

        Bounds b = janelaSr.bounds;
        Sprite caixaSprite = LoadSprite(CaixaSpritePath);
        Sprite chaoSprite = LoadSprite(GroundSpritePath);
        Sprite paredeSprite = LoadSprite(WallSpritePath);
        Sprite guardaSprite = LoadSprite(GuardaSpritePath);
        Sprite bonecoSprite = LoadSprite(PlayerSpritePath);
        Sprite linhaSprite = LoadSprite(LinhaSpritePath);

        // Caixas de estado, logo abaixo do título "Objetivo".
        Transform divisoria = painel.Find("divisoria");
        float yCaixas = divisoria != null ? divisoria.position.y - 0.45f : b.max.y - 0.95f;
        float larguraCaixa = (b.size.x - 0.5f) / 3f;
        string[] nomes = { "Patrulhar", "Perseguir", "Atacar" };
        var caixas = new SpriteRenderer[3];
        var textos = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            Vector3 pos = new Vector3(b.center.x + (i - 1) * (larguraCaixa + 0.08f), yCaixas, 0f);
            caixas[i] = CriarSprite(container, "Estado_" + nomes[i], caixaSprite, pos, new Vector2(larguraCaixa, 0.42f), janelaSr, 3, false);
            if (modeloTexto != null)
            {
                textos[i] = ClonarTexto(modeloTexto, container, "Texto_" + nomes[i], nomes[i], pos, new Vector2(larguraCaixa * 0.9f, 0.36f), 20f);
            }
        }

        // Mini-cena.
        const float ator = 0.45f;
        float chaoTopo = b.min.y + 1.0f;
        float atorY = chaoTopo + ator * 0.5f;
        float xMin = b.min.x + 0.35f;
        float xMax = b.max.x - 0.35f;

        for (float x = b.min.x + 0.2f; x < b.max.x; x += 0.4f)
        {
            CriarSprite(container, "Chao", chaoSprite, new Vector3(x, chaoTopo - 0.4f, 0f), new Vector2(0.4f, 0.8f), janelaSr, 2, true);
            CriarSprite(container, "Chao", chaoSprite, new Vector3(x, chaoTopo - 1.2f, 0f), new Vector2(0.4f, 0.8f), janelaSr, 2, true);
        }

        SpriteRenderer parede = null;
        SpriteRenderer linha = null;
        if (def.preview == IAPuzzlePreview.Modo.Raycast)
        {
            float xParede = xMin + 0.66f * (xMax - xMin);
            parede = CriarSprite(container, "Parede", paredeSprite, new Vector3(xParede, chaoTopo + 0.3f, 0f), new Vector2(0.3f, 0.6f), janelaSr, 3, true);
            linha = CriarSprite(container, "LinhaDeVisao", linhaSprite, new Vector3(xMin, atorY, 0f), new Vector2(1f, 0.04f), janelaSr, 5, true);
        }

        SpriteRenderer guarda = CriarSprite(container, "Guarda", guardaSprite, new Vector3(xMin + 0.3f, atorY, 0f), new Vector2(ator, ator), janelaSr, 4, true);
        SpriteRenderer boneco = CriarSprite(container, "Boneco", bonecoSprite, new Vector3(xMax - 0.2f, atorY, 0f), new Vector2(ator, ator), janelaSr, 4, true);

        TMP_Text icone = null;
        if (modeloTexto != null)
        {
            icone = ClonarTexto(modeloTexto, container, "Icone", "?", guarda.transform.position + Vector3.up * 0.42f, new Vector2(0.8f, 0.4f), 24f);
            icone.fontStyle = FontStyles.Bold;
        }

        IAPuzzlePreview previa = painel.GetComponent<IAPuzzlePreview>();
        if (previa == null) previa = painel.gameObject.AddComponent<IAPuzzlePreview>();

        var so = new SerializedObject(previa);
        so.FindProperty("modo").enumValueIndex = (int)def.preview;
        so.FindProperty("guarda").objectReferenceValue = guarda.transform;
        so.FindProperty("boneco").objectReferenceValue = boneco.transform;
        so.FindProperty("icone").objectReferenceValue = icone;
        so.FindProperty("parede").objectReferenceValue = parede;
        so.FindProperty("linhaVisao").objectReferenceValue = linha;

        SerializedProperty caixasProp = so.FindProperty("caixas");
        SerializedProperty textosProp = so.FindProperty("textosCaixas");
        caixasProp.arraySize = 3;
        textosProp.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            caixasProp.GetArrayElementAtIndex(i).objectReferenceValue = caixas[i];
            textosProp.GetArrayElementAtIndex(i).objectReferenceValue = textos[i];
        }

        so.FindProperty("xMin").floatValue = container.InverseTransformPoint(new Vector3(xMin, 0f, 0f)).x;
        so.FindProperty("xMax").floatValue = container.InverseTransformPoint(new Vector3(xMax, 0f, 0f)).x;
        so.FindProperty("chaoY").floatValue = container.InverseTransformPoint(new Vector3(0f, atorY, 0f)).y;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Sprite com tamanho final em unidades de MUNDO (independe do ppu da arte).
    private static SpriteRenderer CriarSprite(Transform pai, string nome, Sprite sprite, Vector3 posicaoMundo, Vector2 tamanhoMundo, SpriteRenderer referenciaSorting, int ordem, bool mascarado)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        go.transform.position = posicaoMundo;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerID = referenciaSorting.sortingLayerID;
        sr.sortingOrder = ordem;
        sr.maskInteraction = mascarado ? SpriteMaskInteraction.VisibleInsideMask : SpriteMaskInteraction.None;

        Vector2 tamanhoSprite = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
        Vector3 pl = pai.lossyScale;
        go.transform.localScale = new Vector3(
            tamanhoMundo.x / Mathf.Max(0.0001f, tamanhoSprite.x * Mathf.Abs(pl.x)),
            tamanhoMundo.y / Mathf.Max(0.0001f, tamanhoSprite.y * Mathf.Abs(pl.y)),
            1f);
        return sr;
    }

    // Copia um texto TMP do layout (mesma fonte/material/sorting) com outro conteúdo.
    private static TMP_Text ClonarTexto(TMP_Text modelo, Transform pai, string nome, string texto, Vector3 centroMundo, Vector2 tamanhoMundo, float tamanhoFonteMax)
    {
        GameObject go = Object.Instantiate(modelo.gameObject);
        SceneManager.MoveGameObjectToScene(go, pai.gameObject.scene);
        go.name = nome;
        go.transform.SetParent(pai, false);
        for (int i = go.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        }

        SetLossyScale(go.transform, EscalaTexto);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(tamanhoMundo.x / EscalaTexto.x, tamanhoMundo.y / EscalaTexto.y);
        rt.position = new Vector3(centroMundo.x, centroMundo.y, modelo.transform.position.z);
        rt.localRotation = Quaternion.identity;

        TMP_Text t = go.GetComponent<TMP_Text>();
        t.richText = false;
        t.text = texto;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        AjustarFonte(t, rt.rect.size, tamanhoFonteMax);
        return t;
    }

    // O auto-size do TMP cai no tamanho mínimo com estes textos clonados
    // (escala herdada desigual), então a fonte é calculada aqui: começa no
    // máximo e encolhe só o necessário pra caber no retângulo.
    private static void AjustarFonte(TMP_Text t, Vector2 tamanhoRect, float maximo)
    {
        t.enableAutoSizing = false;
        t.fontSize = maximo;
        Vector2 pref = t.GetPreferredValues(t.text);
        float k = 1f;
        if (pref.x > 0.0001f) k = Mathf.Min(k, tamanhoRect.x / pref.x);
        if (pref.y > 0.0001f) k = Mathf.Min(k, tamanhoRect.y / pref.y);
        t.fontSize = Mathf.Max(1f, maximo * k * 0.95f);
    }

    private static void SetLossyScale(Transform t, Vector3 desejada)
    {
        Vector3 pai = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(
            desejada.x / (Mathf.Abs(pai.x) > 0.0001f ? pai.x : 1f),
            desejada.y / (Mathf.Abs(pai.y) > 0.0001f ? pai.y : 1f),
            desejada.z / (Mathf.Abs(pai.z) > 0.0001f ? pai.z : 1f));
    }

    // ------------------------------------------------------------------
    // PuzzleControl
    // ------------------------------------------------------------------

    private static PuzzleControl CriarControle(Scene destino, GameObject grupo, PuzzleDef def, MonoBehaviour listener)
    {
        var go = new GameObject(def.controlName);
        SceneManager.MoveGameObjectToScene(go, destino);
        go.transform.position = grupo.transform.position;

        PuzzleControl control = go.AddComponent<PuzzleControl>();
        var so = new SerializedObject(control);
        so.FindProperty("puzzle").objectReferenceValue = grupo;
        so.FindProperty("totalPieces").intValue = def.depuracao ? 1 : def.zoneIds.Length;
        so.FindProperty("flowManager").objectReferenceValue = null;

        SerializedProperty listenerProp = so.FindProperty("listener");
        if (listenerProp != null)
        {
            listenerProp.objectReferenceValue = listener;
        }
        else
        {
            log.Add("ERRO: PuzzleControl não tem o campo 'listener' — o IAFlow não vai ser avisado ao fim dos puzzles.");
        }

        SetString(so, "tituloCodigo", def.tituloCodigo);
        SetString(so, "codigoMontado", def.codigo);
        SerializedProperty segundos = so.FindProperty("segundosCodigo");
        if (segundos != null) segundos.floatValue = 8f;
        so.ApplyModifiedPropertiesWithoutUndo();
        return control;
    }

    // ------------------------------------------------------------------
    // Utilitários
    // ------------------------------------------------------------------

    private static void SetInt(Object alvo, string campo, int valor)
    {
        var so = new SerializedObject(alvo);
        SerializedProperty p = so.FindProperty(campo);
        if (p == null)
        {
            log.Add($"AVISO: campo '{campo}' não encontrado em {alvo.name}.");
            return;
        }

        p.intValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetString(SerializedObject so, string campo, string valor)
    {
        SerializedProperty p = so.FindProperty(campo);
        if (p != null) p.stringValue = valor ?? "";
    }

    public static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        if (sprite == null && log != null) log.Add("AVISO: sprite não encontrado em " + path);
        return sprite;
    }
}
