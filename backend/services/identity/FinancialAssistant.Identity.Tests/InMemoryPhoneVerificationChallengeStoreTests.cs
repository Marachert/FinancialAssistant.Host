using FinancialAssistant.Identity.Application.Phone;
using FinancialAssistant.Identity.Infrastructure.Authentication;

namespace FinancialAssistant.Identity.Tests;

public sealed class InMemoryPhoneVerificationChallengeStoreTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 7, 8, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PendingDispatch_CannotBeReplacedAfterCooldownBeforeActivation()
    {
        var store = new InMemoryPhoneVerificationChallengeStore();
        var policy = CreatePolicy();
        var first = CreateChallenge("first", BaseTime);
        var second = CreateChallenge("second", BaseTime.AddSeconds(31));

        var firstReservation = await store.TryReserveAsync(first, policy);
        var secondReservation = await store.TryReserveAsync(second, policy);
        var activated = await store.TryActivateAsync(
            first.Id,
            "provider-reference",
            BaseTime.AddSeconds(32));
        var stored = await store.FindAsync(first.Id);

        Assert.True(firstReservation.Reserved);
        Assert.False(secondReservation.Reserved);
        Assert.True(secondReservation.RetryAfterSeconds > 0);
        Assert.True(activated);
        Assert.NotNull(stored);
        Assert.Equal(PhoneVerificationChallengeStatus.Active, stored.Status);
        Assert.Equal("provider-reference", stored.ProviderReference);
    }

    [Fact]
    public async Task ExpiredPendingDispatch_CannotActivateAndDoesNotReturnUsableChallenge()
    {
        var store = new InMemoryPhoneVerificationChallengeStore();
        var policy = CreatePolicy();
        var challenge = CreateChallenge("expired", BaseTime);

        var reservation = await store.TryReserveAsync(challenge, policy);
        var activated = await store.TryActivateAsync(
            challenge.Id,
            "provider-reference",
            challenge.ExpiresAtUtc);

        Assert.True(reservation.Reserved);
        Assert.False(activated);
    }

    [Fact]
    public async Task FailedDispatch_RemainsInsideCooldown()
    {
        var store = new InMemoryPhoneVerificationChallengeStore();
        var policy = CreatePolicy();
        var first = CreateChallenge("first", BaseTime);
        var retry = CreateChallenge("retry", BaseTime.AddSeconds(5));

        Assert.True((await store.TryReserveAsync(first, policy)).Reserved);
        await store.CancelAsync(first.Id);
        var retryReservation = await store.TryReserveAsync(retry, policy);

        Assert.False(retryReservation.Reserved);
        Assert.Equal(25, retryReservation.RetryAfterSeconds);
    }

    [Fact]
    public async Task FailedDispatch_RemainsInsideHourlyCountersAfterCooldown()
    {
        var store = new InMemoryPhoneVerificationChallengeStore();
        var policy = CreatePolicy(maximumStartsPerPhone: 1, maximumStartsPerClient: 1);
        var first = CreateChallenge("first", BaseTime);
        var retry = CreateChallenge("retry", BaseTime.AddSeconds(31));

        Assert.True((await store.TryReserveAsync(first, policy)).Reserved);
        await store.CancelAsync(first.Id);
        var retryReservation = await store.TryReserveAsync(retry, policy);

        Assert.False(retryReservation.Reserved);
        Assert.Equal(3569, retryReservation.RetryAfterSeconds);
    }

    private static PhoneVerificationPolicy CreatePolicy(
        int maximumStartsPerPhone = 5,
        int maximumStartsPerClient = 10) =>
        new(
            TimeSpan.FromMinutes(10),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromHours(1),
            5,
            maximumStartsPerPhone,
            maximumStartsPerClient,
            6);

    private static PhoneVerificationChallengeRecord CreateChallenge(
        string id,
        DateTimeOffset createdAtUtc) =>
        new(
            id,
            "phone-hash",
            "client-hash",
            "sign_in",
            null,
            createdAtUtc,
            createdAtUtc.AddMinutes(10),
            createdAtUtc.AddSeconds(30),
            0,
            PhoneVerificationChallengeStatus.PendingDispatch);
}
