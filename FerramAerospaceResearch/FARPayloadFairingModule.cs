/*
Ferram Aerospace Research v0.14.7
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
        private Bounds fairingBounds;

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

            FindShieldedParts();
        }

        [KSPEvent(name = "FairingShapeChanged", active = true, guiActive = false, guiActiveUnfocused = false)]
        public void FairingShapeChanged()
        {
            this.TriggerPartColliderUpdate();
            this.TriggerPartBoundsUpdate();
            FindShieldedParts();
            var d = part.GetComponent<FARBasicDragModel>();
            if (d != null) d.UpdatePropertiesWithShapeChange();
        }



        private void CalculatePartBounds(Part p)
        {
            FARPartModule m = p.GetComponent<FARPartModule>();
            if(m == null)
                return;
            if (p == this.part)
                for(int i = 0; i < m.PartBounds.Length; i++)
                    fairingBounds.Encapsulate(m.PartBounds[i]);
            else
            {
                Matrix4x4 matrix = part.transform.worldToLocalMatrix * p.transform.localToWorldMatrix;
                for(int i = 0; i < m.PartBounds.Length; i++)
                {
                    Bounds bounds = m.PartBounds[i];
                    bounds.SetMinMax(matrix.MultiplyPoint(bounds.min), matrix.MultiplyPoint(bounds.max));
                    fairingBounds.Encapsulate(bounds);
                }
            }
        }


        private void CalculateFairingBounds()
        {
            fairingBounds = new Bounds();
            if (part.parent != null)
            {
                for (int i = 0; i < part.symmetryCounterparts.Count; i++)
                {
                    Part p = part.symmetryCounterparts[i];
                    if (p != null && p.GetComponent<FARPayloadFairingModule>() != null)
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

            ClearShieldedParts();
            UpdateShipPartsList();
            CalculateFairingBounds();

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
                    relPos += p.transform.TransformDirection(d.CenterOfDrag) + p.transform.position;       //No attach node shifting with this
                }

                relPos = this.part.transform.worldToLocalMatrix.MultiplyVector(relPos);

                Vector3 fairingCenter = fairingBounds.center;
                fairingCenter *= 0.5f;
                fairingCenter = this.part.transform.localToWorldMatrix.MultiplyVector(fairingCenter);
                fairingCenter += this.part.transform.position;

                if (fairingBounds.Contains(relPos))
                {
                        
                    Vector3 vecFromPToPFCenter;
                    Vector3 origin;
                    if (w)
                        origin = w.WingCentroid();
                    else
                        origin = p.transform.position;

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
            Debug.Log("Rescaled Fairing");
            FairingShapeChanged();
        }
    }
}                                               