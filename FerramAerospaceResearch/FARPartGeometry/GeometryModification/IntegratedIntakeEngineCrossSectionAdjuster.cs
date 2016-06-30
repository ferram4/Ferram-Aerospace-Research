/*
Ferram Aerospace Research v0.15.7.1 "Kutta"
=========================
Aerodynamics model for Kerbal Space Program

Copyright 2015, Michael Ferrara, aka Ferram4

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
				stupid_chris, for the RealChuteLite implementation
            			Taverius, for correcting a ton of incorrect values  
				Tetryds, for finding lots of bugs and issues and not letting me get away with them, and work on example crafts
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager updates  
            			ialdabaoth (who is awesome), who originally created Module Manager  
                        	Regex, for adding RPM support  
				DaMichel, for some ferramGraph updates and some control surface-related features  
            			Duxwing, for copy editing the readme  
   
   CompatibilityChecker by Majiir, BSD 2-clause http://opensource.org/licenses/BSD-2-Clause

   Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission  
	http://forum.kerbalspaceprogram.com/threads/55219

   ModularFLightIntegrator by Sarbian, Starwaster and Ferram4, MIT: http://opensource.org/licenses/MIT
	http://forum.kerbalspaceprogram.com/threads/118088

   Toolbar integration powered by blizzy78's Toolbar plugin; used with permission  
	http://forum.kerbalspaceprogram.com/threads/60863
 */

using System;
using System.Reflection;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry.GeometryModification
{
    class IntegratedIntakeEngineCrossSectionAdjuster : ICrossSectionAdjuster
    {
        const double INTAKE_AREA_SCALAR = 100;

        Vector3 vehicleBasisForwardVector;
        double intakeArea;
        int sign = 1;

        Matrix4x4 thisToVesselMatrix;
        Matrix4x4 meshLocalToWorld;
        ModuleResourceIntake intakeModule;
        Transform intakeTrans;

        Part part;
        public Part GetPart()
        {
            return part;
        }

        public bool IntegratedCrossSectionIncreaseDecrease()
        {
            return intakeModule.node == null || intakeModule.node.attachedPart == null;
        }

        public static IntegratedIntakeEngineCrossSectionAdjuster CreateAdjuster(PartModule intake, Matrix4x4 worldToVesselMatrix)
        {
            IntegratedIntakeEngineCrossSectionAdjuster adjuster = new IntegratedIntakeEngineCrossSectionAdjuster();
            adjuster.SetupAdjuster(intake, worldToVesselMatrix);

            return adjuster;
        }

        public static IntegratedIntakeEngineCrossSectionAdjuster CreateAdjuster(ModuleResourceIntake intake, Matrix4x4 worldToVesselMatrix)
        {
            IntegratedIntakeEngineCrossSectionAdjuster adjuster = new IntegratedIntakeEngineCrossSectionAdjuster();
            adjuster.SetupAdjuster(intake, worldToVesselMatrix);

            return adjuster;
        }

        private IntegratedIntakeEngineCrossSectionAdjuster() { }

        public void SetupAdjuster(PartModule intake, Matrix4x4 worldToVesselMatrix)
        {
            this.part = intake.part;
            intakeModule = intake as ModuleResourceIntake;
            intakeTrans = intakeModule.intakeTransform;
            //ModuleResourceIntake intake = intake;


            /*vehicleBasisForwardVector = Vector3.forward;//intakeTrans.forward;

            foreach(AttachNode node in part.attachNodes)
                if(node.nodeType == AttachNode.NodeType.Stack && Vector3.Dot(node.position, (part.transform.worldToLocalMatrix * intakeTrans.localToWorldMatrix).MultiplyVector(Vector3.forward)) > 0)
                {
                    frontNode = node;
                    break;
                }*/

            thisToVesselMatrix = worldToVesselMatrix * intakeTrans.localToWorldMatrix;

            vehicleBasisForwardVector = Vector3.forward;
            vehicleBasisForwardVector = thisToVesselMatrix.MultiplyVector(vehicleBasisForwardVector);

            Type intakeType = intake.GetType();
            intakeArea = (float)intakeType.GetField("Area").GetValue(intake);
        }

        public void SetupAdjuster(ModuleResourceIntake intake, Matrix4x4 worldToVesselMatrix)
        {
            this.part = intake.part;
            intakeModule = intake as ModuleResourceIntake;
            intakeTrans = intakeModule.intakeTransform;
            //ModuleResourceIntake intake = intake;

            /*intakeTrans = part.FindModelTransform(intake.intakeTransformName);
            vehicleBasisForwardVector = Vector3.forward;//intakeTrans.forward;

            foreach (AttachNode node in part.attachNodes)
                if (node.nodeType == AttachNode.NodeType.Stack && Vector3.Dot(node.position, (part.transform.worldToLocalMatrix * intakeTrans.localToWorldMatrix).MultiplyVector(Vector3.forward)) > 0)
                {
                    frontNode = node;
                    break;
                }*/

            thisToVesselMatrix = worldToVesselMatrix * intakeTrans.localToWorldMatrix;

            vehicleBasisForwardVector = Vector3.forward;
            vehicleBasisForwardVector = thisToVesselMatrix.MultiplyVector(vehicleBasisForwardVector);

            intakeArea = INTAKE_AREA_SCALAR * intake.area;

            Debug.Log("Integrated cross-section adjuster");
        }

        public double AreaRemovedFromCrossSection(Vector3 vehicleAxis)
        {
            double dot = Vector3.Dot(vehicleAxis, vehicleBasisForwardVector);
            if (dot > 0.9)
                return intakeArea;
            else
                return 0;
        }

        public double AreaRemovedFromCrossSection()
        {
            if (intakeModule.node == null || intakeModule.node.attachedPart == null)
                return intakeArea * sign;
            else
                return -intakeArea * sign;      //if the intake is covered, switch the math so that it functions like an AirbreathingEngineCrossSectionAdjuster instead
        }

        public double AreaThreshold()
        {
            return 0;
        }

        public void SetForwardBackwardNoFlowDirection(int sign)
        {
            this.sign = sign;
        }

        public int GetForwardBackwardNoFlowSign() { return sign; }
        
        public void TransformBasis(Matrix4x4 matrix)
        {
            Matrix4x4 tempMatrix = thisToVesselMatrix.inverse;
            thisToVesselMatrix = matrix * meshLocalToWorld;

            tempMatrix = thisToVesselMatrix * tempMatrix;

            vehicleBasisForwardVector = tempMatrix.MultiplyVector(vehicleBasisForwardVector);

        }


        public void SetThisToVesselMatrixForTransform()
        {
            meshLocalToWorld = intakeTrans.localToWorldMatrix;
        }

        public void UpdateArea() { }
    }
}
