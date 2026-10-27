using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Guarda quais colliders estão servindo de chão para o jogador (contato com
/// a normal apontando pra cima). Usado pelos três controladores do jogador
/// (Player, AplicacaoPlayerController e InteracaoPlayerController).
///
/// Na saída (OnCollisionExit2D) o Unity entrega a colisão sem pontos de
/// contato, então o chão sai da lista pelo collider, sem olhar a normal.
/// Antes era uma contagem que só voltava a zero no pulo: o robô que saía
/// andando de uma beirada continuava "no chão" durante a queda e podia pular
/// no ar.
/// </summary>
public class ContatosChao
{
    private readonly HashSet<Collider2D> chaos = new HashSet<Collider2D>();

    // Collider desligado, destruído ou virado trigger não segura mais o jogador.
    private static readonly Predicate<Collider2D> semEfeito =
        c => c == null || !c.enabled || !c.gameObject.activeInHierarchy || c.isTrigger;

    public bool NoChao
    {
        get
        {
            if (chaos.Count > 0) chaos.RemoveWhere(semEfeito);
            return chaos.Count > 0;
        }
    }

    /// <summary>
    /// OnCollisionEnter2D. Devolve true quando o jogador estava no ar e esse
    /// é o primeiro chão (a aterrissagem, pra tocar o som).
    /// </summary>
    public bool Entrar(Collision2D collision)
    {
        if (!TemNormalPraCima(collision)) return false;
        bool estavaNoAr = !NoChao;
        return chaos.Add(collision.collider) && estavaNoAr;
    }

    /// <summary>
    /// OnCollisionStay2D. Põe o chão de volta quando o jogador continua parado
    /// em cima dele sem ter saído (ex: o pulo bateu num teto logo acima) ou
    /// passou de um bloco pro outro e a saída do anterior chegou antes.
    /// Subindo, não conta: logo depois do pulo o contato ainda existe por um
    /// passo de física, e isso daria um segundo pulo.
    /// </summary>
    public void Permanecer(Collision2D collision, float velocidadeY)
    {
        if (velocidadeY <= 0.01f && TemNormalPraCima(collision))
        {
            chaos.Add(collision.collider);
        }
    }

    /// <summary>OnCollisionExit2D.</summary>
    public void Sair(Collision2D collision)
    {
        chaos.Remove(collision.collider);
    }

    /// <summary>Esquece o chão na hora (no pulo e ao voltar pro ponto de partida).</summary>
    public void Limpar()
    {
        chaos.Clear();
    }

    private static bool TemNormalPraCima(Collision2D collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.5f) return true;
        }
        return false;
    }
}
