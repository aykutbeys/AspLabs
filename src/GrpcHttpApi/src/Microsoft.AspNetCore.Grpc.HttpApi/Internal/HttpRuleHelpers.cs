// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Google.Api;
using Google.Protobuf.Reflection;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Microsoft.AspNetCore.Grpc.HttpApi.Internal
{
    internal static class HttpRuleHelpers
    {
        public static bool TryResolvePattern(HttpRule http, [NotNullWhen(true)]out string? pattern, [NotNullWhen(true)]out string? verb)
        {
            switch (http.PatternCase)
            {
                case HttpRule.PatternOneofCase.Get:
                    pattern = http.Get;
                    verb = "GET";
                    return true;
                case HttpRule.PatternOneofCase.Put:
                    pattern = http.Put;
                    verb = "PUT";
                    return true;
                case HttpRule.PatternOneofCase.Post:
                    pattern = http.Post;
                    verb = "POST";
                    return true;
                case HttpRule.PatternOneofCase.Delete:
                    pattern = http.Delete;
                    verb = "DELETE";
                    return true;
                case HttpRule.PatternOneofCase.Patch:
                    pattern = http.Patch;
                    verb = "PATCH";
                    return true;
                case HttpRule.PatternOneofCase.Custom:
                    pattern = http.Custom.Path;
                    verb = http.Custom.Kind;
                    return true;
                default:
                    pattern = null;
                    verb = null;
                    return false;
            }
        }

        public static Dictionary<string, List<FieldDescriptor>> ResolveRouteParameterDescriptors(RoutePattern pattern, MessageDescriptor messageDescriptor)
        {
            var routeParameterDescriptors = new Dictionary<string, List<FieldDescriptor>>(StringComparer.Ordinal);
            foreach (var routeParameter in pattern.Parameters)
            {
                if (!ServiceDescriptorHelpers.TryResolveDescriptors(messageDescriptor, routeParameter.Name, out var fieldDescriptors))
                {
                    throw new InvalidOperationException($"Couldn't find matching field for route parameter '{routeParameter.Name}' on {messageDescriptor.Name}.");
                }

                routeParameterDescriptors.Add(routeParameter.Name, fieldDescriptors);
            }

            return routeParameterDescriptors;
        }
    }
}
