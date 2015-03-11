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

        Thread _runtimeThread = null;
        bool _threadDone = false;
        bool updateModules = true;
        internal bool waitingForUpdate = false;

        Dictionary<Part, FARAeroPartModule> aeroModules = new Dictionary<Part, FARAeroPartModule>();
        double machNumber = 0;

        private void Start()
        {
            _vessel = gameObject.GetComponent<Vessel>();
            VesselUpdate();
            this.enabled = true;
        }

        private void FixedUpdate()
        {
            if (FlightGlobals.ready && _runtimeThread != null)
            {
                machNumber = FARAeroUtil.GetMachNumber(_vessel.mainBody, _vessel.altitude, _vessel.srfSpeed);
                if (waitingForUpdate)
                {
                    while (!updateModules) ;
                    foreach (KeyValuePair<Part, FARAeroPartModule> pair in aeroModules)
                    {
                        if (pair.Value)
                        {

                            pair.Value.updateForces = updateModules;
                            pair.Value.AeroForceUpdate();
                        }
                    }
                    updateModules = false;
                    waitingForUpdate = false;
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
                foreach (Part p in _vessel.Parts)
                {
                    FARAeroPartModule m = p.GetComponent<FARAeroPartModule>();
                    if (m != null)
                    {
                        aeroModules.Add(p, m);
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
                return 125000;
        }

        private void CreateVoxel(object nullObj)
        {
            VehicleVoxel newvoxel = new VehicleVoxel(_vessel.parts, _voxelCount, true, true);

            lock (_vessel)
            {
                _vehicleCrossSection = new VoxelCrossSection[newvoxel.MaxArrayLength];
                for (int i = 0; i < _vehicleCrossSection.Length; i++)
                    _vehicleCrossSection[i].includedParts = new List<Part>();

                _voxel = newvoxel;
            }

            if (_runtimeThread == null)
            {
                _runtimeThread = new Thread(UpdateVesselAeroThreadLoop);
                _runtimeThread.Start();
            }
        }

        private void UpdateVesselAeroThreadLoop()
        {
            while (!_threadDone)
            {
                lock (this)
                {
                    Monitor.Wait(this);
                    try
                    {
                        VesselAeroDataUpdate();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
        }

        private void VesselAeroDataUpdate()
        {
            //Vector3 angVelDiff = _vessel.angularVelocity - lastVesselAngVel;
            //Quaternion angVelRot = Quaternion.AngleAxis(-(_vessel.angularVelocity.magnitude * TimeWarp.fixedDeltaTime) * Mathf.Rad2Deg * 1.4f, _vessel.angularVelocity);
            Vector3d velocity = _vessel.ReferenceTransform.worldToLocalMatrix.MultiplyVector(_vessel.srf_velocity);
            //lastVesselAngVel = _vessel.angularVelocity;
            if ((velocity.x != 0 || velocity.y != 0 || velocity.z != 0) && _vessel.atmDensity > 0)
            {
                int front, back;
                double sectionThickness, maxCrossSectionArea;

                _voxel.CrossSectionData(_vehicleCrossSection, velocity, out front, out back, out sectionThickness, out maxCrossSectionArea);

                Vector3d velNorm = velocity.normalized;

                double lastLj = 0;
                //float vehicleLength = sectionThickness * Math.Abs(front - back);
                //float nonZeroCrossSectionEnd = 0;

                double skinFrictionDragCoefficient = FARAeroUtil.SkinFrictionDrag(_vessel.atmDensity, sectionThickness * (back - front), _vessel.srfSpeed, machNumber, FlightGlobals.getExternalTemperature((float)_vessel.altitude, _vessel.mainBody) + 273.15f);
                double invMaxRadFactor = 1f / Math.Sqrt(maxCrossSectionArea / Math.PI);

                double finenessRatio = sectionThickness * (back - front) * 0.5 * invMaxRadFactor;       //vehicle length / max diameter, as calculated from sect thickness * num sections / (2 * max radius) 

                double viscousDrag = 0;          //used in calculating base drag at any point

                //skin friction and pressure drag for a body, taken from 1978 USAF Stability And Control DATCOM, Section 4.2.3.1, Paragraph A
                double viscousDragFactor = 0;
                if (machNumber < 1.2)
                    viscousDragFactor = 60 / (finenessRatio * finenessRatio * finenessRatio) + 0.0025 * finenessRatio;     //pressure drag for a subsonic / transonic body
                if (machNumber > 1)
                    viscousDragFactor *= (machNumber - 1d) * 5d;          //ensures that this value is only skin friction at Mach > 1.2

                viscousDragFactor++;
                viscousDragFactor *= skinFrictionDragCoefficient;       //all of which is affected by skin friction drag

                viscousDragFactor *= sectionThickness;  //increase per section thickness
                
                for (int j = 0; j <= back - front; j++)
                {
                    VoxelCrossSection currentSection = _vehicleCrossSection[j + front];
                    VoxelCrossSection prevSection;
                    if (j == 0)
                        prevSection = _vehicleCrossSection[j + front];
                    else
                        prevSection = _vehicleCrossSection[j - 1 + front];


                    double nominalDragDivQ = 0d;         //drag, divided by dynamic pressure; will be fed into aeromodules
                    Vector3d nominalLiftDivQ = Vector3d.zero;            //lift at the current AoA

                    double cosAngle;
                    Vector3d liftVecDir;
                    cosAngle = GetCosAoAFromCenterLineAndVel(velNorm, prevSection.centroid, currentSection.centroid, out liftVecDir);

                    //Zero-lift drag calcs
                    //Viscous drag calcs for a body, taken from 1978 USAF Stability And Control DATCOM, Section 4.2.3.1, Paragraph A
                    nominalDragDivQ += SubsonicViscousDrag(j, front, maxCrossSectionArea, invMaxRadFactor, ref viscousDrag, viscousDragFactor, ref currentSection);

                    double slenderBodyFactor = 1d;
                    if (finenessRatio < 4)
                        slenderBodyFactor *= finenessRatio * 0.25;

                    //Supersonic Slender Body theory drag, Mach ~0.8 - ~3.5, based on method of NACA TN 4258
                    if (machNumber > 1.2)
                    {
                        double tmp = 1d;
                        if (machNumber > 3.5)
                            tmp = 1.6 - 0.2 * machNumber;

                        tmp *= slenderBodyFactor;
                        if(machNumber < 8)
                            nominalDragDivQ += SupersonicSlenderBodyDrag(j, front, back, sectionThickness, ref lastLj) * tmp;
                           
                    }
                    else if (machNumber > 0.8)
                    {
                        double tmp = 2.5 * machNumber - 2d;
                        tmp *= slenderBodyFactor;
                        nominalDragDivQ += SupersonicSlenderBodyDrag(j, front, back, sectionThickness, ref lastLj) * tmp;
                    }

                    Vector3d momentDivQ;     //used for additional moments that can't be handled by lift and drag alone

                    //Slender Body Lift calcs (Mach number independent)
                    double nomLiftSlend = 0d;

                    if (machNumber < 8)
                    {
                        nomLiftSlend = currentSection.area - prevSection.area;  //calculate area diff, which is the source of slender body potential lift
                        if (nomLiftSlend > currentSection.area * 0.25)
                            nomLiftSlend = currentSection.area * 0.25;      //assume that maximum area change should only be 25% of current area for increasing
                        if (nomLiftSlend < -currentSection.area * 0.1)
                            nomLiftSlend = -currentSection.area * 0.1;      //assume that maximum area change should only be 10% of cur area for decreasing
                        nomLiftSlend = SlenderBodyLift(cosAngle, nomLiftSlend);
                        if(machNumber > 3)
                        {
                            nomLiftSlend *= 1.6 - 0.2 * machNumber;     //account for reduction at hypersonic velocities
                        }
                    }

                    nominalLiftDivQ = nomLiftSlend * liftVecDir;

                    //pertDragDivQ = pertLiftDivQ * (float)Math.Sqrt(Mathf.Clamp01(1 / (cosAngle * cosAngle) - 1));

                    //Newtonian Impact Calculations
                    double nomDragNewt = 0, nomLiftNewt = 0, pertMomentNewt = 0, pertMomentDampNewt = 0;

                    Vector3d unshadowedLiftVec = Vector3d.zero;
                    double unshadowedAoA = GetCosAoAFromCenterLineAndVel(velNorm, prevSection.additonalUnshadowedCentroid, currentSection.additonalUnshadowedCentroid, out unshadowedLiftVec);

                    NewtonianImpactDrag(out nomDragNewt, out nomLiftNewt, out pertMomentNewt, out pertMomentDampNewt, currentSection.area, currentSection.additionalUnshadowedArea, sectionThickness, machNumber, unshadowedAoA);

                    unshadowedLiftVec = nomLiftNewt * liftVecDir;
                    momentDivQ = Vector3d.Cross(liftVecDir, velNorm) * pertMomentNewt;
                    
                    //Separated flow rearward side calculations
                    double nomDragSep = 0d, nomLiftSep = 0d;

                    Vector3d sepLiftVec;
                    SeparatedFlowDrag(out nomDragSep, out nomLiftSep, currentSection.area, currentSection.removedArea, sectionThickness, machNumber, cosAngle);

                    sepLiftVec = nomLiftSep * liftVecDir;

                    
                    Vector3d forceCenter = currentSection.centroid;
                    double denom = (nominalDragDivQ + nomLiftSlend + nomDragNewt + nomLiftNewt + nomDragSep + nomLiftSep);
                    if (denom != 0)
                    {
                        forceCenter *= (nominalDragDivQ + nomLiftSlend);
                        forceCenter += (nomDragNewt + nomLiftNewt) * currentSection.additonalUnshadowedCentroid + (nomDragSep + nomLiftSep) * currentSection.removedCentroid;
                        forceCenter /= denom;
                    }

                    nominalDragDivQ += nomDragNewt + nomDragSep;

                    nominalLiftDivQ += unshadowedLiftVec + sepLiftVec;

                    double frac = 1d / (double)currentSection.includedParts.Count;

                    nominalDragDivQ *= frac;
                    nominalLiftDivQ *= frac;
                    momentDivQ *= frac;
                    pertMomentDampNewt *= frac;


                    for (int i = 0; i < currentSection.includedParts.Count; i++)
                    {
                        Part p = currentSection.includedParts[i];
                        FARAeroPartModule m;
                        if (aeroModules.TryGetValue(p, out m))
                        {
                            m.IncrementAeroForces(velNorm, forceCenter, (float)nominalDragDivQ, nominalLiftDivQ, momentDivQ, (float)pertMomentDampNewt);
                        }
                    }
                }
            }
            updateModules = true;
        }

        private double SupersonicSlenderBodyDrag(int j, int front, int back, double sectionThickness, ref double lastLj)
        {
            double thisLj = j + 0.5;
            double tmp = Math.Log(thisLj);

            thisLj *= tmp;

            double crossSectionEffect = 0d;
            for (int i = j + front; i <= back; i++)
            {
                double area1, area2;
                area1 = Math.Min(_vehicleCrossSection[i].areaDeriv2ToNextSection, _vehicleCrossSection[i].area * sectionThickness * sectionThickness);
                area2 = Math.Min(_vehicleCrossSection[i - j].areaDeriv2ToNextSection, _vehicleCrossSection[i - j].area * sectionThickness * sectionThickness);
                crossSectionEffect += area1 * area2;
            }
            double dragDivQ = (thisLj - lastLj) * crossSectionEffect * sectionThickness * sectionThickness / Math.PI;
            lastLj = thisLj;

            return dragDivQ;
        }

        private double SubsonicViscousDrag(int j, int front, double maxCrossSectionArea, double invMaxRadFactor, ref double viscousDrag, double viscousDragFactor, ref VoxelCrossSection currentSection)
        {
            double tmp = currentSection.area * Math.PI;
            if (tmp <= 0)
                return 0;

            double sectionViscDrag = viscousDragFactor * 2f * Math.Sqrt(tmp);   //increase in viscous drag due to viscosity

            /*viscousDrag += sectionViscDrag / maxCrossSectionArea;     //keep track of viscous drag for base drag purposes

            if (j > 0 && baseRadius > 0)
            {
                float baseDrag = baseRadius * invMaxRadFactor;     //based on ratio of base diameter to max diameter

                baseDrag *= baseDrag * baseDrag;    //Similarly based on 1978 USAF Stability And Control DATCOM, Section 4.2.3.1, Paragraph A
                baseDrag *= 0.029f;
                baseDrag /= (float)Math.Sqrt(viscousDrag);

                sectionViscDrag += baseDrag * maxCrossSectionArea;     //and bring it up to the same level as all the others
            }*/

            return sectionViscDrag;
        }

        private double SlenderBodyLift(double cosAngle, double areaChange)
        {
            double cos2angle = cosAngle * cosAngle;
            double sin2angle = 1 - cos2angle;
            if (sin2angle < 0)
                sin2angle = 0;
            double nomPotentialLift = 2d * Math.Sqrt(sin2angle) * cosAngle;     //convert cosAngle into sin(2 angle) using cos^2 + sin^2 = 1 to get sin and sin(2x) = 2 sin(x) cos(x)
            nomPotentialLift *= areaChange;

            //pertLiftperQ = (cos2angle - sin2angle);
            //pertLiftperQ *= areaChange;

            return nomPotentialLift;
        }

        private double GetCosAoAFromCenterLineAndVel(Vector3d velNormVector, Vector3d forwardCentroid, Vector3d rearwardCentroid, out Vector3d resultingLiftVec)
        {
            Vector3d centroidChange = forwardCentroid - rearwardCentroid;
            if (centroidChange.IsZero())
            {
                resultingLiftVec = Vector3d.zero;
                return 1;
            }
            centroidChange.Normalize();
            resultingLiftVec = Vector3d.Exclude(velNormVector, centroidChange);
            resultingLiftVec.Normalize();

            return Vector3d.Dot(velNormVector, centroidChange);   //get cos(angle)
        }

        private void NewtonianImpactDrag(out double nomDragDivQ, out double nomLiftDivQ, out double nomMomentDivQ, out double pertMomentDampDivQ,
            double overallArea, double exposedArea, double sectionThickness, double machNumber, double cosAngle)
        {
            double cPmax = 1.86;     //max pressure coefficient, TODO: make function of machNumber

            if (machNumber < 0.8 || exposedArea <= 0 || cosAngle <= 0)
            {
                nomDragDivQ = 0;
                nomLiftDivQ = 0;
                //pertDragDivQ = 0;
                //pertLiftDivQ = 0;
                nomMomentDivQ = 0;
                pertMomentDampDivQ = 0;
                return;
            }
            else if (machNumber < 4)
                cPmax *= (0.3125 * machNumber - 0.25);

            /*cPmax *= exposedArea;
            nomDragDivQ = cPmax;
            nomLiftDivQ = Mathf.Clamp(1 / (cosAngle * cosAngle) - 1, 0, float.PositiveInfinity);
            nomLiftDivQ = cPmax * (float)Math.Sqrt(nomLiftDivQ);

            pertDragDivQ = nomLiftDivQ * cosAngle;
            pertLiftDivQ = cPmax / (cosAngle * cosAngle);

            float tmpDist = (float)Math.Sqrt(exposedArea / Math.PI);
            nomMomentDivQ = -cPmax* tmpDist;
            pertMomentDampNewt = -nomMomentDivQ * tmpDist;*/

            double areaFactor = CalculateAreaFactor(overallArea, exposedArea, sectionThickness);
            double liftAreaFactor = 1d - areaFactor;
            if (liftAreaFactor < 0)
                liftAreaFactor = 0;
            liftAreaFactor = Math.Sqrt(liftAreaFactor);
            nomMomentDivQ = -2d * cPmax * areaFactor * liftAreaFactor * sectionThickness * Math.PI;

            pertMomentDampDivQ = -2d * Math.PI * sectionThickness * BesselFunction1ApproxAboutPi_4(2d * Math.Acos(cosAngle)) * (1d - 2d * areaFactor) * liftAreaFactor;

            cPmax *= exposedArea;
            double sin2Angle = 1d - cosAngle * cosAngle;
            if (sin2Angle < 0)
                sin2Angle = 0;

            nomDragDivQ = areaFactor + 0.6666666667 * sin2Angle;
            nomDragDivQ *= cPmax;

            nomLiftDivQ = cPmax * (0.6666666667 + liftAreaFactor) * Math.Sqrt(sin2Angle) * cosAngle;

            //pertDragDivQ = nomLiftDivQ * 2f;

            //pertLiftDivQ = cPmax * (0.6666666667f + liftAreaFactor) * (cosAngle * cosAngle - sin2Angle);
        }

        private void SeparatedFlowDrag(out double nomDragDivQ, out double nomLiftDivQ,
            double overallArea, double removedArea, double sectionThickness, double machNumber, double cosAngle)
        {
            double cD = 1.2;     //max rearward drag coefficient, TODO: make function of machNumber

            if (machNumber <= 0 || removedArea <= 0)
            {
                nomDragDivQ = 0d;
                nomLiftDivQ = 0d;
                //pertDragDivQ = 0;
                //pertLiftDivQ = 0;
                return;
            }
            cD *= removedArea;

            double normalMach = cosAngle * machNumber;

            if (normalMach > 1)
                cD /= (normalMach * normalMach);

            double areaFactor = CalculateAreaFactor(overallArea, removedArea, sectionThickness);
            double sin2Angle = 1d - cosAngle * cosAngle;
            if (sin2Angle < 0)
                sin2Angle = 0;

            nomDragDivQ = areaFactor + 0.6666666667 * sin2Angle;
            nomDragDivQ *= cD;

            nomLiftDivQ = cD * 0.6666666667 * Math.Sqrt(sin2Angle) * cosAngle;

            //pertDragDivQ = nomLiftDivQ * 2f;

            //pertLiftDivQ = cD * 0.6666666667f * (cosAngle * cosAngle - sin2Angle);
        }

        private double CalculateAreaFactor(double overallArea, double exposedArea, double sectionThickness)
        {
            double areaFactor = overallArea * exposedArea;
            areaFactor = Math.Sqrt(areaFactor) * 2d;
            areaFactor -= exposedArea;
            areaFactor /= Math.PI;
            areaFactor /= (areaFactor + sectionThickness * sectionThickness);
            return Math.Max(areaFactor, 0d);
        }

        private double BesselFunction1ApproxAboutPi_4(double x)
        {
            double value = x * ((-0.0471455 * x - 0.0238978) * x + 0.51399) - 0.00291724;
            return value;
        }
    }
}
