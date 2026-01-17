// using System.Diagnostics.CodeAnalysis;
// using Telegram.Bot.Types;
//
// namespace mementobot.Telegram;
//
// public class StateMachineEventRegistry(Dictionary<Type, Event[]> stateMachineEvents)
// {
//     public bool TryMatchStateMachineEvent(Type stateMachineType, Update update, [MaybeNullWhen(false)] out Event @event)
//     {
//         if (stateMachineEvents.TryGetValue(stateMachineType, out var events))
//         {
//             var entry = events.SingleOrDefault(x => x.Condition(update));
//             if (entry is not null)
//             {
//                 @event = entry;
//                 return true;
//             }
//         }
//
//         @event = null;
//         return false;
//     }
// }
//     
// public class StateMachineConfigurator
// {
//     private readonly Dictionary<Type, Event[]> _stateMachineEvents = [];
//
//     public StateMachineEventRegistry Registry => new(_stateMachineEvents);
//         
//     public StateMachineConfigurator AddStateMachine<TStateMachine, TInstance>() where TStateMachine : StateMachine<TInstance>, new() where TInstance : class
//     {
//         var stateMachine = new TStateMachine();
//         var events = stateMachine.Events.ToArray();
//             
//         _stateMachineEvents[typeof(TStateMachine)] = events;
//
//         return this;
//     }
// }
//
// public static class StateMachineDependencyInjectionExtensions
// {
//     extension(IHostApplicationBuilder builder)
//     {
//         public IHostApplicationBuilder ConfigureStateMachines(Action<StateMachineConfigurator> action)
//         {
//             StateMachineConfigurator configurator = new();
//             
//             action(configurator);
//             builder.Services.AddSingleton(configurator.Registry);
//             
//             return builder;
//         }
//     }
// }