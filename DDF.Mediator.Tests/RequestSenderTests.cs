using DDF.Mediator;
using DDF.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DDF.Mediator.Tests;

public class RequestSenderTests
{
    [Fact]
    public async Task SendAsync_Throws_When_Request_Null()
    {
        var services = new ServiceCollection();
        services.AddMediator(typeof(LoggingBehavior), typeof(TimingBehavior));
        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<IRequestSender>();
        await Assert.ThrowsAsync<ArgumentNullException>(() => sender.SendAsync<EchoRequest, string>(null!));
    }

    [Fact]
    public async Task SendAsync_Executes_Behaviors_In_Priority_Order()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ExecutionLog>();
        services.AddMediator(typeof(LoggingBehavior), typeof(TimingBehavior));
        var provider = services.BuildServiceProvider();
        var log = provider.GetRequiredService<ExecutionLog>();
        var sender = provider.GetRequiredService<IRequestSender>();

        var result = await sender.SendAsync<EchoRequest, string>(new EchoRequest { Message = "Order" });
        Assert.Equal("Echo:Order", result);
        Assert.Equal(new[] { "Timing-Before", "Logging-Before", "Logging-After", "Timing-After" }, log.Steps);
    }

    [Fact]
    public async Task SendAsync_Throws_When_Behavior_Missing_Priority()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ExecutionLog>();
        services.AddMediator(typeof(MissingPriorityBehavior));
        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<IRequestSender>();

        await Assert.ThrowsAsync<Exception>(() => sender.SendAsync<EchoRequest, string>(new EchoRequest { Message = "X" }));
    }

    public sealed class MissingPriorityBehavior : IPipelineBehavior<EchoRequest, string>
    {
        public Task<string> HandleAsync(EchoRequest request, NextHandlerDelegate<string> next, CancellationToken cancellationToken = default) => next(cancellationToken);
    }
}
