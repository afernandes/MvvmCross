#nullable enable
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MvvmCross.IoC;
using MvvmCross.Navigation;

namespace MvvmCross.Hosting;

public class MvxHostBuilder : IMvxHostBuilder
{
    //private List<Action<IContainerRegistry>> _registrations { get; }
    private List<Action<IMvxIoCProvider>> _initializations { get; }
    private IMvxIoCProvider _container { get; }
    private Func<IMvxIoCProvider, IMvxNavigationService, Task> _onAppStarted;

    public Func<IServiceCollection, IServiceProvider> ConfigureContainer { get; }
    public IServiceCollection Services { get; }

    public MvxHostBuilder(
        IServiceCollection? serviceCollection = null,
        Func<IServiceCollection, IServiceProvider>? configureContainer = null)
    {
        Services = serviceCollection ?? new ServiceCollection();
        ConfigureContainer = configureContainer ?? (s => s.BuildServiceProvider());
    }

    public virtual IMvxHost Build()
    {
        ConfigureDefaultNullLogging(Services);
        var serviceCollection = ConfigureContainer(Services);
        var loggerFactory = serviceCollection.GetRequiredService<ILoggerFactory>();

        var host = new MvxHost(serviceCollection, loggerFactory);

        host.Run();

        return host;
    }

    private static void ConfigureDefaultNullLogging(IServiceCollection services)
    {
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
    }

    private bool _initialized;
    internal void OnInitialized()
    {
        if (_initialized)
            return;

        _initialized = true;
        _initializations.ForEach(action => action(_container));

        //if (_container.IsRegistered<IModuleCatalog>() && _container.Resolve<IModuleCatalog>().Modules.Any())
        //{
        //    var manager = _container.Resolve<IModuleManager>();
        //    manager.Run();
        //}

        //var navRegistry = _container.Resolve<IMvxNavigationService>();
        //if (!navRegistry.CanNavigate(NavigationPage))
        //{
        //    var registry = _container as IContainerRegistry;
        //    registry
        //        .Register<PrismNavigationPage>(() => new PrismNavigationPage())
        //        .RegisterInstance(new ViewRegistration
        //        {
        //            Name = nameof(NavigationPage),
        //            View = typeof(PrismNavigationPage),
        //            Type = ViewType.Page
        //        });
        //}

        //if (!navRegistry.IsRegistered(nameof(TabbedPage)))
        //{
        //    var registry = _container as IContainerRegistry;
        //    registry.RegisterForNavigation<TabbedPage>();
        //}
    }


    public IMvxHostBuilder OnAppStart(Func<IMvxIoCProvider, IMvxNavigationService, Task> onAppStarted)
    {
        _onAppStarted = onAppStarted;
        return this;
    }
}
