/*
Ferram Aerospace Research v0.15.5.3 "von Helmholtz"
=========================
Aerodynamics model for Kerbal Space Program

Copyright 2015, Michael Ferrara, aka Ferram4

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
				stupid_chris, for the RealChuteLite implementation
            			Taverius, for correcting a ton of incorrect values  
				Tetryds, for finding lots of bugs and issues and not letting me get away with them, and work on example crafts
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager updates  
            			ialdabaoth (who is awesome), who originally created Module Manager  
                        	Regex, for adding RPM support  
				DaMichel, for some ferramGraph updates and some control surface-related features  
            			Duxwing, for copy editing the readme  
   
   CompatibilityChecker by Majiir, BSD 2-clause http://opensource.org/licenses/BSD-2-Clause

   Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission  
	http://forum.kerbalspaceprogram.com/threads/55219

   ModularFLightIntegrator by Sarbian, Starwaster and Ferram4, MIT: http://opensource.org/licenses/MIT
	http://forum.kerbalspaceprogram.com/threads/118088

   Toolbar integration powered by blizzy78's Toolbar plugin; used with permission  
	http://forum.kerbalspaceprogram.com/threads/60863
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using UnityEngine;
using PreFlightTests;
using FerramAerospaceResearch.FARAeroComponents;
using FerramAerospaceResearch.FARPartGeometry;
using FerramAerospaceResearch.FARGUI.FAREditorGUI.Simulation;
using FerramAerospaceResearch.FARGUI.FAREditorGUI.DesignConcerns;
using ferram4;
using Debug = UnityEngine.Debug;

namespace FerramAerospaceResearch.FARGUI.FAREditorGUI
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class EditorGUI : MonoBehaviour
    {
        static EditorGUI instance;
        public static EditorGUI Instance
        {
            get { return instance; }
        }

        int _updateRateLimiter = 0;
        bool _updateQueued = true;

        static bool showGUI = false;
        bool useKSPSkin = true;
        Rect guiRect;
        public static Rect GUIRect
        {
            get { return instance.guiRect; }
        }
        static ApplicationLauncherButton editorGUIAppLauncherButton;
        static IButton blizzyEditorGUIButton;

        VehicleAerodynamics _vehicleAero;
        List<GeometryPartModule> _currentGeometryModules = new List<GeometryPartModule>();
        List<FARWingAerodynamicModel> _wingAerodynamicModel = new List<FARWingAerodynamicModel>();
        Stopwatch voxelWatch = new Stopwatch();

        int prevPartCount = 0;
        bool partMovement = false;

        EditorSimManager _simManager;

        InstantConditionSim _instantSim;
        EditorAreaRulingOverlay _areaRulingOverlay;
        StaticAnalysisGraphGUI _editorGraph;
        StabilityDerivGUI _stabDeriv;
        StabilityDerivSimulationGUI _stabDerivLinSim;

        List<IDesignConcern> _customDesignConcerns = new List<IDesignConcern>();

        MethodInfo editorReportUpdate;

        bool gearToggle = false;
        bool showAoAArrow = true;

        ArrowPointer velocityArrow = null;
        Transform arrowTransform = null;

        GUIDropDown<FAREditorMode> modeDropdown;
        FAREditorMode currentMode = FAREditorMode.STATIC;
        private enum FAREditorMode
        {
            STATIC,
            STABILITY,
            SIMULATION,
            AREA_RULING
        }

        private static string[] FAReditorMode_str = 
        {
            "Static Analysis",
            "Data + Stability Derivatives",
            "Stability Deriv Simulation",
            "Transonic Design"
        };

        void Start()
        {
            if (CompatibilityChecker.IsAllCompatible() && instance == null)
                instance = this;
            else
            {
                GameObject.Destroy(this);
                return;
            }

            _vehicleAero = new VehicleAerodynamics();

            guiRect = new Rect(Screen.width / 4, Screen.height / 6, 10, 10);

            _instantSim = new InstantConditionSim();
            GUIDropDown<int> flapSettingDropDown = new GUIDropDown<int>(new string[] { "0 (up)", "1 (init climb)", "2 (takeoff)", "3 (landing)" }, new int[] { 0, 1, 2, 3 }, 0);
            GUIDropDown<CelestialBody> celestialBodyDropdown = CreateBodyDropdown();

            modeDropdown = new GUIDropDown<FAREditorMode>(FAReditorMode_str, new FAREditorMode[] {FAREditorMode.STATIC, FAREditorMode.STABILITY, FAREditorMode.SIMULATION, FAREditorMode.AREA_RULING});

            _simManager = new EditorSimManager(_instantSim);

            _editorGraph = new StaticAnalysisGraphGUI(_simManager, flapSettingDropDown, celestialBodyDropdown);
            _stabDeriv = new StabilityDerivGUI(_simManager, flapSettingDropDown, celestialBodyDropdown);
            _stabDerivLinSim = new StabilityDerivSimulationGUI(_simManager);

            Color crossSection = GUIColors.GetColor(3);
            crossSection.a = 0.8f;

            Color crossSectionDeriv = GUIColors.GetColor(2);
            crossSectionDeriv.a = 0.8f;

            _areaRulingOverlay = new EditorAreaRulingOverlay(new Color(0.05f, 0.05f, 0.05f, 0.7f), crossSection, crossSectionDeriv, 10, 5);
            guiRect.height = 500;
            guiRect.width = 650;

            GameEvents.onEditorPartEvent.Add(UpdateGeometryEvent);
            GameEvents.onEditorUndo.Add(ResetEditorEvent);
            GameEvents.onEditorRedo.Add(ResetEditorEvent);
            GameEvents.onEditorShipModified.Add(ResetEditorEvent);
            GameEvents.onEditorLoad.Add(ResetEditorEvent);

            GameEvents.onGUIEngineersReportReady.Add(AddDesignConcerns);
            GameEvents.onGUIEngineersReportDestroy.Add(RemoveDesignConcerns);

            RequestUpdateVoxel();
        }

        void AddDesignConcerns()
        {
            editorReportUpdate = EngineersReport.Instance.GetType().GetMethod("OnCraftModified", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            _customDesignConcerns.Add(new AreaRulingConcern(_vehicleAero));
            //_customDesignConcerns.Add(new AeroStabilityConcern(_instantSim, EditorDriver.editorFacility == EditorFacility.SPH ? EditorFacilities.SPH : EditorFacilities.VAB));
            for (int i = 0; i < _customDesignConcerns.Count; i++)
                EngineersReport.Instance.AddTest(_customDesignConcerns[i]);
        }

        void RemoveDesignConcerns()
        {
            for (int i = 0; i < _customDesignConcerns.Count; i++)
                EngineersReport.Instance.RemoveTest(_customDesignConcerns[i]);
        }

        void OnDestroy()
        {
            GameEvents.onEditorPartEvent.Remove(UpdateGeometryEvent);
            GameEvents.onEditorUndo.Remove(ResetEditorEvent);
            GameEvents.onEditorRedo.Remove(ResetEditorEvent);
            GameEvents.onEditorShipModified.Remove(ResetEditorEvent);
            GameEvents.onEditorLoad.Remove(ResetEditorEvent);

            GameEvents.onGUIEngineersReportReady.Remove(AddDesignConcerns);
            GameEvents.onGUIEngineersReportDestroy.Remove(AddDesignConcerns);

            EditorLogic.fetch.Unlock("FAREdLock");

            if (blizzyEditorGUIButton != null)
            {
                blizzyEditorGUIButton.Destroy();
                blizzyEditorGUIButton = null;
            }

            _stabDerivLinSim = null;
            _instantSim = null;
            _areaRulingOverlay = null;
            _editorGraph = null;
            _stabDeriv = null;

            if(_vehicleAero != null)
                _vehicleAero.ForceCleanup();
            _vehicleAero = null;
        }

        #region EditorEvents
        private void ResetEditorEvent(ShipConstruct construct)
        {
            if (EditorLogic.RootPart != null)
            {
                List<Part> partsList = EditorLogic.SortedShipList;
                for (int i = 0; i < partsList.Count; i++)
                    UpdateGeometryModule(partsList[i]);
                
            }

            RequestUpdateVoxel();
        }
        private void ResetEditorEvent(ShipConstruct construct, CraftBrowser.LoadType type)
        {
            ResetEditor();
        }

        public static void ResetEditor()
        {
            Color crossSection = GUIColors.GetColor(3);
            crossSection.a = 0.8f;

            Color crossSectionDeriv = GUIColors.GetColor(2);
            crossSectionDeriv.a = 0.8f;
            instance._areaRulingOverlay.RestartOverlay();
            //instance._areaRulingOverlay = new EditorAreaRulingOverlay(new Color(0.05f, 0.05f, 0.05f, 0.7f), crossSection, crossSectionDeriv, 10, 5);
            RequestUpdateVoxel();
        }
       
        private void UpdateGeometryEvent(ConstructionEventType type, Part pEvent)
        {
            if (type == ConstructionEventType.PartRotated ||
            type == ConstructionEventType.PartOffset ||
            type == ConstructionEventType.PartAttached ||
            type == ConstructionEventType.PartDetached ||
            type == ConstructionEventType.PartRootSelected ||
                type == ConstructionEventType.Unknown)
            {
                if (EditorLogic.SortedShipList.Count > 0)
                    UpdateGeometryModule(type, pEvent);
                RequestUpdateVoxel();

                if (type != ConstructionEventType.Unknown)
                    partMovement = true;
            }
        }

        private void UpdateGeometryModule(ConstructionEventType type, Part p)
        {
            GeometryPartModule g = p.GetComponent<GeometryPartModule>();
            if (g != null && g.Ready)
            {
                if (type == ConstructionEventType.Unknown)
                    g.RebuildAllMeshData();
                else
                    g.EditorUpdate();
            }
        }

        private void UpdateGeometryModule(Part p)
        {
            GeometryPartModule g = p.GetComponent<GeometryPartModule>();
            if (g != null && g.Ready)
            {
                g.EditorUpdate();
            }
        }


        private void LEGACY_UpdateWingAeroModels(bool updateWingInteractions)
        {
            List<Part> partsList = EditorLogic.SortedShipList;
            _wingAerodynamicModel.Clear();
            for (int i = 0; i < partsList.Count; i++)
            {
                Part p = partsList[i];
                if(p != null)
                    if (p.Modules.Contains("FARWingAerodynamicModel"))
                    {
                        FARWingAerodynamicModel w = (FARWingAerodynamicModel)p.Modules["FARWingAerodynamicModel"];
                        if(updateWingInteractions)
                            w.EditorUpdateWingInteractions();
                        _wingAerodynamicModel.Add(w);
                    }
                    else if (p.Modules.Contains("FARControllableSurface"))
                    {
                        FARControllableSurface c = (FARControllableSurface)p.Modules["FARControllableSurface"];
                        if(updateWingInteractions)
                            c.EditorUpdateWingInteractions();
                        _wingAerodynamicModel.Add(c);
                    }
            }

        }
        #endregion

        void FixedUpdate()
        {
            if (EditorLogic.RootPart != null)
            {
                if (_vehicleAero.CalculationCompleted)
                {
                    _vehicleAero.UpdateSonicDragArea();
                    LEGACY_UpdateWingAeroModels(EditorLogic.SortedShipList.Count != prevPartCount || partMovement);
                    prevPartCount = EditorLogic.SortedShipList.Count;

                    voxelWatch.Stop();
                    UnityEngine.Debug.Log("Voxelization Time (ms): " + voxelWatch.ElapsedMilliseconds);

                    voxelWatch.Reset();

                    _simManager.UpdateAeroData(_vehicleAero, _wingAerodynamicModel);
                    UpdateCrossSections();
                    editorReportUpdate.Invoke(EngineersReport.Instance, null);
                }

                if (_updateRateLimiter < FARSettingsScenarioModule.VoxelSettings.minPhysTicksPerUpdate)
                {
                    _updateRateLimiter++;
                }
                else if (_updateQueued)
                {
                    Debug.Log("Updating " + EditorLogic.fetch.ship.shipName);
                    RecalculateVoxel();
                }
            }
            else
            {
                _updateQueued = true;
                _updateRateLimiter = FARSettingsScenarioModule.VoxelSettings.minPhysTicksPerUpdate - 2;
            }

            if (FARDebugValues.useBlizzyToolbar)
                GenerateBlizzyToolbarButton();
            else
                OnGUIAppLauncherReady();
        }

        #region voxel
        public static void RequestUpdateVoxel()
        {
            if (instance._updateRateLimiter > FARSettingsScenarioModule.VoxelSettings.minPhysTicksPerUpdate)
                instance._updateRateLimiter = FARSettingsScenarioModule.VoxelSettings.minPhysTicksPerUpdate - 2;
            instance._updateQueued = true;
            //instance._areaRulingOverlay.SetVisibility(false);

        }

        void RecalculateVoxel()
        {
            if (_updateRateLimiter < FARSettingsScenarioModule.VoxelSettings.minPhysTicksPerUpdate)        //this has been updated recently in the past; queue an update and return
            {
                _updateQueued = true;
                return;
            }
            else                                //last update was far enough in the past to run; reset rate limit counter and clear the queued flag
            {
                _updateRateLimiter = 0;
                _updateQueued = false;
            }
            List<Part> partList = EditorLogic.SortedShipList;

            _currentGeometryModules.Clear();

            for (int i = 0; i < partList.Count; i++)
            {
                Part p = partList[i];
                if (p.Modules.Contains("GeometryPartModule"))
                {
                    GeometryPartModule g = (GeometryPartModule)p.Modules["GeometryPartModule"];
                    if (g != null)
                    {
                        if (g.Ready)
                            _currentGeometryModules.Add(g);
                        else
                        {
                            _updateRateLimiter = FARSettingsScenarioModule.VoxelSettings.minPhysTicksPerUpdate - 2;
                            _updateQueued = true;
                            //Debug.Log("We're not ready!");
                            return;
                        }
                    }
                }
            }
            TriggerIGeometryUpdaters();


            if (_currentGeometryModules.Count > 0)
            {
                voxelWatch.Start();
                if (!_vehicleAero.TryVoxelUpdate(EditorLogic.RootPart.partTransform.worldToLocalMatrix, EditorLogic.RootPart.partTransform.localToWorldMatrix, FARSettingsScenarioModule.VoxelSettings.numVoxelsControllableVessel, partList, _currentGeometryModules, true))
                {
                    voxelWatch.Stop();
                    voxelWatch.Reset();
                    _updateRateLimiter = FARSettingsScenarioModule.VoxelSettings.minPhysTicksPerUpdate - 2;
                    _updateQueued = true;
                }
            }
        }

        private void TriggerIGeometryUpdaters()
        {
            for (int i = 0; i < _currentGeometryModules.Count; i++)
                _currentGeometryModules[i].RunIGeometryUpdaters();
        }

        void UpdateCrossSections()
        {
            double[] areas = _vehicleAero.GetCrossSectionAreas();
            double[] secondDerivAreas = _vehicleAero.GetCrossSection2ndAreaDerivs();
            double[] pressureCoeff = _vehicleAero.GetPressureCoeffs();

            double sectionThickness = _vehicleAero.SectionThickness;
            double offset = _vehicleAero.FirstSectionXOffset();

            double[] xAxis = new double[areas.Length];

            double maxValue = 0;
            for (int i = 0; i < areas.Length; i++)
            {
                maxValue = Math.Max(maxValue, areas[i]);
            }

            for (int i = 0; i < xAxis.Length; i++)
            {
                xAxis[i] = (xAxis.Length - i - 1) * sectionThickness + offset;
            }

            _areaRulingOverlay.UpdateAeroData(_vehicleAero.VoxelAxisToLocalCoordMatrix(), xAxis, areas, secondDerivAreas, pressureCoeff, maxValue);
        }
        #endregion

        #region GUIFunctions

        void OnGUI()
        {
            //Make this an option
            if (useKSPSkin)
                GUI.skin = HighLogic.Skin;

            bool cursorInGUI = false;
            EditorLogic EdLogInstance = EditorLogic.fetch;
            if (showGUI)
            {
                guiRect = GUILayout.Window(this.GetHashCode(), guiRect, OverallSelectionGUI, "FAR Analysis");
                guiRect = GUIUtils.ClampToScreen(guiRect);
                cursorInGUI = guiRect.Contains(GUIUtils.GetMousePos());
            }
            if (cursorInGUI)
            {
                EditorTooltip.Instance.HideToolTip();
                if(!CameraMouseLook.GetMouseLook())
                    EdLogInstance.Lock(false, false, false, "FAREdLock");
                else
                    EdLogInstance.Unlock("FAREdLock");
            }
            else if (!cursorInGUI)
            {
                EdLogInstance.Unlock("FAREdLock");
            }
        }

        void OverallSelectionGUI(int windowId)
        {
            GUILayout.BeginHorizontal(GUILayout.Width(800));
            modeDropdown.GUIDropDownDisplay();
            currentMode = modeDropdown.ActiveSelection;

            GUILayout.BeginVertical();
            if (GUILayout.Button(gearToggle ? "Lower Gear" : "Raise Gear"))
                ToggleGear();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            if (GUILayout.Button(showAoAArrow ? "Hide Vel Indicator" : "Show Vel Indicator"))
                showAoAArrow = !showAoAArrow;
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            //GUILayout.EndHorizontal();
            if (currentMode == FAREditorMode.STATIC)
            {
                _editorGraph.Display();
                guiRect.height = useKSPSkin ? 570 : 450;
            }
            else if (currentMode == FAREditorMode.STABILITY)
            {
                _stabDeriv.Display();
                guiRect.height = useKSPSkin ? 610 : 450;
            }
            else if (currentMode == FAREditorMode.SIMULATION)
            {
                _stabDerivLinSim.Display();
                guiRect.height = useKSPSkin ? 570 : 450;
            }
            else if (currentMode == FAREditorMode.AREA_RULING)
            {
                CrossSectionAnalysisGUI();
                DebugVisualizationGUI();
                guiRect.height = useKSPSkin ? 330 : 220;
            }

            GUI.DragWindow();
        }

        void DebugVisualizationGUI()
        {
            if (GUILayout.Button("Display Debug Voxels"))
                _vehicleAero.DebugVisualizeVoxels(EditorLogic.RootPart.partTransform.localToWorldMatrix);
        }

        void CrossSectionAnalysisGUI()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Transonic Area Ruling Analysis", GUILayout.Width(350));
            GUILayout.EndHorizontal();

            GUIStyle BackgroundStyle = new GUIStyle(GUI.skin.box);
            BackgroundStyle.hover = BackgroundStyle.active = BackgroundStyle.normal;

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(BackgroundStyle, GUILayout.Width(350), GUILayout.ExpandHeight(true));
            GUILayout.Label("Max Cross-Section Area: " + _vehicleAero.MaxCrossSectionArea.ToString("G6") + " m²");
            GUILayout.Label("Mach 1 Wave Drag-Area: " + _vehicleAero.SonicDragArea.ToString("G6") + " m²");
            GUILayout.Label("Critical Mach Number: " + _vehicleAero.CriticalMach.ToString("G6"));
            GUILayout.EndVertical();

            GUILayout.BeginVertical(BackgroundStyle, GUILayout.ExpandHeight(true));
            GUILayout.Label("Minimal wave drag is achieved by maintaining a\n\rsmooth, minimal curvature cross-section curve, including the\n\reffects of intake -> engine ducting.");
            bool areaVisible  = _areaRulingOverlay.IsVisible(EditorAreaRulingOverlay.OverlayType.AREA);
            bool derivVisible = _areaRulingOverlay.IsVisible(EditorAreaRulingOverlay.OverlayType.DERIV);
            bool coeffVisible = _areaRulingOverlay.IsVisible(EditorAreaRulingOverlay.OverlayType.COEFF);

            if (GUILayout.Toggle(areaVisible, "Show cross-sectional area curve (green)") != areaVisible)
                _areaRulingOverlay.SetVisibility(EditorAreaRulingOverlay.OverlayType.AREA, !areaVisible);

            if (GUILayout.Toggle(derivVisible, "Show curvature cross-sectional area curve (yellow)") != derivVisible)
                _areaRulingOverlay.SetVisibility(EditorAreaRulingOverlay.OverlayType.DERIV, !derivVisible);

            if (GUILayout.Toggle(coeffVisible, "Show avg pressure coefficient curve (blue)") != coeffVisible)
                _areaRulingOverlay.SetVisibility(EditorAreaRulingOverlay.OverlayType.COEFF, !coeffVisible);

            GUILayout.Label("Minimize curvature to minimize wave drag");
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
        #endregion

        #region AoAArrow
        void LateUpdate()
        {
            if (arrowTransform == null)
            {
                if (velocityArrow != null)
                    UnityEngine.Object.Destroy(velocityArrow);

                if (EditorLogic.RootPart != null)
                    arrowTransform = EditorLogic.RootPart.partTransform;
                else
                    return;
            }
            if (velocityArrow == null)
                velocityArrow = ArrowPointer.Create(arrowTransform, Vector3.zero, Vector3.forward, 15, Color.white, true);

            if (showGUI && showAoAArrow)
            {
                velocityArrow.gameObject.SetActive(true);
                ArrowDisplay();
            }
            else
                velocityArrow.gameObject.SetActive(false);
        }

        void ArrowDisplay()
        {
            if (currentMode == FAREditorMode.STATIC)
                _editorGraph.ArrowAnim(velocityArrow);
            else if (currentMode == FAREditorMode.STABILITY || currentMode == FAREditorMode.SIMULATION)
                _stabDeriv.ArrowAnim(velocityArrow);
            else
                velocityArrow.Direction = Vector3.zero;
        }
        #endregion

        #region AppLauncher

        private void GenerateBlizzyToolbarButton()
        {
            if (blizzyEditorGUIButton == null)
            {
                blizzyEditorGUIButton = ToolbarManager.Instance.add("FerramAerospaceResearch", "FAREditorButtonBlizzy");
                blizzyEditorGUIButton.TexturePath = "FerramAerospaceResearch/Textures/icon_button_blizzy";
                blizzyEditorGUIButton.ToolTip = "FAR Editor";
                blizzyEditorGUIButton.OnClick += (e) => showGUI = !showGUI;
            }
        }

        private void OnGUIAppLauncherReady()
        {
            if (ApplicationLauncher.Ready && editorGUIAppLauncherButton == null)
            {
                if (EditorDriver.editorFacility == EditorFacility.VAB)
                {
                    editorGUIAppLauncherButton = ApplicationLauncher.Instance.AddModApplication(
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
                    editorGUIAppLauncherButton = ApplicationLauncher.Instance.AddModApplication(
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
            showGUI = true;
        }

        void onAppLaunchToggleOff()
        {
            showGUI = false;
        }

        void DummyVoid() { }
        #endregion

        #region UtilFuncs
        GUIDropDown<CelestialBody> CreateBodyDropdown()
        {
            CelestialBody[] bodies = FlightGlobals.Bodies.ToArray();
            string[] bodyNames = new string[bodies.Length];
            for (int i = 0; i < bodyNames.Length; i++)
                bodyNames[i] = bodies[i].bodyName;

            int kerbinIndex = 1;
            GUIDropDown<CelestialBody> celestialBodyDropdown = new GUIDropDown<CelestialBody>(bodyNames, bodies, kerbinIndex);
            return celestialBodyDropdown;
        }

        void ToggleGear()
        {
            List<Part> partsList = EditorLogic.SortedShipList;
            for(int i = 0; i < partsList.Count; i++)
            {
                Part p = partsList[i];
                if(p.Modules.Contains("ModuleLandingGear"))
                {
                    ModuleLandingGear l = (ModuleLandingGear)p.Modules["ModuleLandingGear"];
                    l.StartDeployed = gearToggle;
                }
                if (p.Modules.Contains("ModuleAdvancedLandingGear"))
                {
                    ModuleAdvancedLandingGear l = (ModuleAdvancedLandingGear)p.Modules["ModuleAdvancedLandingGear"];
                    l.startDeployed = gearToggle;
                }
                if(p.Modules.Contains("FSwheel"))
                {
                    PartModule m = p.Modules["FSwheel"];
                    MethodInfo method = m.GetType().GetMethod("animate", BindingFlags.Instance | BindingFlags.NonPublic);
                    method.Invoke(m, gearToggle ? new object[] { "Deploy" } : new object[] { "Retract" });
                }
                if (p.Modules.Contains("FSBDwheel"))
                {
                    PartModule m = p.Modules["FSBDwheel"];
                    MethodInfo method = m.GetType().GetMethod("animate", BindingFlags.Instance | BindingFlags.NonPublic);
                    method.Invoke(m, gearToggle ? new object[] { "Deploy" } : new object[] { "Retract" });
                }
            }
            gearToggle = !gearToggle;
        }

        #endregion
    }
}
