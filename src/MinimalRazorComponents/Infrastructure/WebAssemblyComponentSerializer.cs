using Microsoft.AspNetCore.Components;
using System.Buffers;
using System.Text.Json;

namespace MinimalRazorComponents.Infrastructure;

internal sealed class WebAssemblyComponentSerializer
{
    private static readonly (IList<ComponentParameter> parameterDefinitions, IList<object?> parameterValues) EmptyParameterDetails = ComponentParameter.FromParameterView(ParameterView.Empty);
    private static readonly string EmptyDefinitions = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(EmptyParameterDetails.parameterDefinitions, WebAssemblyComponentSerializationSettings.JsonSerializationOptions));
    private static readonly string EmptyValues = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(EmptyParameterDetails.parameterValues, WebAssemblyComponentSerializationSettings.JsonSerializationOptions));

    public static WebAssemblyComponentMarker SerializeInvocation(Type type, ParameterView parameters, bool prerendered)
    {
        var assembly = type.Assembly.GetName().Name;
        var typeFullName = type.FullName;
        var (definitions, values) = ComponentParameter.FromParameterView(parameters);

        if (assembly is null || typeFullName is null)
        {
            throw new InvalidOperationException();
        }

        if (!parameters.GetEnumerator().MoveNext())
        {
            // No parameters so use the cache empty values serialization

            return prerendered
                ? WebAssemblyComponentMarker.Prerendered(assembly, typeFullName, EmptyDefinitions, EmptyValues)
                : WebAssemblyComponentMarker.NonPrerendered(assembly, typeFullName, EmptyDefinitions, EmptyValues);
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

        bufferWriter.WriteHtml("<!--Blazor:");
        bufferWriter.WriteHtml(serializedStartRecord);
        bufferWriter.WriteHtml("-->");
    }

    internal static void AppendEpilogue(IBufferWriter<byte> bufferWriter, WebAssemblyComponentMarker record)
    {
        var endRecord = JsonSerializer.Serialize(
            record.GetEndRecord(),
            WebAssemblyComponentSerializationSettings.JsonSerializationOptions);

        bufferWriter.WriteHtml("<!--Blazor:");
        bufferWriter.WriteHtml(endRecord);
        bufferWriter.WriteHtml("-->");
    }
}