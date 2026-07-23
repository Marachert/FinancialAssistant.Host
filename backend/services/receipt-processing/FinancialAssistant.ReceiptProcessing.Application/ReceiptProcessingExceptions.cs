namespace FinancialAssistant.ReceiptProcessing.Application;

public sealed class UnsupportedReceiptMediaTypeException : InvalidOperationException
{
    public UnsupportedReceiptMediaTypeException()
        : base("Only JPEG, PNG, and WebP receipt images are supported.")
    {
    }
}

public sealed class ReceiptFileTooLargeException : InvalidOperationException
{
    public ReceiptFileTooLargeException(long maximumSizeBytes)
        : base($"Receipt images cannot exceed {maximumSizeBytes} bytes.")
    {
    }
}

public sealed class ReceiptIdempotencyConflictException : InvalidOperationException
{
    public ReceiptIdempotencyConflictException()
        : base("The idempotency key was already used for a different receipt image.")
    {
    }
}
