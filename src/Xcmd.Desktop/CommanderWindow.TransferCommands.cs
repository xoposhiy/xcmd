// @implements TransferEntries, DuplicateEntry, DiscoverShortcuts
using Avalonia.Input;
using Xcmd.Core;

namespace Xcmd.Desktop;

public sealed partial class CommanderWindow
{
    internal static IReadOnlyList<TransferCommand> TransferCommands { get; } =
    [
        new("Copy to other panel", "Copy", new(Key.F5), FileOperationKind.Copy),
        new("Copy in this panel", "Copy here", new(Key.F5, KeyModifiers.Shift), FileOperationKind.Copy, SamePanel: true),
        new("Move to other panel", "Move", new(Key.F6), FileOperationKind.Move),
    ];

    internal sealed record TransferCommand(string MenuLabel, string HintLabel, KeyGesture Gesture, FileOperationKind Kind, bool SamePanel = false)
    {
        public string Hint => $"{Gesture}  {HintLabel}";
        public void Execute(CommanderWindow window) => window.TransferSelected(Kind, SamePanel);
    }

    private bool HandleTransferKey(KeyEventArgs e)
    {
        foreach (var command in TransferCommands)
        {
            if (command.Gesture.Key != e.Key || command.Gesture.KeyModifiers != e.KeyModifiers) continue;
            if (launchKeysHeld.Add(e.Key)) command.Execute(this);
            return true;
        }
        return false;
    }
}
