/*
Ferram Aerospace Research v0.14.4.1
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
using System.Text.RegularExpressions;
using System.Reflection;
using System.Linq;
using UnityEngine;
using ferram4.PartExtensions;

/// <summary>
/// This calculates the drag for any general non-wing part, accounting for attachments and orientation
/// 
/// </summary>


namespace ferram4
{

    public class FARBasicDragModel : FARBaseAerodynamics, TweakScale.IRescalable<FARBasicDragModel>
    {
        /// <summary>
        /// Cd_min (skin friction drag)
        /// reference area for Cd
        /// Object type to model -- cylinder, cone, nosecone, fuselage tail pylon
        /// </summary>
        //        [KSPField(isPersistant = false)]
        //        public float S;
        //        [KSPField]
        //        public string DragType = "cylinder";

        [KSPField(isPersistant = false)]
        public FloatCurve CdCurve;

        [KSPField(isPersistant = false)]
        public FloatCurve ClPotentialCurve;

        [KSPField(isPersistant = false)]
        public FloatCurve ClViscousCurve;

        [KSPField(isPersistant = false)]
        public FloatCurve CmCurve;

        [KSPField(isPersistant = false)]
        public Vector3d localUpVector = Vector3d.up;

        [KSPField(isPersistant = false)]
        public Vector3d localForwardVector = Vector3.forward;

        [KSPField(isPersistant = false)]
        public double majorMinorAxisRatio = 1;

        [KSPField(isPersistant = false)]
        public double taperCrossSectionAreaRatio = 0;

        //        public FARBasicDragModel.DragModelType DragEnumType;

        private Vector3d perp = Vector3.zero;
        private Vector3d liftDir = Vector3.zero;
        //private ModuleLandingGear gear = null;
        private List<attachNodeData> attachNodeDragList = new List<attachNodeData>();

        [KSPField(isPersistant = false)]
        public Vector3d CenterOfDrag = Vector3d.zero;

        public bool ignoreAnim = false;

        public Vector3d CoDshift = Vector3d.zero;
        public Vector3d globalCoDShift = Vector3d.zero;

        public double cosAngleCutoff = 0;

        //private float M = 0;

        private bool animatingPart = false;
        private bool currentlyAnimating = false;
        private Animation anim = null;

        private Quaternion to_model_rotation = Quaternion.identity;

        //public Transform[] PartModelTransforms = null;
        //public Transform[] VesselModelTransforms = null;

        [KSPField(isPersistant = false, guiActive = false, guiName = "Current drag", guiUnits = "kN", guiFormat = "F3")]
        protected float currentDrag = 0.0f;

        public double SPlusAttachArea = 0;

        public double YmaxForce = double.MaxValue;
        public double XZmaxForce = double.MaxValue;

        //Forces for blunt body AoA per unit area
        private static double bluntBodySinForceParameter;
        private static double bluntBodyCosForceParameter;
        private static double bluntBodyMomentParameter;

        private void AnimationSetup()
        {
            if (ignoreAnim)
                return;

            foreach (PartModule m in part.Modules)
            {
                FieldInfo field = m.GetType().GetField("animationName");
                if (field != null)        //This handles stock and Firespitter deployment animations
                {
                    string animationName = (string)field.GetValue(m);
                    anim = part.FindModelAnimators(animationName).FirstOrDefault();
                    if (anim != null)
                    {
                        animatingPart = true;
                        break;
                    }
                }
                field = m.GetType().GetField("animName");
                if (field != null)         //This handles Interstellar's deployment animations
                {
                    string animationName = (string)field.GetValue(m);
                    anim = part.FindModelAnimators(animationName).FirstOrDefault();
                    if (anim != null)
                    {
                        animatingPart = true;
                        break;
                    }
                }
            }
        }

        public static void SetBluntBodyParams(double radiusCurvatureRatio)
        {
            double sinThetaMax = 0.5 / radiusCurvatureRatio;
            double thetaMax = Math.Asin(sinThetaMax);
            double cosThetaMax = Math.Cos(thetaMax);

            double refAreaOfSphericalCap = radiusCurvatureRatio * radiusCurvatureRatio;
            refAreaOfSphericalCap = 1 - 1 / refAreaOfSphericalCap;
            refAreaOfSphericalCap = Math.Sqrt(refAreaOfSphericalCap);
            refAreaOfSphericalCap = 1 - refAreaOfSphericalCap;
            refAreaOfSphericalCap *= 2 * Math.PI;

            bluntBodySinForceParameter = thetaMax - cosThetaMax * sinThetaMax;
            bluntBodySinForceParameter *= 0.5;
            bluntBodySinForceParameter /= (refAreaOfSphericalCap);

            bluntBodyCosForceParameter = thetaMax + cosThetaMax * sinThetaMax;
            bluntBodyCosForceParameter /= (refAreaOfSphericalCap);

            bluntBodyMomentParameter = -2 * sinThetaMax * radiusCurvatureRatio / (3 * refAreaOfSphericalCap);
        }

        public void UpdatePropertiesWithShapeChange()
        {
            FARAeroUtil.SetBasicDragModuleProperties(this.part);      //By doing this, we update the properties of this object
            AttachNodeCdAdjust();                                     //In case the node properties somehow update with the node; also to deal with changes in part reference area
        }

        private void UpdatePropertiesWithAnimation()
        {
            if (anim)
            {
                if (anim.isPlaying && !currentlyAnimating)
                {
                    currentlyAnimating = true;
                }
                else if (currentlyAnimating && !anim.isPlaying)
                {
                    currentlyAnimating = false;
                    UpdatePropertiesWithShapeChange();
                }
//                bayProgress = bayAnim[bayAnimationName].normalizedTime;

            }
        }

        public void BuildNewDragModel(double newS, FloatCurve newCd, FloatCurve newClPotential, FloatCurve newClViscous, FloatCurve newCm, Vector3 newCoD, double newMajorMinorAxisRatio, double newCosCutoffAngle, double newTaperCrossSectionArea, double newYmaxForce, double newXZmaxForce)
        {
            S = newS;
            CdCurve = newCd;
            ClPotentialCurve = newClPotential;
            ClViscousCurve = newClViscous;
            CmCurve = newCm;
            CenterOfDrag = newCoD;
            majorMinorAxisRatio = newMajorMinorAxisRatio;
            cosAngleCutoff = newCosCutoffAngle;
            taperCrossSectionAreaRatio = newTaperCrossSectionArea / S;
            SPlusAttachArea = S;

            YmaxForce = newYmaxForce;
            XZmaxForce = newXZmaxForce;

            UpdateUpVector(true);
        }


        public override void Start()
        {
            base.Start();
            

            //gear = part.GetComponent<ModuleLandingGear>();

            OnVesselPartsChange += AttachNodeCdAdjust;

            UpdateUpVector(false || this.part.Modules.Contains("ModuleResourceIntake"));
            //PartModelTransforms = FARGeoUtil.PartModelTransformArray(part);
            AttachNodeCdAdjust();
            AnimationSetup();
            Fields["currentDrag"].guiActive = FARDebugValues.displayForces;
        }

        public Vector3 GetLiftDirection()
        {
            return liftDir;
        }

        private void UpdateUpVector(bool init)
        {
            if (init)
            {
                Quaternion rot = FARGeoUtil.GuessUpRotation(part);

                localUpVector = rot * Vector3d.up;
                localForwardVector = rot * Vector3d.forward;
            }
            //Doesn't work with Vector3d
            //Vector3d.OrthoNormalize(ref localUpVector, ref localForwardVector);

            localUpVector.Normalize();
            localForwardVector = Vector3d.Exclude(localUpVector, localForwardVector).normalized;

            Quaternion to_local = Quaternion.LookRotation(localForwardVector, localUpVector);
            to_model_rotation = Quaternion.Inverse(to_local);
        }

        public double GetCl()
        {
            Vector3d backward;
            if (HighLogic.LoadedSceneIsEditor)
                backward = -EditorLogic.startPod.transform.forward;
            else
                backward = -vessel.transform.forward;
            double ClUpwards;
            ClUpwards = Vector3d.Dot(liftDir, backward);
            ClUpwards *= Cl;

            
            return ClUpwards;
        }

        public Vector3 GetCoD()
        {
            return part_transform.TransformPoint(CoDshift) + globalCoDShift;
        }

        protected override Vector3d PrecomputeCenterOfLift(Vector3d velocity, double MachNumber, FARCenterQuery center)
        {
            try
            {
                Vector3d force = RunDragCalculation(velocity, MachNumber, 1);
                Vector3d pos = GetCoD();
                center.AddForce(pos, force);
                return force;
            }
            catch       //FIX ME!!!
            {           //Yell at KSP devs so that I don't have to engage in bad code practice
                //Debug.Log("The expected exception from the symmetry counterpart part transform internals was caught and suppressed");
                return Vector3.zero;
            }
        }


        public void FixedUpdate()
        {
            currentDrag = 0;

            // With unity objects, "foo" or "foo != null" calls a method to check if
            // it's destroyed. (object)foo != null just checks if it is actually null.
            if (HighLogic.LoadedSceneIsFlight && (object)part != null && FlightGlobals.ready)
            {
                if (animatingPart)
                    UpdatePropertiesWithAnimation();

                if (!isShielded)
                {
                    Rigidbody rb = part.Rigidbody;
                    Vessel vessel = part.vessel;

                    // Check that rb is not destroyed, but vessel is just not null
                    if (rb && (object)vessel != null && vessel.atmDensity > 0 && !vessel.packed)
                    {
                        Vector3d velocity = rb.velocity + Krakensbane.GetFrameVelocity()
                            - FARWind.GetWind(FlightGlobals.currentMainBody, part, rb.position);

                        double soundspeed, v_scalar = velocity.magnitude;

                        rho = FARAeroUtil.GetCurrentDensity(vessel, out soundspeed);
                        if (rho > 0 && v_scalar > 0.1)
                        {
                            Vector3d force = RunDragCalculation(velocity, v_scalar / soundspeed, rho);
                            rb.AddForceAtPosition(force, GetCoD());
                        }
                    }
                }
            }
        }

        public Vector3d GetCoDEditor()
        {
            // no globalCoDshift because of separate GetMomentEditor
            return part_transform.TransformPoint(CoDshift);
        }

        public double GetDragEditor(Vector3d velocityVector, double MachNumber)
        {
            AttachNodeCdAdjust();
            velocityEditor = velocityVector;
            if (part)
                RunDragCalculation(velocityEditor, MachNumber, 1);

            return Cd * S;
        }

        public double GetLiftEditor()
        {
            // This stuff seems to be redundant due to dot products done in FAREditorGUI
            /*Vector3 vert = Vector3.Cross(velocityEditor, Vector3.right).normalized;
            float tmp = Vector3.Dot(-Vector3.Cross(perp, velocityEditor).normalized, vert);
            return Cl * S * tmp;*/

            return Cl * S;
        }

        public double GetMomentEditor()
        {
            return Cm * S;
        }

        public Vector3d RunDragCalculation(Vector3d velocity, double MachNumber, double rho)
        {
            if (isShielded)
            {
                Cl = Cd = Cm = 0;
                return Vector3d.zero;
            }

            double v_scalar = velocity.magnitude;

            if (v_scalar > 0.1)         //Don't Bother if it's not moving or in space
            {
                CoDshift = Vector3d.zero;
                Cd = 0;

                Vector3d velocity_normalized = velocity / v_scalar;

                Vector3d upVector = part_transform.TransformDirection(localUpVector);
                perp = Vector3d.Cross(upVector, velocity).normalized;
                liftDir = Vector3d.Cross(velocity, perp).normalized;

                Vector3d local_velocity = part_transform.InverseTransformDirection(velocity_normalized);
                DragModel(local_velocity, MachNumber);

                double qS = 0.5 * rho * v_scalar * v_scalar * S;   //dynamic pressure, q
                Vector3d D = velocity_normalized * (-qS * Cd);                         //drag
                Vector3d L = liftDir * (qS * Cl);
                Vector3d force = (L + D) * 0.001;
                double force_scalar = force.magnitude;
                currentDrag = (float)force_scalar;
                Vector3d moment = perp * (qS * Cm * 0.001);

                Rigidbody rb = part.Rigidbody;

                if (HighLogic.LoadedSceneIsFlight && (object)rb != null)
                {
                    if (rb.angularVelocity.sqrMagnitude != 0)
                    {
                        Vector3d rot = Vector3d.Exclude(velocity_normalized, rb.angularVelocity);  //This prevents aerodynamic damping a spinning object if its spin axis is aligned with the velocity axis

                        rot *= (-0.00001 * qS);


                        moment += rot;
                    }
                }

                //Must handle aero-structural failure before transforming CoD pos and adding pitching moment to forces or else parts with blunt body drag fall apart too easily
                if (Math.Abs(Vector3d.Dot(force, upVector)) > YmaxForce || Vector3d.Exclude(upVector, force).magnitude > XZmaxForce)
                    if (part.parent && !vessel.packed)
                    {
                        part.SendEvent("AerodynamicFailureStatus");
                        FlightLogger.eventLog.Add("[" + FARMathUtil.FormatTime(vessel.missionTime) + "] Joint between " + part.partInfo.title + " and " + part.parent.partInfo.title + " failed due to aerodynamic stresses.");
                        part.decouple(25);
                    }

                globalCoDShift = Vector3d.Cross(force, moment) / (force_scalar * force_scalar);

                if (double.IsNaN(force_scalar) || double.IsNaN(moment.sqrMagnitude) || double.IsNaN(globalCoDShift.sqrMagnitude))
                {
                    Debug.LogWarning("FAR Error: Aerodynamic force = " + force.magnitude + " Aerodynamic moment = " + moment.magnitude + " CoD Local = " + CoDshift.magnitude + " CoD Global = " + globalCoDShift.magnitude + " " + part.partInfo.title);
                    force = moment = CoDshift = globalCoDShift = Vector3.zero;
                    return force;
                }


                //part.Rigidbody.AddTorque(moment);
                return force;
            }
            else
                return Vector3d.zero;
        }

        private void AttachNodeCdAdjust()
        {

            if (part.Modules.Contains("FARPayloadFairingModule"))       //This doesn't apply blunt drag drag to fairing parts if one of their "exempt" attach nodes is used, indicating attached fairings
            {
                return;
            }
            if(VesselPartList == null)
                UpdateShipPartsList();

            if (attachNodeDragList == null)
                attachNodeDragList = new List<attachNodeData>();

            attachNodeDragList.Clear();

            Transform transform = part.partTransform;

            if (transform == null)
                transform = part.transform;
            if(transform == null)
            {
                Debug.LogError("Part " + part.partInfo.title + " has null transform; drag interactions cannot be applied.");
                return;
            }

            SPlusAttachArea = S;

            Vector3d partUpVector = transform.TransformDirection(localUpVector);
            Bounds[] colliderBounds = part.GetColliderBounds();

            //print("Updating drag for " + part.partInfo.title);
            foreach (AttachNode Attach in part.attachNodes)
            {
                if (Attach.nodeType == AttachNode.NodeType.Stack)
                {
                    if (Attach.id.ToLowerInvariant() == "strut")
                        continue;

                    Vector3d relPos = Attach.position + Attach.offset;
                    Ray ray = new Ray();

                    if (part.Modules.Contains("FARCargoBayModule"))
                    {
                        FARCargoBayModule bay = (FARCargoBayModule)part.Modules["FARCargoBayModule"];

                        Vector3d maxBounds = bay.maxBounds;
                        Vector3d minBounds = bay.minBounds;

                        if (relPos.x < maxBounds.x && relPos.y < maxBounds.y && relPos.z < maxBounds.z && relPos.x > minBounds.x && relPos.y > minBounds.y && relPos.z > minBounds.z)
                        {
                            continue;
                        }
                    }

                    if (Attach.attachedPart != null)
                    {
                        if (AttachedPartIsNotClipping(Attach.attachedPart, colliderBounds))
                            continue;
                    }

                    Vector3d origToNode = transform.localToWorldMatrix.MultiplyVector(relPos);
                    double attachSize = FARMathUtil.Clamp(Attach.size, 0.5, double.PositiveInfinity);

                    if (UnattachedPartRightAgainstNode(origToNode, attachSize, relPos))
                        continue;

                    attachNodeData newAttachNodeData = new attachNodeData();

                    double exposedAttachArea = attachSize * FARAeroUtil.attachNodeRadiusFactor;

                    newAttachNodeData.recipDiameter = 1 / (2 * exposedAttachArea);

                    exposedAttachArea *= exposedAttachArea;
                    exposedAttachArea *= Math.PI * FARAeroUtil.areaFactor;

                    SPlusAttachArea += exposedAttachArea;

                    exposedAttachArea /= FARMathUtil.Clamp(S, 0.01, double.PositiveInfinity);

                    newAttachNodeData.areaValue = exposedAttachArea;
                    if (Vector3d.Dot(origToNode, partUpVector) > 1)
                        newAttachNodeData.pitchesAwayFromUpVec = true;
                    else
                        newAttachNodeData.pitchesAwayFromUpVec = false;

                    newAttachNodeData.location = transform.worldToLocalMatrix.MultiplyVector(origToNode);

                    attachNodeDragList.Add(newAttachNodeData);
                }
            }
        }

        private bool AttachedPartIsNotClipping(Part attachedPart, Bounds[] colliderBounds)
        {
            //string s = "";
            for (int i = 0; i < colliderBounds.Length; i++)
            {
                Bounds bound = colliderBounds[i];
                // s += "Min: " + bound.min.ToString() + " Max: " + bound.max.ToString() + "\n\r";
                if (bound.Contains(attachedPart.transform.position))
                    return false;
                //s += "Found containing point\n\r";
            }
            //Debug.Log(s);
            return true;
        }

        private bool UnattachedPartRightAgainstNode(Vector3d origToNode, double attachSize, Vector3d relPos)
        {
            double mag = (origToNode).magnitude;

            ray.direction = origToNode;

            ray.origin = part.transform.position;

            RaycastHit[] hits = Physics.RaycastAll(ray, (float)(mag + attachSize), FARAeroUtil.RaycastMask);
            foreach (RaycastHit h in hits)
            {
                if (h.collider == part.collider)
                    continue;
                if (h.distance < (mag + attachSize) && h.distance > (mag - attachSize))
                    foreach (Part p in VesselPartList)
                        if (p.collider == h.collider)
                        {
                            return true;
                        }
            }
            return false;
        }

        /// <summary>
        /// These are just a few different attempts to figure drag for various blunt bodies
        /// </summary>
        private void DragModel(Vector3d local_velocity, double M)
        {
            // Has the same x/y/z as the vertices in PartMaxBoundaries etc
            Vector3d model_velocity = to_model_rotation * local_velocity;

            double viscousLift, potentialLift, newtonianLift;
            viscousLift = potentialLift = newtonianLift = 0;
            double CdAdd = 0;
            //float AxialProportion = Vector3.Dot(localUpVector, local_velocity);
            double AxialProportion = model_velocity.y;
            float AxialProportion_flt = (float)model_velocity.y;
            double AxialProportion_2 = AxialProportion * AxialProportion;
            double OneMinusAxial_2 = Math.Abs(1 - AxialProportion_2);
            double M_2 = M * M;
            double M_2_recip = 1 / M_2;
            double maxPressureCoeff;
            
            if(FARDebugValues.useSplinesForSupersonicMath)
                maxPressureCoeff = FARAeroUtil.MaxPressureCoefficient.Evaluate((float)M);
            else
                maxPressureCoeff = FARAeroUtil.MaxPressureCoefficientCalc(M);

            double sepFlowCd = SeparatedFlowDrag(M, M_2, M_2_recip);

            //This handles elliptical and other non-circular cross sections
            //float crossflowParameter = Vector3.Dot(localForwardVector, Vector3.Exclude(localUpVector, local_velocity).normalized);
            //crossflowParameter *= crossflowParameter;
            double crossflowParameter = model_velocity.z * model_velocity.z;
            double crossflow = model_velocity.x * model_velocity.x + crossflowParameter;
            if (crossflow != 0)
                crossflowParameter /= crossflow;

            crossflowParameter = crossflowParameter * majorMinorAxisRatio + (1 - crossflowParameter) / majorMinorAxisRatio;
            if (AxialProportion_2 > 0.98)
            {
                crossflowParameter *= 50 * OneMinusAxial_2;
            }
            
            Cd += CdCurve.Evaluate(AxialProportion_flt);



            viscousLift = ClViscousCurve.Evaluate(AxialProportion_flt);

            double axialDirectionFactor = cosAngleCutoff * AxialProportion;

            if (axialDirectionFactor > 0)
            {
                Cd = Math.Min(Cd, sepFlowCd * taperCrossSectionAreaRatio) * AxialProportion_2 + Cd * OneMinusAxial_2;
            }
            else
            {
                Cd = Math.Min(Cd, maxPressureCoeff * taperCrossSectionAreaRatio) * AxialProportion_2 + Cd * OneMinusAxial_2;
            }

            if (M_2 > 1)
            {
                potentialLift = ClPotentialCurve.Evaluate(AxialProportion_flt) * M_2_recip;
            }
            else
            {
                potentialLift = ClPotentialCurve.Evaluate(AxialProportion_flt);
            }


            Cm = CmCurve.Evaluate(AxialProportion_flt) * 0.1;

            CoDshift = CenterOfDrag;

            double MachMultiplier = MachDragEffect(M);

            Cd *= MachMultiplier;


            Cd += 0.003;

            for (int i = 0; i < attachNodeDragList.Count; i++)
            {
                attachNodeData node = attachNodeDragList[i];

                double dotProd = Vector3d.Dot(node.location.normalized, local_velocity);
                double tmp = 0;
                double Cltmp = 0;
                if (dotProd < 0)
                {
                    dotProd *= dotProd;
                    tmp = sepFlowCd;

                    //                    Cltmp = tmp * (dotProd - 1);
                    //                    Cltmp *= pair.Value;

                    tmp *= node.areaValue * dotProd;


                    //                    Vector3 CoDshiftOffset = -Vector3.Exclude(pair.Key, part.transform.worldToLocalMatrix.MultiplyVector(velocity.normalized)).normalized;
                    //                    CoDshiftOffset *= Mathf.Sqrt(Mathf.Clamp01(1 - dotProd));
                    //                    CoDshiftOffset *= Mathf.Sqrt(1.5f * pair.Value);

                    CoDshift += node.location * (tmp / (tmp + Cd));
                }
                else
                {
                    Vector3d worldPairVec = part_transform.TransformDirection(node.location.normalized);
                    double dotProd_2 = dotProd * dotProd;
                    double liftProd_2 = 1 - dotProd_2;
                    double liftProd = Vector3d.Dot(worldPairVec, liftDir);

                    double forceCoefficient = dotProd_2 * bluntBodyCosForceParameter;
                    forceCoefficient += liftProd_2 * bluntBodySinForceParameter;
                    forceCoefficient *= maxPressureCoeff * node.areaValue;      //force applied perependicular to the flat end of the node

                    tmp = forceCoefficient * dotProd;
                    Cltmp = -forceCoefficient * liftProd;       //negative because lift is in opposite direction of projection of velocity vector onto node direction

                    double Cmtmp = dotProd * liftProd * bluntBodyMomentParameter;
                    Cmtmp *= node.areaValue * node.recipDiameter;

                    if (!node.pitchesAwayFromUpVec)
                        Cmtmp *= -1;

                    Cm += Cmtmp;

                    double tmpCdCl = Math.Sqrt(tmp * tmp + Cltmp * Cltmp);
                    CoDshift += node.location * (tmpCdCl / (tmpCdCl + Math.Sqrt(Cd * Cd + Cl * Cl)));
                    /*double liftProd = Vector3d.Dot(worldPairVec, liftDir);

                    tmp = maxPressureCoeff * dotProd_2 * dotProd;
                    tmp *= node.areaValue;

                    Cltmp = maxPressureCoeff * dotProd_2 * liftProd;
                    Cltmp *= -node.areaValue;

                    double radius = Math.Sqrt(node.areaValue / Math.PI);
                    Vector3 CoDshiftOffset = Vector3.Exclude(node.location, local_velocity).normalized;
                    CoDshiftOffset *= (float)(Math.Abs(liftProd) * radius * 0.4);

                    double Cmtmp;
                    if (node.pitchesAwayFromUpVec)
                        Cmtmp = -0.325 * radius * node.areaValue / S * Math.Abs(liftProd);
                    else
                        Cmtmp = 0.325 * radius * node.areaValue / S * Math.Abs(liftProd);

                    double tmpCdCl = Math.Sqrt(tmp * tmp + Cltmp * Cltmp);

                    CoDshift += node.location * (tmpCdCl / (tmpCdCl + Math.Sqrt(Cd * Cd + Cl * Cl))) + CoDshiftOffset;

                    Cm += Cmtmp;*/
                }

                CdAdd += tmp;
                newtonianLift += Cltmp;
            }


            viscousLift *= MachMultiplier;
            Cd += CdAdd;
            Cl = viscousLift + potentialLift;
            Cl *= crossflowParameter;
            Cm *= crossflowParameter;
            
            Cl += newtonianLift;

//            Debug.Log("Cd = " + Cd + " Cl = " + Cl + " Cm = " + Cm + "\nPot Lift = " + potentialLift + " Visc Lift = " + viscousLift + " Newt Lift = " + newtonianLift + "\nCdAdd = " + CdAdd + " sepFlowCd = " + sepFlowCd + " maxPressureCoeff = " + maxPressureCoeff + "\ntaperCrossSectionAreaRatio = " + taperCrossSectionAreaRatio + " crossflowParameter = " + crossflowParameter);
        }

        private double SeparatedFlowDrag(double M, double M_2, double M_2_recip)
        {
            double sepCd = 1;
            if (M > 1)
                sepCd *= (FARAeroUtil.incompressibleRearAttachDrag + FARAeroUtil.sonicRearAdditionalAttachDrag) * M_2_recip;
            else
                sepCd *= ((M_2 * FARAeroUtil.sonicRearAdditionalAttachDrag) + FARAeroUtil.incompressibleRearAttachDrag);

            return sepCd;
        }

        private double MachDragEffect(double M)
        {
            double multiplier = 1;

            if (M <= 1)
            {
                multiplier = 1 + 0.4 * FARAeroUtil.ExponentialApproximation(10 * M - 10);
            }
            //multiplier = 1f + 0.4f * Mathf.Exp(10 * M - 10f);            //Exponentially increases, mostly from 0.8 to 1;  Models Drag divergence due to locally supersonic flow around object at flight Mach Numbers < 1
            else
                multiplier = 0.15 / M + 1.25;             //Cd drops after Mach 1


            return multiplier;
        }

        public struct attachNodeData
        {
            public double areaValue;
            public double recipDiameter;
            public bool pitchesAwayFromUpVec;
            public Vector3d location;
        }

        public enum AttachGroupType
        {
            INDEPENDENT_NODE,
            VERTICAL_NODES,
            PARALLEL_NODES
        }
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if(node.HasNode("ClCurve"))
            {
                ClPotentialCurve = new FloatCurve();
                ClPotentialCurve.Load(node.GetNode("ClCurve"));
                ClViscousCurve = new FloatCurve();
                ClViscousCurve.Add(-1, 0);
                ClViscousCurve.Add(1, 0);
            }
            if (node.HasValue("majorMinorAxisRatio"))
                double.TryParse(node.GetValue("majorMinorAxisRatio"), out majorMinorAxisRatio);
            if (node.HasValue("taperCrossSectionAreaRatio"))
                double.TryParse(node.GetValue("taperCrossSectionAreaRatio"), out taperCrossSectionAreaRatio);
            if (node.HasValue("cosAngleCutoff"))
                double.TryParse(node.GetValue("cosAngleCutoff"), out cosAngleCutoff);
            if (node.HasValue("ignoreAnim"))
                bool.TryParse(node.GetValue("ignoreAnim"), out ignoreAnim);

            if(node.HasNode("CdCurve"))
            {
                CdCurve = new FloatCurve();
                CdCurve.Load(node.GetNode("CdCurve"));
            }
            if (node.HasNode("ClPotentialCurve"))
            {
                ClPotentialCurve = new FloatCurve();
                ClPotentialCurve.Load(node.GetNode("ClPotentialCurve"));
            }
            if (node.HasNode("ClViscousCurve"))
            {
                ClViscousCurve = new FloatCurve();
                ClViscousCurve.Load(node.GetNode("ClViscousCurve"));
            }
            if (node.HasNode("CmCurve"))
            {
                CmCurve = new FloatCurve();
                CmCurve.Load(node.GetNode("CmCurve"));
            }
        }

        //Blank save node ensures that nothing for this partmodule is saved
        public override void OnSave(ConfigNode node)
        {
            //base.OnSave(node);
        }

        public void OnRescale(TweakScale.ScalingFactor factor)
        {
            if(part.Modules != null)
                UpdatePropertiesWithShapeChange();
        }
    }
}