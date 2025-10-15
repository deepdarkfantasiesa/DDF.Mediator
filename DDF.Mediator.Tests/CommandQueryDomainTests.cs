using DDF.Mediator;
using DDF.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using Xunit;

namespace DDF.Mediator.Tests;

#region Command implementations
public sealed record CreateOrderCommand(string OrderId) : ICommand<string>;
public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, string>
{
    public Task<string> HandleAsync(CreateOrderCommand request, CancellationToken cancellationToken = default)
        => Task.FromResult($"Created:{request.OrderId}");
}

public sealed record LockedCreateOrderCommand(string OrderId) : ICommandLock<string>,ICommand<string>;
public sealed class LockedCreateOrderCommandHandler : ICommandHandler<LockedCreateOrderCommand, string>
{
    public Task<string> HandleAsync(LockedCreateOrderCommand request, CancellationToken cancellationToken = default)
        => Task.FromResult($"LockedCreated:{request.OrderId}");
}
#endregion

#region Query implementations
public sealed class OrderQueryParams : IQueryParam
{
    public string? OrderId { get; set; }
}

public sealed record GetOrderQuery(OrderQueryParams Params) : IQuery<OrderQueryParams, string>;
public sealed class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, OrderQueryParams, string>
{
    public Task<string> HandleAsync(GetOrderQuery request, CancellationToken cancellationToken = default)
        => Task.FromResult($"Order:{request.Params.OrderId}");
}

public sealed record GetOrderCachedQueryParams(string OrderId) : IQueryParam;

public sealed record GetOrderCachedQuery(string OrderId) : IQueryCache<string>,IQuery<GetOrderCachedQueryParams,string>;
public sealed class GetOrderCachedQueryHandler : IQueryHandler<GetOrderCachedQuery, GetOrderCachedQueryParams, string>
{
    public Task<string> HandleAsync(GetOrderCachedQuery request, CancellationToken cancellationToken = default)
        => Task.FromResult($"OrderCached:{request.OrderId}");
}
#endregion

#region Domain events
public sealed record OrderCreatedDomainEvent(string OrderId) : IDomainEvent;
public sealed class OrderCreatedDomainEventHandler1 : IDomainEventHandler<OrderCreatedDomainEvent>
{
    public static int Calls; 
    public Task HandleAsync(OrderCreatedDomainEvent notification, CancellationToken cancellationToken = default)
    { Interlocked.Increment(ref Calls); return Task.CompletedTask; }
}
public sealed class OrderCreatedDomainEventHandler2 : IDomainEventHandler<OrderCreatedDomainEvent>
{
    public static int Calls; 
    public Task HandleAsync(OrderCreatedDomainEvent notification, CancellationToken cancellationToken = default)
    { Interlocked.Increment(ref Calls); return Task.CompletedTask; }
}

public sealed record OrderCreatedDomainEventV2(string OrderId): IDomainEvent;
public sealed class OrderCreatedDomainEventHandler1V2: IDomainEventHandler<OrderCreatedDomainEventV2>
{
	public static int Calls;
	public Task HandleAsync(OrderCreatedDomainEventV2 notification, CancellationToken cancellationToken = default)
	{ Interlocked.Increment(ref Calls); return Task.CompletedTask; }
}
public sealed class OrderCreatedDomainEventHandler2V2: IDomainEventHandler<OrderCreatedDomainEventV2>
{
	public static int Calls;
	public Task HandleAsync(OrderCreatedDomainEventV2 notification, CancellationToken cancellationToken = default)
	{ Interlocked.Increment(ref Calls); return Task.CompletedTask; }
}

#endregion

#region Behaviors
[PipelineBehaviorPriority(2)]
public sealed class DistributedLockBehavior<TCommandLock, TResponse> : IPipelineBehavior<TCommandLock, TResponse>
    where TCommandLock : ICommandLock<TResponse>
{
    private readonly BehaviorLog _log; public DistributedLockBehavior(BehaviorLog log)=>_log=log;
    public async Task<TResponse> HandleAsync(TCommandLock request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
    { _log.Steps.Add("Lock-Before"); var resp = await next(cancellationToken); _log.Steps.Add("Lock-After"); return resp; }
}

[PipelineBehaviorPriority(3)]
public sealed class TransactionBehavior<TCommand, TResponse> : IPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private readonly BehaviorLog _log; public TransactionBehavior(BehaviorLog log)=>_log=log;
    public async Task<TResponse> HandleAsync(TCommand request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
    { _log.Steps.Add("Tx-Before"); var resp = await next(cancellationToken); _log.Steps.Add("Tx-After"); return resp; }
}

[PipelineBehaviorPriority(1)]
public sealed class ValidatorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly BehaviorLog _log; public ValidatorBehavior(BehaviorLog log)=>_log=log;
    public async Task<TResponse> HandleAsync(TRequest request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
    { _log.Steps.Add("Validator"); return await next(cancellationToken); }
}

[PipelineBehaviorPriority(2)]
public sealed class QueryCacheBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IQueryCache<TResponse>
{
    private readonly BehaviorLog _log;
    public QueryCacheBehavior(BehaviorLog log) => _log = log;
    public Task<TResponse> HandleAsync(TRequest request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
    { 
        _log.Steps.Add("Cache-Hit?Miss->Next"); 
        return next(cancellationToken);
    }
}

public sealed class BehaviorLog { public List<string> Steps { get; } = new(); }
#endregion

public class CommandQueryDomainTests
{
    private ServiceProvider BuildServices() {
        var services = new ServiceCollection();
        services.AddSingleton<BehaviorLog>();
		services.AddMediator(
            typeof(QueryCacheBehavior<,>),
		    typeof(ValidatorBehavior<,>),
		    typeof(TransactionBehavior<,>),
		    typeof(DistributedLockBehavior<,>));
		return services.BuildServiceProvider();
    }

    private ServiceProvider BuildForLockedCommands() {
        var services = new ServiceCollection();
        services.AddSingleton<BehaviorLog>();
        services.AddMediator(
            typeof(ValidatorBehavior<,>),
            typeof(TransactionBehavior<,>),
            typeof(DistributedLockBehavior<,>));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Command_Pipeline_Order_No_Lock()
    {
        var sp = BuildServices();
        var log = sp.GetRequiredService<BehaviorLog>();
        var sender = sp.GetRequiredService<IRequestSender>();
        var result = await sender.SendAsync<CreateOrderCommand,string>(new CreateOrderCommand("A1"));
        Assert.Equal("Created:A1", result);
        // 优先级 Level 小的先进入：Validator(1) -> Tx(2)
        Assert.Equal(new[]{"Validator","Tx-Before","Tx-After"}, log.Steps);
    }

    [Fact]
    public async Task Command_Pipeline_Order_With_Lock()
    {
        var sp = BuildForLockedCommands();
        var log = sp.GetRequiredService<BehaviorLog>();
        var sender = sp.GetRequiredService<IRequestSender>();
        var result = await sender.SendAsync<LockedCreateOrderCommand,string>(new LockedCreateOrderCommand("B1"));
        Assert.Equal("LockedCreated:B1", result);
        // 优先级: Validator(1) -> Tx(2) -> Lock(3)
        Assert.Equal(new[] { "Validator", "Lock-Before", "Tx-Before", "Tx-After", "Lock-After" }, log.Steps);
    }

    [Fact]
    public async Task Query_Pipeline_Order_No_Cache()
    {
        var sp = BuildServices();
        var log = sp.GetRequiredService<BehaviorLog>();
        var sender = sp.GetRequiredService<IRequestSender>();
        var result = await sender.SendAsync<GetOrderQuery,string>(new GetOrderQuery(new OrderQueryParams{OrderId="Q1"}));
        Assert.Equal("Order:Q1", result);
        Assert.Equal(new[]{"Validator"}, log.Steps);
    }

    [Fact]
    public async Task Query_Pipeline_Order_With_Cache()
    {
        var sp = BuildServices();
        var log = sp.GetRequiredService<BehaviorLog>();
        var sender = sp.GetRequiredService<IRequestSender>();
        var result = await sender.SendAsync<GetOrderCachedQuery,string>(new GetOrderCachedQuery("CQ1"));
        Assert.Equal("OrderCached:CQ1", result);
        // 优先级: Validator(1) -> Cache(4)
        Assert.Equal(new[]{"Validator","Cache-Hit?Miss->Next"}, log.Steps);
    }

    [Fact]
    public async Task DomainEvent_Publish_Generic_And_Reflection()
    {
        OrderCreatedDomainEventHandler1.Calls = 0; 
        OrderCreatedDomainEventHandler2.Calls = 0;
        var sp = BuildServices();
        var mediator = sp.GetRequiredService<IMediator>();
        var ev = new OrderCreatedDomainEvent("E1");
        await mediator.PublishAsync((INotification)ev); // reflection path
        await mediator.PublishAsync<OrderCreatedDomainEvent>(ev); // generic path
		await mediator.PublishAsync(ev); // generic path
		
        // 每种路径都会调用全部 handler
		Assert.Equal(3, OrderCreatedDomainEventHandler1.Calls);
        Assert.Equal(3, OrderCreatedDomainEventHandler2.Calls);
    }

    [Fact]
    public void Service_Registration_For_Command_Query_DomainEvent()
    {
        var sp = BuildServices();
        // Handlers should be resolvable via reflection publish; direct resolution test:
        var handler1 = sp.GetServices<INotificationHandler<OrderCreatedDomainEvent>>();
        Assert.True(handler1.Any());
    }

    [Fact]
    public async Task PublishDomainEventList()
    {
        var sp = BuildServices();
        var _mediator = sp.GetRequiredService<IMediator>();
        var domianEvents = new List<IDomainEvent>
        {
            new OrderCreatedDomainEvent("DE1"),

			new OrderCreatedDomainEventV2("DE1"),
		};

        foreach (var de in domianEvents)
        {
            await _mediator.PublishAsync((dynamic)de);//这一步实际上会调用泛型的方法
		}

		Assert.Equal(1, OrderCreatedDomainEventHandler1.Calls);
		Assert.Equal(1, OrderCreatedDomainEventHandler2.Calls);
		Assert.Equal(1, OrderCreatedDomainEventHandler1V2.Calls);
		Assert.Equal(1, OrderCreatedDomainEventHandler2V2.Calls);
	}
}
