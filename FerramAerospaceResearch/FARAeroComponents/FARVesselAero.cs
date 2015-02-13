using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using FerramAerospaceResearch.FARPartGeometry;

namespace FerramAerospaceResearch.FARAeroComponents
{
    public class FARVesselAero : MonoBehaviour
    {
        Vessel _vessel;
        VesselType _vType;
        int _voxelCount;

        VehicleVoxel _voxel = null;
        VoxelCrossSection[] _vehicleCrossSection = null;
        int frameCountToUpdate = 0;

        Thread _runtimeThread;
        bool _threadDone = false;

        public void Start()
        {
            _vessel = gameObject.GetComponent<Vessel>();
            VesselUpdate();
            this.enabled = true;
        }

        public void FixedUpdate()
        {
            if (_voxel != null)
                if (frameCountToUpdate <= 0)
                {
                    lock (_vessel)
                    {
                        frameCountToUpdate = 2;
                        Monitor.Pulse(_vessel);
                    }
                }
                else
                    frameCountToUpdate--;
            
        }

        public void OnDestroy()
        {
            _threadDone = true;
            lock (_vessel)
                Monitor.Pulse(_vessel);
        }

        public void VesselUpdate()
        {
            if(_vessel == null)
                _vessel = gameObject.GetComponent<Vessel>();
            _vType = _vessel.vesselType;
            _voxelCount = VoxelCountFromType();

            ThreadPool.QueueUserWorkItem(CreateVoxel);

            Debug.Log("Updating vessel voxel for " + _vessel.vesselName);
        }

        private int VoxelCountFromType()
        {
            if (_vType == VesselType.Debris || _vType == VesselType.Unknown)
                return 20000;
            else
                return 250000;
        }

        private void CreateVoxel(object nullObj)
        {
            VehicleVoxel newvoxel = new VehicleVoxel(_vessel.parts, _voxelCount, true, true);

            lock (_vessel)
            {
                _voxel = newvoxel;
                _vehicleCrossSection = new VoxelCrossSection[_voxel.MaxArrayLength];
            }

            if(_runtimeThread == null)
                _runtimeThread = new Thread(UpdateCrossSectionData);
        }

        private void UpdateCrossSectionData()
        {
            while (!_threadDone)
            {
                int front, back;
                lock (_vessel)
                {
                    _voxel.CrossSectionData(_vehicleCrossSection, _vessel.transform.TransformDirection(_vessel.srf_velocity), out front, out back);
                    Monitor.Wait(_vessel);
                }
            }
        }
    }
}
