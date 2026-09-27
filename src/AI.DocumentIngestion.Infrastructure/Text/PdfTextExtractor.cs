using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace AI.DocumentIngestion.Infrastructure.Text;

public sealed class PdfTextExtractor
{
    private readonly PdfTextExtractionOptions _options;

    public PdfTextExtractor(IOptions<PdfTextExtractionOptions> options)
    {
        _options = options.Value;
    }

    public Task<IReadOnlyList<ExtractedPage>> ExtractAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new ArgumentException("PDF stream must be readable and seekable.", nameof(stream));
        }

        cancellationToken.ThrowIfCancellationRequested();
        stream.Position = 0;

        try
        {
            using var document = PdfDocument.Open(
                stream,
                new ParsingOptions
                {
                    UseLenientParsing = true,
                    SkipMissingFonts = true,
                });

            if (document.IsEncrypted)
            {
                throw new DocumentExtractionException(
                    "Encrypted or password-protected PDF documents are not supported.");
            }

            if (document.NumberOfPages == 0)
            {
                throw new DocumentExtractionException("The PDF contains no pages.");
            }

            if (document.NumberOfPages > _options.MaximumPages)
            {
                throw new DocumentExtractionException(
                    $"The PDF exceeds the {_options.MaximumPages} page limit.");
            }

            var pages = new List<ExtractedPage>(document.NumberOfPages);
            var totalCharacters = 0;
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = TextNormalizer.Normalize(
                    ContentOrderTextExtractor.GetText(
                        page,
                        new ContentOrderTextExtractor.Options
                        {
                            SeparateParagraphsWithDoubleNewline = true,
                            ReplaceWhitespaceWithSpace = false,
                            NegativeGapAsWhitespace = true,
                        }));

                totalCharacters = checked(totalCharacters + text.Length);
                if (totalCharacters > _options.MaximumExtractedCharacters)
                {
                    throw new DocumentExtractionException(
                        $"Extracted PDF text exceeds the {_options.MaximumExtractedCharacters} character limit.");
                }

                pages.Add(new ExtractedPage(page.Number, text));
            }

            if (pages.All(page => string.IsNullOrWhiteSpace(page.Text)))
            {
                throw new DocumentExtractionException(
                    "The PDF contains no extractable text; it may be empty or image-only. OCR is not supported.");
            }

            return Task.FromResult<IReadOnlyList<ExtractedPage>>(pages);
        }
        catch (PdfDocumentEncryptedException exception)
        {
            throw new DocumentExtractionException(
                "Encrypted or password-protected PDF documents are not supported.",
                exception);
        }
        catch (UglyToad.PdfPig.Core.PdfDocumentFormatException exception)
        {
            throw new DocumentExtractionException(
                "The PDF is invalid or corrupted and could not be parsed.",
                exception);
        }
        catch (Exception exception) when (
            exception is not DocumentExtractionException and
            not OperationCanceledException and
            not OutOfMemoryException)
        {
            throw new DocumentExtractionException(
                "The PDF is invalid, corrupted, or uses unsupported PDF features.",
                exception);
        }
        finally
        {
            stream.Position = 0;
        }
    }
}
