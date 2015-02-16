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

        List<FARAeroPartModule> aeroModules = new List<FARAeroPartModule>();
        float tmpFactor = 0;
        float machNumber = 0;

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
                machNumber = (float)_vessel.srfSpeed / 340f;
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

            GetNewAeroModules();
        }

        private void GetNewAeroModules()
        {
            lock (_vessel)
            {
                aeroModules.Clear();
                tmpFactor = 0;
                foreach (Part p in _vessel.Parts)
                {
                    FARAeroPartModule m = p.GetComponent<FARAeroPartModule>();
                    if (m != null)
                    {
                        aeroModules.Add(m);
                        tmpFactor += p.mass;
                    }
                }
            }
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
                    float sectionThickness, maxCrossSectionArea;

                    lock (_vessel)
                    {
                        Vector3 velocity = _vessel.transform.worldToLocalMatrix.MultiplyVector(_vessel.srf_velocity);
                        if (velocity.x != 0 || velocity.y != 0 || velocity.z != 0)
                        {
                            _voxel.CrossSectionData(_vehicleCrossSection, velocity, out front, out back, out sectionThickness, out maxCrossSectionArea);


                            float dragCoefficient = 0;
                            float lastLj = 0;
                            //float vehicleLength = sectionThickness * Math.Abs(front - back);
                            float nonZeroCrossSectionLj = (float)Math.Log(sectionThickness) - 1;
                            //float nonZeroCrossSectionEnd = 0;

                            float invMaxRadFactor = 1f / (float)Math.Sqrt(maxCrossSectionArea / (float)Math.PI);

                            for (int j = 0; j <= Math.Abs(front - back); j++)
                            {
                                float thisLj = j + 0.5f;
                                float tmp = (float)Math.Log(thisLj);

                                thisLj *= tmp;

                                float crossSectionEffect = 0;
                                for (int i = j; i <= Math.Abs(front - back); i++)
                                {
                                    float area1, area2;
                                    area1 = Math.Min(_vehicleCrossSection[i + front].areaDeriv2ToNextSection, _vehicleCrossSection[i + front].area);
                                    area2 = Math.Min(_vehicleCrossSection[i - j + front].areaDeriv2ToNextSection, _vehicleCrossSection[i - j + front].area);
                                    crossSectionEffect += area1 * area2;
                                }

                                dragCoefficient -= (thisLj - lastLj + nonZeroCrossSectionLj) * crossSectionEffect * sectionThickness * sectionThickness;
                                float deltaAreaDeriv = Math.Min(_vehicleCrossSection[j + front].areaDeriv2ToNextSection, _vehicleCrossSection[j + front].area);
                                deltaAreaDeriv *= deltaAreaDeriv;

                                deltaAreaDeriv *= (float)Math.Log(4f / (1f * 2f * (float)Math.Sqrt(_vehicleCrossSection[j + front].area / Math.PI) * invMaxRadFactor));

                                dragCoefficient += 0.5f * deltaAreaDeriv;

                                lastLj = thisLj;
                            }
                            dragCoefficient /= (float)Math.PI;

                            if (aeroModules.Count > 0)
                            {
                                dragCoefficient /= tmpFactor;

                                foreach (FARAeroPartModule m in aeroModules)
                                {
                                    m.IncrementNewDragPerDynPres(dragCoefficient);
                                    m.updateDrag = true;
                                }
                            }
                        }
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
