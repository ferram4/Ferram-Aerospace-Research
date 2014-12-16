/*
Neophyte's Elementary Aerodynamics Replacement v1.3.1
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
using System.Linq;
using UnityEngine;
using KSP;

namespace NEAR
{
    namespace PartExtensions
    {
        public static class FARPartExtensions
        {
            public static Collider[] GetPartColliders(this Part part)
            {
                Collider[] colliders;
                try
                {
                    if (HighLogic.LoadedSceneIsEditor)
                    {
                        Collider[] tmpColliderArray = part.GetComponentsInChildren<Collider>(); //In the editor, this returns all the colliders of this part AND all the colliders of its children, recursively
                        //However, this can also be called on its child parts to get their colliders, so we can exclude the child colliders
                        //Also, fortunately, parent colliders are at the beginning of this; we can take advantage of this to reduce the time iterating through lists
                        List<Collider> partColliders = new List<Collider>();
                        HashSet<Collider> excludedCollidersHash = new HashSet<Collider>();      //We'll use a hash to make this fast

                        for (int i = 0; i < part.children.Count; i++)
                        {
                            Part p = part.children[i];
                            Collider[] excludedColliders = p.GetComponentsInChildren<Collider>();   //All the colliders associated with the immediate child of this part AND their children

                            if (!excludedCollidersHash.Contains(excludedColliders[0]))  //The first collider _must_ be part of the immediate child; because it is closer to the parent, it will appear earlier in tmpColliderArray
                                excludedCollidersHash.Add(excludedColliders[0]);        //That means we only ever need the first collider for our purposes
                        }

                        for (int i = 0; i < tmpColliderArray.Length; i++)
                            if (!excludedCollidersHash.Contains(tmpColliderArray[i]))   //If the collider isn't in the hash, that means that it must belong to _this_ part, because it doesn't belong to any child parts
                                partColliders.Add(tmpColliderArray[i]);
                            else
                                break;  //Once we find something that is in the hash, we're out of the colliders associated with the parent part and can escape

                        colliders = partColliders.ToArray();
                    }
                    else
                        colliders = part.GetComponentsInChildren<Collider>();
                }
                catch (Exception e)
                {
                    //Fail silently because it's the only way to avoid issues with pWings
                    //Debug.LogException(e);
                    colliders = new Collider[1] { part.collider };
                }

                return colliders;
            }
        }
    }
}
