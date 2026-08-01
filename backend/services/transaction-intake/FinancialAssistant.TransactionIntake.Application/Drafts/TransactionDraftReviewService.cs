using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Contracts;
using FinancialAssistant.TransactionIntake.Domain.Drafts;

namespace FinancialAssistant.TransactionIntake.Application.Drafts;

public sealed class TransactionDraftReviewService : ITransactionDraftReviewService
{
    private const int MaximumMutationAttempts = 5;
    private const int MaximumNoteLength = 500;

    private readonly ITransactionDraftStore draftStore;
    private readonly TransactionDraftValidator validator;

    public TransactionDraftReviewService(
        ITransactionDraftStore draftStore,
        TransactionDraftValidator validator)
    {
        this.draftStore = draftStore;
        this.validator = validator;
    }

    public async Task<TransactionDraftResponse?> ReviewAsync(
        string userId,
        string draftId,
        CancellationToken cancellationToken)
    {
        var draft = await draftStore.GetByIdAsync(
            NormalizeRequired(userId, nameof(userId)),
            NormalizeRequired(draftId, nameof(draftId)),
            cancellationToken);
        return draft is null ? null : ToResponse(draft);
    }

    public async Task<TransactionDraftResponse?> UpdateAsync(
        string userId,
        string draftId,
        TransactionDraftUpdateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedUserId = NormalizeRequired(userId, nameof(userId));
        var normalizedDraftId = NormalizeRequired(draftId, nameof(draftId));
        for (var attempt = 0; attempt < MaximumMutationAttempts; attempt++)
        {
            var current = await draftStore.GetByIdAsync(
                normalizedUserId,
                normalizedDraftId,
                cancellationToken);
            if (current is null)
            {
                return null;
            }

            EnsureEditable(current);
            var validated = validator.Validate(
                current.Id,
                current.UserId,
                current.InputFingerprint,
                new ParsedTransactionCandidate(
                    request.Type,
                    request.Amount,
                    request.Currency,
                    request.CategoryId,
                    request.Merchant,
                    request.Date,
                    Confidence: 1m),
                current.CreatedAtUtc,
                new TransactionDraftSuggestionContext(
                    current.Suggestion.Source,
                    current.Suggestion.SourceReferenceId,
                    Ambiguities: Array.Empty<string>(),
                    MissingFields: Array.Empty<string>(),
                    ReviewMessage: "User-reviewed values are ready for confirmation."));
            var replacement = validated with
            {
                InputSource = current.InputSource,
                Note = NormalizeNote(request.Note),
                Status = TransactionDraftStatuses.Draft,
                Revision = current.Revision + 1
            };
            var result = await draftStore.ReplaceAsync(
                normalizedUserId,
                normalizedDraftId,
                current.Revision,
                replacement,
                cancellationToken);
            if (result.Replaced)
            {
                return ToResponse(result.Draft!);
            }
        }

        throw new DraftMutationConflictException();
    }

    public async Task<TransactionDraftResponse?> RejectAsync(
        string userId,
        string draftId,
        CancellationToken cancellationToken)
    {
        var normalizedUserId = NormalizeRequired(userId, nameof(userId));
        var normalizedDraftId = NormalizeRequired(draftId, nameof(draftId));
        for (var attempt = 0; attempt < MaximumMutationAttempts; attempt++)
        {
            var current = await draftStore.GetByIdAsync(
                normalizedUserId,
                normalizedDraftId,
                cancellationToken);
            if (current is null)
            {
                return null;
            }

            if (current.Status == TransactionDraftStatuses.Rejected)
            {
                return ToResponse(current);
            }

            EnsureEditable(current);
            var replacement = current with
            {
                Status = TransactionDraftStatuses.Rejected,
                Revision = current.Revision + 1
            };
            var result = await draftStore.ReplaceAsync(
                normalizedUserId,
                normalizedDraftId,
                current.Revision,
                replacement,
                cancellationToken);
            if (result.Replaced)
            {
                return ToResponse(result.Draft!);
            }
        }

        throw new DraftMutationConflictException();
    }

    private static void EnsureEditable(TransactionDraft draft)
    {
        if (draft.Status != TransactionDraftStatuses.Draft)
        {
            throw new DraftNotEditableException(draft.Status);
        }
    }

    private static string? NormalizeNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        var normalized = string.Join(
            ' ',
            note.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length > MaximumNoteLength)
        {
            throw new ArgumentException(
                $"Note cannot exceed {MaximumNoteLength} characters.",
                nameof(note));
        }

        return normalized;
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > 200)
        {
            throw new ArgumentException("Value cannot exceed 200 characters.", parameterName);
        }

        return normalized;
    }

    internal static TransactionDraftResponse ToResponse(TransactionDraft draft) =>
        new(
            draft.Id,
            draft.Status,
            draft.InputSource,
            draft.Type,
            draft.Amount,
            draft.Currency,
            draft.CategoryId,
            draft.Merchant,
            draft.Date,
            draft.Confidence,
            draft.Ambiguities,
            draft.RequiresReview,
            new TransactionDraftSuggestionMetadataResponse(
                draft.Suggestion.Source,
                draft.Suggestion.SourceReferenceId,
                draft.Suggestion.OutputAuthority,
                draft.Suggestion.Confidence,
                draft.Suggestion.Ambiguities,
                draft.Suggestion.MissingFields,
                draft.Suggestion.ReviewMessage),
            draft.CreatedAtUtc,
            draft.Note);
}
