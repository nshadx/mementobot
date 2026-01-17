namespace mementobot.Telegram;

public static class PipelineDependencyInjectionExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder ConfigurePipeline(Action<PipelineBuilder> action)
        {
            PipelineBuilder instance = new(builder.Services);

            action(instance);
            instance.Build();

            return builder;
        }
    }
}