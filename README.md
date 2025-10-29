# DDF.Mediator

一款轻量级中介者，让开发者快速构建 **命令与查询职责分离(CQRS)+领域驱动设计(DDD)** 架构的项目，让代码的**耦合度更低**、**可维护性更高**

> 当前仍处在快速演进阶段，接口可能调整。

---

## 基本约束和说明

| 分类 | 内容 | 生命周期  | 是否必须声明为密封类（Sealed） |
|------|------|----------------|-----------|
| 标记接口实现 | `IRequest<>`、`INotification`、`IStream<>` 的实现类 | 无生命周期 | 是 |
| 标记接口实现（管道行为） | `IPipelineBehavior<,>` 的实现类 | `Transient` | 是 |
| 处理者实现 | `IRequestHandler<,>`、`INotificationHandler<>`、`IStreamHandler<,>` 的实现类 | `Transient` | 是 |
| 调度中心对象 | 通过 `IMediator`、`IRequestSender`、`INotificationPublisher`、`IStreamSender` 获取的调度中心实例 | `Transient` | 是 |

## 安装
在 "管理NuGet包" 中搜索 "DDF.mediator"

可以看到两个包，分别是 "DDF.Mediator.Abstractions" 、 "DDF.Mediator" 

 "DDF.Mediator.Abstractions" 中是标记接口的包

 "DDF.Mediator" 是调度中心的实现包

项目可根据需要在抽象层安装 "DDF.Mediator.Abstractions" ，在应用层安装 "DDF.Mediator" 

## 基本使用

让程序启动时扫描程序集注册所有Handler
```csharp
builder.Services.AddMediator();
```

### 1.Request
首先定义一个IRequest<>的继承类
```csharp
public sealed record Ping:IRequest<string>
{
	public string Echo { get; init; }
}
```

再定义一个对应的RequestHandler
```csharp
public sealed class Pong: IRequestHandler<Ping, string>
{
	public async Task<string> HandleAsync(Ping request, CancellationToken cancellationToken = default)
	{
		return request.Echo + "Pong";
	}
}
```

最后通过中介者调用
```csharp
//从依赖注入容器中通过IMediator和IRequestSender分别获取mediator、sender，
//可以通过以下四种方式调用，推荐使用指定泛型类型的方式调用，
//因为另一种使用了反射，性能理论上不如纯泛型的好
var mReflectionResponse = await mediator.SendAsync(new Ping { Echo = "Ping-" });//Ping-Pong

var mGenericResponse = await mediator.SendAsync<Ping, string>(new Ping { Echo = "Ping~" });//Ping~Pong

var sReflectionResponse = await sender.SendAsync(new Ping { Echo = "Ping%" });//Ping%Pong

var sGenericResponse = await sender.SendAsync<Ping, string>(new Ping { Echo = "Ping*" });//Ping*Pong
```

定义管道行为，注意一定要指定优先级
```csharp
[PipelineBehaviorPriority(0)]//指定管道行为的优先级
public sealed class TestBehavior<TRequest, TResponse>(ILogger<TestBehavior<TRequest, TResponse>> logger): IPipelineBehavior<TRequest, TResponse>
	where TRequest : IRequest<TResponse>
{
	public async Task<TResponse> HandleAsync(TRequest request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
	{
		logger.LogInformation("TestBehavior-Before");
		var response = await next();
		logger.LogInformation("TestBehavior-After");
		return response;
	}
}
```

然后注册进容器里
```csharp
builder.Services.AddMediator(typeof(TestBehavior<,>));
```

最后再使用mediator或sender以Ping类型的对象为参数发送请求

可以发现运行Pong之前控制台会输出"TestBehavior-Before"

运行Pong之后控制台会输出"TestBehavior-After"

不同的PipelineBehavior可以按照优先级从小到大的顺序嵌套在最终的Handler外层;

### 2.Notification

定义一个INotification的继承类

```csharp
public sealed record Bye: INotification
{
	public string SaySomeThing { get; init; }
}
```

然后定义两个（也可以更多）对应的Handler
```csharp
public sealed class Hi(ILogger<Hi> logger): INotificationHandler<Bye>
{
	public async Task HandleAsync(Bye notification, CancellationToken cancellationToken = default)
	{
		logger.LogInformation($"hi!{notification.SaySomeThing}");
	}
}

public sealed class Hello(ILogger<Hello> logger): INotificationHandler<Bye>
{
	public async Task HandleAsync(Bye notification, CancellationToken cancellationToken = default)
	{
		logger.LogInformation($"hello~{notification.SaySomeThing}");
	}
}
```

最后通过中介者调用
```csharp
//从依赖注入容器中通过IMediator和INotificationPublisher分别获取mediator、publisher，
//通过以下四种方式调用
//NotificationHandler的执行是串行但不保证顺序的
//NotificationHandler的执行不会套用PipelineBehavior

await mediator.PublishAsync(new Bye { SaySomeThing = "see you tomorrow" });//"hi!see you tomorrow"、"hello~see you tomorrow"

await mediator.PublishAsync<Bye>(new Bye { SaySomeThing = "see you soon" });//"hi!see you soon"、"hello~see you soon"

await publisher.PublishAsync(new Bye { SaySomeThing = "goodbye" });//"hi!goodbye"、"hello~goodbye"

await publisher.PublishAsync<Bye>(new Bye { SaySomeThing = "bye bye" });//"hi!bye bye"、"hello~bye bye"

```

### 3.Stream

定义一个IStream<>的继承类
```csharp
public sealed record Week:IStream<string>
{
	public string WhatADay { get; init; }
}
```

再定义一个对应的IStreamHandler
```csharp
public sealed class EachDay: IStreamHandler<Week, string>
{
	public async IAsyncEnumerable<string> HandleAsync(Week request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var days = new List<string> { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

		foreach (var day in days) 
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return request.WhatADay + day;
			await Task.Delay(10, cancellationToken); // 模拟异步
		}
	}
}
```

最后通过中介者调用
```csharp
//从依赖注入容器中通过IMediator和IStreamSender分别获取mediator、streamSender，
//通过以下四种方式调用
//IStreamHandler的执行不会套用PipelineBehavior

var workDays = new List<string>();//{ "workMonday", "workTuesday", "workWednesday", "workThursday", "workFriday", "workSaturday", "workSunday" }
await foreach(var item in mediator.StreamAsync(new Week { WhatADay = "work" }))
{
    workDays.Add(item);
}

var restDays = new List<string>();//{ "restMonday", "restTuesday", "restWednesday", "restThursday", "restFriday", "restSaturday", "restSunday" }
await foreach(var item in mediator.StreamAsync<Week, string>(new Week { WhatADay = "rest" }))
{
	restDays.Add(item);
}

var holidayDays = new List<string>();//{ "holidayMonday", "holidayTuesday", "holidayWednesday", "holidayThursday", "holidayFriday", "holidaySaturday", "holidaySunday" }
await foreach(var item in streamSender.StreamAsync(new Week { WhatADay = "holiday" })) 
{
	holidayDays.Add(item);
}

var vacations = new List<string>();//{ "vacationMonday", "vacationTuesday", "vacationWednesday", "vacationThursday", "vacationFriday", "vacationSaturday", "vacationSunday" }
await foreach(var item in streamSender.StreamAsync<Week, string>(new Week { WhatADay = "vacation" })) 
{
	vacations.Add(item);
}

```


## 核心抽象层 (Abstractions)

### 1. IPipelineBehavior
可插拔请求执行管道接口：
- 可通过 Attribute（例如 `[PipelineBehaviorPriority(1)]`）在实现类上配置指定优先级。
- 支持在构建执行链时排序。
- 形成“先进后出”（栈式包裹）调用模型：最优先的行为最先进入、最后退出。
- 实现类需要直接且只继承IPipelineBehavior

### 2. IRequest  
请求标记接口：
- 对应 1 个 Handler（`IRequestHandler<TRequest,TResponse>`）
- 在调用时可被匹配的管道行为构建为调用链。
- 可自定义返回类型

### 3. INotification
“可广播”的消息标记接口：
- 一个 `INotification` 对应 N 个 `INotificationHandler<TNotification>`。
- 可用于领域事件分发 / 集成事件转换等。
- 无返回值

### 4. IStream（流式响应）
用于长任务 / 数据分段返回 / 推送式处理：
- 按需消费，降低一次性内存压力
- 支持实时/大数据分页、日志拉取、增量同步

---

## 调度器的核心机制

### 1. RequestSender

 1.1 收集与当前 Request 类型匹配的所有管道行为实现和对应的Handler。

 1.2 根据行为的优先级（例如通过 Attribute 或注册配置）进行升序排序。

 1.3 构建责任链（Chain of Responsibility）：
   - 最低数字优先级的行为最外层先执行其“前置逻辑”，然后调用 next
   - 直到最内层的最终 Handler 被调用
   - 返回时按照栈回退顺序执行“后置逻辑”

### 2. NotificationPublisher

 2.1 获取所有匹配的Handler（可多个）

 2.2 依次执行Handler（不保证顺序）

### 3. StreamSender 

 3.1 获取与当前Stream匹配的Handler（单个）

 3.2 运行Handler并进行流式返回

## 进阶使用(CQRS+DDD)

常用的标记接口有：

| 标记接口 | 作用 | 关联的管道行为 |
|----------|------|--------------------|
| IValidate | 需要输入校验 | ValidatorBehavior (优先级 1) |
| IDistributedLock | 分布式锁保护 | DistributedLockBehavior (优先级 2) |
| **ICommand** | 命令语义（修改状态） | TransactionBehavior (优先级 3) |
| **IQuery** | 查询语义（只读） | 可与 QueryReplica / QueryCache 联用 |
| IQueryCache | 需要缓存 | QueryCacheBehavior (优先级 2) |
| IQueryReplica | 走只读库 / 切换连接字符串 | QueryReplicaBehavior (优先级 3) |
| **ICommandHandler<TCommand, TResult>** | 命令处理者 | / |
| **IQueryHandler<TQuery, TResult>** | 查询处理者 | / |

### 1. IValidate
验证标记接口
```csharp
public interface IValidate<TResponse>: IRequest<TResponse>
{
}
```

验证PipelineBehavior
```csharp
[PipelineBehaviorPriority(1)]
public sealed class ValidateBehavior<TValidate, TResponse>: IPipelineBehavior<TValidate, TResponse>
	where TValidate : IValidate<TResponse>
{
	public async Task<TResponse> HandleAsync(TValidate request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
	{
		var response = default(TResponse);

		//可以通过FluentValidation获取验证逻辑
		//横向拓展验证逻辑

		response = await next(cancellationToken);

		return response;
	}
}
```

### 2. IDistributedLock
分布式锁标记接口
```csharp
public interface IDistributedLock<TResponse>: IRequest<TResponse>
{
}
```

分布式锁PipelineBehavior
```csharp
[PipelineBehaviorPriority(2)]
public sealed class DistributedLockBehavior<TDistributedLock, TResponse>(ILogger<DistributedLockBehavior<TDistributedLock, TResponse>> logger): IPipelineBehavior<TDistributedLock, TResponse>
	where TDistributedLock : IDistributedLock<TResponse>
{
	public async Task<TResponse> HandleAsync(TDistributedLock request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
	{
		var response = default(TResponse);

		//向Redis集群获取红锁

		response = await next(cancellationToken);

		//释放锁

		return response;
	}
}
```

### 3. ICommand

命令标记接口
```csharp
public interface ICommand<TResponse>: IRequest<TResponse>
{
}
```

命令处理者标记接口
```csharp
public interface ICommandHandler<TCommand, TResponse>:IRequestHandler<TCommand, TResponse>
	where TCommand : ICommand<TResponse>
{
}
```

数据库事务PipelineBehavior
```csharp
[PipelineBehaviorPriority(3)]
public sealed class TransactionBehavior<TCommand, TResponse>(IDbTransaction _context): IPipelineBehavior<TCommand, TResponse>
	where TCommand : ICommand<TResponse>
{
	public async Task<TResponse> HandleAsync(TCommand request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
	{
            var response = default(TResponse);

            try
            {
                //判断当前上下文对象是否持有事务
                if (_context.HasActiveTransaction)
                {
                    response = await next(cancellationToken);

                    //持久化聚合并分发领域事件
                    await _context.SaveEntitiesAsync(cancellationToken);
                }
                else
                {
                    //开启事务
                    _context.BeginTransaction(cancellationToken);

                    response = await next(cancellationToken);

                    //持久化聚合并分发领域事件，假如领域事件触发了新的command在这里会有一个递归
                    await _context.SaveEntitiesAsync(cancellationToken);

                    //提交事务
                    await _context.CommitTransactionAsync(cancellationToken);
                }
                return response;
            }
            catch (Exception ex)
            {
                //回滚事务
                await _context.RollbackTransaction(cancellationToken);
                throw;
            }
	}
}
```


### 4. IQuery

查询标记接口

```csharp
public interface IQuery<TResponse>: IRequest<TResponse>
{
}
```

查询处理者标记接口
```csharp
public interface IQueryHandler<TQuery, TResponse>: IRequestHandler<TQuery, TResponse>
	where TQuery : IQuery<TResponse>
{
}
```

### 5. IQueryCache

查询缓存标记接口

```csharp
public interface IQueryCache<TResponse>: IRequest<TResponse>
{
}
```

查询缓存PipelineBehavior

```csharp
[PipelineBehaviorPriority(2)]
public sealed class QueryCacheBehavior<TQueryCache, TResponse>: IPipelineBehavior<TQueryCache, TResponse>
	where TQueryCache : IQueryCache<TResponse>
{
	public async Task<TResponse> HandleAsync(TQueryCache request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
	{
		var response = default(TResponse);

		//查询一级或二级缓存，如果命中则直接返回结果

		//如果没有命中缓存则视责任链的包装情况往下调用QueryReplicaBehavior<,>或对应的QueryHandler
		response = await next(cancellationToken);

		//将handler的结果写入分布式缓存并通过分布式缓存通知其他服务插入缓存

		//返回查询结果
		return response;
	}
}
```

### 6. IQueryReplica

查询从库标记接口

```csharp
public interface IQueryReplica<TResponse>: IRequest<TResponse>
{
}
```

```csharp
[PipelineBehaviorPriority(3)]
public sealed class QueryReplicaBehavior<TQueryReplica, TResponse>: IPipelineBehavior<TQueryReplica, TResponse>
	where TQueryReplica : IQueryReplica<TResponse>
{
	public async Task<TResponse> HandleAsync(TQueryReplica request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
	{
		var response = default(TResponse);

		//如果数据库做了集群，可在此处切换数据库连接字符串为同步从库或异步从库，减轻主库的压力

		//调用具体的handler
		response = await next(cancellationToken);

		return response;
	}
}
```

最后再向容器注册这些管道行为
```csharp
builder.Services.AddMediator(
	typeof(DistributedLockBehavior<,>),
	typeof(QueryCacheBehavior<,>),
	typeof(QueryReplicaBehavior<,>),
	typeof(TransactionBehavior<,>),
	typeof(ValidateBehavior<,>));
```

---

### 应用示例

#### 1. Command

自定义一个Command并继承 ICommand, IValidate, IDistributedLock
```csharp
public sealed record CreateOrderCommand(
    Guid OrderId,
    Guid UserId,
    IReadOnlyList<OrderLineDto> Lines
) : ICommand<bool>, IValidate<bool>, IDistributedLock<bool>;
```

在EndPoint、Controller Action、BackgroundHost、gRPC端点或MQ订阅者注入中介者发出这条命令

调度器会从服务提供者获取所需的 PipelineBehavior 并根据 Attribute 的值进行升序排序

排序后的 PipelineBehavior 依次为 ValidatorBehavior 、 DistributedLockBehavior 、 TransactionBehavior

进入顺序：
- ValidatorBehavior
- DistributedLockBehavior
- TransactionBehavior


**执行 Handler (CreateOrderCommandHandler)**


退出顺序（栈回退）：
- TransactionBehavior（提交或回滚）
- DistributedLockBehavior（释放锁）
- ValidatorBehavior（结束作用域）


请求调用顺序如图所示
![command](https://github.com/user-attachments/assets/19b99c11-ea76-4df0-a4f5-218b0b608f20)

关键点说明：

2.校验Command

4.获取分布式锁

6.开启数据库事务

8.获取数据库数据或向上下文操作

10.savechange并提交事务

12.释放分布式锁

#### 2. Query

自定义一个 Query：继承 IQuery, IQueryCache, IQueryReplica

```csharp
public sealed record GetUserProfileQuery(Guid UserId)
    : IQuery<UserProfileDto>, IQueryCache<UserProfileDto>, IQueryReplica<UserProfileDto>;
```

在EndPoint、Controller Action、gRPC端点注入中介者发出这条查询

调度器会从服务提供者获取所需的 PipelineBehavior 并根据 Attribute 的值进行升序排序

排序后的 PipelineBehavior 依次为 QueryCacheBehavior 、 QueryReplicaBehavior


进入顺序：
- QueryCacheBehavior（尝试命中缓存，如果命中则短路直接返回结果）
- QueryReplicaBehavior（切换到只读库）


**执行 Handler (GetUserProfileQuery)**


退出顺序：
- QueryReplicaBehavior
- QueryCacheBehavior（将查询结果写入缓存）


请求调用顺序如图所示
![Query](https://github.com/user-attachments/assets/763e66f7-5e38-401c-9bb6-a81c74a3fdb5)


关键点说明：

2.查询缓存

5.查询从库或主库（如果QueryReplicaBehavior没有切换连接字符串则查询主库）

8.将查询结果写入缓存

若 GetUserProfileQuery 同时需要校验，可追加继承 IValidate<UserProfileDto>，则 ValidatorBehavior 会成为调用责任链的最外层。

---

#### 3. DDD

通过 “中介者 + 管道行为 + 领域事件” 来让聚合之间在事务范围内协作：

##### 核心理念
- 聚合根（Aggregate Root）暴露行为方法（充血模型）
- 行为内部修改自身属性并向 `DomainEvents` 集合添加事件对象（对象实现 `IDomainEvent`）
- 在应用层（CommandHandler）中完成对聚合的操作后，在请求回到 TransactionBehavior 时拦截SaveChanges(见后自定义的SaveEntitiesAsync)：
  1. 收集所有被追踪实体的 `DomainEvents`
  2. 清空实体上的事件集合（防止重复发布）
  3. 通过 Mediator 发布每一个事件（`IDomainEvent : INotification`）
  4. 由对应的事件 Handler（可能触发新的 Command）进行跨聚合协作
- 利用事务行为(TransactionBehavior)：所有级联触发的命令在同一事务内运行，在代码层面实现类似数据库触发器的效果

##### 示例抽象

领域事件标记接口
```csharp
public interface IDomainEvent : INotification
{
}
```

领域事件处理者标记接口
```csharp
public interface IDomainEventHandler<TDomainEvent> : INotificationHandler<TDomainEvent>
 where TDomainEvent : IDomainEvent
{
}
```

聚合根抽象类
```csharp
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = new();
    protected void AddDomainEvent(IDomainEvent evt) => _domainEvents.Add(evt);
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;
    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

自定义的SaveEntitiesAsync
```csharp
  public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
  {
      //先持久化实体
      await base.SaveChangesAsync(cancellationToken);

	  //查询所有已跟踪且拥有领域事件的实体（或聚合根）

	  //获取所有领域事件

	  //清空聚合根中所有领域事件

	  //分发领域事件
	  foreach (var domainEvent in domainEvents)
	      await mediator.PublishAsync((dynamic)domainEvent, cancellationToken);//dynamic的作用是为了绑定领域事件的运行时类型

      return true;
  }
```

##### 聚合示例

确认订单命令
```csharp
public sealed record OrderConfirmedCommand(Guid OrderId):ICommand<bool>, IValidate<bool>, IDistributedLock<bool>;
```

确认订单领域事件
```csharp
public sealed record OrderConfirmedDomainEvent(Guid OrderId):IDomainEvent;
```

订单聚合根
```csharp
public sealed class Order : AggregateRoot
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public List<OrderLine> Lines { get; private set; } = new();

    public void Confirm(Guid orderId)
    {
        // ...业务校验
        AddDomainEvent(new OrderConfirmedDomainEvent(orderId));
    }
}
```

确认订单命令处理者
```csharp
public sealed class OrderConfirmedCommandHandler(Repo<Order> repo) 
    : ICommandHandler<OrderConfirmedCommand, bool>
{
    public async Task<string> HandleAsync(OrderConfirmedCommand request, CancellationToken cancellationToken = default)
    {
        var order = await repo.GeyByIdAsync(request.OrderId,cancellationToken);

        order.Confirm(request.OrderId);

        return true;
    }
}
```

##### 领域事件示例


删除订单相关的缓存处理者
```csharp
public sealed class DeleteCacheDomainEventHandler(IDistributedCache cache): IDomainEventHandler<OrderConfirmedDomainEvent>
{
	public async Task HandleAsync(OrderConfirmedDomainEvent notification, CancellationToken cancellationToken = default)
	{
		await cache.DeleteByTag(Tag.Order, cancellationToken);
	}
}
```

发出新的命令处理者
```csharp
public sealed class SendCommandDomainEventHandler(IMediator mediator): IDomainEventHandler<OrderConfirmedDomainEvent>
{
	public async Task HandleAsync(OrderConfirmedDomainEvent notification, CancellationToken cancellationToken = default)
	{
		await mediator.SendAsync(/* 实例化其他新的命令 */);
	}
}
```

向MQ发送集成事件处理者
```csharp
public sealed class PublishToMQDomainEventHandler: IDomainEventHandler<OrderConfirmedDomainEvent>
{
	public async Task HandleAsync(OrderConfirmedDomainEvent notification, CancellationToken cancellationToken = default)
	{
		//用发件箱模式向mq推送集成事件
	}
}
```

请求调用顺序如图所示
![DomainEvent](https://github.com/user-attachments/assets/cb3e9f9a-2d00-4fda-907f-e27c5a39f487)


关键点说明：

6 - 19 都处在同一数据库的事务中，聚合间交互的触发点在领域事件处理者的中介者发出新的命令

10.SaveEntitiesAsync

11.分发已改变跟踪状态聚合根中的领域事件(DomainEvent)

12.领域事件处理者的调用是串行但不保证顺序的，所以三个处理者对外部的调用都归为同一级

16.SaveEntitiesAsync

17.分发领域事件

19.提交数据库事务

21.释放分布式锁

##### 事件驱动的优势
- 领域逻辑与边缘影响分离
- 横向拓展边缘影响（可以有多个处理者）
- 事务一致（在领域事件处理者中通过中介者发出新的命令也在原有的事务之中）
- 不同的聚合之间解耦


---


## 致谢与贡献

欢迎通过 [Issues](https://github.com/deepdarkfantasiesa/DDF.Mediator/issues) 与 Pull Requests 参与：
- 新增行为
- 性能优化
- 领域事件最佳实践示例

贡献建议：
1. 描述动机与场景
2. 提供测试与文档片段
3. 遵循既定命名与代码风格

---
