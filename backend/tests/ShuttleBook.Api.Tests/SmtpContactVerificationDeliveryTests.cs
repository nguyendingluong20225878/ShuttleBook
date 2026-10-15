using Microsoft.Extensions.Configuration;
using ShuttleBook.Infrastructure.Identity;

namespace ShuttleBook.Api.Tests;

public sealed class SmtpContactVerificationDeliveryTests
{
    [Fact]
    public async Task Phone_delivery_is_rejected_without_synthetic_phone_setting()
    {
        var delivery = new SmtpContactVerificationDelivery(new ConfigurationBuilder().Build());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            delivery.DeliverAsync(ContactType.Phone, "+84901234567", "123456", CancellationToken.None));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Email_delivery_requires_both_starttls_and_credentials_before_network(bool startTls, bool credentials)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Identity:Delivery:SmtpHost"] = "localhost",
            ["Identity:Delivery:SmtpPort"] = "587",
            ["Identity:Delivery:From"] = "no-reply@example.test",
            ["Identity:Delivery:SmtpUseStartTls"] = startTls.ToString(),
            ["Identity:Delivery:RequireTlsAndAuth"] = "true",
            ["Identity:Delivery:SmtpUsername"] = credentials ? "smtp-user" : null,
            ["Identity:Delivery:SmtpPassword"] = credentials ? "smtp-password" : null
        };
        var delivery = new SmtpContactVerificationDelivery(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            delivery.DeliverAsync(ContactType.Email, "recipient@example.test", "123456", CancellationToken.None));
    }
}
