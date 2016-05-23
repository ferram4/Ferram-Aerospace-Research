/*
Ferram Aerospace Research v0.15.6.5 "Knudsen"
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
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry
{
    public static class PartGeometryExtensions
    {
        public static Bounds GetPartOverallLocalMeshBound(this Part part)
        {
            return GetPartOverallMeshBoundsInBasis(part, part.partTransform.worldToLocalMatrix);
        }
        
        public static Bounds GetPartOverallMeshBoundsInBasis(this Part part, Matrix4x4 worldToBasisMatrix)
        {
            Transform[] transforms = part.FindModelComponents<Transform>();

            Vector3 lower = Vector3.one * float.PositiveInfinity;
            Vector3 upper = Vector3.one * float.NegativeInfinity;

            int ignoreLayers = ignoreLayers = LayerMask.NameToLayer("TransparentFX");


            for (int i = transforms.Length - 1; i >= 0; --i)
            {
                Transform t = transforms[i];

                Matrix4x4 matrix = worldToBasisMatrix * t.localToWorldMatrix;

                MeshCollider mc = t.GetComponent<MeshCollider>();
                Mesh m = null;
                if (mc != null)
                {
                    m = mc.sharedMesh;
                    if(m != null)
                        EncapsulateBounds(ref lower, ref upper, matrix, m);
                }


                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (mf != null)
                {
                    m = mf.sharedMesh;
                    MeshRenderer mr = t.GetComponent<MeshRenderer>();
                    if ((t.gameObject.layer == ignoreLayers))
                        m = null;
                }
                else
                {
                    SkinnedMeshRenderer smr = t.GetComponent<SkinnedMeshRenderer>();
                    if (smr != null)
                    {
                        m = new Mesh();
                        smr.BakeMesh(m);
                    }
                }

                if (m == null)
                    continue;

                EncapsulateBounds(ref lower, ref upper, matrix, m);

            }
            Bounds bounds = new Bounds((lower + upper) * 0.5f, upper - lower);
            return bounds;
        }

        static void TransformedPointBounds(Matrix4x4 matrix, Vector3 center, float extX, float extY, float extZ, ref Vector3 lower, ref Vector3 upper)
        {
            Vector3 boundPt = new Vector3 (center.x + extX, center.y + extY, center.z + extZ);
            boundPt = matrix.MultiplyPoint3x4(boundPt);
            lower = Vector3.Min (lower, boundPt);
            upper = Vector3.Max (upper, boundPt);
        }

        private static void EncapsulateBounds(ref Vector3 lower, ref Vector3 upper, Matrix4x4 matrix, Mesh mesh)
        {
            Vector3 center, extents;
            center = mesh.bounds.center;//matrix.MultiplyPoint3x4(m.bounds.center);
            extents = mesh.bounds.extents;//matrix.MultiplyVector(m.bounds.size);

            TransformedPointBounds(matrix, center, +extents.x, +extents.y, +extents.z, ref lower, ref upper);
            TransformedPointBounds(matrix, center, +extents.x, +extents.y, -extents.z, ref lower, ref upper);
            TransformedPointBounds(matrix, center, +extents.x, -extents.y, -extents.z, ref lower, ref upper);
            TransformedPointBounds(matrix, center, +extents.x, -extents.y, +extents.z, ref lower, ref upper);
            TransformedPointBounds(matrix, center, -extents.x, -extents.y, +extents.z, ref lower, ref upper);
            TransformedPointBounds(matrix, center, -extents.x, -extents.y, -extents.z, ref lower, ref upper);
            TransformedPointBounds(matrix, center, -extents.x, +extents.y, -extents.z, ref lower, ref upper);
            TransformedPointBounds(matrix, center, -extents.x, +extents.y, +extents.z, ref lower, ref upper);
        }

        public static Bounds GetPartColliderBoundsInBasis(this Part part, Matrix4x4 worldToBasisMatrix, int excessiveVerts = 2500)
        {
            Transform[] transforms = part.FindModelComponents<Transform>();
            Bounds bounds = new Bounds();
            for (int i = transforms.Length - 1; i >= 0; --i)
            {
                Transform t = transforms[i];

                MeshCollider mc = t.GetComponent<MeshCollider>();
                Mesh m;
                Matrix4x4 matrix = worldToBasisMatrix * t.localToWorldMatrix;

                if (mc == null)
                {
                    BoxCollider bc = t.GetComponent<BoxCollider>();
                    if (bc != null)
                    {
                        bounds.Encapsulate(matrix.MultiplyPoint3x4(bc.bounds.min));
                        bounds.Encapsulate(matrix.MultiplyPoint3x4(bc.bounds.max));
                    }
                    continue;
                }
                else
                    m = mc.sharedMesh;

                if (m == null)
                    continue;

                bounds.Encapsulate(matrix.MultiplyPoint3x4(m.bounds.min));
                bounds.Encapsulate(matrix.MultiplyPoint3x4(m.bounds.max));
                
            }
            return bounds;
        }

        public static List<Transform> PartModelTransformList(this Part p)
        {
            List<Transform> returnList = new List<Transform>();

            List<Transform> propellersToIgnore = IgnoreModelTransformList(p);

            returnList.AddRange(p.FindModelComponents<Transform>());

            foreach (Transform t in propellersToIgnore)
                returnList.Remove(t);

            return returnList;
        }

        private static List<Transform> IgnoreModelTransformList(this Part p)
        {
            PartModule module;
            string transformString;
            List<Transform> Transform = new List<Transform>();

            if (p.Modules.Contains("FSplanePropellerSpinner"))
            {
                module = p.Modules["FSplanePropellerSpinner"];
                transformString = (string)module.GetType().GetField("propellerName").GetValue(module);
                if (transformString != "")
                {
                    Transform.AddRange(p.FindModelComponents<Transform>(transformString));
                }
                transformString = (string)module.GetType().GetField("rotorDiscName").GetValue(module);
                if (transformString != "")
                {
                    Transform.AddRange(p.FindModelComponents<Transform>(transformString));
                }

                transformString = (string)module.GetType().GetField("blade1").GetValue(module);
                if (transformString != "")
                {
                    Transform.AddRange(p.FindModelComponents<Transform>(transformString));
                }

                transformString = (string)module.GetType().GetField("blade2").GetValue(module);
                if (transformString != "")
                {
                    Transform.AddRange(p.FindModelComponents<Transform>(transformString));
                }

                transformString = (string)module.GetType().GetField("blade3").GetValue(module);
                if (transformString != "")
                {
                    Transform.AddRange(p.FindModelComponents<Transform>(transformString));
                }

                transformString = (string)module.GetType().GetField("blade4").GetValue(module);
                if (transformString != "")
                {
                    Transform.AddRange(p.FindModelComponents<Transform>(transformString));
                }
                transformString = (string)module.GetType().GetField("blade5").GetValue(module);
                if (transformString != "")
                {
                    Transform.AddRange(p.FindModelComponents<Transform>(transformString));
                }
            }
            if (p.Modules.Contains("FScopterThrottle"))
            {
                module = p.Modules["FScopterThrottle"];
                transformString = (string)module.GetType().GetField("rotorparent").GetValue(module);
                if (transformString != "")
                {
                    Transform.AddRange(p.FindModelComponents<Transform>(transformString));
                }
            }
            if (p.Modules.Contains<ModuleParachute>())
            {
                module = p.Modules.GetModule<ModuleParachute>();
                transformString = (string)module.GetType().GetField("canopyName").GetValue(module);
                if (transformString != "")
                {
                    Transform.AddRange(p.FindModelComponents<Transform>(transformString));
                }
            }
            if(p.Modules.Contains("USI_ModuleWarpEngine"))      //come up with more proper solution
            {
                Transform.AddRange(p.FindModelComponents<Transform>("Torus_001"));
                Transform.AddRange(p.FindModelComponents<Transform>("EditorWarpBubble"));
            }
            foreach (Transform t in p.FindModelComponents<Transform>())
            {
                if (Transform.Contains(t))
                    continue;
                if (!t.gameObject.activeInHierarchy)
                {
                    Transform.Add(t);
                    continue;
                }

                string tag = t.tag.ToLowerInvariant();
                if (tag == "ladder" || tag == "airlock")
                    Transform.Add(t);
            }

            return Transform;
        }
    }
}
