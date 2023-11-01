using System;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Npgsql.Internal.Converters;
using Npgsql.Internal.Postgres;

namespace Npgsql.Internal.Resolvers;

class JsonTypeInfoResolver : IPgTypeInfoResolver
{
    static JsonSerializerOptions? DefaultSerializerOptions;

    protected TypeInfoMappingCollection Mappings { get; } = new();

    public JsonTypeInfoResolver(JsonSerializerOptions? serializerOptions = null)
        => AddTypeInfos(Mappings, serializerOptions);

    static void AddTypeInfos(TypeInfoMappingCollection mappings, JsonSerializerOptions? serializerOptions = null)
    {
        if (serializerOptions is null)
        {
            serializerOptions = DefaultSerializerOptions;
            if (serializerOptions is null)
            {
                serializerOptions = new JsonSerializerOptions();
                serializerOptions.TypeInfoResolver = new BasicJsonTypeInfoResolver();
                DefaultSerializerOptions = serializerOptions;
            }
        }

        // Jsonb is the first default for JsonDocument
        foreach (var dataTypeName in new[] { DataTypeNames.Jsonb, DataTypeNames.Json })
        {
            var jsonb = dataTypeName == DataTypeNames.Jsonb;
            mappings.AddType<JsonDocument>(dataTypeName, (options, mapping, _) =>
                    mapping.CreateInfo(options, new JsonConverter<JsonDocument, JsonDocument>(jsonb, options.TextEncoding, serializerOptions)),
                isDefault: true);
            mappings.AddStructType<JsonElement>(dataTypeName, (options, mapping, _) =>
                    mapping.CreateInfo(options, new JsonConverter<JsonElement, JsonElement>(jsonb, options.TextEncoding, serializerOptions)));
        }
    }

    protected static void AddArrayInfos(TypeInfoMappingCollection mappings)
    {
        foreach (var dataTypeName in new[] { DataTypeNames.Jsonb, DataTypeNames.Json })
        {
            mappings.AddArrayType<JsonDocument>(dataTypeName);
            mappings.AddStructArrayType<JsonElement>(dataTypeName);
        }
    }

    public PgTypeInfo? GetTypeInfo(Type? type, DataTypeName? dataTypeName, PgSerializerOptions options)
        => Mappings.Find(type, dataTypeName, options);

    sealed class BasicJsonTypeInfoResolver : IJsonTypeInfoResolver
    {
        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            if (type == typeof(JsonDocument))
                return JsonMetadataServices.CreateValueInfo<JsonDocument>(options, JsonMetadataServices.JsonDocumentConverter);
            if (type == typeof(JsonElement))
                return JsonMetadataServices.CreateValueInfo<JsonElement>(options, JsonMetadataServices.JsonElementConverter);
            return null;
        }
    }
}

sealed class JsonArrayTypeInfoResolver : JsonTypeInfoResolver, IPgTypeInfoResolver
{
    new TypeInfoMappingCollection Mappings { get; }

    public JsonArrayTypeInfoResolver(JsonSerializerOptions? serializerOptions = null) : base(serializerOptions)
    {
        Mappings = new TypeInfoMappingCollection(base.Mappings);
        AddArrayInfos(Mappings);
    }

    public new PgTypeInfo? GetTypeInfo(Type? type, DataTypeName? dataTypeName, PgSerializerOptions options)
        => Mappings.Find(type, dataTypeName, options);
}