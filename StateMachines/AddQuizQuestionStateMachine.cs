using mementobot.Telegram;

namespace mementobot.StateMachines;

public class AddQuizQuestionState
{
    public string Question { get; set; } = null!;
    public string Answer { get; set; } = null!;
    public int CurrentState { get; set; }
}

public class AddQuizQuestionStateMachine : StateMachine<AddQuizQuestionState>
{
    public Event AddCommandRaisedEvent { get; } = null!;
    public Event MessageReceivedEvent { get; } = null!;

    public State QuestionSelecting { get; } = null!;
    public State AnswerSelecting { get; } = null!;
    
    public AddQuizQuestionStateMachine()
    {
        Event(AddCommandRaisedEvent);
        
        InstantiateStates(x => x.CurrentState, QuestionSelecting, AnswerSelecting);

        Initially(
            When(AddCommandRaisedEvent)
                .Then(BuildSelectingPages)
        );

        During(QuestionSelecting, 
            When(MessageReceivedEvent)
                .Then(SetQuestion)
        );

        During(AnswerSelecting,
            When(MessageReceivedEvent)
                .Then(SetAnswer)
        );
    }

    private async Task BuildSelectingPages(BehaviorContext context)
    {
        await context.TransitionToState(QuestionSelecting);
    }

    private async Task SetQuestion(BehaviorContext context)
    {
        await context.TransitionToState(AnswerSelecting);
    }

    private async Task SetAnswer(BehaviorContext context)
    {
        await context.SetCompleted();
    }
}