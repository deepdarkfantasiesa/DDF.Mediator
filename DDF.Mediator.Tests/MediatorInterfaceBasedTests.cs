using DDF.Mediator;
using DDF.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DDF.Mediator.Tests;

#region Mediator interface-based request tests
public sealed record MediatorInterfaceRequest(string Data) : IRequest<string>;

public sealed class MediatorInterfaceRequestHandler : IRequestHandler<MediatorInterfaceRequest, string>
{
	public Task<string> HandleAsync(MediatorInterfaceRequest request, CancellationToken cancellationToken = default)
		=> Task.FromResult($"Mediator:{request.Data}");
}

public sealed record CommandInterfaceRequest(string CommandData) : ICommand<string>;

public sealed class CommandInterfaceRequestHandler : IRequestHandler<CommandInterfaceRequest, string>
{
	public Task<string> HandleAsync(CommandInterfaceRequest request, CancellationToken cancellationToken = default)
		=> Task.FromResult($"Command:{request.CommandData}");
}
#endregion

public class MediatorInterfaceBasedTests
{
	private ServiceProvider BuildServices()
	{
		var services = new ServiceCollection();
		services.AddMediator();
		return services.BuildServiceProvider();
	}

	[Fact]
	public async Task Mediator_SendAsync_WithInterfaceRequest_Works()
	{
		var sp = BuildServices();
		var mediator = sp.GetRequiredService<IMediator>();
		IRequest<string> request = new MediatorInterfaceRequest("Test");

		var result = await mediator.SendAsync(request);

		Assert.Equal("Mediator:Test", result);
	}

	[Fact]
	public async Task Mediator_SendAsync_SupportsCommandInterface()
	{
		var sp = BuildServices();
		var mediator = sp.GetRequiredService<IMediator>();
		IRequest<string> request = new CommandInterfaceRequest("Cmd");

		var result = await mediator.SendAsync(request);

		Assert.Equal("Command:Cmd", result);
	}

	[Fact]
	public async Task Mediator_SendAsync_WithInterfaceRequest_ThrowsWhenNull()
	{
		var sp = BuildServices();
		var mediator = sp.GetRequiredService<IMediator>();

		await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.SendAsync<string>(null!));
	}

	[Fact]
	public async Task Mediator_SendAsync_PolymorphicRequestsFromCollection()
	{
		var sp = BuildServices();
		var mediator = sp.GetRequiredService<IMediator>();

		var requests = new List<IRequest<string>>
		{
			new MediatorInterfaceRequest("Req1"),
			new CommandInterfaceRequest("Req2"),
			new MediatorInterfaceRequest("Req3")
		};

		var results = new List<string>();
		foreach(var req in requests)
		{
			results.Add(await mediator.SendAsync(req));
		}

		Assert.Equal(3, results.Count);
		Assert.Equal("Mediator:Req1", results[0]);
		Assert.Equal("Command:Req2", results[1]);
		Assert.Equal("Mediator:Req3", results[2]);
	}

	[Fact]
	public async Task Mediator_SendAsync_InterfaceAndTypedVersionsBothWork()
	{
		var sp = BuildServices();
		var mediator = sp.GetRequiredService<IMediator>();

		// 接口版本
		IRequest<string> interfaceReq = new MediatorInterfaceRequest("Interface");
		var interfaceResult = await mediator.SendAsync(interfaceReq);

		// 泛型版本
		var typedReq = new MediatorInterfaceRequest("Typed");
		var typedResult = await mediator.SendAsync<MediatorInterfaceRequest, string>(typedReq);

		Assert.Equal("Mediator:Interface", interfaceResult);
		Assert.Equal("Mediator:Typed", typedResult);
	}

	[Fact]
	public async Task Mediator_SendAsync_WithDynamic_WorksForPolymorphicRequests()
	{
		var sp = BuildServices();
		var mediator = sp.GetRequiredService<IMediator>();

		var requests = new List<IRequest<string>>
		{
			new MediatorInterfaceRequest("Dynamic1"),
			new CommandInterfaceRequest("Dynamic2")
		};

		var results = new List<string>();
		foreach(var req in requests)
		{
			// 使用 dynamic 保持运行时类型
			results.Add(await ((dynamic)mediator).SendAsync((dynamic)req));
		}

		Assert.Equal("Mediator:Dynamic1", results[0]);
		Assert.Equal("Command:Dynamic2", results[1]);
	}

	[Fact]
	public async Task Mediator_SendAsync_InterfaceRequest_WithCancellation()
	{
		var services = new ServiceCollection();
		services.AddMediator();
		var sp = services.BuildServiceProvider();
		var mediator = sp.GetRequiredService<IMediator>();

		IRequest<string> request = new CancellableInterfaceRequest();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		await Assert.ThrowsAsync<OperationCanceledException>(() => mediator.SendAsync(request, cts.Token));
	}

	private sealed record CancellableInterfaceRequest : IRequest<string>;
	private sealed class CancellableInterfaceRequestHandler : IRequestHandler<CancellableInterfaceRequest, string>
	{
		public Task<string> HandleAsync(CancellableInterfaceRequest request, CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult("OK");
		}
	}
}

public class InterfaceBasedRequestWithBehaviorsTests
{
	[Fact]
	public async Task InterfaceRequest_WithMultipleBehaviors_ExecutesInCorrectOrder()
	{
		var services = new ServiceCollection();
		var log = new MediatorBehaviorLog();
		services.AddSingleton(log);
		services.AddMediator(
			typeof(MediatorValidationBehavior<,>),
			typeof(MediatorLoggingBehavior<,>),
			typeof(MediatorTransactionBehavior<,>));
		var sp = services.BuildServiceProvider();
		var mediator = sp.GetRequiredService<IMediator>();

		IRequest<string> request = new BehaviorTestRequest("Test");
		var result = await mediator.SendAsync(request);

		Assert.Equal("Behavior:Test", result);
		// 优先级: Validation(1) -> Logging(2) -> Transaction(3)
		Assert.Equal(new[] { "Validation", "Logging-Before", "Transaction-Before", "Transaction-After", "Logging-After" }, log.Steps);
	}

	private sealed record BehaviorTestRequest(string Value) : IRequest<string>;
	private sealed class BehaviorTestRequestHandler : IRequestHandler<BehaviorTestRequest, string>
	{
		public Task<string> HandleAsync(BehaviorTestRequest request, CancellationToken cancellationToken = default)
			=> Task.FromResult($"Behavior:{request.Value}");
	}

	[PipelineBehaviorPriority(1)]
	private sealed class MediatorValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
		where TRequest : IRequest<TResponse>
	{
		private readonly MediatorBehaviorLog _log;
		public MediatorValidationBehavior(MediatorBehaviorLog log) => _log = log;
		public async Task<TResponse> HandleAsync(TRequest request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
		{
			_log.Steps.Add("Validation");
			return await next(cancellationToken);
		}
	}

	[PipelineBehaviorPriority(2)]
	private sealed class MediatorLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
		where TRequest : IRequest<TResponse>
	{
		private readonly MediatorBehaviorLog _log;
		public MediatorLoggingBehavior(MediatorBehaviorLog log) => _log = log;
		public async Task<TResponse> HandleAsync(TRequest request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
		{
			_log.Steps.Add("Logging-Before");
			var result = await next(cancellationToken);
			_log.Steps.Add("Logging-After");
			return result;
		}
	}

	[PipelineBehaviorPriority(3)]
	private sealed class MediatorTransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
		where TRequest : IRequest<TResponse>
	{
		private readonly MediatorBehaviorLog _log;
		public MediatorTransactionBehavior(MediatorBehaviorLog log) => _log = log;
		public async Task<TResponse> HandleAsync(TRequest request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
		{
			_log.Steps.Add("Transaction-Before");
			var result = await next(cancellationToken);
			_log.Steps.Add("Transaction-After");
			return result;
		}
	}

	private sealed class MediatorBehaviorLog
	{
		public List<string> Steps { get; } = new();
	}
}
