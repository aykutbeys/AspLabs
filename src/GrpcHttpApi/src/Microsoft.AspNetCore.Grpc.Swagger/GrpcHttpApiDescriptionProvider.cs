// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Google.Api;
using Google.Protobuf.Reflection;
using Grpc.AspNetCore.Server;
using Microsoft.AspNetCore.Grpc.HttpApi.Internal;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Microsoft.AspNetCore.Grpc.HttpApi
{
    internal class GrpcHttpApiDescriptionProvider : IApiDescriptionGroupCollectionProvider
    {
        private readonly EndpointDataSource _endpointDataSource;
        private readonly SchemaGeneratorOptions _swaggerGeneratorOptions;
        private ApiDescriptionGroupCollection? _apiDescriptionGroups;

        public GrpcHttpApiDescriptionProvider(EndpointDataSource endpointDataSource, IOptions<SchemaGeneratorOptions> swaggerGeneratorOptions)
        {
            _endpointDataSource = endpointDataSource;
            _swaggerGeneratorOptions = swaggerGeneratorOptions.Value;
        }

        public ApiDescriptionGroupCollection ApiDescriptionGroups
        {
            get
            {
                if (_apiDescriptionGroups == null)
                {
                    _apiDescriptionGroups = GetCollection();
                }
                return _apiDescriptionGroups;
            }
        }

        private ApiDescriptionGroupCollection GetCollection()
        {
            var descriptions = new List<ApiDescription>();

            var endpoints = _endpointDataSource.Endpoints;

            foreach (var endpoint in endpoints)
            {
                if (endpoint is RouteEndpoint routeEndpoint)
                {
                    var grpcMetadata = endpoint.Metadata.GetMetadata<GrpcMethodMetadata>();
                    var httpRule = endpoint.Metadata.GetMetadata<HttpRule>();
                    var methodDescriptor = endpoint.Metadata.GetMetadata<MethodDescriptor>();
                    if (grpcMetadata != null && httpRule != null && methodDescriptor != null)
                    {
                        if (HttpRuleHelpers.TryResolvePattern(httpRule, out var pattern, out var verb))
                        {
                            var apiDescription = new ApiDescription();
                            apiDescription.HttpMethod = verb;
                            apiDescription.ActionDescriptor = new Mvc.Abstractions.ActionDescriptor
                            {
                                RouteValues = new Dictionary<string, string>
                                {
                                    ["controller"] = methodDescriptor.Service.FullName
                                }
                            };
                            apiDescription.RelativePath = pattern.TrimStart('/');
                            apiDescription.SupportedRequestFormats.Add(new ApiRequestFormat { MediaType = "application/json" });
                            apiDescription.SupportedResponseTypes.Add(new ApiResponseType
                            {
                                ApiResponseFormats = { new ApiResponseFormat { MediaType = "application/json" } },
                                ModelMetadata = new GrpcModelMetadata(ModelMetadataIdentity.ForType(methodDescriptor.OutputType.ClrType)),
                                StatusCode = 200
                            });

                            var messageSchemaGenerator = new MessageSchemaGenerator(methodDescriptor.OutputType);
                            if (!_swaggerGeneratorOptions.CustomTypeMappings.ContainsKey(methodDescriptor.OutputType.ClrType))
                            {
                                _swaggerGeneratorOptions.CustomTypeMappings[methodDescriptor.OutputType.ClrType] = messageSchemaGenerator.GenerateSchema;
                            }

                            var routeParameters = HttpRuleHelpers.ResolveRouteParameterDescriptors(routeEndpoint.RoutePattern, methodDescriptor.InputType);

                            foreach (var routeParameter in routeParameters)
                            {
                                var field = routeParameter.Value.Last();

                                apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
                                {
                                    Name = routeParameter.Key,
                                    //ModelMetadata = new GrpcModelMetadata(ModelMetadataIdentity.ForType(ResolveFieldType(field))),
                                    Source = BindingSource.Path
                                });
                            }

                            descriptions.Add(apiDescription);
                        }
                    }
                }
            }

            var groups = new List<ApiDescriptionGroup>();
            groups.Add(new ApiDescriptionGroup("Test", descriptions));

            return new ApiDescriptionGroupCollection(groups, 1);
        }

        private class MessageSchemaGenerator
        {
            private readonly MessageDescriptor _type;

            public MessageSchemaGenerator(MessageDescriptor type)
            {
                _type = type;
            }

            public OpenApiSchema GenerateSchema()
            {
                var properties = new Dictionary<string, OpenApiSchema>();

                foreach (var field in _type.Fields.InFieldNumberOrder())
                {
                    properties[field.JsonName] = new OpenApiSchema { Type = "string" };
                }

                return new OpenApiSchema
                {
                    Type = "object",
                    Properties = properties
                };
            }
        }

        private static Type ResolveFieldType(FieldDescriptor field)
        {
            switch (field.FieldType)
            {
                case FieldType.Double:
                    return typeof(double);
                case FieldType.Float:
                    return typeof(float);
                case FieldType.Int64:
                    return typeof(long);
                case FieldType.UInt64:
                    return typeof(ulong);
                case FieldType.Int32:
                    return typeof(int);
                case FieldType.Fixed64:
                    return typeof(long);
                case FieldType.Fixed32:
                    return typeof(int);
                case FieldType.Bool:
                    return typeof(bool);
                case FieldType.String:
                    return typeof(string);
                case FieldType.Bytes:
                    return typeof(string);
                case FieldType.UInt32:
                    return typeof(uint);
                case FieldType.SFixed32:
                    return typeof(int);
                case FieldType.SFixed64:
                    return typeof(long);
                case FieldType.SInt32:
                    return typeof(int);
                case FieldType.SInt64:
                    return typeof(long);
                case FieldType.Enum:
                    return typeof(string);
                default:
                    throw new InvalidOperationException("Unexpected field type: " + field.FieldType);
            }
        }
    }

    internal class GrpcModelMetadata : ModelMetadata
    {
        public GrpcModelMetadata(ModelMetadataIdentity identity) : base(identity)
        {
        }

        public override IReadOnlyDictionary<object, object> AdditionalValues { get; }
        public override string BinderModelName { get; }
        public override Type BinderType { get; }
        public override BindingSource BindingSource { get; }
        public override bool ConvertEmptyStringToNull { get; }
        public override string DataTypeName { get; }
        public override string Description { get; }
        public override string DisplayFormatString { get; }
        public override string DisplayName { get; }
        public override string EditFormatString { get; }
        public override ModelMetadata ElementMetadata { get; }
        public override IEnumerable<KeyValuePair<EnumGroupAndName, string>> EnumGroupedDisplayNamesAndValues { get; }
        public override IReadOnlyDictionary<string, string> EnumNamesAndValues { get; }
        public override bool HasNonDefaultEditFormat { get; }
        public override bool HideSurroundingHtml { get; }
        public override bool HtmlEncode { get; }
        public override bool IsBindingAllowed { get; }
        public override bool IsBindingRequired { get; }
        public override bool IsEnum { get; }
        public override bool IsFlagsEnum { get; }
        public override bool IsReadOnly { get; }
        public override bool IsRequired { get; }
        public override ModelBindingMessageProvider ModelBindingMessageProvider { get; }
        public override string NullDisplayText { get; }
        public override int Order { get; }
        public override string Placeholder { get; }
        public override ModelPropertyCollection Properties { get; }
        public override IPropertyFilterProvider PropertyFilterProvider { get; }
        public override Func<object, object> PropertyGetter { get; }
        public override Action<object, object> PropertySetter { get; }
        public override bool ShowForDisplay { get; }
        public override bool ShowForEdit { get; }
        public override string SimpleDisplayProperty { get; }
        public override string TemplateHint { get; }
        public override bool ValidateChildren { get; }
        public override IReadOnlyList<object> ValidatorMetadata { get; }
    }
}
