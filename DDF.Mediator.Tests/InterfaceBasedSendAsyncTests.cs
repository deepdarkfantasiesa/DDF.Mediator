using DDF.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DDF.Mediator.Tests;

#region 直接基于IRequest<>、IRequestHandler<,>接口的请求和处理者定义
public sealed record InterfaceBasedRequest(string Value): IRequest<string>;

public sealed class InterfaceBasedRequestHandler: IRequestHandler<InterfaceBasedRequest, string>
{
	public async Task<string> HandleAsync(InterfaceBasedRequest request, CancellationToken cancellationToken = default)
		=> await Task.FromResult($"InterfaceBased:{request.Value}");
}

public sealed record PolymorphicRequest1(int Number): IRequest<int>;
public sealed record PolymorphicRequest2(int Number): IRequest<int>;

public sealed class PolymorphicRequestHandler1: IRequestHandler<PolymorphicRequest1, int>
{
	public async Task<int> HandleAsync(PolymorphicRequest1 request, CancellationToken cancellationToken = default)
		=> await Task.FromResult(request.Number * 2);
}

public sealed class PolymorphicRequestHandler2: IRequestHandler<PolymorphicRequest2, int>
{
	public async Task<int> HandleAsync(PolymorphicRequest2 request, CancellationToken cancellationToken = default)
		=> await Task.FromResult(request.Number * 3);
}

public sealed record UnhandledRequest: IRequest<bool>;

public sealed record FaultyRequest: IRequest<string>;
public sealed class FaultyRequestHandler: IRequestHandler<FaultyRequest, string>
{
	public Task<string> HandleAsync(FaultyRequest request, CancellationToken cancellationToken = default)
		=> throw new InvalidOperationException("Handler failure");
}

public sealed record CancellableRequest: IRequest<string>;
public sealed class CancellableRequestHandler: IRequestHandler<CancellableRequest, string>
{
	public async Task<string> HandleAsync(CancellableRequest request, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return await Task.FromResult("OK");
	}
}

#endregion

#region 基于IRequest<>的管道行为定义
[PipelineBehaviorPriority(1)]
public sealed class InterfaceValidationBehavior<TRequest, TResponse>: IPipelineBehavior<TRequest, TResponse>
	where TRequest : IRequest<TResponse>
{
	private readonly InterfaceBehaviorLog _log;
	public InterfaceValidationBehavior(InterfaceBehaviorLog log) => _log = log;
	public async Task<TResponse> HandleAsync(TRequest request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
	{
		_log.Steps.Add("Interface-Validation");
		return await next(cancellationToken);
	}
}

[PipelineBehaviorPriority(2)]
public sealed class InterfaceLoggingBehavior<TRequest, TResponse>: IPipelineBehavior<TRequest, TResponse>
	where TRequest : IRequest<TResponse>
{
	private readonly InterfaceBehaviorLog _log;
	public InterfaceLoggingBehavior(InterfaceBehaviorLog log) => _log = log;
	public async Task<TResponse> HandleAsync(TRequest request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
	{
		_log.Steps.Add("Interface-Logging-Before");
		var result = await next(cancellationToken);
		_log.Steps.Add("Interface-Logging-After");
		return result;
	}
}

public sealed class InterfaceBehaviorLog
{
	public List<string> Steps { get; } = new();
}
#endregion

public class InterfaceBasedSendAsyncTests
{
	private ServiceProvider BuildServices()
	{
		var services = new ServiceCollection();
		services.AddSingleton<InterfaceBehaviorLog>();
		services.AddMediator(
			typeof(InterfaceValidationBehavior<,>),
			typeof(InterfaceLoggingBehavior<,>));
		return services.BuildServiceProvider();
	}

	/// <summary>
	/// 
	/// </summary>
	/// <returns></returns>
	[Fact]
	public async Task SendAsync_WithInterfaceParameter_ReturnsCorrectResult()
	{
		var sp = BuildServices();
		var sender = sp.GetRequiredService<IRequestSender>();
		var request = new InterfaceBasedRequest("Test");

		//反射
		var rResult = await sender.SendAsync(request);
		Assert.Equal("InterfaceBased:Test", rResult);

		//泛型
		var tResult = await sender.SendAsync<InterfaceBasedRequest, string>(request);
		Assert.Equal("InterfaceBased:Test", tResult);
	}

	[Fact]
	public async Task SendAsync_WithInterfaceParameter_ExecutesBehaviors()
	{
		var sp = BuildServices();
		var log = sp.GetRequiredService<InterfaceBehaviorLog>();
		var sender = sp.GetRequiredService<IRequestSender>();
		var request = new InterfaceBasedRequest("Pipeline");

		//反射
		var rResult = await sender.SendAsync(request);
		Assert.Equal("InterfaceBased:Pipeline", rResult);
		Assert.Equal(new[] { "Interface-Validation", "Interface-Logging-Before", "Interface-Logging-After" }, log.Steps);

		log.Steps.Clear();

		//泛型
		var tResult = await sender.SendAsync<InterfaceBasedRequest, string>(request);
		Assert.Equal("InterfaceBased:Pipeline", tResult);
		Assert.Equal(new[] { "Interface-Validation", "Interface-Logging-Before", "Interface-Logging-After" }, log.Steps);
	}

	/// <summary>
	/// 空入参
	/// </summary>
	/// <returns></returns>
	[Fact]
	public async Task SendAsync_WithInterfaceParameter_ThrowsWhenNull()
	{
		var sp = BuildServices();
		var sender = sp.GetRequiredService<IRequestSender>();

		InterfaceBasedRequest request = null!;

		//反射
		await Assert.ThrowsAsync<ArgumentNullException>(() => sender.SendAsync(request));

		//泛型
		await Assert.ThrowsAsync<ArgumentNullException>(() => sender.SendAsync<InterfaceBasedRequest, string>(request));
	}

	[Fact]
	public async Task SendAsync_WithInterfaceParameter_SupportsPolymorphism()
	{
		var services = new ServiceCollection();
		services.AddMediator();
		var sp = services.BuildServiceProvider();
		var sender = sp.GetRequiredService<IRequestSender>();

		IRequest<int> request1 = new PolymorphicRequest1(10);
		IRequest<int> request2 = new PolymorphicRequest2(10);

		var result1 = await sender.SendAsync(request1);
		var result2 = await sender.SendAsync(request2);

		Assert.Equal(20, result1);
		Assert.Equal(30, result2);
	}

	/// <summary>
	/// 缺失handler
	/// </summary>
	/// <returns></returns>
	[Fact]
	public async Task SendAsync_WithInterfaceParameter_ThrowsWhenHandlerMissing()
	{
		var services = new ServiceCollection();
		services.AddMediator();
		var sp = services.BuildServiceProvider();
		var sender = sp.GetRequiredService<IRequestSender>();

		IRequest<bool> request = new UnhandledRequest();

		await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(request));
	}

	/// <summary>
	/// handler内抛出异常
	/// </summary>
	/// <returns></returns>
	[Fact]
	public async Task SendAsync_WithInterfaceParameter_HandlesExceptions()
	{
		var services = new ServiceCollection();
		services.AddMediator();
		var sp = services.BuildServiceProvider();
		var sender = sp.GetRequiredService<IRequestSender>();

		IRequest<string> request = new FaultyRequest();

		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(request));
		Assert.Contains("处理请求", ex.Message);
		Assert.NotNull(ex.InnerException);
	}

	/// <summary>
	/// 取消执行handler
	/// </summary>
	/// <returns></returns>
	[Fact]
	public async Task SendAsync_WithInterfaceParameter_HandlesCancellation()
	{
		var services = new ServiceCollection();
		services.AddMediator();
		var sp = services.BuildServiceProvider();
		var sender = sp.GetRequiredService<IRequestSender>();

		IRequest<string> request = new CancellableRequest();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		await Assert.ThrowsAsync<OperationCanceledException>(() => sender.SendAsync(request, cts.Token));
	}

	/// <summary>
	/// 对比反射和泛型版本
	/// </summary>
	/// <returns></returns>
	[Fact]
	public async Task SendAsync_CompareWithTypedVersion()
	{
		var sp = BuildServices();
		var sender = sp.GetRequiredService<IRequestSender>();

		//反射
		IRequest<string> interfaceRequest = new InterfaceBasedRequest("Compare1");
		var interfaceResult = await sender.SendAsync(interfaceRequest);

		//泛型
		var typedRequest = new InterfaceBasedRequest("Compare2");
		var typedResult = await sender.SendAsync<InterfaceBasedRequest, string>(typedRequest);

		Assert.Equal("InterfaceBased:Compare1", interfaceResult);
		Assert.Equal("InterfaceBased:Compare2", typedResult);
	}
}
