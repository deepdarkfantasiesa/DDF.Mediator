namespace DDF.Mediator.Abstractions
{
	/// <summary>
	/// 通知标记接口
	/// </summary>
	public interface INotification
	{
	}

	/// <summary>
	/// 通知处理者标记接口
	/// </summary>
	/// <typeparam name="TNotification">通知类型</typeparam>
	public interface INotificationHandler<in TNotification>
		where TNotification : INotification
	{
		/// <summary>
		/// 处理通知
		/// </summary>
		/// <param name="notification">通知</param>
		/// <param name="cancellationToken">取消token</param>
		/// <returns></returns>
		Task HandleAsync(TNotification notification, CancellationToken cancellationToken = default);
	}
}
