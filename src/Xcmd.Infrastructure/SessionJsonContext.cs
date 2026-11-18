// @implements WorkspaceStorage
using System.Text.Json.Serialization;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(WorkspaceState))]
[JsonSerializable(typeof(ViewerHandoff[]))]
internal sealed partial class SessionJsonContext : JsonSerializerContext;
