namespace DDF.Mediator.Abstractions;

// Command abstractions
public interface ICommand<out TResponse> : IRequest<TResponse> { }

public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{ }

// Command with lock marker
public interface ICommandLock<out TResponse> : IRequest<TResponse> { }

// Query abstractions
public interface IQueryParam { }

public interface IQuery<TParams, out TResponse> : IRequest<TResponse>
    where TParams : IQueryParam
{ }

public interface IQueryCache<out TResponse> : IRequest<TResponse> { }

public interface IQueryHandler<TQuery, TParams, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TParams, TResponse>
    where TParams : IQueryParam
{ }

// Domain event abstractions
public interface IDomainEvent : INotification { }

// NOTE: ԭ��ܵ� INotificationHandler ֻ��һ�����Ͳ������޷���ֵ�����ﶨ��һ�������ӿ��Ա�������¼����������塣
public interface IDomainEventHandler<TDomainEvent> : INotificationHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent
{ }
