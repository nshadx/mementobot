using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace mementobot.Telegram.StateMachine;

/// <summary>
/// Компилирует делегат произвольной сигнатуры в <see cref="Func{BehaviorContext, Task}"/>,
/// резолвя все параметры кроме <see cref="BehaviorContext{TInstance}"/> из <see cref="IServiceProvider"/>.
/// <para>
/// Компиляция происходит один раз при конфигурации машины (в конструкторе).
/// На каждый апдейт — прямой вызов метода на DisplayClass переданной лямбды без рефлексии.
/// </para>
/// </summary>
internal static class ResolvingInvoker<TInstance> where TInstance : class
{
    private static readonly MethodInfo GetRequiredServiceMethod =
        typeof(ServiceProviderServiceExtensions)
            .GetMethod(nameof(ServiceProviderServiceExtensions.GetRequiredService),
                BindingFlags.Public | BindingFlags.Static,
                [typeof(IServiceProvider)])!;

    public static Func<BehaviorContext<TInstance>, Task> Build(Delegate action)
    {
        var contextType = typeof(BehaviorContext<TInstance>);
        var ctxParam = Expression.Parameter(contextType, "ctx");
        var serviceProvider = Expression.Property(ctxParam, nameof(BehaviorContext<TInstance>.ServiceProvider));

        var args = action.Method.GetParameters().Select(p =>
            p.ParameterType == contextType
                ? (Expression)ctxParam
                : Expression.Call(
                    GetRequiredServiceMethod.MakeGenericMethod(p.ParameterType),
                    serviceProvider)
        );

        Expression call = action.Target is { } target
            ? Expression.Call(Expression.Constant(target), action.Method, args)
            : Expression.Call(action.Method, args);

        if (call.Type != typeof(Task))
            call = Expression.Convert(call, typeof(Task));

        return Expression.Lambda<Func<BehaviorContext<TInstance>, Task>>(call, ctxParam).Compile();
    }
}
