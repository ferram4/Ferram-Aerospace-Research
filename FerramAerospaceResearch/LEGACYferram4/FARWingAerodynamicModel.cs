/*
Ferram Aerospace Research v0.15.5 "Haack"
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
using FerramAerospaceResearch;
using FerramAerospaceResearch.FARAeroComponents;

/// <summary>
/// This calculates the lift and drag on a wing in the atmosphere
/// 
/// It uses Prandtl lifting line theory to calculate the basic lift and drag coefficients and includes compressibility corrections for subsonic and supersonic flows; transsonic regime has placeholder
/// </summary>

namespace ferram4
{
    public class FARWingAerodynamicModel : PartModule
    {
        private struct WingEdge
        {
            public Vector3 vesselLocalInPlanePosition;
            public FARWingAerodynamicModel nextWingAttached;
            public float edgeLocation;      //used to determine sorting for edge points to create wing strips later

            public WingEdge(Vector3 position, FARWingAerodynamicModel nextWing, float edgeLocationFactor)
            {
                vesselLocalInPlanePosition = position;
                nextWingAttached = nextWing;
                edgeLocation = edgeLocationFactor;
            }

            
        }

        private class WingEdgeComparer : Comparer<WingEdge>
        {
            public override int Compare(WingEdge x, WingEdge y)
            {
                return x.edgeLocation.CompareTo(y.edgeLocation);
            }
        }

        Vector3 vesselLocalPlanformNormalVector;
        Vector3 edgeSortingVector;

        List<WingEdge> leadingEdgePoints;
        List<WingEdge> trailingEdgePoints;

        Dictionary<FARWingAerodynamicModel, int> leadingEdgeWingAttached;
        Dictionary<FARWingAerodynamicModel, int> trailingEdgeWingAttached;

        protected Transform part_transform;

        public bool isShielded;
        protected double areaTestFactor;

        protected double NUFAR_areaExposedFactor = 0;
        protected double NUFAR_totalExposedAreaFactor = 0;

        #region wing init
        void Start()
        {
            leadingEdgePoints = new List<WingEdge>();
            trailingEdgePoints = new List<WingEdge>();
            leadingEdgeWingAttached = new Dictionary<FARWingAerodynamicModel, int>();
            trailingEdgeWingAttached = new Dictionary<FARWingAerodynamicModel, int>();
            this.enabled = false;
        }
        #endregion

        #region Edge Data From Vox

        public void ResetEdgesForNewWingCalc()
        {
            leadingEdgePoints.Clear();
            trailingEdgePoints.Clear();

            leadingEdgeWingAttached.Clear();
            trailingEdgeWingAttached.Clear();
        }
        
        public void SetComparisonVectorForSorting(Vector3 vesselForwardPerpVec)
        {
            edgeSortingVector = Vector3.ProjectOnPlane(vesselForwardPerpVec, vesselLocalPlanformNormalVector);
        }
        
        public void AddWingEdgePoint(Vector3 vesselLocalEdgeLocation, FARWingAerodynamicModel nextWingPart, bool isLeadingEdge)
        {
            Vector3 projectedEdgeLocation = Vector3.ProjectOnPlane(vesselLocalEdgeLocation, vesselLocalPlanformNormalVector);

            float edgeLocationFactor = Vector3.Dot(projectedEdgeLocation, edgeSortingVector);   //this is used for sorting the edge points to properly create wing strips later on
            if (isLeadingEdge)
            {
                leadingEdgePoints.Add(new WingEdge(projectedEdgeLocation, nextWingPart, edgeLocationFactor));

                if((object)nextWingPart != null)
                    if (leadingEdgeWingAttached.ContainsKey(nextWingPart))
                        leadingEdgeWingAttached[nextWingPart]++;
                    else
                        leadingEdgeWingAttached.Add(nextWingPart, 1);
            }
            else
            {
                trailingEdgePoints.Add(new WingEdge(projectedEdgeLocation, nextWingPart, edgeLocationFactor));

                if ((object)nextWingPart != null)
                    if (trailingEdgeWingAttached.ContainsKey(nextWingPart))
                        trailingEdgeWingAttached[nextWingPart]++;
                    else
                        trailingEdgeWingAttached.Add(nextWingPart, 1);
            }
        }

        #endregion

        #region Build Wing Strip From Edge Data

        public void SortEdges()
        {
            leadingEdgePoints.Sort();
            trailingEdgePoints.Sort();
        }

        public int numEdgesConnectedToWing(FARWingAerodynamicModel wingToCheck, bool isLeadingEdge)
        {
            int num = 0;
            if (isLeadingEdge)
            {
                if (leadingEdgeWingAttached.TryGetValue(wingToCheck, out num))
                    return num;

                return 0;
            }
            else
            {
                if (trailingEdgeWingAttached.TryGetValue(wingToCheck, out num))
                    return num;

                return 0;
            }
        }

        public bool CheckWingStripsAreValid()
        {
            bool valid;
            valid = leadingEdgePoints.Count == trailingEdgePoints.Count;    //each strip must enter and leave the wing; if this doesn't happen something very bad has happened

            foreach (KeyValuePair<FARWingAerodynamicModel, int> wingCount in leadingEdgeWingAttached)
                valid &= wingCount.Key.numEdgesConnectedToWing(this, false) == wingCount.Value;        //check if the count is correct for all the attached wings; remember, our leading edges are their trailing edges

            foreach (KeyValuePair<FARWingAerodynamicModel, int> wingCount in trailingEdgeWingAttached)
                valid &= wingCount.Key.numEdgesConnectedToWing(this, true) == wingCount.Value;        //check if the count is correct for all the attached wings; remember, our trailing edges are their leading edges

            return valid;   //and if everything is good, return true; otherwise, false
        }
        #endregion

        #region Set Shielded Based On Area
        public void NUFAR_ClearExposedAreaFactor()
        {
            NUFAR_areaExposedFactor = 0;
            NUFAR_totalExposedAreaFactor = 0;
        }

        public void NUFAR_CalculateExposedAreaFactor()
        {
            FARAeroPartModule a = (FARAeroPartModule)part.Modules["FARAeroPartModule"];

            NUFAR_areaExposedFactor = Math.Min(a.ProjectedAreas.kN, a.ProjectedAreas.kP);
            NUFAR_totalExposedAreaFactor = Math.Max(a.ProjectedAreas.kN, a.ProjectedAreas.kP);

        }

        public void NUFAR_SetExposedAreaFactor()
        {
            List<Part> counterparts = part.symmetryCounterparts;
            double counterpartsCount = 1;
            double sum = NUFAR_areaExposedFactor;
            double totalExposedSum = NUFAR_totalExposedAreaFactor;

            for (int i = 0; i < counterparts.Count; i++)
            {
                Part p = counterparts[i];
                if (p == null)
                    continue;
                FARWingAerodynamicModel model;
                if (this is FARControllableSurface)
                    model = (FARWingAerodynamicModel)p.Modules["FARControllableSurface"];
                else
                    model = (FARWingAerodynamicModel)p.Modules["FARWingAerodynamicModel"];

                ++counterpartsCount;
                sum += model.NUFAR_areaExposedFactor;
                totalExposedSum += model.NUFAR_totalExposedAreaFactor;
            }
            double tmp = 1 / (counterpartsCount);
            sum *= tmp;
            totalExposedSum *= tmp;

            NUFAR_areaExposedFactor = sum;
            NUFAR_totalExposedAreaFactor = totalExposedSum;

            for (int i = 0; i < counterparts.Count; i++)
            {
                Part p = counterparts[i];
                if (p == null)
                    continue;
                FARWingAerodynamicModel model;
                if (this is FARControllableSurface)
                    model = (FARWingAerodynamicModel)p.Modules["FARControllableSurface"];
                else
                    model = (FARWingAerodynamicModel)p.Modules["FARWingAerodynamicModel"];

                model.NUFAR_areaExposedFactor = sum;
                model.NUFAR_totalExposedAreaFactor = totalExposedSum;
            }

        }

        public void NUFAR_UpdateShieldingStateFromAreaFactor()
        {
            if (NUFAR_areaExposedFactor < areaTestFactor)
                isShielded = true;
            else
            {
                isShielded = false;
            }
        }
        #endregion
    }
}