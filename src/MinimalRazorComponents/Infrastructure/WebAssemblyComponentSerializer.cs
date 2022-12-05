using Microsoft.AspNetCore.Components;
using System.Buffers;
using System.Text.Json;

namespace MinimalRazorComponents.Infrastructure;

internal sealed class WebAssemblyComponentSerializer
{
    public static WebAssemblyComponentMarker SerializeInvocation(Type type, ParameterView parameters, bool prerendered)
    {
        var assembly = type.Assembly.GetName().Name;
        var typeFullName = type.FullName;
        var (definitions, values) = ComponentParameter.FromParameterView(parameters);

        if (assembly is null || typeFullName is null)
        {
            throw new InvalidOperationException();
        }

        // We need to serialize and Base64 encode parameters separately since they can contain arbitrary data that might
        // cause the HTML comment to be invalid (like if you serialize a string that contains two consecutive dashes "--").
        var serializedDefinitions = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(definitions, WebAssemblyComponentSerializationSettings.JsonSerializationOptions));
        var serializedValues = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(values, WebAssemblyComponentSerializationSettings.JsonSerializationOptions));

        return prerendered ? WebAssemblyComponentMarker.Prerendered(assembly, typeFullName, serializedDefinitions, serializedValues) :
            WebAssemblyComponentMarker.NonPrerendered(assembly, typeFullName, serializedDefinitions, serializedValues);
    }

    internal static void AppendPreamble(IBufferWriter<byte> bufferWriter, WebAssemblyComponentMarker record)
    {
        var serializedStartRecord = JsonSerializer.Serialize(
            record,
            WebAssemblyComponentSerializationSettings.JsonSerializationOptions);

        bufferWriter.AppendHtml("<!--Blazor:");
        bufferWriter.AppendHtml(serializedStartRecord);
        bufferWriter.AppendHtml("-->");
    }

    internal static void AppendEpilogue(IBufferWriter<byte> bufferWriter, WebAssemblyComponentMarker record)
    {
        var endRecord = JsonSerializer.Serialize(
            record.GetEndRecord(),
            WebAssemblyComponentSerializationSettings.JsonSerializationOptions);

        bufferWriter.AppendHtml("<!--Blazor:");
        bufferWriter.AppendHtml(endRecord);
        bufferWriter.AppendHtml("-->");
    }
}