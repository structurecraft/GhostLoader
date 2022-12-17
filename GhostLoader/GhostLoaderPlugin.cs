using Rhino;
using System;

namespace GhostLoader
{

    public class GhostLoaderPlugin : Rhino.PlugIns.PlugIn
    {
        public GhostLoaderPlugin()
        {
            Instance = this;
        }

        ///<summary>Gets the only instance of the GhostLoaderPlugin plug-in.</summary>
        public static GhostLoaderPlugin Instance { get; private set; }

    }
}