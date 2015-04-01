/*
Ferram Aerospace Research v0.14.6
Copyright 2014, Michael Ferrara, aka Ferram4

    This file is part of Ferram Aerospace Research.

    Ferram Aerospace Research is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Ferram Aerospace Research is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Ferram Aerospace Research.  If not, see <http://www.gnu.org/licenses/>.

    Serious thanks:		a.g., for tons of bugfixes and code-refactorings
            			Taverius, for correcting a ton of incorrect values
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
            			ialdabaoth (who is awesome), who originally created Module Manager
                        Regex, for adding RPM support
            			Duxwing, for copy editing the readme
 * 
 * Kerbal Engineer Redux created by Cybutek, Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License
 *      Referenced for starting point for fixing the "editor click-through-GUI" bug
 *
 * Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/55219
 *
 * Toolbar integration powered by blizzy78's Toolbar plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/60863
 */

using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using FerramAerospaceResearch.FARAeroComponents;
using ferram4;

namespace FerramAerospaceResearch
{
    //[KSPAddon(KSPAddon.Startup.Flight, false)]
    public class FARKSPAddonFlightScene : MonoBehaviour
    {
        private static FARKSPAddonFlightScene instance = null;
        public static FARKSPAddonFlightScene fetch
        {
            get { return instance; }
        }

        Dictionary<Vessel, FARVesselAero> loadedVessels = new Dictionary<Vessel, FARVesselAero>();

        private void Awake()
        {
            this.enabled = true;
        }

        private void Start()
        {
            instance = this;

            GameEvents.onVesselGoOffRails.Add(VesselCreate);
            GameEvents.onVesselChange.Add(VesselCreate);
            GameEvents.onVesselLoaded.Add(VesselCreate);
            GameEvents.onVesselCreate.Add(VesselCreate);
            GameEvents.onVesselWasModified.Add(VesselUpdate);
            GameEvents.onVesselDestroy.Add(VesselRemoveFromActive);
        }
        private void FixedUpdate()
        {
            if(HighLogic.LoadedSceneIsFlight && FlightGlobals.ready)
                foreach(KeyValuePair<Vessel, FARVesselAero> pair in loadedVessels)
                {
                    Vessel v = pair.Key;
                    FARVesselAero aero = pair.Value;
                    if (aero.ready)
                    {
                        aero.waitingForUpdate = true;
                        lock (aero)
                        {
                            Monitor.Pulse(aero);
                        }
                    }
                }
        }

        private void VesselCreate(Vessel v)
        {
            if (v != null)
            {
                FARVesselAero vAero = v.gameObject.GetComponent<FARVesselAero>();
                if (vAero == null)
                {
                    vAero = v.gameObject.AddComponent<FARVesselAero>();
                }
                if (!loadedVessels.ContainsKey(v))
                    loadedVessels.Add(v, vAero);
                else
                    loadedVessels[v] = vAero;
                vAero.VesselUpdate();
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
            GameEvents.onVesselLoaded.Remove(VesselCreate);
            GameEvents.onVesselCreate.Remove(VesselCreate);
            GameEvents.onVesselWasModified.Remove(VesselUpdate);
            GameEvents.onVesselDestroy.Remove(VesselRemoveFromActive);
        }
    }
}
