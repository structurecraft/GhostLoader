using Rhino.Geometry;
using System.Drawing;
using System.Linq;

namespace GhostLoader
{

    public struct GeomCache
    {

        public Box Box { get; set; }

        public GeometryBase Geometry { get; set; }

        public Color Color { get; set; }

        public Point3d Cent { get; set; }

        public GeomCache(GeometryBase geom, Color color)
        {
            Geometry = geom;
            Color = color;
            Box = geom.GetOptimalBoxFromGeometry();
            Cent = Box.Center;
        }

    }


    public static class GeomCacheExtensions
    {
        private static Box GetOptimalBox(Curve curve)
        {
            Point3d origin = curve.PointAtStart;
            Point3d xPoint = Point3d.Unset;
            Point3d yPoint = Point3d.Unset;

            if (curve.IsClosed)
            {
                
            }
            else
            {
                xPoint = curve.PointAtEnd;
            }

            Point3d[] points = new Point3d[] { origin, xPoint, yPoint };

            Plane.FitPlaneToPoints(points, out Plane plane);
            Box box = new Box(plane, curve);
            return box;
        }

        private static Box GetOptimalBox(LineCurve lineCurve)
        {
            Plane plane = new Plane(lineCurve.PointAtStart, lineCurve.TangentAtStart);
            return new Box(plane, lineCurve);
        }

        private static Box GetOptimalBox(Mesh mesh)
        {
            Plane.FitPlaneToPoints(mesh.Vertices.Select(mv => new Point3d(mv)), out Plane plane);
            return new Box(plane, mesh);
        }

        private static Box GetOptimalBox(Brep brep)
        {
            Plane.FitPlaneToPoints(brep.Vertices.Select(bv => bv.Location), out Plane plane);
            return new Box(plane, brep);
        }

        public static Box GetOptimalBoxFromGeometry(this GeometryBase geom)
            => geom switch
            {
                LineCurve lineCurve => GetOptimalBox(lineCurve),
                Mesh mesh => GetOptimalBox(mesh),
                Brep brep => GetOptimalBox(brep),
                // Curve curve => GetOptimalBox(curve),
                _ => new Box(geom.GetBoundingBox(false))
            };

    }

}
