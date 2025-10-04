using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace YumStudio.DebugKernel.Source;

public partial class Entry : Control
{
  private Kernel kernel;
  private RichTextLabel memorySpecs;
  private RichTextLabel AsmDump;
  private Graph.MemoryGraph graphWorking;
  private Graph.MemoryGraph graphManaged;

  private double _timer = 0;

  private List<long> _historyWorkingSet = [];
  private List<long> _historyManaged = [];
  private const int MaxHistory = 0x2000;

  public override void _Ready()
  {
    kernel = GetNode<Kernel>("Kernel");
    memorySpecs = GetNode<RichTextLabel>("Memory/MemorySpecs");
    graphWorking = GetNode<Graph.MemoryGraph>("Memory/WorkingGraph");
    graphManaged = GetNode<Graph.MemoryGraph>("Memory/ManagedGraph");
    AsmDump = GetNode<RichTextLabel>("AssemblyDumper/RichTextLabel");
    graphWorking.LineColor = new(b: 1f, a: 1f, r: 0, g: 0);
    graphManaged.LineColor = new(b: 0f, a: 1f, r: 1, g: 0);
    string asm = "";
    var args = OS.GetCmdlineArgs();
    for (int i = 0; i < args.Length; i++)
    {
      if (args[i] == "--asm")
      {
        asm = args[i + 1];
        i += 1;
      }
      else
      {
        var smth = new ArgumentException("Expected argument after '--asm'");
        kernel.AddChild(new Label() { Text = $"{smth}\n{new Exception("Cannot load anything -- No such Assembly provided")}" });
      }
    }

    kernel.AssemblyPath = asm;
    kernel.Plug();
    AsmDump.Text = AssemblyDumper.DumpAssembly(kernel.Assembly);
    kernel.Invoke(OS.GetCmdlineArgs());
  }

  public override void _Process(double delta)
  {
    _timer += delta;
    if (_timer >= 0.7f)
    {
      _timer = 0;
      var usage = GetMemoryUsages();

      _historyWorkingSet.Add(usage.Key);
      _historyManaged.Add(usage.Value);
      if (_historyWorkingSet.Count > MaxHistory)
      {
        _historyWorkingSet.Clear();
        _historyManaged.Clear();
      }

      memorySpecs.Text = $"[color=blue]WorkingSet: {FormatBytes(usage.Key)}[/color] | [color=red]Managed: {FormatBytes(usage.Value)}[/color]";

      graphWorking.Data = [.. _historyWorkingSet];
      graphWorking.MaxValue = (long)(_historyWorkingSet.Max() * 1.1);
      graphWorking.QueueRedraw();
      graphManaged.Data = [.. _historyManaged];
      graphManaged.MaxValue = (long)(_historyManaged.Max() * 1.1);
      graphManaged.QueueRedraw();
    }
  }

  private static KeyValuePair<long, long> GetMemoryUsages()
  {
    return new(
      System.Diagnostics.Process.GetCurrentProcess().WorkingSet64,
      GC.GetTotalMemory(false)
    );
  }

  private static string FormatBytes(long bytes)
  {
    string[] sizes = ["B", "KB", "MB", "GB"];
    double len = bytes;
    int order = 0;
    while (len >= 1024 && order < sizes.Length - 1)
    {
      order++;
      len /= 1024;
    }
    return $"{len:0.##} {sizes[order]}";
  }
}
