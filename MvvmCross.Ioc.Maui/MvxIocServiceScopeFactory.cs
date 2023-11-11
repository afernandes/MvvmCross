using MvvmCross.IoC;

namespace MvvmCross.Ioc.Maui
{
    public sealed class MvxIocServiceScopeFactory : IServiceScopeFactory
    {
        private readonly IMvxIoCProvider _scopedResolver;

        public MvxIocServiceScopeFactory(IMvxIoCProvider scopedResolver) => this._scopedResolver = scopedResolver;

        public IServiceScope CreateScope()
        {
            IMvxIoCProvider scopedResolver = this._scopedResolver;
            //IScope scope = scopedResolver.ScopeContext == null ? Scope.Of(scopedResolver.OwnCurrentScope) : scopedResolver.ScopeContext.SetCurrent((SetCurrentScopeHandler)(p => Scope.Of(p)));
            return (IServiceScope)new MvxIocServiceScope(scopedResolver);
        }
    }
}
