using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FerramAerospaceResearch
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    class FARKSPAddonSpaceCenterScene
    {
        void Awake()
        {
            if (FARDebugAndSettings.FARDebugButtonStock)
                FARDebugAndSettings.ForceCloseDebugWindow();
        }
    }
}
