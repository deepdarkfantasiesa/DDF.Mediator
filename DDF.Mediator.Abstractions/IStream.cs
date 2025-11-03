using System.Runtime.CompilerServices;

namespace DDF.Mediator.Abstractions
{
	/// <summary>
	/// 流式响应请求标记
	/// </summary>
	/// <typeparam name="Response">流式响应元素类型</typeparam>
	public interface IStream<out Response>
	{
	}

	/// <summary>
	/// 流式请求处理者
	/// </summary>
	/// <typeparam name="TRequest">请求类型</typeparam>
	/// <typeparam name="TResponse">流式响应元素类型</typeparam>
	public interface IStreamHandler<in TRequest, out TResponse>
		where TRequest : IStream<TResponse>
	{
		/// <summary>
		/// 处理流式请求
		/// </summary>
		/// <param name="request">请求</param>
		/// <param name="cancellationToken">取消token</param>
		/// <returns>异步可枚举响应</returns>
		IAsyncEnumerable<TResponse> HandleAsync(TRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default);
	}
}
