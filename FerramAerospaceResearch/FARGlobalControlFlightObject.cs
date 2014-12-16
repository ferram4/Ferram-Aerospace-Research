/*
Ferram Aerospace Research v0.14.4.1
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
//using Toolbar;

namespace ferram4
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class FARGlobalControlFlightObject : UnityEngine.MonoBehaviour
    {
        private IButton FARFlightButtonBlizzy = null;
        private ApplicationLauncherButton FARFlightButtonStock;
        //private Dictionary<Vessel, List<FARPartModule>> vesselFARPartModules = new Dictionary<Vessel, List<FARPartModule>>();
        static PluginConfiguration config;

        public void Awake()
        {
            if (!CompatibilityChecker.IsAllCompatible())
                return;

            LoadConfigs();
            if (FARDebugValues.useBlizzyToolbar)
            {
                FARFlightButtonBlizzy = ToolbarManager.Instance.add("ferram4", "FAREditorButton");
                FARFlightButtonBlizzy.TexturePath = "FerramAerospaceResearch/Textures/icon_button_blizzy";
                FARFlightButtonBlizzy.ToolTip = "FAR Flight Systems";
                FARFlightButtonBlizzy.OnClick += (e) => FARControlSys.minimize = !FARControlSys.minimize;
            }
            else
                GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);

            InputLockManager.RemoveControlLock("FAREdLock");
        }

        void OnGUIAppLauncherReady()
        {
            if (ApplicationLauncher.Ready)
            {
                FARFlightButtonStock = ApplicationLauncher.Instance.AddModApplication(
                    onAppLaunchToggleOn,
                    onAppLaunchToggleOff,
                    DummyVoid,
                    DummyVoid,
                    DummyVoid,
                    DummyVoid,
                    ApplicationLauncher.AppScenes.ALWAYS,
                    (Texture)GameDatabase.Instance.GetTexture("FerramAerospaceResearch/Textures/icon_button_stock", false));
            }
        }

        void onAppLaunchToggleOn()
        {
            FARControlSys.minimize = false;
        }

        void onAppLaunchToggleOff()
        {
            FARControlSys.minimize = true;
        }

        void DummyVoid() { }

        public void Start()
        {
            if (!CompatibilityChecker.IsAllCompatible())
                return;

            GameEvents.onVesselLoaded.Add(FindPartsWithoutFARModel);
            GameEvents.onVesselGoOffRails.Add(FindPartsWithoutFARModel);
            GameEvents.onVesselWasModified.Add(UpdateFARPartModules);
            GameEvents.onVesselCreate.Add(UpdateFARPartModules);
            GameEvents.onVesselChange.Add(ChangeControlSys);
            GameEvents.onShowUI.Add(ShowUI);
            GameEvents.onHideUI.Add(HideUI);
        }

        private void HideUI()
        {
            FARControlSys.hide = true;
        }

        private void ShowUI()
        {
            FARControlSys.hide = false;
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
                if (q != null && !(q is FARControlSys))
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

        private void ChangeControlSys(Vessel v)
        {
            FARControlSys.SetActiveControlSys(v);
        }

        public void LateUpdate()
        {
            if (!CompatibilityChecker.IsAllCompatible())
                return;

            if (FlightGlobals.ready)
            {
                //FARFlightButton = FARControlSys.ActiveControlSys && (FARControlSys.ActiveControlSys.vessel == FlightGlobals.ActiveVessel);

                if (FARControlSys.ActiveControlSys == null)
                {
                    FARControlSys.SetActiveControlSys(FlightGlobals.ActiveVessel);
                }
            }
        }

        void OnDestroy()
        {
            if (!CompatibilityChecker.IsAllCompatible())
                return;

            if (config != null)
                SaveConfigs();

            GameEvents.onGUIApplicationLauncherReady.Remove(OnGUIAppLauncherReady);
            if (FARFlightButtonStock != null)
                ApplicationLauncher.Instance.RemoveModApplication(FARFlightButtonStock);

            if (FARFlightButtonBlizzy != null)
                FARFlightButtonBlizzy.Destroy();

            //GameEvents.onVesselLoaded.Remove(FindPartsWithoutFARModel);
            GameEvents.onVesselLoaded.Remove(FindPartsWithoutFARModel);
            GameEvents.onVesselGoOffRails.Remove(FindPartsWithoutFARModel);
            GameEvents.onVesselWasModified.Remove(UpdateFARPartModules);
            GameEvents.onVesselCreate.Remove(UpdateFARPartModules);
            GameEvents.onVesselChange.Remove(ChangeControlSys);
            GameEvents.onShowUI.Remove(ShowUI);
            GameEvents.onHideUI.Remove(HideUI);
        }

        public static void LoadConfigs()
        {
            config = KSP.IO.PluginConfiguration.CreateForType<FAREditorGUI>();
            config.load();
            FARControlSys.windowPos = config.GetValue("FlightWindowPos", new Rect(100, 100, 150, 100));
            FARControlSys.AutopilotWinPos = config.GetValue("AutopilotWinPos", new Rect());
            FARControlSys.HelpWindowPos = config.GetValue("HelpWindowPos", new Rect());
            FARControlSys.FlightDataPos = config.GetValue("FlightDataPos", new Rect());
            FARControlSys.FlightDataHelpPos = config.GetValue("FlightDataHelpPos", new Rect());
            FARControlSys.AirSpeedPos = config.GetValue("AirSpeedPos", new Rect());
            FARControlSys.AirSpeedHelpPos = config.GetValue("AirSpeedHelpPos", new Rect());
            FARControlSys.AeroForceTintingPos = config.GetValue("AeroForceTintingPos", new Rect());
            //FARControlSys.minimize = config.GetValue<bool>("FlightGUIBool", false);
            FARControlSys.k_wingleveler_str = config.GetValue("k_wingleveler", "0.05");
            FARControlSys.k_wingleveler = Convert.ToDouble(FARControlSys.k_wingleveler_str);
            FARControlSys.kd_wingleveler_str = config.GetValue("kd_wingleveler", "0.002");
            FARControlSys.kd_wingleveler = Convert.ToDouble(FARControlSys.kd_wingleveler_str);
            FARControlSys.k_yawdamper_str = config.GetValue("k_yawdamper", "0.1");
            FARControlSys.k_yawdamper = Convert.ToDouble(FARControlSys.k_yawdamper_str);
            FARControlSys.k_pitchdamper_str = config.GetValue("k_pitchdamper", "0.25");
            FARControlSys.k_pitchdamper = Convert.ToDouble(FARControlSys.k_pitchdamper_str);
			FARControlSys.k2_pitchdamper_str = config.GetValue("k2_pitchdamper", "0.06");
			FARControlSys.k2_pitchdamper = Convert.ToDouble(FARControlSys.k2_pitchdamper_str);
            FARControlSys.scaleVelocity_str = config.GetValue("scaleVelocity", "150");
            FARControlSys.scaleVelocity = Convert.ToDouble(FARControlSys.scaleVelocity_str);
            FARControlSys.alt_str = config.GetValue("alt", "0");
            FARControlSys.alt = Convert.ToDouble(FARControlSys.alt_str);
            FARControlSys.upperLim_str = config.GetValue("upperLim", "25");
            FARControlSys.upperLim = Convert.ToDouble(FARControlSys.upperLim_str);
            FARControlSys.lowerLim_str = config.GetValue("lowerLim", "-25");
            FARControlSys.lowerLim = Convert.ToDouble(FARControlSys.lowerLim_str);
            FARControlSys.k_limiter_str = config.GetValue("k_limiter", "0.25");
            FARControlSys.k_limiter = Convert.ToDouble(FARControlSys.k_limiter_str);

            FARControlSys.unitMode = (FARControlSys.SurfaceVelUnit)config.GetValue("unitMode", 0);
            FARControlSys.velMode = (FARControlSys.SurfaceVelMode)config.GetValue("velMode", 0);

            FARDebugValues.displayForces = Convert.ToBoolean(config.GetValue("displayForces", "false"));
            FARDebugValues.displayCoefficients = Convert.ToBoolean(config.GetValue("displayCoefficients", "false"));
            FARDebugValues.displayShielding = Convert.ToBoolean(config.GetValue("displayShielding", "false"));
            FARDebugValues.useSplinesForSupersonicMath = Convert.ToBoolean(config.GetValue("useSplinesForSupersonicMath", "true"));
            FARDebugValues.allowStructuralFailures = Convert.ToBoolean(config.GetValue("allowStructuralFailures", "true"));

            FARAeroStress.LoadStressTemplates();
            FARPartClassification.LoadClassificationTemplates();
            FARAeroUtil.LoadAeroDataFromConfig();
        }

        public static void SaveConfigs()
        {
            config.SetValue("FlightWindowPos", FARControlSys.windowPos);
            config.SetValue("AutopilotWinPos", FARControlSys.AutopilotWinPos);
            config.SetValue("HelpWindowPos", FARControlSys.HelpWindowPos);
            config.SetValue("FlightDataPos", FARControlSys.FlightDataPos);
            config.SetValue("FlightDataHelpPos", FARControlSys.FlightDataHelpPos);
            config.SetValue("AirSpeedPos", FARControlSys.AirSpeedPos);
            config.SetValue("AirSpeedHelpPos", FARControlSys.AirSpeedHelpPos);
            config.SetValue("AeroForceTintingPos", FARControlSys.AeroForceTintingPos);
            //config.SetValue("FlightGUIBool", FARControlSys.minimize);
            config.SetValue("k_wingleveler", (FARControlSys.k_wingleveler).ToString());
            config.SetValue("kd_wingleveler", (FARControlSys.kd_wingleveler).ToString());
            config.SetValue("k_yawdamper", (FARControlSys.k_yawdamper).ToString());
			config.SetValue("k_pitchdamper", (FARControlSys.k_pitchdamper).ToString());
			config.SetValue("k2_pitchdamper", (FARControlSys.k2_pitchdamper).ToString());
            config.SetValue("scaleVelocity", (FARControlSys.scaleVelocity).ToString());
            config.SetValue("alt", (FARControlSys.alt).ToString());
            config.SetValue("upperLim", (FARControlSys.upperLim).ToString());
            config.SetValue("lowerLim", (FARControlSys.lowerLim).ToString());
            config.SetValue("k_limiter", (FARControlSys.k_limiter).ToString());

            config.SetValue("unitMode", (int)FARControlSys.unitMode);
            config.SetValue("velMode", (int)FARControlSys.velMode); 
            
            config.save();
        }
    }
}
