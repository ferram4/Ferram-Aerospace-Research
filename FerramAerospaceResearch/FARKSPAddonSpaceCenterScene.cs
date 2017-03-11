using System;
using UnityEngine;

namespace FerramAerospaceResearch
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    class FARKSPAddonSpaceCenterScene : MonoBehaviour
    {
        void Awake()
        {
            if (FARDebugAndSettings.FARDebugButtonStock)
                FARDebugAndSettings.ForceCloseDebugWindow();
        }
    }
}
