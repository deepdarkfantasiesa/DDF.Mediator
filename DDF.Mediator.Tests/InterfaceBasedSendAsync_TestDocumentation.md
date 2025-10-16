# �ӿڲ��� SendAsync �������������ĵ�

## ����
���ĵ�˵��Ϊ `IRequestSender.SendAsync<TResponse>(IRequest<TResponse> request)` �� `IMediator.SendAsync<TResponse>(IRequest<TResponse> request)` �����������ɵĲ���������

## ��������ǩ��
```csharp
// IRequestSender
Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

// IMediator
Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
```

## �����ļ�

### 1. InterfaceBasedSendAsyncTests.cs
���� `IRequestSender.SendAsync<TResponse>(IRequest<TResponse>)` �ĺ��Ĺ��ܡ�

#### ���������嵥

| ���Է��� | ����Ŀ�� | Ԥ�ڽ�� |
|---------|---------|---------|
| `SendAsync_WithInterfaceParameter_ReturnsCorrectResult` | ��֤�ӿڲ������÷�����ȷ��� | ���� "InterfaceBased:Test" |
| `SendAsync_WithInterfaceParameter_ExecutesBehaviors` | ��֤�ܵ���Ϊ�����ȼ�ִ�� | �� Validation(1) -> Logging(2) ˳��ִ�� |
| `SendAsync_WithInterfaceParameter_ThrowsWhenNull` | ��֤�ղ����׳��쳣 | �׳� ArgumentNullException |
| `SendAsync_WithInterfaceParameter_SupportsPolymorphism` | ��֤��̬������ | ��ͬ�������͵õ���ͬ��� |
| `SendAsync_WithInterfaceParameter_ThrowsWhenHandlerMissing` | ��֤ȱ�ٴ�����ʱ�׳��쳣 | �׳� InvalidOperationException |
| `SendAsync_WithInterfaceParameter_HandlesExceptions` | ��֤�쳣��װ | ��װΪ InvalidOperationException |
| `SendAsync_WithInterfaceParameter_HandlesCancellation` | ��֤ȡ�����ƴ��� | �׳� OperationCanceledException |
| `SendAsync_CompareWithTypedVersion` | �ԱȽӿڰ汾�뷺�Ͱ汾 | ���ַ�ʽ���һ�� |

#### �ؼ����Ե�
- **��̬֧��**: ͨ�� `IRequest<int>` �ӿڲ������� `PolymorphicRequest1` �� `PolymorphicRequest2` ���ֲ�ͬ����
- **�ܵ���Ϊ**: ��֤ `IPipelineBehavior<IRequest<TResponse>, TResponse>` ��ִ��
- **�쳣����**: ��֤�ղ�����ȱ�ٴ����ߡ��������쳣��ȡ�����Ƶȳ���

### 2. MediatorInterfaceBasedTests.cs
����ͨ�� `IMediator` ���ýӿڲ�������������������

#### ���������嵥

| ���Է��� | ����Ŀ�� | Ԥ�ڽ�� |
|---------|---------|---------|
| `Mediator_SendAsync_WithInterfaceRequest_Works` | ��֤ Mediator ������������� | ������ȷ��� |
| `Mediator_SendAsync_SupportsCommandInterface` | ��֤֧�� ICommand �������ӿ� | ��ȷʶ�𲢴������� |
| `Mediator_SendAsync_WithInterfaceRequest_ThrowsWhenNull` | ��֤�ղ���У�� | �׳� ArgumentNullException |
| `Mediator_SendAsync_PolymorphicRequestsFromCollection` | ��֤�����ж�̬������������ | ÿ������õ���ȷ��� |
| `Mediator_SendAsync_InterfaceAndTypedVersionsBothWork` | �Ա����ֵ��÷�ʽ | ���һ�� |
| `Mediator_SendAsync_WithDynamic_WorksForPolymorphicRequests` | ��֤ʹ�� dynamic ��������ʱ���� | ��ȷ����ͬ���� |
| `Mediator_SendAsync_InterfaceRequest_WithCancellation` | ��֤ȡ������ | �׳� OperationCanceledException |

#### �ؼ����Ե�
- **����������**: ���� `List<IRequest<T>>` �в�ͬ�������͵�������������
- **dynamic ����**: ��֤ʹ�� dynamic �ؼ��ֱ�������ʱ������Ϣ
- **�����ӿ�**: ���� `ICommand<T>` �������� `IRequest<T>` �Ľӿ�

### 3. InterfaceBasedRequestWithBehaviorsTests
���Խӿڲ������������ܵ���Ϊ��Э����

#### ���������嵥

| ���Է��� | ����Ŀ�� | Ԥ�ڽ�� |
|---------|---------|---------|
| `InterfaceRequest_WithMultipleBehaviors_ExecutesInCorrectOrder` | ��֤�����Ϊ�����ȼ�ִ�� | Validation(1) -> Logging(2) -> Transaction(3) |

#### �ؼ����Ե�
- **���ӹܵ�**: ��֤ 3 ����ͬ���ȼ���Ϊ��ִ��˳��
- **��Ϊ����**: IPipelineBehavior<IRequest<string>, string>

## ʵ��ʹ�ó���

### ���� 1: ��̬������
```csharp
var requests = new List<IRequest<string>>
{
    new CreateUserRequest("Alice"),
    new UpdateUserRequest("Bob"),
    new DeleteUserRequest("Charlie")
};

foreach(var req in requests)
{
    await mediator.SendAsync(req); // ��������ʱ���ͷ��ɵ���ͬ������
}
```

### ���� 2: ͨ��������ܵ�
```csharp
// ����ͨ����Ϊ������������ IRequest<T>
[PipelineBehaviorPriority(1)]
public class GlobalValidationBehavior<TResponse> : IPipelineBehavior<IRequest<TResponse>, TResponse>
{
    public async Task<TResponse> HandleAsync(IRequest<TResponse> request, NextHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // ����������ִ��ͨ����֤
        ValidateRequest(request);
        return await next(cancellationToken);
    }
}
```

### ���� 3: ����/��ѯ���� (CQRS)
```csharp
// ��������Ͳ�ѯ��������ӿ�
IRequest<Result> request = isCommand 
    ? new CreateOrderCommand(orderId) 
    : new GetOrderQuery(orderId);

var result = await mediator.SendAsync(request);
```

## Handler ע��Ҫ��

Ϊʹ�ӿڲ�����������������Handler ��Ҫע��Ϊ�ӿ����ͣ�

```csharp
// ��ȷ��ע��Ϊ IRequestHandler<IRequest<string>, string>
services.AddTransient<IRequestHandler<IRequest<string>, string>, MyHandler>();

// ��ͨ�� ServiceExtensions.AddMediator �Զ�ɨ��ע��
```

## �ܵ���Ϊע��Ҫ��

�ܵ���Ϊ��Ҫ��Խӿ�����ע�᣺

```csharp
// ע������������ IRequest<string> ����Ϊ
services.AddMediator(typeof(IPipelineBehavior<IRequest<string>, string>));
```

## �뷺�Ͱ汾�ĶԱ�

| ���� | �ӿڲ����汾 | ���Ͳ����汾 |
|------|-------------|-------------|
| ����ǩ�� | `SendAsync<TResponse>(IRequest<TResponse>)` | `SendAsync<TRequest, TResponse>(TRequest)` |
| ���Ͱ�ȫ | ����ʱ�ӿ�Լ�� | ����ʱ��������Լ�� |
| ��̬֧�� | ? ��Ȼ֧�֣�����ʱ���ɣ� | ? ��Ҫ�ֶ�ת�� |
| ���ϴ��� | ? ��ֱ��ʹ�� List<IRequest<T>> | ? ��Ҫ��ÿ�����͵������� |
| Handler ���� | ���ӿ����ͽ��� | ���������ͽ��� |
| ���� | �Եͣ�����ʱ���ͼ�飩 | �Ըߣ�����ʱȷ���� |
| ���ó��� | ������������ܹ���CQRS | ��һ������������ȷ���� |

## ���ܿ���

1. **Handler ����**: �ӿڰ汾��Ҫͨ�� `IRequestHandler<IRequest<TResponse>, TResponse>` ������Ȼ���� Handler �ڲ�ͨ��ģʽƥ������ͼ����ɵ������߼���
2. **�ܵ���Ϊ**: �ӿڰ汾����Ϊ����Ϊ `IPipelineBehavior<IRequest<TResponse>, TResponse>`�����÷�Χ���㵫������Ϣ���١�
3. **�Ƽ�ʵ��**: 
   - ��Ƶ��һ��������ʹ�÷��Ͱ汾
   - ������̬����ʹ�ýӿڰ汾
   - �ɻ��ʹ�����ַ�ʽ

## ���в���

```bash
# ����������������
dotnet test --filter "FullyQualifiedName~InterfaceBased"

# ���� RequestSender �����
dotnet test --filter "FullyQualifiedName~InterfaceBasedSendAsyncTests"

# ���� Mediator �����
dotnet test --filter "FullyQualifiedName~MediatorInterfaceBasedTests"

# ������ΪЭ������
dotnet test --filter "FullyQualifiedName~InterfaceBasedRequestWithBehaviorsTests"
```

## �ܽ�

���β��Ը����������ӿڲ��� `SendAsync` �����ģ�
- ? �������ܣ��������á����ؽ����
- ? �쳣�����ղ�����ȱ�� Handler��Handler �쳣��ȡ����
- ? �ܵ���Ϊ�����ȼ���ִ��˳�򡢶���ΪЭ����
- ? ��̬֧�֣���ͬ�������͡�����������dynamic ���ã�
- ? �뷺�Ͱ汾�ĶԱȣ�����һ���ԣ�
- ? Mediator �㼯�ɣ�����������·��

���� **16 ����������**�������ʴﵽ���������ĺ��ĳ�����߽�������
