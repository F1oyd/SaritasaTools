﻿// Copyright (c) 2015-2016, Saritasa. All rights reserved.
// Licensed under the BSD license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Saritasa.Tools.Messages.Abstractions;
using Saritasa.Tools.Messages.Internal;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Saritasa.Tools.Messages.Common
{
    /// <summary>
    /// Provides common functionality for Command/Query/Event executor middlewares.
    /// </summary>
    public abstract class BaseExecutorMiddleware : IMessagePipelineMiddleware, IAsyncMessagePipelineMiddleware
    {
        const string ParamKeyMethod = "method";
        const string ParamKeyClass = "class";

        /// <inheritdoc />
        public string Id { get; set; } = "Executor";

        /// <summary>
        /// Types resolver.
        /// </summary>
        protected Func<Type, object> Resolver { get; set; }

        /// <summary>
        /// If true the middleware will resolve project using internal resolver.
        /// </summary>
        public bool UseInternalObjectResolver { get; set; }

        /// <summary>
        /// If true the middleware will try to resolve executing method parameters. Default is false.
        /// </summary>
        public bool UseParametersResolve { get; set; }

        /// <summary>
        /// .ctor
        /// </summary>
        protected BaseExecutorMiddleware(IDictionary<string, string> dict)
        {
            if (dict == null)
            {
                throw new ArgumentNullException(nameof(dict));
            }

            if (dict.ContainsKey("id"))
            {
                Id = dict["id"];
            }

            if (!dict.ContainsKey(ParamKeyMethod) && !dict.ContainsKey(ParamKeyClass))
            {
                this.Resolver = Commands.CommandPipeline.NullResolver;
                return;
            }

            string methodName = dict["method"], className = dict["class"];
            var targetType = Type.GetType(className);
            if (targetType == null)
            {
                throw new InvalidOperationException($"Cannot find type {className}.");
            }
            var method = targetType.GetTypeInfo().GetMethod(methodName, BindingFlags.IgnoreCase | BindingFlags.Static
                                                                        | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException($"Cannot find method {methodName}. Make sure there is a static method in class {className}.");
            }
            if (method.GetParameters().Length != 1 || method.GetParameters()[0].ParameterType != typeof(Type)
                || method.ReturnType != typeof(object))
            {
                throw new InvalidOperationException($"Method {methodName} must have one input parameter type of Type and return Object.");
            }

            // Create delegate.
            var paramExpression = System.Linq.Expressions.Expression.Parameter(typeof(Type), "arg");
            var call = System.Linq.Expressions.Expression.Call(method, paramExpression);
            Func<Type, object> resolver = System.Linq.Expressions.Expression.Lambda<Func<Type, object>>(call, paramExpression).Compile();
            this.Resolver = resolver;
        }

        /// <summary>
        /// .ctor
        /// </summary>
        /// <param name="resolver">Types resolver.</param>
        protected BaseExecutorMiddleware(Func<Type, object> resolver)
        {
            if (resolver == null)
            {
                throw new ArgumentNullException(nameof(resolver));
            }
            Resolver = resolver;
        }

        /// <inheritdoc />
        public abstract void Handle(IMessage message);

        /// <inheritdoc />
        public abstract Task HandleAsync(IMessage message, CancellationToken cancellationToken);

        /// <summary>
        /// If UseInternalObjectResolver is turned off internal IoC container is used. Otherwise
        /// it relies on provided IoC implementation.
        /// </summary>
        /// <param name="type">Type to resolve.</param>
        /// <param name="loggingSource">Logging source. Optional.</param>
        /// <returns>Object or null if cannot be resolved.</returns>
        protected object ResolveObject(Type type, string loggingSource = "")
        {
            return UseInternalObjectResolver ?
                TypeHelpers.ResolveObjectForType(type, Resolver, loggingSource) :
                Resolver(type);
        }

        private object[] GetAndResolveHandlerParameters(object obj, MethodBase handlerMethod)
        {
            if (UseParametersResolve)
            {
                var parameters = handlerMethod.GetParameters();
                var paramsarr = new object[parameters.Length];
                if (parameters.Length > 1)
                {
                    if (handlerMethod.DeclaringType != obj.GetType())
                    {
                        paramsarr[0] = obj;
                        for (int i = 1; i < parameters.Length; i++)
                        {
                            paramsarr[i] = Resolver(parameters[i].ParameterType);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            paramsarr[i] = Resolver(parameters[i].ParameterType);
                        }
                    }
                    return paramsarr;
                }
                if (parameters.Length == 1)
                {
                    paramsarr[0] = obj;
                }
                return paramsarr;
            }
            return new[] { obj };
        }

        /// <summary>
        /// Execute method. If method is awaitable method will wait for it.
        /// </summary>
        /// <param name="handler">Handler.</param>
        /// <param name="obj">The first argument.</param>
        /// <param name="handlerMethod">Method to execute.</param>
        protected void ExecuteHandler(object handler, object obj, MethodBase handlerMethod)
        {
            var result = handlerMethod.Invoke(handler, GetAndResolveHandlerParameters(obj, handlerMethod));
            var task = result as Task;
            task?.ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Execute method in async mode. Handler should return <see cref="Task" /> otherwise
        /// it will be executed in sync mode.
        /// </summary>
        /// <param name="handler">Async handler.</param>
        /// <param name="obj">The first argument.</param>
        /// <param name="handlerMethod">Method to execute.</param>
        protected async Task ExecuteHandlerAsync(object handler, object obj, MethodBase handlerMethod)
        {
            var task = handlerMethod.Invoke(handler, GetAndResolveHandlerParameters(obj, handlerMethod)) as Task;
            if (task != null)
            {
                await task;
            }
        }
    }
}
