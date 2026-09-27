namespace AI.DocumentIngestion.Infrastructure.Embeddings;

public static class XlmRobertaInputBuilder
{
    public const long BeginningOfSentenceTokenId = 0;
    public const long PaddingTokenId = 1;
    public const long EndOfSentenceTokenId = 2;

    public static long[] AddSpecialTokensAndTruncate(
        IReadOnlyList<uint> tokenIds,
        int maximumLength)
    {
        ArgumentNullException.ThrowIfNull(tokenIds);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumLength, 2);

        var contentLength = Math.Min(tokenIds.Count, maximumLength - 2);
        var result = new long[contentLength + 2];
        result[0] = BeginningOfSentenceTokenId;
        for (var index = 0; index < contentLength; index++)
        {
            result[index + 1] = tokenIds[index];
        }

        result[^1] = EndOfSentenceTokenId;
        return result;
    }
}
