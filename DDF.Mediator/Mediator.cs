using DDF.Mediator.Abstractions;
using System.Runtime.CompilerServices;

namespace DDF.Mediator
{
	/// <summary>
	/// 中介者
	/// </summary>
	public interface IMediator: IRequestSender, INotificationPublisher, IStreamSender
	{
	}

	/// <summary>
	/// 中介者
	/// </summary>
	public sealed class Mediator: IMediator
	{
		/// <summary>
		/// 请求发送者
		/// </summary>
		private readonly IRequestSender _sender;

		/// <summary>
		/// 通知发布者
		/// </summary>
		private readonly INotificationPublisher _publisher;

		/// <summary>
		/// 流式返回请求发送者
		/// </summary>
		private readonly IStreamSender _streamSender;

		/// <summary>
		/// 中介者
		/// </summary>
		/// <param name="sender">请求发送者</param>
		/// <param name="publisher">通知发布者</param>
		/// <param name="streamSender">流式返回请求发送者</param>
		public Mediator(IRequestSender sender, INotificationPublisher publisher, IStreamSender streamSender)
		{
			_sender = sender;
			_publisher = publisher;
			_streamSender = streamSender;
		}

		/// <summary>
		/// 发送请求（反射）
		/// </summary>
		/// <typeparam name="TResponse">请求类型</typeparam>
		/// <param name="request">请求</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns></returns>
		public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
		{
			return await _sender.SendAsync(request, cancellationToken);
		}

		/// <summary>
		/// 发送请求（泛型）
		/// </summary>
		/// <typeparam name="TRequest">请求类型</typeparam>
		/// <typeparam name="TResponse">响应类型</typeparam>
		/// <param name="request">请求</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns></returns>
		public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
			where TRequest : IRequest<TResponse>
		{
			return await _sender.SendAsync<TRequest, TResponse>(request, cancellationToken);
		}

		/// <summary>
		/// 发布通知（反射）
		/// </summary>
		/// <param name="notification">通知</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns></returns>
		public async Task PublishAsync(INotification notification, CancellationToken cancellationToken = default)
		{
			await _publisher.PublishAsync(notification, cancellationToken);
		}

		/// <summary>
		/// 发布通知（泛型）
		/// </summary>
		/// <typeparam name="TNotification">通知类型</typeparam>
		/// <param name="notification">通知</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns></returns>
		public async Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
			where TNotification : INotification
		{
			await _publisher.PublishAsync<TNotification>(notification, cancellationToken);
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
			await foreach (var item in _streamSender.StreamAsync<TResponse>(request, cancellationToken)
				//.WithCancellation(cancellationToken)
				)
			{
				yield return item;
			}
		}

		/// <summary>
		/// 发送流式响应请求（泛型）
		/// </summary>
		/// <typeparam name="TStream">请求类型</typeparam>
		/// <typeparam name="TResponse">响应元素类型</typeparam>
		/// <param name="request">请求</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns></returns>
		public async IAsyncEnumerable<TResponse> StreamAsync<TStream, TResponse>(TStream request, [EnumeratorCancellation] CancellationToken cancellationToken = default) where TStream : IStream<TResponse>
		{
			await foreach (var item in _streamSender.StreamAsync<TStream, TResponse>(request, cancellationToken)
				//.WithCancellation(cancellationToken)
				)
			{
				yield return item;
			}
		}
	}
}
