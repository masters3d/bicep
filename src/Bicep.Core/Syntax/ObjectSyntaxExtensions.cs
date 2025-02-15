// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Bicep.Core.Extensions;

namespace Bicep.Core.Syntax
{
    public static class ObjectSyntaxExtensions
    {
        /// <summary>
        /// Converts a syntactically valid object syntax node to a dictionary mapping property name strings to property syntax nodes. Returns the first property in the case of duplicate names.
        /// </summary>
        /// <param name="syntax">The object syntax node</param>
        public static ImmutableDictionary<string, ObjectPropertySyntax> ToNamedPropertyDictionary(this ObjectSyntax syntax)
        {
            var dictionary = new Dictionary<string, ObjectPropertySyntax>(LanguageConstants.IdentifierComparer);
            foreach (var property in syntax.Properties)
            {
                if (property.TryGetKeyText() is {} key && !dictionary.ContainsKey(key))
                {
                    dictionary[key] = property;
                }
            }

            return dictionary.ToImmutableDictionary(LanguageConstants.IdentifierComparer);
        }

        /// <summary>
        /// Converts a syntactically valid object syntax node to a dictionary mapping property name strings to property syntax node values. Returns the first property value in the case of duplicate names.
        /// </summary>
        /// <param name="syntax">The object syntax node</param>
        public static ImmutableDictionary<string, SyntaxBase> ToNamedPropertyValueDictionary(this ObjectSyntax syntax)
            => ToNamedPropertyDictionary(syntax).ToImmutableDictionary(x => x.Key, x => x.Value.Value, LanguageConstants.IdentifierComparer);

        /// <summary>
        /// Returns the specified property by name on any valid or invalid object syntax node if there is exactly one property by that name.
        /// Returns null if the property does not exist or if multiple properties by that name exist. This method is intended for a single
        /// one-off property lookup and avoids allocation of a dictionary. If you need to make multiple look ups, use another extension in this class.
        /// </summary>
        /// <param name="syntax">The object syntax node</param>
        /// <param name="propertyName">The property name</param>
        public static ObjectPropertySyntax? SafeGetPropertyByName(this ObjectSyntax syntax, string propertyName)
        {
            ObjectPropertySyntax? result = null;

            var matchingValidProperties = syntax.Properties
                .Where(p => p.TryGetKeyText() is { } validName && string.Equals(validName, propertyName, LanguageConstants.IdentifierComparison));

            foreach (var property in matchingValidProperties)
            {
                if (result == null)
                {
                    // we have not yet seen a name match
                    // store it
                    result = property;
                }
                else
                {
                    // we have already seen a name match, which means we have a duplicate property
                    // no point proceeding any further
                    return null;
                }
            }

            return result;
        }

        public static ObjectSyntax MergeProperty(this ObjectSyntax? syntax, string propertyName, string propertyValue) =>
            syntax.MergeProperty(propertyName, SyntaxFactory.CreateStringLiteral(propertyValue));

        public static ObjectSyntax MergeProperty(this ObjectSyntax? syntax, string propertyName, SyntaxBase propertyValue)
        {
            if (syntax == null)
            {
                return SyntaxFactory.CreateObject(SyntaxFactory.CreateObjectProperty(propertyName, propertyValue).AsEnumerable());
            }

            var properties = syntax.Properties.ToList();
            int matchingIndex = 0;

            while (matchingIndex < properties.Count)
            {
                if (string.Equals(properties[matchingIndex].TryGetKeyText(), propertyName, LanguageConstants.IdentifierComparison))
                {
                    break;
                }

                matchingIndex++;
            }

            if (matchingIndex < properties.Count)
            {
                // If both property values are objects, merge them. Otherwise, replace the matching property value.
                SyntaxBase mergedValue = properties[matchingIndex].Value is ObjectSyntax sourceObject && propertyValue is ObjectSyntax targetObject
                    ? sourceObject.DeepMerge(targetObject)
                    : propertyValue;

                properties[matchingIndex] = SyntaxFactory.CreateObjectProperty(propertyName, mergedValue);
            }
            else
            {
                properties.Add(SyntaxFactory.CreateObjectProperty(propertyName, propertyValue));
            }

            return SyntaxFactory.CreateObject(properties);
        }

        public static ObjectSyntax DeepMerge(this ObjectSyntax? sourceObject, ObjectSyntax targetObject)
        {
            if (sourceObject == null)
            {
                return targetObject;
            }

            return targetObject.Properties.Aggregate(sourceObject, (mergedObject, property) =>
                property.TryGetKeyText() is string propertyName
                    ? mergedObject.MergeProperty(propertyName, property.Value)
                    : mergedObject);
        }
    }
}