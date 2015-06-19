/*
Ferram Aerospace Research v0.15.3 "Froude"
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
    public class GeometryMesh
    {
        public Vector3[] vertices;
        public int[] triangles;
        public Transform meshTransform;
        public Matrix4x4 thisToVesselMatrix;
        public Matrix4x4 meshLocalToWorld;
        public Bounds bounds;
        private GeometryPartModule module;

        public int invertXYZ;

        public GeometryMesh(MeshData meshData, Transform meshTransform, Matrix4x4 worldToVesselMatrix, GeometryPartModule module)
        {
            Vector3[] untransformedVerts = meshData.vertices;
            int[] triangles = meshData.triangles;
            Bounds meshBounds = meshData.bounds;

            vertices = new Vector3[untransformedVerts.Length];
            this.thisToVesselMatrix = worldToVesselMatrix * meshTransform.localToWorldMatrix;

            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = thisToVesselMatrix.MultiplyPoint3x4(untransformedVerts[i]);

            this.triangles = triangles;
            this.meshTransform = meshTransform;

            Vector3 size = thisToVesselMatrix.MultiplyVector(meshBounds.size);
            size.x = Math.Abs(size.x);
            size.y = Math.Abs(size.y);
            size.z = Math.Abs(size.z);

            bounds = TransformBounds(meshBounds, thisToVesselMatrix);

            this.module = module;
            
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

            Matrix4x4 tempMatrix = thisToVesselMatrix.inverse;
            thisToVesselMatrix = newThisToVesselMatrix * meshLocalToWorld;

            tempMatrix = thisToVesselMatrix *  tempMatrix;

            bounds = TransformBounds(bounds, tempMatrix);

            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = tempMatrix.MultiplyPoint3x4(vertices[i]);
        }

        public void MultithreadTransformBasis(object newThisToVesselMatrixObj)
        {
            lock (this)
            {
                try
                {
                    Matrix4x4 tempMatrix = thisToVesselMatrix.inverse;

                    thisToVesselMatrix = (Matrix4x4)newThisToVesselMatrixObj * meshLocalToWorld;

                    tempMatrix = thisToVesselMatrix * tempMatrix;

                    bounds = TransformBounds(bounds, tempMatrix);

                    for (int i = 0; i < vertices.Length; i++)
                        vertices[i] = tempMatrix.MultiplyPoint3x4(vertices[i]);

                    module.DecrementMeshesToUpdate();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        private Bounds TransformBounds(Bounds oldBounds, Matrix4x4 matrix)
        {
            Bounds bounds = new Bounds();
            Vector3 center, extents;
            center = oldBounds.center;//matrix.MultiplyPoint3x4(m.bounds.center);
            extents = oldBounds.extents;//matrix.MultiplyVector(m.bounds.size);

            /*size.x = Math.Abs(size.x);
            size.y = Math.Abs(size.y);
            size.z = Math.Abs(size.z);*/

            Vector3 boundPt;
            boundPt = center + extents;
            boundPt = matrix.MultiplyPoint3x4(boundPt);
            bounds.Encapsulate(boundPt);

            boundPt = center - extents;
            boundPt = matrix.MultiplyPoint3x4(boundPt);
            bounds.Encapsulate(boundPt);

            boundPt = center;
            boundPt.x += extents.x;
            boundPt.y += extents.y;
            boundPt.z -= extents.z;
            boundPt = matrix.MultiplyPoint3x4(boundPt);
            bounds.Encapsulate(boundPt);

            boundPt = center;
            boundPt.x += extents.x;
            boundPt.y -= extents.y;
            boundPt.z += extents.z;
            boundPt = matrix.MultiplyPoint3x4(boundPt);
            bounds.Encapsulate(boundPt);

            boundPt = center;
            boundPt.x -= extents.x;
            boundPt.y += extents.y;
            boundPt.z += extents.z;
            boundPt = matrix.MultiplyPoint3x4(boundPt);
            bounds.Encapsulate(boundPt);

            boundPt = center;
            boundPt.x -= extents.x;
            boundPt.y -= extents.y;
            boundPt.z += extents.z;
            boundPt = matrix.MultiplyPoint3x4(boundPt);
            bounds.Encapsulate(boundPt);

            boundPt = center;
            boundPt.x -= extents.x;
            boundPt.y += extents.y;
            boundPt.z -= extents.z;
            boundPt = matrix.MultiplyPoint3x4(boundPt);
            bounds.Encapsulate(boundPt);

            boundPt = center;
            boundPt.x += extents.x;
            boundPt.y -= extents.y;
            boundPt.z -= extents.z;
            boundPt = matrix.MultiplyPoint3x4(boundPt);

            bounds.Encapsulate(boundPt);

            return bounds;
        }
    }
}
