/*
Neophyte's Elementary Aerodynamics Replacement v1.2.1
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

namespace NEAR
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class FARGlobalControlEditorObject : UnityEngine.MonoBehaviour
    {
        private int count = 0;
        private int part_count_all = -1;
        private int part_count_ship = -1;


        public static bool EditorPartsChanged = true;

        public void Awake()
        {
            if (!CompatibilityChecker.IsAllCompatible())
                return;

            LoadConfigs();
        }

        public void LateUpdate()
        {
            if (!CompatibilityChecker.IsAllCompatible())
                return;

            FARAeroUtil.ResetEditorParts();
            FARBaseAerodynamics.GlobalCoLReady = false;

            if (EditorLogic.fetch)
            {
                if (EditorLogic.startPod != null)
                {
                    var editorShip = FARAeroUtil.AllEditorParts;


                    if (FARAeroUtil.EditorAboutToAttach() && count++ >= 10)
                    {
                        EditorPartsChanged = true;
                        count = 0;
                    }

                    if (part_count_all != editorShip.Count || part_count_ship != EditorLogic.SortedShipList.Count || EditorPartsChanged)
                    {
                        FindPartsWithoutFARModel(editorShip);
                        for (int i = 0; i < editorShip.Count; i++)
                        {
                            Part p = editorShip[i];
                            for (int j = 0; j < p.Modules.Count; j++)
                            {
                                PartModule m = p.Modules[j];
                                if (m is FARBaseAerodynamics)
                                    (m as FARBaseAerodynamics).ClearShielding();
                            }
                        }

                        for (int i = 0; i < editorShip.Count; i++)
                        {
                            Part p = editorShip[i];
                            for (int j = 0; j < p.Modules.Count; j++)
                            {
                                PartModule m = p.Modules[j];
                                if (m is FARPartModule)
                                    (m as FARPartModule).ForceOnVesselPartsChange();
                            }
                        }
                        part_count_all = editorShip.Count;
                        part_count_ship = EditorLogic.SortedShipList.Count;
                        EditorPartsChanged = false;
                    }
                }
            }
        }

        private bool FindPartsWithoutFARModel(List<Part> editorShip)
        {
            bool returnValue = false;

            for (int i = 0; i < editorShip.Count; i++)
            {
                Part p = editorShip[i];

                if (p == null)
                    continue;

                if (p != null && FARAeroUtil.IsNonphysical(p) &&
                    p.physicalSignificance != Part.PhysicalSignificance.NONE)
                {
                    MonoBehaviour.print(p + ": FAR correcting physical significance to fix CoM in editor");
                    p.physicalSignificance = Part.PhysicalSignificance.NONE;
                }

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
            FARPartClassification.LoadClassificationTemplates();
            FARAeroUtil.LoadAeroDataFromConfig();
        }
    }

}
