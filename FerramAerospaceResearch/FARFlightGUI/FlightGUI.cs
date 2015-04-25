using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using FerramAerospaceResearch.FARAeroComponents;
using ferram4;

namespace FerramAerospaceResearch.FARGUI
{
    public class FlightGUI : VesselModule
    {
        Vessel _vessel;
        FARVesselAero _vesselAero;
        List<FARAeroPartModule> _currentAeroModules;

        List<FARWingAerodynamicModel> _LEGACY_currentWingAeroModel = new List<FARWingAerodynamicModel>();

        Vector3 totalAeroForceVector;

        double liftForce, dragForce, sideForce;
        double liftCoeff, dragCoeff, sideCoeff;
        double refArea;

        string dataString = "";

        static bool showGUI = false;
        public static bool showAllGUI = true;
        static Rect guiRect;
        static ApplicationLauncherButton flightGUIAppLauncherButton;

        void Start()
        {
            _vessel = GetComponent<Vessel>();
            _vesselAero = GetComponent<FARVesselAero>();
            this.enabled = true;
            OnGUIAppLauncherReady();
        }

        void UpdateAeroModules(List<FARAeroPartModule> newAeroModules)
        {
            _currentAeroModules = newAeroModules;
            _LEGACY_currentWingAeroModel.Clear();

            for(int i = 0; i < _vessel.parts.Count; i++)
            {
                Part p = _vessel.parts[i];
                FARWingAerodynamicModel w = p.GetComponent<FARWingAerodynamicModel>();
                if ((object)w != null)
                    _LEGACY_currentWingAeroModel.Add(w);
            }
        }

        #region PhysicsBlock
        void FixedUpdate()
        {
            CalculateTotalAeroForce();
            CalculateForceBreakdown();
        }

        void CalculateTotalAeroForce()
        {
            totalAeroForceVector = Vector3.zero;
            if (_currentAeroModules != null)
            {
                for (int i = 0; i < _currentAeroModules.Count; i++)
                {
                    FARAeroPartModule m = _currentAeroModules[i];
                    if ((object)m != null)
                        totalAeroForceVector += m.worldSpaceAeroForce;
                }
            }

            for(int i = 0; i < _LEGACY_currentWingAeroModel.Count; i++)
            {
                FARWingAerodynamicModel w = _LEGACY_currentWingAeroModel[i];
                if ((object)w != null)
                    totalAeroForceVector += w.worldSpaceForce;
            }
        }

        void CalculateForceBreakdown()
        {
            Vector3d velVectorNorm = _vessel.srf_velocity.normalized;

            dragForce = -Vector3d.Dot(totalAeroForceVector, velVectorNorm);     //reverse along vel normal will be drag

            Vector3d remainderVector = totalAeroForceVector + velVectorNorm * dragForce;

            liftForce = -Vector3d.Dot(remainderVector, _vessel.ReferenceTransform.forward);     //forward points down for the vessel, so reverse along that will be lift
            sideForce = Vector3d.Dot(remainderVector, _vessel.ReferenceTransform.right);        //and the side force

            refArea = _vesselAero.MaxCrossSectionArea;

            double invAndDynPresArea = refArea;
            invAndDynPresArea *= 0.5 * _vessel.atmDensity * _vessel.srf_velocity.sqrMagnitude;
            invAndDynPresArea = 1000 / invAndDynPresArea;

            dragCoeff = dragForce * invAndDynPresArea;
            liftCoeff = liftForce * invAndDynPresArea;
            sideCoeff = sideForce * invAndDynPresArea;
        }
        #endregion

        #region GUI Functions
        void LateUpdate()
        {
            CreateString();
        }

        void CreateString()
        {
            StringBuilder data = new StringBuilder();
            data.AppendLine(_vesselAero.MachNumber.ToString("F3"));
            data.AppendLine(_vesselAero.ReynoldsNumber.ToString("e2"));
            data.AppendLine();

            data.AppendLine(_vessel.dynamicPressurekPa.ToString("F3"));
            data.AppendLine(_vessel.atmDensity.ToString("F3"));
            data.AppendLine();

            data.Append(liftForce.ToString("F3"));
            data.AppendLine(" kN");
            data.Append(dragForce.ToString("F3"));
            data.AppendLine(" kN");
            data.Append(sideForce.ToString("F3"));
            data.AppendLine(" kN");
            data.AppendLine();

            data.AppendLine(liftCoeff.ToString("F3"));
            data.AppendLine(dragCoeff.ToString("F3"));
            data.AppendLine(sideCoeff.ToString("F3"));
            data.Append(refArea.ToString("F3"));
            data.Append(" m^2");

            dataString = data.ToString();
        }


        void OnGUI()
        {
            if (_vessel == FlightGlobals.ActiveVessel && showGUI && showAllGUI)
            {
                guiRect = GUILayout.Window(this.GetHashCode(), guiRect, MainFlightGUIWindow, "FAR Flight Systems", GUILayout.MinWidth(150));
            }
        }

        void MainFlightGUIWindow(int windowId)
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Box("Mach:\n\rReynolds:\n\r\n\rQ:\n\rdensity:\n\r\n\rLift:\n\rDrag:\n\rSideForce:\n\r\n\rCl:\n\rCd:\n\rCy:\n\rRef Area:", GUILayout.Width(75));
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            GUILayout.Box(dataString, GUILayout.Width(75));
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }
        #endregion

        #region AppLauncher

        public void OnGUIAppLauncherReady()
        {
            if (ApplicationLauncher.Ready && flightGUIAppLauncherButton == null)
            {
                flightGUIAppLauncherButton = ApplicationLauncher.Instance.AddModApplication(
                    onAppLaunchToggleOn,
                    onAppLaunchToggleOff,
                    DummyVoid,
                    DummyVoid,
                    DummyVoid,
                    DummyVoid,
                    ApplicationLauncher.AppScenes.FLIGHT,
                    (Texture)GameDatabase.Instance.GetTexture("FerramAerospaceResearch/Textures/icon_button_stock", false));
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

        private void HideUI()
        {
            showAllGUI = false;
        }

        private void ShowUI()
        {
            showAllGUI = true;
        }
        #endregion
    }
}
