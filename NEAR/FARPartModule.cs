/*
Neophyte's Elementary Aerodynamics Replacement v1.1.1
Copyright 2014, Michael Ferrara, aka Ferram4

    This file is part of Neophyte's Elementary Aerodynamics Replacement.

    Neophyte's Elementary Aerodynamics Replacement is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Kerbal Joint Reinforcement is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Neophyte's Elementary Aerodynamics Replacement.  If not, see <http://www.gnu.org/licenses/>.

    Serious thanks:		a.g., for tons of bugfixes and code-refactorings
            			Taverius, for correcting a ton of incorrect values
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
            			ialdabaoth (who is awesome), who originally created Module Manager
            			Duxwing, for copy editing the readme
 *
 * Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/55219
 *
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NEAR
{
    public class FARPartModule : PartModule
    {
        protected Callback OnVesselPartsChange;
        public List<Part> VesselPartList = null;
        int VesselPartListCount = 0;

        public void ForceOnVesselPartsChange()
        {
            if(OnVesselPartsChange != null)
                OnVesselPartsChange();
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);
            OnVesselPartsChange = UpdateShipPartsList;
            UpdateShipPartsList();

            if (HighLogic.LoadedSceneIsEditor)
            {
                part.OnEditorAttach += OnEditorAttach;
                part.OnEditorDetach += OnEditorAttach;
                part.OnEditorDestroy += OnEditorAttach;
            }
        }        

        public virtual void OnEditorAttach()
        {
            //print(part + " OnEditorAttach");

            FARGlobalControlEditorObject.EditorPartsChanged = true;
        }

        protected void UpdateShipPartsList()
        {
            VesselPartList = GetShipPartList();
        }

        public List<Part> GetShipPartList()
        {
            List<Part> list = null;
            if (HighLogic.LoadedSceneIsEditor)
                list = FARAeroUtil.AllEditorParts;
            else if (vessel)
                list = vessel.Parts;
            else
            {
                list = new List<Part>();
                if (part)
                    list.Add(part);
            }
            VesselPartListCount = list.Count;

//            print("Updated Vessel Part List...");

            return list;
        }

        //public override void OnSave(ConfigNode node)
        //{
        //    //By blanking this nothing should be saved to the craft file or the persistance file
        //}

    }
}
