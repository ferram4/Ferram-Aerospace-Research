/*
Ferram Aerospace Research v0.13.1
Copyright 2013, Michael Ferrara, aka Ferram4

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
using System.Reflection;
using System.Linq;
using UnityEngine;

namespace ferram4
{
    public class FARCargoBayModule : FARPartModule
    {
        [KSPField(guiActive = true, guiActiveEditor = true, isPersistant = false)]
        private int partsShielded = 0;


        private List<Part> FARShieldedParts = new List<Part>();
        

        private Vector3 minBounds = new Vector3();

        private Vector3 maxBounds = new Vector3();
        private static StartState state;

        private bool bayOpen = true;

        //private PartModule BayAnim = null;
        private static int frameCounterCargo = 0;
        private static FARCargoBayModule BayController;

        private Animation bayAnim = null;
        private string bayAnimationName;

//        [KSPField(guiActive = true, isPersistant = false)]
//        private float bayProgress = 0;

        private bool bayAnimating = true;

//        private LineRenderer line = null;

        [KSPEvent]
        private void UpdateCargoParts()
        {
            if (start == StartState.Editor && FARAeroUtil.EditorAboutToAttach(false) &&
                !FARAeroUtil.CurEditorParts.Contains(part))
                return;

            if (bayAnim == null || !bayOpen || start == StartState.Editor)
                FindShieldedParts();
        }

        public override void OnStart(StartState start)
        {
            state = start;
            base.OnStart(start);
            BayAnimationSetup();
            OnVesselPartsChange += UpdateCargoParts;
        }

        public override void OnEditorAttach()
        {
            base.OnEditorAttach();

            minBounds = maxBounds = Vector3.zero;
            ClearShieldedParts();
        }

        private void BayAnimationSetup()
        {
            foreach (PartModule m in part.Modules)
            {
                FieldInfo field = m.GetType().GetField("animationName");

                if (field != null)
                {
                    bayAnimationName = (string)field.GetValue(m);
                    bayAnim = part.FindModelAnimators(bayAnimationName).FirstOrDefault();

                    if (bayAnim != null)
                        break;
                }
            }
            //bayProgress = bayAnim[bayAnimationName].normalizedTime;
        }


        private bool RaycastingFunction(Vector3 direction)
        {
            if (minBounds == Vector3.zero)
                CalculateBayBounds();

            Ray ray = new Ray();

            ray.origin = part.transform.position;       //Set ray to start at center
            ray.direction = direction;

            bool hitMyself = false;

            // Make sure the raycast sphere fits into the bay
            Vector3 size = maxBounds - minBounds;
            float radius = Mathf.Min(1f, Mathf.Min(size.x, size.y, size.z) * 0.15f);

            RaycastHit[] hits = Physics.SphereCastAll(ray, radius, 100, FARAeroUtil.RaycastMask);
            foreach (RaycastHit h in hits)
            {
                if (h.collider.attachedRigidbody)
                    if(h.collider.attachedRigidbody.GetComponent<Part>() == this.part)
                    {
                        hitMyself = true;
                    }
                if (hitMyself)
                    break;
            }


            return hitMyself;
        }


        private bool CheckBayClosed()
        {
            Vector3 forward = part.transform.forward, right = part.transform.right;

            int count = 8;
            float step = 2 * Mathf.PI / count;

            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Mathf.Cos(i*step) * forward + Mathf.Sin(i*step) * right;

                if (!RaycastingFunction(dir))
                    return false;
            }

            return true;
        }

        public override void FixedUpdate()
        {

            base.FixedUpdate();

            UpdateShieldedParts();
            
            if (this == BayController)
            {
                frameCounterCargo++;
                if (frameCounterCargo > 10)
                {
                    part.SendEvent("UpdateCargoParts");
                    frameCounterCargo = 0;
                }
            }
        }

        private void UpdateShieldedParts()
        {
            if (start == StartState.Editor)
                return;

            if (bayAnim)
            {
                if (bayAnim.isPlaying && !bayAnimating)
                {
                    ClearShieldedParts();
                    bayAnimating = true;
                }
                else if (bayAnimating && !bayAnim.isPlaying)
                {
                    bayAnimating = false;

                    if (bayOpen && CheckBayClosed())
                        FindShieldedParts();
                }
//                bayProgress = bayAnim[bayAnimationName].normalizedTime;

            }
            else if (BayController == null)
                BayController = this;

        }

        private void CalculateBayBounds()
        {
            foreach (Transform t in part.FindModelComponents<Transform>())
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

                    maxBounds.x = Mathf.Max(maxBounds.x, v.x);
                    minBounds.x = Mathf.Min(minBounds.x, v.x);
                    maxBounds.y = Mathf.Max(maxBounds.y, v.y);
                    minBounds.y = Mathf.Min(minBounds.y, v.y);
                    maxBounds.z = Mathf.Max(maxBounds.z, v.z);
                    minBounds.z = Mathf.Min(minBounds.z, v.z);
                }
            }
            minBounds.x *= 0.98f;
            maxBounds.x *= 0.98f;
            minBounds.z *= 0.98f;
            maxBounds.z *= 0.98f;
        }

        private void FindShieldedParts()
        {
            if (minBounds == new Vector3())
            {
                CalculateBayBounds();
            }
            ClearShieldedParts();
            bayOpen = false;
            UpdateShipPartsList();

            float y_margin = Mathf.Max(0.12f, 0.03f * (maxBounds.y-minBounds.y));

            foreach (Part p in VesselPartList)
            {
                if (FARShieldedParts.Contains(p)|| p == null || p == part || part.symmetryCounterparts.Contains(p))
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
                    relPos += w.AerodynamicCenter;
                }
                else
                {
                    b = d as FARBaseAerodynamics;
                    relPos += p.transform.TransformPoint(d.CenterOfDrag);       //No attach node shifting with this
                }

                relPos = this.part.transform.worldToLocalMatrix.MultiplyVector(relPos);
                if (relPos.x < maxBounds.x && relPos.y < maxBounds.y+y_margin && relPos.z < maxBounds.z && relPos.x > minBounds.x && relPos.y > minBounds.y-y_margin && relPos.z > minBounds.z)
                {
                    if (relPos.y > maxBounds.y || relPos.y < minBounds.y)
                    {
                        // Enforce strict y bounds for parent and stack children
                        if (p == this.part.parent ||
                            p.parent == this.part && p.attachMode == AttachModes.STACK)
                            continue;
                    }
/*                    if (w)
                    {
                        if (w.nonSideAttach <= 0)
                        {
                            relPos = p.transform.position - this.part.transform.position;
                            relPos -= p.transform.right * Mathf.Sign(p.srfAttachNode.originalOrientation.x) * w.b_2;

                            relPos = this.part.transform.worldToLocalMatrix.MultiplyVector(relPos);

                            if (!(relPos.x < maxBounds.x && relPos.y < maxBounds.y && relPos.z < maxBounds.z && relPos.x > minBounds.x && relPos.y > minBounds.y && relPos.z > minBounds.z))
                                continue;
                        }
                    }*/
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
                }
            }
            if(start != StartState.Editor)
                foreach (Vessel v in FlightGlobals.Vessels)
                {
                    if (v == this.vessel)
                        continue;

                    Vector3 relPos = v.transform.position - this.part.transform.position;
                    relPos = this.part.transform.worldToLocalMatrix.MultiplyVector(relPos);
                    if (relPos.x < maxBounds.x && relPos.y < maxBounds.y && relPos.z < maxBounds.z && relPos.x > minBounds.x && relPos.y > minBounds.y && relPos.z > minBounds.z)
                    {
                        foreach (Part p in v.Parts)
                        {
                            FARBaseAerodynamics b = null;
                            b = p.GetComponent<FARBaseAerodynamics>();
                            if (b == null)
                                continue;

                            FARShieldedParts.Add(p);
                            if (b)
                            {
                                b.isShielded = true;
                                //print("Shielded: " + p.partInfo.title);
                            }

                        }
                    }
                }
            partsShielded = FARShieldedParts.Count;
        }

        private void ClearShieldedParts()
        {
//            print("Clearing Parts in Cargo Bay...");
            foreach (Part p in FARShieldedParts)
            {
                if (p == null)
                    continue;
                FARBaseAerodynamics b = p.GetComponent<FARWingAerodynamicModel>() as FARBaseAerodynamics;
                if (b == null)
                    b = p.GetComponent<FARBasicDragModel>() as FARBaseAerodynamics;
                if (b == null)
                    continue;

                b.isShielded = false;
            }
            FARShieldedParts.Clear();
            bayOpen = true;
            partsShielded = 0;
        }
    }
}