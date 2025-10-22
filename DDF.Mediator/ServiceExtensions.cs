using DDF.Mediator.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DDF.Mediator
{
	/// <summary>
	/// 拓展函数
	/// </summary>
	public static class ServiceExtensions
	{
		/// <summary>
		/// 注册中介者
		/// </summary>
		/// <param name="services"></param>
		/// <param name="pipelineBehaviors">管道行为</param>
		/// <returns></returns>
		/// <exception cref="Exception"></exception>
		public static IServiceCollection AddMediator(this IServiceCollection services, params Type[] pipelineBehaviors)
		{
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();

			#region 注册StreamRequest
			var streamRequestTypes = assemblies.SelectMany(t => t.GetTypes())
				.Where(t => t.IsClass
					&& !t.IsAbstract
					&& t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStream<>)))
				.ToList();

			// 遍历所有 IRequest 类型，注册对应的 IRequestHandler
			foreach(var streamRequestType in streamRequestTypes)
			{
				var responseType = streamRequestType.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStream<>)).GetGenericArguments()[0];
				var handlerType = typeof(IStreamHandler<,>).MakeGenericType(streamRequestType, responseType);

				var implementationType = assemblies.SelectMany(t => t.GetTypes())
					.FirstOrDefault(t => t.GetInterfaces().Contains(handlerType));

				if(implementationType != null)
				{
					services.TryAddTransient(handlerType, implementationType);
				}
			}
			#endregion

			#region 注册Request
			var requestTypes = assemblies.SelectMany(t => t.GetTypes())
				.Where(t => t.IsClass
					&& !t.IsAbstract
					&& t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)))
				.ToList();

			// 遍历所有 IRequest 类型，注册对应的 IRequestHandler
			foreach(var requestType in requestTypes)
			{
				var responseType = requestType.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)).GetGenericArguments()[0];
				var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);

				var implementationType = assemblies.SelectMany(t => t.GetTypes())
					.FirstOrDefault(t => t.GetInterfaces().Contains(handlerType));

				if(implementationType != null)
				{
					services.TryAddTransient(handlerType, implementationType);
				}
			}
			#endregion

			#region 注册Notification
			var notificationTypes = assemblies.SelectMany(t => t.GetTypes())
				.Where(t => t.IsClass
					&& !t.IsAbstract
					&& t.GetInterfaces().Any(i => i == typeof(INotification)))
				.ToList();

			// 遍历所有 INotification 类型，注册对应的 INotificationHandler
			foreach(var notificationType in notificationTypes)
			{
				var handlerType = typeof(INotificationHandler<>).MakeGenericType(notificationType);

				var implementationTypes = assemblies.SelectMany(t => t.GetTypes())
					.Where(t => t.GetInterfaces().Contains(handlerType))
					.ToList();

				foreach(var implementationType in implementationTypes)
				{
					services.AddTransient(handlerType, implementationType);
				}
			}
			#endregion

			#region 注册管道
			var pipelineInterfaceDefinitions = new List<Type>() { typeof(IPipelineBehavior<,>) };

			foreach(var behavior in pipelineBehaviors)
			{
				if(!behavior.IsSealed)
					throw new Exception($"管道行为{behavior}为非密封类");

				// 该类型直接实现的接口（排除其父接口的父接口）
				var allIfaces = behavior.GetInterfaces().ToList();
				var inheritedIfaces = allIfaces.SelectMany(i => i.GetInterfaces()).Distinct().ToList();
				var directIfaces = allIfaces.Except(inheritedIfaces).ToList();

				if(directIfaces == null || directIfaces.Count != 1)
					throw new Exception($"{behavior}只允许继承一种管道行为接口");

				Type? directIface = null;
				if(behavior.IsGenericType)
				{
					directIface = directIfaces.First().GetGenericTypeDefinition();
				}
				else
				{
					directIface = directIfaces.First();
				}
				if(!directIface.IsGenericType || !pipelineInterfaceDefinitions.Contains(directIface.GetGenericTypeDefinition()))
					throw new Exception("管道行为的直接继承接口不合法");

				// 为闭合泛型接口注册实现
				services.AddTransient(directIface, behavior);
			}
			#endregion

			#region 发送、发布的对象
			services.TryAddTransient<IStreamSender, StreamSender>();
			services.TryAddTransient<INotificationPublisher, NotificationPublisher>();
			services.TryAddTransient<IRequestSender, RequestSender>();
			services.TryAddTransient<IMediator, Mediator>();
			#endregion

			return services;
		}

	}
}
