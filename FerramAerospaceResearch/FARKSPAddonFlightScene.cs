using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using FerramAerospaceResearch.FARAeroComponents;
using ferram4;

namespace FerramAerospaceResearch
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class FARKSPAddonFlightScene : MonoBehaviour
    {
        Dictionary<Vessel, FARVesselAero> loadedVessels = new Dictionary<Vessel, FARVesselAero>();
        private void Start()
        {
            FARAeroStress.LoadStressTemplates();
            FARPartClassification.LoadClassificationTemplates();
            FARAeroUtil.LoadAeroDataFromConfig();

            GameEvents.onVesselGoOffRails.Add(VesselCreate);
            GameEvents.onVesselChange.Add(VesselCreate);
            GameEvents.onVesselCreate.Add(VesselCreate);
            GameEvents.onVesselWasModified.Add(VesselUpdate);
            GameEvents.onVesselDestroy.Add(VesselRemoveFromActive);
        }

        private void FixedUpdate()
        {
            if(FlightGlobals.ready)
                foreach(KeyValuePair<Vessel, FARVesselAero> pair in loadedVessels)
                {
                    Vessel v = pair.Key;
                    lock(v)
                    {
                        Monitor.Pulse(v);
                    }
                    pair.Value.waitingForUpdate = true;
                }
        }

        private void VesselCreate(Vessel v)
        {
            if (v != null)
            {
                if (v.gameObject.GetComponent<FARVesselAero>() == null)
                {
                    FARVesselAero vAero = v.gameObject.AddComponent<FARVesselAero>();
                    if (!loadedVessels.ContainsKey(v))
                        loadedVessels.Add(v, vAero);
                }
            }
        }
        //This should be a static on FARVesselAero, but KSP doesn't like that
        private void VesselUpdate(Vessel v)
        {
            if (v != null)
            {
                FARVesselAero _vAero;
                if(loadedVessels.TryGetValue(v, out _vAero))
                    if (_vAero != null)
                        _vAero.VesselUpdate();
            }
        }

        private void VesselRemoveFromActive(Vessel v)
        {
            if(v != null)
                loadedVessels.Remove(v);
        }

        private void OnDestroy()
        {
            GameEvents.onVesselGoOffRails.Remove(VesselCreate);
            GameEvents.onVesselChange.Remove(VesselCreate);
            GameEvents.onVesselCreate.Remove(VesselCreate);
            GameEvents.onVesselWasModified.Remove(VesselUpdate);
            GameEvents.onVesselDestroy.Remove(VesselRemoveFromActive);
        }
    }
}
