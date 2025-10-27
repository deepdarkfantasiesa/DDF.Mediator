# DDF.Mediator

一个聚焦于“可组合抽象 + 可控管道行为 + CQRS + DDD + 领域事件”整合的轻量级 .NET 中介者框架。  
相比传统 Mediator，本框架强调：通过标记接口驱动行为选择，通过可排序的管道（Pipeline Behavior）构建责任链，通过领域事件与聚合根交互实现事务内协作。

> 当前仍处在快速演进阶段，接口可能调整。

---

## 1. 核心抽象层 (Abstractions)

本框架的精髓在于抽象层，主角包括：

### 1.1 IPipelineBehavior
定义请求执行的“可插拔处理环节”。特点：
- 可通过 Attribute（例如 `[PipelineBehaviorPriority(1)]`）在实现类上配置指定优先级。
- 支持在构建执行链时排序。
- 形成“先进后出”（栈式包裹）调用模型：最优先的行为最先进入、最后退出。

### 1.2 IRequest
表示一个“需唯一 Handler 处理”的请求类型。  
一个具体 Request（继承 `IRequest<TResponse>`）：
- 对应 1 个 Handler（`IRequestHandler<TRequest,TResponse>`）
- 可匹配多个管道行为：框架根据该 Request 额外实现的标记接口（例如 `IValidate`, `ICommand` 等）筛选管道集合，排序后封装成责任链。
- 管道行为按优先级构建调用包裹，体现“先进后出”。

### 1.3 INotification
表示一个“可广播”的事件消息：
- 一个 `INotification` 对应 N 个 `INotificationHandler<TNotification>`。
- 可用于领域事件分发 / 集成事件转换等。

### 1.4 IStream（流式响应）
用于长任务 / 数据分段返回 / 推送式处理：
- 按需消费，降低一次性内存压力
- 支持实时/大数据分页、日志拉取、增量同步

---

## 2. 调度器的核心机制

Request
1. 收集与当前 Request 类型匹配的所有管道行为实现。
2. 根据行为的优先级（例如通过 Attribute 或注册配置）进行升序排序。
3. 构建责任链（Chain of Responsibility）：
   - 最低数字优先级的行为最外层先执行其“前置逻辑”，然后调用 next
   - 直到最内层的最终 Handler 被调用
   - 返回时按照栈回退顺序执行“后置逻辑”

Notification
1. 获取所有匹配的Handler
2. 依次执行Handler（不保证顺序）

Stream 
1.  获取匹配的Handler（单个）
2.  运行Handler并进行流式返回

## 3. CQRS 扩展：标记接口体系

通过继承自 `IRequest<TResponse>` 的标记接口，让调度器“识别所需管道行为”和最终的Handler：

常用的标记接口有：

| 标记接口 | 作用 | 通常关联的管道行为 |
|----------|------|--------------------|
| IValidate | 需要输入校验 | ValidatorBehavior (优先级 1) |
| IDistributedLock | 分布式锁保护 | DistributedLockBehavior (优先级 2) |
| **ICommand** | 命令语义（修改状态） | TransactionBehavior (优先级 3) |
| **IQuery** | 查询语义（只读） | 可与 QueryReplica / QueryCache 联用 |
| IQueryCache | 需要缓存 | QueryCacheBehavior (优先级 2) |
| IQueryReplica | 走只读库 / 切换连接字符串 | QueryReplicaBehavior (优先级 3) |

处理者标记接口（继承自 `IRequestHandler<TRequest,TResponse>`）进一步让语义明确：
- **ICommandHandler<TCommand, TResult>**
- **IQueryHandler<TQuery, TResult>**

---

## 4. 常用的管道行为（示例设计）

由前可知，Request 的调度中心是通过识别 IRequest (及其继承接口)来决定从服务提供者获取哪些管道行为，基于这一点，我们可以做到自定义不同的标记接口和对应的管道行为，为请求对象在被调度的过程中赋予不同的能力

| 行为 | 优先级(Priority) | 适用 | 说明 |
|------|------------------|------|------|
| ValidatorBehavior | 1 | IValidate | 执行数据注解 / FluentValidation；若失败短路 |
| DistributedLockBehavior | 2 | IDistributedLock | 获取锁（Redis / DB）；执行完释放；支持重入检测 |
| QueryCacheBehavior | 2 | IQueryCache | 先查缓存；命中则短路返回；未命中执行 next 并写入缓存 |
| TransactionBehavior | 3 | ICommand | 若当前 DbContext 已有事务则复用，否则开启新事务包裹 next |
| QueryReplicaBehavior | 3 | IQueryReplica | 在当前作用域将连接字符串/ DbContext 指向只读副本，执行完还原 |

TODO：这里如果有优先级冲突，那么在程序启动注册时就该报错，计划将来支持这一点

---

## 5. 应用示例

### 5.1 Command 类型：继承 ICommand, IValidate, IDistributedLock

```csharp
public sealed record CreateOrderCommand(
    Guid OrderId,
    Guid UserId,
    IReadOnlyList<OrderLineDto> Lines
) : ICommand<bool>, IValidate<bool>, IDistributedLock<bool>;
```

调度器匹配行为：
- ValidatorBehavior (1)
- DistributedLockBehavior (2)
- TransactionBehavior (3)

进入顺序：
1. ValidatorBehavior
2. DistributedLockBehavior
3. TransactionBehavior

**执行 Handler (CreateOrderCommandHandler)**

退出顺序（栈回退）：
- TransactionBehavior（提交或回滚）
- DistributedLockBehavior（释放锁）
- ValidatorBehavior（结束作用域）

### 5.2 一个 Query 类型：继承 IQuery, IQueryCache, IQueryReplica

```csharp
public sealed record GetUserProfileQuery(Guid UserId)
    : IQuery<UserProfileDto>, IQueryCache<UserProfileDto>, IQueryReplica<UserProfileDto>;
```

调度器匹配行为：
- QueryCacheBehavior (2)
- QueryReplicaBehavior (3)

进入顺序：
1. QueryCacheBehavior（尝试命中缓存）
2. QueryReplicaBehavior（切换到只读库）

**执行 Handler (GetUserProfileQuery)**

退出顺序：
- QueryReplicaBehavior（还原连接）
- QueryCacheBehavior（写回缓存）

若 Query 同时需要校验，可增加 IValidate，则 ValidatorBehavior (1) 会成为最外层。

---

## 6. DDD 集成：高内聚低耦合的实现方式

通过中介者 + 标记管道行为，让聚合根逻辑与跨领域协作解耦：

### 核心理念
- 聚合根（Aggregate Root）只暴露行为方法（充血模型）
- 行为内部修改自身状态并向 `DomainEvents` 集合添加事件对象（对象实现 `IDomainEvent`）
- 在应用层（CommandHandler）中完成对聚合根的操作后，在请求回到 TransactionBehavior 时拦截(或者调用重写的SaveChanges) SaveChanges：
  1. 收集所有被追踪实体的 `DomainEvents`
  2. 清空实体上的事件集合（防止重复发布）
  3. 通过 Mediator 发布每一个事件（`IDomainEvent : INotification`）
  4. 由对应的事件 Handler（可能触发新的 Command）进行跨聚合协作
- 利用事务行为(TransactionBehavior)：所有级联触发的命令在同一事务内运行，在代码层面实现类似数据库触发器的效果

### 示例抽象
```csharp
public interface IDomainEvent : INotification
{
    DateTime OccurredOn { get; }
}

public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = new();
    protected void AddDomainEvent(IDomainEvent evt) => _domainEvents.Add(evt);
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;
    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

### 聚合示例
```csharp
public sealed class Order : AggregateRoot
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public List<OrderLine> Lines { get; private set; } = new();

    public void AddLine(Guid productId, int quantity, decimal price)
    {
        Lines.Add(new OrderLine(productId, quantity, price));
        AddDomainEvent(new OrderLineAddedDomainEvent(Id, productId));
    }

    public void Confirm()
    {
        // ...业务校验
        AddDomainEvent(new OrderConfirmedDomainEvent(Id, UserId));
    }
}
```

### 在 CommandHandler 中
```csharp
public sealed class CreateOrderCommandHandler 
    : ICommandHandler<CreateOrderCommand, bool>
{
    private readonly AppDbContext _db;
    private readonly IMediator _mediator;

    public bool Handle(CreateOrderCommand cmd)
    {
        var order = new Order(cmd.OrderId, cmd.UserId);
        foreach (var l in cmd.Lines)
            order.AddLine(l.ProductId, l.Quantity, l.Price);

        //CommandHandler中一般不需要手动SaveChange、Commit、RollBack，这些操作交给TransactionBehavior即可
        //_db.Orders.Add(order);
        //_db.SaveChanges();

        // CommandHandler执行完之后，请求会回到 TransactionBehavior 的 next() 之后
        return true;
    }

    // SaveChanges 重载方法里：
    // 1. 调用 SaveChanges 将变更的数据作用于数据库，但不提交事务
    // 2. 收集所有状态为已变更的聚合根的 DomainEvents
    // 3. 清空 DomainEvents
    // 4. 通过中介者 Publish 事件 -> 触发对应的 INotificationHandler
    // 5. 假如 INotificationHandler 中通过中介者触发了新的级联 Command ，新的 Command 依然包裹在前面开启的事务中，如果没有新的 Command 则提交事务
}

```

### 事件驱动的协作
例如 `OrderConfirmedDomainEventHandler` 触发：
- 发送积分累加命令（通过中介者触发新的 Command,新 Command 复用事务）
- 分发消息到外部系统（比如同步数据到ES、基于TagBase删除Redis中对应的缓存、或是将向MQ发送集成事件）

### 优势
- 高内聚：聚合行为与领域事件聚焦本身逻辑
- 低耦合：跨聚合协作通过事件编排
- 关注点分离：验证、事务、锁、缓存、读库路由全部由管道行为处理
- 可测试性：每个 Handler / Behavior / Aggregate 可独立测试
- 可插拔：新增一个行为仅需实现 `IPipelineBehavior` + 标记接口策略，即自动参与链

---

## 7. 行为优先级与顺序总结

| 优先级 | 建议放置的职责 | 原因 |
|--------|----------------|------|
| 1 | 校验（Validator） | 最早失败，避免后续资源消耗 |
| 2 | 分布式锁 / 缓存 | 锁要在事务前；缓存查询前置可短路 |
| 3 | 事务 / 读库切换 | 事务应包裹核心执行；读库路由在缓存后 |

进入：1 → 2 → 3 → Handler  
退出：Handler → 3 → 2 → 1

---

## 8. 使用建议

- 聚合根仅暴露行为方法，不直接让外层修改集合属性
- 在事务行为中确保领域事件发布时机（保存后但未提交 / 提交前 / 提交后）根据业务选择
- 对幂等的领域事件设计“事件去重”策略（可记录事件 ID）
- 分布式锁注意锁粒度（使用 Request 的业务键而非全局键）
- QueryCache 行为需根据 Request 序列化键（可实现 ICacheKeyProvider）

---

## 9. FAQ

| 问题 | 解答 |
|------|------|
| 为什么使用标记接口而不是特性？ | 标记接口更易于在类型系统内组合与扫描，且可与泛型约束协同。 |
| 如何避免行为过多导致性能下降？ | 控制行为拆分粒度，合并低复杂度行为；增加 Metrics 监控。 |
| 领域事件是否会导致循环触发？ | 需要在设计上避免命令互相生成同类事件；可加入“事件深度”限制。 |

---

## 10. 致谢与贡献

欢迎通过 [Issues](https://github.com/deepdarkfantasiesa/DDF.Mediator/issues) 与 Pull Requests 参与：
- 新增行为
- 性能优化
- 领域事件最佳实践示例

贡献建议：
1. 描述动机与场景
2. 提供测试与文档片段
3. 遵循既定命名与代码风格

---
