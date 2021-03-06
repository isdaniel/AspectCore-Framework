﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AspectCore.Extensions.Reflection;
using AspectCore.Utils;

namespace AspectCore.DynamicProxy
{
    [NonAspect]
    internal sealed class RuntimeAspectContext : AspectContext
    {
        private static readonly ConcurrentDictionary<MethodInfo, MethodReflector> reflectorTable = new ConcurrentDictionary<MethodInfo, MethodReflector>();

        private volatile IDictionary<string, object> _data;
        private IServiceProvider _serviceProvider;
        private MethodInfo _implMethod;
        private object _implInstance;
        private bool _disposedValue = false;

        public override IServiceProvider ServiceProvider
        {
            get
            {
                if (_serviceProvider == null)
                {
                    throw new NotSupportedException("The current context does not support IServiceProvider.");
                }

                return _serviceProvider;
            }
        }

        public override IDictionary<string, object> AdditionalData
        {

            get
            {
                if (_data == null)
                {
                    _data = new Dictionary<string, object>();
                }
                return _data;
            }
        }

        public override object ReturnValue { get; set; }

        public override MethodInfo ServiceMethod { get; }

        public override object[] Parameters { get; }

        public override MethodInfo ProxyMethod { get; }

        public override object ProxyInstance { get; }

        public RuntimeAspectContext(IServiceProvider serviceProvider, MethodInfo serviceMethod, MethodInfo implMethod, MethodInfo proxyMethod, object implInstance, object proxyInstance, object[] parameters)
        {
            _serviceProvider = serviceProvider;
            _implMethod = implMethod;
            _implInstance = implInstance;
            ServiceMethod = serviceMethod;
            ProxyMethod = proxyMethod;
            ProxyInstance = proxyInstance;
            Parameters = parameters;
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposedValue)
            {
                return;
            }

            if (!disposing)
            {
                return;
            }

            if (_data == null)
            {
                _disposedValue = true;
                return;
            }

            foreach (var key in _data.Keys.ToArray())
            {
                _data.TryGetValue(key, out object value);

                var disposable = value as IDisposable;

                disposable?.Dispose();

                _data.Remove(key);
            }

            _disposedValue = true;
        }

        public override Task Complete()
        {
            var reflector = reflectorTable.GetOrAdd(_implMethod, method => method.GetReflector(CallOptions.Call));
            ReturnValue = reflector.Invoke(_implInstance, Parameters);
            return TaskUtils.CompletedTask;
        }

        public override Task Break()
        {
            if (ReturnValue == null)
            {
                ReturnValue = ServiceMethod.ReturnParameter.ParameterType.GetDefaultValue();
            }
            return TaskUtils.CompletedTask;
        }
    }
}