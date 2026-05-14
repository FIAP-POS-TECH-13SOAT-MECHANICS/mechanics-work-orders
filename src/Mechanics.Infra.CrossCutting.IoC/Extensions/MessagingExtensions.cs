using Mechanics.Application.WorkOrders.Consumers;
using Mechanics.Infra.Messaging.Extensions;
using Mechanics.Infra.Messaging.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mechanics.Infra.CrossCutting.IoC.Extensions;

public static class MessagingExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AwsCredentialsOptions>(configuration.GetSection("AwsCredentials"));
        services.Configure<MessagingOptions>(configuration.GetSection(nameof(MessagingOptions)));

        services.AddMessaging(builder =>
        {
            if (configuration.GetSection(nameof(MessagingOptions)).Get<MessagingOptions>()!.DisableConsumers)
                return;

            builder.AddConsumer<WorkOrderStatusChangedConsumer, WorkOrderStatusChangedEvent>();
            builder.AddConsumer<PaymentApprovedConsumer, PaymentApprovedEvent>();
        });

        return services;
    }
}
