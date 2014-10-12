/*
Ferram Aerospace Research v0.14.2
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
using UnityEngine;

namespace ferram4
{
    public class FARWingInteraction
    {
        private FARWingAerodynamicModel parentWingModule;
        private Transform parentWingTransform;
        private Part parentWingPart;
        private Vector3 rootChordMidLocal;
        private Vector3 rootChordMidPt;
        private short srfAttachFlipped;

        private FARWingAerodynamicModel[] nearbyWingModulesForward;
        private FARWingAerodynamicModel[] nearbyWingModulesBackward;
        private FARWingAerodynamicModel[] nearbyWingModulesLeftward;
        private FARWingAerodynamicModel[] nearbyWingModulesRightward;

        private Dictionary<FARWingAerodynamicModel, double> nearbyUpstreamWingModulesAndInfluenceFactors = new Dictionary<FARWingAerodynamicModel, double>();
        private Vector3d previousParallelInPlaneLocal;
        private int upstreamRecalculationCount = 10;

        private double forwardExposure;
        private double backwardExposure;
        private double leftwardExposure;
        private double rightwardExposure;

        private double effectiveUpstreamMAC;
        private double effectiveUpstreamb_2;
        private double effectiveUpstreamLiftSlope;
        private double effectiveUpstreamArea;
        private double effectiveUpstreamStall;
        private double effectiveUpstreamCosSweepAngle;
        private double effectiveUpstreamAoAMax;
        private double effectiveUpstreamCd0;

        public double EffectiveUpstreamMAC { get { return effectiveUpstreamMAC; } private set { effectiveUpstreamMAC = value; } }
        public double EffectiveUpstreamb_2 { get { return effectiveUpstreamb_2; } private set { effectiveUpstreamb_2 = value; } }
        public double EffectiveUpstreamLiftSlope { get { return effectiveUpstreamLiftSlope; } private set { effectiveUpstreamLiftSlope = value; } }
        public double EffectiveUpstreamArea { get { return effectiveUpstreamArea; } private set { effectiveUpstreamArea = value; } }
        public double EffectiveUpstreamStall { get { return effectiveUpstreamStall; } private set { effectiveUpstreamStall = value; } }
        public double EffectiveUpstreamCosSweepAngle { get { return effectiveUpstreamCosSweepAngle; } private set { effectiveUpstreamCosSweepAngle = value; } }
        public double EffectiveUpstreamAoAMax { get { return effectiveUpstreamAoAMax; } private set { effectiveUpstreamAoAMax = value; } }
        public double EffectiveUpstreamCd0 { get { return effectiveUpstreamCd0; } private set { effectiveUpstreamCd0 = value; } }


        private static FloatCurve wingCamberFactor = null;
        private static FloatCurve wingCamberMoment = null;

        private double aRFactor = 1;
        private double clInterferenceFactor = 1;

        public double ARFactor
        {
            get { return aRFactor; }
            private set { aRFactor = value; }
        }

        public double ClInterferenceFactor
        {
            get { return clInterferenceFactor; }
            private set { clInterferenceFactor = value; }
        }
        
        public FARWingInteraction(FARWingAerodynamicModel parentModule, Part parentPart, Transform partTransform, Vector3 rootChordMid, short srfAttachNegative)
        {
            parentWingModule = parentModule;
            parentWingPart = parentPart;
            parentWingTransform = partTransform;
            rootChordMidLocal = rootChordMid;
            srfAttachFlipped = srfAttachNegative;

            if (wingCamberFactor == null)
            {
                wingCamberFactor = new FloatCurve();
                wingCamberFactor.Add(0, 0);
                for (double i = 0.1; i <= 0.9; i += 0.1)
                {
                    double tmp = i * 2;
                    tmp--;
                    tmp = Math.Acos(tmp);

                    tmp = tmp - Math.Sin(tmp);
                    tmp /= Math.PI;
                    tmp = 1 - tmp;

                    wingCamberFactor.Add((float)i, (float)tmp);
                }
                wingCamberFactor.Add(1, 1);
            }

            if (wingCamberMoment == null)
            {
                wingCamberMoment = new FloatCurve();
                for (double i = 0; i <= 1; i += 0.1)
                {
                    double tmp = i * 2;
                    tmp--;
                    tmp = Math.Acos(tmp);

                    tmp = (Math.Sin(2 * tmp) - 2 * Math.Sin(tmp)) / (8 * (Math.PI - tmp + Math.Sin(tmp)));

                    wingCamberMoment.Add((float)i, (float)tmp);
                }
            }

        }

        /// <summary>
        /// Recalculates all nearby wings; call when vessel has changed shape
        /// </summary>
        /// <param name="VesselPartList">A list of all parts on this vessel</param>
        /// <param name="isSmallSrf">If a part should be considered a small attachable surface, like an aileron, elevator, etc; used to calculate nearby wings properly</param>
        public void UpdateWingInteraction(List<Part> VesselPartList, bool isSmallSrf)
        {
            float flt_MAC = (float)parentWingModule.MAC;
            float flt_b_2 = (float)parentWingModule.b_2;

            rootChordMidPt = parentWingPart.transform.position + parentWingTransform.TransformDirection(rootChordMidLocal);

            if(isSmallSrf)
            {
                forwardExposure = ExposureSmallSrf(out nearbyWingModulesForward, parentWingPart.transform.up, VesselPartList, flt_b_2, flt_MAC);

                backwardExposure = ExposureSmallSrf(out nearbyWingModulesBackward , -parentWingPart.transform.up, VesselPartList, flt_b_2, flt_MAC);

                leftwardExposure = ExposureSmallSrf(out nearbyWingModulesLeftward, -parentWingPart.transform.right, VesselPartList, flt_b_2, flt_MAC);

                rightwardExposure = ExposureSmallSrf(out nearbyWingModulesRightward, parentWingPart.transform.right, VesselPartList, flt_b_2, flt_MAC);
            }
            else
            {
                forwardExposure = ExposureInChordDirection(out nearbyWingModulesForward, parentWingPart.transform.up, VesselPartList, flt_b_2, flt_MAC);

                backwardExposure = ExposureInChordDirection(out nearbyWingModulesBackward , -parentWingPart.transform.up, VesselPartList, flt_b_2, flt_MAC);

                leftwardExposure = ExposureInSpanDirection(out nearbyWingModulesLeftward, -parentWingPart.transform.right, VesselPartList, flt_b_2, flt_MAC);

                rightwardExposure = ExposureInSpanDirection(out nearbyWingModulesRightward, parentWingPart.transform.right, VesselPartList, flt_b_2, flt_MAC);
            }

            //This part handles effects of biplanes, triplanes, etc.
            double ClCdInterference = 1;
            ClCdInterference *= WingInterference(parentWingPart.transform.forward, VesselPartList, flt_b_2);
            ClCdInterference *= WingInterference(-parentWingPart.transform.forward, VesselPartList, flt_b_2);

            ClInterferenceFactor = ClCdInterference;
        }

        private double WingInterference(Vector3 rayDirection, List<Part> PartList, float dist)
        {
            double interferencevalue = 1;

            Ray ray = new Ray();

            ray.origin = parentWingModule.WingCentroid();
            ray.direction = rayDirection;

            RaycastHit hit = new RaycastHit();

            bool gotSomething = false;

            hit.distance = 0;
            RaycastHit[] hits = Physics.RaycastAll(ray, dist, FARAeroUtil.RaycastMask);
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                if (h.collider != null)
                {
                    for (int j = 0; j < PartList.Count; j++)
                    {
                        Part p = PartList[j];

                        if (p == parentWingPart)
                            continue;

                        Collider[] colliders;
                        try
                        {
                            colliders = p.GetComponentsInChildren<Collider>();
                        }
                        catch (Exception e)
                        {
                            //Fail silently because it's the only way to avoid issues with pWings
                            //Debug.LogException(e);
                            colliders = new Collider[1] { p.collider };
                        }
                        if (p.Modules.Contains("FARWingAerodynamicModel"))
                        {
                            for (int k = 0; k < colliders.Length; k++)
                                if (h.collider == colliders[k] && h.distance > 0)
                                {

                                    double tmp = h.distance / dist;
                                    tmp = FARMathUtil.Clamp(tmp, 0, 1);
                                    interferencevalue = Math.Min(tmp, interferencevalue);
                                    gotSomething = true;

                                    break;
                                }
                        }
                        if (gotSomething)
                            break;
                    }
                }
            }
            return interferencevalue;
        }

        #region StandardWingExposureDetection
        private double ExposureInChordDirection(out FARWingAerodynamicModel[] nearbyWings, Vector3 rayDirection, List<Part> vesselPartList, float b_2, float MAC)
        {
            Ray ray = new Ray();
            ray.direction = rayDirection;

            RaycastHit hit = new RaycastHit();

            nearbyWings = new FARWingAerodynamicModel[5];

            double exposure = 1;
            for (int i = 0; i < 5; i++)
            {
                ray.origin = rootChordMidPt - (float)(b_2 * (i * 0.2 + 0.1)) * parentWingPart.transform.right.normalized * srfAttachFlipped;

                hit.distance = 0;
                RaycastHit[] hits = Physics.RaycastAll(ray, b_2, FARAeroUtil.RaycastMask);
                bool gotSomething = false;
                for (int j = 0; j < hits.Length; j++)
                {
                    RaycastHit h = hits[j];
                    if (h.collider != null)
                    {
                        for (int k = 0; k < vesselPartList.Count; k++)
                        {
                            Part p = vesselPartList[k];
                            if (p == parentWingPart)
                                continue;

                            Collider[] colliders;
                            try
                            {
                                colliders = p.GetComponentsInChildren<Collider>();
                            }
                            catch (Exception e)
                            {
                                //Fail silently because it's the only way to avoid issues with pWings
                                //Debug.LogException(e);
                                colliders = new Collider[1] { p.collider };
                            }
                            for (int l = 0; l < colliders.Length; l++)
                                if (h.collider == colliders[l] && h.distance > 0)
                                {
                                    exposure -= 0.2;

                                    if (p.Modules.Contains("FARWingAerodynamicModel"))
                                    {
                                        FARWingAerodynamicModel hitModule = (FARWingAerodynamicModel)p.Modules["FARWingAerodynamicModel"];
                                        nearbyWings[i] = hitModule;
                                    }
                                    else
                                        nearbyWings[i] = null;

                                    gotSomething = true;

                                    break;
                                }
                            if (gotSomething)
                                break;
                        }
                    }
                    if (gotSomething)
                        break;
                }
            }

            return exposure;
        }

        private double ExposureInSpanDirection(out FARWingAerodynamicModel[] nearbyWings, Vector3 rayDirection, List<Part> vesselPartList, float b_2, float MAC)
        {
            Ray ray = new Ray();
            ray.direction = rayDirection;

            RaycastHit hit = new RaycastHit();

            nearbyWings = new FARWingAerodynamicModel[5];

            double exposure = 1;
            for (int i = 0; i < 5; i++)
            {
                ray.origin = rootChordMidPt + (float)(MAC * i * 0.25 - (MAC * 0.5)) * parentWingPart.transform.up.normalized * 0.8f;
                ray.origin -= (float)(b_2 * 0.5) * parentWingPart.transform.right.normalized * srfAttachFlipped;

                hit.distance = 0;
                RaycastHit[] hits = Physics.RaycastAll(ray, b_2, FARAeroUtil.RaycastMask);
                bool gotSomething = false;
                for (int j = 0; j < hits.Length; j++)
                {
                    RaycastHit h = hits[j];
                    if (h.collider != null)
                    {
                        for (int k = 0; k < vesselPartList.Count; k++)
                        {
                            Part p = vesselPartList[k];
                            if (p == parentWingPart)
                                continue;

                            Collider[] colliders;
                            try
                            {
                                colliders = p.GetComponentsInChildren<Collider>();
                            }
                            catch (Exception e)
                            {
                                //Fail silently because it's the only way to avoid issues with pWings
                                //Debug.LogException(e);
                                colliders = new Collider[1] { p.collider };
                            }
                            for (int l = 0; l < colliders.Length; l++)
                                if (h.collider == colliders[l] && h.distance > 0)
                                {
                                    exposure -= 0.2;

                                    if (p.Modules.Contains("FARWingAerodynamicModel"))
                                    {
                                        FARWingAerodynamicModel hitModule = (FARWingAerodynamicModel)p.Modules["FARWingAerodynamicModel"];
                                        nearbyWings[i] = hitModule;
                                    }
                                    else
                                        nearbyWings[i] = null;

                                    gotSomething = true;
                                    break;
                                }
                            if (gotSomething)
                                break;
                        }
                    }
                    if (gotSomething)
                        break;
                }
            }

            return exposure;
        }
        #endregion

        #region SmallSrfExposureDetection

        private double ExposureSmallSrf(out FARWingAerodynamicModel[] nearbyWings, Vector3 rayDirection, List<Part> vesselPartList, float b_2, float MAC)
        {
            Ray ray = new Ray();
            ray.direction = rayDirection;

            RaycastHit hit = new RaycastHit();

            nearbyWings = new FARWingAerodynamicModel[1];

            double exposure = 1;
            ray.origin = rootChordMidPt - (float)(MAC * 0.7) * parentWingPart.transform.up.normalized;

            hit.distance = 0;
            RaycastHit[] hits = Physics.RaycastAll(ray.origin, ray.direction, b_2, FARAeroUtil.RaycastMask);
            bool gotSomething = false;
            for (int j = 0; j < hits.Length; j++)
            {
                RaycastHit h = hits[j];
                if (h.collider != null)
                {
                    for (int k = 0; k < vesselPartList.Count; k++)
                    {
                        Part p = vesselPartList[k];
                        if (p == parentWingPart)
                            continue;

                        Collider[] colliders;
                        try
                        {
                            colliders = p.GetComponentsInChildren<Collider>();
                        }
                        catch (Exception e)
                        {
                            //Fail silently because it's the only way to avoid issues with pWings
                            //Debug.LogException(e);
                            colliders = new Collider[1] { p.collider };
                        }
                        for (int l = 0; l < colliders.Length; l++)
                            if (h.collider == colliders[l] && h.distance > 0)
                            {
                                exposure -= 1;

                                if (p.Modules.Contains("FARWingAerodynamicModel"))
                                {
                                    FARWingAerodynamicModel hitModule = (FARWingAerodynamicModel)p.Modules["FARWingAerodynamicModel"];
                                    nearbyWings[0] = hitModule;
                                }
                                else
                                    nearbyWings[0] = null;

                                gotSomething = true;

                                break;
                            }
                        if (gotSomething)
                            break;
                    }
                }
                if (gotSomething)
                    break;
            }


            return exposure;
        }
        #endregion

        public bool HasWingsUpstream()
        {
            return nearbyUpstreamWingModulesAndInfluenceFactors.Count > 0;
        }

        private void UpdateStaticEffectiveUpstreamValues()
        {
            effectiveUpstreamMAC = 0;
            effectiveUpstreamb_2 = 0;
            effectiveUpstreamArea = 0;

            foreach (KeyValuePair<FARWingAerodynamicModel, double> pair in nearbyUpstreamWingModulesAndInfluenceFactors)
            {
                effectiveUpstreamMAC += pair.Key.GetMAC() * pair.Value;
                effectiveUpstreamb_2 += pair.Key.Getb_2() * pair.Value;
                effectiveUpstreamArea += pair.Key.S * pair.Value;
            }
        }

        /// <summary>
        /// Accounts for increments in lift due to camber changes from upstream wings, and returns changes for this wing part; returns true if there are wings in front of it
        /// </summary>
        /// <param name="thisWingAoA">AoA of this wing in rad</param>
        /// <param name="thisWingMachNumber">Mach Number of this wing in rad</param>
        /// <param name="ACWeight">Weighting value for applying ACshift</param>
        /// <param name="ACShift">Value used to shift the wing AC due to interactive effects</param>
        /// <param name="ClIncrementFromRear">Increase in Cl due to this</param>
        /// <returns></returns>
        public void CalculateEffectsOfUpstreamWing(double thisWingAoA, double thisWingMachNumber, 
            ref double ACweight, ref double ACshift, ref double ClIncrementFromRear)
        {
            double effectiveUpstreamAngle = 0;
            double thisWingMAC, thisWingb_2;

            thisWingMAC = parentWingModule.GetMAC();
            thisWingb_2 = parentWingModule.Getb_2();

            effectiveUpstreamLiftSlope = 0;
            effectiveUpstreamStall = 0;
            effectiveUpstreamCosSweepAngle = 0;
            effectiveUpstreamAoAMax = 0;
            effectiveUpstreamCd0 = 0;

            foreach(KeyValuePair<FARWingAerodynamicModel, double> pair in nearbyUpstreamWingModulesAndInfluenceFactors)
            {
                double tmp = Vector3d.Dot(pair.Key.GetLiftDirection(), parentWingModule.GetLiftDirection());

                effectiveUpstreamLiftSlope += pair.Key.GetLiftSlope() * pair.Value * Math.Abs(tmp);
                effectiveUpstreamStall += pair.Key.GetStall() * pair.Value * Math.Abs(tmp);
                effectiveUpstreamCosSweepAngle += pair.Key.GetCosSweepAngle() * pair.Value * Math.Abs(tmp);
                effectiveUpstreamAoAMax += pair.Key.AoAmax * pair.Value * Math.Abs(tmp);
                effectiveUpstreamCd0 += pair.Key.GetCd0() * pair.Value * Math.Abs(tmp);

                double wAoA = pair.Key.CalculateAoA(pair.Key.GetVelocity()) * Math.Sign(tmp);
                tmp = (thisWingAoA - wAoA) * Math.Abs(tmp);                //First, make sure that the AoA are wrt the same direction; then account for any strange angling of the part that shouldn't be there

                effectiveUpstreamAngle += tmp;
            }

            double MachCoeff = FARMathUtil.Clamp(1 - thisWingMachNumber * thisWingMachNumber, 0, 1);

            if (MachCoeff != 0)
            {
                double flapRatio = FARMathUtil.Clamp(thisWingMAC / (thisWingMAC + effectiveUpstreamMAC), 0, 1);
                float flt_flapRatio = (float)flapRatio;
                double flapFactor = wingCamberFactor.Evaluate(flt_flapRatio);        //Flap Effectiveness Factor
                double dCm_dCl = wingCamberMoment.Evaluate(flt_flapRatio);           //Change in moment due to change in lift from flap

                //This accounts for the wing possibly having a longer span than the flap
                double WingFraction = FARMathUtil.Clamp(thisWingb_2 / effectiveUpstreamb_2, 0, 1);
                //This accounts for the flap possibly having a longer span than the wing it's attached to
                double FlapFraction = FARMathUtil.Clamp(effectiveUpstreamb_2 / thisWingb_2, 0, 1);

                double ClIncrement = flapFactor * effectiveUpstreamLiftSlope * effectiveUpstreamAngle;   //Lift created by the flap interaction
                ClIncrement *= (parentWingModule.S * FlapFraction + effectiveUpstreamArea * WingFraction) / parentWingModule.S;                   //Increase the Cl so that even though we're working with the flap's area, it accounts for the added lift across the entire object

                ACweight = ClIncrement * MachCoeff; // Total flap Cl for the purpose of applying ACshift, including the bit subtracted below

                ClIncrement -= FlapFraction * effectiveUpstreamLiftSlope * effectiveUpstreamAngle;        //Removing additional angle so that lift of the flap is calculated as lift at wing angle + lift due to flap interaction rather than being greater

                ACshift = (dCm_dCl + 0.75 * (1 - flapRatio)) * (thisWingMAC + effectiveUpstreamMAC);      //Change in Cm with change in Cl

                ClIncrementFromRear = ClIncrement * MachCoeff;
            }
        }

        /// <summary>
        /// Updates all FARWingInteraction orientation-based variables using the in-wing-plane velocity vector
        /// </summary>
        /// <param name="parallelInPlaneLocal">Normalized local velocity vector projected onto wing surface</param>
        public void UpdateOrientationForInteraction(Vector3d parallelInPlaneLocal)
        {
            if (upstreamRecalculationCount-- > 0 && Vector3d.Dot(parallelInPlaneLocal, previousParallelInPlaneLocal) > 0.996)
            {
                return;
            }
            upstreamRecalculationCount = 10;
            previousParallelInPlaneLocal = parallelInPlaneLocal;

            double wingForwardDir = parallelInPlaneLocal.y;
            double wingLeftwardDir = parallelInPlaneLocal.x * srfAttachFlipped;

            ARFactor = CalculateARFactor(wingForwardDir, wingLeftwardDir);
            UpdateUpstreamWingModules(wingForwardDir, wingLeftwardDir);
            if (HasWingsUpstream())
                UpdateStaticEffectiveUpstreamValues();
        }

        /// <summary>
        /// Sets upstream wing modules for proper calculation of upstream and downstream influences on wings
        /// </summary>
        /// <param name="wingForwardDir">Local velocity vector forward component</param>
        /// <param name="wingLeftwardDir">Local velocity vector leftward component</param>
        private void UpdateUpstreamWingModules(double wingForwardDir, double wingLeftwardDir)
        {
            nearbyUpstreamWingModulesAndInfluenceFactors.Clear();

            if(wingForwardDir > 0)
            {
                wingForwardDir *= wingForwardDir;
                //If wingForwardDir > 0, then that means that the wingModulesForward are upstream; add them

                for(int i = 0; i < nearbyWingModulesForward.Length; i++)
                {
                    AddWingAndInfluenceToUpstreamWings(nearbyWingModulesForward[i], wingForwardDir, nearbyWingModulesForward.Length);
                }
            }
            else
            {
                wingForwardDir *= wingForwardDir;
                for (int i = 0; i < nearbyWingModulesBackward.Length; i++)
                {
                    AddWingAndInfluenceToUpstreamWings(nearbyWingModulesBackward[i], wingForwardDir, nearbyWingModulesBackward.Length);
                }
            }
            
            if(wingLeftwardDir > 0)
            {
                wingLeftwardDir *= wingLeftwardDir;
                for (int i = 0; i < nearbyWingModulesLeftward.Length; i++)
                {
                    AddWingAndInfluenceToUpstreamWings(nearbyWingModulesLeftward[i], wingLeftwardDir, nearbyWingModulesLeftward.Length);
                }
            }
            else
            {
                wingLeftwardDir *= wingLeftwardDir;
                for (int i = 0; i < nearbyWingModulesRightward.Length; i++)
                {
                    AddWingAndInfluenceToUpstreamWings(nearbyWingModulesRightward[i], wingLeftwardDir, nearbyWingModulesRightward.Length);
                }
            }

        }

        private void AddWingAndInfluenceToUpstreamWings(FARWingAerodynamicModel upstreamWing, double directionalInfluence, double numWingsInDirection)
        {
            //Null wings indicates there is nothing there
            if ((object)upstreamWing == null)
                return;

            //Check to see if we already have this wing in the dict
            if (nearbyUpstreamWingModulesAndInfluenceFactors.ContainsKey(upstreamWing))
            {
                //If we do, add the appropriate influence to it

                //Account for the off-angle affect of this as well as the number of wings listed for this direction
                double influenceFactor = directionalInfluence / numWingsInDirection;

                influenceFactor += nearbyUpstreamWingModulesAndInfluenceFactors[upstreamWing];  //Add the previous numbers
                nearbyUpstreamWingModulesAndInfluenceFactors[upstreamWing] = influenceFactor;   //And update the dict value
            }
            else
            {
                //If we don't, add it, and set the influence factor

                //Account for the off-angle affect of this as well as the number of wings listed for this direction
                double influenceFactor = directionalInfluence / numWingsInDirection;

                nearbyUpstreamWingModulesAndInfluenceFactors[upstreamWing] = influenceFactor;   //And add it to the dictionary, along with the influence factor
            }

        }

        /// <summary>
        /// Calculates effect of nearby wings on the effective aspect ratio multiplier of this wing
        /// </summary>
        /// <param name="wingForwardDir">Local velocity vector forward component</param>
        /// <param name="wingLeftwardDir">Local velocity vector leftward component</param>
        /// <returns>Factor to multiply aspect ratio by to calculate effective lift slope and drag</returns>
        private double CalculateARFactor(double wingForwardDir, double wingLeftwardDir)
        {
            double wingtipExposure = 0;
            double wingrootExposure = 0;

            if (wingForwardDir > 0)
            {
                wingForwardDir *= wingForwardDir;
                wingtipExposure += leftwardExposure * wingForwardDir;
                wingrootExposure += rightwardExposure * wingForwardDir;
            }
            else
            {
                wingForwardDir *= wingForwardDir;
                wingtipExposure += rightwardExposure * wingForwardDir;
                wingrootExposure += leftwardExposure * wingForwardDir;
            }

            if (wingLeftwardDir > 0)
            {
                wingLeftwardDir *= wingLeftwardDir;
                wingtipExposure += backwardExposure * wingLeftwardDir;
                wingrootExposure += forwardExposure * wingLeftwardDir;
            }
            else
            {
                wingLeftwardDir *= wingLeftwardDir;
                wingtipExposure += forwardExposure * wingLeftwardDir;
                wingrootExposure += backwardExposure * wingLeftwardDir;
            }

            wingtipExposure = 1 - wingtipExposure;
            wingrootExposure = 1 - wingrootExposure;


            double effective_AR_modifier = (wingrootExposure + wingtipExposure);

            if (effective_AR_modifier < 1)
                return (effective_AR_modifier + 1);
            else
                return 2 * (2 - effective_AR_modifier) + 30 * (effective_AR_modifier - 1);
        }
    }
}
