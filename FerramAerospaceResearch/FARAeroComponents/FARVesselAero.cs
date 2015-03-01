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

        Dictionary<Part, FARAeroPartModule> aeroModules = new Dictionary<Part, FARAeroPartModule>();
        float machNumber = 0;

        bool updateModules = false;

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

            if (frameCountToUpdate > 0)
                frameCountToUpdate--;
            else
            {
                frameCountToUpdate = 1;
                lock (_vessel)
                {
                    Monitor.Pulse(_vessel);
                }
            }

            if(updateModules)
            {
                updateModules = false;
                foreach (KeyValuePair<Part, FARAeroPartModule> pair in aeroModules)
                {
                    FARAeroPartModule m = pair.Value;
                    m.updateForces = true;
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
                return 250000;
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
                    updateModules = true;
                }
            }
        }

        private void VesselAeroDataUpdate()
        {

            int front, back;
            float sectionThickness, maxCrossSectionArea;

            Vector3 velocity = _vessel.transform.worldToLocalMatrix.MultiplyVector(_vessel.srf_velocity);
            if (velocity.x != 0 || velocity.y != 0 || velocity.z != 0)
            {
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
                    cosAngle = GetCosAoAFromCenterLineAndVel(velNorm, prevSection.centroid, currentSection.centroid, out nominalLiftDivQ);


                    //Zero-lift drag calcs
                    //Viscous drag calcs for a body, taken from 1978 USAF Stability And Control DATCOM, Section 4.2.3.1, Paragraph A
                    nominalDragDivQ += SubsonicViscousDrag(j, front, maxCrossSectionArea, invMaxRadFactor, baseRadius, ref viscousDrag, viscousDragFactor, ref currentSection);

                    //Supersonic Slender Body theory drag, Mach ~0.8 - ~3.5, based on method of NACA TN 4258
                    if (machNumber > 1.2)
                        nominalDragDivQ += SupersonicSlenderBodyDrag(j, front, back, sectionThickness, ref lastLj);
                    else if (machNumber > 0.8)
                    {
                        float tmp = 2.5f * machNumber - 2f;
                        nominalDragDivQ += SupersonicSlenderBodyDrag(j, front, back, sectionThickness, ref lastLj) * tmp;
                    }

                    //Slender Body Lift calcs (Mach number independent)
                    float nomLiftSlend, pertLiftSlend;
                    nomLiftSlend = SlenderBodyLift(cosAngle, Math.Min(currentSection.area - prevSection.area, maxCrossSectionArea * 0.1f), out pertLiftSlend);
                    pertLiftDivQ += pertLiftSlend;

                    nominalLiftDivQ *= (nomLiftSlend);

                    pertDragDivQ = pertLiftDivQ * (float)Math.Sqrt(Mathf.Clamp01(1 / (cosAngle * cosAngle) - 1));

                    //Newtonian Impact Calculations
                    float nomDragNewt, pertDragNewt, nomLiftNewt, pertLiftNewt;

                    Vector3 unshadowedLiftVec;
                    float unshadowedAoA = GetCosAoAFromCenterLineAndVel(velNorm, prevSection.additonalUnshadowedCentroid, currentSection.additonalUnshadowedCentroid, out unshadowedLiftVec);

                    NewtonianImpactDrag(out nomDragNewt, out nomLiftNewt, out pertDragNewt, out pertLiftNewt, currentSection.area, currentSection.additionalUnshadowedArea, sectionThickness, machNumber, unshadowedAoA);

                    unshadowedLiftVec = nomLiftNewt * unshadowedLiftVec;

                    float nomDragSep = 0, pertDragSep = 0, nomLiftSep = 0, pertLiftSep = 0;

                    Vector3 sepLiftVec;
                    float sepAoA = GetCosAoAFromCenterLineAndVel(velNorm, prevSection.removedCentroid, currentSection.removedCentroid, out sepLiftVec);

                    SeparatedFlowDrag(out nomDragSep, out nomLiftSep, out pertDragSep, out pertLiftSep, currentSection.area, currentSection.removedArea, sectionThickness, machNumber, sepAoA);

                    sepLiftVec = nomLiftSep * sepLiftVec;

                    float denom = (nominalDragDivQ + nomLiftSlend + nomDragNewt + nomLiftNewt + nomDragSep + nomLiftSep);
                    Vector3 forceCenter = currentSection.centroid;
                    if (denom != 0)
                    {
                        forceCenter *= (nominalDragDivQ + nomLiftSlend);
                        forceCenter += (nomDragNewt + nomLiftNewt) * currentSection.additonalUnshadowedCentroid + (nomDragSep + nomLiftSep) * currentSection.removedCentroid;
                        forceCenter /= denom;
                    }

                    nominalDragDivQ += nomDragNewt + nomLiftSep;

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

                    nominalLiftDivQ *= frac;
                    pertLiftDivQ *= frac;

                    foreach (Part p in currentSection.includedParts)
                    {
                        FARAeroPartModule m;
                        if (aeroModules.TryGetValue(p, out m))
                        {
                            m.IncrementAeroForces(velNorm, forceCenter, nominalDragDivQ, nominalLiftDivQ, pertDragDivQ, pertLiftDivQ);
                        }
                    }
                }
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
            float nomPotentialLift = 2f * (float)Math.Sqrt(Mathf.Clamp01(1 - cosAngle * cosAngle)) * cosAngle;     //convert cosAngle into sin(2 angle) using cos^2 + sin^2 = 1 to get sin and sin(2x) = 2 sin(x) cos(x)
            nomPotentialLift *= areaChange;

            pertLiftperQ = 2f * cosAngle;
            pertLiftperQ *= areaChange;

            return nomPotentialLift;
        }

        private float GetCosAoAFromCenterLineAndVel(Vector3 velVector, Vector3 forwardCentroid, Vector3 rearwardCentroid, out Vector3 resultingLiftVec)
        {
            Vector3 centroidChange = forwardCentroid - rearwardCentroid;
            centroidChange.Normalize();
            float cosAngle = Vector3.Dot(velVector, centroidChange);   //get cos(angle)

            resultingLiftVec = Vector3.Exclude(velVector, centroidChange);
            resultingLiftVec.Normalize();

            return cosAngle;
        }

        private void NewtonianImpactDrag(out float nomDragDivQ, out float nomLiftDivQ, out float pertDragDivQ, out float pertLiftDivQ,
            float overallArea, float exposedArea, float sectionThickness, float machNumber, float cosAngle)
        {
            float cPmax = 1.86f;     //max pressure coefficient, TODO: make function of machNumber

            if (machNumber < 0.8f)
            {
                nomDragDivQ = 0;
                nomLiftDivQ = 0;
                pertDragDivQ = 0;
                pertLiftDivQ = 0;
                return;
            }
            else if (machNumber < 4f)
                cPmax *= (0.3125f * machNumber - 0.25f);

            cPmax *= exposedArea;

            float areaFactor = CalculateAreaFactor(overallArea, exposedArea, sectionThickness);

            nomDragDivQ = (float)Math.Sqrt(areaFactor);
            nomDragDivQ *= nomDragDivQ * nomDragDivQ;
            nomDragDivQ *= cPmax;

            float sin2Angle = 1 - cosAngle * cosAngle;

            nomLiftDivQ = (areaFactor * (float)Math.PI + 0.666666666666666667f) * cPmax;

            pertLiftDivQ = nomLiftDivQ * 2f * cosAngle * (float)Math.Sqrt(Mathf.Clamp01(sin2Angle));

            nomLiftDivQ *= sin2Angle;

            pertDragDivQ = nomLiftDivQ * 2f;
        }

        private void SeparatedFlowDrag(out float nomDragDivQ, out float nomLiftDivQ, out float pertDragDivQ, out float pertLiftDivQ,
            float overallArea, float removedArea, float sectionThickness, float machNumber, float cosAngle)
        {
            float cD = 0.3f;     //max rearward drag coefficient, TODO: make function of machNumber

            if (machNumber <= 0f)
            {
                nomDragDivQ = 0;
                nomLiftDivQ = 0;
                pertDragDivQ = 0;
                pertLiftDivQ = 0;
                return;
            }

            if (machNumber > 1f)
                cD /= machNumber * machNumber;

            cD *= removedArea;

            float areaFactor = CalculateAreaFactor(overallArea, removedArea, sectionThickness);

            nomDragDivQ = (float)Math.Sqrt(areaFactor);
            nomDragDivQ *= nomDragDivQ * nomDragDivQ;
            nomDragDivQ *= cD;

            float sin2Angle = 1 - cosAngle * cosAngle;

            nomLiftDivQ = (areaFactor * (float)Math.PI + 0.666666666666666667f) * cD;

            pertLiftDivQ = nomLiftDivQ * 2f * cosAngle * (float)Math.Sqrt(Mathf.Clamp01(sin2Angle));

            nomLiftDivQ *= sin2Angle;

            pertDragDivQ = nomLiftDivQ * 2f;
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
