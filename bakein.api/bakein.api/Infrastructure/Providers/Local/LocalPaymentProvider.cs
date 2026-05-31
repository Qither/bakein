using Bakein.Api.Application.Providers;

namespace Bakein.Api.Infrastructure.Providers.Local;

public sealed class LocalPaymentProvider : IPaymentProvider
{
    public Task<PaymentIntentResult> CreatePaymentIntentAsync(PaymentIntentCommand command, CancellationToken cancellationToken)
    {
        var providerIntentId = $"local-pay-{command.PaymentIntentId:N}";
        var result = new PaymentIntentResult(
            "local",
            providerIntentId,
            $"local_secret_{command.PaymentIntentId:N}",
            DateTimeOffset.UtcNow.AddMinutes(30));

        return Task.FromResult(result);
    }
}
