// @implements HeadlessVerification
namespace Xcmd.Probe.Infrastructure;

public static class RuleProbe
{
    public static void Rejects<TException>(Action action, string message) where TException : Exception
    {
        try { action(); }
        catch (TException error)
        {
            ProbeContext.Equal(message, error.Message, "The validation reason is preserved");
            return;
        }
        throw new InvalidOperationException("Expected " + typeof(TException).Name + ": " + message);
    }
}
