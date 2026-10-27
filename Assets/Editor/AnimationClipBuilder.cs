using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Monta os AnimationClips "Corrida"/"Pulo"/"Idle" e o AnimatorController do
/// Player da fase "Animacao". Usa Animator (Mecanim), não o componente
/// Animation clássico — troca de sprite via curva de Object Reference não é
/// avaliada de forma confiável pelo Animation clássico no Unity atual (o
/// clipe "toca" e IsPlaying fica true, mas o sprite nunca troca na tela).
///
/// Versão Animator da fase: o controller tem os parâmetros e as transições
/// que o jogador monta no puzzle (ver AnimatorPuzzleUI), com os MESMOS nomes
/// que o AnimacaoPlayerFisica atualiza todo quadro (constantes lidas de lá):
/// - velocidadeX (float) = |linearVelocity.x|, noChao (bool) = isGrounded
/// - Idle -> Corrida: velocidadeX &gt; 0.1
/// - Corrida -> Idle: velocidadeX &lt; 0.1
/// - Any State -> Pulo: noChao == false (sem transição pra si mesmo)
/// - Pulo -> Idle: noChao == true
/// Toda transição também exige regrasMontadas == true: é o "interruptor"
/// que deixa o personagem congelado no Idle antes do puzzle (a falha
/// proposital do começo da fase). Transições sem exit time e com duração 0
/// (animação de sprite não mistura quadros).
///
/// Uso: Tools > Animação > Montar Clipes de Animação (o "Montar Cena de
/// Animação" também chama isto). Idempotente: reaproveita clipes e estados
/// existentes, garante os parâmetros e recria as transições do zero.
/// </summary>
public static class AnimationClipBuilder
{
    private const string RunFolder = "Assets/Anim/Player/Run";
    private const string JumpFolder = "Assets/Anim/Player/Jump";
    // Clipes do player com visor (Assets/Anim/Player/Visor). Se algum sumir,
    // GetOrCreateClip gera de novo a partir dos quadros antigos (Run/Jump).
    private const string RunClipPath = "Assets/Anim/Player/Visor/Visor_Correr.anim";
    private const string JumpClipPath = "Assets/Anim/Player/Visor/Visor_Pular.anim";
    private const string IdleClipPath = "Assets/Anim/Player/Visor/Visor_Parado.anim";
    private const string CrouchClipPath = "Assets/Anim/Player/Visor/Visor_Agachar.anim";
    private const string CrouchWalkClipPath = "Assets/Anim/Player/Visor/Visor_AndarAgachado.anim";
    private const string ControllerPath = "Assets/Anim/Player/AnimacaoController.controller";

    public const string IdleStateName = "Idle";
    public const string RunStateName = "Corrida";
    public const string JumpStateName = "Pulo";

    [MenuItem("Tools/Animação/Montar Clipes de Animação")]
    public static void BuildClipsMenuItem()
    {
        AnimatorController controller = BuildAll(out _, out _, out _);
        if (controller == null)
        {
            Debug.LogError("[AnimationClipBuilder] Não consegui montar os clipes/controller — confira os avisos acima.");
            return;
        }

        Debug.Log($"[AnimationClipBuilder] Prontos: {IdleClipPath}, {RunClipPath}, {JumpClipPath} e {ControllerPath}.");
    }

    /// <summary>Monta (ou reaproveita/completa) os 3 clipes e o AnimatorController. Retorna null em caso de erro.</summary>
    public static AnimatorController BuildAll(out AnimationClip idleClip, out AnimationClip runClip, out AnimationClip jumpClip)
    {
        runClip = GetOrCreateClip(RunClipPath, RunFolder, "player_run_dust", frameRate: 10f, loop: true);
        jumpClip = GetOrCreateClip(JumpClipPath, JumpFolder, "player_jump", frameRate: 10f, loop: false);
        idleClip = GetOrCreateIdleClip(runClip);

        if (runClip == null || jumpClip == null || idleClip == null)
        {
            return null;
        }

        AnimatorController controller = GetOrCreateController(idleClip, runClip, jumpClip);
        AddCrouchStates(controller);
        return controller;
    }

    // Agachar (PlayerAgachar) fica fora do puzzle: estados e setas extras que
    // o jogador não monta, mas que também exigem regrasMontadas.
    // Idle <-> Agachado e Corrida <-> AndarAgachado pelo parâmetro "agachado";
    // Agachado <-> AndarAgachado pela velocidadeX.
    private static void AddCrouchStates(AnimatorController controller)
    {
        AnimationClip crouchClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(CrouchClipPath);
        AnimationClip crouchWalkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(CrouchWalkClipPath);
        if (controller == null || crouchClip == null || crouchWalkClip == null)
        {
            Debug.LogWarning("[AnimationClipBuilder] Sem os clipes de agachar em Assets/Anim/Player/Visor — o controller fica sem Agachado.");
            return;
        }

        EnsureParameter(controller, AnimacaoPlayerFisica.ParamAgachado, AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState idle = FindOrAddState(sm, IdleStateName, AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath));
        AnimatorState run = FindOrAddState(sm, RunStateName, AssetDatabase.LoadAssetAtPath<AnimationClip>(RunClipPath));
        AnimatorState crouch = FindOrAddState(sm, AnimacaoPlayerFisica.EstadoAgachado, crouchClip);
        AnimatorState crouchWalk = FindOrAddState(sm, AnimacaoPlayerFisica.EstadoAndarAgachado, crouchWalkClip);

        const float limiar = AnimacaoPlayerFisica.LimiarParado;
        string ag = AnimacaoPlayerFisica.ParamAgachado;
        string vx = AnimacaoPlayerFisica.ParamVelocidadeX;

        AnimatorStateTransition t;
        t = idle.AddTransition(crouch); ConfigureTransition(t); t.AddCondition(AnimatorConditionMode.If, 0f, ag);
        t = run.AddTransition(crouchWalk); ConfigureTransition(t); t.AddCondition(AnimatorConditionMode.If, 0f, ag);
        t = crouch.AddTransition(idle); ConfigureTransition(t); t.AddCondition(AnimatorConditionMode.IfNot, 0f, ag);
        t = crouchWalk.AddTransition(run); ConfigureTransition(t); t.AddCondition(AnimatorConditionMode.IfNot, 0f, ag);
        t = crouch.AddTransition(crouchWalk); ConfigureTransition(t); t.AddCondition(AnimatorConditionMode.Greater, limiar, vx);
        t = crouchWalk.AddTransition(crouch); ConfigureTransition(t); t.AddCondition(AnimatorConditionMode.Less, limiar, vx);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    // Reaproveita o Idle.anim já existente (criado à mão, se for o caso);
    // se não existir, gera um de 1 frame parado usando o primeiro frame da
    // Corrida como pose — melhor que não ter Idle nenhum.
    private static AnimationClip GetOrCreateIdleClip(AnimationClip runClipForFallback)
    {
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
        if (existing != null)
        {
            if (!existing.legacy)
            {
                return existing;
            }

            AssetDatabase.DeleteAsset(IdleClipPath);
        }

        Sprite fallbackFrame = GetFirstSpriteFrame(runClipForFallback);
        if (fallbackFrame == null)
        {
            Debug.LogError($"[AnimationClipBuilder] Não encontrei {IdleClipPath} nem consegui gerar um substituto (Corrida sem frames).");
            return null;
        }

        var clip = new AnimationClip { frameRate = 10f };
        var binding = new EditorCurveBinding { type = typeof(SpriteRenderer), path = "", propertyName = "m_Sprite" };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, new[] { new ObjectReferenceKeyframe { time = 0f, value = fallbackFrame } });

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.CreateAsset(clip, IdleClipPath);
        AssetDatabase.SaveAssets();

        return clip;
    }

    private static Sprite GetFirstSpriteFrame(AnimationClip clip)
    {
        if (clip == null)
        {
            return null;
        }

        var binding = new EditorCurveBinding { type = typeof(SpriteRenderer), path = "", propertyName = "m_Sprite" };
        ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
        return keyframes != null && keyframes.Length > 0 ? keyframes[0].value as Sprite : null;
    }

    private static AnimatorController GetOrCreateController(AnimationClip idleClip, AnimationClip runClip, AnimationClip jumpClip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

        AnimatorState idleState = FindOrAddState(stateMachine, IdleStateName, idleClip);
        AnimatorState runState = FindOrAddState(stateMachine, RunStateName, runClip);
        AnimatorState jumpState = FindOrAddState(stateMachine, JumpStateName, jumpClip);

        // O personagem sempre começa parado.
        stateMachine.defaultState = idleState;

        EnsureParameters(controller);
        RebuildTransitions(stateMachine, idleState, runState, jumpState);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        return controller;
    }

    // Garante os 3 parâmetros com o tipo certo. noChao começa true (o
    // personagem nasce no chão) e regrasMontadas começa false (sem regras
    // até o puzzle).
    private static void EnsureParameters(AnimatorController controller)
    {
        EnsureParameter(controller, AnimacaoPlayerFisica.ParamVelocidadeX, AnimatorControllerParameterType.Float);
        EnsureParameter(controller, AnimacaoPlayerFisica.ParamNoChao, AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, AnimacaoPlayerFisica.ParamRegrasMontadas, AnimatorControllerParameterType.Bool);

        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == AnimacaoPlayerFisica.ParamNoChao)
            {
                parameters[i].defaultBool = true;
            }
            else if (parameters[i].name == AnimacaoPlayerFisica.ParamRegrasMontadas)
            {
                parameters[i].defaultBool = false;
            }
            else if (parameters[i].name == AnimacaoPlayerFisica.ParamVelocidadeX)
            {
                parameters[i].defaultFloat = 0f;
            }
        }
        controller.parameters = parameters;
    }

    private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter p in controller.parameters)
        {
            if (p.name != name)
            {
                continue;
            }

            if (p.type == type)
            {
                return;
            }

            // Mesmo nome com tipo errado (edição manual): recria.
            controller.RemoveParameter(p);
            break;
        }

        controller.AddParameter(name, type);
    }

    // Apaga todas as transições (dos estados e do Any State) e recria as 4
    // do puzzle — assim rodar de novo nunca duplica seta.
    private static void RebuildTransitions(AnimatorStateMachine stateMachine, AnimatorState idle, AnimatorState run, AnimatorState jump)
    {
        foreach (ChildAnimatorState child in stateMachine.states)
        {
            foreach (AnimatorStateTransition t in child.state.transitions.ToArray())
            {
                child.state.RemoveTransition(t);
            }
        }

        foreach (AnimatorStateTransition t in stateMachine.anyStateTransitions.ToArray())
        {
            stateMachine.RemoveAnyStateTransition(t);
        }

        const float limiar = AnimacaoPlayerFisica.LimiarParado;

        AnimatorStateTransition idleToRun = idle.AddTransition(run);
        ConfigureTransition(idleToRun);
        idleToRun.AddCondition(AnimatorConditionMode.Greater, limiar, AnimacaoPlayerFisica.ParamVelocidadeX);

        AnimatorStateTransition runToIdle = run.AddTransition(idle);
        ConfigureTransition(runToIdle);
        runToIdle.AddCondition(AnimatorConditionMode.Less, limiar, AnimacaoPlayerFisica.ParamVelocidadeX);

        AnimatorStateTransition anyToJump = stateMachine.AddAnyStateTransition(jump);
        ConfigureTransition(anyToJump);
        anyToJump.canTransitionToSelf = false;
        anyToJump.AddCondition(AnimatorConditionMode.IfNot, 0f, AnimacaoPlayerFisica.ParamNoChao);

        AnimatorStateTransition jumpToIdle = jump.AddTransition(idle);
        ConfigureTransition(jumpToIdle);
        jumpToIdle.AddCondition(AnimatorConditionMode.If, 0f, AnimacaoPlayerFisica.ParamNoChao);
    }

    private static void ConfigureTransition(AnimatorStateTransition t)
    {
        t.hasExitTime = false;
        t.exitTime = 0f;
        t.hasFixedDuration = true;
        t.duration = 0f;
        t.offset = 0f;
        t.interruptionSource = TransitionInterruptionSource.None;
        // O "interruptor" das regras: sem o puzzle montado, nenhuma seta dispara.
        t.AddCondition(AnimatorConditionMode.If, 0f, AnimacaoPlayerFisica.ParamRegrasMontadas);
    }

    /// <summary>Sprite do clipe Idle (primeiro quadro) — usado pela prévia do puzzle e como sprite inicial do Player.</summary>
    public static Sprite LoadIdleSprite()
    {
        return GetFirstSpriteFrame(AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath));
    }

    private static AnimatorState FindOrAddState(AnimatorStateMachine stateMachine, string name, Motion motion)
    {
        foreach (ChildAnimatorState child in stateMachine.states)
        {
            if (child.state.name == name)
            {
                child.state.motion = motion;
                return child.state;
            }
        }

        AnimatorState newState = stateMachine.AddState(name);
        newState.motion = motion;
        return newState;
    }

    private static AnimationClip GetOrCreateClip(string clipPath, string spriteFolder, string frameNamePrefix, float frameRate, bool loop)
    {
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (existing != null)
        {
            if (!existing.legacy)
            {
                return existing;
            }

            // Veio de uma versão anterior (componente Animation clássico) —
            // Animator exige clipes não-legacy. Recria no mesmo caminho.
            AssetDatabase.DeleteAsset(clipPath);
        }

        Sprite[] frames = LoadFramesInOrder(spriteFolder, frameNamePrefix);
        if (frames.Length == 0)
        {
            Debug.LogError($"[AnimationClipBuilder] Não encontrei frames (\"{frameNamePrefix}*\") em {spriteFolder}.");
            return null;
        }

        var clip = new AnimationClip { frameRate = frameRate };

        var binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };

        var keyframes = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe { time = i / frameRate, value = frames[i] };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.CreateAsset(clip, clipPath);
        AssetDatabase.SaveAssets();

        return clip;
    }

    // Carrega só os frames numerados (ex: player_run_dust_01..06), ignorando
    // a spritesheet combinada e qualquer outro arquivo que esteja na pasta.
    // Público pra ser reaproveitado pela prévia do puzzle do Animator (ver
    // AnimacaoSceneBuilder) e pela cena Tutorial_Anim.
    public static Sprite[] LoadFramesInOrder(string folder, string namePrefix)
    {
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });

        return guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
            .Where(s => s != null && s.name.StartsWith(namePrefix) && !s.name.Contains("spritesheet"))
            .OrderBy(s => s.name)
            .ToArray();
    }
}
