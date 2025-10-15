using DDF.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DDF.Mediator
{
	/// <summary>
	/// 通知发布者
	/// </summary>
	public interface INotificationPublisher
	{
		/// <summary>
		/// 发布（反射获取处理者）
		/// </summary>
		/// <param name="notification">通知</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns></returns>
		Task PublishAsync(INotification notification, CancellationToken cancellationToken = default);

		/// <summary>
		/// 发布（服务定位获取处理者）
		/// </summary>
		/// <typeparam name="TNotification">通知类型</typeparam>
		/// <param name="notification">通知</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns></returns>
		Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
			where TNotification : INotification;
	}

	/// <summary>
	/// 通知发布者
	/// </summary>
	public sealed class NotificationPublisher: INotificationPublisher
	{
		/// <summary>
		/// 服务提供者
		/// </summary>
		private readonly IServiceProvider _serviceProvider;

		/// <summary>
		/// 请求发送者
		/// </summary>
		/// <param name="serviceProvider">服务提供者</param>
		public NotificationPublisher(IServiceProvider serviceProvider)
		{
			_serviceProvider = serviceProvider;
		}

		/// <summary>
		/// 发布（反射获取处理者）
		/// </summary>
		/// <param name="notification">通知</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns></returns>
		public async Task PublishAsync(INotification notification, CancellationToken cancellationToken = default)
		{
			var handlerType = typeof(INotificationHandler<>).MakeGenericType(notification.GetType());

			var handlers = _serviceProvider.GetServices(handlerType);

			foreach(var handler in handlers)
			{
				// 使用反射调用 Handle 方法
				var handleMethod = handlerType.GetMethod("HandleAsync");
				if(handleMethod == null)
				{
					throw new InvalidOperationException($"Handle method not found on {handlerType.Name}");
				}

				var task = (Task)handleMethod.Invoke(handler, new object[] { notification, cancellationToken }) ?? throw new Exception("");

				await task;
			}
		}

		/// <summary>
		/// 发布（服务定位获取处理者）
		/// </summary>
		/// <typeparam name="TNotification">通知类型</typeparam>
		/// <param name="notification">通知</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns></returns>
		public async Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
		{
			var handlers = _serviceProvider.GetServices<INotificationHandler<TNotification>>();
			foreach(var handler in handlers)
			{
				await handler.HandleAsync(notification, cancellationToken);
			}
		}
	}
}
