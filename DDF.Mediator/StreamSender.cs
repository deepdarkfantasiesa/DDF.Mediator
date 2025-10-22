using DDF.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

namespace DDF.Mediator
{
	/// <summary>
	/// 流式响应请求发送者
	/// </summary>
	public interface IStreamSender
	{
		/// <summary>
		/// 发送流式响应请求（反射）
		/// </summary>
		/// <typeparam name="TResponse">响应元素类型</typeparam>
		/// <param name="request">请求</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns></returns>
		IAsyncEnumerable<TResponse> StreamAsync<TResponse>(IStream<TResponse> request, [EnumeratorCancellation] CancellationToken cancellationToken = default);

		/// <summary>
		/// 发送流式响应请求（泛型）
		/// </summary>
		/// <typeparam name="TStream">请求类型</typeparam>
		/// <typeparam name="TResponse">响应元素类型</typeparam>
		/// <param name="request">请求</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns></returns>
		IAsyncEnumerable<TResponse> StreamAsync<TStream, TResponse>(TStream request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
			where TStream : IStream<TResponse>;
	}

	/// <summary>
	/// 流式响应请求发送者
	/// </summary>
	public sealed class StreamSender: IStreamSender
	{
		/// <summary>
		/// 服务提供者
		/// </summary>
		private readonly IServiceProvider _serviceProvider;

		/// <summary>
		/// 流式响应请求发送者
		/// </summary>
		/// <param name="serviceProvider">服务提供者</param>
		public StreamSender(IServiceProvider serviceProvider)
		{
			_serviceProvider = serviceProvider;
		}

		/// <summary>
		/// 发送流式响应请求（反射）
		/// </summary>
		/// <typeparam name="TResponse">响应元素类型</typeparam>
		/// <param name="request">请求</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns></returns>
		public async IAsyncEnumerable<TResponse> StreamAsync<TResponse>(IStream<TResponse> request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			if(request == null)
				throw new ArgumentNullException(nameof(request));

			var requestType = request.GetType();
			var handlerInterfaceType = typeof(IStreamHandler<,>).MakeGenericType(requestType, typeof(TResponse));
			var handler = _serviceProvider.GetService(handlerInterfaceType)
				?? throw new InvalidOperationException($"未找到流式请求处理者：{requestType.Name}");

			var handleMethod = handlerInterfaceType.GetMethod("HandleAsync")
				?? throw new InvalidOperationException($"未找到 HandleAsync 方法：{handlerInterfaceType.Name}");

			var asyncEnumerable = (IAsyncEnumerable<TResponse>)handleMethod.Invoke(handler, new object[] { request, cancellationToken })!;

			await foreach(var item in asyncEnumerable.WithCancellation(cancellationToken))
			{
				yield return item;
			}
		}

		/// <summary>
		/// 发送流式响应请求（泛型）
		/// </summary>
		/// <typeparam name="TStream">流式请求类型</typeparam>
		/// <typeparam name="TResponse">响应元素类型</typeparam>
		/// <param name="request">请求</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns></returns>
		public async IAsyncEnumerable<TResponse> StreamAsync<TStream, TResponse>(TStream request, [EnumeratorCancellation] CancellationToken cancellationToken = default) 
			where TStream : IStream<TResponse>
		{
			if(request == null)
				throw new ArgumentNullException(nameof(request));

			var handler = _serviceProvider.GetRequiredService<IStreamHandler<TStream, TResponse>>();
			var asyncEnumerable = handler.HandleAsync(request, cancellationToken);

			await foreach(var item in asyncEnumerable.WithCancellation(cancellationToken))
			{
				yield return item;
			}
		}
	}
}
