// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Microsoft.AspNetCore.Routing.Internal
{
    public class MapActionExpressionTreeBuilderTest
    {
        [Fact]
        public async Task RequestDelegateInvokesAction()
        {
            var invoked = false;
            void TestAction()
            {
                invoked = true;
            }

            var requestDelegate = MapActionExpressionTreeBuilder.BuildRequestDelegate((Action)TestAction);

            await requestDelegate(null!);

            Assert.True(invoked);
        }

        [Fact]
        public async Task RequestDelegatePopulatesFromRouteParameterBasedOnParameterName()
        {
            const string paramName = "value";
            const int originalRouteParam = 42;

            int? deserializedRouteParam = null;

            void TestAction([FromRoute] int value)
            {
                deserializedRouteParam = value;
            }

            var httpContext = new DefaultHttpContext();
            httpContext.Request.RouteValues[paramName] = originalRouteParam.ToString(NumberFormatInfo.InvariantInfo);

            var requestDelegate = MapActionExpressionTreeBuilder.BuildRequestDelegate((Action<int>)TestAction);

            await requestDelegate(httpContext);

            Assert.Equal(originalRouteParam, deserializedRouteParam);
        }

        [Fact]
        public async Task RequestDelegatePopulatesFromRouteParameterBasedOnAttributeNameProperty()
        {
            const string specifiedName = "value";
            const int originalRouteParam = 42;

            int? deserializedRouteParam = null;

            void TestAction([FromRoute(Name = specifiedName)] int foo)
            {
                deserializedRouteParam = foo;
            }

            var httpContext = new DefaultHttpContext();
            httpContext.Request.RouteValues[specifiedName] = originalRouteParam.ToString(NumberFormatInfo.InvariantInfo);

            var requestDelegate = MapActionExpressionTreeBuilder.BuildRequestDelegate((Action<int>)TestAction);

            await requestDelegate(httpContext);

            Assert.Equal(originalRouteParam, deserializedRouteParam);
        }

        [Fact]
        public async Task UsesDefaultValueIfNoMatchingRouteValue()
        {
            const string unmatchedName = "value";
            const int unmatchedRouteParam = 42;

            int? deserializedRouteParam = null;

            void TestAction([FromRoute] int foo)
            {
                deserializedRouteParam = foo;
            }

            var httpContext = new DefaultHttpContext();
            httpContext.Request.RouteValues[unmatchedName] = unmatchedRouteParam.ToString(NumberFormatInfo.InvariantInfo);

            var requestDelegate = MapActionExpressionTreeBuilder.BuildRequestDelegate((Action<int>)TestAction);

            await requestDelegate(httpContext);

            Assert.Equal(0, deserializedRouteParam);
        }

        [Fact]
        public async Task RequestDelegatePopulatesFromQueryParameterBasedOnParameterName()
        {
            const string paramName = "value";
            const int originalQueryParam = 42;

            int? deserializedRouteParam = null;

            void TestAction([FromQuery] int value)
            {
                deserializedRouteParam = value;
            }

            var query = new QueryCollection(new Dictionary<string, StringValues>()
            {
                [paramName] = originalQueryParam.ToString(NumberFormatInfo.InvariantInfo)
            });

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Query = query;

            var requestDelegate = MapActionExpressionTreeBuilder.BuildRequestDelegate((Action<int>)TestAction);

            await requestDelegate(httpContext);

            Assert.Equal(originalQueryParam, deserializedRouteParam);
        }

        [Fact]
        public async Task RequestDelegatePopulatesFromHeaderParameterBasedOnParameterName()
        {
            const string customHeaderName = "X-Custom-Header";
            const int originalHeaderParam = 42;

            int? deserializedRouteParam = null;

            void TestAction([FromHeader(Name = customHeaderName)] int value)
            {
                deserializedRouteParam = value;
            }

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers[customHeaderName] = originalHeaderParam.ToString(NumberFormatInfo.InvariantInfo);

            var requestDelegate = MapActionExpressionTreeBuilder.BuildRequestDelegate((Action<int>)TestAction);

            await requestDelegate(httpContext);

            Assert.Equal(originalHeaderParam, deserializedRouteParam);
        }

        [Fact]
        public async Task RequestDelegatePopulatesFromBodyParameter()
        {
            Todo originalTodo = new()
            {
                Name = "Write more tests!"
            };

            Todo? deserializedRequestBody = null;

            void TestAction([FromBody] Todo todo)
            {
                deserializedRequestBody = todo;
            }

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Content-Type"] = "application/json";

            var requestBodyBytes = JsonSerializer.SerializeToUtf8Bytes(originalTodo);
            httpContext.Request.Body = new MemoryStream(requestBodyBytes);

            var requestDelegate = MapActionExpressionTreeBuilder.BuildRequestDelegate((Action<Todo>)TestAction);

            await requestDelegate(httpContext);

            Assert.NotNull(deserializedRequestBody);
            Assert.Equal(originalTodo.Name, deserializedRequestBody!.Name);
        }


        [Fact]
        public async Task RequestDelegatePopulatesFromFormParameterBasedOnParameterName()
        {
            const string paramName = "value";
            const int originalQueryParam = 42;

            int? deserializedRouteParam = null;

            void TestAction([FromForm] int value)
            {
                deserializedRouteParam = value;
            }

            var form = new FormCollection(new Dictionary<string, StringValues>()
            {
                [paramName] = originalQueryParam.ToString(NumberFormatInfo.InvariantInfo)
            });

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Form = form;

            var requestDelegate = MapActionExpressionTreeBuilder.BuildRequestDelegate((Action<int>)TestAction);

            await requestDelegate(httpContext);

            Assert.Equal(originalQueryParam, deserializedRouteParam);
        }

        [Fact]
        public void BuildRequestDelegateThrowsInvalidOperationExceptionGivenBothFromBodyAndFromFormOnDifferentParameters()
        {
            void TestAction([FromBody] int value1, [FromForm] int value2) { }
            void TestActionWithFlippedParams([FromForm] int value1, [FromBody] int value2) { }

            Assert.Throws<InvalidOperationException>(() => MapActionExpressionTreeBuilder.BuildRequestDelegate((Action<int, int>)TestAction));
            Assert.Throws<InvalidOperationException>(() => MapActionExpressionTreeBuilder.BuildRequestDelegate((Action<int, int>)TestActionWithFlippedParams));
        }

        [Fact]
        public void BuildRequestDelegateThrowsInvalidOperationExceptionGivenFromBodyOnMultipleParameters()
        {
            void TestAction([FromBody] int value1, [FromBody] int value2) { }

            Assert.Throws<InvalidOperationException>(() => MapActionExpressionTreeBuilder.BuildRequestDelegate((Action<int, int>)TestAction));
        }

        [Fact]
        public async Task RequestDelegatePopulatesFromServiceParameterBasedOnParameterType()
        {
            var myOriginalService = new MyService();
            MyService? injectedService = null;

            void TestAction([FromService] MyService myService)
            {
                injectedService = myService;
            }

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(myOriginalService);

            var httpContext = new DefaultHttpContext();
            httpContext.RequestServices = serviceCollection.BuildServiceProvider();

            var requestDelegate = MapActionExpressionTreeBuilder.BuildRequestDelegate((Action<MyService>)TestAction);

            await requestDelegate(httpContext);

            Assert.Same(myOriginalService, injectedService);
        }

        [Fact]
        public async Task RequestDelegatePopulatesHttpContextParameterWithoutAttribute()
        {
            HttpContext? httpContextArgument = null;

            void TestAction(HttpContext httpContext)
            {
                httpContextArgument = httpContext;
            }

            var httpContext = new DefaultHttpContext();

            var requestDelegate = MapActionExpressionTreeBuilder.BuildRequestDelegate((Action<HttpContext>)TestAction);

            await requestDelegate(httpContext);

            Assert.Same(httpContext, httpContextArgument);
        }

        [Fact]
        public async Task RequestDelegatePopulatesIFormCollectionParameterWithoutAttribute()
        {
            IFormCollection? formCollectionArgument = null;

            void TestAction(IFormCollection httpContext)
            {
                formCollectionArgument = httpContext;
            }

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Content-Type"] = "application/x-www-form-urlencoded";

            var requestDelegate = MapActionExpressionTreeBuilder.BuildRequestDelegate((Action<IFormCollection>)TestAction);

            await requestDelegate(httpContext);

            Assert.Same(httpContext.Request.Form, formCollectionArgument);
        }

        [Fact]
        public async Task RequestDelegateWritesComplexReturnValueAsJsonResponseBody()
        {
            Todo originalTodo = new()
            {
                Name = "Write even more tests!"
            };

            Todo TestAction() => originalTodo;

            var httpContext = new DefaultHttpContext();
            var responseBodyStream = new MemoryStream();
            httpContext.Response.Body = responseBodyStream;

            var requestDelegate = MapActionExpressionTreeBuilder.BuildRequestDelegate((Func<Todo>)TestAction);

            await requestDelegate(httpContext);

            var deserializedResponseBody = JsonSerializer.Deserialize<Todo>(responseBodyStream.ToArray(), new JsonSerializerOptions
            {
                // TODO: the output is "{\"id\":0,\"name\":\"Write even more tests!\",\"isComplete\":false}"
                // Verify that the camelCased property names are consistent with MVC and if so whether we should keep the behavior.
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(deserializedResponseBody);
            Assert.Equal(originalTodo.Name, deserializedResponseBody!.Name);
        }

        [Fact]
        public async Task RequestDelegateUsesCustomIResult()
        {
            var resultString = "Still not enough tests!";

            CustomResult TestAction() => new(resultString!);

            var httpContext = new DefaultHttpContext();
            var responseBodyStream = new MemoryStream();
            httpContext.Response.Body = responseBodyStream;

            var requestDelegate = MapActionExpressionTreeBuilder.BuildRequestDelegate((Func<CustomResult>)TestAction);

            await requestDelegate(httpContext);

            var decodedResponseBody = Encoding.UTF8.GetString(responseBodyStream.ToArray());

            Assert.Equal(resultString, decodedResponseBody);
        }

        private class Todo
        {
            public int Id { get; set; }
            public string? Name { get; set; } = "Todo";
            public bool IsComplete { get; set; }
        }

        private class FromRouteAttribute : Attribute, IFromRouteMetadata
        {
            public string? Name { get; set; }
        }

        private class FromQueryAttribute : Attribute, IFromQueryMetadata
        {
            public string? Name { get; set; }
        }

        private class FromHeaderAttribute : Attribute, IFromHeaderMetadata
        {
            public string? Name { get; set; }
        }

        private class FromBodyAttribute : Attribute, IFromBodyMetadata
        {
        }

        private class FromFormAttribute : Attribute, IFromFormMetadata
        {
            public string? Name { get; set; }
        }

        private class FromServiceAttribute : Attribute, IFromServiceMetadata
        {
        }

        private class MyService
        {
        }

        private class CustomResult : IResult
        {
            private readonly string _resultString;

            public CustomResult(string resultString)
            {
                _resultString = resultString;
            }

            public Task ExecuteAsync(HttpContext httpContext)
            {
                return httpContext.Response.WriteAsync(_resultString);
            }
        }
    }
}
