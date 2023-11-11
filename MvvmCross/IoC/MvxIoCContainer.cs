// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MS-PL license.
// See the LICENSE file in the project root for more information.
#nullable enable
using System.Collections;
using System.Reflection;
using Microsoft.Extensions.Logging;
using MvvmCross.Base;
using MvvmCross.Exceptions;
using MvvmCross.Logging;

namespace MvvmCross.IoC
{
    public sealed class MvxIoCContainer
        : IMvxIoCProvider
    {
        private readonly Dictionary<Type, IResolver> _resolvers = new();
        private readonly Dictionary<Type, bool> _circularTypeDetection = new();
        private readonly object _lockObject = new();
        private readonly IMvxIocOptions _options;
        private readonly IMvxPropertyInjector? _propertyInjector;
        private readonly IMvxIoCProvider? _parentProvider;

        private readonly Dictionary<Type, IList<Func<object>>> _iEnumerableFactories = new Dictionary<Type, IList<Func<object>>>();

        private IMvxIocOptions Options => _options;

        public MvxIoCContainer(IMvxIocOptions? options, IMvxIoCProvider? parentProvider = null)
        {
            _options = options ?? new MvxIocOptions();
            if (_options.PropertyInjectorType != null)
            {
                _propertyInjector = Activator.CreateInstance(_options.PropertyInjectorType) as IMvxPropertyInjector;
            }
            if (_propertyInjector != null)
            {
                RegisterSingleton(typeof(IMvxPropertyInjector), _propertyInjector);
            }
            if (parentProvider != null)
            {
                _parentProvider = parentProvider;
            }
        }

        public MvxIoCContainer(IMvxIoCProvider parentProvider)
            : this(null, parentProvider)
        {
            if (parentProvider == null)
            {
                throw new ArgumentNullException(nameof(parentProvider), "Provide a parent ioc provider to this constructor");
            }
        }

        private interface IResolver
        {
            object? Resolve();

            ResolverType ResolveType { get; }

            void SetGenericTypeParameters(Type[] genericTypeParameters);
        }

        private sealed class ConstructingResolver : IResolver
        {
            private readonly Type _type;
            private readonly IMvxIoCProvider _parent;

            public ConstructingResolver(Type type, IMvxIoCProvider parent)
            {
                _type = type;
                _parent = parent;
            }

            public object? Resolve()
            {
                return _parent.IoCConstruct(_type, (object?)null);
            }

            public void SetGenericTypeParameters(Type[] genericTypeParameters)
            {
                throw new InvalidOperationException("This Resolver does not set generic type parameters");
            }

            public ResolverType ResolveType => ResolverType.DynamicPerResolve;
        }

        private sealed class FuncConstructingResolver : IResolver
        {
            private readonly Func<object?> _constructor;

            public FuncConstructingResolver(Func<object?> constructor)
            {
                _constructor = constructor;
            }

            public object? Resolve()
            {
                return _constructor();
            }

            public void SetGenericTypeParameters(Type[] genericTypeParameters)
            {
                throw new InvalidOperationException("This Resolver does not set generic type parameters");
            }

            public ResolverType ResolveType => ResolverType.DynamicPerResolve;
        }

        private sealed class SingletonResolver : IResolver
        {
            private readonly object _theObject;

            public SingletonResolver(object theObject)
            {
                _theObject = theObject;
            }

            public object Resolve()
            {
                return _theObject;
            }

            public void SetGenericTypeParameters(Type[] genericTypeParameters)
            {
                throw new InvalidOperationException("This Resolver does not set generic type parameters");
            }

            public ResolverType ResolveType => ResolverType.Singleton;
        }

        private sealed class ConstructingSingletonResolver : IResolver
        {
            private readonly object _syncObject = new();
            private readonly Func<object> _constructor;
            private object? _theObject;

            public ConstructingSingletonResolver(Func<object> theConstructor)
            {
                _constructor = theConstructor;
            }

            public object? Resolve()
            {
                if (_theObject != null)
                    return _theObject;

                object constructed;
                lock (_syncObject)
                {
                    if (_theObject != null)
                        return _theObject;

                    constructed = _constructor();
                }
                _theObject = constructed;

                return _theObject;
            }

            public void SetGenericTypeParameters(Type[] genericTypeParameters)
            {
                throw new InvalidOperationException("This Resolver does not set generic type parameters");
            }

            public ResolverType ResolveType => ResolverType.Singleton;
        }

        private sealed class ConstructingOpenGenericResolver : IResolver
        {
            private readonly Type _genericTypeDefinition;
            private readonly IMvxIoCProvider _parent;

            private Type[]? _genericTypeParameters;

            public ConstructingOpenGenericResolver(Type genericTypeDefinition, IMvxIoCProvider parent)
            {
                _genericTypeDefinition = genericTypeDefinition;
                _parent = parent;
            }

            public void SetGenericTypeParameters(Type[] genericTypeParameters)
            {
                _genericTypeParameters = genericTypeParameters;
            }

            public object? Resolve()
            {
                if (_genericTypeParameters == null)
                {
                    throw new MvxIoCResolveException("No Generic Type Parameters provided for Type: {0}",
                        _genericTypeDefinition.FullName);
                }

                return _parent.IoCConstruct(_genericTypeDefinition.MakeGenericType(_genericTypeParameters),
                    (object?)null);
            }

            public ResolverType ResolveType => ResolverType.DynamicPerResolve;
        }

        public void RegisterType<TTInterface, TImplementation>() where TTInterface : class where TImplementation : class, TTInterface
        {
            RegisterTypeInternal<TTInterface, TImplementation>();

            var t = typeof(TTInterface);
            if (!_iEnumerableFactories.TryGetValue(t, out var factories))
            {
                factories = new List<Func<object>>();
                _iEnumerableFactories.Add(t, factories);
            }
            // HACK: in order to NOT override an existing service in MvvmCross and still be able to directly resovle the concrete implementation
            // and return it in an IEnuemrable<tInterface>, we need to register the implementation as itself and resovle it later on
            RegisterTypeInternal<TImplementation, TImplementation>();
            factories.Add(Resolve<TImplementation>);

            // finally register a factory which uses the cached registrations to actually resolve an IEnumerable of tInterface :-)
            RegisterTypeInternal<IEnumerable<TTInterface>>(() => _iEnumerableFactories[t].Select(f => f()).Cast<TTInterface>().ToArray());
        }

        public void RegisterType<TInterface>(Func<TInterface> constructor) where TInterface : class
        {
            RegisterTypeInternal(constructor);


            var t = typeof(TInterface);
            if (!_iEnumerableFactories.TryGetValue(t, out var factories))
            {
                factories = new List<Func<object>>();
                _iEnumerableFactories.Add(t, factories);
            }
            factories.Add(Resolve<TInterface>);

            // finally register a factory which uses the cached registrations to actually resolve an IEnumerable of tInterface :-)
            RegisterTypeInternal<IEnumerable<TInterface>>(() => _iEnumerableFactories[t].Select(f => f()).Cast<TInterface>().ToArray());
        }

        public void RegisterType(Type tInterface, Func<object> constructor)
        {
            RegisterTypeInternal(tInterface, constructor);

            if (!_iEnumerableFactories.TryGetValue(tInterface, out var factories))
            {
                factories = new List<Func<object>>();
                _iEnumerableFactories.Add(tInterface, factories);
            }
            factories.Add(constructor);
            var enumType = typeof(IEnumerable<>).MakeGenericType(tInterface);
            // finally register a factory which uses the cached registrations to actually resolve an IEnumerable of tInterface :-)
            RegisterTypeInternal(enumType, () => _iEnumerableFactories[tInterface].Select(f => f()).CreateEnumerable(tInterface));
        }

        public void RegisterType(Type tInterface, Type tImplementation)
        {
            RegisterTypeInternal(tInterface, tImplementation);


            if (!_iEnumerableFactories.TryGetValue(tInterface, out var factories))
            {
                factories = new List<Func<object>>();
                _iEnumerableFactories.Add(tInterface, factories);
            }
            // HACK: in order to NOT override an existing service in MvvmCross and still be able to directly resovle the concrete implementation
            // and return it in an IEnuemrable<tInterface>, we need to register the implementation as itself and resovle it later on
            RegisterTypeInternal(tImplementation, tImplementation);
            factories.Add(() => Resolve(tImplementation));

            var enumType = typeof(IEnumerable<>).MakeGenericType(tInterface);
            // finally register a factory which uses the cached registrations to actually resolve an IEnumerable of tInterface :-)
            RegisterTypeInternal(enumType, () => _iEnumerableFactories[tInterface].Select(f => f()).CreateEnumerable(tInterface));
        }

        public void RegisterSingleton<TInterface>(TInterface theObject) where TInterface : class
        {
            RegisterSingletonInternal(theObject);


            var t = typeof(TInterface);
            if (!_iEnumerableFactories.TryGetValue(t, out var factories))
            {
                factories = new List<Func<object>>();
                _iEnumerableFactories.Add(t, factories);
            }
            factories.Add(() => theObject);
            // finally register a factory which uses the cached registrations to actually resolve an IEnumerable of tInterface :-)
            RegisterTypeInternal<IEnumerable<TInterface>>(() => _iEnumerableFactories[t].Select(f => f()).Cast<TInterface>().ToArray());
        }

        public void RegisterSingleton(Type tInterface, object theObject)
        {
            RegisterSingletonInternal(tInterface, theObject);


            var t = tInterface;
            if (!_iEnumerableFactories.TryGetValue(t, out var factories))
            {
                factories = new List<Func<object>>();
                _iEnumerableFactories.Add(t, factories);
            }
            factories.Add(() => theObject);
            var enumType = typeof(IEnumerable<>).MakeGenericType(t);
            // finally register a factory which uses the cached registrations to actually resolve an IEnumerable of tInterface :-)
            //RegisterTypeInternal(enumType, () => _iEnumerableFactories[t].Select(f => f()).CreateEnumerable(tInterface));
            RegisterTypeInternal(enumType, () => _iEnumerableFactories[t].Select(f => f()).ToArray());
        }

        public void RegisterSingleton<TInterface>(Func<TInterface> theConstructor) where TInterface : class
        {
            RegisterSingletonInternal(theConstructor);


            var t = typeof(TInterface);
            if (!_iEnumerableFactories.TryGetValue(t, out var factories))
            {
                factories = new List<Func<object>>();
                _iEnumerableFactories.Add(t, factories);
            }
            factories.Add(Resolve<TInterface>);

            // finally register a factory which uses the cached registrations to actually resolve an IEnumerable of tInterface :-)
            RegisterTypeInternal<IEnumerable<TInterface>>(() => _iEnumerableFactories[t].Select(f => f()).Cast<TInterface>().ToArray());
        }

        public void RegisterSingleton(Type tInterface, Func<object> theConstructor)
        {
            RegisterSingletonInternal(tInterface, theConstructor);

            var t = tInterface;
            if (!_iEnumerableFactories.TryGetValue(t, out var factories))
            {
                factories = new List<Func<object>>();
                _iEnumerableFactories.Add(t, factories);
            }

            factories.Add(theConstructor);
            var enumType = typeof(IEnumerable<>).MakeGenericType(t);
            // finally register a factory which uses the cached registrations to actually resolve an IEnumerable of tInterface :-)
            RegisterTypeInternal(enumType, () => _iEnumerableFactories[t].Select(f => f()).CreateEnumerable(tInterface));
        }

        public bool CanResolve<T>()
            where T : class
        {
            return CanResolve(typeof(T));
        }

        public bool CanResolve(Type type)
        {
            lock (_lockObject)
            {
                if (_resolvers.ContainsKey(type))
                {
                    return true;
                }
                if (_parentProvider != null && _parentProvider.CanResolve(type))
                {
                    return true;
                }
                return false;
            }
        }

        public bool TryResolve<T>(out T? resolved)
            where T : class
        {
            try
            {
                var toReturn = TryResolve(typeof(T), out var item);
                resolved = (T?)item;
                return toReturn;
            }
            catch (MvxIoCResolveException)
            {
                resolved = (T?)typeof(T).CreateDefault();
                return false;
            }
        }

        public bool TryResolve(Type type, out object? resolved)
        {
            lock (_lockObject)
            {
                return InternalTryResolve(type, out resolved);
            }
        }

        public T? Resolve<T>()
            where T : class
        {
            return (T?)Resolve(typeof(T));
        }

        public object? Resolve(Type type)
        {
            lock (_lockObject)
            {
                if (!InternalTryResolve(type, out var resolved))
                {
                    throw new MvxIoCResolveException("Failed to resolve type {0}", type.FullName);
                }
                return resolved;
            }
        }

        public T? GetSingleton<T>()
            where T : class
        {
            return (T?)GetSingleton(typeof(T));
        }

        public object? GetSingleton(Type type)
        {
            lock (_lockObject)
            {
                if (!InternalTryResolve(type, ResolverType.Singleton, out var resolved))
                {
                    throw new MvxIoCResolveException("Failed to resolve type {0}", type.FullName);
                }
                return resolved;
            }
        }

        public T? Create<T>()
            where T : class
        {
            return (T?)Create(typeof(T));
        }

        public object? Create(Type type)
        {
            lock (_lockObject)
            {
                if (!InternalTryResolve(type, ResolverType.DynamicPerResolve, out var resolved))
                {
                    throw new MvxIoCResolveException("Failed to resolve type {0}", type.FullName);
                }
                return resolved;
            }
        }

        public void RegisterTypeInternal<TInterface, TToConstruct>()
            where TInterface : class
            where TToConstruct : class, TInterface
        {
            RegisterTypeInternal(typeof(TInterface), typeof(TToConstruct));
        }

        public void RegisterTypeInternal<TInterface>(Func<TInterface> constructor)
            where TInterface : class
        {
            var resolver = new FuncConstructingResolver(constructor);
            InternalSetResolver(typeof(TInterface), resolver);
        }

        public void RegisterTypeInternal(Type t, Func<object?> constructor)
        {
            var resolver = new FuncConstructingResolver(() =>
            {
                var ret = constructor();
                if (ret != null && !t.IsInstanceOfType(ret))
                {
                    try
                    {
                        ret = ExtendedMvxIocProviderExtensions.CreateEnumerable((IEnumerable<object>)ret, t.GenericTypeArguments[0]);
                    }
                    catch (Exception e)
                    {
                        throw new MvxIoCResolveException("Constructor failed to return a compatibly object for type {0}",
                            t.FullName);
                    }
                }

                return ret;
            });

            InternalSetResolver(t, resolver);
        }

        public void RegisterTypeInternal(Type tFrom, Type tTo)
        {
            IResolver resolver;
            if (tFrom.GetTypeInfo().IsGenericTypeDefinition)
            {
                resolver = new ConstructingOpenGenericResolver(tTo, this);
            }
            else
            {
                resolver = new ConstructingResolver(tTo, this);
            }

            InternalSetResolver(tFrom, resolver);
        }

        public void RegisterSingletonInternal<TInterface>(TInterface theObject)
            where TInterface : class
        {
            RegisterSingletonInternal(typeof(TInterface), theObject);
        }

        public void RegisterSingletonInternal(Type tInterface, object theObject)
        {
            InternalSetResolver(tInterface, new SingletonResolver(theObject));
        }

        public void RegisterSingletonInternal<TInterface>(Func<TInterface> theConstructor)
            where TInterface : class
        {
            RegisterSingletonInternal(typeof(TInterface), theConstructor);
        }

        public void RegisterSingletonInternal(Type tInterface, Func<object> theConstructor)
        {
            InternalSetResolver(tInterface, new ConstructingSingletonResolver(theConstructor));
        }

        public object? IoCConstruct(Type type)
        {
            return IoCConstruct(type, (IDictionary<string, object>?)null);
        }

        public object? IoCConstruct(Type type, object? arguments)
        {
            return IoCConstruct(type, arguments?.ToPropertyDictionary());
        }

        public T? IoCConstruct<T>()
            where T : class
        {
            return (T?)IoCConstruct(typeof(T), (IDictionary<string, object>?)null);
        }

        public T? IoCConstruct<T>(IDictionary<string, object>? arguments)
            where T : class
        {
            return (T?)IoCConstruct(typeof(T), arguments);
        }

        public T? IoCConstruct<T>(object? arguments)
            where T : class
        {
            return (T?)IoCConstruct(typeof(T), arguments?.ToPropertyDictionary());
        }

        public T? IoCConstruct<T>(params object?[] arguments) where T : class
        {
            return (T?)IoCConstruct(typeof(T), arguments);
        }

        public object IoCConstruct(Type type, params object?[] arguments)
        {
            var selectedConstructor = type.FindApplicableConstructor(arguments);

            if (selectedConstructor == null)
            {
                throw new MvxIoCResolveException("Failed to find constructor for type {0} with arguments: {1}",
                    type.FullName, arguments?.Select(x => x.GetType().Name));
            }

            var parameters = GetIoCParameterValues(type, selectedConstructor, arguments);
            return IoCConstruct(type, selectedConstructor, parameters.ToArray());
        }

        public object IoCConstruct(Type type, IDictionary<string, object>? arguments)
        {
            var selectedConstructor = type.FindApplicableConstructor(arguments);

            if (selectedConstructor == null)
            {
                throw new MvxIoCResolveException("Failed to find constructor for type {0}", type.FullName);
            }

            var parameters = GetIoCParameterValues(type, selectedConstructor, arguments);
            return IoCConstruct(type, selectedConstructor, parameters.ToArray());
        }

        private object IoCConstruct(Type type, ConstructorInfo constructor, object[] arguments)
        {
            object toReturn;
            try
            {
                toReturn = constructor.Invoke(arguments);
            }
            catch (TargetInvocationException invocation)
            {
                throw new MvxIoCResolveException(invocation, "Failed to construct {0}", type.Name);
            }

            try
            {
                InjectProperties(toReturn);
            }
            catch (Exception)
            {
                if (!Options.CheckDisposeIfPropertyInjectionFails)
                    throw;

                toReturn.DisposeIfDisposable();
                throw;
            }
            return toReturn;
        }

        public void CleanAllResolvers()
        {
            lock (_lockObject)
            {
                _resolvers.Clear();
                _circularTypeDetection.Clear();
            }
        }

        private enum ResolverType
        {
            DynamicPerResolve,
            Singleton,
            Unknown
        }

        public IMvxIoCProvider CreateChildContainer() => new MvxIoCContainer(this);

        private static readonly ResolverType? ResolverTypeNoneSpecified = null;

        private static bool Supports(IResolver? resolver, ResolverType? requiredResolverType)
        {
            if (resolver == null)
                return false;

            if (!requiredResolverType.HasValue)
                return true;

            return resolver.ResolveType == requiredResolverType.Value;
        }

        private bool InternalTryResolve(Type type, out object? resolved)
        {
            return InternalTryResolve(type, ResolverTypeNoneSpecified, out resolved);
        }

        private bool InternalTryResolve(Type type, ResolverType? requiredResolverType, out object? resolved)
        {
            if (!TryGetResolver(type, out var resolver))
            {
                if (_parentProvider != null && _parentProvider.TryResolve(type, out resolved))
                {
                    return true;
                }

                resolved = type.CreateDefault();
                return false;
            }

            if (!Supports(resolver, requiredResolverType))
            {
                resolved = type.CreateDefault();
                return false;
            }

            return InternalTryResolve(type, resolver!, out resolved);
        }

        private bool InternalTryResolve(Type type, IResolver resolver, out object? resolved)
        {
            var detectingCircular = ShouldDetectCircularReferencesFor(resolver);
            if (detectingCircular)
            {
                try
                {
                    _circularTypeDetection.Add(type, true);
                }
                catch (ArgumentException)
                {
                    // the item already exists in the lookup table
                    // - this is "game over" for the IoC lookup
                    // - see https://github.com/MvvmCross/MvvmCross/issues/553
                    MvxLogHost.Default?.Log(LogLevel.Error,
                        "IoC circular reference detected - cannot currently resolve {TypeName}", type.Name);
                    resolved = type.CreateDefault();
                    return false;
                }
            }

            try
            {
                if (resolver is ConstructingOpenGenericResolver)
                {
                    resolver.SetGenericTypeParameters(type.GetTypeInfo().GenericTypeArguments);
                }

                var raw = resolver.Resolve();
                if (raw == null)
                {
                    throw new MvxException("Resolver returned null");
                }
                if (!type.IsInstanceOfType(raw))
                {
                    throw new MvxException("Resolver returned object type {0} which does not support interface {1}",
                                           raw.GetType().FullName, type.FullName);
                }

                resolved = raw;
                return true;
            }
            finally
            {
                if (detectingCircular)
                {
                    _circularTypeDetection.Remove(type);
                }
            }
        }

        private bool TryGetResolver(Type type, out IResolver? resolver)
        {
            if (_resolvers.TryGetValue(type, out resolver))
            {
                return true;
            }

            if (!type.GetTypeInfo().IsGenericType)
            {
                return false;
            }

            return _resolvers.TryGetValue(type.GetTypeInfo().GetGenericTypeDefinition(), out resolver);
        }

        private bool ShouldDetectCircularReferencesFor(IResolver resolver)
        {
            switch (resolver.ResolveType)
            {
                case ResolverType.DynamicPerResolve:
                    return Options.TryToDetectDynamicCircularReferences;

                case ResolverType.Singleton:
                    return Options.TryToDetectSingletonCircularReferences;

                case ResolverType.Unknown:
                    throw new MvxException("A resolver must have a known type - error in {0}", resolver.GetType().Name);
                default:
                    throw new ArgumentOutOfRangeException(nameof(resolver), "unknown resolveType of " + resolver.ResolveType);
            }
        }

        private void InternalSetResolver(Type interfaceType, IResolver resolver)
        {
            lock (_lockObject)
            {
                _resolvers[interfaceType] = resolver;
            }
        }

        private void InjectProperties(object toReturn)
        {
            _propertyInjector?.Inject(toReturn, _options.PropertyInjectorOptions);
        }

        private List<object> GetIoCParameterValues(Type type, MethodBase selectedConstructor, IDictionary<string, object>? arguments)
        {
            var parameters = new List<object>();
            foreach (var parameterInfo in selectedConstructor.GetParameters())
            {
                if (!string.IsNullOrEmpty(parameterInfo.Name) &&
                    arguments?.TryGetValue(parameterInfo.Name, out var argument) is true)
                {
                    parameters.Add(argument);
                }
                else if (TryResolveParameter(type, parameterInfo, out var parameterValue))
                {
                    parameters.Add(parameterValue);
                }
            }
            return parameters;
        }

        private List<object> GetIoCParameterValues(Type type, MethodBase selectedConstructor, object[]? arguments)
        {
            var parameters = new List<object>();
            if (arguments == null)
                return parameters;

            var unusedArguments = arguments.ToList();

            foreach (var parameterInfo in selectedConstructor.GetParameters())
            {
                var argumentMatch = unusedArguments.Find(arg => parameterInfo.ParameterType.IsInstanceOfType(arg));

                if (argumentMatch != null)
                {
                    parameters.Add(argumentMatch);
                    unusedArguments.Remove(argumentMatch);
                }
                else if (TryResolveParameter(type, parameterInfo, out var parameterValue) && parameterValue != null)
                {
                    parameters.Add(parameterValue);
                }
            }
            return parameters;
        }

        private bool TryResolveParameter(Type type, ParameterInfo parameterInfo, out object? parameterValue)
        {
            if (!TryResolve(parameterInfo.ParameterType, out parameterValue))
            {
                if (parameterInfo.IsOptional)
                {
                    parameterValue = Type.Missing;
                }
                else
                {
                    throw new MvxIoCResolveException(
                        "Failed to resolve parameter for parameter {0} of type {1} when creating {2}. You may pass it as an argument",
                        parameterInfo.Name,
                        parameterInfo.ParameterType.Name,
                        type.FullName);
                }
            }

            return true;
        }
    }

    internal static class ExtendedMvxIocProviderExtensions
    {
        public static object CreateEnumerable(this IEnumerable<object> items, Type tInterface)
        {
            try
            {
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(tInterface));
                foreach (var item in items)
                    list.Add(item);
                return list;
            }
            catch (Exception e)
            {
                return items;
            }
        }
    }
}
