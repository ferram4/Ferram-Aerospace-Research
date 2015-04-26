using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using KSP;
using ProceduralFairings;

namespace FerramAerospaceResearch.FARPartGeometry.GeometryUpdaters
{
    class StockProcFairingGeoUpdater : IGeometryUpdater
    {
        ModuleProceduralFairing fairing;
        GeometryPartModule geoModule;
        List<Bounds> prevPanelBounds;
        KFSMEvent deployEvent;

        public StockProcFairingGeoUpdater(ModuleProceduralFairing fairing, GeometryPartModule geoModule)
        {
            this.fairing = fairing;
            this.geoModule = geoModule;

            if (HighLogic.LoadedSceneIsEditor)
                prevPanelBounds = new List<Bounds>();
        }

        public void EditorGeometryUpdate()
        {
            List<FairingPanel> panels = fairing.Panels;
            bool rebuildMesh = false;

            rebuildMesh = prevPanelBounds.Count == panels.Count;        //if bounds count doesn't equal panels count, the number of panels changed

            if (rebuildMesh)
                prevPanelBounds.Clear();

            for (int i = 0; i < panels.Count; i++)      //set them back to where they started to prevent voxelization errors
            {
                panels[i].SetExplodedView(0);
                Bounds panelBounds = panels[i].GetBounds();

                if(i >= prevPanelBounds.Count)      //set new panel bounds
                {
                    rebuildMesh = true;
                    prevPanelBounds.Add(panelBounds);
                }
                else if(panelBounds != prevPanelBounds[i])
                {
                    rebuildMesh = true;
                    prevPanelBounds.Add(panelBounds);
                }
            }

            if (rebuildMesh)
                geoModule.RebuildAllMeshData();
        }

        public void FlightGeometryUpdate()
        {
            if (deployEvent == null)
            {
                Debug.Log("Update fairing event");
                FieldInfo[] fields = fairing.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
                deployEvent = (KFSMEvent)fields[31].GetValue(fairing);
                deployEvent.OnEvent += delegate { FairingDeployGeometryUpdate(); };
            }
        }

        private void FairingDeployGeometryUpdate()
        {
            Debug.Log("Fairing Geometry Update");
            geoModule.GeometryPartModuleRebuildMeshData();
        }
    }
}
