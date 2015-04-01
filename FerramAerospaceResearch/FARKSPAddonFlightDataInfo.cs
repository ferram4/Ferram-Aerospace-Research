using System;
using UnityEngine;
using ferram4;

namespace FerramAerospaceResearch
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class FARKSPAddonFlightDataInfo : MonoBehaviour
    {
        void Start()
        {
            FARAeroStress.LoadStressTemplates();
            FARPartClassification.LoadClassificationTemplates();
            FARAeroUtil.LoadAeroDataFromConfig();
            FARDebugOptions.LoadConfigs();
            //GameObject.Destroy(this.gameObject);
        }
    }
}
