/*
Neophyte's Elementary Aerodynamics Replacement v1.3.1
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
using UnityEngine;

namespace NEAR
{
    public abstract class FARBaseAerodynamics : FARPartModule
    {
        [KSPField(isPersistant = false, guiActive = false)]
        public double Cl;
        [KSPField(isPersistant = false, guiActive = false)]
        public double Cd;
        [KSPField(isPersistant = false, guiActive = false)]
        public double Cm;

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

        public override void Start()
        {
            base.Start();

            part.OnEditorDetach += ClearShielding;
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

        protected virtual void ResetCenterOfLift()
        {
            // Clear state when preparing CoL computation
        }

        protected virtual Vector3d PrecomputeCenterOfLift(Vector3d velocity, FARCenterQuery center)
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

            if (EditorDriver.editorFacility == EditorFacility.SPH)
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
                ba.PrecomputeCenterOfLift(vel, dummy);
            foreach (var ba in parts)
                ba.CoLForce = ba.PrecomputeCenterOfLift(vel, lift);

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
                ba.PrecomputeCenterOfLift(vel, dummy);
            foreach (var ba in parts)
                ba.CoLForce -= ba.PrecomputeCenterOfLift(vel, lift);

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
            CoLMarker.dir = Vector3.zero;
            CoLMarker.lift = CoLForce.magnitude;
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (node.HasValue("S"))
                double.TryParse(node.GetValue("S"), out S);
        }
    }
}
