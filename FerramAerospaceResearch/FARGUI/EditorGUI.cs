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
        ferramGraph graph = new ferramGraph(400, 275);

        void Start()
        {
            instance = this;

            _vehicleAero = new VehicleAerodynamics();
            guiRect.height = 500;
            guiRect.width = 650;
            GameEvents.onEditorPartEvent.Add(UpdateGeometryEvent);
        }

        void OnDestroy()
        {
            GameEvents.onEditorPartEvent.Remove(UpdateGeometryEvent);
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

        void FixedUpdate()
        {
            if((object)EditorLogic.RootPart != null)
                if (_updateRateLimiter < 20)
                {
                    _updateRateLimiter++;
                }
                else if (_updateQueued)
                    RecalculateVoxel();

            OnGUIAppLauncherReady();
        }

        public static void UpdateVoxel()
        {
            if (instance._updateRateLimiter > 18)
                instance._updateRateLimiter = 18;
            instance._updateQueued = true;
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
            graph.Clear();

            double[] areas = _vehicleAero.GetCrossSectionAreas();
            double[] secondDerivAreas = _vehicleAero.GetCrossSection2ndAreaDerivs();

            double section = 1d / areas.Length;

            double[] xAxis = new double[areas.Length];
            for(int i = 0; i < xAxis.Length; i++)
            {
                xAxis[i] = section * i;
            }

            double lowerBound = 0;
            double upperBound = 0;

            for (int i = 0; i < areas.Length; i++)
            {
                lowerBound = Math.Min(lowerBound, secondDerivAreas[i]);

                upperBound = Math.Max(upperBound, areas[i]);
                upperBound = Math.Max(upperBound, secondDerivAreas[i]);
            }

            graph.SetBoundaries(0, 1, lowerBound, upperBound);
            graph.SetGridScaleUsingValues(0.1, 0.5);

            graph.AddLine("S", xAxis, areas, Color.green);
            graph.AddLine("S\'\'", xAxis, secondDerivAreas, Color.yellow);

            graph.horizontalLabel = "Body Station";
            graph.verticalLabel = "Area, 2nd Deriv Area";

            graph.Update();
        }

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
            if (GUILayout.Button("Update"))
                UpdateCrossSections();

            GraphDisplay();
        }

        void GraphDisplay()
        {
            GUIStyle graphBackingStyle = new GUIStyle(GUI.skin.box);
            graphBackingStyle.hover = graphBackingStyle.active = graphBackingStyle.normal;

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(540));

            graph.Display(graphBackingStyle, 0, 0);

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
