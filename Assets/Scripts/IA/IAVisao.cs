using UnityEngine;

/// <summary>
/// Utilitários de percepção compartilhados pelo Guarda (GuardaIA) e pelo
/// Aliado (AliadoIA): onde fica o "centro" de um personagem e se existe uma
/// linha de visão livre até ele (Physics2D.Raycast bloqueado por paredes).
///
/// Ficam num lugar só porque inimigo e aliado usam exatamente a mesma
/// percepção. É isso que a fase quer mostrar: a estrutura é a mesma, só
/// mudam o alvo e as regras.
/// </summary>
public static class IAVisao
{
    /// <summary>Centro de um personagem: o centro do Collider2D, se tiver; senão a posição do Transform.</summary>
    public static Vector2 Centro(Transform t)
    {
        if (t == null)
        {
            return Vector2.zero;
        }

        Collider2D col = t.GetComponent<Collider2D>();
        return col != null && col.enabled ? (Vector2)col.bounds.center : (Vector2)t.position;
    }

    /// <summary>
    /// Lança um Raycast de <paramref name="origem"/> até o centro de
    /// <paramref name="alvo"/>. Devolve true se o raio chega no alvo sem
    /// bater antes em nada sólido da máscara <paramref name="obstaculos"/>.
    ///
    /// Ignorados no caminho: colliders do próprio observador, triggers e
    /// corpos dinâmicos (outros personagens não tapam a visão, só o cenário).
    /// </summary>
    public static bool LinhaLivre(Vector2 origem, Transform alvo, Transform quemOlha, LayerMask obstaculos)
    {
        return LinhaLivre(origem, alvo, quemOlha, obstaculos, out _);
    }

    /// <summary>Mesmo que <see cref="LinhaLivre(Vector2, Transform, Transform, LayerMask)"/>, devolvendo onde o raio parou (útil pra Gizmos).</summary>
    public static bool LinhaLivre(Vector2 origem, Transform alvo, Transform quemOlha, LayerMask obstaculos, out Vector2 pontoFinal)
    {
        Vector2 destino = Centro(alvo);
        pontoFinal = destino;

        if (alvo == null)
        {
            return false;
        }

        Vector2 delta = destino - origem;
        float distancia = delta.magnitude;
        if (distancia < 0.01f)
        {
            return true;
        }

        // RaycastAll devolve os acertos em ordem de distância: o primeiro
        // obstáculo "de verdade" que aparecer antes do alvo bloqueia a visão.
        RaycastHit2D[] hits = Physics2D.RaycastAll(origem, delta / distancia, distancia, obstaculos);
        foreach (RaycastHit2D hit in hits)
        {
            Collider2D col = hit.collider;
            if (col == null || col.isTrigger)
            {
                continue;
            }

            Transform t = col.transform;
            if (quemOlha != null && t.IsChildOf(quemOlha))
            {
                continue;
            }

            if (t.IsChildOf(alvo))
            {
                return true;
            }

            Rigidbody2D rb = col.attachedRigidbody;
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
            {
                continue;
            }

            pontoFinal = hit.point;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Faz os personagens (jogador, guarda, aliado) atravessarem uns aos
    /// outros. Quem "pega" o jogador é o dano (TakeDamage), não um empurrão
    /// físico, e assim ninguém fica entalado em ninguém no corredor.
    /// </summary>
    public static void IgnorarColisoesEntre(params Component[] personagens)
    {
        for (int i = 0; i < personagens.Length; i++)
        {
            if (personagens[i] == null) continue;
            Collider2D[] a = personagens[i].GetComponentsInChildren<Collider2D>(true);

            for (int j = i + 1; j < personagens.Length; j++)
            {
                if (personagens[j] == null) continue;
                Collider2D[] b = personagens[j].GetComponentsInChildren<Collider2D>(true);

                foreach (Collider2D ca in a)
                {
                    foreach (Collider2D cb in b)
                    {
                        Physics2D.IgnoreCollision(ca, cb, true);
                    }
                }
            }
        }
    }
}
