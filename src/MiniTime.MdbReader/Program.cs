using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Globalization;
using System.IO;
using System.Text;

namespace MiniTime.MdbReader
{
    /// <summary>
    /// Leitor de MDB (Access/Jet) para o importador. Precisa ser x86 porque o Jet 4.0 só existe em 32 bits.
    /// Saída em NDJSON (UTF-8) na saída padrão:
    ///   {"event":"tables","tables":[{"name":"X","count":N,"columns":["A","B"]}]}
    ///   {"event":"table","name":"X","count":N}
    ///   {"t":"X","r":{"A":1,"B":"txt","C":null}}
    ///   {"event":"done"}
    /// Erros: mensagem em stderr e código de saída != 0.
    /// Uso: MiniTime.MdbReader.exe &lt;arquivo.mdb&gt; [--pwd senha] [--schema | --tables A,B]
    /// </summary>
    internal static class Program
    {
        // A senha do MDB nunca fica no código: vem de --pwd ou da variável de ambiente MINITIME_MDB_PWD.

        private static int Main(string[] args)
        {
            var saida = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = false, NewLine = "\n" };
            try
            {
                if (args.Length < 1) { Console.Error.WriteLine("Uso: MiniTime.MdbReader <arquivo.mdb> [--pwd senha] [--schema | --tables A,B]  (ou variável MINITIME_MDB_PWD)"); return 2; }
                string arquivo = args[0], senha = Environment.GetEnvironmentVariable("MINITIME_MDB_PWD") ?? "";
                bool soSchema = false;
                HashSet<string> filtro = null;
                for (int i = 1; i < args.Length; i++)
                {
                    if (args[i] == "--pwd" && i + 1 < args.Length) senha = args[++i];
                    else if (args[i] == "--schema") soSchema = true;
                    else if (args[i] == "--tables" && i + 1 < args.Length)
                        filtro = new HashSet<string>(args[++i].Split(','), StringComparer.OrdinalIgnoreCase);
                }
                if (!File.Exists(arquivo)) { Console.Error.WriteLine("Arquivo não encontrado: " + arquivo); return 3; }

                using (var conn = Abrir(arquivo, senha))
                {
                    var tabelas = ListarTabelas(conn);
                    if (filtro != null) tabelas.RemoveAll(t => !filtro.Contains(t));

                    var sb = new StringBuilder("{\"event\":\"tables\",\"tables\":[");
                    for (int i = 0; i < tabelas.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        sb.Append("{\"name\":").Append(Json(tabelas[i])).Append(",\"count\":").Append(Contar(conn, tabelas[i]))
                          .Append(",\"columns\":[");
                        var cols = Colunas(conn, tabelas[i]);
                        for (int k = 0; k < cols.Count; k++) { if (k > 0) sb.Append(','); sb.Append(Json(cols[k])); }
                        sb.Append("]}");
                    }
                    sb.Append("]}");
                    saida.WriteLine(sb.ToString());
                    saida.Flush();
                    if (soSchema) return 0;

                    foreach (var t in tabelas)
                    {
                        saida.WriteLine("{\"event\":\"table\",\"name\":" + Json(t) + ",\"count\":" + Contar(conn, t) + "}");
                        using (var cmd = new OleDbCommand("SELECT * FROM [" + t + "]", conn))
                        using (var r = cmd.ExecuteReader())
                        {
                            var nomes = new string[r.FieldCount];
                            for (int i = 0; i < nomes.Length; i++) nomes[i] = Json(r.GetName(i));
                            var linha = new StringBuilder();
                            while (r.Read())
                            {
                                linha.Clear();
                                linha.Append("{\"t\":").Append(Json(t)).Append(",\"r\":{");
                                for (int i = 0; i < nomes.Length; i++)
                                {
                                    if (i > 0) linha.Append(',');
                                    linha.Append(nomes[i]).Append(':').Append(Valor(r.IsDBNull(i) ? null : r.GetValue(i)));
                                }
                                linha.Append("}}");
                                saida.WriteLine(linha.ToString());
                            }
                        }
                        saida.Flush();
                    }
                    saida.WriteLine("{\"event\":\"done\"}");
                    saida.Flush();
                }
                return 0;
            }
            catch (Exception ex)
            {
                try { saida.Flush(); } catch { }
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static OleDbConnection Abrir(string arquivo, string senha)
        {
            string[] provedores = { "Microsoft.Jet.OLEDB.4.0", "Microsoft.ACE.OLEDB.12.0", "Microsoft.ACE.OLEDB.16.0" };
            Exception ultimo = null;
            foreach (var p in provedores)
            {
                foreach (var pwd in senha.Length > 0 ? new[] { senha, "" } : new[] { "" })
                {
                    var cs = "Provider=" + p + ";Data Source=" + arquivo + ";Mode=Read;" +
                             (pwd.Length > 0 ? "Jet OLEDB:Database Password=" + pwd + ";" : "");
                    var c = new OleDbConnection(cs);
                    try { c.Open(); return c; }
                    catch (Exception ex) { ultimo = ex; c.Dispose(); }
                }
            }
            throw new InvalidOperationException("Não foi possível abrir o MDB (provedor ou senha): " + (ultimo == null ? "" : ultimo.Message));
        }

        private static List<string> ListarTabelas(OleDbConnection c)
        {
            var lista = new List<string>();
            var dt = c.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, new object[] { null, null, null, "TABLE" });
            foreach (DataRow row in dt.Rows) lista.Add((string)row["TABLE_NAME"]);
            lista.Sort(StringComparer.OrdinalIgnoreCase);
            return lista;
        }

        private static List<string> Colunas(OleDbConnection c, string tabela)
        {
            var dt = c.GetOleDbSchemaTable(OleDbSchemaGuid.Columns, new object[] { null, null, tabela, null });
            var rows = new List<DataRow>();
            foreach (DataRow r in dt.Rows) rows.Add(r);
            rows.Sort((a, b) => Convert.ToInt32(a["ORDINAL_POSITION"]).CompareTo(Convert.ToInt32(b["ORDINAL_POSITION"])));
            var nomes = new List<string>();
            foreach (var r in rows) nomes.Add((string)r["COLUMN_NAME"]);
            return nomes;
        }

        private static long Contar(OleDbConnection c, string t)
        {
            using (var cmd = new OleDbCommand("SELECT COUNT(*) FROM [" + t + "]", c))
                return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        private static string Valor(object v)
        {
            if (v == null) return "null";
            if (v is bool) return (bool)v ? "true" : "false";
            if (v is DateTime) return Json(((DateTime)v).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            if (v is byte[]) return Json("b64:" + Convert.ToBase64String((byte[])v));
            if (v is string) return Json((string)v);
            if (v is IFormattable) return ((IFormattable)v).ToString(null, CultureInfo.InvariantCulture);
            return Json(Convert.ToString(v, CultureInfo.InvariantCulture));
        }

        private static string Json(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (var ch in s)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        else sb.Append(ch);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
