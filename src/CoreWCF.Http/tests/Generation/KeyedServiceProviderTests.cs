﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests.Generation;

public partial class KeyedServiceProviderTests
{
    private readonly ITestOutputHelper _output;

    public KeyedServiceProviderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public class GetInjectedKeyedServiceTestTheoryData : TheoryData<Type>
    {
        public GetInjectedKeyedServiceTestTheoryData()
        {
            Add(typeof(SingletonStartup<MyKeyedServiceContract>));
            Add(typeof(SingletonStartup<MyOtherKeyedServiceContract>));
            Add(typeof(TransientStartup<MyKeyedServiceContract>));
            Add(typeof(TransientStartup<MyOtherKeyedServiceContract>));
            Add(typeof(Startup<MyExtraKeyedServiceContract>));
        }
    }

    [Theory]
    [ClassData(typeof(GetInjectedKeyedServiceTestTheoryData))]
    public void InjectedKeyedServiceTests(Type startupType)
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder(_output, startupType).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<IMyKeyedServiceContract>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/basichttp.svc")));
            IMyKeyedServiceContract channel = factory.CreateChannel();
            string result = channel.Hello("John");
            Assert.Equal("Bonjour John", result);
        }
    }

    [System.ServiceModel.ServiceContract]
    public interface IMyKeyedServiceContract
    {
        [System.ServiceModel.OperationContract]
        string Hello(string value);
    }

    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public partial class MyKeyedServiceContract : IMyKeyedServiceContract
    {
        public string Hello(string value, [Injected(ServiceKey = "fr")] HelloProvider o) => $"{o.Invoke()} {value}";
    }

    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public partial class MyOtherKeyedServiceContract : IMyKeyedServiceContract
    {
        public string Hello(string value, [FromKeyedServices("fr")] HelloProvider o) => $"{o.Invoke()} {value}";
    }

    [ServiceBehavior(IncludeExceptionDetailInFaults = true, InstanceContextMode = InstanceContextMode.Single)]
    public partial class MyExtraKeyedServiceContract : IMyKeyedServiceContract
    {
        public string Hello(string value, [FromKeyedServices("fr")] HelloProvider o) => $"{o.Invoke()} {value}";
    }

    public delegate string HelloProvider();

    internal class Startup<T> where T : class, IMyKeyedServiceContract
    {
        protected virtual void OnRegisterService(IServiceCollection services)
        {

        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
            OnRegisterService(services);
            services.AddKeyedTransient<HelloProvider>("fr", (_, _) => () => "Bonjour");
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<T>();
                builder.AddServiceEndpoint<T, IMyKeyedServiceContract>(new BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
            });
        }
    }

    internal class SingletonStartup<T> : Startup<T> where T : class, IMyKeyedServiceContract
    {
        protected override void OnRegisterService(IServiceCollection services) =>    services.AddSingleton<T>();
    }

    internal class TransientStartup<T> : Startup<T> where T : class, IMyKeyedServiceContract
    {
        protected override void OnRegisterService(IServiceCollection services) => services.AddTransient<T>();
    }
}
