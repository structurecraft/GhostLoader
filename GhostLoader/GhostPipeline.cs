using Rhino.Display;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.PlugIns;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GhostLoader
{

    internal sealed class GhostPipeline
    {

        internal static GhostPipeline Instance { get; set; }

        internal ConcurrentDictionary<string, IEnumerable<GeomCache>> DrawingItems { get; }

        BoundingBox _bbox { get; set; }
        private bool enabled { get; set; }

        public bool Enabled
        {
            get => enabled;
            set
            {
                if (enabled != value)
                {
                    enabled = value;

                    if (enabled)
                    {
                        DisplayPipeline.CalculateBoundingBox += CalculateBoundingBox;
                        DisplayPipeline.PostDrawObjects += PostDrawObjects;
                    }
                    else
                    {
                        DisplayPipeline.CalculateBoundingBox -= CalculateBoundingBox;
                        DisplayPipeline.PostDrawObjects -= PostDrawObjects;
                    }
                }
            }
        }

        public GhostPipeline()
        {
            DrawingItems = new ConcurrentDictionary<string, IEnumerable<GeomCache>>();
            Enabled = true;
        }

        DisplayMaterial dMaterial;

        public void PostDrawObjects(object sender, DrawEventArgs e)
        {
            BoundingBox cameraBox = e.Display.Viewport.GetFrustumBoundingBox();

            IEnumerable<GeomCache> cacheItems = DrawingItems.SelectMany(kvp => kvp.Value).OrderBy(c => c.Color.ToArgb()).ToArray();
            var enumer = cacheItems.GetEnumerator();

            while(enumer.MoveNext())
            {
                GeomCache cache = enumer.Current;
                GeometryBase geom = cache.Geometry;
                BoundingBox box = cache.Box;
                Color color = cache.Color;
                Point3d cent = cache.Cent;

                if (!cameraBox.Contains(cent)) continue;

                double dist = e.Viewport.CameraLocation.DistanceTo(cent);
                if (dist > 750_000) continue;

                if (color != dMaterial?.Diffuse)
                {
                    dMaterial = new DisplayMaterial(color);
                }

                if (dist > 250_000)
                {
                    Line line = new Line(box.PointAt(0, 0.5, 0.5), box.PointAt(1, 0.5, 0.5));
                    e.Display.DrawLine(line, color, 1);
                    continue;
                }
                else if (dist > 500_000)
                {
                    e.Display.DrawBox(box, color, 1);
                    continue;
                }

                if (geom is Curve curve)
                {
                    e.Display.DrawCurve(curve, color);
                }
                else if (geom is Brep brep)
                {
                    if (dist > 50_000)
                    {
                        e.Display.DrawBrepWires(brep, color);
                    }
                    else
                    {
                        e.Display.DrawBrepShaded(brep, dMaterial);
                    }
                }
                else if (geom is Mesh mesh)
                {
                    if (dist > 50_000)
                    {
                        e.Display.DrawMeshWires(mesh, color);
                    }
                    else
                    {
                        e.Display.DrawMeshShaded(mesh, dMaterial);
                    }
                }

            }

        }

        public void CalculateBoundingBox(object sender, CalculateBoundingBoxEventArgs e)
        {
            IEnumerable<string> models = DrawingItems.Keys;
            var enumer = models.GetEnumerator();
            while(enumer.MoveNext())
            {
                GenBoxes(enumer.Current);
            }

            e.IncludeBoundingBox(_bbox);
        }

        private void GenBoxes(string document)
        {
            BoundingBox modelBox = _bbox;
            if (!DrawingItems.TryGetValue(document, out IEnumerable<GeomCache> geometry)) return;
            foreach(GeomCache geom in geometry)
            {
                modelBox.Union(geom.Box);
            }

            _bbox = modelBox;
        }

        public async Task Add(string document, IEnumerable<GeomCache> fileObjects)
        {
            if (!DrawingItems.ContainsKey(document))
            {
                await Task.Run(() => DrawingItems.TryAdd(document, fileObjects));
                await Task.Run(() => GenBoxes(document));
            }
        }


    }

}
