/*
Ferram Aerospace Research v0.14.5.1
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
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KSP.IO;

namespace ferram4
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class FARGlobalControlEditorObject : UnityEngine.MonoBehaviour
    {
        private int count = 0;
        private FAREditorGUI editorGUI = null;
        private int part_count_all = -1;
        private int part_count_ship = -1;
        private IButton FAREditorButtonBlizzy = null;
        private ApplicationLauncherButton FAREditorButtonStock = null;
        private bool buttonsNeedInitializing = true;
        private Part lastRootPart = null;
        private List<Part> editorShip;


        public static bool EditorPartsChanged = true;

        static PluginConfiguration config;

        public void Awake()
        {
            if (!CompatibilityChecker.IsAllCompatible())
                return;

            GameEvents.onEditorPartEvent.Add(UpdateWingInteractionsEvent);
            buttonsNeedInitializing = true;
            LoadConfigs();
        }

        void OnGUIAppLauncherReady()
        {
            if (ApplicationLauncher.Ready)
            {
                if (EditorDriver.editorFacility == EditorFacility.VAB)
                {
                    FAREditorButtonStock = ApplicationLauncher.Instance.AddModApplication(
                        onAppLaunchToggleOn,
                        onAppLaunchToggleOff,
                        DummyVoid,
                        DummyVoid,
                        DummyVoid,
                        DummyVoid,
                        ApplicationLauncher.AppScenes.VAB,
                        (Texture)GameDatabase.Instance.GetTexture("FerramAerospaceResearch/Textures/icon_button_stock", false));
                }
                else
                {
                    FAREditorButtonStock = ApplicationLauncher.Instance.AddModApplication(
                        onAppLaunchToggleOn,
                        onAppLaunchToggleOff,
                        DummyVoid,
                        DummyVoid,
                        DummyVoid,
                        DummyVoid,
                        ApplicationLauncher.AppScenes.SPH,
                        (Texture)GameDatabase.Instance.GetTexture("FerramAerospaceResearch/Textures/icon_button_stock", false));
                }
            }
        }

        void onAppLaunchToggleOn()
        {
            FAREditorGUI.minimize = false;
        }

        void onAppLaunchToggleOff()
        {
            FAREditorGUI.minimize = true;
        }

        void DummyVoid() { }

        private void HideUI()
        {
            FAREditorGUI.hide = true;
        }

        private void ShowUI()
        {
            FAREditorGUI.hide = false;
        }

        private void UpdateWingInteractionsEvent(ConstructionEventType type, Part pEvent)
        {
            if (type == ConstructionEventType.PartRotating ||
                type == ConstructionEventType.PartOffsetting ||
                type == ConstructionEventType.PartAttached ||
                type == ConstructionEventType.PartDetached)
            {
                if (editorShip == null)
                    return;
                UpdateEditorShipModules();
            }
        }

        private void UpdateWingInteractionsPart(ConstructionEventType type, Part p)
        {
            EditorPartsChanged = true;
            FARWingAerodynamicModel w = p.GetComponent<FARWingAerodynamicModel>();
            if ((object)w != null)
            {
                if (type == ConstructionEventType.PartAttached)
                    w.OnWingAttach();
                else if (type == ConstructionEventType.PartDetached)
                    w.OnWingDetach();
                w.EditorUpdateWingInteractions();
            }
        }

        private void UpdateEditorShipModules()
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

        public void LateUpdate()
        {
            if (!CompatibilityChecker.IsAllCompatible())
                return;

            FARAeroUtil.ResetEditorParts();
            FARBaseAerodynamics.GlobalCoLReady = false;

            if (EditorLogic.fetch)
            {
                if (editorGUI == null)
                {
                    editorGUI = new FAREditorGUI();
                    //editorGUI.LoadGUIParameters();
                    editorGUI.RestartCtrlGUI();
                    GameEvents.onEditorUndo.Add(editorGUI.ResetAll);
                    GameEvents.onEditorRedo.Add(editorGUI.ResetAll);
                }
                if (EditorLogic.RootPart != null)
                {
                    editorShip = FARAeroUtil.AllEditorParts;

                    if (buttonsNeedInitializing)
                        InitializeButtons();

                    /*if (EditorLogic.RootPart != lastRootPart)
                    {
                        lastRootPart = EditorLogic.RootPart;
                        EditorPartsChanged = true;
                    }*/

                    if (FARAeroUtil.EditorAboutToAttach() && count++ >= 20)
                    {
                        EditorPartsChanged = true;
                        count = 0;
                    }

                    if (part_count_all != editorShip.Count || part_count_ship != EditorLogic.SortedShipList.Count || EditorPartsChanged)
                    {
                        UpdateEditorShipModules();
                    }
                }
                else if (!buttonsNeedInitializing)
                    DestroyButtons();
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
                if (q != null && !(q is FARControlSys))
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

        void InitializeButtons()
        {
            if (FARDebugValues.useBlizzyToolbar && FAREditorButtonBlizzy == null)
            {
                FAREditorButtonBlizzy = ToolbarManager.Instance.add("ferram4", "FAREditorButton");
                FAREditorButtonBlizzy.TexturePath = "FerramAerospaceResearch/Textures/icon_button_blizzy";
                FAREditorButtonBlizzy.ToolTip = "FAR Editor Analysis";
                FAREditorButtonBlizzy.OnClick += (e) => FAREditorGUI.minimize = !FAREditorGUI.minimize;
            }
            else if (FAREditorButtonStock == null)
            {
                GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);
                if (ApplicationLauncher.Ready)
                    OnGUIAppLauncherReady();
            }
            

            GameEvents.onShowUI.Add(ShowUI);
            GameEvents.onHideUI.Add(HideUI);

            buttonsNeedInitializing = false;
        }

        void DestroyButtons()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(OnGUIAppLauncherReady);
            GameEvents.onShowUI.Remove(ShowUI);
            GameEvents.onHideUI.Remove(HideUI);

            if (FAREditorButtonStock != null)
                ApplicationLauncher.Instance.RemoveModApplication(FAREditorButtonStock);
            if (FAREditorButtonBlizzy != null)
                FAREditorButtonBlizzy.Destroy();

            buttonsNeedInitializing = true;
        }

        void OnDestroy()
        {
            if (!CompatibilityChecker.IsAllCompatible())
                return;

            GameEvents.onEditorPartEvent.Remove(UpdateWingInteractionsEvent);
            SaveConfigs();
            DestroyButtons();
        }



        public static void LoadConfigs()
        {
            config = KSP.IO.PluginConfiguration.CreateForType<FAREditorGUI>();
            config.load();
            FARDebugValues.displayForces = Convert.ToBoolean(config.GetValue("displayForces", "false"));
            FARDebugValues.displayCoefficients = Convert.ToBoolean(config.GetValue("displayCoefficients", "false"));
            FARDebugValues.displayShielding = Convert.ToBoolean(config.GetValue("displayShielding", "false"));

            FAREditorGUI.windowPos = config.GetValue("windowPos", new Rect());
            FAREditorGUI.minimize = true;
            //FAREditorGUI.minimize = config.GetValue("EditorGUIBool", true);
            if (FAREditorGUI.windowPos.y < 75)
                FAREditorGUI.windowPos.y = 75;


            FARPartClassification.LoadClassificationTemplates();
            FARAeroUtil.LoadAeroDataFromConfig();
        }

        public static void SaveConfigs()
        {
            config.SetValue("windowPos", FAREditorGUI.windowPos);
            //config.SetValue("EditorGUIBool", FAREditorGUI.minimize);
            //print(FARAeroUtil.areaFactor + " " + FARAeroUtil.attachNodeRadiusFactor * 2 + " " + FARAeroUtil.incompressibleRearAttachDrag + " " + FARAeroUtil.sonicRearAdditionalAttachDrag);
            config.save();
        }
    }

}
