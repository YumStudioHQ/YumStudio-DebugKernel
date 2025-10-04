using Godot;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace YumStudio.DebugKernel.Source;

[GlobalClass]
public partial class Kernel : Node
{
  public string AssemblyPath { get; set; }
  public MethodBase EntryPoint { get; private set; } = null;
  public const string EntryMethodName = "_YumStudioEntry";
  private const string self = "DebugKernel";
  public Assembly Assembly { get; private set; }

  private readonly ConcurrentQueue<Action<double>> _processQueue = new();
  private readonly ConcurrentQueue<Action> _exitQueue = new();
  private readonly ConcurrentQueue<Action<InputEvent>> _inputQueue = new();
  private ConcurrentQueue<Action> _safeReady = new();

  private static string FormatMethodInfoDetailed(MethodInfo method)
  {
    var attrs = new List<string>();
    if (method.IsPublic) attrs.Add("public");
    if (method.IsPrivate) attrs.Add("private");
    if (method.IsStatic) attrs.Add("static");
    if (method.IsAbstract) attrs.Add("abstract");
    if (method.IsVirtual) attrs.Add("virtual");

    var modifiers = string.Join(" ", attrs);

    var declaringType = method.DeclaringType?.FullName ?? "<UnknownType>";
    var returnType = method.ReturnType?.FullName ?? "void";
    var parameters = string.Join(", ",
        method.GetParameters()
              .Select(p => $"{p.ParameterType.FullName} {p.Name}"));

    return $"{modifiers} {returnType} {declaringType}.{method.Name}({parameters})";
  }

  public void Plug()
  {
    if (!File.Exists(AssemblyPath)) throw new FileNotFoundException($"Cannot load assembly {AssemblyPath}");
    GD.Print($"{self}: Starting debug kernel for assembly {AssemblyPath}");

    AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
    {
      var loaded = AppDomain.CurrentDomain
                            .GetAssemblies()
                            .FirstOrDefault(a => a.GetName().Name == assemblyName.Name);

      if (loaded != null)
      {
        GD.Print($"{self}.Resolver: Reusing already loaded assembly: {loaded.FullName}");
        return loaded;
      }

      GD.Print($"{self}.Resolver: Could not resolve: {assemblyName.FullName}");
      return null;
    };

    Assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(AssemblyPath));
    foreach (var type in Assembly.GetTypes())
    {
      var method = type.GetMethod(EntryMethodName, BindingFlags.Static | BindingFlags.Public);
      if (method != null)
      {
        GD.Print($"{self}: Entry found: {FormatMethodInfoDetailed(method)}");
        EntryPoint = method;
        return;
      }
    }

    throw new MissingMethodException($"Expected method '{EntryMethodName}'");
  }

  public void Invoke(string[] args)
  {
    Action<Action<double>> safeQueue = _processQueue.Enqueue;
    Action<Action> exitQueue = _exitQueue.Enqueue;
    Action<Action<InputEvent>> inputQueue = _inputQueue.Enqueue;
    Action<Action> safeReady = _safeReady.Enqueue;

    EntryPoint?.Invoke(null, [this, safeReady, safeQueue, exitQueue, inputQueue, args]);
    while (_safeReady.TryDequeue(out var action))
    {
      try
      {
        action();
      }
      catch (Exception ex)
      {
        GD.PrintErr($"{self}: Input action threw exception: {ex}");
      }
    }
    _safeReady = null;
  }

  public override void _Process(double delta)
  {
    while (_processQueue.TryDequeue(out var action))
    {
      try
      {
        action(delta);
      }
      catch (Exception ex)
      {
        GD.PrintErr($"{self}: Action threw exception: {ex}");
      }
    }
  }

  public override void _Input(InputEvent @event)
  {
    while (_inputQueue.TryDequeue(out var action))
    {
      try
      {
        action(@event);
      }
      catch (Exception ex)
      {
        GD.PrintErr($"{self}: Input action threw exception: {ex}");
      }
    }
  }

  public override void _ExitTree()
  {
    while (_exitQueue.TryDequeue(out var action))
    {
      try
      {
        action();
      }
      catch (Exception ex)
      {
        GD.PrintErr($"{self}: Input action threw exception: {ex}");
      }
    }
  }
}
