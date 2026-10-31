# GDContainer

Scoped dependency injection for Godot 4 C#. No dependencies beyond Godot itself.

## Install
Copy `addons/GDContainer/` into your project's `addons/` folder, build the C# solution, and (optionally) enable the plugin in Project Settings > Plugins.

## Usage
```csharp
using GDContainer;

public partial class RootScope : DependencyScope
{
    public override void Configure()
    {
        Register<MyService>();            // lazy singleton, constructor-injected
        RegisterInstance(someObject);     // existing instance
        RegisterNode(someNode);           // existing node, injected when it enters the tree
        RegisterEntryPoint<MyStartup>();  // created eagerly
        RegisterFactory(() => new Thing());
    }
}
```

- `[Inject]` on a public method receives resolved dependencies. Child nodes of a scope are injected when they enter the tree.
- `Factory<T>.Create()` builds an instance and injects it.
- Classes implementing `GDContainer.Interfaces.IReady` / `IProcess` get `Ready()` / `Process(double)` called by the owning scope.
- Set `ParentScope` to chain scopes; unresolved types fall back to the parent.
