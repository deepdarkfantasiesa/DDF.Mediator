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
		/// 发送请求
		/// </summary>
		/// <typeparam name="TRequest">请求类型</typeparam>
		/// <typeparam name="TResponse">响应类型</typeparam>
		/// <param name="request">请求</param>
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
		/// 发送请求
		/// </summary>
		/// <typeparam name="TRequest">请求类型</typeparam>
		/// <typeparam name="TResponse">响应类型</typeparam>
		/// <param name="request">请求</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns></returns>
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

			var handler = _serviceProvider
				.GetRequiredService<IRequestHandler<TRequest, TResponse>>();

			NextHandlerDelegate<TResponse> finalHandler = async cancellationToken => await handler.HandleAsync(request, cancellationToken);

			// 反向构建管道链，确保优先级高的先执行
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
				// 取消操作，重新抛出
				throw;
			}
			catch(Exception ex)
			{
				// 包装其他异常，提供更多上下文信息
				throw new InvalidOperationException(
					$"处理请求 {typeof(TRequest).Name} 时发生异常", ex);
			}
		}
	}
}
