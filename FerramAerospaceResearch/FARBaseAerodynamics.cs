/*
Ferram Aerospace Research v0.14.1
Copyright 2014, Michael Ferrara, aka Ferram4

    This file is part of Ferram Aerospace Research.

    Ferram Aerospace Research is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Kerbal Joint Reinforcement is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Ferram Aerospace Research.  If not, see <http://www.gnu.org/licenses/>.

    Serious thanks:		a.g., for tons of bugfixes and code-refactorings
            			Taverius, for correcting a ton of incorrect values
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
            			ialdabaoth (who is awesome), who originally created Module Manager
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
    // An accumulator class for summarizing a set of forces acting on the body
    public class FARCenterQuery
    {
        // Total force.
        public Vector3d force = Vector3d.zero;
        // Torque needed to compensate if force were applied at origin.
        public Vector3d torque = Vector3d.zero;

        // Weighted average of force positions used as aid in choosing the
        // single center location on the line of physically equivalent ones.
        public Vector3d pos = Vector3d.zero;
        public double amount = 0.0;

        // Record a force applied at a point
        public void AddForce(Vector3d npos, Vector3d nforce)
        {
            double size = nforce.magnitude;
            force += nforce;
            torque += Vector3d.Cross(npos, nforce);
            pos += npos*size;
            amount += size;
        }

        // Record an abstracted torque; point of application does not matter.
        public void AddTorque(Vector3d ntorque)
        {
            torque += ntorque;
        }

        // Merge two force sets
        public void AddAll(FARCenterQuery q2) {
            force += q2.force;
            torque += q2.torque;
            pos += q2.pos;
            amount += q2.amount;
        }

        // Returns a center of weight-like average of force positions.
        // Unless all forces are strictly parallel it doesn't mean much.
        public Vector3d GetPos()
        {
            return amount > 0 ? pos / amount : Vector3d.zero;
        }

        public void SetPos(Vector3d npos)
        {
            pos = npos;
            amount = 1;
        }

        // Compensating torque at different origin.
        public Vector3d TorqueAt(Vector3d origin)
        {
            return torque - Vector3d.Cross(origin, force);
        }

        // Returns a point that requires minimal residual torque
        // (or even 0 if possible) and is closest to origin.
        // Any remaining torque is always parallel to force.
        public Vector3d GetMinTorquePos(Vector3d origin)
        {
            double fmag = force.sqrMagnitude;
            if (fmag <= 0) return origin;

            return origin + Vector3d.Cross(force, TorqueAt(origin)) / fmag;
        }

        public Vector3d GetMinTorquePos()
        {
            return GetMinTorquePos(GetPos());
        }

        // The physics engine limits torque that can be applied to a single
        // object. This tries to replicate it based on results of experiments.
        // In practice this is probably not necessary for FAR, but since this
        // knowledge has been obtained, might as well turn it into code.
        public static float TorqueClipFactor(Vector3 torque, Rigidbody body)
        {
            Vector3 tq = Quaternion.Inverse(body.rotation * body.inertiaTensorRotation) * torque;
            Vector3 tensor = body.inertiaTensor;
            float acceleration = new Vector3(tq.x/tensor.x, tq.y/tensor.y, tq.z/tensor.z).magnitude;
            return Mathf.Max(1.0f, acceleration * Time.fixedDeltaTime / body.maxAngularVelocity);
        }
    }

    public abstract class FARBaseAerodynamics : FARPartModule
    {
        [KSPField(isPersistant = false, guiActive = false)]
        public double Cl;
        [KSPField(isPersistant = false, guiActive = false)]
        public double Cd;
        [KSPField(isPersistant = false, guiActive = false)]
        public double Cm;

        
        protected FARControlSys FARControl;
        //protected float MachNumber = 0;
        protected Vector3d velocityEditor = Vector3.zero;

        protected Transform part_transform;

        protected static Ray ray;
        protected static RaycastHit hit;

        //[KSPField(isPersistant = false, guiActive = true)]
        public double S;

        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true)]
        public bool isShielded = false;

        

        public static bool GlobalCoLReady = false;
        private static Vector3d GlobalCoL;
        private Vector3 CoLForce;

        public override void OnAwake()
        {
            base.OnAwake();
            part_transform = part.partTransform;

            //refArea = S;
            //Terrible, hacky fix for part.partTransform going bad
            if (part.partTransform == null && part == part.vessel.rootPart)
                part_transform = vessel.vesselTransform;
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);
            Fields["isShielded"].guiActive = FARDebugValues.displayShielding;

            part.OnEditorDetach += ClearShielding;

            if (!(this is FARControlSys))
            {
                Fields["Cl"].guiActive = Fields["Cd"].guiActive = Fields["Cm"].guiActive = FARDebugValues.displayCoefficients;
            }
        }

        public void ClearShielding()
        {
            isShielded = false;
        }

        public virtual Vector3d GetVelocity()
        {
            if (HighLogic.LoadedSceneIsFlight)
                return part.Rigidbody.velocity + Krakensbane.GetFrameVelocityV3f();
            else
                return velocityEditor;
        }

        public Vector3d GetVelocity(Vector3 refPoint)
        {
            Vector3d velocity = Vector3.zero;
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (part.Rigidbody)
                    velocity += part.Rigidbody.GetPointVelocity(refPoint);

                velocity += Krakensbane.GetFrameVelocity() - Krakensbane.GetLastCorrection() * TimeWarp.fixedDeltaTime;
                return velocity;
            }
            else
                return velocityEditor;
        }

        public double GetMachNumber(CelestialBody body, double altitude, Vector3d velocity)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {

                if (FARControl != null)
                    return FARControl.MachNumber;
                else
                    return FARAeroUtil.GetMachNumber(body, altitude, velocity);
            }
            else
            {
                print("GetMachNumber called in editor");
                return 0;
            }
        }

        protected virtual void ResetCenterOfLift()
        {
            // Clear state when preparing CoL computation
        }

        protected virtual Vector3d PrecomputeCenterOfLift(Vector3d velocity, double MachNumber, FARCenterQuery center)
        {
            return Vector3d.zero;
        }

        public static List<FARBaseAerodynamics> GetAllEditorModules()
        {
            var parts = new List<FARBaseAerodynamics>();

            foreach (Part p in FARAeroUtil.AllEditorParts)
                foreach (PartModule m in p.Modules)
                    if (m is FARBaseAerodynamics)
                        parts.Add(m as FARBaseAerodynamics);

            return parts;
        }

        private static void PrecomputeGlobalCenterOfLift()
        {
            /* Center of lift is the location where the derivative of
               the total torque provided by aerodynamic forces relative to
               AoA is zero (or at least minimal). This approximates the
               derivative by a simple subtraction, like before. */

            Vector3 vel_base, vel_fuzz;

            if (EditorLogic.fetch.editorType == EditorLogic.EditorMode.SPH)
            {
                vel_base = Vector3.forward;
                vel_fuzz = 0.02f * Vector3.up;
            }
            else
            {
                vel_base = Vector3.up;
                vel_fuzz = -0.02f * Vector3.forward;
            }

            FARCenterQuery lift = new FARCenterQuery();
            FARCenterQuery dummy = new FARCenterQuery();

            var parts = GetAllEditorModules();

            // Pass 1
            Vector3 vel = (vel_base - vel_fuzz).normalized;
            foreach (var ba in parts)
            {
                ba.velocityEditor = vel;
                ba.ResetCenterOfLift();
            }

            // run computations twice to let things like flap interactions settle
            foreach (var ba in parts)
                ba.PrecomputeCenterOfLift(vel, 0, dummy);
            foreach (var ba in parts)
                ba.CoLForce = ba.PrecomputeCenterOfLift(vel, 0, lift);

            // flip sign of data in the accumulator to indirectly subtract passes
            lift.force = -lift.force;
            lift.torque = - lift.torque;

            // Pass 2
            vel = (vel_base + vel_fuzz).normalized;
            foreach (var ba in parts)
            {
                ba.velocityEditor = vel;
                ba.ResetCenterOfLift();
            }

            foreach (var ba in parts)
                ba.PrecomputeCenterOfLift(vel, 0, dummy);
            foreach (var ba in parts)
                ba.CoLForce -= ba.PrecomputeCenterOfLift(vel, 0, lift);

            // Choose the center location
            GlobalCoL = lift.GetMinTorquePos();
            GlobalCoLReady = true;
        }

        public void OnCenterOfLiftQuery(CenterOfLiftQuery CoLMarker)
        {
            // Compute the actual center ourselves once per frame
            if (!GlobalCoLReady && HighLogic.LoadedSceneIsEditor)
                PrecomputeGlobalCenterOfLift();

            // Feed the precomputed values to the vanilla indicator
            CoLMarker.pos = GlobalCoL;
            CoLMarker.dir = CoLForce.normalized;
            CoLMarker.lift = CoLForce.magnitude * 50f;
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (node.HasValue("S"))
                double.TryParse(node.GetValue("S"), out S);
        }
    }
}
