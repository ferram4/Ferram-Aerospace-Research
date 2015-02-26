using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using FerramAerospaceResearch.FARAeroComponents;
using ferram4;

namespace FerramAerospaceResearch
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class FARKSPAddonFlightScene : MonoBehaviour
    {
        private void Start()
        {
            FARAeroStress.LoadStressTemplates();
            FARPartClassification.LoadClassificationTemplates();
            FARAeroUtil.LoadAeroDataFromConfig();

            GameEvents.onVesselGoOffRails.Add(VesselCreate);
            GameEvents.onVesselChange.Add(VesselCreate);
            GameEvents.onVesselCreate.Add(VesselCreate);
            GameEvents.onVesselWasModified.Add(VesselUpdate);
        }

        private void VesselCreate(Vessel v)
        {
            if(v != null)
                if (v.gameObject.GetComponent<FARVesselAero>() == null)
                {
                    FARVesselAero vAero = v.gameObject.AddComponent<FARVesselAero>();
                }
        }
        //This should be a static on FARVesselAero, but KSP doesn't like that
        private void VesselUpdate(Vessel v)
        {
            FARVesselAero _vAero = v.gameObject.GetComponent<FARVesselAero>();
            if (_vAero != null)
                _vAero.VesselUpdate();
        }

        private void OnDestroy()
        {
            GameEvents.onVesselGoOffRails.Remove(VesselCreate);
            GameEvents.onVesselChange.Remove(VesselCreate);
            GameEvents.onVesselCreate.Remove(VesselCreate);
            GameEvents.onVesselWasModified.Remove(VesselUpdate);
        }
    }
}
