using System.Linq;
using System.Reflection;
using System.Text;

namespace YumStudio.DebugKernel.Source;

public static class AssemblyDumper
{
  public static string DumpAssembly(Assembly asm, bool includePrivate = true)
  {
    if (asm == null) return "";
    var sb = new StringBuilder();

    sb.AppendLine($"[Assembly] {asm.FullName}");
    sb.AppendLine($"Location: {asm.Location}");
    sb.AppendLine($"Referenced Assemblies:");
    foreach (var refAsm in asm.GetReferencedAssemblies())
      sb.AppendLine($"  - {refAsm.FullName}");

    sb.AppendLine("\n[Types]");
    foreach (var type in asm.GetTypes().OrderBy(t => t.Namespace).ThenBy(t => t.Name))
    {
      sb.AppendLine($"\n== {type.FullName} ==");

      var flags = includePrivate
          ? BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static
          : BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

      // Fields
      foreach (var field in type.GetFields(flags))
        sb.AppendLine($"  [Field] {field.FieldType.Name} {field.Name}");

      // Properties
      foreach (var prop in type.GetProperties(flags))
        sb.AppendLine($"  [Property] {prop.PropertyType.Name} {prop.Name}");

      // Methods
      foreach (var method in type.GetMethods(flags))
      {
        var parameters = string.Join(", ", method.GetParameters()
                                .Select(p => $"{p.ParameterType.Name} {p.Name}"));
        sb.AppendLine($"  [Method] {method.ReturnType.Name} {method.Name}({parameters})");
      }
    }

    return sb.ToString();
  }
}
