/*
Ferram Aerospace Research v0.14.6
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
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry
{
    public static class PartGeometryExtensions
    {
        public static Bounds GetPartOverallMeshBoundsInBasis(this Part part, Matrix4x4 worldToBasisMatrix, int excessiveVerts = 2500)
        {
            Transform[] transforms = part.FindModelComponents<Transform>();
            Bounds bounds = new Bounds();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];

                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (mf == null)
                    continue;
                Mesh m = mf.sharedMesh;

                if (m == null)
                    continue;
                Matrix4x4 matrix = worldToBasisMatrix * t.localToWorldMatrix;

                if (m.vertices.Length < excessiveVerts)
                    for (int j = 0; j < m.vertices.Length; j++)
                    {
                        bounds.Encapsulate(matrix.MultiplyPoint3x4(m.vertices[j]));
                    }
                else
                {
                    bounds.Encapsulate(matrix.MultiplyPoint3x4(m.bounds.min));
                    bounds.Encapsulate(matrix.MultiplyPoint3x4(m.bounds.max));
                }
            }
            return bounds;
        }
        
        public static Bounds[] GetPartMeshBoundsInPartSpace(this Part part, int excessiveVerts = 2500)
        {
            Transform[] transforms = part.FindModelComponents<Transform>();
            Bounds[] bounds = new Bounds[transforms.Length];
            Matrix4x4 partMatrix = part.transform.worldToLocalMatrix;
            for (int i = 0; i < transforms.Length; i++)
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
                        newBounds.Encapsulate(matrix.MultiplyPoint3x4(m.vertices[j]));
                    }
                else
                {
                    newBounds.SetMinMax(matrix.MultiplyPoint3x4(m.bounds.min), matrix.MultiplyPoint3x4(m.bounds.max));
                }

                bounds[i] = newBounds;
            }
            return bounds;
        }

        public static Bounds[] GetPartMeshBoundsListInBasis(this Part part, Transform basis, int excessiveVerts = 2500)
        {
            Transform[] transforms = part.FindModelComponents<Transform>();
            Bounds[] bounds = new Bounds[transforms.Length];
            Matrix4x4 partMatrix = basis.worldToLocalMatrix;
            for (int i = 0; i < transforms.Length; i++)
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
                        newBounds.Encapsulate(matrix.MultiplyPoint3x4(m.vertices[j]));
                    }
                else
                {
                    newBounds.SetMinMax(matrix.MultiplyPoint3x4(m.bounds.min), matrix.MultiplyPoint3x4(m.bounds.max));
                }

                bounds[i] = newBounds;
            }
            return bounds;
        }
    }
}
