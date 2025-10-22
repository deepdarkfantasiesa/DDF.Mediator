namespace DDF.Mediator.Abstractions;

/// <summary>
/// ��֤��ǽӿ�
/// </summary>
/// <typeparam name="TResponse">��Ӧ����</typeparam>
public interface IValidatior<out TResponse>:IRequest<TResponse> { }

/// <summary>
/// �����ǽӿ�
/// </summary>
/// <typeparam name="TResponse"></typeparam>
public interface ICommand<out TResponse>: IRequest<TResponse> { }

/// <summary>
/// ������߱�ǽӿ�
/// </summary>
/// <typeparam name="TCommand">��������</typeparam>
/// <typeparam name="TResponse">��Ӧ����</typeparam>
public interface ICommandHandler<TCommand, TResponse>: IRequestHandler<TCommand, TResponse>
	where TCommand : ICommand<TResponse>
{ }

/// <summary>
/// �ֲ�ʽ����ǽӿ�
/// </summary>
/// <typeparam name="TResponse">��Ӧ����</typeparam>
public interface IDistributedLock<out TResponse>: IRequest<TResponse> { }

/// <summary>
/// query������ǽӿ�
/// </summary>
public interface IQueryParam { }

/// <summary>
/// query��ǽӿ�
/// </summary>
/// <typeparam name="TParams">query��������</typeparam>
/// <typeparam name="TResponse">��Ӧ����</typeparam>
public interface IQuery<TParams, out TResponse>: IRequest<TResponse>
	where TParams : IQueryParam
{ }

/// <summary>
/// query�����ǽӿ�
/// </summary>
/// <typeparam name="TResponse">��Ӧ����</typeparam>
public interface IQueryCache<out TResponse>: IRequest<TResponse> { }

/// <summary>
/// query�����߱�ǽӿ�
/// </summary>
/// <typeparam name="TQuery">query����</typeparam>
/// <typeparam name="TParams">query��������</typeparam>
/// <typeparam name="TResponse">��Ӧ����</typeparam>
public interface IQueryHandler<TQuery, TParams, TResponse>: IRequestHandler<TQuery, TResponse>
	where TQuery : IQuery<TParams, TResponse>
	where TParams : IQueryParam
{ }

/// <summary>
/// �����¼���ǽӿ�
/// </summary>
public interface IDomainEvent: INotification { }

/// <summary>
/// �����¼������߱�ǽӿ�
/// </summary>
/// <typeparam name="TDomainEvent">�����¼�����</typeparam>
public interface IDomainEventHandler<TDomainEvent>: INotificationHandler<TDomainEvent>
	where TDomainEvent : IDomainEvent
{ }
