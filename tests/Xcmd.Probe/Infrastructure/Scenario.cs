// @implements HeadlessVerification
namespace Xcmd.Probe.Infrastructure;

public sealed record Scenario(string Id, string Requirement, Func<string, Task<Dictionary<string, object>>> Execute)
{
    public Scenario(string id, string requirement, Action check) : this(id, requirement, _ =>
    {
        check();
        return Task.FromResult(new Dictionary<string, object>());
    })
    { }

    public Scenario(string id, string requirement, Func<ProbeContext, Task> run) : this(id, requirement, async folder =>
    {
        using var context = new ProbeContext(folder);
        try
        {
            await context.StartAsync();
            await run(context);
            context.SaveSnapshot();
            return context.Measurements;
        }
        catch
        {
            context.SaveSnapshot();
            context.CaptureFailure();
            throw;
        }
    })
    { }
}
