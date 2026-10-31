using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GDContainer.Interfaces;

namespace GDContainer;

public abstract partial class DependencyScope : Node
{

    private readonly HashSet<Type> _unresolvedSingletons = new();
    
    private readonly Dictionary<Type, object> _services = new();

    private readonly List<IReady> _readies = new();

    private readonly List<IProcess> _processes = new();

    public DependencyScope ParentScope { get; set; }


    public override void _EnterTree()
    {
        base._EnterTree();
        Configure();
    }

    public override void _Ready()
    {
        base._Ready();
        foreach (var ready in _readies)
        {
            ready.Ready();
        }
        ChildEnteredTree += OnChildEnteredTree;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        foreach (var process in _processes)
        {
            process.Process(delta);
        }
    }



    public void Register<T>()
    {
        
        _unresolvedSingletons.Add(typeof(T));
    }
    
    public void RegisterInstance<T>(T instance)
        where T: class
    {
        if (_services.ContainsKey(typeof(T)))
        {
            _services[typeof(T)] = instance;
        }
        else
        {
            _services.Add(typeof(T), instance);
        }
    }

    public void RegisterInstance<T, TI>(T instance)
        where T : class, TI
    {
        _services[typeof(TI)] = instance;
    }

    public void RegisterNode<T>(T instance)
        where T: Node
    {
        RegisterInstance(instance);
        instance.Connect(Node.SignalName.TreeEntered, Callable.From((() =>
        {   
            InjectInstance(instance);
        })), (uint)ConnectFlags.OneShot);
    }

    public void RegisterEntryPoint<T>()
    {
        Type type = typeof(T);
        var newInstance = CreateSingleton(type);
        _services.Add(type, newInstance);
        if ( type.IsAssignableTo(typeof(IReady)))
        {

            var readyInstance = (IReady)newInstance;
            if (_readies.Count((ready => ready is T)) > 0)
            {
                _readies.RemoveAll((ready => ready is T));
            }
            
            
            _readies.Add((IReady)newInstance);
        }
    }

    public void RegisterFactory<T>(Func<T> factoryFunc)
        where T: class
    {
        Factory<T> newFactory = new Factory<T>(factoryFunc, this);
        RegisterInstance(newFactory);
    }


    private object CreateSingleton(Type type)
    {
        if (!type.IsClass)
        {
            throw new InvalidOperationException($"{type.Name} is not a class");
            
        }


        ConstructorInfo[] constructors = type.GetConstructors();

        if (constructors.Length == 0)
        {
            throw new InvalidOperationException($"{type.Name} has no constructor");
        }

        ConstructorInfo constructor = constructors.OrderBy(ctor => ctor.GetParameters().Length).First();

        var parameters = constructor.GetParameters();

        List<object> ctorParams = new List<object>();
        
        foreach (var parameterInfo in parameters)
        {
            Type paramType = parameterInfo.ParameterType;
            if (TryResolve(paramType, out var paramInstance))
            {
                ctorParams.Add(paramInstance);
            }
            else
            {
                throw new InvalidOperationException($"{type.Name}: Dependency {paramType.Name} not registered");
            }
            
        }

        var instance = Activator.CreateInstance(type, ctorParams.ToArray());

        if (instance is IProcess process)
        {
            _processes.Add(process);
        }

        if (type.IsAssignableTo(typeof(IReady)))
        {
            _readies.Add((IReady)instance);
        }
        
        return instance;
    }

    public object Resolve(Type type)
    {
        if (TryResolve(type, out var service))
        {
            return service;
        }

        throw new InvalidOperationException($"No dependency found for type {type.Name}");
    }
    
    public T Resolve<T>()
        where T: class
    {

        return (T)Resolve(typeof(T));

    }

    public bool TryResolve<T>(out T result)
        where T: class
    {
        bool found = TryResolve(typeof(T), out var instance);
        result = (T)instance;
        return found;

    }

    public bool TryResolve(Type type, out object result)
    {

        if (_unresolvedSingletons.Contains(type))
        {
            var newInstance = CreateSingleton(type);
            _services.Add(type, newInstance);
            _unresolvedSingletons.Remove(type);
        }
        
        if (_services.TryGetValue(type,  out var instance))
        {
            result = instance;
            return true;
        }
        else if (ParentScope != null && ParentScope.TryResolve(type, out var parentInstance))
        {
            result = parentInstance;
            return true;
        }
        else
        {
            result = null;
            return false;
        }
    }
    
    

    public void InjectInstance( object instance)
    {
        Type type = instance.GetType();
        var methods = type.GetMethods().Where(x => x.GetCustomAttributes(typeof(InjectAttribute), true).Length > 0);
        
        foreach (var methodInfo in methods)
        {
            var args = methodInfo.GetParameters().Select(x =>
            {
                if (TryResolve(x.ParameterType, out var instance))
                {
                    return instance;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"{type.Name}: Dependency {x.ParameterType.Name} not registered");
                }
            }).ToArray();

            methodInfo.Invoke(instance, args);

        }
    }
    
    private void OnChildEnteredTree(Node node)
    {
        InjectInstance(node);
        //node.ChildEnteredTree += OnChildEnteredTree;
    }

    public abstract void Configure();

}
