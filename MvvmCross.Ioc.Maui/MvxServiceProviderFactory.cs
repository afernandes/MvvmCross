using MvvmCross.IoC;

#nullable enable
namespace MvvmCross.Ioc.Maui
{
    public class MvxServiceProviderFactory : IServiceProviderFactory<IMvxIoCProvider>
    {
        private IServiceCollection services;

        private Action<IMvxIoCProvider> _registerTypes { get; }

        private Lazy<IMvxIoCProvider> _currentContainer { get; }

        public MvxServiceProviderFactory(Action<IMvxIoCProvider> registerTypes)
        {
            this._registerTypes = registerTypes;
            this._currentContainer = new Lazy<IMvxIoCProvider>((Func<IMvxIoCProvider>)(() => MvxIoCProvider.Instance ?? MvxIoCProvider.Initialize(null)));
        }

        public MvxServiceProviderFactory(IMvxIoCProvider containerExtension)
        {
            this._registerTypes = (Action<IMvxIoCProvider>)(_ => { });
            this._currentContainer = new Lazy<IMvxIoCProvider>((Func<IMvxIoCProvider>)(() => containerExtension));
        }

        public IMvxIoCProvider CreateBuilder(IServiceCollection services)
        {
            this.services = services;

            IMvxIoCProvider container = this._currentContainer.Value;
            //container.Populate(services);
            //this._registerTypes(container);
            return container;
        }

        public IServiceProvider CreateServiceProvider(IMvxIoCProvider containerExtension)
            => containerExtension.CreateServiceProvider(this.services);
    }
}
