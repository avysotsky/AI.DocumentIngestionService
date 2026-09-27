namespace AI.DocumentIngestion.Infrastructure.Embeddings;

public static class EmbeddingMath
{
    public static float[] MeanPoolAndNormalize(
        ReadOnlySpan<float> hiddenStates,
        ReadOnlySpan<long> attentionMask,
        int sequenceLength,
        int dimensions)
    {
        if (sequenceLength <= 0 || dimensions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequenceLength));
        }

        if (hiddenStates.Length != sequenceLength * dimensions)
        {
            throw new ArgumentException("Hidden-state shape does not match sequence dimensions.");
        }

        if (attentionMask.Length != sequenceLength)
        {
            throw new ArgumentException("Attention-mask length does not match the sequence.");
        }

        var result = new float[dimensions];
        var includedTokens = 0;
        for (var token = 0; token < sequenceLength; token++)
        {
            if (attentionMask[token] == 0)
            {
                continue;
            }

            includedTokens++;
            var offset = token * dimensions;
            for (var dimension = 0; dimension < dimensions; dimension++)
            {
                result[dimension] += hiddenStates[offset + dimension];
            }
        }

        if (includedTokens == 0)
        {
            throw new InvalidOperationException("Cannot pool an empty attention mask.");
        }

        double squaredNorm = 0;
        for (var dimension = 0; dimension < dimensions; dimension++)
        {
            result[dimension] /= includedTokens;
            squaredNorm += result[dimension] * result[dimension];
        }

        var norm = Math.Sqrt(squaredNorm);
        if (norm <= double.Epsilon)
        {
            throw new InvalidOperationException("Cannot normalize a zero embedding.");
        }

        for (var dimension = 0; dimension < dimensions; dimension++)
        {
            result[dimension] = (float)(result[dimension] / norm);
        }

        return result;
    }
}
