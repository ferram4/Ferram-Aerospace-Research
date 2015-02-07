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
using System.Threading;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry
{
    public class VehicleVoxel
    {
        float elementSize;
        float invElementSize;
        VoxelSection[, ,] voxelSections;
        int xLength, yLength, zLength;
        int itemsQueued = 0;
        object _locker = new object();
        Vector3 lowerRightCorner;

        public VehicleVoxel(List<Part> partList, int elementCount, bool multiThreaded)
        {
            Bounds vesselBounds = new Bounds();
            List<GeometryPartModule> geoModules = new List<GeometryPartModule>();
            for(int i = 0; i < partList.Count; i++)
            {
                Part p = partList[i];
                GeometryPartModule m = p.GetComponent<GeometryPartModule>();
                if (m != null)
                {
                    vesselBounds.Encapsulate(m.overallMeshBounds);
                    geoModules.Add(m);
                }
            }

            float voxelVolume = vesselBounds.size.x * vesselBounds.size.y * vesselBounds.size.z;
            float elementVol = voxelVolume / (float)elementCount;
            elementSize = (float)Math.Pow(elementVol, 1f / 3f);
            invElementSize = 1 / elementSize;

            float tmp = 0.125f * invElementSize;

            xLength = (int)Math.Ceiling(vesselBounds.size.x * tmp);
            yLength = (int)Math.Ceiling(vesselBounds.size.y * tmp);
            zLength = (int)Math.Ceiling(vesselBounds.size.z * tmp);

            Debug.Log(elementSize);
            Debug.Log(xLength + " " + yLength + " " + zLength);
            Debug.Log(vesselBounds);

            lowerRightCorner = vesselBounds.min;

            voxelSections = new VoxelSection[xLength, yLength, zLength];

            for(int i = 0; i < geoModules.Count; i++)
            {
                GeometryPartModule m = geoModules[i];
                for (int j = 0; j < m.geometryMeshes.Count; j++)
                {
                    if (multiThreaded)
                    {
                        WorkData data = new WorkData(m.part, m.geometryMeshes[j], m.meshToVesselMatrixList[j]);
                        ThreadPool.QueueUserWorkItem(UpdateFromMesh, data);
                    }
                    else
                        UpdateFromMesh(m.geometryMeshes[j], m.part, m.meshToVesselMatrixList[j]);
                    itemsQueued++;
                }
            }
            System.Threading.ThreadPriority currentPrio = Thread.CurrentThread.Priority;
            Thread.CurrentThread.Priority = System.Threading.ThreadPriority.BelowNormal;
            lock (_locker)
                while (itemsQueued > 0)
                    Monitor.Wait(_locker);
                
            Thread.CurrentThread.Priority = currentPrio;
            try
            {

                SolidifyVoxel();

            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

        }

        ~VehicleVoxel()
        {
            ClearVisualVoxels();
        }

        private void SetVoxelSection(int i, int j, int k, Part part)
        {
            int iSec, jSec, kSec;
            //Find the voxel section that this point points to
            iSec = i / 8;
            jSec = j / 8;
            kSec = k / 8;
            VoxelSection section;

            lock (voxelSections)
            {
                section = voxelSections[iSec, jSec, kSec];
                if (section == null)
                {
                    section = new VoxelSection(elementSize, lowerRightCorner + new Vector3(iSec, jSec, kSec) * elementSize * 8);
                    voxelSections[iSec, jSec, kSec] = section;
                }
            }

            //Debug.Log(i.ToString() + ", " + j.ToString() + ", " + k.ToString() + ", " + part.partInfo.title);

            section.SetVoxelPoint(i % 8, j % 8, k % 8, part);
        }

        private VoxelSection GetVoxelSection(int i, int j, int k)
        {
            int iSec, jSec, kSec;
            //Find the voxel section that this point points to
            iSec = i / 8;
            jSec = j / 8;
            kSec = k / 8;

            VoxelSection section;
            lock (voxelSections)
            {
                section = voxelSections[iSec, jSec, kSec];
            }
            return section;
        }

        private Part GetPartAtVoxelPos(int i, int j, int k)
        {
            int iSec, jSec, kSec;
            //Find the voxel section that this point points to
            iSec = i / 8;
            jSec = j / 8;
            kSec = k / 8;

            VoxelSection section;
            lock (voxelSections)
            {
                section = voxelSections[iSec, jSec, kSec];
            }
            if (section == null)
                return null;

            return section.GetVoxelPoint(i % 8, j % 8, k % 8);
        }

        private Part GetPartAtVoxelPos(int i, int j, int k, ref VoxelSection section)
        {
            return section.GetVoxelPoint(i % 8, j % 8, k % 8);
        }
        private void UpdateFromMesh(object stuff)
        {
            try
            {
                WorkData data = (WorkData)stuff;
                Part part = data.part;
                Mesh mesh = data.mesh;
                Matrix4x4 transform = data.transform;
                UpdateFromMesh(mesh, part, transform);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        private void UpdateFromMesh(Mesh mesh, Part part, Matrix4x4 transform)
        {
            Vector3[] vertsVoxelSpace = new Vector3[mesh.vertices.Length];
            Bounds meshBounds = mesh.bounds;//new Bounds();
            for (int i = 0; i < vertsVoxelSpace.Length; i++)
            {
                Vector3 vert = transform.MultiplyPoint3x4(mesh.vertices[i]);
                //meshBounds.Encapsulate(vert);
                vertsVoxelSpace[i] = vert;
            }

            if (meshBounds.size.x < elementSize && meshBounds.size.y < elementSize && meshBounds.size.z < elementSize)
            {
                CalculateVoxelShellFromTinyMesh(ref meshBounds, ref part);
                lock (_locker)
                {
                    itemsQueued--;
                    Monitor.Pulse(_locker);
                } 
                return;
            }

            float rc = 0.5f;

            for (int a = 0; a < mesh.triangles.Length; a += 3)
            {
                Vector3 vert1, vert2, vert3;

                vert1 = vertsVoxelSpace[mesh.triangles[a]];
                vert2 = vertsVoxelSpace[mesh.triangles[a + 1]];
                vert3 = vertsVoxelSpace[mesh.triangles[a + 2]];


                CalculateVoxelShellForTriangle(ref vert1, ref vert2, ref vert3, ref rc, ref part);
            }

            lock (_locker)
            {
                itemsQueued--;
                Monitor.Pulse(_locker);
            }
        }

        private void CalculateVoxelShellFromTinyMesh(ref Bounds meshBounds, ref Part part)
        {
            int lowerI, lowerJ, lowerK;
            int upperI, upperJ, upperK;

            Vector3 min, max;
            min = (meshBounds.min - lowerRightCorner) * invElementSize;
            max = (meshBounds.max - lowerRightCorner) * invElementSize;

            lowerI = (int)Math.Floor(min.x);
            lowerJ = (int)Math.Floor(min.y);
            lowerK = (int)Math.Floor(min.z);

            upperI = (int)Math.Ceiling(max.x);
            upperJ = (int)Math.Ceiling(max.y);
            upperK = (int)Math.Ceiling(max.z);

            lowerI = Math.Max(lowerI, 0);
            lowerJ = Math.Max(lowerJ, 0);
            lowerK = Math.Max(lowerK, 0);

            upperI = Math.Min(upperI, xLength * 8 - 1);
            upperJ = Math.Min(upperJ, yLength * 8 - 1);
            upperK = Math.Min(upperK, zLength * 8 - 1);

            for (int i = lowerI; i <= upperI; i++)
                for (int j = lowerJ; j <= upperJ; j++)
                    for (int k = lowerK; k <= upperK; k++)
                    {
                        SetVoxelSection(i, j, k, part);
                    }
        }

        private void CalculateVoxelShellForTriangle(ref Vector3 vert1, ref Vector3 vert2, ref Vector3 vert3, ref float rc, ref Part part)
        {
            Vector4 plane = CalculateEquationOfPlane(ref vert1, ref vert2, ref vert3);

            float x, y, z;
            x = Math.Abs(plane.x);
            y = Math.Abs(plane.y);
            z = Math.Abs(plane.z);

            Vector4 indexPlane = TransformPlaneToIndices(plane);

            if (x > y && x > z)
                VoxelShellTrianglePerpX(ref indexPlane, ref vert1, ref vert2, ref vert3, ref rc, ref part);
            else if(y > x && y > z)
                VoxelShellTrianglePerpY(ref indexPlane, ref vert1, ref vert2, ref vert3, ref rc, ref part);
            else
                VoxelShellTrianglePerpZ(ref indexPlane, ref vert1, ref vert2, ref vert3, ref rc, ref part);
        }

        private void VoxelShellTrianglePerpX(ref Vector4 indexPlane, ref Vector3 vert1, ref Vector3 vert2, ref Vector3 vert3, ref float rc, ref Part part)
        {
            Vector2 vert1Proj, vert2Proj, vert3Proj;
            vert1Proj = new Vector2(vert1.y - lowerRightCorner.y, vert1.z - lowerRightCorner.z) * invElementSize;
            vert2Proj = new Vector2(vert2.y - lowerRightCorner.y, vert2.z - lowerRightCorner.z) * invElementSize;
            vert3Proj = new Vector2(vert3.y - lowerRightCorner.y, vert3.z - lowerRightCorner.z) * invElementSize;

            Vector2 p1p2, p1p3;
            p1p2 = vert2Proj - vert1Proj;
            p1p3 = vert3Proj - vert1Proj;

            float dot12_12, dot12_13, dot13_13;
            dot12_12 = Vector2.Dot(p1p2, p1p2);
            dot12_13 = Vector2.Dot(p1p2, p1p3);
            dot13_13 = Vector2.Dot(p1p3, p1p3);

            float invDenom = 1 / (dot12_12 * dot13_13 - dot12_13 * dot12_13);

            int lowJ, highJ, lowK, highK;
            lowJ = (int)Math.Min(vert1Proj.x, Math.Min(vert2Proj.x, vert3Proj.x));
            highJ = (int)Math.Ceiling(Math.Max(vert1Proj.x, Math.Max(vert2Proj.x, vert3Proj.x)));
            lowK = (int)Math.Min(vert1Proj.y, Math.Min(vert2Proj.y, vert3Proj.y));
            highK = (int)Math.Ceiling(Math.Max(vert1Proj.y, Math.Max(vert2Proj.y, vert3Proj.y)));


            lowJ = Math.Max(lowJ, 0);
            lowK = Math.Max(lowK, 0);
            highJ = Math.Min(highJ, yLength * 8 - 1);
            highK = Math.Min(highK, zLength * 8 - 1);

            float invPlaneX = 1 / indexPlane.x;

            for (int j = lowJ; j <= highJ; j++)
                for (int k = lowK; k <= highK; k++)
                {
                    Vector2 pt = new Vector2(j, k);
                    Vector2 p1TestPt = pt - vert1Proj;
                    float dot12_test, dot13_test;
                    dot12_test = Vector2.Dot(p1p2, p1TestPt);
                    dot13_test = Vector2.Dot(p1p3, p1TestPt);

                    float u, v;
                    u = (dot13_13 * dot12_test - dot12_13 * dot13_test) * invDenom;
                    v = (dot12_12 * dot13_test - dot12_13 * dot12_test) * invDenom;

                    if (u >= 0 && v >= 0 && u + v <= 1)
                    {
                        int i = (int)Math.Round(-(indexPlane.y * j + indexPlane.z * k + indexPlane.w) * invPlaneX);
                        if (i < 0 || i >= xLength * 8)
                            continue;

                        SetVoxelSection(i, j, k, part);
                        continue;
                    }
                    Vector2 p2TestPt = pt - vert2Proj;
                    Vector2 p3TestPt = pt - vert3Proj;
                    if (p1TestPt.magnitude < rc || p2TestPt.magnitude < rc || p3TestPt.magnitude < rc)
                    {
                        int i = (int)Math.Round(-(indexPlane.y * j + indexPlane.z * k + indexPlane.w) * invPlaneX);
                        if (i < 0 || i >= xLength * 8)
                            continue;

                        SetVoxelSection(i, j, k, part);
                        continue;
                    }

                    if ((u >= 0 && u <= 1 && DistancePerp(p1p2, p1TestPt) < rc) ||
                        (v >= 0 && v <= 1 && DistancePerp(p1p3, p1TestPt) < rc) ||
                        (u >= 0 && v >= 0 && DistancePerp(vert3Proj - vert2Proj, p2TestPt) < rc))
                    {
                        int i = (int)Math.Round(-(indexPlane.y * j + indexPlane.z * k + indexPlane.w) * invPlaneX);
                        if (i < 0 || i >= xLength * 8)
                            continue;

                        SetVoxelSection(i, j, k, part);

                    }
                }
        }

        private void VoxelShellTrianglePerpY(ref Vector4 indexPlane, ref Vector3 vert1, ref Vector3 vert2, ref Vector3 vert3, ref float rc, ref Part part)
        {
            Vector2 vert1Proj, vert2Proj, vert3Proj;
            vert1Proj = new Vector2(vert1.x - lowerRightCorner.x, vert1.z - lowerRightCorner.z) * invElementSize;
            vert2Proj = new Vector2(vert2.x - lowerRightCorner.x, vert2.z - lowerRightCorner.z) * invElementSize;
            vert3Proj = new Vector2(vert3.x - lowerRightCorner.x, vert3.z - lowerRightCorner.z) * invElementSize;

            
            Vector2 p1p2, p1p3;
            p1p2 = vert2Proj - vert1Proj;
            p1p3 = vert3Proj - vert1Proj;

            float dot12_12, dot12_13, dot13_13;
            dot12_12 = Vector2.Dot(p1p2, p1p2);
            dot12_13 = Vector2.Dot(p1p2, p1p3);
            dot13_13 = Vector2.Dot(p1p3, p1p3);

            float invDenom = 1 / (dot12_12 * dot13_13 - dot12_13 * dot12_13);

            int lowI, highI, lowK, highK;
            lowI = (int)Math.Min(vert1Proj.x, Math.Min(vert2Proj.x, vert3Proj.x));
            highI = (int)Math.Ceiling(Math.Max(vert1Proj.x, Math.Max(vert2Proj.x, vert3Proj.x)));
            lowK = (int)Math.Min(vert1Proj.y, Math.Min(vert2Proj.y, vert3Proj.y));
            highK = (int)Math.Ceiling(Math.Max(vert1Proj.y, Math.Max(vert2Proj.y, vert3Proj.y)));


            lowI = Math.Max(lowI, 0);
            lowK = Math.Max(lowK, 0);
            highI = Math.Min(highI, xLength * 8 - 1);
            highK = Math.Min(highK, zLength * 8 - 1);

            float invPlaneY = 1 / indexPlane.y;
            
            for (int i = lowI; i <= highI; i++)
                for (int k = lowK; k <= highK; k++)
                {
                    Vector2 pt = new Vector2(i, k);
                    Vector2 p1TestPt = pt - vert1Proj;
                    float dot12_test, dot13_test;
                    dot12_test = Vector2.Dot(p1p2, p1TestPt);
                    dot13_test = Vector2.Dot(p1p3, p1TestPt);

                    float u, v;
                    u = (dot13_13 * dot12_test - dot12_13 * dot13_test) * invDenom;
                    v = (dot12_12 * dot13_test - dot12_13 * dot12_test) * invDenom;

                    if (u >= 0 && v >= 0 && u + v <= 1)
                    {
                        int j = (int)Math.Round(-(indexPlane.x * i + indexPlane.z * k + indexPlane.w) * invPlaneY);
                        if (j < 0 || j >= yLength * 8)
                            continue;
                        SetVoxelSection(i, j, k, part);
                        continue;
                    }
                    Vector2 p2TestPt = pt - vert2Proj;
                    Vector2 p3TestPt = pt - vert3Proj;
                    if (p1TestPt.magnitude < rc || p2TestPt.magnitude < rc || p3TestPt.magnitude < rc)
                    {
                        int j = (int)Math.Round(-(indexPlane.x * i + indexPlane.z * k + indexPlane.w) * invPlaneY);
                        if (j < 0 || j >= yLength * 8)
                            continue;
                        SetVoxelSection(i, j, k, part);
                        continue;
                    }

                    if ((u >= 0 && u <= 1 && DistancePerp(p1p2, p1TestPt) < rc) ||
                        (v >= 0 && v <= 1 && DistancePerp(p1p3, p1TestPt) < rc) ||
                        (u >= 0 && v >= 0 && DistancePerp(vert3Proj - vert2Proj, p2TestPt) < rc))
                    {
                        int j = (int)Math.Round(-(indexPlane.x * i + indexPlane.z * k + indexPlane.w) * invPlaneY);
                        if (j < 0 || j >= yLength * 8)
                            continue;
                        SetVoxelSection(i, j, k, part);
                    }
                }
        }

        private void VoxelShellTrianglePerpZ(ref Vector4 indexPlane, ref Vector3 vert1, ref Vector3 vert2, ref Vector3 vert3, ref float rc, ref Part part)
        {
            Vector2 vert1Proj, vert2Proj, vert3Proj;
            vert1Proj = new Vector2(vert1.x - lowerRightCorner.x, vert1.y - lowerRightCorner.y) * invElementSize;
            vert2Proj = new Vector2(vert2.x - lowerRightCorner.x, vert2.y - lowerRightCorner.y) * invElementSize;
            vert3Proj = new Vector2(vert3.x - lowerRightCorner.x, vert3.y - lowerRightCorner.y) * invElementSize;

            Vector2 p1p2, p1p3;
            p1p2 = vert2Proj - vert1Proj;
            p1p3 = vert3Proj - vert1Proj;

            float dot12_12, dot12_13, dot13_13;
            dot12_12 = Vector2.Dot(p1p2, p1p2);
            dot12_13 = Vector2.Dot(p1p2, p1p3);
            dot13_13 = Vector2.Dot(p1p3, p1p3);

            float invDenom = 1 / (dot12_12 * dot13_13 - dot12_13 * dot12_13);

            int lowI, highI, lowJ, highJ;
            lowI = (int)Math.Min(vert1Proj.x, Math.Min(vert2Proj.x, vert3Proj.x));
            highI = (int)Math.Ceiling(Math.Max(vert1Proj.x, Math.Max(vert2Proj.x, vert3Proj.x)));
            lowJ = (int)Math.Min(vert1Proj.y, Math.Min(vert2Proj.y, vert3Proj.y));
            highJ = (int)Math.Ceiling(Math.Max(vert1Proj.y, Math.Max(vert2Proj.y, vert3Proj.y)));


            lowI = Math.Max(lowI, 0);
            lowJ = Math.Max(lowJ, 0);
            highI = Math.Min(highI, xLength * 8 - 1);
            highJ = Math.Min(highJ, yLength * 8 - 1);

            float invPlaneZ = 1 / indexPlane.z;

            for (int i = lowI; i <= highI; i++)
                for (int j = lowJ; j <= highJ; j++)
                {
                    Vector2 pt = new Vector2(i, j);
                    Vector2 p1TestPt = pt - vert1Proj;
                    float dot12_test, dot13_test;
                    dot12_test = Vector2.Dot(p1p2, p1TestPt);
                    dot13_test = Vector2.Dot(p1p3, p1TestPt);

                    float u, v;
                    u = (dot13_13 * dot12_test - dot12_13 * dot13_test) * invDenom;
                    v = (dot12_12 * dot13_test - dot12_13 * dot12_test) * invDenom;

                    if (u >= 0 && v >= 0 && u + v <= 1)
                    {
                        int k = (int)Math.Round(-(indexPlane.x * i + indexPlane.y * j + indexPlane.w) * invPlaneZ);
                        if (k < 0 || k >= zLength * 8)
                            continue;

                        SetVoxelSection(i, j, k, part);
                        continue;
                    }
                    Vector2 p2TestPt = pt - vert2Proj;
                    Vector2 p3TestPt = pt - vert3Proj;
                    if (p1TestPt.magnitude < rc || p2TestPt.magnitude < rc || p3TestPt.magnitude < rc)
                    {
                        int k = (int)Math.Round(-(indexPlane.x * i + indexPlane.y * j + indexPlane.w) * invPlaneZ);
                        if (k < 0 || k >= zLength * 8)
                            continue;

                        SetVoxelSection(i, j, k, part);
                        continue;
                    }

                    if ((u >= 0 && u <= 1 && DistancePerp(p1p2, p1TestPt) < rc) ||
                        (v >= 0 && v <= 1 && DistancePerp(p1p3, p1TestPt) < rc) ||
                        (u >= 0 && v >= 0 && DistancePerp(vert3Proj - vert2Proj, p2TestPt) < rc))
                    {
                        int k = (int)Math.Round(-(indexPlane.x * i + indexPlane.y * j + indexPlane.w) * invPlaneZ);
                        if (k < 0 || k >= zLength * 8)
                            continue;

                        SetVoxelSection(i, j, k, part);
                    }
                }
        }

        private float DistancePerp(Vector2 testVec, Vector2 perpVector)
        {
            float tmp = perpVector.x;
            perpVector.x = perpVector.y;
            perpVector.y = -tmp;
            perpVector.Normalize();

            return Math.Abs(Vector2.Dot(testVec, perpVector));
        }

        private Vector4 CalculateEquationOfPlane(ref Vector3 pt1, ref Vector3 pt2, ref Vector3 pt3)
        {
            Vector3 p1p2 = pt2 - pt1;
            Vector3 p1p3 = pt3 - pt1;

            Vector3 tmp = Vector3.Cross(p1p2, p1p3).normalized;

            Vector4 result = new Vector4(tmp.x, tmp.y, tmp.z);

            result.w = -(pt1.x * result.x + pt1.y * result.y + pt1.z * result.z);

            return result;
        }

        private Vector4 TransformPlaneToIndices(Vector4 plane)
        {
            Vector4 newPlane = new Vector4();
            newPlane.x = plane.x * elementSize;
            newPlane.y = plane.y * elementSize;
            newPlane.z = plane.z * elementSize;
            newPlane.w = plane.w + plane.x * lowerRightCorner.x + plane.y * lowerRightCorner.y + plane.z * lowerRightCorner.z;

            return newPlane;
        }

        public float[] CrossSectionalArea(Vector3 orientation)
        {
            float[] crossSections = new float[yLength * 8];
            float areaPerElement = elementSize * elementSize;
            for (int j = 0; j < yLength * 8; j++)
            {
                float area = 0;
                for (int i = 0; i < xLength * 8; i++)
                    for (int k = 0; k < zLength * 8; k++)
                    {
                        if (GetPartAtVoxelPos(i, j, k) != null)
                            area += areaPerElement;
                    }
                Debug.Log(area);
                crossSections[j] = area;
            }
            return crossSections;
        }

        public void ClearVisualVoxels()
        {
            for (int i = 0; i < xLength; i++)
                for (int j = 0; j < yLength; j++)
                    for (int k = 0; k < zLength; k++)
                    {
                        VoxelSection section = voxelSections[i, j, k];
                        if (section != null)
                        {
                            section.ClearVisualVoxels();
                        }
                    }
        }

        public void VisualizeVoxel(Vector3 vesselOffset)
        {
            for(int i = 0; i < xLength; i++)
                for(int j = 0; j < yLength; j++)
                    for(int k = 0; k < zLength; k++)
                    {
                        VoxelSection section = voxelSections[i, j, k];
                        if(section != null)
                        {
                            section.VisualizeVoxels(vesselOffset);
                        }
                    }
        }
        private void SolidifyVoxel()
        {
            SweepPlanePoint[,] sweepPlane = new SweepPlanePoint[xLength * 8, zLength * 8];
            List<SweepPlanePoint> activePts = new List<SweepPlanePoint>();
            List<SweepPlanePoint> inactiveInteriorPts = new List<SweepPlanePoint>();
            SweepPlanePoint[] neighboringSweepPlanePts = new SweepPlanePoint[4];
            for (int j = 0; j < yLength * 8; j++) //Iterate from front of vehicle to back
            {
                for (int i = 0; i < xLength * 8; i++) //Iterate across the cross-section plane to add voxel shell and mark active interior points
                    for (int k = 0; k < zLength * 8; k++)
                    {
                        Part p = GetPartAtVoxelPos(i, j, k);
                        SweepPlanePoint pt = sweepPlane[i, k];

                        if (pt == null && p != null) //If there is a section of voxel there, but no pt, add a new voxel shell pt to the sweep plane
                        {
                            pt = new SweepPlanePoint(p, i, k);
                            sweepPlane[i, k] = pt;
                            continue;
                        }
                        else if (pt != null)
                        {
                            if (p == null) //If there is a pt there, but no part listed, this is an interior pt or a the cross-section is shrinking
                            {
                                if (pt.mark == SweepPlanePoint.MarkingType.VoxelShell) //label it as active so that it can be determined once all the points have been checked
                                {
                                    pt.mark = SweepPlanePoint.MarkingType.Active;
                                    activePts.Add(pt); //And add it to the list of active interior pts
                                }
                            }
                            else
                            { //Make sure the point is labeled as a voxel shell if there is already a part there
                                pt.mark = SweepPlanePoint.MarkingType.VoxelShell;
                                pt.part = p;
                                inactiveInteriorPts.Remove(pt);
                                activePts.Remove(pt);
                            }
                        }
                    }
                for (int i = 0; i < activePts.Count; i++) //Then, iterate through all active points for this section
                {
                    SweepPlanePoint activeInteriorPt = activePts[i]; //Get active interior pt
                    if (activeInteriorPt.i + 1 < xLength * 8) //And all of its 4-neighbors
                        neighboringSweepPlanePts[0] = sweepPlane[activeInteriorPt.i + 1, activeInteriorPt.k];
                    else
                        neighboringSweepPlanePts[0] = null;
                    if (activeInteriorPt.i - 1 >= 0)
                        neighboringSweepPlanePts[1] = sweepPlane[activeInteriorPt.i - 1, activeInteriorPt.k];
                    else
                        neighboringSweepPlanePts[1] = null;
                    if (activeInteriorPt.k + 1 < zLength * 8)
                        neighboringSweepPlanePts[2] = sweepPlane[activeInteriorPt.i, activeInteriorPt.k + 1];
                    else
                        neighboringSweepPlanePts[2] = null;
                    if (activeInteriorPt.k - 1 >= 0)
                        neighboringSweepPlanePts[3] = sweepPlane[activeInteriorPt.i, activeInteriorPt.k - 1];
                    else
                        neighboringSweepPlanePts[3] = null;
                    bool remove = false;
                    for (int m = 0; m < neighboringSweepPlanePts.Length; m++) //Check if the active point is surrounded by 4 neighbors
                        if (neighboringSweepPlanePts[m] == null) //If any of them are null, this active point is larger than the current cross-section
                        { //In that case, it should be set to be removed
                            remove = true;
                            break;
                        }
                    if (remove) //If it is set to be removed...
                    {
                        for (int m = 0; m < neighboringSweepPlanePts.Length; m++) //Go through all the neighboring points
                        {
                            SweepPlanePoint neighbor = neighboringSweepPlanePts[m];
                            if (neighbor != null && neighbor.mark == SweepPlanePoint.MarkingType.InactiveInterior) //For the ones that exist, and are inactive interior...
                            {
                                neighbor.mark = SweepPlanePoint.MarkingType.Active; //...mark them active
                                inactiveInteriorPts.Remove(neighbor); //remove them from inactiveInterior
                                activePts.Add(neighbor); //And add them to the end of activePts
                            }
                        }
                        sweepPlane[activeInteriorPt.i, activeInteriorPt.k] = null; //Then, set this point to null in the sweepPlane
                        SetVoxelSection(activeInteriorPt.i, j, activeInteriorPt.k, null); //Set the point on the voxel to null
                        activePts[i] = null; //And clear it out for this guy
                    }
                    else
                    { //If it's surrounded by other points, it's inactive; add it to that list
                        activeInteriorPt.mark = SweepPlanePoint.MarkingType.InactiveInterior;
                        inactiveInteriorPts.Add(activeInteriorPt);
                    }
                }
                //Debug.Log(activePts.Count + " " + inactiveInteriorPts.Count);
                activePts.Clear(); //Clear activePts every iteration
                for (int i = 0; i < inactiveInteriorPts.Count; i++) //Any remaining inactive interior pts are guaranteed to be on the inside of the vehicle
                {
                    SweepPlanePoint inactivePt = inactiveInteriorPts[i]; //Get each
                    SetVoxelSection(inactivePt.i, j, inactivePt.k, inactivePt.part); //And update the voxel accordingly
                }
            }
            //Cleanup
            sweepPlane = null;
            activePts = null;
            inactiveInteriorPts = null;
            neighboringSweepPlanePts = null;
        }
/*        private void SolidifyVoxelMultithread()
        {
            SweepPlanePoint[,] sweepPlane = new SweepPlanePoint[xLength * 8, zLength * 8];
            List<SweepPlanePoint> activePts = new List<SweepPlanePoint>();
            List<SweepPlanePoint> inactiveInteriorPts = new List<SweepPlanePoint>();
            Thread[] threads = new Thread[4];

            int midI, highI;
            int midK, highK;

            highI = xLength * 8;
            midI = highI / 2;

            highK = zLength * 8;
            midK = highK / 2;

            lock (_locker)
                itemsQueued = 1;  //This primes the system; it will only be set back to 0 when a solidify completes

            threads[0] = new Thread(SolidifySection);
            threads[0].Start(new SolidifyData(sweepPlane, activePts, inactiveInteriorPts, 0, midI, 0, midK, 0));
            threads[1] = new Thread(SolidifySection);
            threads[1].Start(new SolidifyData(sweepPlane, activePts, inactiveInteriorPts, 0, midI, midK, highK, 1));
            threads[2] = new Thread(SolidifySection);
            threads[2].Start(new SolidifyData(sweepPlane, activePts, inactiveInteriorPts, midI, highI, 0, midK, 2));
            threads[3] = new Thread(SolidifySection);
            threads[3].Start(new SolidifyData(sweepPlane, activePts, inactiveInteriorPts, midI, highI, midK, highK, 3));
            //SolidifySection(ref sweepPlane, 0, xLength * 8, 0, zLength * 8);

            while (itemsQueued > 0)
                Thread.Sleep(15);

            //Cleanup
            sweepPlane = null;
            activePts = null;
            inactiveInteriorPts = null;
        }
        private void SolidifySection(object data)
        {
            SolidifyData solidData = (SolidifyData)data;
            SolidifySection(ref solidData.sweepPlane, ref solidData.activePts, ref solidData.inactiveInteriorPts, solidData.lowI, solidData.highI, solidData.lowK, solidData.highK, solidData.threadInd);
        }

        private void SolidifySection(ref SweepPlanePoint[,] sweepPlane, ref List<SweepPlanePoint> activePts, ref List<SweepPlanePoint> inactiveInteriorPts, int lowI, int highI, int lowK, int highK, int threadInd)
        {
            int threadNum = threadInd + 1;
            SweepPlanePoint[] neighboringSweepPlanePts = new SweepPlanePoint[4];
            for (int j = 0; j < yLength * 8; j++)      //Iterate from front of vehicle to back
            {
                lock (_locker)
                    itemsQueued++;

                for (int i = lowI; i < highI; i++)    //Iterate across the cross-section plane to add voxel shell and mark active interior points
                    for (int k = lowK; k < highK; k++)
                    {
                        Part p = GetPartAtVoxelPos(i, j, k);
                        SweepPlanePoint pt = sweepPlane[i, k];

                        if (pt == null && p != null)        //If there is a section of voxel there, but no pt, add a new voxel shell pt to the sweep plane
                        {
                            pt = new SweepPlanePoint(p, i, k);
                            sweepPlane[i, k] = pt;
                            continue;
                        }
                        else if (pt != null)
                        {
                            if (p == null)                  //If there is a pt there, but no part listed, this is an interior pt or a the cross-section is shrinking
                            {
                                if (pt.mark == SweepPlanePoint.MarkingType.VoxelShell)  //label it as active so that it can be determined once all the points have been checked
                                {
                                    pt.mark = SweepPlanePoint.MarkingType.Active;
                                    lock(activePts)
                                        activePts.Add(pt);      //And add it to the list of active interior pts
                                }
                            }
                            else
                            {       //Make sure the point is labeled as a voxel shell if there is already a part there
                                pt.mark = SweepPlanePoint.MarkingType.VoxelShell;
                                lock (inactiveInteriorPts)
                                    inactiveInteriorPts.Remove(pt);
                                lock (activePts)
                                    activePts.Remove(pt);
                            }
                        }
                    }
                lock (_locker)
                    itemsQueued--;

                while (itemsQueued > 1)
                    Thread.Sleep(5);

                lock (_locker)
                    itemsQueued++;

                if (threadInd == 0)
                {

                    for (int i = 0; i < activePts.Count; i++) //Then, iterate through all active points for this section
                    {
                        SweepPlanePoint activeInteriorPt = activePts[i]; //Get active interior pt
                        if (activeInteriorPt.i + 1 < xLength * 8) //And all of its 4-neighbors
                            neighboringSweepPlanePts[0] = sweepPlane[activeInteriorPt.i + 1, activeInteriorPt.k];
                        else
                            neighboringSweepPlanePts[0] = null;
                        if (activeInteriorPt.i - 1 >= 0)
                            neighboringSweepPlanePts[1] = sweepPlane[activeInteriorPt.i - 1, activeInteriorPt.k];
                        else
                            neighboringSweepPlanePts[1] = null;
                        if (activeInteriorPt.k + 1 < zLength * 8)
                            neighboringSweepPlanePts[2] = sweepPlane[activeInteriorPt.i, activeInteriorPt.k + 1];
                        else
                            neighboringSweepPlanePts[2] = null;
                        if (activeInteriorPt.k - 1 >= 0)
                            neighboringSweepPlanePts[3] = sweepPlane[activeInteriorPt.i, activeInteriorPt.k - 1];
                        else
                            neighboringSweepPlanePts[3] = null;
                        bool remove = false;
                        for (int m = 0; m < neighboringSweepPlanePts.Length; m++) //Check if the active point is surrounded by 4 neighbors
                            if (neighboringSweepPlanePts[m] == null) //If any of them are null, this active point is larger than the current cross-section
                            { //In that case, it should be set to be removed
                                remove = true;
                                break;
                            }
                        if (remove) //If it is set to be removed...
                        {
                            for (int m = 0; m < neighboringSweepPlanePts.Length; m++) //Go through all the neighboring points
                            {
                                SweepPlanePoint neighbor = neighboringSweepPlanePts[m];
                                if (neighbor != null && neighbor.mark == SweepPlanePoint.MarkingType.InactiveInterior) //For the ones that exist, and are inactive interior...
                                {
                                    neighbor.mark = SweepPlanePoint.MarkingType.Active; //...mark them active
                                    inactiveInteriorPts.Remove(neighbor); //remove them from inactiveInterior
                                    activePts.Add(neighbor); //And add them to the end of activePts
                                }
                            }
                            sweepPlane[activeInteriorPt.i, activeInteriorPt.k] = null; //Then, set this point to null in the sweepPlane
                            SetVoxelSection(activeInteriorPt.i, j, activeInteriorPt.k, null); //Set the point on the voxel to null
                            activePts[i] = null; //And clear it out for this guy
                        }
                        else
                        { //If it's surrounded by other points, it's inactive; add it to that list
                            activeInteriorPt.mark = SweepPlanePoint.MarkingType.InactiveInterior;
                            inactiveInteriorPts.Add(activeInteriorPt);
                        }
                    }
                }

                lock (_locker)
                    itemsQueued--;

                while (itemsQueued > 1)
                    Thread.Sleep(5);

                #region multithreadedActivePts
                /*while (activePts.Count > 0)
                {
                    lock (_locker)
                        itemsQueued++;

                    List<SweepPlanePoint> myActivePts = new List<SweepPlanePoint>(activePts.GetRange(threadInd / 4 * activePts.Count, activePts.Count / 4));

                    lock (_locker)
                        itemsQueued--;

                    while (itemsQueued > 1) ;

                    lock (activePts)
                        activePts.Clear();

                    lock (_locker)
                        itemsQueued++;
                    

                    for (int i = 0; i < myActivePts.Count; i++)    //Then, iterate through all active points for this section
                    {
                        SweepPlanePoint activeInteriorPt;    //Get active interior pt
                        bool remove = false;
                        //Check if the active point is surrounded by 4 neighbors
                        //If any of them are null, this active point is larger than the current cross-section
                        //In that case, it should be set to be removed
                        lock (sweepPlane)
                        {
                            activeInteriorPt = myActivePts[i];
                            Monitor.Enter(activeInteriorPt);
                            Debug.Log("Enter center");
                            if (activeInteriorPt.i + 1 < xLength * 8)           //And all of its 4-neighbors
                            {
                                SweepPlanePoint pt = sweepPlane[activeInteriorPt.i + 1, activeInteriorPt.k];
                                neighboringSweepPlanePts[0] = pt;
                                if (pt != null)
                                    Monitor.Enter(pt);
                                else
                                    remove = true;
                            }
                            else
                            {
                                neighboringSweepPlanePts[0] = null;
                                remove = true;
                            }

                            if (activeInteriorPt.i - 1 >= 0)
                            {
                                SweepPlanePoint pt = sweepPlane[activeInteriorPt.i - 1, activeInteriorPt.k];
                                neighboringSweepPlanePts[1] = pt;
                                if (pt != null)
                                    Monitor.Enter(pt);
                                else
                                    remove = true;
                            }
                            else
                            {
                                neighboringSweepPlanePts[1] = null;
                                remove = true;
                            }

                            if (activeInteriorPt.k + 1 < zLength * 8)
                            {
                                SweepPlanePoint pt = sweepPlane[activeInteriorPt.i, activeInteriorPt.k + 1];
                                neighboringSweepPlanePts[2] = pt;
                                if (pt != null)
                                    Monitor.Enter(pt);
                                else
                                    remove = true;
                            }
                            else
                            {
                                neighboringSweepPlanePts[2] = null;
                                remove = true;
                            }

                            if (activeInteriorPt.k - 1 >= 0)
                            {
                                SweepPlanePoint pt = sweepPlane[activeInteriorPt.i, activeInteriorPt.k - 1];
                                neighboringSweepPlanePts[3] = pt;
                                if (pt != null)
                                    Monitor.Enter(pt);
                                else
                                    remove = true;
                            }
                            else
                            {
                                neighboringSweepPlanePts[3] = null;
                                remove = true;
                            }
                        }
                        try
                        {
                            if (remove)              //If it is set to be removed...
                            {
                                for (int m = 0; m < neighboringSweepPlanePts.Length; m++)   //Go through all the neighboring points
                                {
                                    SweepPlanePoint neighbor = neighboringSweepPlanePts[m];
                                    if (neighbor != null && neighbor.mark == SweepPlanePoint.MarkingType.InactiveInterior)    //For the ones that exist, and are inactive interior...
                                    {
                                        neighbor.mark = SweepPlanePoint.MarkingType.Active;         //...mark them active
                                        lock (inactiveInteriorPts)
                                            inactiveInteriorPts.Remove(neighbor);                       //remove them from inactiveInterior
                                        lock(activePts)
                                            activePts.Add(neighbor);                                    //And add them to the end of activePts
                                    }
                                }
                                lock (sweepPlane)
                                    sweepPlane[activeInteriorPt.i, activeInteriorPt.k] = null;          //Then, set this point to null in the sweepPlane
                                SetVoxelSection(activeInteriorPt.i, j, activeInteriorPt.k, null);   //Set the point on the voxel to null
                                myActivePts[i] = null;                                                //And clear it out for this guy
                            }
                            else
                            {           //If it's surrounded by other points, it's inactive; add it to that list
                                activeInteriorPt.mark = SweepPlanePoint.MarkingType.InactiveInterior;
                                lock (inactiveInteriorPts)
                                    inactiveInteriorPts.Add(activeInteriorPt);
                            }
                        }
                        finally
                        {
                            for (int m = 0; m < neighboringSweepPlanePts.Length; m++)
                                if (neighboringSweepPlanePts[m] != null)
                                {
                                    Monitor.Exit(neighboringSweepPlanePts[m]);
                                }
                            Monitor.Exit(activeInteriorPt);
                            activeInteriorPt = null;
                        }
                    }
                    lock (_locker)
                        itemsQueued--;

                    while (itemsQueued > 1) ;

                }
#endregion

                lock (_locker)
                    itemsQueued++;

                for (int m = inactiveInteriorPts.Count * (int)Math.Round(threadInd / 4f); m < inactiveInteriorPts.Count * (int)Math.Round(threadNum / 4f); m++)      //Any remaining inactive interior pts are guaranteed to be on the inside of the vehicle
                {
                    SweepPlanePoint inactivePt = inactiveInteriorPts[m];        //Get each
                    SetVoxelSection(inactivePt.i, j, inactivePt.k, inactivePt.part);    //And update the voxel accordingly
                }

                lock (_locker)
                    itemsQueued--;

                while (itemsQueued > 1)
                    Thread.Sleep(5);
            }
            lock (_locker)
                itemsQueued = 0;

            neighboringSweepPlanePts = null;
        }*/

        private class SweepPlanePoint
        {
            public Part part;
            public int i, k;

            public MarkingType mark = MarkingType.VoxelShell;

            public SweepPlanePoint(Part part, int i, int k)
            {
                this.i = i;
                this.k = k;
                this.part = part;
            }

            public enum MarkingType
            {
                VoxelShell,
                Active,
                InactiveInterior
            }
        }

        private class SolidifyData
        {
            public SweepPlanePoint[,] sweepPlane;
            public List<SweepPlanePoint> activePts;
            public List<SweepPlanePoint> inactiveInteriorPts;
            public int lowI, highI, lowK, highK;
            public int threadInd;

            public SolidifyData(SweepPlanePoint[,] sweepPlane, List<SweepPlanePoint> activePts, List<SweepPlanePoint> inactiveInteriorPts, int lowI, int highI, int lowK, int highK, int threadInd)
            {
                this.sweepPlane = sweepPlane;
                this.activePts = activePts;
                this.inactiveInteriorPts = inactiveInteriorPts;
                this.lowI = lowI;
                this.lowK = lowK;
                this.highI = highI;
                this.highK = highK;
                this.threadInd = threadInd;
            }
        }

        private class WorkData
        {
            public Part part;
            public Mesh mesh;
            public Matrix4x4 transform;

            public WorkData(Part part, Mesh mesh, Matrix4x4 transform)
            {
                this.part = part;
                this.mesh = mesh;
                this.transform = transform;
            }
        }
    }
}
