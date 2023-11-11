using MvvmCross.IoC;

namespace MvvmCross.Ioc.Maui
{
    public sealed class MvxIocServiceScope : IServiceScope, IDisposable
    {
        private readonly IMvxIoCProvider _resolverContext;

        public IServiceProvider ServiceProvider => new MvxServiceProvider(this._resolverContext);

        public MvxIocServiceScope(IMvxIoCProvider resolverContext) => this._resolverContext = resolverContext;

        public void Dispose()
        {
            //this._resolverContext.Dispose();
        }
    }
}
