using Newtonsoft.Json;

namespace DotnetProcessBridge.Constants;

internal static class SerializationConstants
{
    internal static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.None
    };
}
