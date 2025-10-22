namespace DDF.Mediator.Abstractions;

/// <summary>
/// 验证标记接口
/// </summary>
/// <typeparam name="TResponse">响应类型</typeparam>
public interface IValidatior<out TResponse>:IRequest<TResponse> { }

/// <summary>
/// 命令标记接口
/// </summary>
/// <typeparam name="TResponse"></typeparam>
public interface ICommand<out TResponse>: IRequest<TResponse> { }

/// <summary>
/// 命令处理者标记接口
/// </summary>
/// <typeparam name="TCommand">命令类型</typeparam>
/// <typeparam name="TResponse">响应类型</typeparam>
public interface ICommandHandler<TCommand, TResponse>: IRequestHandler<TCommand, TResponse>
	where TCommand : ICommand<TResponse>
{ }

/// <summary>
/// 分布式锁标记接口
/// </summary>
/// <typeparam name="TResponse">响应类型</typeparam>
public interface IDistributedLock<out TResponse>: IRequest<TResponse> { }

/// <summary>
/// query参数标记接口
/// </summary>
public interface IQueryParam { }

/// <summary>
/// query标记接口
/// </summary>
/// <typeparam name="TParams">query参数类型</typeparam>
/// <typeparam name="TResponse">响应类型</typeparam>
public interface IQuery<TParams, out TResponse>: IRequest<TResponse>
	where TParams : IQueryParam
{ }

/// <summary>
/// query缓存标记接口
/// </summary>
/// <typeparam name="TResponse">响应类型</typeparam>
public interface IQueryCache<out TResponse>: IRequest<TResponse> { }

/// <summary>
/// query处理者标记接口
/// </summary>
/// <typeparam name="TQuery">query类型</typeparam>
/// <typeparam name="TParams">query参数类型</typeparam>
/// <typeparam name="TResponse">响应类型</typeparam>
public interface IQueryHandler<TQuery, TParams, TResponse>: IRequestHandler<TQuery, TResponse>
	where TQuery : IQuery<TParams, TResponse>
	where TParams : IQueryParam
{ }

/// <summary>
/// 领域事件标记接口
/// </summary>
public interface IDomainEvent: INotification { }

/// <summary>
/// 领域事件处理者标记接口
/// </summary>
/// <typeparam name="TDomainEvent">领域事件类型</typeparam>
public interface IDomainEventHandler<TDomainEvent>: INotificationHandler<TDomainEvent>
	where TDomainEvent : IDomainEvent
{ }
