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
        const double EVENT_HORIZON = 750_000;
        const double HORIZON = 500_000;
        const double LOW_DETAIL = 250_000;
        const double MEDIUM_DETAIL = 50_000;
        const double HIGH_DETAIL = 25_000;

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

            IEnumerable<GeomCache> cacheItems = DrawingItems.SelectMany(kvp => kvp.Value)  // Put all geometry together
                                                .OrderBy(c => c.Color.ToArgb()) // Optimises for Pipeline
                                                .ToArray();                     // Ensure there is a copy
            
            var enumer = cacheItems.GetEnumerator();
            while(enumer.MoveNext())
            {
                GeomCache cache = enumer.Current;

                // Don't draw anything outside the Camera
                if (!cameraBox.Contains(cache.Cent)) continue;

                double dist = e.Viewport.CameraLocation.DistanceTo(cache.Cent);

                if (cache.Color != dMaterial?.Diffuse)
                {
                    dMaterial = new DisplayMaterial(cache.Color);
                }

                if (dist < HIGH_DETAIL)
                {
                    _DrawHighDetail(e, cache);
                }
                else if (dist < MEDIUM_DETAIL)
                {
                    _DrawMediumDetail(e, cache);
                }
                else if (dist < LOW_DETAIL)
                {
                    _DrawLowDetail(e, cache);
                }
                else if (dist < HORIZON)
                {
                    _DrawHorizonDetail(e, cache);
                }
                else // EVENT_HORIZON
                {
                    _DrawPostHorizonDetail(e, cache);
                }

            }

        }

        private void _DrawHighDetail(DrawEventArgs e, GeomCache cache)
        {
            if (cache.Geometry is Curve curve)
            {
                e.Display.DrawCurve(curve, cache.Color);
            }
            else if (cache.Geometry is Brep brep)
            {
                e.Display.DrawBrepShaded(brep, dMaterial);
            }
            else if (cache.Geometry is Mesh mesh)
            {
                e.Display.DrawMeshShaded(mesh, dMaterial);
            }
        }

        private void _DrawMediumDetail(DrawEventArgs e, GeomCache cache)
        {
            if (cache.Geometry is Curve curve)
            {
                e.Display.DrawCurve(curve, cache.Color);
            }
            else if (cache.Geometry is Brep brep)
            {
                e.Display.DrawBrepWires(brep, cache.Color);
            }
            else if (cache.Geometry is Mesh mesh)
            {
                e.Display.DrawMeshWires(mesh, cache.Color);
            }
        }

        private void _DrawLowDetail(DrawEventArgs e, GeomCache cache)
        {
            Line line = new Line(cache.Box.PointAt(0, 0.5, 0.5), cache.Box.PointAt(1, 0.5, 0.5));
            e.Display.DrawLine(line, cache.Color, 1);
        }

        private void _DrawHorizonDetail(DrawEventArgs e, GeomCache cache)
        {
            // TODO : Box needs to be better aligned
            e.Display.DrawBox(cache.Box, cache.Color, 1);
        }

        private void _DrawPostHorizonDetail(DrawEventArgs e, GeomCache cache)
        {

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
                modelBox.Union(geom.Box.BoundingBox);
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
