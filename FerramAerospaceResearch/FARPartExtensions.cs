/*
Ferram Aerospace Research v0.15.6.1 "von Kármán"
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
using System.Linq;
using UnityEngine;
using KSP;

namespace FerramAerospaceResearch
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
                catch
                {   //FIXME
                    //Fail silently because it's the only way to avoid issues with pWings
                    //Debug.LogException(e);
                    colliders = new Collider[1] { part.collider };
                }

                return colliders;
            }
            
            public static Bounds[] GetPartMeshBoundsInPartSpace(this Part part, int excessiveVerts = 2500)
            {
                Transform[] transforms = part.FindModelComponents<Transform>();
                Bounds[] bounds = new Bounds[transforms.Length];
                Matrix4x4 partMatrix = part.partTransform.worldToLocalMatrix;
                for(int i = 0; i < transforms.Length; i++)
                {
                    Bounds newBounds = new Bounds();
                    Transform t = transforms[i];

                    MeshFilter mf = t.GetComponent<MeshFilter>();
                    if (mf == null)
                        continue;
                    Mesh m = mf.sharedMesh;

                    if (m == null)
                        continue;
                    Matrix4x4 matrix = partMatrix * t.localToWorldMatrix;

                    if (m.vertices.Length < excessiveVerts)
                        for (int j = 0; j < m.vertices.Length; j++)
                        {
                            newBounds.Encapsulate(matrix.MultiplyPoint(m.vertices[j]));
                        }
                    else
                    {
                        newBounds.SetMinMax(matrix.MultiplyPoint(m.bounds.min), matrix.MultiplyPoint(m.bounds.max));
                    }

                    bounds[i] = newBounds;
                }
                return bounds;
            }

            #region RealChuteLite
            /// <summary>
            /// Returns the total mass of the part
            /// </summary>
            public static float TotalMass(this Part part)
            {
                return part.physicalSignificance != Part.PhysicalSignificance.NONE ? part.mass + part.GetResourceMass() : 0;
            }

            /// <summary>
            /// Initiates an animation for later use
            /// </summary>
            /// <param name="animationName">Name of the animation</param>
            public static void InitiateAnimation(this Part part, string animationName)
            {
                foreach (Animation animation in part.FindModelAnimators(animationName))
                {
                    AnimationState state = animation[animationName];
                    state.normalizedTime = 0;
                    state.normalizedSpeed = 0;
                    state.enabled = false;
                    state.wrapMode = WrapMode.Clamp;
                    state.layer = 1;
                }
            }

            /// <summary>
            /// Plays an animation at a given speed
            /// </summary>
            /// <param name="animationName">Name of the animation</param>
            /// <param name="animationSpeed">Speed to play the animation at</param>
            public static void PlayAnimation(this Part part, string animationName, float animationSpeed)
            {
                foreach (Animation animation in part.FindModelAnimators(animationName))
                {
                    AnimationState state = animation[animationName];
                    state.normalizedTime = 0;
                    state.normalizedSpeed = animationSpeed;
                    state.enabled = true;
                    animation.Play(animationName);
                }
            }

            /// <summary>
            /// Skips directly to the given time of the animation
            /// </summary>
            /// <param name="animationName">Name of the animation to skip to</param>
            /// <param name="animationSpeed">Speed of the animation after the skip</param>
            /// <param name="animationTime">Normalized time skip</param>
            public static void SkipToAnimationTime(this Part part, string animationName, float animationSpeed, float animationTime)
            {
                foreach (Animation animation in part.FindModelAnimators(animationName))
                {
                    AnimationState state = animation[animationName];
                    state.normalizedTime = animationTime;
                    state.normalizedSpeed = animationSpeed;
                    state.enabled = true;
                    animation.Play(animationName);
                }
            }
            #endregion
        }
    }
}
