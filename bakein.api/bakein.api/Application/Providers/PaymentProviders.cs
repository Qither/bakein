namespace Bakein.Api.Application.Providers;

public interface IPaymentProvider
{
    Task<PaymentIntentResult> CreatePaymentIntentAsync(PaymentIntentCommand command, CancellationToken cancellationToken);
}

public sealed record PaymentIntentCommand(
    Guid PaymentIntentId,
    Guid OrderId,
    Guid AccountId,
    int AmountCents,
    string Currency);

public sealed record PaymentIntentResult(
    string Provider,
    string ProviderIntentId,
    string ClientSecret,
    DateTimeOffset ExpiresAt);
