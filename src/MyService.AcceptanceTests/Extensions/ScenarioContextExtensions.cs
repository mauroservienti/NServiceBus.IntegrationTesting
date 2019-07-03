using MyService.AcceptanceTests.Templates;

namespace MyService.AcceptanceTests.Extensions
{
    static class ScenarioContextExtensions
    {
        public static bool WhenHandlerIsInvoked<THandler>(this SpecialContext context)
        {
            return context.InvokedHandlers.Contains(typeof(THandler));
        }

        public static bool HasFailedMessages(this SpecialContext context)
        {
            return !context.FailedMessages.IsEmpty;
        }
    }
}
