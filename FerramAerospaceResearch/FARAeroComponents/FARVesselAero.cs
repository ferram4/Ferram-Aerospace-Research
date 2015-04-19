/*
Ferram Aerospace Research v0.14.6
Copyright 2014, Michael Ferrara, aka Ferram4

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
            			Taverius, for correcting a ton of incorrect values
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
            			ialdabaoth (who is awesome), who originally created Module Manager
                        Regex, for adding RPM support
            			Duxwing, for copy editing the readme
 * 
 * Kerbal Engineer Redux created by Cybutek, Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License
 *      Referenced for starting point for fixing the "editor click-through-GUI" bug
 *
 * Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/55219
 *
 * Toolbar integration powered by blizzy78's Toolbar plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/60863
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using UnityEngine;
using FerramAerospaceResearch.FARPartGeometry;
using ferram4;

namespace FerramAerospaceResearch.FARAeroComponents
{
    public class FARVesselAero : MonoBehaviour
    {
        Vessel _vessel;
        VesselType _vType;
        int _voxelCount;

        VehicleVoxel _voxel = null;
        VoxelCrossSection[] _vehicleCrossSection = null;

        internal bool ready = false;

        double length = 0;
        Vector3 vesselMainAxis;
        Matrix4x4 vesselToWorldMatrix;
        Matrix4x4 vesselToLocalMatrix;
        List<FARAeroPartModule> _currentAeroModules;
        List<FARAeroPartModule> _newAeroModules;

        List<FARAeroSection> _currentAeroSections;
        List<FARAeroSection> _newAeroSections;

        int _updateRateLimiter = 20;
        bool _updateQueued = true;

        private void Start()
        {
            _vessel = gameObject.GetComponent<Vessel>();
            VesselUpdate();
            this.enabled = true;
            for(int i = 0; i < _vessel.Parts.Count; i++)
            {
                Part p = _vessel.Parts[i];
                p.maximum_drag = 0;
                p.minimum_drag = 0;
                p.angularDrag = 0;
            }
        }

        private void FixedUpdate()
        {
            if (ready)
            {
                _currentAeroSections = _newAeroSections;
                _newAeroSections = null;

                _currentAeroModules = _newAeroModules;
                _newAeroModules = null;
                ready = false;
            } 
            
            if (FlightGlobals.ready && _currentAeroSections != null)
            {
                float atmDensity = (float)_vessel.atmDensity;

                if (atmDensity <= 0)
                    return;

                float machNumber = (float)FARAeroUtil.GetMachNumber(_vessel.mainBody, _vessel.altitude, _vessel.srfSpeed);
                float reynoldsNumber = (float)FARAeroUtil.CalculateReynoldsNumber(_vessel.atmDensity, length, _vessel.srfSpeed, machNumber, FlightGlobals.getExternalTemperature((float)_vessel.altitude, _vessel.mainBody) + 273.15f);
                float skinFrictionDragCoefficient = (float)FARAeroUtil.SkinFrictionDrag(reynoldsNumber, machNumber);

                Vector3 frameVel = Krakensbane.GetFrameVelocityV3f();

                for (int i = 0; i < _currentAeroModules.Count; i++)
                {
                    FARAeroPartModule m = _currentAeroModules[i];
                    if (m != null)
                        m.UpdateVelocityAndAngVelocity(frameVel);
                }
                
                for (int i = 0; i < _currentAeroSections.Count; i++)
                    _currentAeroSections[i].CalculateAeroForces(atmDensity, machNumber, reynoldsNumber / (float)length, skinFrictionDragCoefficient);

                for (int i = 0; i < _currentAeroModules.Count; i++)
                {
                    FARAeroPartModule m = _currentAeroModules[i];
                    if (m != null)
                        m.ApplyForces();
                }

                if (_updateRateLimiter < 20)
                {
                    _updateRateLimiter++;
                }
                else if (_updateQueued)
                    VesselUpdate();
            }
        }

        public void AnimationVoxelUpdate()
        {
            VesselUpdate();
        }

        public void VesselUpdate()
        {
            if(_vessel == null)
                _vessel = gameObject.GetComponent<Vessel>();

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
            _vType = _vessel.vesselType;

            _voxelCount = VoxelCountFromType();

            vesselToLocalMatrix = _vessel.transform.worldToLocalMatrix;
            vesselToWorldMatrix = _vessel.transform.localToWorldMatrix;

            vesselMainAxis = CalculateVesselMainAxis();

            ThreadPool.QueueUserWorkItem(CreateVoxel);

            Debug.Log("Updating vessel voxel for " + _vessel.vesselName);
        }

        //TODO: have this grab from a config file
        private int VoxelCountFromType()
        {
            if (_vType == VesselType.Debris || _vType == VesselType.Unknown)
                return 20000;
            else
                return 125000;
        }

        private void CreateVoxel(object nullObj)
        {
            try
            {
                UpdateGeometryPartModules();
                VehicleVoxel newvoxel = new VehicleVoxel(_vessel.parts, _voxelCount, true, true);

                _vehicleCrossSection = newvoxel.EmptyCrossSectionArray;

                _voxel = newvoxel;

                CalculateVesselAeroProperties();
                ready = true;
            }
            catch(Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void UpdateGeometryPartModules()
        {
            List<Part> vesselPartsList = _vessel.Parts;
            for(int i = 0; i < vesselPartsList.Count; i++)
            {
                Part p = vesselPartsList[i];
                if ((object)p == null)
                    continue;
                GeometryPartModule geoModule = p.GetComponent<GeometryPartModule>();
                if ((object)geoModule != null)
                    geoModule.UpdateTransformMatrixList(vesselToLocalMatrix);
            }
        }

        private void CalculateVesselAeroProperties()
        {
            int front, back, numSections;
            double sectionThickness, maxCrossSectionArea;

            _voxel.CrossSectionData(_vehicleCrossSection, vesselMainAxis, out front, out back, out sectionThickness, out maxCrossSectionArea);

            numSections = back - front;
            double invMaxRadFactor = 1f / Math.Sqrt(maxCrossSectionArea / Math.PI);

            double finenessRatio = sectionThickness * numSections * 0.5 * invMaxRadFactor;       //vehicle length / max diameter, as calculated from sect thickness * num sections / (2 * max radius) 

            //skin friction and pressure drag for a body, taken from 1978 USAF Stability And Control DATCOM, Section 4.2.3.1, Paragraph A
            double viscousDragFactor = 0;
            viscousDragFactor = 60 / (finenessRatio * finenessRatio * finenessRatio) + 0.0025 * finenessRatio;     //pressure drag for a subsonic / transonic body due to skin friction
            viscousDragFactor++;

            viscousDragFactor /= (double)numSections;   //fraction of viscous drag applied to each section

            double criticalMachNumber = CalculateCriticalMachNumber(finenessRatio);

            double transonicWaveDragFactor = -sectionThickness * sectionThickness / (2 * Math.PI);

            length = sectionThickness * numSections;

            _newAeroSections = new List<FARAeroSection>();
            HashSet<FARAeroPartModule> tmpAeroModules = new HashSet<FARAeroPartModule>();

            for (int i = 0; i <= numSections; i++)  //index in the cross sections
            {
                int index = i + front;      //index along the actual body

                double prevArea, curArea, nextArea;
               
                curArea = _vehicleCrossSection[index].area;
                if (i == 0)
                    prevArea = 0;
                else
                    prevArea = _vehicleCrossSection[index - 1].area;
                if (i == numSections)
                    nextArea = 0;
                else
                    nextArea = _vehicleCrossSection[index + 1].area;

                FloatCurve xForcePressureAoA0 = new FloatCurve();
                FloatCurve xForcePressureAoA180 = new FloatCurve();

                FloatCurve xForceSkinFriction = new FloatCurve();

                //Potential and Viscous lift calcs
                float areaChange = (float)(curArea - prevArea);
                float areaChangeMax = (float)Math.Min(curArea, prevArea) * 0.15f;

                float sonicBaseDrag = 0.21f;

                sonicBaseDrag *= areaChange;    //area base drag acts over

                if (areaChange > areaChangeMax)
                    areaChange = areaChangeMax;
                else if (areaChange < -areaChangeMax)
                    areaChange = -areaChangeMax;
                else
                    sonicBaseDrag *= Math.Abs(areaChange / areaChangeMax);      //some scaling for small changes in cross-section

                double flatnessRatio = _vehicleCrossSection[index].flatnessRatio;
                if (flatnessRatio >= 1)
                    sonicBaseDrag /= (float)flatnessRatio;
                else
                    sonicBaseDrag *= (float)flatnessRatio;

                float viscCrossflowDrag = (float)(Math.Sqrt(curArea / Math.PI) * sectionThickness * 2d);

                double surfaceArea = curArea * Math.PI;
                if(surfaceArea < 0)
                    surfaceArea = 0;
                surfaceArea = 2d * Math.Sqrt(surfaceArea); //section circumference
                surfaceArea *= sectionThickness;    //section surface area for viscous effects

                xForceSkinFriction.Add(0f, (float)(surfaceArea * viscousDragFactor), 0, 0);   //subsonic incomp visc drag
                xForceSkinFriction.Add(1f, (float)(surfaceArea * viscousDragFactor), 0, 0);   //transonic visc drag
                xForceSkinFriction.Add(2f, (float)surfaceArea, 0, 0);                     //above Mach 1.4, visc is purely surface drag, no pressure-related components simulated

                float sonicWaveDrag = (float)CalculateTransonicWaveDrag(i, index, numSections, front, sectionThickness, Math.Min(maxCrossSectionArea * 0.1, curArea * 0.25));
                sonicWaveDrag *= 0.8f;     //this is just to account for the higher drag being felt due to the inherent blockiness of the model being used
                float hypersonicDragForward = (float)CalculateHypersonicDrag(prevArea, curArea, sectionThickness);
                float hypersonicDragBackward = (float)CalculateHypersonicDrag(nextArea, curArea, sectionThickness);

                if (hypersonicDragForward > 0)
                    hypersonicDragForward = 0;
                if (hypersonicDragBackward > 0)
                    hypersonicDragBackward = 0;

                float hypersonicMomentForward = (float)CalculateHypersonicMoment(prevArea, curArea, sectionThickness);
                float hypersonicMomentBackward = (float)CalculateHypersonicMoment(nextArea, curArea, sectionThickness);

                if (hypersonicMomentForward > 0)
                    hypersonicMomentForward = 0;
                if (hypersonicMomentBackward > 0)
                    hypersonicMomentBackward = 0;

                xForcePressureAoA0.Add(25f, hypersonicDragForward, 0f, 0f);
                xForcePressureAoA180.Add(25f, -hypersonicDragBackward, 0f, 0f);

                if (sonicBaseDrag > 0)      //occurs with increase in area; force applied at 180 AoA
                {
                    xForcePressureAoA0.Add((float)criticalMachNumber, hypersonicDragForward * 0.4f, 0f, 0f);    //hypersonic drag used as a proxy for effects due to flow separation
                    xForcePressureAoA180.Add((float)criticalMachNumber, (sonicBaseDrag * 0.1f - hypersonicDragBackward * 0.4f), 0f, 0f);

                    xForcePressureAoA0.Add(1f, sonicWaveDrag + hypersonicDragForward * 0.35f, 0f, 0f);     //positive is force forward; negative is force backward
                    xForcePressureAoA180.Add(1f, -sonicWaveDrag - hypersonicDragBackward * 0.35f + sonicBaseDrag, 0f, 0f);
                }
                else if (sonicBaseDrag < 0)
                {
                    xForcePressureAoA0.Add((float)criticalMachNumber, (sonicBaseDrag * 0.1f + hypersonicDragForward * 0.4f), 0f, 0f);
                    xForcePressureAoA180.Add((float)criticalMachNumber, -hypersonicDragBackward * 0.4f, 0f, 0f);

                    xForcePressureAoA0.Add(1f, sonicWaveDrag + hypersonicDragForward * 0.35f + sonicBaseDrag, 0f, 0f);     //positive is force forward; negative is force backward
                    xForcePressureAoA180.Add(1f, -sonicWaveDrag - hypersonicDragBackward * 0.35f, 0f, 0f);
                }
                else
                {
                    xForcePressureAoA0.Add((float)criticalMachNumber, hypersonicDragForward * 0.4f, 0f, 0f);
                    xForcePressureAoA180.Add((float)criticalMachNumber, -hypersonicDragBackward * 0.4f, 0f, 0f);

                    xForcePressureAoA0.Add(1f, sonicWaveDrag + hypersonicDragForward * 0.35f, 0f, 0f);     //positive is force forward; negative is force backward
                    xForcePressureAoA180.Add(1f, -sonicWaveDrag - hypersonicDragBackward * 0.35f, 0f, 0f);
                }

                Vector3 xRefVector;
                if (index == front || index == back)
                    xRefVector = vesselMainAxis;
                else
                {
                    xRefVector = (Vector3)(_vehicleCrossSection[index - 1].centroid - _vehicleCrossSection[index + 1].centroid).normalized;
                    Vector3 offMainAxisVec = Vector3.Exclude(vesselMainAxis, xRefVector);
                    float tanAoA = offMainAxisVec.magnitude / (2f * (float)sectionThickness);
                    if (tanAoA > 0.17632698070846497347109038686862f)
                    {
                        offMainAxisVec.Normalize();
                        offMainAxisVec *= 0.17632698070846497347109038686862f;
                        xRefVector = vesselMainAxis + offMainAxisVec;
                        xRefVector.Normalize();
                    }
                }
                
                Vector3 nRefVector = Matrix4x4.TRS(Vector3.zero, Quaternion.FromToRotation(vesselMainAxis, xRefVector), Vector3.one).MultiplyVector(_vehicleCrossSection[index].flatNormalVector);

                Vector3 centroid = vesselToWorldMatrix.MultiplyPoint3x4(_vehicleCrossSection[index].centroid);
                xRefVector = vesselToWorldMatrix.MultiplyVector(xRefVector);
                nRefVector = vesselToWorldMatrix.MultiplyVector(nRefVector);

                Dictionary<Part, VoxelCrossSection.SideAreaValues> includedPartsAndAreas = _vehicleCrossSection[index].partSideAreaValues;
                List<FARAeroPartModule> includedModules = new List<FARAeroPartModule>();
                List<float> weighting = new List<float>();

                float weightingFactor = 0;

                foreach(KeyValuePair<Part, VoxelCrossSection.SideAreaValues> pair in includedPartsAndAreas)
                {
                    FARAeroPartModule m = pair.Key.GetComponent<FARAeroPartModule>();
                    if (m != null)
                        includedModules.Add(m);
                    weightingFactor += (float)pair.Value.count;
                    weighting.Add((float)pair.Value.count);
                }
                weightingFactor = 1 / weightingFactor;
                for (int j = 0; j < includedModules.Count; j++)
                {
                    weighting[j] *= weightingFactor;
                }
                FARAeroSection section = new FARAeroSection(xForcePressureAoA0, xForcePressureAoA180, xForceSkinFriction, areaChange, viscCrossflowDrag
                    , (float)flatnessRatio, hypersonicMomentForward, hypersonicMomentBackward, 
                    centroid, xRefVector, nRefVector, includedModules, includedPartsAndAreas, weighting);

                _newAeroSections.Add(section);
                tmpAeroModules.UnionWith(includedModules);
            }
            _newAeroModules = tmpAeroModules.ToList();
            _voxel = null;
        }

        private double CalculateHypersonicMoment(double lowArea, double highArea, double sectionThickness)
        {
            double moment = highArea + lowArea - 2 * Math.Sqrt(highArea * lowArea);
            moment = moment / (moment + sectionThickness * sectionThickness * Math.PI);     //calculate sin^2
            if (moment < 0)
                return 0;
            moment = moment * Math.Sqrt(Math.Max(1 - moment, 0));
            moment *= Math.Sqrt(Math.Max(highArea * Math.PI, 0)) + Math.Sqrt(Math.Max(lowArea * Math.PI, 0)) * 2;     //account for radius and factor of 4pi
            moment *= (highArea - lowArea) * 2;     //account for area to act over and Cp max = 2
            return -moment;
        }

        private double CalculateHypersonicDrag(double lowArea, double highArea, double sectionThickness)
        {
            double drag = highArea + lowArea - 2 * Math.Sqrt(highArea * lowArea);
            drag = drag / (drag + sectionThickness * sectionThickness * Math.PI);       //calculate sin^2
            drag *= drag * drag;
            if (drag < 0)
                return 0;
            drag = Math.Sqrt(drag);
            drag *= (lowArea - highArea) * 2;
            return drag;        //force is negative 
        }

        private double CalculateTransonicWaveDrag(int i, int index, int numSections, int front, double sectionThickness, double cutoff)
        {
            double currentSectAreaCrossSection = Math.Min(_vehicleCrossSection[index].areaDeriv2ToNextSection, cutoff);

            if (currentSectAreaCrossSection == 0)       //quick escape for 0 cross-section section drag
                return 0;
            
            double lj2ndTerm = 0;
            double drag = 0;
            int limDoubleDrag = Math.Min(i, numSections - i);
            double sectionThicknessSq = sectionThickness * sectionThickness;

            for (int j = 0; j <= limDoubleDrag; j++)      //section of influence from ahead and behind
            {
                double thisLj = (j + 0.5) * Math.Log(j + 0.5);
                double tmp = thisLj;
                thisLj -= lj2ndTerm;
                lj2ndTerm = tmp;

                tmp = Math.Min(_vehicleCrossSection[index + j].areaDeriv2ToNextSection, cutoff);
                tmp += Math.Min(_vehicleCrossSection[index - j].areaDeriv2ToNextSection, cutoff);

                drag += tmp * thisLj;
            }
            if(i < numSections - i)
            {
                for (int j = 2 * i + 1; j < numSections; j++)
                {
                    double thisLj = (j - i + 0.5) * Math.Log(j - i + 0.5);
                    double tmp = thisLj;
                    thisLj -= lj2ndTerm;
                    lj2ndTerm = tmp;

                    tmp = Math.Min(_vehicleCrossSection[j + front].areaDeriv2ToNextSection, cutoff);

                    drag += tmp * thisLj;
                }
            }
            else if (i > numSections - i)
            {
                for (int j = numSections - 2 * i - 2; j >= 0; j--)
                {
                    double thisLj = (i - j + 0.5) * Math.Log(i - j + 0.5);
                    double tmp = thisLj;
                    thisLj -= lj2ndTerm;
                    lj2ndTerm = tmp;

                    tmp = Math.Min(_vehicleCrossSection[j + front].areaDeriv2ToNextSection, cutoff);

                    drag += tmp * thisLj;
                }
            }

            drag *= sectionThicknessSq;
            drag /= 2 * Math.PI;
            drag *= currentSectAreaCrossSection;
            return -drag;
        }

        private double CalculateCriticalMachNumber(double finenessRatio)
        {
            if (finenessRatio > 10)
                return 0.975;
            if (finenessRatio < 1.5)
                return 0.335;
            if(finenessRatio > 4)
            {
                if (finenessRatio > 6)
                    return 0.00625 * finenessRatio + 0.9125;

                return 0.025 * finenessRatio + 0.8;
            }
            else if( finenessRatio < 3)
                return 0.33 * finenessRatio - 0.16;

            return 0.07 * finenessRatio + 0.62;
        }

        private Vector3 CalculateVesselMainAxis()
        {
            Vector3 axis = Vector3.zero;
            List<Part> vesselPartsList = _vessel.Parts;
            for(int i = 0; i < vesselPartsList.Count; i++)      //get axis by averaging all parts up vectors
            {
                Part p = vesselPartsList[i];
                GeometryPartModule m = p.GetComponent<GeometryPartModule>();
                if(m != null)
                {
                    Bounds b = m.overallMeshBounds;
                    axis += p.transform.up * b.size.x * b.size.y * b.size.z;    //scale part influence by approximate size
                }
            }
            axis.Normalize();   //normalize axis for later calcs
            float dotProd;

            dotProd = Math.Abs(Vector3.Dot(axis, _vessel.transform.up));
            if (dotProd >= 0.99)        //if axis and _vessel.up are nearly aligned, just use _vessel.up
                return Vector3.up;

            dotProd = Math.Abs(Vector3.Dot(axis, _vessel.transform.forward));

            if (dotProd >= 0.99)        //Same for forward...
                return Vector3.forward;

            dotProd = Math.Abs(Vector3.Dot(axis, _vessel.transform.right));

            if (dotProd >= 0.99)        //and right...
                return Vector3.right;

            //Otherwise, now we need to use axis, since it's obviously not close to anything else

            axis = vesselToLocalMatrix.MultiplyVector(axis);

            return axis;
        }
    }
}
