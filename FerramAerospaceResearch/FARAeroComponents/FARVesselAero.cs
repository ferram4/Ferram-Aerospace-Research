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
        int frameCountToUpdate = 0;

        Thread _runtimeThread = null;
        bool _threadDone = false;
        bool updateModules = false;

        Dictionary<Part, FARAeroPartModule> aeroModules = new Dictionary<Part, FARAeroPartModule>();
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
            machNumber = (float)FARAeroUtil.GetMachNumber(_vessel.mainBody, _vessel.altitude, _vessel.srfSpeed);
            //VesselAeroDataUpdate();
            if (frameCountToUpdate > 0)
                frameCountToUpdate--;
            else
            {
                frameCountToUpdate = 0;
                lock (_vessel)
                {
                    Monitor.Pulse(_vessel);
                }
            }
            lock (this)
            {
                foreach (KeyValuePair<Part, FARAeroPartModule> pair in aeroModules)
                {
                    if (pair.Value)
                    {

                        pair.Value.updateForces = updateModules;
                        pair.Value.AeroForceUpdate();
                    }
                }
                updateModules = false;
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
                return 175000;
        }

        private void CreateVoxel(object nullObj)
        {
            VehicleVoxel newvoxel = new VehicleVoxel(_vessel.parts, _voxelCount, true, true);

            lock (_vessel)
            {
                _vehicleCrossSection = new VoxelCrossSection[newvoxel.MaxArrayLength];
                for (int i = 0; i < _vehicleCrossSection.Length; i++)
                    _vehicleCrossSection[i].includedParts = new HashSet<Part>();

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
                lock (_vessel)
                {
                    Monitor.Wait(_vessel);
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
            Quaternion angVelRot = Quaternion.AngleAxis(-(_vessel.angularVelocity.magnitude * TimeWarp.fixedDeltaTime) * Mathf.Rad2Deg, _vessel.angularVelocity);
            Vector3 velocity = _vessel.ReferenceTransform.worldToLocalMatrix.MultiplyVector(angVelRot * (_vessel.srf_velocity + _vessel.acceleration * TimeWarp.fixedDeltaTime));

            if (velocity.x != 0 || velocity.y != 0 || velocity.z != 0 && _vessel.atmDensity >= 0)
            {
                int front, back;
                float sectionThickness, maxCrossSectionArea;

                _voxel.CrossSectionData(_vehicleCrossSection, velocity, out front, out back, out sectionThickness, out maxCrossSectionArea);

                Vector3 velNorm = velocity.normalized;

                float lastLj = 0;
                //float vehicleLength = sectionThickness * Math.Abs(front - back);
                //float nonZeroCrossSectionEnd = 0;

                float skinFrictionDragCoefficient = (float)FARAeroUtil.SkinFrictionDrag(_vessel.atmDensity, sectionThickness * (back - front), _vessel.srfSpeed, machNumber, FlightGlobals.getExternalTemperature((float)_vessel.altitude, _vessel.mainBody) + 273.15f);
                float invMaxRadFactor = 1f / (float)Math.Sqrt(maxCrossSectionArea / (float)Math.PI);

                float finenessRatio = sectionThickness * (back - front) * 0.5f * invMaxRadFactor;       //vehicle length / max diameter, as calculated from sect thickness * num sections / (2 * max radius) 

                float viscousDrag = 0;          //used in calculating base drag at any point

                //skin friction and pressure drag for a body, taken from 1978 USAF Stability And Control DATCOM, Section 4.2.3.1, Paragraph A
                float viscousDragFactor = 0;
                if (machNumber < 1.2)
                    viscousDragFactor = 60 / (finenessRatio * finenessRatio * finenessRatio) + 0.0025f * finenessRatio;     //pressure drag for a subsonic / transonic body
                if (machNumber > 1)
                    viscousDragFactor *= (machNumber - 1) * 5;          //ensures that this value is only skin friction at Mach > 1.2

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


                    float nominalDragDivQ = 0;         //drag, divided by dynamic pressure; will be fed into aeromodules
                    float pertDragDivQ = 0;             //drag from AoA perturbations
                    Vector3 nominalLiftDivQ = Vector3.zero;            //lift at the current AoA
                    float pertLiftDivQ = 0;             //lift from AoA perturbations

                    float baseRadius = (float)Math.Sqrt(currentSection.removedArea / Math.PI) - (float)Math.Sqrt(currentSection.area / Math.PI);

                    float cosAngle;
                    Vector3 liftVecDir;
                    cosAngle = GetCosAoAFromCenterLineAndVel(velNorm, prevSection.centroid, currentSection.centroid, out liftVecDir);

                    //Zero-lift drag calcs
                    //Viscous drag calcs for a body, taken from 1978 USAF Stability And Control DATCOM, Section 4.2.3.1, Paragraph A
                    nominalDragDivQ += SubsonicViscousDrag(j, front, maxCrossSectionArea, invMaxRadFactor, baseRadius, ref viscousDrag, viscousDragFactor, ref currentSection);

                    float slenderBodyFactor = 1;
                    if (finenessRatio < 4)
                        slenderBodyFactor *= finenessRatio * 0.25f;

                    //Supersonic Slender Body theory drag, Mach ~0.8 - ~3.5, based on method of NACA TN 4258
                    if (machNumber > 1.2)
                    {
                        float tmp = 1;
                        if (machNumber > 3.5)
                            tmp = 1.6f - 0.2f * machNumber;

                        tmp *= slenderBodyFactor;
                        if(machNumber < 8)
                            nominalDragDivQ += SupersonicSlenderBodyDrag(j, front, back, sectionThickness, ref lastLj) * tmp;
                           
                    }
                    else if (machNumber > 0.8)
                    {
                        float tmp = 2.5f * machNumber - 2f;
                        tmp *= slenderBodyFactor;
                        nominalDragDivQ += SupersonicSlenderBodyDrag(j, front, back, sectionThickness, ref lastLj) * tmp;
                    }

                    Vector3 momentDivQ;     //used for additional moments that can't be handled by lift and drag alone

                    //Slender Body Lift calcs (Mach number independent)
                    float nomLiftSlend = 0, pertLiftSlend = 0;
                    if(machNumber < 3)
                        nomLiftSlend = SlenderBodyLift(cosAngle, Math.Max(currentSection.area - prevSection.area, currentSection.area * 0.25f), out pertLiftSlend);
                    else if (machNumber < 8)
                    {
                        float hypersonicFactor = 1.6f - 0.2f * machNumber;
                        nomLiftSlend = hypersonicFactor * SlenderBodyLift(cosAngle, Math.Max(currentSection.area - prevSection.area, currentSection.area * 0.25f), out pertLiftSlend);
                        pertLiftDivQ *= hypersonicFactor;
                    }

                    pertLiftDivQ += pertLiftSlend;

                    nominalLiftDivQ = nomLiftSlend * liftVecDir;

                    //pertDragDivQ = pertLiftDivQ * (float)Math.Sqrt(Mathf.Clamp01(1 / (cosAngle * cosAngle) - 1));

                    //Newtonian Impact Calculations
                    float nomDragNewt = 0, pertDragNewt = 0, nomLiftNewt = 0, pertLiftNewt = 0, pertMomentNewt = 0, pertMomentDampNewt = 0;

                    Vector3 unshadowedLiftVec = Vector3.zero;
                    float unshadowedAoA = GetCosAoAFromCenterLineAndVel(velNorm, prevSection.additonalUnshadowedCentroid, currentSection.additonalUnshadowedCentroid, out unshadowedLiftVec);

                    NewtonianImpactDrag(out nomDragNewt, out nomLiftNewt, out pertDragNewt, out pertLiftNewt, out pertMomentNewt, out pertMomentDampNewt, currentSection.area, currentSection.additionalUnshadowedArea, sectionThickness, machNumber, unshadowedAoA);

                    unshadowedLiftVec = nomLiftNewt * liftVecDir;
                    //momentDivQ = Vector3.Cross(liftVecDir, velNorm) * pertMomentNewt;
                    
                    //Separated flow rearward side calculations
                    float nomDragSep = 0, pertDragSep = 0, nomLiftSep = 0, pertLiftSep = 0;

                    Vector3 sepLiftVec;
                    SeparatedFlowDrag(out nomDragSep, out nomLiftSep, out pertDragSep, out pertLiftSep, currentSection.area, currentSection.removedArea, sectionThickness, machNumber, cosAngle);

                    sepLiftVec = nomLiftSep * liftVecDir;

                    
                    Vector3 forceCenter = currentSection.centroid;
                    float denom = (nominalDragDivQ + nomLiftSlend + nomDragNewt + nomLiftNewt + nomDragSep + nomLiftSep);
                    if (denom != 0)
                    {
                        forceCenter *= (nominalDragDivQ + nomLiftSlend);
                        forceCenter += (nomDragNewt + nomLiftNewt) * currentSection.additonalUnshadowedCentroid + (nomDragSep + nomLiftSep) * currentSection.removedCentroid;
                        forceCenter /= denom;
                    }

                    nominalDragDivQ += nomDragNewt + nomDragSep;

                    nominalLiftDivQ += unshadowedLiftVec + sepLiftVec;
                    pertDragDivQ += pertDragNewt + pertDragSep;
                    pertLiftDivQ += pertLiftNewt + pertLiftSep;


                    float frac = 0;
                    foreach (Part p in currentSection.includedParts)
                    {
                        frac++; //make sure to distribute forces properly
                    }
                    frac = 1 / frac;
                    nominalDragDivQ *= frac;
                    pertDragDivQ *= frac;
                    pertMomentNewt *= frac;
                    pertMomentDampNewt *= frac;

                    nominalLiftDivQ *= frac;
                    pertLiftDivQ *= frac;

                    foreach (Part p in currentSection.includedParts)
                    {
                        FARAeroPartModule m;
                        if (aeroModules.TryGetValue(p, out m))
                        {
                            m.IncrementAeroForces(velNorm, forceCenter, nominalDragDivQ, nominalLiftDivQ, Vector3.zero, pertDragDivQ, pertLiftDivQ, pertMomentNewt, pertMomentDampNewt);
                        }
                    }
                }
                lock (this)
                    updateModules = true;
            }
        }

        private float SupersonicSlenderBodyDrag(int j, int front, int back, float sectionThickness, ref float lastLj)
        { 
            float thisLj = j + 0.5f;
            float tmp = ICSILog.Log(thisLj);

            thisLj *= tmp;

            float crossSectionEffect = 0;
            for (int i = j + front; i <= back; i++)
            {
                float area1, area2;
                area1 = Math.Min(_vehicleCrossSection[i].areaDeriv2ToNextSection, _vehicleCrossSection[i].area * sectionThickness * sectionThickness);
                area2 = Math.Min(_vehicleCrossSection[i - j].areaDeriv2ToNextSection, _vehicleCrossSection[i - j].area * sectionThickness * sectionThickness);
                crossSectionEffect += area1 * area2;
            }
            float dragDivQ = (thisLj - lastLj) * crossSectionEffect * sectionThickness * sectionThickness / (float)Math.PI;
            lastLj = thisLj;

            return dragDivQ;
        }

        private float SubsonicViscousDrag(int j, int front, float maxCrossSectionArea, float invMaxRadFactor, float baseRadius, ref float viscousDrag, float viscousDragFactor, ref VoxelCrossSection currentSection)
        {
            float sectionViscDrag = viscousDragFactor * 2f * (float)Math.Sqrt(Math.PI * currentSection.area);   //increase in viscous drag due to viscosity
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

        private float SlenderBodyLift(float cosAngle, float areaChange, out float pertLiftperQ)
        {
            float cos2angle = cosAngle * cosAngle;
            float sin2angle = Mathf.Clamp01(1 - cos2angle);
            float nomPotentialLift = 2f * (float)Math.Sqrt(sin2angle) * cosAngle;     //convert cosAngle into sin(2 angle) using cos^2 + sin^2 = 1 to get sin and sin(2x) = 2 sin(x) cos(x)
            nomPotentialLift *= areaChange;

            pertLiftperQ = (cos2angle - sin2angle);
            pertLiftperQ *= areaChange;

            return nomPotentialLift;
        }

        private float GetCosAoAFromCenterLineAndVel(Vector3 velNormVector, Vector3 forwardCentroid, Vector3 rearwardCentroid, out Vector3 resultingLiftVec)
        {
            Vector3 centroidChange = forwardCentroid - rearwardCentroid;
            if (centroidChange.IsZero())
            {
                resultingLiftVec = Vector3.zero;
                return 1;
            }
            centroidChange.Normalize();
            float cosAngle = Vector3.Dot(velNormVector, centroidChange);   //get cos(angle)

            resultingLiftVec = Vector3.Exclude(velNormVector, centroidChange);
            resultingLiftVec.Normalize();

            return cosAngle;
        }

        private void NewtonianImpactDrag(out float nomDragDivQ, out float nomLiftDivQ, out float pertDragDivQ, out float pertLiftDivQ, out float nomMomentDivQ, out float pertMomentDampDivQ,
            float overallArea, float exposedArea, float sectionThickness, float machNumber, float cosAngle)
        {
            float cPmax = 1.86f;     //max pressure coefficient, TODO: make function of machNumber

            if (machNumber < 0.8f || exposedArea <= 0 || cosAngle <= 0)
            {
                nomDragDivQ = 0;
                nomLiftDivQ = 0;
                pertDragDivQ = 0;
                pertLiftDivQ = 0;
                nomMomentDivQ = 0;
                pertMomentDampDivQ = 0;
                return;
            }
            else if (machNumber < 4f)
                cPmax *= (0.3125f * machNumber - 0.25f);

            /*cPmax *= exposedArea;
            nomDragDivQ = cPmax;
            nomLiftDivQ = Mathf.Clamp(1 / (cosAngle * cosAngle) - 1, 0, float.PositiveInfinity);
            nomLiftDivQ = cPmax * (float)Math.Sqrt(nomLiftDivQ);

            pertDragDivQ = nomLiftDivQ * cosAngle;
            pertLiftDivQ = cPmax / (cosAngle * cosAngle);

            float tmpDist = (float)Math.Sqrt(exposedArea / Math.PI);
            nomMomentDivQ = -cPmax* tmpDist;
            pertMomentDampNewt = -nomMomentDivQ * tmpDist;*/
            
            float areaFactor = CalculateAreaFactor(overallArea, exposedArea, sectionThickness);
            float liftAreaFactor = areaFactor * (float)Math.Sqrt(Mathf.Clamp01(1 - areaFactor));
            nomMomentDivQ = -2 * cPmax * liftAreaFactor * sectionThickness * (float)Math.PI;

            pertMomentDampDivQ = -nomMomentDivQ * sectionThickness * 0.5f;

            cPmax *= exposedArea;
            float sin2Angle = Mathf.Clamp01(1 - cosAngle * cosAngle);

            nomDragDivQ = areaFactor + 0.6666666667f * sin2Angle;
            nomDragDivQ *= cPmax;

            nomLiftDivQ = cPmax * (0.6666666667f + liftAreaFactor) * (float)Math.Sqrt(sin2Angle) * cosAngle;

            pertDragDivQ = nomLiftDivQ * 2f;

            pertLiftDivQ = cPmax * (0.6666666667f + liftAreaFactor) * (cosAngle * cosAngle - sin2Angle);
        }

        private void SeparatedFlowDrag(out float nomDragDivQ, out float nomLiftDivQ, out float pertDragDivQ, out float pertLiftDivQ,
            float overallArea, float removedArea, float sectionThickness, float machNumber, float cosAngle)
        {
            float cD = 1.2f;     //max rearward drag coefficient, TODO: make function of machNumber

            if (machNumber <= 0f || removedArea <= 0)
            {
                nomDragDivQ = 0;
                nomLiftDivQ = 0;
                pertDragDivQ = 0;
                pertLiftDivQ = 0;
                return;
            }
            cD *= removedArea;

            float normalMach = cosAngle * machNumber;

            if (normalMach > 1)
                cD /= (normalMach * normalMach);

            float areaFactor = CalculateAreaFactor(overallArea, removedArea, sectionThickness);
            float sin2Angle = Mathf.Clamp01(1 - cosAngle * cosAngle);

            nomDragDivQ = areaFactor + 0.6666666667f * sin2Angle;
            nomDragDivQ *= cD;

            nomLiftDivQ = cD * 0.6666666667f * (float)Math.Sqrt(sin2Angle) * cosAngle;

            pertDragDivQ = nomLiftDivQ * 2f;

            pertLiftDivQ = cD * 0.6666666667f * (cosAngle * cosAngle - sin2Angle);
        }

        private float CalculateAreaFactor(float overallArea, float exposedArea, float sectionThickness)
        {
            float areaFactor = overallArea * exposedArea;
            areaFactor = (float)Math.Sqrt(areaFactor) * 2;
            areaFactor -= exposedArea;
            areaFactor /= (float)Math.PI;
            areaFactor /= (areaFactor + sectionThickness * sectionThickness);
            return Math.Max(areaFactor, 0f);
        }
    }
}
