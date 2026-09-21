namespace Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;

// 坐标内部一律使用毫米与双精度数（PRD 5.1）。
public struct Point2
{
    public double X { get; set; }

    public double Y { get; set; }
}

public struct Bounds2
{
    public double MinX { get; set; }

    public double MinY { get; set; }

    public double MaxX { get; set; }

    public double MaxY { get; set; }
}
