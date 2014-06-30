/*
NEAR: Easymode Aerodynamics Replacement v1.0
Copyright 2014, Michael Ferrara, aka Ferram4

    This file is part of NEAR: Easymode Aerodynamics Replacement.

    NEAR: Easymode Aerodynamics Replacement is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Kerbal Joint Reinforcement is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with NEAR: Easymode Aerodynamics Replacement.  If not, see <http://www.gnu.org/licenses/>.

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
using UnityEngine;
using KSP.IO;
using Toolbar;

namespace NEAR
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class FARGlobalControlEditorObject : UnityEngine.MonoBehaviour
    {
        private int count = 0;
        private int part_count = -1;

        public static bool EditorPartsChanged = false;

        static PluginConfiguration config;


        public void LateUpdate()
        {
            FARAeroUtil.ResetEditorParts();
            FARBaseAerodynamics.GlobalCoLReady = false;

            if (EditorLogic.fetch)
            {
                if (EditorLogic.startPod != null)
                {
                    var editorShip = FARAeroUtil.AllEditorParts;

                    FindPartsWithoutFARModel(editorShip);

                    if (FARAeroUtil.EditorAboutToAttach() && count++ >= 10)
                    {
                        EditorPartsChanged = true;
                        count = 0;
                    }

                    if (part_count != editorShip.Count || EditorPartsChanged)
                    {
                        foreach (Part p in editorShip)
                            foreach (PartModule m in p.Modules)
                                if (m is FARPartModule)
                                    (m as FARPartModule).ForceOnVesselPartsChange();

                        part_count = editorShip.Count;
                        EditorPartsChanged = false;
                    }
                }

            }

        }


        private bool FindPartsWithoutFARModel(List<Part> editorShip)
        {
            bool returnValue = false;
            foreach (Part p in editorShip)
            {
                if(p == null)
                    continue;

                if (p != null && FARAeroUtil.IsNonphysical(p) &&
                    p.physicalSignificance != Part.PhysicalSignificance.NONE)
                {
                    MonoBehaviour.print(p + ": FAR correcting physical significance to fix CoM in editor");
                    p.physicalSignificance = Part.PhysicalSignificance.NONE;
                }

                string title = p.partInfo.title.ToLowerInvariant();

                if (p is StrutConnector || p is FuelLine || p is ControlSurface || p is Winglet || FARPartClassification.ExemptPartFromGettingDragModel(p, title))
                    continue;

                FARPartModule q = p.GetComponent<FARPartModule>();
                if (q != null)
                    continue;

                bool updatedModules = false;

                if (FARPartClassification.PartIsCargoBay(p, title))
                {
                    if (!p.Modules.Contains("FARCargoBayModule"))
                    {
                        p.AddModule("FARCargoBayModule");
                        p.Modules["FARCargoBayModule"].OnStart(PartModule.StartState.Editor);
                        FARAeroUtil.AddBasicDragModule(p);
                        p.Modules["FARBasicDragModel"].OnStart(PartModule.StartState.Editor);
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
                            p.Modules["FARPayloadFairingModule"].OnStart(PartModule.StartState.Editor);
                            FARAeroUtil.AddBasicDragModule(p);
                            p.Modules["FARBasicDragModel"].OnStart(PartModule.StartState.Editor);
                            updatedModules = true;
                        }
                    }

                    if (!updatedModules && !p.Modules.Contains("FARBasicDragModel"))
                    {
                        FARAeroUtil.AddBasicDragModule(p);
                        p.Modules["FARBasicDragModel"].OnStart(PartModule.StartState.Editor);
                        updatedModules = true;
                    }
                }

                returnValue |= updatedModules;

                FARPartModule b = p.GetComponent<FARPartModule>();
                if (b != null)
                    b.VesselPartList = editorShip;             //This prevents every single part in the ship running this due to VesselPartsList not being initialized

               
            }
            return returnValue;
        }

        public static void LoadConfigs()
        {
            config = KSP.IO.PluginConfiguration.CreateForType<FARGlobalControlEditorObject>();
            config.load();

            FARPartClassification.LoadClassificationTemplates();
            FARAeroUtil.LoadAeroDataFromConfig();
        }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class FARGlobalControlFlightObject : UnityEngine.MonoBehaviour
    {
        //private List<Vessel> vesselsWithFARModules = null;
        //private Dictionary<Vessel, List<FARPartModule>> vesselFARPartModules = new Dictionary<Vessel, List<FARPartModule>>();
        static PluginConfiguration config;
        private Vessel lastActiveVessel = null;

        public void Start()
        {
            GameEvents.onVesselGoOffRails.Add(FindPartsWithoutFARModel);
            GameEvents.onVesselWasModified.Add(UpdateFARPartModules);
            GameEvents.onVesselCreate.Add(UpdateFARPartModules);
        }

        private void UpdateFARPartModules(Vessel v)
        {
            foreach (Part p in v.Parts)
                foreach (PartModule m in p.Modules)
                    if (m is FARPartModule)
                        (m as FARPartModule).ForceOnVesselPartsChange();
        }

        private void FindPartsWithoutFARModel(Vessel v)
        {
            List<FARPartModule> FARPartModules = new List<FARPartModule>();

            bool returnValue = false;
            foreach (Part p in v.Parts)
            {
                if (p == null)
                    continue;

                string title = p.partInfo.title.ToLowerInvariant();

                if (p is StrutConnector || p is FuelLine || p is ControlSurface || p is Winglet || FARPartClassification.ExemptPartFromGettingDragModel(p, title))
                    continue;

                if (p.Modules.Contains("FARPartModule"))
                {
                    foreach (PartModule m in p.Modules)
                        if (m is FARPartModule)
                            FARPartModules.Add(m as FARPartModule);
                    continue;
                }

                if (p.Modules.Contains("ModuleCommand") && !p.Modules.Contains("FARControlSys"))
                {
                    p.AddModule("FARControlSys");
                    PartModule m = p.Modules["FARControlSys"];
                    m.OnStart(PartModule.StartState.Flying);

                    FARPartModules.Add(m as FARPartModule);
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
                        FARPartModules.Add(m as FARPartModule);

                        FARAeroUtil.AddBasicDragModule(p);
                        m = p.Modules["FARBasicDragModel"];
                        m.OnStart(PartModule.StartState.Flying);
                        FARPartModules.Add(m as FARPartModule);

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
                            FARPartModules.Add(m as FARPartModule);

                            FARAeroUtil.AddBasicDragModule(p);
                            m = p.Modules["FARBasicDragModel"];
                            m.OnStart(PartModule.StartState.Flying);
                            FARPartModules.Add(m as FARPartModule);
                            updatedModules = true;
                        }
                    }

                    if (!updatedModules && !p.Modules.Contains("FARBasicDragModel"))
                    {
                        FARAeroUtil.AddBasicDragModule(p);
                        PartModule m = p.Modules["FARBasicDragModel"];
                        m.OnStart(PartModule.StartState.Flying);
                        FARPartModules.Add(m as FARPartModule);

                        updatedModules = true;
                    }
                }

                returnValue |= updatedModules;

                FARPartModule b = p.GetComponent<FARPartModule>();
                if (b != null)
                    b.VesselPartList = v.Parts;             //This prevents every single part in the ship running this due to VesselPartsList not being initialized
            }
        }



        void OnDestroy()
        {
            GameEvents.onVesselGoOffRails.Remove(FindPartsWithoutFARModel);
            GameEvents.onVesselWasModified.Remove(UpdateFARPartModules);
            GameEvents.onVesselCreate.Remove(UpdateFARPartModules);
        }

        public static void LoadConfigs()
        {
            config = KSP.IO.PluginConfiguration.CreateForType<FARGlobalControlFlightObject>();
            config.load();

            FARPartClassification.LoadClassificationTemplates();
            FARAeroUtil.LoadAeroDataFromConfig();
        }
    }
}
