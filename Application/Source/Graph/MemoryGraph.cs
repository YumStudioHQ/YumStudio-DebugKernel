using Godot;
using System.Collections.Generic;

namespace YumStudio.DebugKernel.Source.Graph;

[GlobalClass]
public partial class MemoryGraph : Control
{
  public List<long> Data { get; set; } = [];
  public Color LineColor { get; set; } = new Color(0, 1, 0);
  public long MaxValue { get; set; } = 1;

  public override void _Draw()
  {
    if (Data.Count < 2) return;

    float stepX = Size.X / (Data.Count - 1);

    for (int i = 1; i < Data.Count; i++)
    {
      float x1 = (i - 1) * stepX;
      float y1 = Size.Y - (float)Data[i - 1] / MaxValue * Size.Y;
      float x2 = i * stepX;
      float y2 = Size.Y - (float)Data[i] / MaxValue * Size.Y;

      DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), LineColor, 4);
    }
  }
}
