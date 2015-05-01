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
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;
using FerramAerospaceResearch;

namespace FerramAerospaceResearch.FARAeroComponents
{
    //Used to hold relevant aero data for each part before applying it
    public class FARAeroPartModule : PartModule, ILiftProvider
    {
        public Vector3 partLocalVel;
        public Vector3 partLocalVelNorm;
        public Vector3 partLocalAngVel;

        public Vector3 worldSpaceAeroForce;

        Vector3 partLocalForce;
        Vector3 partLocalTorque;

        ProjectedArea projectedArea;

        private double partStressMaxY = double.MaxValue;
        private double partStressMaxXZ = double.MaxValue;
        private double partForceMaxY = double.MaxValue;
        private double partForceMaxXZ = double.MaxValue;

        private ArrowPointer liftArrow;
        private ArrowPointer dragArrow;

        bool fieldsVisible = false;

        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = false, guiFormat = "F3", guiUnits = "kN")]
        public float dragForce;

        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = false, guiFormat = "F3", guiUnits = "kN")]
        public float liftForce;

        private Transform partTransform;

        public ProjectedArea ProjectedAreas
        {
            get { return projectedArea; }
        }

        public struct ProjectedArea
        {
            public double iN, iP;  //area in x direction
            public double jN, jP;  //area in y direction
            public double kN, kP;  //area in z direction
            public double totalArea;


            public static ProjectedArea operator +(ProjectedArea a, ProjectedArea b)
            {
                a.iN += b.iN;
                a.iP += b.iP;
                a.jN += b.jN;
                a.jP += b.jP;
                a.kN += b.kN;
                a.kP += b.kP;
                return a;
            }

            public static ProjectedArea operator + (ProjectedArea a, FARPartGeometry.VoxelCrossSection.SideAreaValues b)
            {
                a.iN += b.iN;
                a.iP += b.iP;
                a.jN += b.jN;
                a.jP += b.jP;
                a.kN += b.kN;
                a.kP += b.kP;
                return a;
            }
        }

        public void SetShielded()
        {
            part.ShieldedFromAirstream = true;
        }

        public void SetProjectedArea(ProjectedArea areas, Matrix4x4 vesselToWorldMatrix)
        {
            ProjectedArea transformedArea = new ProjectedArea();
            Matrix4x4 transformMatrix = part.transform.worldToLocalMatrix * vesselToWorldMatrix;

            IncrementAreas(ref transformedArea, (float)areas.iP * Vector3.right, transformMatrix);
            IncrementAreas(ref transformedArea, (float)areas.iN * -Vector3.right, transformMatrix);
            IncrementAreas(ref transformedArea, (float)areas.jP * Vector3.up, transformMatrix);
            IncrementAreas(ref transformedArea, (float)areas.jN * -Vector3.up, transformMatrix);
            IncrementAreas(ref transformedArea, (float)areas.kP * Vector3.forward, transformMatrix);
            IncrementAreas(ref transformedArea, (float)areas.kN * -Vector3.forward, transformMatrix);

            projectedArea = transformedArea;
            projectedArea.totalArea = projectedArea.iN + projectedArea.iP + projectedArea.jN + projectedArea.jP + projectedArea.kN + projectedArea.kP;

            if (projectedArea.totalArea <= 0)
            {
                part.ShieldedFromAirstream = true;
                if (fieldsVisible)
                {
                    Fields["dragForce"].guiActive = false;
                    Fields["liftForce"].guiActive = false;
                    fieldsVisible = false;
                }
                if ((object)liftArrow != null)
                {
                    UnityEngine.Object.Destroy(liftArrow);
                    liftArrow = null;
                }
                if ((object)dragArrow != null)
                {
                    UnityEngine.Object.Destroy(dragArrow);
                    dragArrow = null;
                }
            }
            else
            {
                part.ShieldedFromAirstream = false;
            }

            double areaForStress = projectedArea.totalArea / 6;
            if (areaForStress <= 0.1)
            {
                partForceMaxY = double.MaxValue;
                partForceMaxXZ = double.MaxValue;
                return;
            }
            partForceMaxY = areaForStress * partStressMaxY;
            partForceMaxXZ = areaForStress * partStressMaxXZ;
        }

        private void IncrementAreas(ref ProjectedArea data, Vector3 vector, Matrix4x4 transformMatrix)
        {
            vector = transformMatrix.MultiplyVector(vector);

            if (vector.x >= 0)
                data.iP += vector.x;
            else
                data.iN -= vector.x;

            if (vector.y >= 0)
                data.jP += vector.y;
            else
                data.jN -= vector.y;

            if (vector.z >= 0)
                data.kP += vector.z;
            else
                data.kN -= vector.z;
        }

        void Start()
        {
            part.maximum_drag = 0;
            part.minimum_drag = 0;
            part.angularDrag = 0;
            this.enabled = false;

            partLocalVel = Vector3.zero;
            partLocalForce = Vector3.zero;
            partLocalTorque = Vector3.zero;

            if(FARDebugValues.allowStructuralFailures)
            {
                FARPartStressTemplate template = FARAeroStress.DetermineStressTemplate(this.part);
                partStressMaxY = template.YmaxStress;
                partStressMaxXZ = template.XZmaxStress;
            }
            partTransform = part.transform;
        }

        public double ProjectedAreaWorld(Vector3 normalizedDirectionVector)
        {
            return ProjectedAreaLocal(partTransform.worldToLocalMatrix.MultiplyVector(normalizedDirectionVector));
        }

        public double ProjectedAreaLocal(Vector3 normalizedDirectionVector)
        {
            double area = 0;
            if (normalizedDirectionVector.x > 0)
                area += normalizedDirectionVector.x * projectedArea.iP;
            else
                area -= normalizedDirectionVector.x * projectedArea.iN;

            if (normalizedDirectionVector.y > 0)
                area += normalizedDirectionVector.y * projectedArea.jP;
            else
                area -= normalizedDirectionVector.y * projectedArea.jN;

            if (normalizedDirectionVector.z > 0)
                area += normalizedDirectionVector.z * projectedArea.kP;
            else
                area -= normalizedDirectionVector.z * projectedArea.kN;

            return area;
        }

        public void ApplyForces()
        {
            if (float.IsNaN(partLocalForce.sqrMagnitude))
                partLocalForce = Vector3.zero;
            if (float.IsNaN(partLocalTorque.sqrMagnitude))
                partLocalTorque = Vector3.zero;

            if(!vessel.packed)
                CheckAeroStressFailure();

            Matrix4x4 matrix = partTransform.localToWorldMatrix;
            Rigidbody rb = part.Rigidbody;

            worldSpaceAeroForce = matrix.MultiplyVector(partLocalForce);

            UpdateAeroDisplay();

            rb.AddForce(worldSpaceAeroForce);
            rb.AddTorque(matrix.MultiplyVector(partLocalTorque));

            partLocalForce = Vector3.zero;
            partLocalTorque = Vector3.zero;

        }

        public void AddLocalForce(Vector3 partLocalForce, Vector3 partLocalLocation)
        {
            this.partLocalForce += partLocalForce;
            this.partLocalTorque += Vector3.Cross(partLocalLocation - part.CoMOffset, partLocalForce);
        }

        public void AddLocalForceAndTorque(Vector3 partLocalForce, Vector3 partLocalTorque, Vector3 partLocalLocation)
        {
            Vector3 localRadVector = partLocalLocation - part.CoMOffset;
            this.partLocalForce += partLocalForce;
            this.partLocalTorque += Vector3.Cross(localRadVector, partLocalForce);

            this.partLocalTorque += partLocalTorque;
        }

        public void UpdateVelocityAndAngVelocity(Vector3 frameVel)
        {
            if (partTransform == null)
                if (part != null)
                    partTransform = part.transform;
                else
                    return;

            Matrix4x4 matrix = partTransform.worldToLocalMatrix;
            Rigidbody rb = part.Rigidbody;

            partLocalVel = rb.velocity + frameVel
                        - FARWind.GetWind(FARAeroUtil.CurrentBody, part, rb.position); 
            partLocalVel = matrix.MultiplyVector(partLocalVel);

            partLocalVelNorm = partLocalVel.normalized;

            partLocalAngVel = rb.angularVelocity;
            partLocalAngVel = matrix.MultiplyVector(partLocalAngVel);
        }

        public void OnCenterOfLiftQuery(CenterOfLiftQuery CoLMarker)
        {
            // Compute the actual center ourselves once per frame
            // Feed the precomputed values to the vanilla indicator
            CoLMarker.pos = FerramAerospaceResearch.FARAeroComponents.EditorAeroCenter.VesselRootLocalAeroCenter;      //hacking the old stuff to work with the new
            CoLMarker.pos = EditorLogic.RootPart.transform.localToWorldMatrix.MultiplyPoint3x4(CoLMarker.pos);
            CoLMarker.dir = Vector3.zero;
            CoLMarker.lift = 1;
        }

        private void CheckAeroStressFailure()
        {
            if (partForceMaxY < partLocalForce.y || Vector3.ProjectOnPlane(partLocalForce, Vector3.up).magnitude > partForceMaxXZ)
                ApplyAeroStressFailure();
        }

        private void ApplyAeroStressFailure()
        {
            bool failureOccured = false;
            if (part.Modules.Contains("ModuleProceduralFairing"))
            {
                ModuleProceduralFairing fairing = (ModuleProceduralFairing)part.Modules["ModuleProceduralFairing"];
                fairing.ejectionForce = 0.5f;

                fairing.DeployFairing();
                failureOccured = true;
            }

            List<Part> children = part.children;
            for (int i = 0; i < children.Count; i++)
            {
                Part child = children[i];
                child.decouple(25);


                failureOccured = true;
            }
            if (part.parent)
            {
                part.decouple(25);
                failureOccured = true;
            }

            if (failureOccured)
            {
                if (vessel)
                {
                    vessel.SendMessage("AerodynamicFailureStatus");
                    FlightLogger.eventLog.Add("[" + FARMathUtil.FormatTime(vessel.missionTime) + "] " + part.partInfo.title + " failed due to aerodynamic stresses.");
                    if (FARDebugValues.aeroFailureExplosions)
                    {
                        FXMonger.Explode(part, partTransform.position, (float)projectedArea.totalArea * 0.001f);
                    }
                }
            }
        }

        private void UpdateAeroDisplay()
        {
            Vector3 worldDragArrow = Vector3.zero;
            Vector3 worldLiftArrow = Vector3.zero;

            if (PhysicsGlobals.AeroForceDisplay || PhysicsGlobals.AeroDataDisplay)
            {
                Vector3 worldVelNorm = partTransform.localToWorldMatrix.MultiplyVector(partLocalVelNorm);
                worldDragArrow = Vector3.Dot(worldSpaceAeroForce, worldVelNorm) * worldVelNorm;
                worldLiftArrow = worldSpaceAeroForce - worldDragArrow;
            }
            if (PhysicsGlobals.AeroForceDisplay)
            {
                if (liftArrow == null)
                    liftArrow = ArrowPointer.Create(partTransform, Vector3.zero, worldLiftArrow, worldLiftArrow.magnitude * PhysicsGlobals.AeroForceDisplayScale, Color.cyan, true);
                else
                {
                    liftArrow.Direction = worldLiftArrow;
                    liftArrow.Length = worldLiftArrow.magnitude * PhysicsGlobals.AeroForceDisplayScale;
                }

                if (dragArrow == null)
                    dragArrow = ArrowPointer.Create(partTransform, Vector3.zero, worldDragArrow, worldDragArrow.magnitude * PhysicsGlobals.AeroForceDisplayScale, Color.red, true);
                else
                {
                    dragArrow.Direction = worldDragArrow;
                    dragArrow.Length = worldDragArrow.magnitude * PhysicsGlobals.AeroForceDisplayScale;
                }
            }
            else
            {
                if ((object)liftArrow != null)
                {
                    UnityEngine.Object.Destroy(liftArrow);
                    liftArrow = null;
                }
                if ((object)dragArrow != null)
                {
                    UnityEngine.Object.Destroy(dragArrow);
                    dragArrow = null;
                }
            }

            if (PhysicsGlobals.AeroDataDisplay)
            {
                if (!fieldsVisible)
                {
                    Fields["dragForce"].guiActive = true;
                    Fields["liftForce"].guiActive = true;
                    fieldsVisible = true;
                }

                dragForce = worldDragArrow.magnitude;
                liftForce = worldLiftArrow.magnitude;

            }
            else if (fieldsVisible)
            {
                Fields["dragForce"].guiActive = false;
                Fields["liftForce"].guiActive = false;
                fieldsVisible = false;
            }

        }
    }
}
