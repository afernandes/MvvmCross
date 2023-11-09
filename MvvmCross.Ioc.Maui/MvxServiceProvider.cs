using MvvmCross.IoC;

namespace MvvmCross.Ioc.Maui
{

    internal class MvxServiceProvider : IServiceProvider
    {
        private IMvxIoCProvider _container { get; }

        public MvxServiceProvider(IMvxIoCProvider container)
        {
            _container = container;
        }

        object IServiceProvider.GetService(Type serviceType) => _container.Resolve(serviceType);
    }
}
