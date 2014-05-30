/*
Ferram Aerospace Research v0.13.3
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
using System.Linq;
using UnityEngine;

namespace ferram4
{
    /// <summary>
    /// This class detects all parts inside it and sets their drag, lift and moment coefficients to zero so as to simulate a payload fairing
    /// </summary>
    public class FARPayloadFairingModule : FARPartModule
    {
        [KSPField(guiActive = false, isPersistant = false)]
        private int partsShielded = 0;


        private List<Part> FARShieldedParts = new List<Part>();
        private List<Vector3> minBounds = new List<Vector3>();
        private List<Vector3> maxBounds = new List<Vector3>();

        //private Vector3 minBounds = new Vector3();

        //private Vector3 maxBounds = new Vector3();
        private static StartState state;

//        private LineRenderer line = null;


        public override void OnStart(StartState start)
        {
            state = start;
            base.OnStart(start);
            OnVesselPartsChange += FindShieldedParts;
            Fields["partsShielded"].guiActive = FARDebugValues.displayShielding;
        }

        public override void OnEditorAttach()
        {
            base.OnEditorAttach();

            ClearShieldedParts();
            minBounds.Clear();
            maxBounds.Clear();
        }

        public override void FixedUpdate()
        {
//            if (start == StartState.Editor)
//                return;

            if (minBounds.Count == 0)
            {
                CalculateFairingBounds();
                FindShieldedParts();
            }
            
//            line.SetPosition(0, minBounds + part.transform.position);
//            line.SetPosition(1, maxBounds + part.transform.position);
            
        }



        [KSPEvent(name = "FairingShapeChanged", active = true, guiActive = false, guiActiveUnfocused = false)]
        public void FairingShapeChanged()
        {
            minBounds.Clear();
            maxBounds.Clear();
            var d = part.GetComponent<FARBasicDragModel>();
            if (d != null) d.UpdatePropertiesWithShapeChange();
        }



        private void CalculatePartBounds(Part p)
        {
            Vector3 minBoundVec, maxBoundVec;
            minBoundVec = maxBoundVec = Vector3.zero;
            foreach (Transform t in p.FindModelComponents<Transform>())
            {
                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (mf == null)
                    continue;
                Mesh m = mf.mesh;

                if (m == null)
                    continue;

                var matrix = part.transform.worldToLocalMatrix * t.localToWorldMatrix;

                foreach (Vector3 vertex in m.vertices)
                {
                    Vector3 v = matrix.MultiplyPoint3x4(vertex);

                    maxBoundVec.x = Mathf.Max(maxBoundVec.x, v.x);
                    minBoundVec.x = Mathf.Min(minBoundVec.x, v.x);
                    maxBoundVec.y = Mathf.Max(maxBoundVec.y, v.y);
                    minBoundVec.y = Mathf.Min(minBoundVec.y, v.y);
                    maxBoundVec.z = Mathf.Max(maxBoundVec.z, v.z);
                    minBoundVec.z = Mathf.Min(minBoundVec.z, v.z);
                }
                minBoundVec.x *= 1.05f;
                maxBoundVec.x *= 1.05f;
                minBoundVec.z *= 1.05f;
                maxBoundVec.z *= 1.05f;
            }
            minBounds.Add(minBoundVec);
            maxBounds.Add(maxBoundVec);
        }


        private void CalculateFairingBounds()
        {
            if (part.parent != null)
            {
                foreach (Part p in part.symmetryCounterparts)
                    if (p.GetComponent<FARPayloadFairingModule>() != null)
                    {
                        CalculatePartBounds(p);
                    }

                CalculatePartBounds(part);
            }
            else
                CalculatePartBounds(part);

            //minBounds.x *= 1.05f;
            //maxBounds.x *= 1.05f;
            //minBounds.z *= 1.05f;
            //maxBounds.z *= 1.05f;
        }

        private void ClearShieldedParts()
        {
            foreach (Part p in FARShieldedParts)
            {
                if (p == null || p.Modules == null)
                    continue;
                FARBaseAerodynamics b = null;
                b = p.GetComponent<FARWingAerodynamicModel>() as FARBaseAerodynamics;
                if (b == null)
                    b = p.GetComponent<FARBasicDragModel>() as FARBaseAerodynamics;
                if (b == null)
                    continue;
                b.isShielded = false;
            }
            FARShieldedParts.Clear();
            partsShielded = 0;
        }

        private void FindShieldedParts()
        {
            if (start == StartState.Editor && FARAeroUtil.EditorAboutToAttach(false) &&
                !FARAeroUtil.CurEditorParts.Contains(part))
                return;

            if (minBounds.Count == 0)
            {
                CalculateFairingBounds();
            }

            ClearShieldedParts();
            UpdateShipPartsList();

            foreach (Part p in VesselPartList)
            {
                if (FARShieldedParts.Contains(p) || p == null || p == part || part.symmetryCounterparts.Contains(p))
                    continue;
                
                FARBaseAerodynamics b = null;
                FARBasicDragModel d = null;
                FARWingAerodynamicModel w = null;
                Vector3 relPos = -this.part.transform.position;
                w = p.GetComponent<FARWingAerodynamicModel>();
                if ((object)w == null)
                {
                    d = p.GetComponent<FARBasicDragModel>();
                }
                if ((object)w == null && (object)d == null)
                    continue;
                //if (p.GetComponent<FARPayloadFairingModule>() != null)
                //    continue;
                if (w)
                {
                    b = w as FARBaseAerodynamics;
                    relPos += w.WingCentroid();
                }
                else
                {
                    b = d as FARBaseAerodynamics;
                    relPos += p.transform.TransformPoint(d.CenterOfDrag);       //No attach node shifting with this
                }


                relPos = this.part.transform.worldToLocalMatrix.MultiplyVector(relPos);
                for (int i = 0; i < minBounds.Count; i++)
                {
                    Vector3 minBoundVec, maxBoundVec;
                    minBoundVec = minBounds[i];
                    maxBoundVec = maxBounds[i];
                    if (relPos.x < maxBoundVec.x && relPos.y < maxBoundVec.y && relPos.z < maxBoundVec.z && relPos.x > minBoundVec.x && relPos.y > minBoundVec.y && relPos.z > minBoundVec.z)
                    {
                        FARShieldedParts.Add(p);
                        if (b)
                        {
                            b.isShielded = true;
                            //print("Shielded: " + p.partInfo.title);
                        }
                        foreach (Part q in p.symmetryCounterparts)
                        {
                            if (q == null)
                                continue;
                            FARShieldedParts.Add(q);
                            b = q.GetComponent<FARBaseAerodynamics>();
                            if (b)
                            {
                                b.isShielded = true;
                                //print("Shielded: " + p.partInfo.title);
                            }
                        }
                        break;
                    }
                }
            }
            partsShielded = FARShieldedParts.Count;
        }
    }
}                                               