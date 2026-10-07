using System.Windows;
using System.Windows.Controls;
using MiniTime.App.Controls;
using MiniTime.App.Services;
using MiniTime.Core.Models;
using MiniTime.Core.Util;
using MiniTime.Data;

namespace MiniTime.App.Views;

// Textos de validação vêm das mensagens do programa original (recursos do MiniTime.exe).

public sealed class HorariosView : CadastroView<Horario>
{
    public HorariosView() => Montar();

    protected override Repository<Horario> Repo => Sessao.Repos.Horarios;
    protected override string Titulo => "Tabela de Horários de Trabalho";
    protected override object[] Chave(Horario e) => [e.Codigo];
    protected override string Descricao(Horario e) => $"{e.Codigo:00} - {e.Descricao}";
    protected override Horario NovoRegistro() => new()
    {
        Codigo = Repo.ProximoCodigo(),
        TolManha = Sessao.Repos.Parametros.Obter().TolManha,
        TolTarde = Sessao.Repos.Parametros.Obter().TolTarde,
        TolSaida = Sessao.Repos.Parametros.Obter().TolSaida,
    };

    protected override IReadOnlyList<ColunaGrid> Colunas =>
    [
        new("Código", nameof(Horario.Codigo), 60, "00"),
        new("Descrição", nameof(Horario.Descricao), 0),
    ];

    protected override IReadOnlyList<Campo> Campos =>
    [
        new() { Rotulo = "Código", Propriedade = nameof(Horario.Codigo), Tipo = TipoCampo.Inteiro, SomenteNovo = true, Dica = "1 a 99" },
        new() { Rotulo = "Descrição", Propriedade = nameof(Horario.Descricao), MaxLength = 30 },
        new() { Grupo = "Faixa horária", Rotulo = "Entrada", Propriedade = nameof(Horario.DeSS1), Tipo = TipoCampo.Hora },
        new() { Grupo = "Faixa horária", Rotulo = "Saída", Propriedade = nameof(Horario.DeSS4), Tipo = TipoCampo.Hora },
        new() { Grupo = "Faixa horária", Rotulo = "Noturno", Propriedade = nameof(Horario.Noturno), Tipo = TipoCampo.Booleano, Dica = "Entrada maior que a saída (ex.: 22:00 às 06:00)" },
        new() { Grupo = "Faixa horária", Rotulo = "Não trabalha (folga)", Propriedade = nameof(Horario.NaoTrabalha), Tipo = TipoCampo.Booleano },
        new() { Grupo = "Intervalo", Rotulo = "Tem intervalo", Propriedade = nameof(Horario.Intervalo), Tipo = TipoCampo.Booleano },
        new() { Grupo = "Intervalo", Rotulo = "Saída para o intervalo", Propriedade = nameof(Horario.DeSS2), Tipo = TipoCampo.Hora },
        new() { Grupo = "Intervalo", Rotulo = "Retorno do intervalo", Propriedade = nameof(Horario.DeSS3), Tipo = TipoCampo.Hora },
        new() { Grupo = "Intervalo", Rotulo = "Marcação obrigatória", Propriedade = nameof(Horario.RefObrig), Tipo = TipoCampo.Booleano, Dica = "O funcionário precisa bater o ponto para sair e voltar do intervalo" },
        new() { Grupo = "Intervalo", Rotulo = "Tempo mínimo (min)", Propriedade = nameof(Horario.RefMinimo), Tipo = TipoCampo.Inteiro },
        new() { Grupo = "Tolerância de atraso (min)", Rotulo = "Entrada manhã", Propriedade = nameof(Horario.TolManha), Tipo = TipoCampo.Inteiro },
        new() { Grupo = "Tolerância de atraso (min)", Rotulo = "Retorno tarde", Propriedade = nameof(Horario.TolTarde), Tipo = TipoCampo.Inteiro },
        new() { Grupo = "Tolerância de atraso (min)", Rotulo = "Saída", Propriedade = nameof(Horario.TolSaida), Tipo = TipoCampo.Inteiro },
        new() { Grupo = "Tolerância de hora extra (min)", Rotulo = "Entrada", Propriedade = nameof(Horario.TolExtEnt), Tipo = TipoCampo.Inteiro },
        new() { Grupo = "Tolerância de hora extra (min)", Rotulo = "Intervalo", Propriedade = nameof(Horario.TolExtInt), Tipo = TipoCampo.Inteiro },
        new() { Grupo = "Tolerância de hora extra (min)", Rotulo = "Saída", Propriedade = nameof(Horario.TolExtSai), Tipo = TipoCampo.Inteiro },
    ];

    protected override string? Validar(Horario e, bool novo)
    {
        if (e.Codigo == 0) return "O Código não pode ser Zero!";
        if (e.Codigo > 99) return "O Código não pode ser maior que 99";
        if (string.IsNullOrWhiteSpace(e.Descricao)) return "Descrição em Branco!";
        if (e.NaoTrabalha != 0) return null;

        if (e.DeSS1 is not { } ent || e.DeSS4 is not { } sai) return "Horário de trabalho inválido!";
        if (e.Noturno != 0)
        {
            if (ent <= sai) return "Para utilizar a opção horário de trabalho noturno o horário de entrada deve ser maior que o horário de saída.";
            return null; // com virada de dia as comparações do intervalo seguem outra regra
        }
        if (ent >= sai) return "Horário de trabalho inválido!";
        if (e.Intervalo != 0)
        {
            if (e.DeSS2 is not { } si || e.DeSS3 is not { } ri) return "Horário de trabalho inválido!";
            if (si <= ent) return "O horário de saída para o intervalo deve ser maior que o horário de entrada.";
            if (ri <= si) return "O horário de retorno do intervalo deve ser maior que o horário de saída do intervalo.";
            if (ri >= sai) return "O horário de retorno do intervalo deve ser menor que o horário de saída.";
        }
        return null;
    }

    protected override string? AvisoExclusao(Horario e)
    {
        var emUso = Sessao.Repos.Jornadas.Contar("Segunda=@c OR Terca=@c OR Quarta=@c OR Quinta=@c OR Sexta=@c OR Sabado=@c OR Domingo=@c", ("c", e.Codigo));
        return emUso > 0
            ? $"Atenção: {emUso} jornada(s) utilizam esta faixa horária. Após a exclusão verifique e atualize as jornadas."
            : "Pode haver jornadas utilizando essa faixa horária. Após a exclusão verifique e atualize as jornadas.";
    }
}

public sealed class JornadasView : CadastroView<Jornada>
{
    public JornadasView() => Montar();

    protected override Repository<Jornada> Repo => Sessao.Repos.Jornadas;
    protected override string Titulo => "Tabela de Jornadas";
    protected override object[] Chave(Jornada e) => [e.Codigo];
    protected override string Descricao(Jornada e) => $"{e.Codigo:00} - {e.Descricao}";
    protected override Jornada NovoRegistro() => new() { Codigo = Repo.ProximoCodigo(), DiaDSR = 1 };

    protected override IReadOnlyList<ColunaGrid> Colunas =>
    [
        new("Código", nameof(Jornada.Codigo), 60, "00"),
        new("Descrição", nameof(Jornada.Descricao), 0),
    ];

    private static List<(int, string)> OpcoesHorario()
    {
        var l = new List<(int, string)> { (0, "Sem expediente") };
        l.AddRange(Sessao.Repos.Horarios.Todos("Codigo").Select(h => (h.Codigo, $"{h.Codigo:00} - {h.Descricao}")));
        return l;
    }

    protected override IReadOnlyList<Campo> Campos
    {
        get
        {
            var op = OpcoesHorario();
            Campo Dia(string nome, string prop) => new() { Grupo = "Horário de trabalho em cada dia", Rotulo = nome, Propriedade = prop, Tipo = TipoCampo.Escolha, Opcoes = op, Largura = 300 };
            return
            [
                new() { Rotulo = "Código", Propriedade = nameof(Jornada.Codigo), Tipo = TipoCampo.Inteiro, SomenteNovo = true, Dica = "1 a 99" },
                new() { Rotulo = "Descrição", Propriedade = nameof(Jornada.Descricao), MaxLength = 35 },
                Dia("Segunda-feira", nameof(Jornada.Segunda)),
                Dia("Terça-feira", nameof(Jornada.Terca)),
                Dia("Quarta-feira", nameof(Jornada.Quarta)),
                Dia("Quinta-feira", nameof(Jornada.Quinta)),
                Dia("Sexta-feira", nameof(Jornada.Sexta)),
                Dia("Sábado", nameof(Jornada.Sabado)),
                Dia("Domingo", nameof(Jornada.Domingo)),
                new() { Grupo = "Opções", Rotulo = "Tratamento de DSR", Propriedade = nameof(Jornada.TrataDSR), Tipo = TipoCampo.Booleano, Dica = "O espelho de ponto trata as horas de DSR" },
                new() { Grupo = "Opções", Rotulo = "Não marca falta", Propriedade = nameof(Jornada.NaoMarcarFalta), Tipo = TipoCampo.Booleano, Dica = "Dia útil sem marcações não é considerado falta" },
                new() { Grupo = "Opções", Rotulo = "Horário semanal", Propriedade = nameof(Jornada.JornSemanal), Tipo = TipoCampo.Duracao, Dica = "Jornada semanal a cumprir (HH:MM)" },
                new() { Grupo = "Opções", Rotulo = "Dia do DSR", Propriedade = nameof(Jornada.DiaDSR), Tipo = TipoCampo.Escolha, Largura = 160,
                        Opcoes = [(1, "Domingo"), (2, "Segunda-feira"), (3, "Terça-feira"), (4, "Quarta-feira"), (5, "Quinta-feira"), (6, "Sexta-feira"), (7, "Sábado")] },
            ];
        }
    }

    protected override string? Validar(Jornada e, bool novo)
    {
        if (e.Codigo == 0) return "O Código não pode ser Zero!";
        if (e.Codigo > 99) return "O Código não pode ser maior que 99";
        if (string.IsNullOrWhiteSpace(e.Descricao)) return "Descrição em Branco!";
        return null;
    }

    protected override string? AntesExcluir(Jornada e)
    {
        var n = Sessao.Repos.Funcionarios.Contar("Horario = @c", ("c", e.Codigo));
        return n > 0 ? $"Esta jornada está associada a {n} funcionário(s). Associe outra jornada a eles antes de excluir." : null;
    }
}

public sealed class FeriadosView : CadastroView<Feriado>
{
    public FeriadosView() => Montar();

    protected override Repository<Feriado> Repo => Sessao.Repos.Feriados;
    protected override string Titulo => "Tabela de Feriados";
    protected override object[] Chave(Feriado e) => [e.Codigo];
    protected override string Descricao(Feriado e) => $"{e.Dia:00}/{e.Mes:00} - {e.Descricao}";
    protected override Feriado NovoRegistro() => new() { Codigo = Repo.ProximoCodigo() };
    protected override string Ordem => "Mes, Dia";

    protected override IReadOnlyList<ColunaGrid> Colunas =>
    [
        new("Código", nameof(Feriado.Codigo), 60, "00"),
        new("Dia", nameof(Feriado.Dia), 50, "00"),
        new("Mês", nameof(Feriado.Mes), 50, "00"),
        new("Descrição", nameof(Feriado.Descricao), 0),
    ];

    protected override IReadOnlyList<Campo> Campos =>
    [
        new() { Rotulo = "Código", Propriedade = nameof(Feriado.Codigo), Tipo = TipoCampo.Inteiro, SomenteNovo = true },
        new() { Rotulo = "Descrição", Propriedade = nameof(Feriado.Descricao), MaxLength = 30 },
        new() { Rotulo = "Dia", Propriedade = nameof(Feriado.Dia), Tipo = TipoCampo.Inteiro },
        new() { Rotulo = "Mês", Propriedade = nameof(Feriado.Mes), Tipo = TipoCampo.Inteiro },
    ];

    protected override string? Validar(Feriado e, bool novo)
    {
        if (e.Codigo == 0) return "O Código não pode ser Zero!";
        if (string.IsNullOrWhiteSpace(e.Descricao)) return "Descrição em Branco!";
        if (e.Dia == 0) return "Dia em Branco!";
        if (e.Mes == 0) return "Mês em Branco!";
        if (e.Mes is < 1 or > 12) return "Mês Inválido!";
        // 29/02 é aceito (feriado em ano bissexto); demais dias conforme o mês
        if (e.Dia < 1 || e.Dia > DateTime.DaysInMonth(2024, e.Mes)) return "Dia Inválido!";
        return null;
    }
}

public sealed class JustificativasView : CadastroView<Justificativa>
{
    public JustificativasView() => Montar();

    protected override Repository<Justificativa> Repo => Sessao.Repos.Justificativas;
    protected override string Titulo => "Tabela de Justificativas";
    protected override object[] Chave(Justificativa e) => [e.Codigo];
    protected override string Descricao(Justificativa e) => $"{e.Codigo:00} - {e.Descricao}";
    protected override Justificativa NovoRegistro() => new() { Codigo = Repo.ProximoCodigo() };

    protected override IReadOnlyList<ColunaGrid> Colunas =>
    [
        new("Código", nameof(Justificativa.Codigo), 60, "00"),
        new("Descrição", nameof(Justificativa.Descricao), 0),
    ];

    protected override IReadOnlyList<Campo> Campos =>
    [
        new() { Rotulo = "Código", Propriedade = nameof(Justificativa.Codigo), Tipo = TipoCampo.Inteiro, SomenteNovo = true },
        new() { Rotulo = "Descrição", Propriedade = nameof(Justificativa.Descricao), MaxLength = 60, Largura = 360 },
    ];

    protected override string? Validar(Justificativa e, bool novo)
    {
        if (e.Codigo == 0) return "O Código não pode ser Zero!";
        if (string.IsNullOrWhiteSpace(e.Descricao)) return "Descrição em Branco!";
        e.Tipo = e.Tipo == 0 ? 3 : e.Tipo; // o legado grava sempre 3
        return null;
    }

    protected override string? AntesExcluir(Justificativa e)
    {
        var n = Sessao.Repos.Marcacoes.Contar("Justificativa = @c", ("c", e.Codigo));
        return n > 0 ? $"Esta justificativa está em uso em {n} marcação(ões)." : null;
    }
}

public sealed class AlarmesView : CadastroView<Sirene>
{
    public AlarmesView() => Montar();

    protected override Repository<Sirene> Repo => Sessao.Repos.Sirenes;
    protected override string Titulo => "Manutenção do Arquivo de Horário de Alarmes";
    protected override object[] Chave(Sirene e) => [e.Codigo];
    protected override string Descricao(Sirene e) => $"{e.Codigo:00} - {e.Descricao}";
    protected override Sirene NovoRegistro() => new() { Codigo = Repo.ProximoCodigo(), Duracao = 3, Util = true };

    protected override IReadOnlyList<ColunaGrid> Colunas =>
    [
        new("Código", nameof(Sirene.Codigo), 60, "00"),
        new("Descrição", nameof(Sirene.Descricao), 0),
        new("Horário", nameof(Sirene.Horario), 70, null, HoraConverter.Instance),
    ];

    protected override IReadOnlyList<Campo> Campos =>
    [
        new() { Rotulo = "Código", Propriedade = nameof(Sirene.Codigo), Tipo = TipoCampo.Inteiro, SomenteNovo = true, Dica = "1 a 30" },
        new() { Rotulo = "Descrição", Propriedade = nameof(Sirene.Descricao), MaxLength = 30 },
        new() { Rotulo = "Horário", Propriedade = nameof(Sirene.Horario), Tipo = TipoCampo.Hora },
        new() { Rotulo = "Duração (s)", Propriedade = nameof(Sirene.Duracao), Tipo = TipoCampo.Inteiro },
        new() { Rotulo = "Dias úteis", Propriedade = nameof(Sirene.Util), Tipo = TipoCampo.Booleano },
        new() { Rotulo = "Fim de semana", Propriedade = nameof(Sirene.FimSem), Tipo = TipoCampo.Booleano },
    ];

    protected override string? Validar(Sirene e, bool novo)
    {
        if (e.Codigo == 0) return "O Código não pode ser Zero!";
        if (e.Codigo > 30) return "O Código não pode ser maior que 30";
        if (string.IsNullOrWhiteSpace(e.Descricao)) return "Descrição em Branco!";
        if (e.Horario is null) return "Horário de trabalho inválido!";
        return null;
    }
}

public sealed class FuncionariosView : CadastroView<Funcionario>
{
    private readonly DataGrid _gridFerias = new() { Height = 150, AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single, CanUserAddRows = false };
    private readonly DatePicker _ini = new() { Width = 120 };
    private readonly DatePicker _fim = new() { Width = 120 };
    private readonly StackPanel _painelFerias = new() { Margin = new Thickness(0, 14, 0, 0) };
    private readonly int _digitos = Math.Clamp(Sessao.Repos.Parametros.Obter().NumCartao, 4, 6);

    public FuncionariosView()
    {
        MontarFerias();
        Montar();
    }

    protected override Repository<Funcionario> Repo => Sessao.Repos.Funcionarios;
    protected override string Titulo => "Manutenção do Arquivo de Cartões";
    protected override object[] Chave(Funcionario e) => [e.Codigo];
    protected override string Ordem => "Codigo";
    protected override string Descricao(Funcionario e) => $"{e.CodigoCurto} - {e.Nome}";
    protected override UIElement? Extra => _painelFerias;
    protected override Funcionario NovoRegistro() => new() { SentidoMudancaDataDiaLivre = 1, HorarioDiurno = true };

    protected override IReadOnlyList<ColunaGrid> Colunas =>
    [
        new("Cartão", nameof(Funcionario.CodigoCurto), 80),
        new("Nome", nameof(Funcionario.Nome), 0),
    ];

    protected override IReadOnlyList<Campo> Campos
    {
        get
        {
            var jornadas = new List<(int, string)> { (0, "Sem horário") };
            jornadas.AddRange(Sessao.Repos.Jornadas.Todos("Codigo").Select(j => (j.Codigo, $"{j.Codigo:00} - {j.Descricao}")));
            return
            [
                new() { Rotulo = "Cartão", Propriedade = nameof(Funcionario.CodigoCurto), SomenteNovo = true, Largura = 140, MaxLength = _digitos, Dica = $"Somente números, até {_digitos} dígitos (conforme os parâmetros do relógio)" },
                new() { Rotulo = "Nome", Propriedade = nameof(Funcionario.Nome), MaxLength = 50, Largura = 340 },
                new() { Rotulo = "RG", Propriedade = nameof(Funcionario.RG), MaxLength = 15 },
                new() { Rotulo = "Função", Propriedade = nameof(Funcionario.Cargo), MaxLength = 30, Largura = 300 },
                new() { Rotulo = "Jornada", Propriedade = nameof(Funcionario.Horario), Tipo = TipoCampo.Escolha, Opcoes = jornadas, Largura = 340 },
                new() { Grupo = "Opções", Rotulo = "Horário de almoço móvel", Propriedade = nameof(Funcionario.HorarioAlmoco), Tipo = TipoCampo.Booleano, Dica = "Pode almoçar fora do horário do cadastro, respeitando o tempo mínimo" },
                new() { Grupo = "Opções", Rotulo = "Hora extra autorizada", Propriedade = nameof(Funcionario.AutHoraExtra), Tipo = TipoCampo.Booleano, Dica = "Se não autorizada, a marcação extra é desprezada" },
                new() { Grupo = "Horário diurno", Rotulo = "Horário diurno", Propriedade = nameof(Funcionario.HorarioDiurno), Tipo = TipoCampo.Booleano },
                new() { Grupo = "Horário diurno", Rotulo = "Mudança de data em dia livre às", Propriedade = nameof(Funcionario.HrMudancaDataDiaLivre), Tipo = TipoCampo.Hora },
                new() { Grupo = "Horário diurno", Rotulo = "Sentido da mudança", Propriedade = nameof(Funcionario.SentidoMudancaDataDiaLivre), Tipo = TipoCampo.Escolha, Largura = 260,
                        Opcoes = [(0, "Dia anterior para dia atual"), (1, "Dia atual para dia posterior")] },
                new() { Grupo = "Horário noturno", Rotulo = "Horário noturno", Propriedade = nameof(Funcionario.HorarioNoturno), Tipo = TipoCampo.Booleano },
                new() { Grupo = "Horário noturno", Rotulo = "Mudança de data às", Propriedade = nameof(Funcionario.HrMudancaData), Tipo = TipoCampo.Hora },
            ];
        }
    }

    protected override string? Validar(Funcionario e, bool novo)
    {
        var cod = e.CodigoCurto;
        if (!cod.All(char.IsDigit)) return "O número do cartão deve conter apenas números.";
        if (cod.TrimStart('0').Length == 0) return "O Código não pode ser Zero!";
        if (cod.Length > _digitos) return $"O número do cartão deve ter no máximo {_digitos} dígitos.";
        if (string.IsNullOrWhiteSpace(e.Nome)) return "Descrição em Branco!";
        if (e.Horario != 0 && !Sessao.Repos.Jornadas.Existe(e.Horario)) return "A jornada associada a este funcionário não existe.";
        e.Nome = e.Nome.Trim();
        return null;
    }

    protected override void DepoisExcluir(Funcionario e)
    {
        // Marcações ficam (histórico); férias e templates biométricos do cartão saem junto.
        Sessao.Db.Execute("DELETE FROM Ferias WHERE Funcionario = @c", ("c", e.Codigo));
        Sessao.Db.Execute("DELETE FROM Templates WHERE CodigoCartao = @c", ("c", e.Codigo));
    }

    protected override string? AvisoExclusao(Funcionario e)
    {
        var n = Sessao.Repos.Marcacoes.Contar("Cracha = @c", ("c", e.Codigo));
        return n > 0 ? $"As {n:N0} marcações deste cartão permanecem no banco." : null;
    }

    protected override void AoCarregar(Funcionario e)
    {
        _painelFerias.IsEnabled = !EhNovo;
        CarregarFerias();
    }

    protected override void AoGravar(Funcionario e, bool novo)
    {
        _painelFerias.IsEnabled = true;
    }

    // ---------- férias ----------

    private void MontarFerias()
    {
        _painelFerias.Children.Add(new TextBlock { Text = "Férias", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        _gridFerias.Columns.Add(new DataGridTextColumn { Header = "Início", Binding = new System.Windows.Data.Binding(nameof(Ferias.Inicio)) { StringFormat = "dd/MM/yyyy" }, Width = 110 });
        _gridFerias.Columns.Add(new DataGridTextColumn { Header = "Fim", Binding = new System.Windows.Data.Binding(nameof(Ferias.Fim)) { StringFormat = "dd/MM/yyyy" }, Width = 110 });
        _painelFerias.Children.Add(_gridFerias);

        var linha = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        linha.Children.Add(new TextBlock { Text = "Início:", Style = (Style)Application.Current.FindResource("Rotulo") });
        linha.Children.Add(_ini);
        linha.Children.Add(new TextBlock { Text = "Fim:", Style = (Style)Application.Current.FindResource("Rotulo"), Margin = new Thickness(12, 0, 8, 0) });
        linha.Children.Add(_fim);
        var add = new Button { Content = "Incluir", Margin = new Thickness(12, 0, 6, 0) };
        var del = new Button { Content = "Excluir período" };
        add.Click += (_, _) => IncluirFerias();
        del.Click += (_, _) => ExcluirFerias();
        linha.Children.Add(add);
        linha.Children.Add(del);
        _painelFerias.Children.Add(linha);
    }

    private void CarregarFerias()
    {
        _gridFerias.ItemsSource = Editando is null || EhNovo
            ? null
            : Sessao.Repos.Ferias.Onde("Funcionario = @f", "Inicio", ("f", Editando.Codigo));
    }

    private void IncluirFerias()
    {
        if (Editando is null || EhNovo) return;
        if (_ini.SelectedDate is not { } ini || _fim.SelectedDate is not { } fim) { Mensagens.Aviso("Informe o início e o fim das férias."); return; }
        if (fim < ini) { Mensagens.Aviso("Data Inválida!"); return; }
        Sessao.Repos.Ferias.Inserir(new Ferias { Funcionario = Editando.Codigo, Inicio = ini.Date, Fim = fim.Date });
        _ini.SelectedDate = null; _fim.SelectedDate = null;
        CarregarFerias();
    }

    private void ExcluirFerias()
    {
        if (_gridFerias.SelectedItem is not Ferias f) return;
        if (!Mensagens.Confirmar("Confirma Exclusão?")) return;
        Sessao.Repos.Ferias.Excluir(f.Id);
        CarregarFerias();
    }
}
