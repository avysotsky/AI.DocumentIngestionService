namespace AI.DocumentIngestion.Infrastructure.Text;

public sealed record TextChunk(string Text, int? PageNumber);

public static class TextChunker
{
    public const int MaximumChunkLength = 1_500;
    public const int MaximumChunkCount = 10_000;

    public static IReadOnlyList<TextChunk> ChunkPages(
        IReadOnlyList<ExtractedPage> pages,
        Func<string, int> tokenCounter,
        int maximumTokens)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ValidateTokenBudget(tokenCounter, maximumTokens);

        var chunks = new List<TextChunk>();
        foreach (var page in pages)
        {
            AddChunks(page.Text, page.PageNumber, tokenCounter, maximumTokens, chunks);
        }

        return chunks;
    }

    public static IReadOnlyList<TextChunk> ChunkText(
        string text,
        Func<string, int> tokenCounter,
        int maximumTokens)
    {
        ValidateTokenBudget(tokenCounter, maximumTokens);

        var chunks = new List<TextChunk>();
        AddChunks(text, null, tokenCounter, maximumTokens, chunks);
        return chunks;
    }

    private static void AddChunks(
        string text,
        int? pageNumber,
        Func<string, int> tokenCounter,
        int maximumTokens,
        List<TextChunk> chunks)
    {
        var normalized = TextNormalizer.Normalize(text);
        for (var offset = 0; offset < normalized.Length;)
        {
            var length = Math.Min(MaximumChunkLength, normalized.Length - offset);
            length = FindMaximumTokenBoundLength(
                normalized,
                offset,
                length,
                tokenCounter,
                maximumTokens);

            if (offset + length < normalized.Length)
            {
                var breakAt = FindNaturalBreak(normalized, offset, length);
                if (breakAt >= offset)
                {
                    length = breakAt - offset + 1;
                }
            }

            var chunk = normalized.Substring(offset, length).Trim();
            if (chunk.Length > 0)
            {
                if (chunks.Count >= MaximumChunkCount)
                {
                    throw new DocumentExtractionException(
                        $"Document produces more than {MaximumChunkCount} chunks.");
                }

                if (tokenCounter(chunk) > maximumTokens)
                {
                    throw new DocumentExtractionException(
                        "A document chunk exceeds the embedding model token limit.");
                }

                chunks.Add(new TextChunk(chunk, pageNumber));
            }

            offset += length;
        }
    }

    private static int FindMaximumTokenBoundLength(
        string text,
        int offset,
        int maximumLength,
        Func<string, int> tokenCounter,
        int maximumTokens)
    {
        maximumLength = AlignToRuneBoundary(text, offset, maximumLength);
        if (tokenCounter(text.Substring(offset, maximumLength)) <= maximumTokens)
        {
            return maximumLength;
        }

        var low = 1;
        var high = maximumLength - 1;
        var best = 0;
        while (low <= high)
        {
            var middle = AlignToRuneBoundary(
                text,
                offset,
                low + ((high - low) / 2));
            if (middle == 0)
            {
                low = 2;
                continue;
            }

            var tokenCount = tokenCounter(text.Substring(offset, middle));
            if (tokenCount <= maximumTokens)
            {
                best = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        if (best == 0)
        {
            throw new DocumentExtractionException(
                "The embedding model token limit is too small for the document text.");
        }

        return best;
    }

    private static int AlignToRuneBoundary(string text, int offset, int length)
    {
        var boundary = offset + length;
        return boundary < text.Length &&
               length > 0 &&
               char.IsHighSurrogate(text[boundary - 1]) &&
               char.IsLowSurrogate(text[boundary])
            ? length - 1
            : length;
    }

    private static int FindNaturalBreak(string text, int offset, int length)
    {
        var minimum = offset + (length / 2);
        for (var index = offset + length - 1; index >= minimum; index--)
        {
            if (text[index] == '\n' ||
                (char.IsWhiteSpace(text[index]) && index > offset && IsSentenceEnd(text[index - 1])))
            {
                return index;
            }
        }

        return text.LastIndexOfAny(['\n', ' ', '\t'], offset + length - 1, length);
    }

    private static void ValidateTokenBudget(
        Func<string, int> tokenCounter,
        int maximumTokens)
    {
        ArgumentNullException.ThrowIfNull(tokenCounter);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumTokens, 3);
    }

    private static bool IsSentenceEnd(char value) => value is '.' or '!' or '?' or ';' or ':';
}
