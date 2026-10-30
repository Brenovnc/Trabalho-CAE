using System.Globalization;
using System.Text;

namespace StudyPlatform.Api.Services.Modules;

internal static class CrosswordEntryRules
{
    public static IReadOnlyDictionary<string, string[]> Validate(string? word, string? clue, string pathPrefix = "")
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        void Add(string field, string message)
        {
            var path = string.IsNullOrEmpty(pathPrefix) ? field : $"{pathPrefix}.{field}";
            if (!errors.TryGetValue(path, out var messages)) errors[path] = messages = [];
            messages.Add(message);
        }

        var hasWord = !string.IsNullOrWhiteSpace(word);
        var hasClue = !string.IsNullOrWhiteSpace(clue);
        if (hasWord != hasClue)
        {
            if (!hasWord) Add("word", "Informe a palavra junto com a definição.");
            if (!hasClue) Add("clue", "Informe a definição junto com a palavra.");
            return errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
        }
        if (!hasWord) return new Dictionary<string, string[]>();

        var value = word!.Trim();
        if (value.Length > 80) Add("word", "A palavra pode ter no máximo 80 caracteres.");
        var normalized = value.Normalize(NormalizationForm.FormD);
        var letterCount = 0;
        var valid = true;
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetter(character)) letterCount++;
            else if (character is not (' ' or '-')) valid = false;
        }
        if (!valid) Add("word", "Use somente letras, espaços ou hífens na palavra.");
        if (letterCount < 2) Add("word", "A palavra deve ter pelo menos duas letras após normalização.");
        if (clue!.Length > 1000) Add("clue", "A definição pode ter no máximo 1000 caracteres.");
        if (string.IsNullOrWhiteSpace(clue)) Add("clue", "A definição não pode ficar vazia.");
        return errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
    }
}
