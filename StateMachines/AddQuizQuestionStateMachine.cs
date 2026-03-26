using mementobot.Services;
using mementobot.Services.Messages;
using mementobot.Telegram;
using mementobot.Telegram.StateMachine;

namespace mementobot.StateMachines;

internal class AddQuizQuestionState
{
    public QuizPickingState QuizPickingState { get; set; } = null!;

    public string Question { get; set; } = null!;
    public string Answer { get; set; } = null!;

    public int CurrentState { get; set; }
}

internal class AddQuizQuestionStateMachine : StateMachine<AddQuizQuestionState>
{
    public Event AddCommandReceivedEvent { get; private set; } = null!;
    public Event MessageReceivedEvent { get; private set; } = null!;

    public State<AddQuizQuestionState> FillingQuestion { get; private set; } = null!;
    public State<AddQuizQuestionState> FillingAnswer { get; private set; } = null!;

    public AddQuizQuestionStateMachine(QuizPickingStateMachine quizPickingStateMachine)
    {
        ConfigureEvent(() => AddCommandReceivedEvent, update => update.Message?.Text?.StartsWith("/add") ?? false);
        ConfigureEvent(() => MessageReceivedEvent, update => update.Message?.Text is not null);

        ConfigureStates(state => state.CurrentState, () => FillingQuestion, () => FillingAnswer);

        ConfigureStateMachine(quizPickingStateMachine, x => x.QuizPickingState);

        Initially(
            When(AddCommandReceivedEvent)
                .Then(context =>
                {
                    context.Instance.QuizPickingState.Published = false;
                    context.Instance.QuizPickingState.OnlyCurrentUser = true;
                    return Task.CompletedTask;
                })
                .TransitionTo(quizPickingStateMachine, quizPickingStateMachine.Initial),
            Ignore(MessageReceivedEvent)
        );

        When(quizPickingStateMachine, quizPickingStateMachine.QuizPickedEvent)
            .Then(async (BehaviorContext<AddQuizQuestionState> context, AddQuizQuestionMessage prompt) =>
            {
                await prompt.ApplyInputQuestion(context.Update.GetChatId());
            })
            .TransitionTo(FillingQuestion);

        During(FillingQuestion,
            When(MessageReceivedEvent)
                .Then(async (BehaviorContext<AddQuizQuestionState> context, AddQuizQuestionMessage prompt, IContextAccessor accessor) =>
                {
                    context.Instance.Question = context.Update.Message!.Text!;
                    accessor.Current.DeleteUserMessage = true;
                    await prompt.ApplyInputAnswer(context.Update.GetChatId());
                })
                .TransitionTo(FillingAnswer)
        );

        During(FillingAnswer,
            When(MessageReceivedEvent)
                .Then(async (BehaviorContext<AddQuizQuestionState> context, AddQuizQuestionMessage prompt, IContextAccessor accessor) =>
                {
                    context.Instance.Answer = context.Update.Message!.Text!;
                    accessor.Current.DeleteUserMessage = true;
                    await prompt.Delete(context.Update.GetChatId());
                })
                .TransitionTo(Final)
        );

        Finally(x => x.Then(async (BehaviorContext<AddQuizQuestionState> context, QuizService quizService, QuestionAddedMessage questionAddedMessage) =>
        {
            quizService.AddQuizQuestion(
                quizId: context.Instance.QuizPickingState.QuizId,
                question: context.Instance.Question,
                answer: context.Instance.Answer);

            await questionAddedMessage.Apply(context.Update.GetChatId());
        }));

        SetCompletedOnFinal();
    }
}
