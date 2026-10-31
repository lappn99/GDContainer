using System;

namespace GDContainer;

public class Factory<T>(Func<T> factoryFunc, DependencyScope scope)
    where T : class
{
    private Func<T> _factoryFunc = factoryFunc;
    private DependencyScope _scope = scope;

    public T Create()
    {

        var newInstance = _factoryFunc.Invoke();
        _scope.InjectInstance(newInstance);
        
        return newInstance;
    }
}