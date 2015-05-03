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
using UnityEngine;
using FerramAerospaceResearch;

namespace ferram4
{
    public abstract class FARBaseAerodynamics : FARPartModule, ILiftProvider
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

        //Reset tinting for this part and its children
 //       private bool resetTinting;

        //[KSPField(isPersistant = false, guiActive = true)]
        public double S;

        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true)]
        public bool isShielded = true;

        public double rho;

        // Keep track if the tinting effect is active or not
        //private bool tintIsActive = false;

        public override void OnAwake()
        {
            base.OnAwake();
            part_transform = part.partTransform;

            //refArea = S;
            //Terrible, hacky fix for part.partTransform going bad
            if (part.partTransform == null && part == part.vessel.rootPart)
                part_transform = vessel.vesselTransform;
            if(HighLogic.LoadedSceneIsEditor)
                part_transform = part.partTransform;
        }

        public override void Start()
        {
            base.Start();
        }

/*        public virtual void LateUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {

                // If no tinting has been selected:
                if (!FARControlSys.tintForCd && !FARControlSys.tintForCl && !FARControlSys.tintForStall)
                {
                    // If it's active, turn it off
                    if (tintIsActive)
                    {
                        this.part.SetHighlightDefault();

                        tintIsActive = false;
                    }
                    // else, nothing to do

                    // and then return
                    return;
                }

                Color tintColor = AeroVisualizationTintingCalculation();

                if (tintColor.a != 0)
                {
                    this.part.SetHighlightType(Part.HighlightType.AlwaysOn);
                    this.part.SetHighlightColor(tintColor);
                    this.part.SetHighlight(true, false);
                    resetTinting = true;

                    tintIsActive = true;
                }
                else if (part.highlightType != Part.HighlightType.OnMouseOver)
                {
                    this.part.SetHighlightType(Part.HighlightType.OnMouseOver);
                    this.part.SetHighlightColor(Part.defaultHighlightPart);
                    this.part.SetHighlight(false, false);
                }
                else if (resetTinting)
                {
                    this.part.SetHighlightType(Part.HighlightType.Disabled);
                    this.part.SetHighlight(false, true);
                    resetTinting = false;
                }
            }
        }

        //Returns the tinted color if active; else it returns an alpha 0 color
        protected virtual Color AeroVisualizationTintingCalculation()
        {
            float satCl = 0, satCd = 0;

            if (!FARControlSys.tintForCl && !FARControlSys.tintForCd)
                return new Color(0, 0, 0, 0);

            if (FARControlSys.tintForCl)
                satCl = (float)Math.Abs(this.Cl / FARControlSys.fullySaturatedCl) * 10;
            if (FARControlSys.tintForCd)
                satCd = (float)Math.Abs(this.Cd / FARControlSys.fullySaturatedCd) * 10;

            Color tintColor = new Color(satCd, 0.5f * (satCl + satCd), satCl, 1);

            return tintColor;
        }*/

        /*public void ClearShielding()
        {
            isShielded = false;
        }

        public void ActivateShielding()
        {
            isShielded = true;
            Cl = 0;
            Cd = 0;
            Cm = 0;
        }*/


        public virtual Vector3d GetVelocity()
        {
            if (HighLogic.LoadedSceneIsFlight)
                return part.Rigidbody.velocity + Krakensbane.GetFrameVelocityV3f()
                    - FARWind.GetWind(FlightGlobals.currentMainBody, part, part.Rigidbody.position);
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
                velocity -= FARWind.GetWind(FlightGlobals.currentMainBody, part, part.Rigidbody.position);

                return velocity;
            }
            else
                return velocityEditor;
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

            foreach (Part p in EditorLogic.SortedShipList)
                foreach (PartModule m in p.Modules)
                    if (m is FARBaseAerodynamics)
                        parts.Add(m as FARBaseAerodynamics);

            return parts;
        }

        public static void PrecomputeGlobalCenterOfLift(FARCenterQuery lift, FARCenterQuery dummy, Vector3 vel)
        {
            /* Center of lift is the location where the derivative of
               the total torque provided by aerodynamic forces relative to
               AoA is zero (or at least minimal). This approximates the
               derivative by a simple subtraction, like before. */
            
            var parts = GetAllEditorModules();

            foreach (var ba in parts)
            {
                ba.velocityEditor = vel;
                ba.ResetCenterOfLift();
            }

            // run computations twice to let things like flap interactions settle
            foreach (var ba in parts)
                ba.PrecomputeCenterOfLift(vel, 0, dummy);
            foreach (var ba in parts)
                ba.PrecomputeCenterOfLift(vel, 0, lift);
        }

        public void OnCenterOfLiftQuery(CenterOfLiftQuery CoLMarker)
        {
            // Compute the actual center ourselves once per frame
            // Feed the precomputed values to the vanilla indicator
            CoLMarker.pos = FerramAerospaceResearch.FARAeroComponents.EditorAeroCenter.VesselRootLocalAeroCenter;      //hacking the old stuff to work with the new
            CoLMarker.pos = EditorLogic.RootPart.partTransform.localToWorldMatrix.MultiplyPoint3x4(CoLMarker.pos);
            CoLMarker.dir = Vector3.zero;
            CoLMarker.lift = 1;
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (node.HasValue("S"))
                double.TryParse(node.GetValue("S"), out S);
        }
    }
}
