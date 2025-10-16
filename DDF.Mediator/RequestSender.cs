using DDF.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace DDF.Mediator
{
	/// <summary>
	/// 请求发送者
	/// </summary>
	public interface IRequestSender
	{
		/// <summary>
		/// 发送请求（基于接口类型）
		/// </summary>
		/// <typeparam name="TResponse">响应类型</typeparam>
		/// <param name="request">请求对象</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns></returns>
		Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

		/// <summary>
		/// 发送请求（基于具体类型）
		/// </summary>
		/// <typeparam name="TRequest">请求类型</typeparam>
		/// <typeparam name="TResponse">响应类型</typeparam>
		/// <param name="request">请求对象</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns></returns>
		Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
			where TRequest : IRequest<TResponse>;
	}

	/// <summary>
	/// 请求发送者
	/// </summary>
	public sealed class RequestSender: IRequestSender
	{
		/// <summary>
		/// 服务提供者
		/// </summary>
		private readonly IServiceProvider _serviceProvider;

		/// <summary>
		/// 请求发送者
		/// </summary>
		/// <param name="serviceProvider">服务提供者</param>
		public RequestSender(IServiceProvider serviceProvider)
		{
			_serviceProvider = serviceProvider;
		}

		/// <summary>
		/// 发送请求（基于接口类型，运行时解析具体类型）
		/// </summary>
		public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
		{
			if(request == null)
				throw new ArgumentNullException(nameof(request));

			// 获取请求的运行时具体类型
			var requestType = request.GetType();
			
			// 构造 IRequestHandler<ConcreteType, TResponse>
			var handlerInterfaceType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
			var handler = _serviceProvider.GetService(handlerInterfaceType);
			
			if(handler == null)
				throw new InvalidOperationException($"未找到请求类型 {requestType.Name} 的处理者");

			// 获取适用于具体请求类型的管道行为
			var behaviorInterfaceType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));
			var behaviors = _serviceProvider.GetServices(behaviorInterfaceType)
				.Select(p => new
				{
					Behavior = p,
					Priority = p.GetType().GetCustomAttribute<PipelineBehaviorPriorityAttribute>() ??
							  throw new Exception($"请为管道行为 {p.GetType().Name} 指定优先级特性")
				})
				.OrderByDescending(p => p.Priority.Level)
				.Select(p => p.Behavior)
				.ToList();

			// 构造最终的 handler 委托
			var handleMethod = handlerInterfaceType.GetMethod("HandleAsync")!;
			NextHandlerDelegate<TResponse> finalHandler = async ct => 
			{
				var task = (Task<TResponse>)handleMethod.Invoke(handler, new object[] { request, ct })!;
				return await task;
			};

			// 反向构建管道链
			foreach(var behavior in behaviors)
			{
				var previousNext = finalHandler;
				var behaviorType = behavior.GetType();
				var handleAsyncMethod = behaviorInterfaceType.GetMethod("HandleAsync")!;
				
				finalHandler = async ct =>
				{
					var task = (Task<TResponse>)handleAsyncMethod.Invoke(behavior, new object[] { request, previousNext, ct })!;
					return await task;
				};
			}

			try
			{
				return await finalHandler(cancellationToken);
			}
			catch(OperationCanceledException)
			{
				throw;
			}
			catch(Exception ex)
			{
				throw new InvalidOperationException($"处理请求 {requestType.Name} 时发生异常", ex);
			}
		}

		/// <summary>
		/// 发送请求（基于具体类型）
		/// </summary>
		public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
			where TRequest : IRequest<TResponse>
		{
			if(request == null)
				throw new ArgumentNullException(nameof(request));

			var behaviors = _serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>()
				.Select(p => new
				{
					Behavior = p,
					Priority = p.GetType().GetCustomAttribute<PipelineBehaviorPriorityAttribute>() ??
							  throw new Exception($"请为管道行为 {p.GetType().Name} 指定优先级特性")
				})
				.OrderByDescending(p => p.Priority.Level)
				.Select(p => p.Behavior)
				.ToList();

			var handler = _serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();

			NextHandlerDelegate<TResponse> finalHandler = async cancellationToken => await handler.HandleAsync(request, cancellationToken);

			foreach(var behavior in behaviors)
			{
				var previousNext = finalHandler;
				finalHandler = async cancellationToken => await behavior.HandleAsync(request, previousNext, cancellationToken);
			}

			try
			{
				return await finalHandler(cancellationToken);
			}
			catch(OperationCanceledException)
			{
				throw;
			}
			catch(Exception ex)
			{
				throw new InvalidOperationException($"处理请求 {typeof(TRequest).Name} 时发生异常", ex);
			}
		}
	}
}
