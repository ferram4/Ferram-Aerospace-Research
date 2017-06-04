/*
Ferram Aerospace Research v0.15.8.1 "Lewis"
=========================
Aerodynamics model for Kerbal Space Program

Copyright 2017, Michael Ferrara, aka Ferram4

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
using FerramAerospaceResearch.FARThreading;

namespace FerramAerospaceResearch.FARPartGeometry
{
    public class GeometryMesh
    {
        public Vector3[] vertices;
        private Vector3[] meshLocalVerts;
        public int[] triangles;
        public Transform meshTransform;
        public Matrix4x4 thisToVesselMatrix;
        public Matrix4x4 meshLocalToWorld;
        public Bounds bounds;
        public Part part;
        private GeometryPartModule module;
        public bool valid;

        public int invertXYZ;

        /// <summary> cache activity state of gameobject locally as we cannot access it from other threads </summary>
        public bool gameObjectActiveInHierarchy;

        public GeometryMesh(MeshData meshData, Transform meshTransform, Matrix4x4 worldToVesselMatrix, GeometryPartModule module)
        {
            this.meshLocalVerts = meshData.vertices;
            this.triangles = meshData.triangles;
            Bounds meshBounds = meshData.bounds;

            vertices = new Vector3[meshLocalVerts.Length];
            this.thisToVesselMatrix = worldToVesselMatrix * meshTransform.localToWorldMatrix;

            for (int i = 0; i < vertices.Length; i++)
            {
                //vertices[i] = thisToVesselMatrix.MultiplyPoint3x4(untransformedVerts[i]);
                Vector3 v = meshLocalVerts[i];
                Vector3 vert = Vector3.zero;
                vert.x = thisToVesselMatrix.m00 * v.x + thisToVesselMatrix.m01 * v.y + thisToVesselMatrix.m02 * v.z + thisToVesselMatrix.m03;
                vert.y = thisToVesselMatrix.m10 * v.x + thisToVesselMatrix.m11 * v.y + thisToVesselMatrix.m12 * v.z + thisToVesselMatrix.m13;
                vert.z = thisToVesselMatrix.m20 * v.x + thisToVesselMatrix.m21 * v.y + thisToVesselMatrix.m22 * v.z + thisToVesselMatrix.m23;

                float tmpTestVert = vert.x + vert.y + vert.z;
                if (float.IsNaN(tmpTestVert) || float.IsInfinity(tmpTestVert))
                    ThreadSafeDebugLogger.Instance.RegisterMessage("Mesh error in " + module.part.partInfo.title);
                vertices[i] = vert;
            }

            this.meshTransform = meshTransform;
            this.gameObjectActiveInHierarchy = meshTransform.gameObject.activeInHierarchy;

            bounds = TransformBounds(meshBounds, thisToVesselMatrix);

            float tmpTestBounds = bounds.center.x + bounds.center.y + bounds.center.z +
                bounds.extents.x + bounds.extents.y + bounds.extents.z;
            if (float.IsNaN(tmpTestBounds) || float.IsInfinity(tmpTestBounds))
            {
                ThreadSafeDebugLogger.Instance.RegisterMessage("Bounds error in " + module.part.partInfo.title);
                valid = false;
            }
            else
                valid = true;

            this.module = module;
            this.part = module.part;
            
            if (!module.part.isMirrored)
                invertXYZ = 1;
            else
                invertXYZ = -1;
        }

        public bool TrySetThisToVesselMatrixForTransform()
        {
            if (meshTransform == null)
                return false;
            lock(this)
                meshLocalToWorld = meshTransform.localToWorldMatrix;
            return true;
        }

        public void TransformBasis(Matrix4x4 newThisToVesselMatrix)
        {
            try
            {
                Matrix4x4 tempMatrix = newThisToVesselMatrix * meshLocalToWorld;
                //Matrix4x4 tempMatrix = thisToVesselMatrix.inverse;
                //thisToVesselMatrix = newThisToVesselMatrix * meshLocalToWorld;

                //tempMatrix = thisToVesselMatrix * tempMatrix;

                //bounds = TransformBounds(bounds, tempMatrix);

                Vector3 low, high;
                low = Vector3.one * float.PositiveInfinity;
                high = Vector3.one * float.NegativeInfinity;

                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 vert = tempMatrix.MultiplyPoint3x4(meshLocalVerts[i]);// = Vector3.zero;

                    float tmpTestVert = vert.x + vert.y + vert.z;
                    if (float.IsNaN(tmpTestVert) || float.IsInfinity(tmpTestVert))
                    {
                        ThreadSafeDebugLogger.Instance.RegisterMessage("Transform error in " + module.part.partInfo.title);
                        valid = false;
                    }
                    else
                        valid = true;

                    vertices[i] = vert;
                    low = Vector3.Min(low, vert);
                    high = Vector3.Max(high, vert);
                }

                bounds = new Bounds(0.5f * (high + low), high - low);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                module.DecrementMeshesToUpdate();
            }
        }

        public void MultithreadTransformBasis(object newThisToVesselMatrixObj)
        {
            lock (this)
            {
                this.TransformBasis((Matrix4x4)newThisToVesselMatrixObj);
            }
        }

        private Bounds TransformBounds(Bounds oldBounds, Matrix4x4 matrix)
        {
            Vector3 center, extents;
            center = oldBounds.center;//matrix.MultiplyPoint3x4(m.bounds.center);
            extents = oldBounds.extents;//matrix.MultiplyVector(m.bounds.size);

            Vector3 lower = Vector3.one * float.PositiveInfinity;
            Vector3 upper = Vector3.one * float.NegativeInfinity;

            TransformedPointBounds(matrix, center, +extents.x, +extents.y, +extents.z, ref lower, ref upper);
            TransformedPointBounds(matrix, center, +extents.x, +extents.y, -extents.z, ref lower, ref upper);
            TransformedPointBounds(matrix, center, +extents.x, -extents.y, -extents.z, ref lower, ref upper);
            TransformedPointBounds(matrix, center, +extents.x, -extents.y, +extents.z, ref lower, ref upper);
            TransformedPointBounds(matrix, center, -extents.x, -extents.y, +extents.z, ref lower, ref upper);
            TransformedPointBounds(matrix, center, -extents.x, -extents.y, -extents.z, ref lower, ref upper);
            TransformedPointBounds(matrix, center, -extents.x, +extents.y, -extents.z, ref lower, ref upper);
            TransformedPointBounds(matrix, center, -extents.x, +extents.y, +extents.z, ref lower, ref upper);

            Bounds bounds = new Bounds((lower + upper) * 0.5f, upper - lower);
            //FARThreading.ThreadSafeDebugLogger.Instance.RegisterMessage("Bounds center: " + bounds.center + " extents: " + bounds.extents);
            return bounds;
        }

        void TransformedPointBounds(Matrix4x4 matrix, Vector3 center, float extX, float extY, float extZ, ref Vector3 lower, ref Vector3 upper)
        {
            Vector3 boundPt = new Vector3(center.x + extX, center.y + extY, center.z + extZ);
            boundPt = matrix.MultiplyPoint3x4(boundPt);
            lower = Vector3.Min(lower, boundPt);
            upper = Vector3.Max(upper, boundPt);
        }
    }
}
