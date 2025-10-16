using DDF.Mediator.Abstractions;

namespace DDF.Mediator
{
	/// <summary>
	/// 中介者
	/// </summary>
	public interface IMediator: IRequestSender, INotificationPublisher
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
		/// 中介者
		/// </summary>
		/// <param name="sender">请求发送者</param>
		/// <param name="publisher">通知发布者</param>
		public Mediator(IRequestSender sender, INotificationPublisher publisher)
		{
			_sender = sender;
			_publisher = publisher;
		}

		/// <summary>
		/// 发送请求（基于接口类型）
		/// </summary>
		public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
		{
			return await _sender.SendAsync(request, cancellationToken);
		}

		/// <summary>
		/// 发送请求（基于具体类型）
		/// </summary>
		public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default) 
			where TRequest : IRequest<TResponse>
		{
			return await _sender.SendAsync<TRequest, TResponse>(request, cancellationToken);
		}

		/// <summary>
		/// 发布（反射获取处理者）
		/// </summary>
		public async Task PublishAsync(INotification notification, CancellationToken cancellationToken = default)
		{
			await _publisher.PublishAsync(notification, cancellationToken);
		}

		/// <summary>
		/// 发布（服务定位获取处理者）
		/// </summary>
		public async Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default) 
			where TNotification : INotification
		{
			await _publisher.PublishAsync<TNotification>(notification, cancellationToken);
		}
	}
}
