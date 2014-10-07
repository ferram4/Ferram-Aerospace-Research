/*
Neophyte's Elementary Aerodynamics Replacement v1.2
Copyright 2014, Michael Ferrara, aka Ferram4

    This file is part of Neophyte's Elementary Aerodynamics Replacement.

    Neophyte's Elementary Aerodynamics Replacement is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Kerbal Joint Reinforcement is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Neophyte's Elementary Aerodynamics Replacement.  If not, see <http://www.gnu.org/licenses/>.

    Serious thanks:		a.g., for tons of bugfixes and code-refactorings
            			Taverius, for correcting a ton of incorrect values
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
            			ialdabaoth (who is awesome), who originally created Module Manager
            			Duxwing, for copy editing the readme
 *
 * Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/55219
 *
 */



using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KSP.IO;
//using Toolbar;

namespace NEAR
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class FARGlobalControlFlightObject : UnityEngine.MonoBehaviour
    {
        //private Dictionary<Vessel, List<FARPartModule>> vesselFARPartModules = new Dictionary<Vessel, List<FARPartModule>>();
        private Vessel lastActiveVessel = null;

        public void Awake()
        {
            if (!CompatibilityChecker.IsAllCompatible())
                return;

            LoadConfigs();
        }

        public void Start()
        {
            if (!CompatibilityChecker.IsAllCompatible())
                return;

            GameEvents.onVesselLoaded.Add(FindPartsWithoutFARModel);
            GameEvents.onVesselGoOffRails.Add(FindPartsWithoutFARModel);
            GameEvents.onVesselWasModified.Add(UpdateFARPartModules);
            GameEvents.onVesselCreate.Add(UpdateFARPartModules);
        }

        
        private void UpdateFARPartModules(Vessel v)
        {
            for (int i = 0; i < v.Parts.Count; i++)
            {
                Part p = v.Parts[i];
                for (int j = 0; j < p.Modules.Count; j++)
                {
                    PartModule m = p.Modules[j];
                    if (m is FARPartModule)
                        (m as FARPartModule).ForceOnVesselPartsChange();
                }
            }
        }

        private void FindPartsWithoutFARModel(Vessel v)
        {
            for (int i = 0; i < v.Parts.Count; i++)
            {
                Part p = v.Parts[i];
                if (p == null)
                    continue;

                string title = p.partInfo.title.ToLowerInvariant();

                if (p.Modules.Contains("FARBasicDragModel"))
                {
                    List<PartModule> modulesToRemove = new List<PartModule>();
                    for (int j = 0; j < p.Modules.Count; j++)
                    {
                        PartModule m = p.Modules[j];
                        if (!(m is FARBasicDragModel))
                            continue;
                        FARBasicDragModel d = m as FARBasicDragModel;
                        if (d.CdCurve == null || d.ClPotentialCurve == null || d.ClViscousCurve == null || d.CmCurve == null)
                        {
                            modulesToRemove.Add(m);
                        }
                    }
                    if (modulesToRemove.Count > 0)
                    {
                        for (int j = 0; j < modulesToRemove.Count; j++)
                        {
                            PartModule m = modulesToRemove[j];
                            p.RemoveModule(m);
                            Debug.Log("Removing Incomplete FAR Drag Module");
                        }
                        if (p.Modules.Contains("FARPayloadFairingModule"))
                            p.RemoveModule(p.Modules["FARPayloadFairingModule"]);
                        if (p.Modules.Contains("FARCargoBayModule"))
                            p.RemoveModule(p.Modules["FARCargoBayModule"]);
                        if (p.Modules.Contains("FARControlSys"))
                            p.RemoveModule(p.Modules["FARControlSys"]);
                    }
                }

                if (p is StrutConnector || p is FuelLine || p is ControlSurface || p is Winglet || FARPartClassification.ExemptPartFromGettingDragModel(p, title))
                    continue;

                if (p.Modules.Contains("ModuleCommand") && !p.Modules.Contains("FARControlSys"))
                {
                    p.AddModule("FARControlSys");
                    PartModule m = p.Modules["FARControlSys"];
                    m.OnStart(PartModule.StartState.Flying);
                    //Debug.Log("Added FARControlSys to " + p.partInfo.title);
                }

                FARPartModule q = p.GetComponent<FARPartModule>();
                if (q != null)
                    continue;

                bool updatedModules = false;

                if (FARPartClassification.PartIsCargoBay(p, title))
                {
                    if (!p.Modules.Contains("FARCargoBayModule"))
                    {
                        p.AddModule("FARCargoBayModule");
                        PartModule m = p.Modules["FARCargoBayModule"];
                        m.OnStart(PartModule.StartState.Flying);

                        FARAeroUtil.AddBasicDragModule(p);
                        m = p.Modules["FARBasicDragModel"];
                        m.OnStart(PartModule.StartState.Flying);

                        updatedModules = true;
                    }
                }
                if (!updatedModules)
                {
                    if (FARPartClassification.PartIsPayloadFairing(p, title))
                    {
                        if (!p.Modules.Contains("FARPayloadFairingModule"))
                        {
                            p.AddModule("FARPayloadFairingModule");
                            PartModule m = p.Modules["FARPayloadFairingModule"];
                            m.OnStart(PartModule.StartState.Flying);

                            FARAeroUtil.AddBasicDragModule(p);
                            m = p.Modules["FARBasicDragModel"];
                            m.OnStart(PartModule.StartState.Flying);
                            updatedModules = true;
                        }
                    }

                    if (!updatedModules && !p.Modules.Contains("FARBasicDragModel"))
                    {
                        FARAeroUtil.AddBasicDragModule(p);
                        PartModule m = p.Modules["FARBasicDragModel"];
                        m.OnStart(PartModule.StartState.Flying);

                        updatedModules = true;
                    }
                }

                //returnValue |= updatedModules;

                FARPartModule b = p.GetComponent<FARPartModule>();
                if (b != null)
                    b.VesselPartList = p.vessel.Parts;             //This prevents every single part in the ship running this due to VesselPartsList not being initialized
            }
            UpdateFARPartModules(v);
        }

        void OnDestroy()
        {
            if (!CompatibilityChecker.IsAllCompatible())
                return;

            //GameEvents.onVesselLoaded.Remove(FindPartsWithoutFARModel);
            GameEvents.onVesselLoaded.Remove(FindPartsWithoutFARModel);
            GameEvents.onVesselGoOffRails.Remove(FindPartsWithoutFARModel);
            GameEvents.onVesselWasModified.Remove(UpdateFARPartModules);
            GameEvents.onVesselCreate.Remove(UpdateFARPartModules);
        }

        public static void LoadConfigs()
        {
            FARPartClassification.LoadClassificationTemplates();
            FARAeroUtil.LoadAeroDataFromConfig();
        }
    }
}
