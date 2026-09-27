using System.Text;
using StudyPlatform.Api.Exceptions;

namespace StudyPlatform.Api.Services.Students;

public sealed class CsvStudentParser
{
    public const int MaxFileBytes = 1024 * 1024;
    public const int MaxRecords = 2000;

    public IReadOnlyList<ParsedStudentCsvRow> Parse(byte[] bytes)
    {
        if (bytes.Length == 0) throw InvalidCsv("O arquivo CSV está vazio.");
        if (bytes.Length > MaxFileBytes) throw new ApiException(413, "csv_file_too_large", "O arquivo CSV deve ter no máximo 1 MiB.");

        string content;
        try
        {
            var offset = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble) ? Encoding.UTF8.Preamble.Length : 0;
            content = new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            throw InvalidCsv("O arquivo deve estar codificado em UTF-8.");
        }

        var records = ParseRecords(content);
        if (records.Count == 0) throw InvalidCsv("O arquivo CSV não contém cabeçalho.");
        if (records.Count - 1 > MaxRecords) throw new ApiException(413, "csv_too_many_rows", $"O arquivo pode conter no máximo {MaxRecords} linhas de dados.");

        var header = records[0].Fields.Select(x => x.Trim().ToLowerInvariant()).ToArray();
        if (header.Length == 0 || header[0].Length == 0) throw InvalidCsv("O cabeçalho 'matricula' é obrigatório.");
        if (header.Distinct(StringComparer.Ordinal).Count() != header.Length)
            throw InvalidCsv("O cabeçalho contém colunas repetidas.");
        if (!header.Contains("matricula", StringComparer.Ordinal)) throw InvalidCsv("O cabeçalho 'matricula' é obrigatório.");
        if (header.Any(x => x is not ("matricula" or "nome"))) throw InvalidCsv("Coluna desconhecida no cabeçalho. Use somente 'matricula' e, opcionalmente, 'nome'.");

        var enrollmentIndex = Array.IndexOf(header, "matricula");
        var nameIndex = Array.IndexOf(header, "nome");
        return records.Skip(1)
            .Where(record => !(record.Fields.Count == 1 && string.IsNullOrWhiteSpace(record.Fields[0])))
            .Select(record => new ParsedStudentCsvRow(
                record.LineNumber,
                record.Fields.Count == header.Length ? Clean(record.Fields[enrollmentIndex]) : string.Empty,
                nameIndex >= 0 && record.Fields.Count == header.Length ? CleanOptional(record.Fields[nameIndex]) : null,
                record.Fields.Count == header.Length ? null : "A quantidade de campos não corresponde ao cabeçalho."))
            .ToArray();
    }

    private static List<CsvRecord> ParseRecords(string content)
    {
        var records = new List<CsvRecord>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var state = CsvState.Start;
        var line = 1;
        var recordLine = 1;

        for (var index = 0; index < content.Length; index++)
        {
            var c = content[index];
            var newline = c is '\r' or '\n';
            if (newline)
            {
                var width = c == '\r' && index + 1 < content.Length && content[index + 1] == '\n' ? 2 : 1;
                if (state == CsvState.Quoted)
                {
                    field.Append('\n');
                    if (width == 2) index++;
                    line++;
                    continue;
                }

                FinishField();
                FinishRecord();
                if (width == 2) index++;
                line++;
                recordLine = line;
                continue;
            }

            switch (state)
            {
                case CsvState.Start:
                    if (c == ',') FinishField();
                    else if (c == '"') state = CsvState.Quoted;
                    else { field.Append(c); state = CsvState.Unquoted; }
                    break;
                case CsvState.Unquoted:
                    if (c == ',') { FinishField(); state = CsvState.Start; }
                    else if (c == '"') throw InvalidCsv($"Aspas inválidas na linha {line}.");
                    else field.Append(c);
                    break;
                case CsvState.Quoted:
                    if (c != '"') field.Append(c);
                    else if (index + 1 < content.Length && content[index + 1] == '"') { field.Append('"'); index++; }
                    else state = CsvState.AfterQuote;
                    break;
                case CsvState.AfterQuote:
                    if (c == ',') { FinishField(); state = CsvState.Start; }
                    else if (c is ' ' or '\t') { /* espaços após campo citado */ }
                    else throw InvalidCsv($"Conteúdo inesperado após aspas na linha {line}.");
                    break;
            }
        }

        if (state == CsvState.Quoted) throw InvalidCsv($"Campo entre aspas não foi fechado (a partir da linha {recordLine}).");
        if (field.Length > 0 || fields.Count > 0 || state != CsvState.Start)
        {
            FinishField();
            FinishRecord();
        }
        return records;

        void FinishField() { fields.Add(field.ToString()); field.Clear(); }
        void FinishRecord() {
            records.Add(new CsvRecord(recordLine, fields.ToArray()));
            if (records.Count > MaxRecords + 1) throw new ApiException(413, "csv_too_many_rows", $"O arquivo pode conter no máximo {MaxRecords} linhas de dados.");
            fields.Clear();
            state = CsvState.Start;
        }
    }

    private static string Clean(string value) => value.Trim();
    private static string? CleanOptional(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static ApiException InvalidCsv(string message) => new(400, "invalid_csv", message);
    private enum CsvState { Start, Unquoted, Quoted, AfterQuote }
    private sealed record CsvRecord(int LineNumber, IReadOnlyList<string> Fields);
}

public sealed record ParsedStudentCsvRow(int LineNumber, string EnrollmentNumber, string? Name, string? StructuralError);
