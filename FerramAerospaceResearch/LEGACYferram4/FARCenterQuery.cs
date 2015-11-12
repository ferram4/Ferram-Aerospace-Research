/*
Ferram Aerospace Research v0.15.5.3 "von Helmholtz"
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
using System.Collections.Generic;
using UnityEngine;

namespace ferram4
{
    // An accumulator class for summarizing a set of forces acting on the body and calculating the AerodynamicCenter
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

        /*public Vector3d GetACPosition()
        {
            Vector3d bVec = new Vector3d();
            bVec.x = force.y * torque.z - force.z * torque.y;
            bVec.y = force.x * torque.z - force.z * torque.x;
            bVec.z = force.x * torque.y - force.y * torque.x;

            double approxA01, approxA02, approxA12;         //commonly used force directions
            approxA01 = -1 / (force.z * force.z);
            approxA02 = -1 / (force.y * force.y);
            approxA12 = -1 / (force.x * force.x);

            Vector3d acPos = new Vector3d();
            acPos.x = force.x * force.x / (force.y * force.y * force.z * force.z) * bVec.x + bVec.y * approxA01 + bVec.z * approxA02;
            acPos.y = force.y * force.y / (force.x * force.x * force.z * force.z) * bVec.y + bVec.x * approxA01 + bVec.z * approxA12;
            acPos.x = force.z * force.z / (force.y * force.y * force.x * force.x) * bVec.z + bVec.y * approxA12 + bVec.x * approxA02;

            acPos *= 0.5;
            acPos += GetPos();

            return acPos;
        }*/
        public void ClearAll()
        {
            force = Vector3d.zero;
            torque = Vector3d.zero;
            pos = Vector3d.zero;
            amount = 0;
        }

        // Record a force applied at a point
        public void AddForce(Vector3d npos, Vector3d nforce)
        {
            double size = nforce.magnitude;
            force += nforce;
            torque += Vector3d.Cross(npos, nforce);
            pos += npos * size;
            amount += size;
        }

        // Record an abstracted torque or couple; application point is irrelevant
        public void AddTorque(Vector3d ntorque)
        {
            torque += ntorque;
        }

        // Merge two force sets
        public void AddAll(FARCenterQuery q2)
        {
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
            float acceleration = new Vector3(tq.x / tensor.x, tq.y / tensor.y, tq.z / tensor.z).magnitude;
            return Mathf.Max(1.0f, acceleration * Time.fixedDeltaTime / body.maxAngularVelocity);
        }
    }
}
