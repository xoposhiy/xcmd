using System.Text.Json.Serialization;
using Xcmd.Core;

namespace Xcmd.Infrastructure;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(FileTypeConfiguration))]
internal sealed partial class FileTypesJsonContext : JsonSerializerContext;
