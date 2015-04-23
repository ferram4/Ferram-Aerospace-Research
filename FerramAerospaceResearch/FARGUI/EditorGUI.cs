using System;
using System.Collections.Generic;
using UnityEngine;
using FerramAerospaceResearch.FARAeroComponents;
using ferram4;

namespace FerramAerospaceResearch.FARGUI
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

        static bool showGUI = false;
        static Rect guiRect;
        static ApplicationLauncherButton editorGUIAppLauncherButton;

        VehicleAerodynamics _vehicleAero;
        EditorAeroCenter _aeroCenter;
        EditorAreaRulingOverlay _areaRulingOverlay;

        ferramGraph _graph = new ferramGraph(400, 275);


        void Start()
        {
            instance = this;

            _vehicleAero = new VehicleAerodynamics();
            _aeroCenter = new EditorAeroCenter();
            _areaRulingOverlay = new EditorAreaRulingOverlay(Color.gray, Color.green, Color.yellow);
            guiRect.height = 500;
            guiRect.width = 650;
            GameEvents.onEditorPartEvent.Add(UpdateGeometryEvent);
            GameEvents.onEditorUndo.Add(UpdateGeometryEvent);
            GameEvents.onEditorRedo.Add(UpdateGeometryEvent);
        }

        void OnDestroy()
        {
            GameEvents.onEditorPartEvent.Remove(UpdateGeometryEvent);
            GameEvents.onEditorUndo.Remove(UpdateGeometryEvent);
            GameEvents.onEditorRedo.Remove(UpdateGeometryEvent);
            EditorLogic.fetch.Unlock("FAREdLock");
        }

        private void UpdateGeometryEvent(ConstructionEventType type, Part pEvent)
        {
            if (type == ConstructionEventType.PartRotated ||
            type == ConstructionEventType.PartOffset ||
            type == ConstructionEventType.PartAttached ||
            type == ConstructionEventType.PartDetached ||
            type == ConstructionEventType.PartRootSelected)
            {
                UpdateVoxel();
            }
        }
        private void UpdateGeometryEvent(ShipConstruct construct)
        {
            UpdateVoxel();
        }


        void FixedUpdate()
        {
            if ((object)EditorLogic.RootPart != null)
            {
                if (_vehicleAero.CalculationCompleted)
                {
                    _aeroCenter.UpdateAeroData(_vehicleAero);
                    UpdateCrossSections();
                } 

                if (_updateRateLimiter < 20)
                {
                    _updateRateLimiter++;
                }
                else if (_updateQueued)
                    RecalculateVoxel();
            }

            OnGUIAppLauncherReady();
        }

        #region CenterOfLiftCalcs

        #endregion
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
            if (_updateRateLimiter < 20)        //this has been updated recently in the past; queue an update and return
            {
                _updateQueued = true;
                return;
            }
            else                                //last update was far enough in the past to run; reset rate limit counter and clear the queued flag
            {
                _updateRateLimiter = 0;
                _updateQueued = false;
            }

            _vehicleAero.VoxelUpdate(EditorLogic.RootPart.transform.worldToLocalMatrix, EditorLogic.RootPart.transform.localToWorldMatrix, EDITOR_VOXEL_COUNT, EditorLogic.SortedShipList);
        }

        void UpdateCrossSections()
        {
            //_graph.Clear();

            double[] areas = _vehicleAero.GetCrossSectionAreas();
            double[] secondDerivAreas = _vehicleAero.GetCrossSection2ndAreaDerivs();

            double sectionThickness = _vehicleAero.SectionThickness;
            double offset = _vehicleAero.FirstSectionXOffset();

            double[] xAxis = new double[areas.Length];

            double scalingFactor = 0;
            for (int i = 0; i < areas.Length; i++)
            {
                scalingFactor = Math.Max(scalingFactor, areas[i]);
            }

            scalingFactor = 10 / scalingFactor;     //all scaled to a 10 m max height for area;

            for (int i = 0; i < xAxis.Length; i++)
            {
                xAxis[i] = (xAxis.Length - i - 1) * sectionThickness + offset;
                areas[i] *= scalingFactor;
                secondDerivAreas[i] *= scalingFactor;
            }

            _areaRulingOverlay.UpdateAeroData(_vehicleAero.VoxelAxisToLocalCoordMatrix(), xAxis, areas, secondDerivAreas);
        }
        #endregion

        #region GUIFunctions
        void OnGUI()
        {
            bool cursorInGUI = false;
            EditorLogic EdLogInstance = EditorLogic.fetch;
            if (showGUI)
            {
                guiRect = GUILayout.Window(this.GetHashCode(), guiRect, OverallSelectionGUI, "FAR Analysis");

                cursorInGUI = guiRect.Contains(FARGUIUtils.GetMousePos());
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
            DebugVisualizationGUI();
            CrossSectionAnalysisGUI();
            GUI.DragWindow();
        }

        void DebugVisualizationGUI()
        {
            if (GUILayout.Button("Display Debug Voxels"))
                _vehicleAero.DebugVisualizeVoxels(EditorLogic.RootPart.transform.localToWorldMatrix);
        }

        void CrossSectionAnalysisGUI()
        {
            if (GUILayout.Button("Toggle CrossSections"))
                _areaRulingOverlay.ToggleVisibility();

            //GraphDisplay();
        }

        void GraphDisplay()
        {
            GUIStyle graphBackingStyle = new GUIStyle(GUI.skin.box);
            graphBackingStyle.hover = graphBackingStyle.active = graphBackingStyle.normal;

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(540));

            _graph.Display(graphBackingStyle, 0, 0);

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
    }
}
