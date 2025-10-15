using DDF.Mediator.Abstractions;
using DDF.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace DDF.Mediator.Tests;

// 测试用请求与处理者
public sealed class EchoRequest : IRequest<string>
{
    public string? Message { get; init; }
}

public sealed class EchoRequestHandler : IRequestHandler<EchoRequest, string>
{
    public Task<string> HandleAsync(EchoRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult($"Echo:{request.Message}");
}

// 测试用通知与处理者
public sealed class SampleNotification : INotification
{
    public string? Name { get; init; }
}

public sealed class FirstNotificationHandler : INotificationHandler<SampleNotification>
{
    public static int Calls = 0;
    public Task HandleAsync(SampleNotification notification, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref Calls);
        return Task.CompletedTask;
    }
}

public sealed class SecondNotificationHandler : INotificationHandler<SampleNotification>
{
    public static int Calls = 0;
    public Task HandleAsync(SampleNotification notification, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref Calls);
        return Task.CompletedTask;
    }
}

// 管道行为（记录执行顺序）
[PipelineBehaviorPriority(2)]
public sealed class LoggingBehavior : IPipelineBehavior<EchoRequest, string>
{
    private readonly ExecutionLog _log;
    public LoggingBehavior(ExecutionLog log) => _log = log;
    public async Task<string> HandleAsync(EchoRequest request, NextHandlerDelegate<string> next, CancellationToken cancellationToken = default)
    {
        _log.Steps.Add("Logging-Before");
        var resp = await next(cancellationToken);
        _log.Steps.Add("Logging-After");
        return resp;
    }
}

[PipelineBehaviorPriority(1)] // 更高优先级（数字小）
public sealed class TimingBehavior : IPipelineBehavior<EchoRequest, string>
{
    private readonly ExecutionLog _log;
    public TimingBehavior(ExecutionLog log) => _log = log;
    public async Task<string> HandleAsync(EchoRequest request, NextHandlerDelegate<string> next, CancellationToken cancellationToken = default)
    {
        _log.Steps.Add("Timing-Before");
        var resp = await next(cancellationToken);
        _log.Steps.Add("Timing-After");
        return resp;
    }
}

public sealed class ExecutionLog
{
    public List<string> Steps { get; } = new();
}
