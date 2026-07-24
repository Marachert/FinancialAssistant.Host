namespace FinancialAssistant.ReceiptProcessing.Application;

public static class OcrProviderErrorCodes
{
    public const string InvalidReceiptContent = "invalid_receipt_content";

    public const string InvalidProviderResponse = "invalid_provider_response";

    public const string ProviderFailure = "provider_failure";

    public const string ProviderTimeout = "provider_timeout";

    public const string ProviderUnavailable = "provider_unavailable";
}

public sealed class OcrProviderException : Exception
{
    private static readonly HashSet<string> AllowedErrorCodes =
        new(StringComparer.Ordinal)
        {
            OcrProviderErrorCodes.InvalidReceiptContent,
            OcrProviderErrorCodes.InvalidProviderResponse,
            OcrProviderErrorCodes.ProviderFailure,
            OcrProviderErrorCodes.ProviderTimeout,
            OcrProviderErrorCodes.ProviderUnavailable
        };

    public OcrProviderException(string errorCode, bool isTransient)
        : base("OCR provider request failed.")
    {
        if (!AllowedErrorCodes.Contains(errorCode))
        {
            throw new ArgumentException(
                "OCR provider error code is not recognized.",
                nameof(errorCode));
        }

        ErrorCode = errorCode;
        IsTransient = isTransient;
    }

    public string ErrorCode { get; }

    public bool IsTransient { get; }
}
