using Eto.Forms;
using Rhino;
using Rhino.Commands;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GhostLoader
{

    public sealed class OpenModelCommand : Rhino.Commands.Command
    {

        public OpenModelCommand()
        {
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static OpenModelCommand Instance { get; private set; }

        /// <inheritdoc/>
        public override string EnglishName => "OpenModel";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            if (null == GhostPipeline.Instance)
            {
                GhostPipeline.Instance = new GhostPipeline();
            }

            IEnumerable<string> models = FindModels();
            Task.Run(() => Task.WhenAll(GetLoadingTasks(models)));

            return Result.Success;
        }

        internal IEnumerable<string> FindModels()
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                CheckFileExists = true,
                // CurrentFilter = "*.3dm",
                MultiSelect = true,
                Title = "Choose File(s)",
            };

            DialogResult result = ofd.ShowDialog(Rhino.UI.RhinoEtoApp.MainWindow);
            if (DialogResult.Yes == result || DialogResult.Ok == result)
            {
                return ofd.Filenames;
            }
            else
            {
                return Array.Empty<string>();
            }
        }

        internal async Task LoadModel(string filepath)
        {
            string fileName = Path.GetFileNameWithoutExtension(filepath);
            File3dm file = File3dm.Read(filepath,
                                        File3dm.TableTypeFilter.ObjectTable,
                                        File3dm.ObjectTypeFilter.Any);

            IEnumerable<GeomCache> cache = GetObjects(file);
            await GhostPipeline.Instance.Add(fileName, cache.ToArray());
        }

        internal IEnumerable<GeomCache> GetObjects(File3dm file)
        {
            var fileObjects = file.Objects;
            var fileEnumer = fileObjects.GetEnumerator();
            while (fileEnumer.MoveNext())
            {
                var curr = fileEnumer.Current;
                if (curr.Attributes.Space == Rhino.DocObjects.ActiveSpace.PageSpace) continue;
                if (curr.Geometry is TextEntity) continue;

                var geom = new GeomCache(curr.Geometry, curr.Attributes.ObjectColor);
                if (null == geom.Geometry) continue;

                yield return geom;
            }
        }

        private IEnumerable<Task> GetLoadingTasks(IEnumerable<string> models)
        {
            // TO DO: Parallelize Load Call.
            /*
            Parallel.ForEachAsync(models,
                (string model) => LoadModel(model)
            );
            */

            var enumer = models.GetEnumerator();
            while(enumer.MoveNext())
            {
                string currentModel = enumer.Current;
                yield return LoadModel(currentModel);
            }
        }

    }

}
