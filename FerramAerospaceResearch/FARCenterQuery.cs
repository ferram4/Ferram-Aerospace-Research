/*
Ferram Aerospace Research v0.14.4
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

        /*//Component of ac position created by part location
        public Vector3d acPartPosComponent = Vector3d.zero;

        //Component of ac position due to interactions with location of AC on other axes
        public Vector3d acAxisInteractComponent = Vector3d.zero;

        public void AddAerodynamicForcesAndMoments(Vector3d force, Vector3d moment, Vector3d pos)
        {
            double tmp;

            tmp = force.x / force.y;
            acPartPosComponent.x = tmp * pos.y - moment.z + pos.x;
            acAxisInteractComponent.x = moment.z - tmp;

            tmp = force.y / force.z;
            acPartPosComponent.y = tmp * pos.z - moment.x + pos.y;
            acAxisInteractComponent.y = moment.x - tmp;

            tmp = force.z / force.x;
            acPartPosComponent.z = tmp * pos.x - moment.y + pos.z;
            acAxisInteractComponent.z = moment.y - tmp;
        }

        public Vector3d GetACPosition()
        {
            double denominator = 1 - acAxisInteractComponent.x * acAxisInteractComponent.y * acAxisInteractComponent.z;

            Vector3d acPos = Vector3d.zero;

            acPos.x = acPartPosComponent.x
                + acPartPosComponent.y * acAxisInteractComponent.x
                + acPartPosComponent.z * acAxisInteractComponent.x * acAxisInteractComponent.y;

            acPos.y = acPartPosComponent.y
                + acPartPosComponent.z * acAxisInteractComponent.y
                + acPartPosComponent.x * acAxisInteractComponent.y * acAxisInteractComponent.z;

            acPos.z = acPartPosComponent.z
                + acPartPosComponent.x * acAxisInteractComponent.z
                + acPartPosComponent.y * acAxisInteractComponent.z * acAxisInteractComponent.x;

            acPos /= denominator;

            return acPos;
        }*/

        // Record a force applied at a point
        public void AddForce(Vector3d npos, Vector3d nforce)
        {
            double size = nforce.magnitude;
            force += nforce;
            torque += Vector3d.Cross(npos, nforce);
            pos += npos * size;
            amount += size;
        }

        // Record an abstracted torque; point of application does not matter.
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
