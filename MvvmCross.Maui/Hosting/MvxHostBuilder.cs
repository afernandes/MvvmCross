#nullable enable
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MvvmCross.Ioc.Maui;
using MvvmCross.IoC;
using MvvmCross.Maui;
using MvvmCross.Maui.Hosting;
using MvvmCross.Navigation;

namespace MvvmCross.Hosting;

public class MvxHostBuilder : IMvxHostBuilder
{
    private List<Action<IMvxIoCProvider>> _registrations { get; }
    private List<Action<IMvxIoCProvider>> _initializations { get; }
    private IMvxIoCProvider _container { get; }
    private Func<IMvxIoCProvider, IMvxNavigationService, Task> _onAppStarted;

    public Func<IMvxIoCProvider, IServiceProvider> ConfigureContainer { get; }
    public IMvxIoCProvider Container { get; }

    /// <summary>
    /// Gets the associated <see cref="MauiAppBuilder"/>.
    /// </summary>
    public MauiAppBuilder MauiBuilder { get; }

    public MvxHostBuilder(
        MauiAppBuilder builder,
        IMvxIoCProvider? container = null,
        Func<IMvxIoCProvider, IServiceProvider>? configureContainer = null)
    {
        _registrations = new List<Action<IMvxIoCProvider>>();
        _initializations = new List<Action<IMvxIoCProvider>>();

        Container = container ?? MvxIoCProvider.Initialize(null);
        ConfigureContainer = configureContainer ?? (s => new MvxServiceProvider(s));

        Container.RegisterType(() => this);
        Container.RegisterSingleton(typeof(IMauiInitializeService), new MvxInitializationService());

        Container.RegisterSingleton(typeof(IMvxIoCProvider), Container);
        Container.RegisterType<IServiceScopeFactory, MvxIocServiceScopeFactory>();
        Container.RegisterType<IServiceScope, MvxIocServiceScope>();

        Container.RegisterType<IWindowCreator, MvxWindowManager>();
        

        MauiBuilder = builder;
        MauiBuilder.ConfigureContainer(new MvxServiceProviderFactory(Container));
    }

    public virtual IMvxHost Build()
    {
        ConfigureDefaultNullLogging(Container);
        var serviceCollection = ConfigureContainer(Container);
        var loggerFactory = serviceCollection.GetRequiredService<ILoggerFactory>();

        var host = new MvxHost(serviceCollection, loggerFactory);

        host.Run();

        return host;
    }

    private static void ConfigureDefaultNullLogging(IMvxIoCProvider services)
    {
        services.RegisterSingleton(typeof(ILoggerFactory), new NullLoggerFactory());
        services.RegisterSingleton(typeof(ILogger<>), typeof(NullLogger<>));
    }

    private bool _initialized;
    internal void OnInitialized()
    {
        if (_initialized)
            return;

        _initialized = true;
        _initializations.ForEach(action => action(_container));
    }


    public IMvxHostBuilder OnAppStart(Func<IMvxIoCProvider, IMvxNavigationService, Task> onAppStarted)
    {
        _onAppStarted = onAppStarted;
        return this;
    }
}
