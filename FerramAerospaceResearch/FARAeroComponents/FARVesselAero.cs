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

        Thread _runtimeThread = null;
        bool _threadDone = false;

        private void Start()
        {
            _vessel = gameObject.GetComponent<Vessel>();
            VesselUpdate();
            this.enabled = true;
        }

        //TODO: Investigate overhead of this method and related worker thread; check for garbage collection issues
        private void FixedUpdate()
        {
            if (frameCountToUpdate > 0)
                frameCountToUpdate--;
            else
            {
                frameCountToUpdate = 2;
                lock (_vessel)
                {
                    Monitor.Pulse(_vessel);
                }
            }
        }

        private void OnDestroy()
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

        //TODO: have this grab from a config file
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

            if (_runtimeThread == null)
            {
                _runtimeThread = new Thread(UpdateVesselAeroData);
                _runtimeThread.Start();
            }
        }

        private void UpdateVesselAeroData()
        {
            try
            {
                while (!_threadDone)
                {
                    int front, back;
                    float sectionThickness;
                    lock (_vessel)
                    {
                        _voxel.CrossSectionData(_vehicleCrossSection, _vessel.transform.TransformDirection(_vessel.srf_velocity), out front, out back, out sectionThickness);


                        float dragCoefficient = 0;
                        float lastLj = 0;
                        //float vehicleLength = sectionThickness * Math.Abs(front - back);
                        float nonZeroCrossSectionLj = (float)Math.Log(sectionThickness) - 1;
                        //float nonZeroCrossSectionEnd = _vehicleCrossSection[back].area_deriv1 / (float)Math.PI;

                        for (int j = 0; j <= Math.Abs(front - back); j++)
                        {
                            float thisLj = j + 0.5f;
                            thisLj *= (float)Math.Log(thisLj);

                            float crossSectionEffect = 0;
                            for (int i = j; i <= Math.Abs(front - back); i++)
                            {
                                crossSectionEffect += _vehicleCrossSection[i + front].area_deriv2 * _vehicleCrossSection[i - j + front].area_deriv2;
                            }

                            dragCoefficient += (thisLj - lastLj + nonZeroCrossSectionLj) * crossSectionEffect;

                            lastLj = thisLj;
                        }
                        //TODO: Add full effect of non-zero cross-section at vehicle end
                        dragCoefficient *= sectionThickness * sectionThickness / (float)Math.PI;
                        Debug.Log(dragCoefficient);


                        Monitor.Wait(_vessel);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
