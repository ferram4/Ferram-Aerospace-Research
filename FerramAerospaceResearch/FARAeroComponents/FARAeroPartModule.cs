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

namespace FerramAerospaceResearch.FARAeroComponents
{
    //Used to hold relevant aero data for each part before applying it
    public class FARAeroPartModule : PartModule
    {
        public Vector3 partLocalVel;
        public Vector3 partLocalAngVel;
        public Vector3 partLocalNorm;
        Vector3 partLocalForce;
        Vector3 partLocalTorque;


        void Start()
        {
            part.maximum_drag = 0;
            part.minimum_drag = 0;
            part.angularDrag = 0;
            this.enabled = false;

            partLocalVel = Vector3.zero;
            partLocalForce = Vector3.zero;
            partLocalTorque = Vector3.zero;
        }

        public void ApplyForces()
        {
            Rigidbody body = part.Rigidbody;

            body.AddRelativeForce(partLocalForce);
            body.AddRelativeTorque(partLocalTorque);

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
            //this.partLocalForce += Vector3.Cross(partLocalTorque, localRadVector) / localRadVector.sqrMagnitude;
        }

        public void UpdateVelocityAndAngVelocity(Vector3 frameVel)
        {
            partLocalVel = part.Rigidbody.velocity + frameVel;
            partLocalVel = part.transform.worldToLocalMatrix.MultiplyVector(partLocalVel);

            partLocalNorm = partLocalVel.normalized;

            partLocalAngVel = part.Rigidbody.angularVelocity;
            partLocalAngVel = part.transform.worldToLocalMatrix.MultiplyVector(partLocalAngVel);
        }

    
    }
}
