﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using VerifyTests;

class CustomContractResolver :
    DefaultContractResolver
{
    bool ignoreEmptyCollections;
    bool ignoreFalse;
    bool includeObsoletes;
    IReadOnlyDictionary<Type, List<string>> ignoredMembers;
    IReadOnlyList<string> ignoredByNameMembers;
    IReadOnlyList<Type> ignoredTypes;
    IReadOnlyList<Func<Exception, bool>> ignoreMembersThatThrow;
    IReadOnlyDictionary<Type, List<Func<object, bool>>> ignoredInstances;
    SharedScrubber scrubber;

    public CustomContractResolver(
        bool ignoreEmptyCollections,
        bool ignoreFalse,
        bool includeObsoletes,
        IReadOnlyDictionary<Type, List<string>> ignoredMembers,
        IReadOnlyList<string> ignoredByNameMembers,
        IReadOnlyList<Type> ignoredTypes,
        IReadOnlyList<Func<Exception, bool>> ignoreMembersThatThrow,
        IReadOnlyDictionary<Type, List<Func<object, bool>>> ignoredInstances,
        SharedScrubber scrubber)
    {
        Guard.AgainstNull(ignoredMembers, nameof(ignoredMembers));
        Guard.AgainstNull(ignoredTypes, nameof(ignoredTypes));
        Guard.AgainstNull(ignoreMembersThatThrow, nameof(ignoreMembersThatThrow));
        this.ignoreEmptyCollections = ignoreEmptyCollections;
        this.ignoreFalse = ignoreFalse;
        this.includeObsoletes = includeObsoletes;
        this.ignoredMembers = ignoredMembers;
        this.ignoredByNameMembers = ignoredByNameMembers;
        this.ignoredTypes = ignoredTypes;
        this.ignoreMembersThatThrow = ignoreMembersThatThrow;
        this.ignoredInstances = ignoredInstances;
        this.scrubber = scrubber;
        IgnoreSerializableInterface = true;
    }

    protected override JsonDictionaryContract CreateDictionaryContract(Type objectType)
    {
        var contract = base.CreateDictionaryContract(objectType);
        contract.DictionaryKeyResolver = value =>
        {
            var keyType = contract.DictionaryKeyType;
            if (keyType == typeof(Guid))
            {
                if (scrubber.TryParseConvertGuid(value, out var result))
                {
                    return result;
                }
            }

            if (keyType == typeof(DateTimeOffset))
            {
                if (scrubber.TryParseConvertDateTimeOffset(value, out var result))
                {
                    return result;
                }
            }

            if (keyType == typeof(DateTime))
            {
                if (scrubber.TryParseConvertDateTime(value, out var result))
                {
                    return result;
                }
            }

            if (keyType == typeof(Type))
            {
                var type = Type.GetType(value);
                if (type == null)
                {
                    throw new Exception($"Could not load type `{value}`.");
                }

                return TypeNameConverter.GetName(type);
            }

            return value;
        };

        return contract;
    }

    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization serialization)
    {
        var property = base.CreateProperty(member, serialization);

        var valueProvider = property.ValueProvider;
        var propertyType = property.PropertyType;
        if (propertyType == null || valueProvider == null)
        {
            return property;
        }

        if (ignoreEmptyCollections)
        {
            property.SkipEmptyCollections(member);
        }

        property.ConfigureIfBool(member, ignoreFalse);

        if (!includeObsoletes)
        {
            if (member.GetCustomAttribute<ObsoleteAttribute>(true) != null)
            {
                property.Ignored = true;
                return property;
            }
        }

        if (ignoredTypes.Any(x => x.IsAssignableFrom(propertyType)))
        {
            property.Ignored = true;
            return property;
        }

        var propertyName = property.PropertyName!;
        if (ignoredByNameMembers.Contains(propertyName))
        {
            property.Ignored = true;
            return property;
        }

        foreach (var keyValuePair in ignoredMembers)
        {
            if (keyValuePair.Value.Contains(propertyName))
            {
                if (keyValuePair.Key.IsAssignableFrom(property.DeclaringType))
                {
                    property.Ignored = true;
                    return property;
                }
            }
        }

        if (ignoredInstances.TryGetValue(propertyType, out var funcs))
        {
            property.ShouldSerialize = declaringInstance =>
            {
                var instance = valueProvider.GetValue(declaringInstance);

                if (instance == null)
                {
                    return false;
                }

                foreach (var func in funcs)
                {
                    if (func(instance))
                    {
                        return false;
                    }
                }

                return true;
            };
        }

        property.ValueProvider = new CustomValueProvider(valueProvider, propertyType, ignoreMembersThatThrow);

        return property;
    }
}