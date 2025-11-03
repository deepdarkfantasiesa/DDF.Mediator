namespace DDF.Mediator.Abstractions
{
	/// <summary>
	/// 请求标记接口
	/// </summary>
	/// <typeparam name="TResponse">请求响应类型</typeparam>
	public interface IRequest<out TResponse>
	{
	}

	/// <summary>
	/// 请求处理者标记接口
	/// </summary>
	/// <typeparam name="TRequest">请求类型</typeparam>
	/// <typeparam name="TResponse">响应类型</typeparam>
	public interface IRequestHandler<in TRequest,TResponse>
		where TRequest: IRequest<TResponse>
	{
		/// <summary>
		/// 处理请求
		/// </summary>
		/// <param name="request">请求</param>
		/// <param name="cancellationToken">取消token</param>
		/// <returns></returns>
		Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
	}
}
