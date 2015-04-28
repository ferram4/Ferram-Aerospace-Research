using System;
using System.Collections.Generic;
using UnityEngine;
using FerramAerospaceResearch.FARAeroComponents;
using FerramAerospaceResearch.FARPartGeometry;
using FerramAerospaceResearch.FARGUI.FAREditorGUI.Simulation;
using ferram4;

namespace FerramAerospaceResearch.FARGUI.FAREditorGUI
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class EditorGUI : MonoBehaviour
    {
        const int EDITOR_VOXEL_COUNT = 125000;

        static EditorGUI instance;
        public static EditorGUI Instance
        {
            get { return instance; }
        }

        int _updateRateLimiter = 0;
        bool _updateQueued = true;
        bool _updateRebuildGeo = false;

        static bool showGUI = false;
        bool useKSPSkin = true;
        Rect guiRect;
        public static Rect GUIRect
        {
            get { return instance.guiRect; }
        }
        static ApplicationLauncherButton editorGUIAppLauncherButton;

        VehicleAerodynamics _vehicleAero;
        List<GeometryPartModule> _currentGeometryModules = new List<GeometryPartModule>();
        EditorSimManager _simManager;

        EditorAreaRulingOverlay _areaRulingOverlay;
        StaticAnalysisGraphGUI _editorGraph;
        StabilityDerivGUI _stabDeriv;
        StabilityDerivSimulationGUI _stabDerivLinSim;

        bool gearToggle = false;

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
            if (instance == null)
                instance = this;
            else
                GameObject.Destroy(this);

            _vehicleAero = new VehicleAerodynamics();

            InstantConditionSim instantSim = new InstantConditionSim();
            GUIDropDown<int> flapSettingDropDown = new GUIDropDown<int>(new string[] { "0 (up)", "1 (init climb)", "2 (takeoff)", "3 (landing)" }, new int[] { 0, 1, 2, 3 }, 0);
            GUIDropDown<CelestialBody> celestialBodyDropdown = CreateBodyDropdown();

            _simManager = new EditorSimManager();

            _editorGraph = new StaticAnalysisGraphGUI(_simManager, flapSettingDropDown, celestialBodyDropdown);
            _stabDeriv = new StabilityDerivGUI(_simManager, flapSettingDropDown, celestialBodyDropdown);
            _stabDerivLinSim = new StabilityDerivSimulationGUI(_simManager);

            Color crossSection = EditorColors.GetColor(3);
            crossSection.a = 0.8f;

            Color crossSectionDeriv = EditorColors.GetColor(2);
            crossSectionDeriv.a = 0.8f;

            _areaRulingOverlay = new EditorAreaRulingOverlay(new Color(0.05f, 0.05f, 0.05f, 0.7f), crossSection, crossSectionDeriv, 10, 5);
            guiRect.height = 500;
            guiRect.width = 650;



            GameEvents.onEditorPartEvent.Add(UpdateGeometryEvent);
            GameEvents.onEditorUndo.Add(ResetEditorEvent);
            GameEvents.onEditorRedo.Add(ResetEditorEvent);
            GameEvents.onEditorShipModified.Add(ResetEditorEvent);
            GameEvents.onEditorLoad.Add(ResetEditorEvent);
            UpdateVoxel();
        }

        void OnDestroy()
        {
            GameEvents.onEditorPartEvent.Remove(UpdateGeometryEvent);
            GameEvents.onEditorUndo.Remove(ResetEditorEvent);
            GameEvents.onEditorRedo.Remove(ResetEditorEvent);
            GameEvents.onEditorShipModified.Remove(ResetEditorEvent);
            GameEvents.onEditorLoad.Remove(ResetEditorEvent);

            EditorLogic.fetch.Unlock("FAREdLock");
        }

        #region EditorEvents
        private void ResetEditorEvent(ShipConstruct construct)
        {
            List<Part> partsList = EditorLogic.SortedShipList;
            for (int i = 0; i < partsList.Count; i++)
                UpdateGeometryModule(partsList[i]);

            UpdateVoxel();
            instance._updateRebuildGeo = true;
        }
        private void ResetEditorEvent(ShipConstruct construct, CraftBrowser.LoadType type)
        {
            ResetEditor();
        }

        public static void ResetEditor()
        {
            Color crossSection = EditorColors.GetColor(3);
            crossSection.a = 0.8f;

            Color crossSectionDeriv = EditorColors.GetColor(2);
            crossSectionDeriv.a = 0.8f;

            instance._areaRulingOverlay = new EditorAreaRulingOverlay(new Color(0.05f, 0.05f, 0.05f, 0.7f), crossSection, crossSectionDeriv, 10, 5);
            UpdateVoxel();
            instance._updateRebuildGeo = true;
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
                UpdateGeometryModule(type, pEvent);
                UpdateVoxel();
                instance._updateRebuildGeo = true;
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


        private void LEGACY_UpdateWingAeroModels()
        {
            List<Part> partsList = EditorLogic.SortedShipList;
            for (int i = 0; i < partsList.Count; i++)
            {
                Part p = partsList[i];
                if (p.Modules.Contains("FARWingAerodynamicModel"))
                {
                    FARWingAerodynamicModel w = (FARWingAerodynamicModel)p.Modules["FARWingAerodynamicModel"];
                    if (w != null)
                        w.EditorUpdateWingInteractions();
                }
            }

        }
        #endregion

        void FixedUpdate()
        {
            if ((object)EditorLogic.RootPart != null)
            {
                if (_vehicleAero.CalculationCompleted)
                {
                    _simManager.UpdateAeroData(_vehicleAero);
                    UpdateCrossSections();
                } 

                if (_updateRateLimiter < 40)
                {
                    _updateRateLimiter++;
                }
                else if (_updateQueued)
                    RecalculateVoxel();
            }

            OnGUIAppLauncherReady();
        }

        #region voxel
        public static void UpdateVoxel()
        {
            if (instance._updateRateLimiter > 18)
                instance._updateRateLimiter = 18;
            instance._updateQueued = true;
            //instance._areaRulingOverlay.SetVisibility(false);

        }

        void RecalculateVoxel()
        {
            if (_updateRateLimiter < 30)        //this has been updated recently in the past; queue an update and return
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
            if (_updateRebuildGeo)
            {
                _currentGeometryModules.Clear();

                for (int i = 0; i < partList.Count; i++)
                {
                    Part p = partList[i];
                    GeometryPartModule g = p.GetComponent<GeometryPartModule>();
                    if ((object)g != null)
                    {
                        _currentGeometryModules.Add(g);
                    }
                }
            }
            TriggerIGeometryUpdaters();

            
            if(_currentGeometryModules.Count > 0)
                _vehicleAero.VoxelUpdate(EditorLogic.RootPart.transform.worldToLocalMatrix, EditorLogic.RootPart.transform.localToWorldMatrix, EDITOR_VOXEL_COUNT, partList, _currentGeometryModules, true);

            _updateRebuildGeo = false;
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

            _areaRulingOverlay.UpdateAeroData(_vehicleAero.VoxelAxisToLocalCoordMatrix(), xAxis, areas, secondDerivAreas, maxValue);
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
                EdLogInstance.Lock(false, false, false, "FAREdLock");
            }
            else if (!cursorInGUI)
            {
                EdLogInstance.Unlock("FAREdLock");
            }
        }

        void OverallSelectionGUI(int windowId)
        {
            GUILayout.BeginHorizontal();
            currentMode = (FAREditorMode)GUILayout.SelectionGrid((int)currentMode, FAReditorMode_str, 4);
            GUILayout.BeginVertical();
            if (GUILayout.Button("Toggle Gear"))
                ToggleGear();
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
                guiRect.height = useKSPSkin ? 300 : 200;
            }

            GUI.DragWindow();
        }

        void DebugVisualizationGUI()
        {
            if (GUILayout.Button("Display Debug Voxels"))
                _vehicleAero.DebugVisualizeVoxels(EditorLogic.RootPart.transform.localToWorldMatrix);
        }

        void CrossSectionAnalysisGUI()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Transonic Area Ruling Analysis", GUILayout.Width(350));
            if (GUILayout.Button("Toggle Cross-Section Area Curves", GUILayout.Width(350)))
                _areaRulingOverlay.ToggleVisibility();
            GUILayout.EndHorizontal();

            GUIStyle BackgroundStyle = new GUIStyle(GUI.skin.box);
            BackgroundStyle.hover = BackgroundStyle.active = BackgroundStyle.normal;

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(BackgroundStyle, GUILayout.Width(350));
            GUILayout.Label("Max Cross-Section Area: " + _vehicleAero.MaxCrossSectionArea.ToString("G6") + " m²");
            GUILayout.Label("Mach 1 Wave Drag-Area: " + _vehicleAero.SonicDragArea.ToString("G6") + " m²");
            GUILayout.Label("Critical Mach Number: " + _vehicleAero.CriticalMach.ToString("G6"));
            GUILayout.Label("\n\r\n\r");
            GUILayout.EndVertical();

            GUILayout.BeginVertical(BackgroundStyle);
            GUILayout.Label("Minimal wave drag is achieved by maintaining a\n\rsmooth, minimal curvature cross-section curve.\n\r");
            GUILayout.Label("Green: cross-sectional area.");
            GUILayout.Label("Yellow: curvature cross-sectional area curve.");
            GUILayout.Label("Minimize curvature to minimize wave drag");
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
        #endregion

        #region AppLauncher
        public void OnGUIAppLauncherReady()
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
            }
            gearToggle = !gearToggle;
        }

        #endregion
    }
}
