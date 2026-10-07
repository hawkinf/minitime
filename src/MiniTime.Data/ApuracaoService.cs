using MiniTime.Core.Apuracao;
using MiniTime.Core.Models;
using MiniTime.Core.Util;

namespace MiniTime.Data;

/// <summary>Liga o motor de apuração ao banco: carrega o contexto, grava classificações e edita marcações.</summary>
public sealed class ApuracaoService(Repositorios repos)
{
    /// <summary>
    /// Período de apuração "atual" para o dia de fechamento: o último período já encerrado.
    /// Fechamento 31 (ou ≥ último dia do mês) = mês civil; senão de (D+1 do mês anterior) até D.
    /// </summary>
    public static (DateTime Ini, DateTime Fim) PeriodoPadrao(DateTime hoje, int diaFechamento)
    {
        if (diaFechamento is < 1 or >= 28)
        {
            var mes = new DateTime(hoje.Year, hoje.Month, 1);
            // mês civil anterior se estamos no início do mês, senão o mês corrente até ontem
            var ini = hoje.Day <= 5 ? mes.AddMonths(-1) : mes;
            return (ini, ini.AddMonths(1).AddDays(-1));
        }
        var fimMes = new DateTime(hoje.Year, hoje.Month, diaFechamento);
        var fim = fimMes <= hoje ? fimMes : fimMes.AddMonths(-1);
        return (fim.AddMonths(-1).AddDays(1), fim);
    }

    public ContextoApuracao Contexto(Funcionario f, DateTime? hoje = null)
    {
        var jornada = f.Horario != 0 ? repos.Jornadas.Obter(f.Horario) : null;
        return new ContextoApuracao
        {
            Funcionario = f,
            Jornada = jornada,
            Horarios = repos.Horarios.Todos().ToDictionary(h => h.Codigo),
            Feriados = repos.Feriados.Todos().Select(x => (x.Dia, x.Mes)).ToList(),
            Ferias = repos.Ferias.Onde("Funcionario = @f", null, ("f", f.Codigo)),
            Justificativas = repos.Justificativas.Todos().ToDictionary(j => j.Codigo),
            Hoje = hoje ?? DateTime.Today,
        };
    }

    public ResultadoApuracao Apurar(Funcionario f, DateTime ini, DateTime fim, bool persistir = true, DateTime? hoje = null)
    {
        // batidas de um dia podem cair na janela do dia seguinte (turno noturno): lê com folga
        var marcacoes = repos.Marcacoes.Periodo(ini.Date.AddDays(-1), fim.Date.AddDays(2), f.Codigo);
        var res = Apurador.Apurar(Contexto(f, hoje), marcacoes, ini, fim);
        if (persistir) Persistir(res);
        return res;
    }

    /// <summary>Grava a classificação (posição/situação/divergência) nas batidas, como o sistema legado fazia.</summary>
    public int Persistir(ResultadoApuracao res)
    {
        var n = 0;
        using var c = repos.Db.Abrir();
        using var tx = c.BeginTransaction();
        foreach (var m in res.Dias.SelectMany(d => d.Marcacoes))
        {
            var o = m.Origem;
            if (o.EntradaSaida == m.Posicao && o.Situacao == m.Situacao && o.Divergencia == m.Divergencia) continue;
            MiniTimeDb.Execute(c, tx, "UPDATE Marcacao SET EntradaSaida=@e, Situacao=@s, Divergencia=@d WHERE Id=@id",
                ("e", m.Posicao), ("s", m.Situacao), ("d", m.Divergencia), ("id", o.Id));
            o.EntradaSaida = m.Posicao; o.Situacao = m.Situacao; o.Divergencia = m.Divergencia;
            n++;
        }
        tx.Commit();
        return n;
    }

    // ------------------------------------------------------------ edição de marcações

    /// <summary>Insere uma batida manual (Tipo 0). A justificativa explica por que foi inserida.</summary>
    public Marcacao IncluirMarcacao(Funcionario f, DateTime dataHora, int justificativa)
    {
        var dt = new DateTime(dataHora.Year, dataHora.Month, dataHora.Day, dataHora.Hour, dataHora.Minute, 0);
        if (repos.Marcacoes.Existe(f.Codigo, dt))
            throw new InvalidOperationException("Já existe uma marcação neste dia e horário.");
        var m = new Marcacao { Cracha = f.Codigo, DataHora = dt, Tipo = TipoMarcacao.Manual, Justificativa = Math.Max(0, justificativa) };
        repos.Marcacoes.Inserir(m);
        return m;
    }

    /// <summary>Marcação coletada → desprezada (não conta nos cálculos); manual → exclui.</summary>
    public void DesprezarOuExcluir(Marcacao m)
    {
        if (m.Tipo == TipoMarcacao.Coletada)
            repos.Db.Execute("UPDATE Marcacao SET Tipo=@t, EntradaSaida=-1 WHERE Id=@id", ("t", TipoMarcacao.Desprezada), ("id", m.Id));
        else
            repos.Marcacoes.Excluir(m.Id);
    }

    public void RestaurarDesprezada(Marcacao m)
        => repos.Db.Execute("UPDATE Marcacao SET Tipo=@t, EntradaSaida=0, Situacao=0, Divergencia=0 WHERE Id=@id AND Tipo=@d",
            ("t", TipoMarcacao.Coletada), ("id", m.Id), ("d", TipoMarcacao.Desprezada));

    /// <summary>Abona o dia inteiro (falta justificada). Substitui justificativa anterior do dia.</summary>
    public void JustificarDia(Funcionario f, DateTime dia, int justificativa)
    {
        RemoverJustificativaDia(f, dia);
        repos.Marcacoes.Inserir(new Marcacao
        {
            Cracha = f.Codigo, DataHora = dia.Date.AddHours(23), Tipo = TipoMarcacao.Gerada, EntradaSaida = Posicao.StatusDia,
            Situacao = SituacaoMarcacao.Justificada, Divergencia = 3, Justificativa = justificativa,
        });
    }

    public void RemoverJustificativaDia(Funcionario f, DateTime dia)
        => repos.Db.Execute("DELETE FROM Marcacao WHERE Cracha=@c AND Tipo=@t AND EntradaSaida=@e AND DataHora>=@a AND DataHora<@b",
            ("c", f.Codigo), ("t", TipoMarcacao.Gerada), ("e", Posicao.StatusDia), ("a", dia.Date), ("b", dia.Date.AddDays(1)));

    /// <summary>Abona a divergência de uma coluna do espelho (1=entrada, 2=saída int., 3=retorno, 4=saída).</summary>
    public void JustificarColuna(Funcionario f, DateTime dia, int posicao, int justificativa, int minuto)
    {
        repos.Db.Execute("DELETE FROM Marcacao WHERE Cracha=@c AND Tipo=@t AND EntradaSaida=@e AND DataHora>=@a AND DataHora<@b",
            ("c", f.Codigo), ("t", TipoMarcacao.Gerada), ("e", posicao), ("a", dia.Date), ("b", dia.Date.AddDays(1)));
        if (justificativa <= 0) return;
        repos.Marcacoes.Inserir(new Marcacao
        {
            Cracha = f.Codigo, DataHora = dia.Date.AddMinutes(Math.Clamp(minuto, 0, 1439)), Tipo = TipoMarcacao.Gerada, EntradaSaida = posicao,
            Situacao = SituacaoMarcacao.Justificada, Divergencia = posicao is 2 or 4 ? 2 : 1, Justificativa = justificativa,
        });
    }

    /// <summary>
    /// Apaga as marcações inseridas/geradas e restaura as coletadas do relógio (função "Restaurar marcações"):
    /// desfaz desprezadas e remove manuais e geradas do período.
    /// </summary>
    public void RestaurarPeriodo(Funcionario f, DateTime ini, DateTime fim)
    {
        var a = ini.Date; var b = fim.Date.AddDays(1);
        repos.Db.Execute("DELETE FROM Marcacao WHERE Cracha=@c AND DataHora>=@a AND DataHora<@b AND Tipo IN (@m,@g)",
            ("c", f.Codigo), ("a", a), ("b", b), ("m", TipoMarcacao.Manual), ("g", TipoMarcacao.Gerada));
        repos.Db.Execute("UPDATE Marcacao SET Tipo=@t, EntradaSaida=0, Situacao=0, Divergencia=0 WHERE Cracha=@c AND DataHora>=@a AND DataHora<@b AND Tipo=@d",
            ("t", TipoMarcacao.Coletada), ("c", f.Codigo), ("a", a), ("b", b), ("d", TipoMarcacao.Desprezada));
    }
}
