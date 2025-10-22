using DDF.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using Xunit;

namespace DDF.Mediator.Tests
{
	/// <summary>
	/// 流式返回请求标识
	/// </summary>
	public sealed class RangeStreamRequest: IStream<int>
	{
		public int Start { get; init; }
		public int Count { get; init; }
	}

	/// <summary>
	/// 处理者
	/// </summary>
	public sealed class RangeStreamHandler: IStreamHandler<RangeStreamRequest, int>
	{
		public async IAsyncEnumerable<int> HandleAsync(RangeStreamRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			for(int i = 0; i < request.Count; i++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				yield return request.Start + i;
				await Task.Delay(10, cancellationToken); // 模拟异步
			}
		}
	}

	public class StreamRequestSenderTests
	{
		/// <summary>
		/// 基本调用
		/// </summary>
		/// <returns></returns>
		[Fact]
		public async Task StreamAsync_ReturnsExpectedRange()
		{
			var services = new ServiceCollection();
			services.AddMediator();
			var provider = services.BuildServiceProvider();
			var streamSender = provider.GetRequiredService<IStreamSender>();

			var request = new RangeStreamRequest { Start = 5, Count = 3 };
			var results = new List<int>();

			//反射
			await foreach(var item in streamSender.StreamAsync(request))
			{
				results.Add(item);
			}
			Assert.Equal(new[] { 5, 6, 7 }, results);

			results.Clear();

			//泛型
			await foreach(var item in streamSender.StreamAsync<RangeStreamRequest, int>(request)) 
			{
				results.Add(item);
			}
			Assert.Equal(new[] { 5, 6, 7 }, results);
		}

		/// <summary>
		/// 空入参
		/// </summary>
		/// <returns></returns>
		[Fact]
		public async Task StreamAsync_ThrowsOnNullRequest()
		{
			var services = new ServiceCollection();
			services.AddMediator();
			var provider = services.BuildServiceProvider();
			var streamSender = provider.GetRequiredService<IStreamSender>();

			await Assert.ThrowsAsync<ArgumentNullException>(() => streamSender.StreamAsync<int>(null!).GetAsyncEnumerator().MoveNextAsync().AsTask());
		}

		/// <summary>
		/// 取消任务
		/// </summary>
		/// <returns></returns>
		[Fact]
		public async Task StreamAsync_CanBeCancelled()
		{
			var services = new ServiceCollection();
			services.AddMediator();
			var provider = services.BuildServiceProvider();
			var streamSender = provider.GetRequiredService<IStreamSender>();

			var request = new RangeStreamRequest { Start = 0, Count = 100 };
			using var cts = new CancellationTokenSource();
			cts.Cancel();

			await Assert.ThrowsAsync<OperationCanceledException>(async () =>
			{
				await foreach(var _ in streamSender.StreamAsync(request, cts.Token)) { }
			});

			await Assert.ThrowsAsync<OperationCanceledException>(async () =>
			{
				await foreach(var _ in streamSender.StreamAsync<RangeStreamRequest, int>(request, cts.Token)) { }
			});
		}
	}
}
