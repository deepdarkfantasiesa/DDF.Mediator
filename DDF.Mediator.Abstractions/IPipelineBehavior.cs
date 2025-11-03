namespace DDF.Mediator.Abstractions
{
	/// <summary>
	/// 下一个处理者的委托
	/// </summary>
	/// <typeparam name="TResponse">响应类型</typeparam>
	/// <param name="cancellationToken">取消token</param>
	/// <returns></returns>
	public delegate Task<TResponse> NextHandlerDelegate<TResponse>(CancellationToken cancellationToken = default);

	/// <summary>
	/// 管道行为标记接口
	/// </summary>
	public interface IPipelineBehavior<in TRequest, TResponse>
		where TRequest : IRequest<TResponse>
	{
		/// <summary>
		/// 管道处理
		/// </summary>
		/// <param name="request">请求</param>
		/// <param name="next">下一个处理者的委托</param>
		/// <param name="cancellationToken">取消token</param>
		/// <returns></returns>
		Task<TResponse> HandleAsync(TRequest request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default);
	}

	/// <summary>
	/// 管道行为优先级特性
	/// 数字越小优先级越高
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public sealed class PipelineBehaviorPriorityAttribute: Attribute
	{
		/// <summary>
		/// 等级
		/// </summary>
		public int Level { get; }

		/// <summary>
		/// 管道行为优先级特性
		/// 数字越小优先级越高
		/// </summary>
		/// <param name="level">等级</param>
		public PipelineBehaviorPriorityAttribute(int level)
		{
			Level = level;
		}
	}
}
