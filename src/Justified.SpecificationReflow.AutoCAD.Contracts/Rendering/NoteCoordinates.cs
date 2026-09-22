using Justified.SpecificationReflow.AutoCAD.Contracts.Geometry;

namespace Justified.SpecificationReflow.AutoCAD.Contracts.Rendering;

// PRD 7.1：world = anchor + unitScale × (pageOffset + local)。local.x = 栏左 + 行内原点。
public static class NoteCoordinates
{
    public static Point2 World(Point2 anchor, double unitScale, Point2 pageOffset, double columnLeft, double baseline, Point2 relativeOrigin, double baselineOffset)
    {
        return new Point2
        {
            X = anchor.X + unitScale * (pageOffset.X + columnLeft + relativeOrigin.X),
            Y = anchor.Y + unitScale * (pageOffset.Y + baseline + relativeOrigin.Y + baselineOffset)
        };
    }

    // 当前 UCS 的平面原点与绕 Z 的转角。命令在身份 UCS 下应得到与拾取点相同的结果。
    public static Point2 UcsToWcs(Point2 ucsPoint, Point2 originWcs, double rotationRadians)
    {
        var cos = System.Math.Cos(rotationRadians);
        var sin = System.Math.Sin(rotationRadians);
        return new Point2
        {
            X = originWcs.X + ucsPoint.X * cos - ucsPoint.Y * sin,
            Y = originWcs.Y + ucsPoint.X * sin + ucsPoint.Y * cos
        };
    }
}
