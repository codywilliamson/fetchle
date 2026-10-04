using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

namespace Fetchle.Mcp;

static class ToolSchema
{
    static readonly JsonSchemaExporterOptions ExporterOptions = new()
    {
        // otherwise the root object and array items come out nullable
        TreatNullObliviousAsNonNullable = true,
        TransformSchemaNode = CopyAttributes,
    };

    public static JsonElement For<T>(JsonTypeInfo<T> typeInfo) =>
        JsonElement.Parse(JsonSchemaExporter.GetJsonSchemaAsNode(typeInfo, ExporterOptions).ToJsonString());

    static JsonNode CopyAttributes(JsonSchemaExporterContext ctx, JsonNode schema)
    {
        if (ctx.PropertyInfo?.AttributeProvider is not { } provider || schema is not JsonObject node)
        {
            return schema;
        }
        foreach (var attribute in provider.GetCustomAttributes(inherit: false))
        {
            switch (attribute)
            {
                case DescriptionAttribute description:
                    node["description"] = description.Description;
                    break;
                case DefaultValueAttribute { Value: int value }:
                    node["default"] = value;
                    break;
            }
        }
        return node;
    }
}
