using MiniTime.Core.Apuracao;
using MiniTime.Core.Models;
using MiniTime.Data;
using Xunit.Abstractions;

namespace MiniTime.Tests;

/// <summary>
/// Compara o motor novo com a classificação gravada pelo sistema legado nas batidas coletadas (Tipo 7).
/// Só roda quando MINITIME_PARITY_DB aponta para um SQLite importado do MDB real.
/// </summary>
public class ParidadeTests(ITestOutputHelper saida)
{
    [Fact]
    [Trait("Category", "Live")]
    public void Classificacao_das_batidas_confere_com_o_legado()
    {
        var caminho = Environment.GetEnvironmentVariable("MINITIME_PARITY_DB");
        if (string.IsNullOrEmpty(caminho) || !File.Exists(caminho)) return;

        var rep = new Repositorios(new MiniTimeDb(caminho));
        var horarios = rep.Horarios.Todos().ToDictionary(h => h.Codigo);
        var jornadas = rep.Jornadas.Todos().ToDictionary(j => j.Codigo);
        var feriados = rep.Feriados.Todos().Select(f => (f.Dia, f.Mes)).ToList();
        var ferias = rep.Ferias.Todos();
        var desde = new DateTime(2022, 6, 1);
        var ate = new DateTime(2026, 8, 31);

        long total = 0, iguais = 0, ignoradasIguais = 0, ignoradasTotal = 0, divIguais = 0, divTotal = 0;
        foreach (var f in rep.Funcionarios.Todos())
        {
            if (!jornadas.TryGetValue(f.Horario, out var j)) continue;
            var marc = rep.Marcacoes.Periodo(desde, ate.AddDays(1), f.Codigo);
            var ctx = new ContextoApuracao { Funcionario = f, Jornada = j, Horarios = horarios, Feriados = feriados, Ferias = ferias, Hoje = ate };
            var res = Apurador.Apurar(ctx, marc, desde, ate);
            foreach (var dia in res.Dias.Where(d => d.Horario is not null))
            {
                // só dias sem intervenção manual, com batidas já processadas pelo legado
                var doDia = marc.Where(m => m.DataHora.Date == dia.Data).ToList();
                if (doDia.Any(m => m.Tipo == TipoMarcacao.Manual && m.Justificativa != -1)) continue;
                if (doDia.Any(m => m.Tipo == TipoMarcacao.Gerada && m.EntradaSaida is >= 1 and <= 4)) continue;
                foreach (var m in dia.Marcacoes.Where(x => x.Origem.Tipo == TipoMarcacao.Coletada && x.Origem.EntradaSaida != 0))
                {
                    total++;
                    var legado = m.Origem.EntradaSaida;
                    if (m.Posicao == legado) iguais++;
                    if ((m.Posicao == Posicao.Ignorada) == (legado == -1)) ignoradasIguais++;
                    ignoradasTotal++;
                    if (legado is >= 1 and <= 4 && m.Posicao is >= 1 and <= 4)
                    {
                        divTotal++;
                        if ((m.Divergencia != 0) == (m.Origem.Divergencia != 0)) divIguais++;
                    }
                }
            }
        }
        saida.WriteLine($"batidas={total} posição igual={iguais} ({100.0 * iguais / Math.Max(1, total):F1}%)  " +
                        $"ignorada igual={100.0 * ignoradasIguais / Math.Max(1, ignoradasTotal):F1}%  " +
                        $"divergência igual={100.0 * divIguais / Math.Max(1, divTotal):F1}% de {divTotal}");
        Assert.True(total > 1000, "poucos dados para comparar");
        Assert.True(100.0 * iguais / total >= 70, "paridade de posição abaixo de 70%");
    }
}
