using DDF.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DDF.Mediator.Tests;

#region �ܵ���Ϊ

/// <summary>
/// �ܵ�ִ����־
/// </summary>
public sealed class BehaviorLog { public List<string> Steps { get; } = new(); }

/// <summary>
/// ���Թܵ���Ϊ
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
[PipelineBehaviorPriority(0)]
public sealed class TestBehavior<TRequest, TResponse>: IPipelineBehavior<TRequest, TResponse>
	where TRequest : IRequest<TResponse>
{
	private readonly BehaviorLog _log;
	public TestBehavior(BehaviorLog log) => _log = log;
	public async Task<TResponse> HandleAsync(TRequest request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
	{
		_log.Steps.Add("Test-Before");
		var resp = await next(cancellationToken);
		_log.Steps.Add("Test-After");
		return resp;
	}
}

/// <summary>
/// ��֤�ܵ���Ϊ
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
[PipelineBehaviorPriority(1)]
public sealed class ValidatorBehavior<TRequest, TResponse>: IPipelineBehavior<TRequest, TResponse>
	where TRequest : IValidatior<TResponse>
{
	private readonly BehaviorLog _log;
	public ValidatorBehavior(BehaviorLog log) => _log = log;
	public async Task<TResponse> HandleAsync(TRequest request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
	{
		_log.Steps.Add("Validator");
		return await next(cancellationToken);
	}
}

/// <summary>
/// �ֲ�ʽ���ܵ���Ϊ
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
[PipelineBehaviorPriority(2)]
public sealed class DistributedLockBehavior<TRequest, TResponse>: IPipelineBehavior<TRequest, TResponse>
	where TRequest : IDistributedLock<TResponse>
{
	private readonly BehaviorLog _log;
	public DistributedLockBehavior(BehaviorLog log) => _log = log;
	public async Task<TResponse> HandleAsync(TRequest request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
	{
		_log.Steps.Add("Lock-Before");
		var resp = await next(cancellationToken);
		_log.Steps.Add("Lock-After");
		return resp;
	}
}

/// <summary>
/// ����ܵ���Ϊ
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
[PipelineBehaviorPriority(3)]
public sealed class TransactionBehavior<TRequest, TResponse>: IPipelineBehavior<TRequest, TResponse>
	where TRequest : ICommand<TResponse>
{
	private readonly BehaviorLog _log;
	public TransactionBehavior(BehaviorLog log) => _log = log;
	public async Task<TResponse> HandleAsync(TRequest request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
	{
		_log.Steps.Add("Tx-Before");
		var resp = await next(cancellationToken);
		_log.Steps.Add("Tx-After");
		return resp;
	}
}

//[PipelineBehaviorPriority(1)]
//public sealed class ValidatorBehavior<TRequest, TResponse>: IPipelineBehavior<TRequest, TResponse>
//	where TRequest : IRequest<TResponse>
//{
//	private readonly BehaviorLog _log; public ValidatorBehavior(BehaviorLog log) => _log = log;
//	public async Task<TResponse> HandleAsync(TRequest request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
//	{ _log.Steps.Add("Validator"); return await next(cancellationToken); }
//}

/// <summary>
/// ��ѯ����ܵ���Ϊ
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
[PipelineBehaviorPriority(2)]
public sealed class QueryCacheBehavior<TRequest, TResponse>: IPipelineBehavior<TRequest, TResponse>
	where TRequest : IQueryCache<TResponse>
{
	private readonly BehaviorLog _log;
	public QueryCacheBehavior(BehaviorLog log) => _log = log;
	public async Task<TResponse> HandleAsync(TRequest request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
	{
		_log.Steps.Add("QueryCache-Before");
		var resp = await next(cancellationToken);
		_log.Steps.Add("QueryCache-After");
		return resp;
	}
}

#endregion

#region Command implementations
public sealed record CreateOrderCommand(string OrderId): ICommand<string>;
public sealed class CreateOrderCommandHandler: ICommandHandler<CreateOrderCommand, string>
{
	public async Task<string> HandleAsync(CreateOrderCommand request, CancellationToken cancellationToken = default)
		=> await Task.FromResult($"Created:{request.OrderId}");
}

public sealed record LockedCreateOrderCommand(string OrderId): IDistributedLock<string>, ICommand<string>;
public sealed class LockedCreateOrderCommandHandler: ICommandHandler<LockedCreateOrderCommand, string>
{
	public async Task<string> HandleAsync(LockedCreateOrderCommand request, CancellationToken cancellationToken = default)
		=> await Task.FromResult($"LockedCreated:{request.OrderId}");
}

public sealed record LockedValidateCreateOrderCommand(string OrderId): IDistributedLock<string>, ICommand<string>, IValidatior<string>;
public sealed class LockedValidateCreateOrderCommandHandler: ICommandHandler<LockedValidateCreateOrderCommand, string>
{
	public async Task<string> HandleAsync(LockedValidateCreateOrderCommand request, CancellationToken cancellationToken = default)
		=> await Task.FromResult($"LockedValidatedCreated:{request.OrderId}");
}

#endregion

#region Query implementations
public sealed class OrderQueryParams: IQueryParam
{
	public string? OrderId { get; set; }
}

public sealed record GetOrderQuery(OrderQueryParams Params): IQuery<OrderQueryParams, string>;
public sealed class GetOrderQueryHandler: IQueryHandler<GetOrderQuery, OrderQueryParams, string>
{
	public async Task<string> HandleAsync(GetOrderQuery request, CancellationToken cancellationToken = default)
		=> await Task.FromResult($"Order:{request.Params.OrderId}");
}

public sealed record GetOrderCachedQueryParams(string OrderId): IQueryParam;

public sealed record GetOrderCachedQuery(string OrderId): IQueryCache<string>, IQuery<GetOrderCachedQueryParams, string>;
public sealed class GetOrderCachedQueryHandler: IQueryHandler<GetOrderCachedQuery, GetOrderCachedQueryParams, string>
{
	public async Task<string> HandleAsync(GetOrderCachedQuery request, CancellationToken cancellationToken = default)
		=> await Task.FromResult($"OrderCached:{request.OrderId}");
}

public sealed record GetOrderValidateCacheQuery(string OrderId):IQueryCache<string>,IQuery<GetOrderCachedQueryParams, string>,IValidatior<string>;

public sealed class GetOrderValidateCacheQueryHandler: IQueryHandler<GetOrderValidateCacheQuery, GetOrderCachedQueryParams, string>
{
	public async Task<string> HandleAsync(GetOrderValidateCacheQuery request, CancellationToken cancellationToken = default)
		=> await Task.FromResult($"OrderValidateCached:{request.OrderId}");
}

#endregion

#region Domain events
/// <summary>
/// �����¼�1
/// </summary>
/// <param name="OrderId"></param>
public sealed record OrderCreatedDomainEvent(string OrderId): IDomainEvent;
public sealed class OrderCreatedDomainEventHandler1: IDomainEventHandler<OrderCreatedDomainEvent>
{
	public static int Calls;
	public async Task HandleAsync(OrderCreatedDomainEvent notification, CancellationToken cancellationToken = default)
	{
		Interlocked.Increment(ref Calls);
		await Task.CompletedTask;
	}
}
public sealed class OrderCreatedDomainEventHandler2: IDomainEventHandler<OrderCreatedDomainEvent>
{
	public static int Calls;
	public async Task HandleAsync(OrderCreatedDomainEvent notification, CancellationToken cancellationToken = default)
	{
		Interlocked.Increment(ref Calls);
		await Task.CompletedTask;
	}
}

/// <summary>
/// �����¼�2
/// </summary>
/// <param name="OrderId"></param>
public sealed record OrderCreatedDomainEventV2(string OrderId): IDomainEvent;
public sealed class OrderCreatedDomainEventHandler3: IDomainEventHandler<OrderCreatedDomainEventV2>
{
	public static int Calls;
	public async Task HandleAsync(OrderCreatedDomainEventV2 notification, CancellationToken cancellationToken = default)
	{
		Interlocked.Increment(ref Calls);
		await Task.CompletedTask;
	}
}
public sealed class OrderCreatedDomainEventHandler4: IDomainEventHandler<OrderCreatedDomainEventV2>
{
	public static int Calls;
	public async Task HandleAsync(OrderCreatedDomainEventV2 notification, CancellationToken cancellationToken = default)
	{
		Interlocked.Increment(ref Calls);
		await Task.CompletedTask;
	}
}

#endregion

public class CommandQueryDomainTests
{
	private ServiceProvider BuildServices()
	{
		var services = new ServiceCollection();
		services.AddSingleton<BehaviorLog>();
		services.AddMediator(
			typeof(TestBehavior<,>),
			typeof(QueryCacheBehavior<,>),
			typeof(ValidatorBehavior<,>),
			typeof(TransactionBehavior<,>),
			typeof(DistributedLockBehavior<,>));
		return services.BuildServiceProvider();
	}

	/// <summary>
	/// ֻ���ò��ԡ�����ܵ���Ϊ
	/// </summary>
	/// <returns></returns>
	[Fact]
	public async Task Command_Pipeline_Order_No_Validate_No_Lock()
	{
		var sp = BuildServices();
		var log = sp.GetRequiredService<BehaviorLog>();
		var sender = sp.GetRequiredService<IRequestSender>();

		//����
		var tResult = await sender.SendAsync<CreateOrderCommand, string>(new CreateOrderCommand("A1"));
		Assert.Equal("Created:A1", tResult);
		Assert.Equal(new[] { "Test-Before", "Tx-Before", "Tx-After", "Test-After" }, log.Steps);

		log.Steps.Clear();

		//����
		var rResult = await sender.SendAsync(new CreateOrderCommand("A2"));
		Assert.Equal("Created:A2", rResult);
		Assert.Equal(new[] { "Test-Before", "Tx-Before", "Tx-After", "Test-After" }, log.Steps);
	}

	/// <summary>
	/// �������񡢷ֲ�ʽ���ܵ���Ϊ
	/// </summary>
	/// <returns></returns>
	[Fact]
	public async Task Command_Pipeline_Order_With_Lock()
	{
		var sp = BuildServices();
		var log = sp.GetRequiredService<BehaviorLog>();
		var sender = sp.GetRequiredService<IRequestSender>();

		//����
		var tResult = await sender.SendAsync<LockedCreateOrderCommand, string>(new LockedCreateOrderCommand("B1"));
		Assert.Equal("LockedCreated:B1", tResult);
		Assert.Equal(new[] { "Test-Before", "Lock-Before", "Tx-Before", "Tx-After", "Lock-After", "Test-After" }, log.Steps);

		log.Steps.Clear();

		//����
		var rResult = await sender.SendAsync(new LockedCreateOrderCommand("B2"));
		Assert.Equal("LockedCreated:B2", rResult);
		Assert.Equal(new[] { "Test-Before", "Lock-Before", "Tx-Before", "Tx-After", "Lock-After", "Test-After" }, log.Steps);
	}

	/// <summary>
	/// ������֤�����񡢷ֲ�ʽ���ܵ���Ϊ
	/// </summary>
	/// <returns></returns>
	[Fact]
	public async Task Command_Pipeline_Order_With_Lock_Validate()
	{
		var sp = BuildServices();
		var log = sp.GetRequiredService<BehaviorLog>();
		var sender = sp.GetRequiredService<IRequestSender>();

		//����
		var tResult = await sender.SendAsync<LockedValidateCreateOrderCommand, string>(new LockedValidateCreateOrderCommand("C1"));
		Assert.Equal("LockedValidatedCreated:C1", tResult);
		Assert.Equal(new[] { "Test-Before", "Validator", "Lock-Before", "Tx-Before", "Tx-After", "Lock-After", "Test-After" }, log.Steps);

		log.Steps.Clear();

		//����
		var rResult = await sender.SendAsync(new LockedValidateCreateOrderCommand("C2"));
		Assert.Equal("LockedValidatedCreated:C2", rResult);
		Assert.Equal(new[] { "Test-Before", "Validator", "Lock-Before", "Tx-Before", "Tx-After", "Lock-After", "Test-After" }, log.Steps);
	}

	/// <summary>
	/// ֻ���ò��Թܵ���Ϊ��query
	/// </summary>
	/// <returns></returns>
	[Fact]
	public async Task Query_Pipeline_Order_No_Cache()
	{
		var sp = BuildServices();
		var log = sp.GetRequiredService<BehaviorLog>();
		var sender = sp.GetRequiredService<IRequestSender>();

		//����
		var tResult = await sender.SendAsync<GetOrderQuery, string>(new GetOrderQuery(new OrderQueryParams { OrderId = "Q1" }));
		Assert.Equal("Order:Q1", tResult);
		Assert.Equal(new[] { "Test-Before", "Test-After" }, log.Steps);

		log.Steps.Clear();

		//����
		var rResult = await sender.SendAsync(new GetOrderQuery(new OrderQueryParams { OrderId = "Q2" }));
		Assert.Equal("Order:Q2", rResult);
		Assert.Equal(new[] { "Test-Before", "Test-After" }, log.Steps);
	}

	/// <summary>
	/// ���ò��ԡ���ѯ����ܵ���Ϊ��query
	/// </summary>
	/// <returns></returns>
	[Fact]
	public async Task Query_Pipeline_Order_With_Cache()
	{
		var sp = BuildServices();
		var log = sp.GetRequiredService<BehaviorLog>();
		var sender = sp.GetRequiredService<IRequestSender>();

		//����
		var tResult = await sender.SendAsync<GetOrderCachedQuery, string>(new GetOrderCachedQuery("CQ1"));
		Assert.Equal("OrderCached:CQ1", tResult);
		Assert.Equal(new[] { "Test-Before", "QueryCache-Before", "QueryCache-After", "Test-After" }, log.Steps);

		log.Steps.Clear();

		//����
		var rResult = await sender.SendAsync(new GetOrderCachedQuery("CQ2"));
		Assert.Equal("OrderCached:CQ2", rResult);
		Assert.Equal(new[] { "Test-Before", "QueryCache-Before", "QueryCache-After", "Test-After" }, log.Steps);
	}

	/// <summary>
	/// ���ò��ԡ���֤����ѯ����ܵ���Ϊ��query
	/// </summary>
	/// <returns></returns>
	[Fact]
	public async Task Query_Pipeline_Order_With_Cache_Validate()
	{
		var sp = BuildServices();
		var log = sp.GetRequiredService<BehaviorLog>();
		var sender = sp.GetRequiredService<IRequestSender>();

		//����
		var result = await sender.SendAsync<GetOrderValidateCacheQuery, string>(new GetOrderValidateCacheQuery("CQ3"));
		Assert.Equal("OrderValidateCached:CQ3", result);
		Assert.Equal(new[] { "Test-Before", "Validator", "QueryCache-Before", "QueryCache-After", "Test-After" }, log.Steps);

		log.Steps.Clear();

		//����
		var rResult = await sender.SendAsync(new GetOrderValidateCacheQuery("CQ4"));
		Assert.Equal("OrderValidateCached:CQ4", rResult);
		Assert.Equal(new[] { "Test-Before", "Validator", "QueryCache-Before", "QueryCache-After", "Test-After" }, log.Steps);
	}

	[Fact]
	public async Task DomainEvent_Publish_Generic_And_Reflection()
	{
		OrderCreatedDomainEventHandler1.Calls = 0;
		OrderCreatedDomainEventHandler2.Calls = 0;
		OrderCreatedDomainEventHandler3.Calls = 0;
		OrderCreatedDomainEventHandler4.Calls = 0;
		var sp = BuildServices();
		var mediator = sp.GetRequiredService<IMediator>();
		var ev = new OrderCreatedDomainEvent("E1");
		//����
		await mediator.PublishAsync((INotification)ev);
		//����
		await mediator.PublishAsync<OrderCreatedDomainEvent>(ev);
		//����
		await mediator.PublishAsync(ev);

		// ÿ��·���������ȫ�� handler
		Assert.Equal(3, OrderCreatedDomainEventHandler1.Calls);
		Assert.Equal(3, OrderCreatedDomainEventHandler2.Calls);
		Assert.Equal(0, OrderCreatedDomainEventHandler3.Calls);
		Assert.Equal(0, OrderCreatedDomainEventHandler4.Calls);
	}

	/// <summary>
	/// ���������¼������ߵ�ע�����
	/// </summary>
	[Fact]
	public void Service_Registration_For_Command_Query_DomainEvent()
	{
		var sp = BuildServices();
		// Handlers should be resolvable via reflection publish; direct resolution test:
		var handler1 = sp.GetServices<INotificationHandler<OrderCreatedDomainEvent>>();
		Assert.Equal(2, handler1.Count());
	}

	/// <summary>
	/// ģ��SaveChange�ַ������¼�
	/// </summary>
	/// <returns></returns>
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

		foreach(var de in domianEvents)
		{
			//���÷��ͷ������������dynamic������ʱ�������ͣ������÷���ķ����������޷��ҵ�������
			await _mediator.PublishAsync((dynamic)de);
		}

		Assert.Equal(1, OrderCreatedDomainEventHandler1.Calls);
		Assert.Equal(1, OrderCreatedDomainEventHandler2.Calls);
		Assert.Equal(1, OrderCreatedDomainEventHandler3.Calls);
		Assert.Equal(1, OrderCreatedDomainEventHandler4.Calls);
	}
}
