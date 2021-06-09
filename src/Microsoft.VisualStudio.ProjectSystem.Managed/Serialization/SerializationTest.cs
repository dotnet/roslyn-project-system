﻿// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.VisualStudio.Serialization
{
    public static class SerializationTest
    {
        public static void Serialize(Stream stream, object? value)
        {
            if(value is null)
            {
                return;
            }
            var writer = new StreamWriter(stream);
            var container = ContainerizeObjectHierarchy(value.GetType(), value);
            // Utf8JsonReader/Writer are more efficient
            var options = new JsonSerializerOptions { Converters = { new SerializationContainerConverter() } };
            var jsonString = JsonSerializer.Serialize(container, options);
            File.AppendAllLines(@"C:\Workspace\JsonTest-Serialize.txt", new[] { jsonString });
            writer.Write(jsonString);
            writer.Flush();
            stream.Position = 0;
        }

        private static readonly Type ObjectType = typeof(object);
        private static readonly Type ObjectArrayType = typeof(object[]);
        private static readonly Type ISerializableType = typeof(ISerializable);

        private static PropertyInfo[] GetProperties(Type type) => type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy)
            .Where(pi => pi.CanRead && pi.CanWrite && pi.GetCustomAttribute<JsonIgnoreAttribute>() is null
                && (pi.GetCustomAttribute<BrowsableAttribute>() is null || pi.GetCustomAttribute<BrowsableAttribute>().Browsable))
            .ToArray();

        // https://github.com/Azure/autorest.powershell/blob/67d99227e4cb8b03ca3168731bcbe23ae14d35e5/powershell/resources/psruntime/BuildTime/PsExtensions.cs#L37-L45
        private static bool IsSimple(this Type type) =>
            type.IsPrimitive
            || type.IsEnum
            || type == typeof(string)
            || type == typeof(decimal);

        // https://github.com/Azure/autorest.powershell/blob/67d99227e4cb8b03ca3168731bcbe23ae14d35e5/powershell/resources/psruntime/BuildTime/PsExtensions.cs#L16-L35
        public static Type? GetElementType(Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType();
            }

            if (type.IsGenericType && typeof(IEnumerable<>).IsAssignableFrom(type))
            {
                return type.GetGenericArguments().First();
            }

            return null;
        }

        private static SerializationContainer ContainerizeObjectHierarchy(Type type, object value)
        {
            var isISerializable = ISerializableType.IsAssignableFrom(type);
            if (!isISerializable && !type.IsSimple())
            {
                foreach (var property in GetProperties(type))
                {
                    var propertyValue = property.GetValue(value);
                    if(propertyValue is null)
                    {
                        continue;
                    }

                    var propertyType = property.PropertyType;
                    if ((propertyType == ObjectArrayType || (GetElementType(propertyType) is Type elementType && !elementType.IsSimple()))
                        && propertyValue is IList propertyCollection)
                    {
                        for (var i = 0; i < propertyCollection.Count; ++i)
                        {
                            if (propertyCollection[i] is not null)
                            {
                                var container = ContainerizeObjectHierarchy(propertyCollection[i].GetType(), propertyCollection[i]);
                                // Only assign the container itself if the property is defined as an object array.
                                propertyCollection[i] = propertyType == ObjectArrayType ? container : container.Value;
                            }
                        }
                        continue;
                    }

                    if (propertyType == ObjectType || !propertyType.IsSimple())
                    {
                        var container = ContainerizeObjectHierarchy(propertyValue.GetType(), propertyValue);
                        // Only assign the container itself if the property is defined as an object.
                        property.SetValue(value, propertyType == ObjectType ? container : container.Value);
                    }
                }
            }

            return new SerializationContainer
            {
                Type = type,
                Value = value,
                IsISerializable = isISerializable
            };
        }

        //private static Type[] AllowedTypes = { typeof(int), typeof(bool), typeof(string), };

        public static object? Deserialize(Stream stream)
        {
            var streamAsString = new StreamReader(stream).ReadToEnd();
            File.AppendAllLines(@"C:\Workspace\JsonTest-Deserialize.txt", new[] { streamAsString });
            // Type filtering will use FullName from the type
            var container = ConvertContainerJsonString(streamAsString);
            return DecontainerizeObjectHierarchy(container);
        }

        private static SerializationContainer ConvertContainerJsonString(string jsonString)
        {
            var options = new JsonSerializerOptions { Converters = { new SerializationContainerConverter() } };
            if (JsonSerializer.Deserialize<SerializationContainer>(jsonString, options) is not SerializationContainer container
                || Type.GetType(container.AssemblyQualifiedName) is not Type type
                || container.Value is not JsonElement element)
            {
                throw new InvalidDataException();
            }

            if (container.IsISerializable)
            {
                using var stream = new MemoryStream(new UTF8Encoding().GetBytes(element.GetRawText()));
                var value = new DataContractJsonSerializer(container.Type).ReadObject(stream);
                return new SerializationContainer
                {
                    Type = type,
                    Value = value,
                    IsISerializable = true
                };
            }

            return new SerializationContainer
            {
                Type = type,
                Value = JsonSerializer.Deserialize(element.GetRawText(), type, options),
                IsISerializable = false
            };
        }

        private static object? DecontainerizeObjectHierarchy(SerializationContainer container)
        {
            (Type type, object? value, bool isISerializable) = (container.Type, container.Value, container.IsISerializable);
            if (!isISerializable && value is not null && !type.IsSimple())
            {
                foreach (var property in GetProperties(type))
                {
                    var propertyValue = property.GetValue(value);
                    if (propertyValue is null)
                    {
                        continue;
                    }

                    var propertyType = property.PropertyType;
                    if ((propertyType == ObjectArrayType || (GetElementType(propertyType) is Type testElementType && !testElementType.IsSimple()))
                        && propertyValue is IList propertyCollection)
                    {
                        for (var i = 0; i < propertyCollection.Count; ++i)
                        {
                            if (propertyCollection[i] is not null)
                            {
                                var elementContainer = new SerializationContainer
                                {
                                    Type = propertyCollection[i].GetType(),
                                    Value = propertyCollection[i],
                                    IsISerializable = false
                                };
                                if (propertyCollection[i] is JsonElement jsonElement)
                                {
                                    elementContainer = ConvertContainerJsonString(jsonElement.GetRawText());
                                }
                                propertyCollection[i] = DecontainerizeObjectHierarchy(elementContainer);
                            }
                        }
                        continue;
                    }

                    if (propertyType == ObjectType || !propertyType.IsSimple())
                    {
                        var propertyContainer = new SerializationContainer
                        {
                            Type = propertyType,
                            Value = propertyValue,
                            IsISerializable = false
                        };
                        if (propertyValue is JsonElement jsonElement)
                        {
                            propertyContainer = ConvertContainerJsonString(jsonElement.GetRawText());
                        }
                        property.SetValue(value, DecontainerizeObjectHierarchy(propertyContainer));
                    }
                }
            }

            return value;
        }
    }

    public class SerializationContainer
    {
        [JsonIgnore]
        public Type Type { get; set; }
        public string AssemblyQualifiedName
        {
            get => Type.AssemblyQualifiedName;
            set => Type = Type.GetType(value);
        }
        public object? Value { get; set; }
        public bool IsISerializable { get; set; }
    }

    ////https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-converters-how-to?pivots=dotnet-5-0#deserialize-inferred-types-to-object-properties
    //public class ObjectToInferredTypesConverter : JsonConverter<object>
    //{
    //    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    //    {
    //        JsonTokenType.True => true,
    //        JsonTokenType.False => false,
    //        JsonTokenType.Number when reader.TryGetInt64(out long l) => l,
    //        JsonTokenType.Number => reader.GetDouble(),
    //        JsonTokenType.String when reader.TryGetDateTime(out DateTime datetime) => datetime,
    //        JsonTokenType.String => reader.GetString(),
    //        _ => JsonDocument.ParseValue(ref reader).RootElement.Clone()
    //    };

    //    public override void Write(Utf8JsonWriter writer, object objectToWrite, JsonSerializerOptions options) =>
    //        JsonSerializer.Serialize(writer, objectToWrite, objectToWrite.GetType(), options);
    //}

    //https://github.com/dotnet/runtime/issues/1784
    public class SerializationContainerConverter : JsonConverter<SerializationContainer>
    {
        public override SerializationContainer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            JsonSerializer.Deserialize<SerializationContainer>(JsonDocument.ParseValue(ref reader).RootElement.GetRawText());

        public override void Write(Utf8JsonWriter writer, SerializationContainer container, JsonSerializerOptions options)
        {
            if (container.IsISerializable)
            {
                //https://davidsekar.com/javascript/converting-json-date-string-date-to-date-object
                //https://stackoverflow.com/a/115034/294804
                var settings = new DataContractJsonSerializerSettings { DateTimeFormat = new DateTimeFormat("o") };
                using var stream = new MemoryStream();
                new DataContractJsonSerializer(container.Type, settings).WriteObject(stream, container.Value);
                stream.Position = 0;

                // Manually serialize the container using the DataContract's serialization of Value.
                writer.WriteStartObject();
                writer.WriteString(nameof(SerializationContainer.AssemblyQualifiedName), container.AssemblyQualifiedName);
                writer.WritePropertyName(nameof(SerializationContainer.Value));
                JsonDocument.Parse(stream).WriteTo(writer);
                writer.WriteBoolean(nameof(SerializationContainer.IsISerializable), true);
                writer.WriteEndObject();

                return;
            }

            JsonDocument.Parse(JsonSerializer.Serialize(container)).WriteTo(writer);
        }
    }
}
