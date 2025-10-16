# 接口参数 SendAsync 方法测试用例文档

## 概述
本文档说明为 `IRequestSender.SendAsync<TResponse>(IRequest<TResponse> request)` 和 `IMediator.SendAsync<TResponse>(IRequest<TResponse> request)` 新增方法生成的测试用例。

## 新增方法签名
```csharp
// IRequestSender
Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

// IMediator
Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
```

## 测试文件

### 1. InterfaceBasedSendAsyncTests.cs
测试 `IRequestSender.SendAsync<TResponse>(IRequest<TResponse>)` 的核心功能。

#### 测试用例清单

| 测试方法 | 测试目的 | 预期结果 |
|---------|---------|---------|
| `SendAsync_WithInterfaceParameter_ReturnsCorrectResult` | 验证接口参数调用返回正确结果 | 返回 "InterfaceBased:Test" |
| `SendAsync_WithInterfaceParameter_ExecutesBehaviors` | 验证管道行为按优先级执行 | 按 Validation(1) -> Logging(2) 顺序执行 |
| `SendAsync_WithInterfaceParameter_ThrowsWhenNull` | 验证空参数抛出异常 | 抛出 ArgumentNullException |
| `SendAsync_WithInterfaceParameter_SupportsPolymorphism` | 验证多态请求处理 | 不同具体类型得到不同结果 |
| `SendAsync_WithInterfaceParameter_ThrowsWhenHandlerMissing` | 验证缺少处理者时抛出异常 | 抛出 InvalidOperationException |
| `SendAsync_WithInterfaceParameter_HandlesExceptions` | 验证异常包装 | 包装为 InvalidOperationException |
| `SendAsync_WithInterfaceParameter_HandlesCancellation` | 验证取消令牌处理 | 抛出 OperationCanceledException |
| `SendAsync_CompareWithTypedVersion` | 对比接口版本与泛型版本 | 两种方式结果一致 |

#### 关键测试点
- **多态支持**: 通过 `IRequest<int>` 接口参数处理 `PolymorphicRequest1` 和 `PolymorphicRequest2` 两种不同类型
- **管道行为**: 验证 `IPipelineBehavior<IRequest<TResponse>, TResponse>` 的执行
- **异常处理**: 验证空参数、缺少处理者、处理者异常、取消令牌等场景

### 2. MediatorInterfaceBasedTests.cs
测试通过 `IMediator` 调用接口参数方法的完整场景。

#### 测试用例清单

| 测试方法 | 测试目的 | 预期结果 |
|---------|---------|---------|
| `Mediator_SendAsync_WithInterfaceRequest_Works` | 验证 Mediator 层调用正常工作 | 返回正确结果 |
| `Mediator_SendAsync_SupportsCommandInterface` | 验证支持 ICommand 等派生接口 | 正确识别并处理命令 |
| `Mediator_SendAsync_WithInterfaceRequest_ThrowsWhenNull` | 验证空参数校验 | 抛出 ArgumentNullException |
| `Mediator_SendAsync_PolymorphicRequestsFromCollection` | 验证集合中多态请求批量处理 | 每个请求得到正确结果 |
| `Mediator_SendAsync_InterfaceAndTypedVersionsBothWork` | 对比两种调用方式 | 结果一致 |
| `Mediator_SendAsync_WithDynamic_WorksForPolymorphicRequests` | 验证使用 dynamic 保持运行时类型 | 正确处理不同类型 |
| `Mediator_SendAsync_InterfaceRequest_WithCancellation` | 验证取消令牌 | 抛出 OperationCanceledException |

#### 关键测试点
- **集合批处理**: 测试 `List<IRequest<T>>` 中不同具体类型的请求批量发送
- **dynamic 调用**: 验证使用 dynamic 关键字保持运行时类型信息
- **派生接口**: 测试 `ICommand<T>` 等派生自 `IRequest<T>` 的接口

### 3. InterfaceBasedRequestWithBehaviorsTests
测试接口参数请求与多个管道行为的协作。

#### 测试用例清单

| 测试方法 | 测试目的 | 预期结果 |
|---------|---------|---------|
| `InterfaceRequest_WithMultipleBehaviors_ExecutesInCorrectOrder` | 验证多个行为按优先级执行 | Validation(1) -> Logging(2) -> Transaction(3) |

#### 关键测试点
- **复杂管道**: 验证 3 个不同优先级行为的执行顺序
- **行为类型**: IPipelineBehavior<IRequest<string>, string>

## 实际使用场景

### 场景 1: 多态请求处理
```csharp
var requests = new List<IRequest<string>>
{
    new CreateUserRequest("Alice"),
    new UpdateUserRequest("Bob"),
    new DeleteUserRequest("Charlie")
};

foreach(var req in requests)
{
    await mediator.SendAsync(req); // 根据运行时类型分派到不同处理者
}
```

### 场景 2: 通用请求处理管道
```csharp
// 定义通用行为，适用于所有 IRequest<T>
[PipelineBehaviorPriority(1)]
public class GlobalValidationBehavior<TResponse> : IPipelineBehavior<IRequest<TResponse>, TResponse>
{
    public async Task<TResponse> HandleAsync(IRequest<TResponse> request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // 对所有请求执行通用验证
        ValidateRequest(request);
        return await next(cancellationToken);
    }
}
```

### 场景 3: 命令/查询分离 (CQRS)
```csharp
// 所有命令和查询共享基础接口
IRequest<Result> request = isCommand 
    ? new CreateOrderCommand(orderId) 
    : new GetOrderQuery(orderId);

var result = await mediator.SendAsync(request);
```

## Handler 注册要求

为使接口参数方法正常工作，Handler 需要注册为接口类型：

```csharp
// 正确：注册为 IRequestHandler<IRequest<string>, string>
services.AddTransient<IRequestHandler<IRequest<string>, string>, MyHandler>();

// 或通过 ServiceExtensions.AddMediator 自动扫描注册
```

## 管道行为注册要求

管道行为需要针对接口类型注册：

```csharp
// 注册适用于所有 IRequest<string> 的行为
services.AddMediator(typeof(IPipelineBehavior<IRequest<string>, string>));
```

## 与泛型版本的对比

| 特性 | 接口参数版本 | 泛型参数版本 |
|------|-------------|-------------|
| 方法签名 | `SendAsync<TResponse>(IRequest<TResponse>)` | `SendAsync<TRequest, TResponse>(TRequest)` |
| 类型安全 | 编译时接口约束 | 编译时具体类型约束 |
| 多态支持 | ? 天然支持（运行时分派） | ? 需要手动转换 |
| 集合处理 | ? 可直接使用 List<IRequest<T>> | ? 需要对每个类型单独处理 |
| Handler 解析 | 按接口类型解析 | 按具体类型解析 |
| 性能 | 略低（运行时类型检查） | 略高（编译时确定） |
| 适用场景 | 批量处理、插件架构、CQRS | 单一请求处理、类型明确场景 |

## 性能考虑

1. **Handler 解析**: 接口版本需要通过 `IRequestHandler<IRequest<TResponse>, TResponse>` 解析，然后在 Handler 内部通过模式匹配或类型检查分派到具体逻辑。
2. **管道行为**: 接口版本的行为类型为 `IPipelineBehavior<IRequest<TResponse>, TResponse>`，适用范围更广但类型信息较少。
3. **推荐实践**: 
   - 高频单一类型请求使用泛型版本
   - 批量多态请求使用接口版本
   - 可混合使用两种方式

## 运行测试

```bash
# 运行所有新增测试
dotnet test --filter "FullyQualifiedName~InterfaceBased"

# 运行 RequestSender 层测试
dotnet test --filter "FullyQualifiedName~InterfaceBasedSendAsyncTests"

# 运行 Mediator 层测试
dotnet test --filter "FullyQualifiedName~MediatorInterfaceBasedTests"

# 运行行为协作测试
dotnet test --filter "FullyQualifiedName~InterfaceBasedRequestWithBehaviorsTests"
```

## 总结

本次测试覆盖了新增接口参数 `SendAsync` 方法的：
- ? 基本功能（正常调用、返回结果）
- ? 异常处理（空参数、缺少 Handler、Handler 异常、取消）
- ? 管道行为（优先级、执行顺序、多行为协作）
- ? 多态支持（不同具体类型、集合批处理、dynamic 调用）
- ? 与泛型版本的对比（功能一致性）
- ? Mediator 层集成（完整调用链路）

共计 **16 个测试用例**，覆盖率达到新增方法的核心场景与边界条件。
