using DDF.Mediator;
using DDF.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DDF.Mediator.Tests;

public class ServiceRegistrationTests
{
    private ServiceProvider BuildProvider(params Type[] behaviors)
    {
        var services = new ServiceCollection();
        // 添加日志容器等共享状态
        services.AddSingleton<ExecutionLog>();
        // 显式添加测试处理者/通知处理者，以确保扫描时已加载当前程序集
        services.AddMediator(behaviors);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddMediator_RegistersCoreServices()
    {
        var provider = BuildProvider(typeof(LoggingBehavior), typeof(TimingBehavior));
        Assert.NotNull(provider.GetService<IMediator>());
        Assert.NotNull(provider.GetService<IRequestSender>());
        Assert.NotNull(provider.GetService<INotificationPublisher>());
    }

    [Fact]
    public async Task Request_Handler_Is_Invoked()
    {
        var provider = BuildProvider(typeof(LoggingBehavior), typeof(TimingBehavior));
        var mediator = provider.GetRequiredService<IMediator>();
        var result = await mediator.SendAsync<EchoRequest, string>(new EchoRequest { Message = "Hi" });
        Assert.Equal("Echo:Hi", result);
    }

    [Fact]
    public async Task Notification_All_Handlers_Are_Invoked_By_Generic_Publish()
    {
        FirstNotificationHandler.Calls = 0;
        SecondNotificationHandler.Calls = 0;
        var provider = BuildProvider(typeof(LoggingBehavior), typeof(TimingBehavior));
        var mediator = provider.GetRequiredService<IMediator>();

        await mediator.PublishAsync(new SampleNotification { Name = "N1" });

        Assert.Equal(1, FirstNotificationHandler.Calls);
        Assert.Equal(1, SecondNotificationHandler.Calls);
    }

    [Fact]
    public async Task Notification_All_Handlers_Are_Invoked_By_Reflection_Publish()
    {
        FirstNotificationHandler.Calls = 0;
        SecondNotificationHandler.Calls = 0;
        var provider = BuildProvider(typeof(LoggingBehavior), typeof(TimingBehavior));
        var mediator = provider.GetRequiredService<IMediator>();

        INotification notification = new SampleNotification { Name = "N2" };
        await mediator.PublishAsync(notification);

        Assert.Equal(1, FirstNotificationHandler.Calls);
        Assert.Equal(1, SecondNotificationHandler.Calls);
    }

    [Fact]
    public async Task Pipeline_Behaviors_Respect_Priority_Order()
    {
        var provider = BuildProvider(typeof(LoggingBehavior), typeof(TimingBehavior));
        var log = provider.GetRequiredService<ExecutionLog>();
        var mediator = provider.GetRequiredService<IMediator>();

        var _ = await mediator.SendAsync<EchoRequest, string>(new EchoRequest { Message = "X" });

        // 优先级：Timing(1) 在 Logging(2) 之前进入
        // 调用顺序：Timing-Before -> Logging-Before -> handler -> Logging-After -> Timing-After
        Assert.Equal(new[] { "Timing-Before", "Logging-Before", "Logging-After", "Timing-After" }, log.Steps);
    }

    [Fact]
    public void AddMediator_Throws_When_Behavior_Not_Sealed()
    {
        var services = new ServiceCollection();
        Assert.Throws<Exception>(() => services.AddMediator(typeof(UnsealedBehavior)));
    }

    // 非密封行为（用于异常测试）
    [PipelineBehaviorPriority(5)]
    public class UnsealedBehavior : IPipelineBehavior<EchoRequest, string>
    {
        public Task<string> HandleAsync(EchoRequest request, NextHandlerDelegate<string> next, CancellationToken cancellationToken = default) => next();
    }
}
