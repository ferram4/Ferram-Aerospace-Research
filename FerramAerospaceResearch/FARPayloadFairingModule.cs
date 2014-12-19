/*
Ferram Aerospace Research v0.14.5.1
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
using UnityEngine;
using ferram4.PartExtensions;

namespace ferram4
{
    /// <summary>
    /// This class detects all parts inside it and sets their drag, lift and moment coefficients to zero so as to simulate a payload fairing
    /// </summary>
    public class FARPayloadFairingModule : FARPartModule, TweakScale.IRescalable<FARPayloadFairingModule>
    {
        [KSPField(guiActive = false, guiActiveEditor = true, isPersistant = false)]
        private int partsShielded = 0;


        private List<Part> FARShieldedParts = new List<Part>();
        private List<Vector3> minBounds = new List<Vector3>();
        private List<Vector3> maxBounds = new List<Vector3>();

        public override void Start()
        {
            base.Start();
            OnVesselPartsChange += FindShieldedParts;
            Fields["partsShielded"].guiActive = FARDebugValues.displayShielding;
            FindShieldedParts();
        }

        public override void OnEditorAttach()
        {
            base.OnEditorAttach();

            minBounds.Clear();
            maxBounds.Clear();
            FindShieldedParts();
        }

        [KSPEvent(name = "FairingShapeChanged", active = true, guiActive = false, guiActiveUnfocused = false)]
        public void FairingShapeChanged()
        {
            minBounds.Clear();
            maxBounds.Clear();
            this.PartColliders = part.GetPartColliders();
            FindShieldedParts();
            var d = part.GetComponent<FARBasicDragModel>();
            if (d != null) d.UpdatePropertiesWithShapeChange();
        }



        private void CalculatePartBounds(Part p)
        {
            Vector3 minBoundVec, maxBoundVec;
            minBoundVec = maxBoundVec = Vector3.zero;
            Transform[] transformList = FARGeoUtil.PartModelTransformArray(p);
            for (int i = 0; i < transformList.Length; i++)
            {
                Transform t = transformList[i];

                MeshFilter mf = t.GetComponent<MeshFilter>();
                if ((object)mf == null)
                    continue;
                Mesh m = mf.mesh;

                if ((object)m == null)
                    continue;

                var matrix = part.transform.worldToLocalMatrix * t.localToWorldMatrix;

                for (int j = 0; j < m.vertices.Length; j++)
                {
                    Vector3 v = matrix.MultiplyPoint3x4(m.vertices[j]);

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
                for (int i = 0; i < part.symmetryCounterparts.Count; i++)
                {
                    Part p = part.symmetryCounterparts[i];
                    if (p.GetComponent<FARPayloadFairingModule>() != null)
                    {
                        CalculatePartBounds(p);
                    }
                }
                CalculatePartBounds(part);
            }
            else
                CalculatePartBounds(part);
        }

        private void ClearShieldedParts()
        {
            for (int i = 0; i < FARShieldedParts.Count; i++)
            {
                Part p = FARShieldedParts[i];

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
            /*if (HighLogic.LoadedSceneIsEditor/* && FARAeroUtil.EditorAboutToAttach(false) &&
                !FARAeroUtil.CurEditorParts.Contains(part))
                return;*/
            if (minBounds.Count == 0)
            {
                CalculateFairingBounds();
            }

            ClearShieldedParts();
            UpdateShipPartsList();

            Collider[] colliders = this.PartColliders;
            
            for (int i = 0; i < VesselPartList.Count; i++)
            {
                Part p = VesselPartList[i];

                if (FARShieldedParts.Contains(p) || p == null || p == part || part.symmetryCounterparts.Contains(p))
                    continue;

                FARBaseAerodynamics b = null;
                FARBasicDragModel d = null;
                FARWingAerodynamicModel w = null;
                Vector3 relPos = -part.transform.position;
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
                    relPos += p.partTransform.TransformDirection(d.CenterOfDrag) + p.partTransform.position;       //No attach node shifting with this
                }

                relPos = this.part.transform.worldToLocalMatrix.MultiplyVector(relPos);
                for (int j = 0; j < minBounds.Count; j++)
                {
                    Vector3 minBoundVec, maxBoundVec;
                    minBoundVec = minBounds[j];
                    maxBoundVec = maxBounds[j];

                    Vector3 fairingCenter = maxBoundVec + minBoundVec;
                    fairingCenter *= 0.5f;
                    fairingCenter = this.part.partTransform.localToWorldMatrix.MultiplyVector(fairingCenter);
                    fairingCenter += this.part.partTransform.position;

                    if (relPos.x < maxBoundVec.x && relPos.y < maxBoundVec.y && relPos.z < maxBoundVec.z && relPos.x > minBoundVec.x && relPos.y > minBoundVec.y && relPos.z > minBoundVec.z)
                    {
                        
                        Vector3 vecFromPToPFCenter;
                        Vector3 origin;
                        if (w)
                            origin = w.WingCentroid();
                        else
                            origin = p.partTransform.position;

                        vecFromPToPFCenter = fairingCenter - origin;

                        RaycastHit[] hits = Physics.RaycastAll(origin, vecFromPToPFCenter, vecFromPToPFCenter.magnitude, FARAeroUtil.RaycastMask);

                        bool outsideMesh = false;

                        for (int k = 0; k < hits.Length; k++)
                        {
                            if (colliders.Contains(hits[k].collider))
                            {
                                outsideMesh = true;
                                break;
                            }
                        }
                        if (outsideMesh)
                            continue;

                        FARShieldedParts.Add(p);
                        if (b)
                        {
                            b.ActivateShielding();
                            //print("Shielded: " + p.partInfo.title);
                        }
                        for (int k = 0; k < p.symmetryCounterparts.Count; k++)
                        {
                            Part q = p.symmetryCounterparts[k];

                            if (q == null)
                                continue;
                            FARShieldedParts.Add(q);
                            b = q.GetComponent<FARBaseAerodynamics>();
                            if (b)
                            {
                                b.ActivateShielding();
                                //print("Shielded: " + p.partInfo.title);
                            }
                        }
                        break;
                    }
                }
            }
            partsShielded = FARShieldedParts.Count;
        }

        //Blank save node ensures that nothing for this partmodule is saved
        public override void OnSave(ConfigNode node)
        {
            //base.OnSave(node);
        }

        public void OnRescale(TweakScale.ScalingFactor factor)
        {
            FairingShapeChanged();
        }
    }
}                                               