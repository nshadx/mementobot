using mementobot.Services;

namespace mementobot.Middlewares;

internal class EnsureStateMiddleware(
    StateService stateService
) : IMiddleware
{
    public async Task Invoke(Context context, UpdateDelegate next)
    {
        if (stateService.GetStateId(context.UserId, StateType.SelectQuizUserState) is int selectQuizUserStateId)
        {
            var actionType = stateService.GetActionType(selectQuizUserStateId);
            context.ActionType = actionType;
            context.CurrentState = StateType.SelectQuizUserState;
        }
        else if (stateService.GetStateId(context.UserId, StateType.QuizProgressUserState) is not null)
        {
            context.CurrentState = StateType.QuizProgressUserState;
        }
        else if (stateService.GetStateId(context.UserId, StateType.AddQuestionUserState) is not null)
        {
            context.CurrentState = StateType.AddQuestionUserState;
        }

        await next(context);
    }
}