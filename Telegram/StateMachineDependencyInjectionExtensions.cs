namespace mementobot.Telegram;

public static class StateMachineDependencyInjectionExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddStateMachine<TStateMachine, TInstance>() where TStateMachine : StateMachine<TInstance> where TInstance : class
        {
            builder.Services.AddSingleton<IStateMachine, TStateMachine>();
            
            return builder;
        }
    }
}