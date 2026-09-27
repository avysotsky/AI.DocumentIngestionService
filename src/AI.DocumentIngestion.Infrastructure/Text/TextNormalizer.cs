using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AI.DocumentIngestion.Infrastructure.Text;

public static partial class TextNormalizer
{
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var normalized = text.Normalize(NormalizationForm.FormC).ReplaceLineEndings("\n");
        var builder = new StringBuilder(normalized.Length);
        foreach (var rune in normalized.EnumerateRunes())
        {
            if (rune.Value is '\n' or '\t' ||
                Rune.GetUnicodeCategory(rune) is not (
                    UnicodeCategory.Control or
                    UnicodeCategory.Surrogate or
                    UnicodeCategory.OtherNotAssigned))
            {
                builder.Append(rune.ToString());
            }
        }

        var lines = builder.ToString().Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            lines[index] = HorizontalWhitespace().Replace(lines[index], " ").Trim();
        }

        return ExcessBlankLines().Replace(string.Join('\n', lines), "\n\n").Trim();
    }

    [GeneratedRegex(@"[\p{Zs}\t]+", RegexOptions.CultureInvariant)]
    private static partial Regex HorizontalWhitespace();

    [GeneratedRegex(@"\n(?:\s*\n){2,}", RegexOptions.CultureInvariant)]
    private static partial Regex ExcessBlankLines();
}
