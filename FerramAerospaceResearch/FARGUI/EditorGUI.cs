using System;
using System.Collections.Generic;
using UnityEngine;
using FerramAerospaceResearch.FARPartGeometry;

namespace FerramAerospaceResearch.FARGUI
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    class EditorGUI : MonoBehaviour
    {
        VehicleVoxel _voxel;
        VoxelCrossSection[] _vehicleCrossSection = null;

        const int EDITOR_VOXEL_COUNT = 125000;

        static EditorGUI instance;
        static EditorGUI Instance
        {
            get { return instance; }
        }

        static bool showGUI = false;
        static Rect guiRect;
        static ApplicationLauncherButton editorGUIAppLauncherButton;

        void Start()
        {
            if (instance == null)
                instance = this;
            else
                GameObject.Destroy(this);
        }

        static void UpdateVoxel()
        {
            instance.RecalculateVoxel();
        }

        void RecalculateVoxel()
        {

        }

        private void CreateVoxel(object nullObj)
        {
            try
            {
                VehicleVoxel newvoxel = new VehicleVoxel(EditorLogic.SortedShipList, EDITOR_VOXEL_COUNT, true, true);

                _vehicleCrossSection = newvoxel.EmptyCrossSectionArray;

                _voxel = newvoxel;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
