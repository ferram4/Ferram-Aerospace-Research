/*
Neophyte's Elementary Aerodynamics Replacement v1.0.2
Copyright 2014, Michael Ferrara, aka Ferram4

    This file is part of Neophyte's Elementary Aerodynamics Replacement.

    Neophyte's Elementary Aerodynamics Replacement is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Kerbal Joint Reinforcement is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Neophyte's Elementary Aerodynamics Replacement.  If not, see <http://www.gnu.org/licenses/>.

    Serious thanks:		a.g., for tons of bugfixes and code-refactorings
            			Taverius, for correcting a ton of incorrect values
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
            			ialdabaoth (who is awesome), who originally created Module Manager
            			Duxwing, for copy editing the readme
 *
 * Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/55219
 *
 */

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Linq;
using UnityEngine;

/// <summary>
/// This calculates the drag for any general non-wing part, accounting for attachments and orientation
/// 
/// </summary>


namespace NEAR
{

    public class FARBasicDragModel : FARBaseAerodynamics
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
        private Dictionary<Vector3d, attachNodeData> attachNodeDragDict = new Dictionary<Vector3d, attachNodeData>();

        [KSPField(isPersistant = false)]
        public Vector3d CenterOfDrag = Vector3d.zero;

        public Vector3d CoDshift = Vector3d.zero;
        public Vector3d globalCoDShift = Vector3d.zero;
        public double cosAngleCutoff = 0;

        //private float M = 0;

        private bool animatingPart = false;
        private bool currentlyAnimating = true;
        private Animation anim = null;

        private Quaternion to_model_rotation = Quaternion.identity;

        public Transform[] PartModelTransforms = null;
        public Transform[] VesselModelTransforms = null;

        [KSPField(isPersistant = false, guiActive = false, guiName = "Current drag", guiUnits = "kN", guiFormat = "F3")]
        protected float currentDrag = 0.0f;

        public double YmaxForce = double.MaxValue;
        public double XZmaxForce = double.MaxValue;

        private void AnimationSetup()
        {
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

            YmaxForce = newYmaxForce;
            XZmaxForce = newXZmaxForce;

            UpdateUpVector(true);
        }


        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            

            //gear = part.GetComponent<ModuleLandingGear>();

            OnVesselPartsChange += AttachNodeCdAdjust;

            UpdateUpVector(false);
            PartModelTransforms = FARGeoUtil.PartModelTransformArray(part);
            AttachNodeCdAdjust();
            AnimationSetup();
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

        /*private void ConvertDragTypeToEnum()
        {
            if (DragType.ToLowerInvariant() == "nosecone")
                DragEnumType = DragModelType.NOSECONE;
            else if (DragType.ToLowerInvariant() == "cone")
                DragEnumType = DragModelType.CONE;
            else if (DragType.ToLowerInvariant() == "tailpylon")
                DragEnumType = DragModelType.TAILPYLON;
            else if (DragType.ToLowerInvariant() == "engine")
                DragEnumType = DragModelType.ENGINE;
            else if (DragType.ToLowerInvariant() == "skinfriction")
                DragEnumType = DragModelType.SKINFRICTION;
            else if (DragType.ToLowerInvariant() == "commandpod")
                DragEnumType = DragModelType.COMMANDPOD;
            else
                DragEnumType = DragModelType.CYLINDER;
        }*/

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

        protected override Vector3d PrecomputeCenterOfLift(Vector3d velocity, FARCenterQuery center)
        {
            Vector3d force = RunDragCalculation(velocity, 1);
            Vector3d pos = GetCoD();
            center.AddForce(pos, force);
            return force;
        }


        public void FixedUpdate()
        {
            currentDrag = 0;

            // With unity objects, "foo" or "foo != null" calls a method to check if
            // it's destroyed. (object)foo != null just checks if it is actually null.
            if (HighLogic.LoadedSceneIsFlight && (object)part != null)
            {
                if (animatingPart)
                    UpdatePropertiesWithAnimation();

                if (!isShielded)
                {
                    Rigidbody rb = part.Rigidbody;
                    Vessel vessel = part.vessel;

                    // Check that rb is not destroyed, but vessel is just not null
                    if (rb && (object)vessel != null && vessel.atmDensity > 0)
                    {
                        Vector3d velocity = rb.velocity + Krakensbane.GetFrameVelocity();
                        double v_scalar = velocity.magnitude;

                        double rho = vessel.atmDensity;
                        if (rho > 0 && v_scalar > 0.1)
                        {
                            Vector3d force = RunDragCalculation(velocity, rho);
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

        public double GetDragEditor(Vector3d velocityVector)
        {
            AttachNodeCdAdjust();
            velocityEditor = velocityVector;
            if (part)
                RunDragCalculation(velocityEditor, 1);

            return Cd * S;

        }

        public Vector3d RunDragCalculation(Vector3d velocity, double rho)
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
                //float Parallel = Vector3.Dot(upVector, velocity_normalized);

                Vector3d upVector = part_transform.TransformDirection(localUpVector);
                perp = Vector3d.Cross(upVector, velocity).normalized;
                liftDir = Vector3d.Cross(velocity, perp).normalized;

                Vector3d local_velocity = part_transform.InverseTransformDirection(velocity_normalized);
                DragModel(local_velocity);

                //if(gear && start != StartState.Editor)
                //    if(gear.gearState != ModuleLandingGear.GearStates.RETRACTED)
                //        Cd += 0.1f;
               /* if(anim)
                    if (anim.Progress > 0.5f)
                        Cd *= 1.5f;*/



                double qS = 0.5 * rho * v_scalar * v_scalar * S;   //dynamic pressure, q
                Vector3d D = velocity_normalized * (-qS * Cd);                         //drag
                Vector3d L = liftDir * (qS * Cl);
                Vector3d force = (L + D) * 0.001;
                double force_scalar = force.magnitude;
                currentDrag = (float)force_scalar;
                Vector3d moment = perp * (qS * Cm * 0.001);

                Rigidbody rb = part.Rigidbody;

                if (HighLogic.LoadedSceneIsFlight && rb)
                {
                    if (rb.angularVelocity.sqrMagnitude != 0)
                    {
                        Vector3d rot = Vector3d.Exclude(velocity_normalized, rb.angularVelocity);  //This prevents aerodynamic damping a spinning object if its spin axis is aligned with the velocity axis

                        rot *= (-0.00001 * qS);

                        // This seems redundant due to the moment addition below?
                        /*
                        if(!float.IsNaN(rot.sqrMagnitude))
                            part.Rigidbody.AddTorque(rot);
                        */

                        moment += rot;
                    }
                    //moment = (moment + lastMoment) / 2;
                    //lastMoment = moment;
//                    CoDshift += CenterOfDrag;
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
            //BaseCd = 0;
//            if (!part.Modules.Contains("FARPayloadFairingModule"))
//            {
            if (part.Modules.Contains("FARPayloadFairingModule"))       //This doesn't apply blunt drag drag to fairing parts if one of their "exempt" attach nodes is used, indicating attached fairings
            {
                return;
            }

            if(VesselPartList == null)
                UpdateShipPartsList();

            if (attachNodeDragDict == null)
                attachNodeDragDict = new Dictionary<Vector3d, attachNodeData>();

            attachNodeDragDict.Clear();

            Transform transform = part.partTransform;

            Vector3d partUpVector = transform.TransformDirection(localUpVector);

            //print("Updating drag for " + part.partInfo.title);
            foreach (AttachNode Attach in part.attachNodes)
            {
                if (Attach.nodeType == AttachNode.NodeType.Stack)
                {
                    if (Attach.attachedPart != null)
                    {
                        continue;
                    }
                    if (Attach.id.ToLowerInvariant() == "strut")
                        continue;

/*                    string attachId = Attach.id.ToLowerInvariant();
                    bool leaveAttachLoop = false;
                    foreach (string s in FARMiscData.exemptAttachNodes)
                        if (attachId.Contains(s))
                        {
                            leaveAttachLoop = true;
                            break;
                        }
                    if (leaveAttachLoop)
                        continue;*/
                        

                    Ray ray = new Ray();

                    Vector3d relPos = Attach.position + Attach.offset;

                    if(part.Modules.Contains("FARCargoBayModule"))
                    {
                        FARCargoBayModule bay = (FARCargoBayModule)part.Modules["FARCargoBayModule"];

                        Vector3d maxBounds = bay.maxBounds;
                        Vector3d minBounds = bay.minBounds;

                        if (relPos.x < maxBounds.x && relPos.y < maxBounds.y && relPos.z < maxBounds.z && relPos.x > minBounds.x && relPos.y > minBounds.y && relPos.z > minBounds.z)
                        {
                            return;
                        }
                    }

                    Vector3d origToNode = transform.localToWorldMatrix.MultiplyVector(relPos);

                    double mag = (origToNode).magnitude;



                    //print(part.partInfo.title + " Part Loc: " + part.transform.position + " Attach Loc: " + (origToNode + part.transform.position) + " Dist: " + mag);

                    ray.direction = origToNode;
                    ray.origin = transform.position;

                    double attachSize = FARMathUtil.Clamp(Attach.size, 0.5, double.PositiveInfinity);

                    bool gotIt = false;
                    RaycastHit[] hits = Physics.RaycastAll(ray, (float)(mag + attachSize), FARAeroUtil.RaycastMask);
                    foreach (RaycastHit h in hits)
                    {
                        if (h.collider == part.collider)
                            continue;
                        if (h.distance < (mag + attachSize) && h.distance > (mag - attachSize))
                            foreach (Part p in VesselPartList)
                                if (p.collider == h.collider)
                                {
                                    gotIt = true;
                                    break;
                                }
                        if (gotIt)
                        {
                            break;
                        }
                    }
                        
                    if (!gotIt)
                    {
//                            float exposedAttachArea = (Mathf.PI * Mathf.Pow(attachSize * FARAeroUtil.attachNodeRadiusFactor, 2) / Mathf.Clamp(S, 0.01f, Mathf.Infinity));
                        double exposedAttachArea = attachSize * FARAeroUtil.attachNodeRadiusFactor;
                        exposedAttachArea *= exposedAttachArea;
                        exposedAttachArea *= Math.PI * FARAeroUtil.areaFactor;
                        exposedAttachArea /= FARMathUtil.Clamp(S, 0.01, double.PositiveInfinity);

                        attachNodeData newAttachNodeData = new attachNodeData();
                        newAttachNodeData.areaValue = exposedAttachArea;
                        if (Vector3d.Dot(origToNode, partUpVector) > 1)
                            newAttachNodeData.pitchesAwayFromUpVec = true;
                        else
                            newAttachNodeData.pitchesAwayFromUpVec = false;


                        if (attachNodeDragDict.ContainsKey(transform.worldToLocalMatrix.MultiplyVector(origToNode)))
                        {
                            attachNodeData tmp = attachNodeDragDict[transform.worldToLocalMatrix.MultiplyVector(origToNode)];
                            tmp.areaValue += newAttachNodeData.areaValue;
                            attachNodeDragDict[transform.worldToLocalMatrix.MultiplyVector(origToNode)] = tmp;
                        }
                        else
                            attachNodeDragDict.Add(part.transform.worldToLocalMatrix.MultiplyVector(origToNode), newAttachNodeData);
                    }
                }
            }
//            }

            //print(part.partInfo.title + " Num unused Attach Nodes: " + attachNodeDragDict.Count);
        }
        /// <summary>
        /// These are just a few different attempts to figure drag for various blunt bodies
        /// </summary>
        private void DragModel(Vector3d local_velocity)
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
            double maxPressureCoeff = 1;

            double sepFlowCd = SeparatedFlowDrag();

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

            potentialLift = ClPotentialCurve.Evaluate(AxialProportion_flt);

            Cm = CmCurve.Evaluate(AxialProportion_flt) * 0.1;


            CoDshift = CenterOfDrag;

            Cd += 0.003;       //Skin friction drag

            foreach (KeyValuePair<Vector3d, attachNodeData> pair in attachNodeDragDict)
            {
                double dotProd = Vector3d.Dot(pair.Key.normalized, local_velocity);
                double tmp = 0;
                double Cltmp = 0;
                if (dotProd < 0)
                {
                    dotProd *= dotProd;
                    tmp = sepFlowCd;

                    tmp *= pair.Value.areaValue * dotProd;


                    CoDshift += pair.Key * (tmp / (tmp + Cd));
                }
                else
                {
                    Vector3d worldPairVec = part_transform.TransformDirection(pair.Key.normalized);
                    double dotProd_2 = dotProd * dotProd;
                    double liftProd = Vector3d.Dot(worldPairVec, liftDir);

                    tmp = maxPressureCoeff * dotProd_2 * dotProd;
                    tmp *= pair.Value.areaValue;

                    Cltmp = maxPressureCoeff * dotProd_2 * liftProd;
                    Cltmp *= -pair.Value.areaValue;

                    double radius = Math.Sqrt(pair.Value.areaValue / Math.PI);
                    Vector3 CoDshiftOffset = Vector3.Exclude(pair.Key, local_velocity).normalized;
                    CoDshiftOffset *= (float)(Math.Abs(liftProd) * radius);

                    double tmpCdCl = tmp + Math.Abs(Cltmp);

                    CoDshift += pair.Key * ((tmpCdCl) / (tmpCdCl + Cd)) + CoDshiftOffset;
                    if(pair.Value.pitchesAwayFromUpVec)
                        Cm -= 0.25 * radius * pair.Value.areaValue / S * Math.Abs(liftProd);
                    else
                        Cm += 0.25 * radius * pair.Value.areaValue / S * Math.Abs(liftProd);
                }



                CdAdd += tmp;
                newtonianLift += Cltmp;
            }
            Cd += CdAdd;
            Cl = viscousLift + potentialLift;
            Cl *= crossflowParameter;
            Cm *= crossflowParameter;
            
            Cl += newtonianLift;

            //Debug.Log("Cd = " + Cd + " Cl = " + Cl + " Cm = " + Cm + "\nPot Lift = " + potentialLift + " Visc Lift = " + viscousLift + " Newt Lift = " + newtonianLift + "\nCdAdd = " + CdAdd + " sepFlowCd = " + sepFlowCd + " maxPressureCoeff = " + maxPressureCoeff + "\ntaperCrossSectionAreaRatio = " + taperCrossSectionAreaRatio + " crossflowParameter = " + crossflowParameter);
        }

        private double SeparatedFlowDrag()
        {
            double sepCd = FARAeroUtil.rearNodeDragFactor;
            return sepCd;
        }

        public struct attachNodeData
        {
            public double areaValue;
            public bool pitchesAwayFromUpVec;
        }
    }
}