// @implements QualityChecks
namespace Xcmd.Architecture.Samples.Core
{
    internal sealed class UsesUi : Avalonia.Controls.Control;
}

namespace Xcmd.Architecture.Samples.Desktop
{
    internal sealed class View;
}

namespace Xcmd.Architecture.Samples.Infrastructure
{
    internal sealed class UsesView { public Desktop.View View { get; } = new(); }
}

namespace Xcmd.Architecture.Samples.Production
{
    internal sealed class UsesTest { public Scenarios.Case Case { get; } = new(); }
}

namespace Xcmd.Architecture.Samples.TestInfrastructure
{
    internal sealed class UsesCase { public Scenarios.Case Case { get; } = new(); }
}

namespace Xcmd.Architecture.Samples.Scenarios
{
    internal sealed class Case { public System.Diagnostics.Process Process { get; } = new(); }
}

namespace Xcmd.Architecture.Samples.First
{
    internal sealed class UsesSecond { public Second.UsesFirst? Other { get; set; } }
}

namespace Xcmd.Architecture.Samples.Second
{
    internal sealed class UsesFirst { public First.UsesSecond? Other { get; set; } }
}
