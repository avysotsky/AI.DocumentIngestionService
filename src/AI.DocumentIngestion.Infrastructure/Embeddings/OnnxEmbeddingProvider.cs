using AI.DocumentIngestion.Application.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Tokenizers.HuggingFace.Tokenizer;

namespace AI.DocumentIngestion.Infrastructure.Embeddings;

public sealed class OnnxEmbeddingProvider : IEmbeddingProvider, IDisposable
{
    private const string ModelFileName = "model.onnx";
    private const string TokenizerFileName = "tokenizer.json";

    private readonly OnnxEmbeddingOptions _options;
    private readonly Tokenizer _tokenizer;
    private readonly InferenceSession _session;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _tokenizerLock = new();

    public OnnxEmbeddingProvider(IOptions<OnnxEmbeddingOptions> options)
    {
        _options = options.Value;
        var modelDirectory = Path.GetFullPath(_options.ModelPath);
        var modelPath = Path.Combine(modelDirectory, ModelFileName);
        var tokenizerPath = Path.Combine(modelDirectory, TokenizerFileName);

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                $"ONNX embedding model was not found at '{modelPath}'.", modelPath);
        }

        if (!File.Exists(tokenizerPath))
        {
            throw new FileNotFoundException(
                $"XLM-R tokenizer was not found at '{tokenizerPath}'.", tokenizerPath);
        }

        _tokenizer = Tokenizer.FromFile(tokenizerPath);
        _session = new InferenceSession(modelPath);
        ValidateModel();
    }

    public int MaximumInputTokenCount => _options.MaxTokenLength;

    public int CountTokens(string text, EmbeddingInputKind inputKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        lock (_tokenizerLock)
        {
            return EncodeWithoutSpecialTokens(text, inputKind).Count + 2;
        }
    }

    public async Task<IReadOnlyList<EmbeddingResult>> GenerateAsync(
        IReadOnlyList<string> texts,
        EmbeddingInputKind inputKind,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(texts);
        if (texts.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Embedding input cannot be empty.", nameof(texts));
        }

        if (texts.Count == 0)
        {
            return [];
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var results = new List<EmbeddingResult>(texts.Count);
            for (var offset = 0; offset < texts.Count; offset += _options.BatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = Math.Min(_options.BatchSize, texts.Count - offset);
                RunBatch(texts, offset, count, inputKind, results);
            }

            return results;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void RunBatch(
        IReadOnlyList<string> texts,
        int offset,
        int count,
        EmbeddingInputKind inputKind,
        List<EmbeddingResult> results)
    {
        var sequences = new long[count][];
        var maximumSequenceLength = 0;
        for (var item = 0; item < count; item++)
        {
            Google.Protobuf.Collections.RepeatedField<uint> tokenIds;
            lock (_tokenizerLock)
            {
                tokenIds = EncodeWithoutSpecialTokens(texts[offset + item], inputKind);
            }

            sequences[item] = XlmRobertaInputBuilder.AddSpecialTokensAndTruncate(
                tokenIds,
                _options.MaxTokenLength);
            maximumSequenceLength = Math.Max(maximumSequenceLength, sequences[item].Length);
        }

        var inputIds = new DenseTensor<long>([count, maximumSequenceLength]);
        var attentionMask = new DenseTensor<long>([count, maximumSequenceLength]);
        for (var item = 0; item < count; item++)
        {
            for (var token = 0; token < maximumSequenceLength; token++)
            {
                if (token < sequences[item].Length)
                {
                    inputIds[item, token] = sequences[item][token];
                    attentionMask[item, token] = 1;
                }
                else
                {
                    inputIds[item, token] = XlmRobertaInputBuilder.PaddingTokenId;
                    attentionMask[item, token] = 0;
                }
            }
        }

        using var inferenceResults = _session.Run(
        [
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask),
        ]);
        var output = inferenceResults[0].AsTensor<float>();
        if (output.Dimensions.Length != 3 ||
            output.Dimensions[0] != count ||
            output.Dimensions[1] != maximumSequenceLength ||
            output.Dimensions[2] != OnnxEmbeddingOptions.Dimensions)
        {
            throw new InvalidOperationException(
                $"Unexpected ONNX output shape: [{string.Join(',', output.Dimensions.ToArray())}].");
        }

        var outputValues = output.ToArray();
        var itemSize = maximumSequenceLength * OnnxEmbeddingOptions.Dimensions;
        var maskValues = attentionMask.ToArray();
        for (var item = 0; item < count; item++)
        {
            var vector = EmbeddingMath.MeanPoolAndNormalize(
                outputValues.AsSpan(item * itemSize, itemSize),
                maskValues.AsSpan(item * maximumSequenceLength, maximumSequenceLength),
                maximumSequenceLength,
                OnnxEmbeddingOptions.Dimensions);
            results.Add(new EmbeddingResult(vector, sequences[item].Length));
        }
    }

    private Google.Protobuf.Collections.RepeatedField<uint> EncodeWithoutSpecialTokens(string text, EmbeddingInputKind inputKind)
    {
        var prefix = inputKind == EmbeddingInputKind.Query ? "query: " : "passage: ";
        return _tokenizer.Encode(prefix + text, false).Encodings[0].Ids;
    }

    private void ValidateModel()
    {
        if (!_session.InputMetadata.ContainsKey("input_ids") ||
            !_session.InputMetadata.ContainsKey("attention_mask"))
        {
            throw new InvalidOperationException(
                "The ONNX model must expose input_ids and attention_mask inputs.");
        }

        var outputDimensions = _session.OutputMetadata.Values.First().Dimensions;
        if (outputDimensions.Length != 3 ||
            outputDimensions[^1] != OnnxEmbeddingOptions.Dimensions)
        {
            throw new InvalidOperationException(
                $"The ONNX model must output {OnnxEmbeddingOptions.Dimensions}-dimensional token embeddings.");
        }
    }

    public void Dispose()
    {
        _session.Dispose();
        _gate.Dispose();
    }
}
