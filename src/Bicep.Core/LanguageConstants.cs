// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Bicep.Core.Parsing;
using Bicep.Core.Resources;
using Bicep.Core.TypeSystem;

namespace Bicep.Core
{
    public static class LanguageConstants
    {
        public const string LanguageId = "bicep";
        public const string LanguageFileExtension = ".bicep";

        public const int MaxParameterCount = 256;
        public const int MaxIdentifierLength = 255;

        public const string ErrorName = "<error>";
        public const string MissingName = "<missing>";

        public const string TargetScopeKeyword = "targetScope";
        public const string ParameterKeyword = "param";
        public const string OutputKeyword = "output";
        public const string VariableKeyword = "var";
        public const string ResourceKeyword = "resource";
        public const string ModuleKeyword = "module";
        public const string ExistingKeyword = "existing";

        public const string IfKeyword = "if";
        public const string ForKeyword = "for";
        public const string InKeyword = "in";

        public const string TargetScopeTypeTenant = "tenant";
        public const string TargetScopeTypeManagementGroup = "managementGroup";
        public const string TargetScopeTypeSubscription = "subscription";
        public const string TargetScopeTypeResourceGroup = "resourceGroup";

        public static ImmutableSortedSet<string> DeclarationKeywords = new[] { ParameterKeyword, VariableKeyword, ResourceKeyword, OutputKeyword, ModuleKeyword }.ToImmutableSortedSet(StringComparer.Ordinal);

        public static ImmutableSortedSet<string> ContextualKeywords = DeclarationKeywords
            .Add(TargetScopeKeyword)
            .Add(IfKeyword)
            .Add(ForKeyword)
            .Add(InKeyword);

        public const string TrueKeyword = "true";
        public const string FalseKeyword = "false";
        public const string NullKeyword = "null";

        public static readonly ImmutableDictionary<string, TokenType> Keywords = new Dictionary<string, TokenType>(StringComparer.Ordinal)
        {
            [TrueKeyword] = TokenType.TrueKeyword,
            [FalseKeyword] = TokenType.FalseKeyword,
            [NullKeyword] = TokenType.NullKeyword
        }.ToImmutableDictionary();

        public const string ParameterAllowedPropertyName = "allowed";
        public const string ParameterDefaultPropertyName = "default";

        public const string ModuleParamsPropertyName = "params";
        public const string ModuleOutputsPropertyName = "outputs";

        public const string ResourceIdPropertyName = "id";
        public const string ResourceLocationPropertyName = "location";
        public const string ResourceNamePropertyName = "name";
        public const string ResourceTypePropertyName = "type";
        public const string ResourceApiVersionPropertyName = "apiVersion";
        public const string ResourceScopePropertyName = "scope";
        public const string ResourceParentPropertyName = "parent";
        public const string ResourceDependsOnPropertyName = "dependsOn";
        public const string TypeNameString = "string";

        public static readonly StringComparer IdentifierComparer = StringComparer.Ordinal;
        public static readonly StringComparison IdentifierComparison = StringComparison.Ordinal;

        public const string StringDelimiter = "'";
        public const string StringHoleOpen = "${";
        public const string StringHoleClose = "}";

        public static readonly TypeSymbol Any = new AnyType();
        public static readonly TypeSymbol ResourceRef = CreateResourceScopeReference(ResourceScope.Module | ResourceScope.Resource);

        // type used for the item type in the dependsOn array type
        public static readonly TypeSymbol ResourceOrResourceCollectionRefItem = UnionType.Create(
            ResourceRef,
            new TypedArrayType(CreateResourceScopeReference(ResourceScope.Module), TypeSymbolValidationFlags.Default),
            new TypedArrayType(CreateResourceScopeReference(ResourceScope.Resource), TypeSymbolValidationFlags.Default));

        // the type of the dependsOn property in module and resource bodies
        public static readonly TypeSymbol ResourceOrResourceCollectionRefArray = new TypedArrayType(ResourceOrResourceCollectionRefItem, TypeSymbolValidationFlags.Default);
        
        public static readonly TypeSymbol String = new PrimitiveType(TypeNameString, TypeSymbolValidationFlags.Default);
        // LooseString should be regarded as equal to the 'string' type, but with different validation behavior
        public static readonly TypeSymbol LooseString = new PrimitiveType(TypeNameString, TypeSymbolValidationFlags.AllowLooseStringAssignment);
        public static readonly TypeSymbol Object = new ObjectType("object", TypeSymbolValidationFlags.Default, Enumerable.Empty<TypeProperty>(), LanguageConstants.Any);
        public static readonly TypeSymbol Int = new PrimitiveType("int", TypeSymbolValidationFlags.Default);
        public static readonly TypeSymbol Bool = new PrimitiveType("bool", TypeSymbolValidationFlags.Default);
        public static readonly TypeSymbol Null = new PrimitiveType(NullKeyword, TypeSymbolValidationFlags.Default);
        public static readonly TypeSymbol Array = new ArrayType("array");

        // declares the description property but also allows any other property of any type
        public static readonly TypeSymbol ParameterModifierMetadata = new ObjectType(nameof(ParameterModifierMetadata), TypeSymbolValidationFlags.Default, CreateParameterModifierMetadataProperties(), Any, TypePropertyFlags.Constant);

        public static readonly TypeSymbol Tags = new ObjectType(nameof(Tags), TypeSymbolValidationFlags.Default, Enumerable.Empty<TypeProperty>(), String, TypePropertyFlags.None);

        // types allowed to use in output and parameter declarations
        public static readonly ImmutableSortedDictionary<string, TypeSymbol> DeclarationTypes = new[] { String, Object, Int, Bool, Array }.ToImmutableSortedDictionary(type => type.Name, type => type, StringComparer.Ordinal);

        public static TypeSymbol? TryGetDeclarationType(string? typeName)
        {
            if (typeName != null && DeclarationTypes.TryGetValue(typeName, out var primitiveType))
            {
                return primitiveType;
            }

            return null;
        }

        public static TypeSymbol CreateParameterModifierType(TypeSymbol primitiveType, TypeSymbol allowedValuesType)
        {
            return new ObjectType($"ParameterModifier<{allowedValuesType.Name}>", TypeSymbolValidationFlags.Default, CreateParameterModifierProperties(primitiveType, allowedValuesType), additionalPropertiesType: null);
        }

        private static IEnumerable<TypeProperty> CreateParameterModifierProperties(TypeSymbol primitiveType, TypeSymbol allowedValuesType)
        {
            /*
             * The primitiveType may be set to "any" when there's a parse error in the declared type syntax node.
             * In that case, we cannot determine which modifier properties are allowed, so we allow them all.
             */

            if (ReferenceEquals(primitiveType, String) || ReferenceEquals(primitiveType, Object) || ReferenceEquals(primitiveType, Any))
            {
                // only string and object types have secure equivalents
                yield return new TypeProperty("secure", Bool, TypePropertyFlags.Constant);
            }

            // default value is allowed to have expressions
            yield return new TypeProperty(ParameterDefaultPropertyName, allowedValuesType);

            //if (premitiveType is ArrayType && allowedValuesType)
            allowedValuesType = allowedValuesType is TypedArrayType ? allowedValuesType : new TypedArrayType(allowedValuesType, TypeSymbolValidationFlags.Default);
            yield return new TypeProperty(ParameterAllowedPropertyName, allowedValuesType, TypePropertyFlags.Constant);

            if (ReferenceEquals(primitiveType, Int) || ReferenceEquals(primitiveType, Any))
            {
                // value constraints are valid on integer parameters only
                yield return new TypeProperty("minValue", Int, TypePropertyFlags.Constant);
                yield return new TypeProperty("maxValue", Int, TypePropertyFlags.Constant);
            }

            if (ReferenceEquals(primitiveType, String) || ReferenceEquals(primitiveType, Array) || ReferenceEquals(primitiveType, Any))
            {
                // strings and arrays can have length constraints
                yield return new TypeProperty("minLength", Int, TypePropertyFlags.Constant);
                yield return new TypeProperty("maxLength", Int, TypePropertyFlags.Constant);
            }

            yield return new TypeProperty("metadata", ParameterModifierMetadata, TypePropertyFlags.Constant);
        }

        private static IEnumerable<TypeProperty> CreateParameterModifierMetadataProperties()
        {
            yield return new TypeProperty("description", String, TypePropertyFlags.Constant);
        }

        public static IEnumerable<TypeProperty> GetCommonResourceProperties(ResourceTypeReference reference)
        {
            yield return new TypeProperty(ResourceIdPropertyName, String, TypePropertyFlags.ReadOnly | TypePropertyFlags.DeployTimeConstant);
            yield return new TypeProperty(ResourceNamePropertyName, String, TypePropertyFlags.Required | TypePropertyFlags.DeployTimeConstant);
            yield return new TypeProperty(ResourceTypePropertyName, new StringLiteralType(reference.FullyQualifiedType), TypePropertyFlags.ReadOnly | TypePropertyFlags.DeployTimeConstant);
            yield return new TypeProperty(ResourceApiVersionPropertyName, new StringLiteralType(reference.ApiVersion), TypePropertyFlags.ReadOnly | TypePropertyFlags.DeployTimeConstant);
        }

        public static IEnumerable<string> GetResourceScopeDescriptions(ResourceScope resourceScope)
        {
            if (resourceScope == ResourceScope.None)
            {
                yield return "none";
            }

            if (resourceScope.HasFlag(ResourceScope.Resource))
            {
                yield return "resource";
            }
            if (resourceScope.HasFlag(ResourceScope.Module))
            {
                yield return "module";
            }
            if (resourceScope.HasFlag(ResourceScope.Tenant))
            {
                yield return "tenant";
            }
            if (resourceScope.HasFlag(ResourceScope.ManagementGroup))
            {
                yield return "managementGroup";
            }
            if (resourceScope.HasFlag(ResourceScope.Subscription))
            {
                yield return "subscription";
            }
            if (resourceScope.HasFlag(ResourceScope.ResourceGroup))
            {
                yield return "resourceGroup";
            }
        }        

        public static ResourceScopeType CreateResourceScopeReference(ResourceScope resourceScope)
        {
            var scopeDescriptions = string.Join(" | ", GetResourceScopeDescriptions(resourceScope));

            return new ResourceScopeType(scopeDescriptions, resourceScope);
        }

        public static TypeSymbol CreateModuleType(IEnumerable<TypeProperty> paramsProperties, IEnumerable<TypeProperty> outputProperties, ResourceScope moduleScope, ResourceScope containingScope, string typeName)
        {
            var paramsType = new ObjectType(ModuleParamsPropertyName, TypeSymbolValidationFlags.Default, paramsProperties, null);
            // If none of the params are reqired, we can allow the 'params' declaration to be omitted entirely
            var paramsRequiredFlag = paramsProperties.Any(x => x.Flags.HasFlag(TypePropertyFlags.Required)) ? TypePropertyFlags.Required : TypePropertyFlags.None;

            var outputsType = new ObjectType(ModuleOutputsPropertyName, TypeSymbolValidationFlags.Default, outputProperties, null);

            var scopePropertyFlags = TypePropertyFlags.WriteOnly | TypePropertyFlags.DeployTimeConstant | TypePropertyFlags.DisallowAny;
            if (moduleScope != containingScope)
            {
                // If the module scope matches the parent scope, we can safely omit the scope property
                scopePropertyFlags |= TypePropertyFlags.Required;
            }

            var moduleBody = new ObjectType(
                typeName,
                TypeSymbolValidationFlags.Default,
                new[]
                {
                    new TypeProperty(ResourceNamePropertyName, LanguageConstants.String, TypePropertyFlags.Required | TypePropertyFlags.DeployTimeConstant),
                    new TypeProperty(ResourceScopePropertyName, CreateResourceScopeReference(moduleScope), scopePropertyFlags),
                    new TypeProperty(ModuleParamsPropertyName, paramsType, paramsRequiredFlag | TypePropertyFlags.WriteOnly),
                    new TypeProperty(ModuleOutputsPropertyName, outputsType, TypePropertyFlags.ReadOnly),
                    new TypeProperty(ResourceDependsOnPropertyName, ResourceOrResourceCollectionRefArray, TypePropertyFlags.WriteOnly | TypePropertyFlags.DisallowAny),
                },
                null);

            return new ModuleType(typeName, moduleScope, moduleBody);
        }

        public static IEnumerable<TypeProperty> CreateResourceProperties(ResourceTypeReference resourceTypeReference)
        {
            /*
             * The following properties are intentionally excluded from this model:
             * - SystemData - this is a read-only property that doesn't belong on PUTs
             * - id - that is not allowed in templates
             * - type - included in resource type on resource declarations
             * - apiVersion - included in resource type on resource declarations
             */

            foreach (var prop in GetCommonResourceProperties(resourceTypeReference))
            {
                yield return prop;
            }

            // TODO: Model type fully
            yield return new TypeProperty("sku", Object);

            yield return new TypeProperty("kind", String);
            yield return new TypeProperty("managedBy", String);

            var stringArray = new TypedArrayType(String, TypeSymbolValidationFlags.Default);
            yield return new TypeProperty("managedByExtended", stringArray);

            yield return new TypeProperty("location", String);

            // TODO: Model type fully
            yield return new TypeProperty("extendedLocation", Object);

            yield return new TypeProperty("zones", stringArray);

            yield return new TypeProperty("plan", Object);

            yield return new TypeProperty("eTag", String);

            yield return new TypeProperty("tags", Tags);

            // TODO: Model type fully
            yield return new TypeProperty("scale", Object);

            // TODO: Model type fully
            yield return new TypeProperty("identity", Object);

            yield return new TypeProperty("properties", Object);

            var resourceRefArray = new TypedArrayType(ResourceRef, TypeSymbolValidationFlags.Default);
            yield return new TypeProperty(ResourceDependsOnPropertyName, resourceRefArray, TypePropertyFlags.WriteOnly | TypePropertyFlags.DisallowAny);
        }
    }
}
