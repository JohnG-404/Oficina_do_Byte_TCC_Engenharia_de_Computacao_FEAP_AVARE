using UnityEngine;

/// <summary>
/// Configuração compartilhada pra evitar o "engancho" clássico de física 2D:
/// um personagem que pula do lado de uma plataforma fica preso na quina
/// (colisão) e/ou grudado por atrito, sem conseguir se soltar nem pular de
/// novo. Usado pelo Player e por qualquer PhysicsTarget que sirva de
/// plataforma.
///
/// Dois ajustes resolvem os dois sintomas:
/// - Sem atrito (PhysicsMaterial2D): evita "grudar" deslizando na lateral.
/// - Cantos arredondados (BoxCollider2D.edgeRadius): evita travar exatamente
///   na quina — sem isso, a colisão às vezes calcula uma normal ambígua ali
///   e trava o movimento horizontal como se tivesse batido numa parede.
/// </summary>
public static class Physics2DTuning
{
    private static PhysicsMaterial2D noFrictionMaterial;
    private static PhysicsMaterial2D platformGripMaterial;

    private static PhysicsMaterial2D NoFriction =>
        noFrictionMaterial != null
            ? noFrictionMaterial
            : noFrictionMaterial = new PhysicsMaterial2D("SemAtrito (gerado)") { friction = 0f, bounciness = 0f };

    // Atrito de verdade — pra objetos que passam a ter Física e Colisão e
    // vão receber peso em cima (ex: um bloco vira degrau de outro na Escada
    // Física). Sem isso, o objeto compartilhava o MESMO material sem atrito
    // do Player, e sem nada segurando ele no lugar, o impacto de pular em
    // cima já bastava pra empurrar o bloco pro lado e derrubar tudo. O
    // Player continua sem atrito do lado dele (ver NoFriction), então o
    // "engancho na quina" que o SemAtrito resolve continua resolvido.
    private static PhysicsMaterial2D PlatformGrip =>
        platformGripMaterial != null
            ? platformGripMaterial
            : platformGripMaterial = new PhysicsMaterial2D("GripDePlataforma (gerado)") { friction = 0.6f, bounciness = 0f };

    /// <param name="edgeRadiusWorldUnits">
    /// Quanto arredondar/encolher, em unidades de MUNDO (não locais). Objetos
    /// da fase costumam ter escala não-uniforme (ex: uma plataforma comprida
    /// e baixa) — encolher o Size em unidades locais por um valor fixo faz
    /// esse encolhimento virar bem maior num eixo que no outro depois de
    /// multiplicado pela escala, deixando o collider "pra dentro" demais só
    /// num lado. Por isso convertemos o valor desejado em unidades de mundo
    /// pra unidades locais por eixo, dividindo pela escala de cada eixo.
    /// </param>
    public static void PreventCornerSnag(Collider2D collider, float edgeRadiusWorldUnits = 0.08f)
    {
        RoundCorners(collider, edgeRadiusWorldUnits);
        collider.sharedMaterial = NoFriction;
    }

    /// <summary>
    /// Mesmo arredondamento de quina do <see cref="PreventCornerSnag"/>, mas
    /// com atrito de verdade em vez de zero — usado pelo PhysicsTarget, que
    /// pode precisar segurar peso (o Player, ou outro bloco empilhado em
    /// cima) sem escorregar.
    /// </summary>
    public static void PreventCornerSnagWithGrip(Collider2D collider, float edgeRadiusWorldUnits = 0.08f)
    {
        RoundCorners(collider, edgeRadiusWorldUnits);
        collider.sharedMaterial = PlatformGrip;
    }

    private static void RoundCorners(Collider2D collider, float edgeRadiusWorldUnits)
    {
        if (collider is BoxCollider2D box && box.edgeRadius <= 0f)
        {
            Vector3 lossyScale = collider.transform.lossyScale;
            float scaleX = Mathf.Approximately(lossyScale.x, 0f) ? 1f : Mathf.Abs(lossyScale.x);
            float scaleY = Mathf.Approximately(lossyScale.y, 0f) ? 1f : Mathf.Abs(lossyScale.y);

            float localRadiusX = edgeRadiusWorldUnits / scaleX;
            float localRadiusY = edgeRadiusWorldUnits / scaleY;

            box.size = Vector2.Max(
                box.size - new Vector2(localRadiusX * 2f, localRadiusY * 2f),
                Vector2.one * 0.1f);

            // edgeRadius é um único valor (não por eixo) — usa o menor raio local
            // pra não estourar o eixo mais "apertado" pela escala.
            box.edgeRadius = Mathf.Min(localRadiusX, localRadiusY, Mathf.Min(box.size.x, box.size.y) * 0.4f);
        }
    }
}
