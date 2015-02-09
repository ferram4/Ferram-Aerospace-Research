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
using FerramAerospaceResearch.FARThreading;

namespace FerramAerospaceResearch.FARPartGeometry
{
    public class VehicleVoxel
    {
        float elementSize;
        float invElementSize;

        VoxelSection[, ,] voxelSections;
        int xLength, yLength, zLength;
        int xCellLength, yCellLength, zCellLength;
        int threadsQueued = 0;
        bool solidDone = false;
        object _locker = new object();

        Vector3 lowerRightCorner;
        const float RC = 0.5f;

        public VehicleVoxel(List<Part> partList, int elementCount, bool multiThreaded, bool solidify)
        {
            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            List<GeometryPartModule> geoModules = new List<GeometryPartModule>();
            for(int i = 0; i < partList.Count; i++)
            {
                Part p = partList[i];
                GeometryPartModule m = p.GetComponent<GeometryPartModule>();
                if (m != null)
                {
                    Vector3 minBounds = m.overallMeshBounds.min;
                    Vector3 maxBounds = m.overallMeshBounds.max;

                    min.x = Math.Min(min.x, minBounds.x);
                    min.y = Math.Min(min.y, minBounds.y);
                    min.z = Math.Min(min.z, minBounds.z);

                    max.x = Math.Max(max.x, maxBounds.x);
                    max.y = Math.Max(max.y, maxBounds.y);
                    max.z = Math.Max(max.z, maxBounds.z);

                    geoModules.Add(m);
                }
            }

            Vector3 size = max - min;

            float voxelVolume = size.x * size.y * size.z;
            float elementVol = voxelVolume / (float)elementCount;
            elementSize = (float)Math.Pow(elementVol, 1f / 3f);
            invElementSize = 1 / elementSize;

            float tmp = 0.125f * invElementSize;

            xLength = (int)Math.Ceiling(size.x * tmp);
            yLength = (int)Math.Ceiling(size.y * tmp);
            zLength = (int)Math.Ceiling(size.z * tmp);

            xCellLength = xLength * 8;
            yCellLength = yLength * 8;
            zCellLength = zLength * 8;

            Debug.Log(elementSize);
            Debug.Log(xLength + " " + yLength + " " + zLength);
            Debug.Log(size);

            Vector3 extents = new Vector3(); //this will be the distance from the center to the edges of the voxel object
            extents.x = xLength * 4 * elementSize;
            extents.y = yLength * 4 * elementSize;
            extents.z = zLength * 4 * elementSize;

            Vector3 center = (max + min) * 0.5f;    //Center of the vessel

            lowerRightCorner = center - extents;    //This places the center of the voxel at the center of the vehicle to achieve maximum symmetry

            voxelSections = new VoxelSection[xLength, yLength, zLength];

            for(int i = 0; i < geoModules.Count; i++)
            {
                GeometryPartModule m = geoModules[i];
                for (int j = 0; j < m.meshDataList.Count; j++)
                {
                    if (multiThreaded)
                    {
                        WorkData data = new WorkData(m.part, m.meshDataList[j]);
                        ThreadPool.QueueUserWorkItem(UpdateFromMesh, data);
                    }
                    else
                        UpdateFromMesh(m.meshDataList[j], m.part);
                    threadsQueued++;
                }
            }
            System.Threading.ThreadPriority currentPrio = Thread.CurrentThread.Priority;
            Thread.CurrentThread.Priority = System.Threading.ThreadPriority.BelowNormal;
            lock (_locker)
                while (threadsQueued > 0)
                    Monitor.Wait(_locker);
                
            Thread.CurrentThread.Priority = currentPrio;
            if(solidify)
                try
                {
                    SolidifyVoxel();
                    //MultithreadSolidifyVoxel();
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

        //Use when guaranteed that you will not attempt to write to the same section simultaneously
        private unsafe void SetVoxelSectionNoLock(int i, int j, int k, Part part)
        {
            int iSec, jSec, kSec;
            //Find the voxel section that this point points to

            iSec = i >> 3;
            jSec = j >> 3;
            kSec = k >> 3;

            VoxelSection section;

            section = voxelSections[iSec, jSec, kSec];
            if (section == null)
            {
                section = new VoxelSection(elementSize, lowerRightCorner + new Vector3(iSec, jSec, kSec) * elementSize * 8, iSec * 8, jSec * 8, kSec * 8);
                voxelSections[iSec, jSec, kSec] = section;
            }
           
            //Debug.Log(i.ToString() + ", " + j.ToString() + ", " + k.ToString() + ", " + part.partInfo.title);

            section.SetVoxelPointGlobalIndexNoLock(i, j, k, part);
        }
        
        private unsafe void SetVoxelSection(int i, int j, int k, Part part)
        {
            int iSec, jSec, kSec;
            //Find the voxel section that this point points to

            iSec = i >> 3;
            jSec = j >> 3;
            kSec = k >> 3;

            VoxelSection section;

            lock (voxelSections)
            {
                section = voxelSections[iSec, jSec, kSec];
                if (section == null)
                {
                    section = new VoxelSection(elementSize, lowerRightCorner + new Vector3(iSec, jSec, kSec) * elementSize * 8, iSec * 8, jSec * 8, kSec * 8);
                    voxelSections[iSec, jSec, kSec] = section;
                }
            }

            //Debug.Log(i.ToString() + ", " + j.ToString() + ", " + k.ToString() + ", " + part.partInfo.title);

            section.SetVoxelPointGlobalIndex(i, j, k, part);
        }

        private unsafe VoxelSection GetVoxelSection(int i, int j, int k)
        {
            int iSec, jSec, kSec;
            //Find the voxel section that this point points to
            iSec = i >> 3;
            jSec = j >> 3;
            kSec = k >> 3;

            VoxelSection section;
            lock (voxelSections)
            {
                section = voxelSections[iSec, jSec, kSec];
            }
            return section;
        }

        private unsafe Part GetPartAtVoxelPos(int i, int j, int k)
        {
            int iSec, jSec, kSec;
            //Find the voxel section that this point points to

            iSec = i >> 3;
            jSec = j >> 3;
            kSec = k >> 3;

            VoxelSection section;
            //lock (voxelSections)      //No locks are needed because reading and writing are not done in different threads simultaneously
            //{
                section = voxelSections[iSec, jSec, kSec];
            //}
            if (section == null)
                return null;

            return section.GetVoxelPointGlobalIndex(i, j, k);
        }

        private unsafe Part GetPartAtVoxelPos(int i, int j, int k, ref VoxelSection section)
        {
            return section.GetVoxelPointGlobalIndex(i, j, k);
        }

        private void UpdateFromMesh(object stuff)
        {
            try
            {
                WorkData data = (WorkData)stuff;
                Part part = data.part;
                GeometryMesh mesh = data.mesh;
                UpdateFromMesh(mesh, part);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        private unsafe void UpdateFromMesh(GeometryMesh mesh, Part part)
        {
            if (mesh.bounds.size.x < elementSize * 2 && mesh.bounds.size.y < elementSize * 2 && mesh.bounds.size.z < elementSize * 2)
            {
                CalculateVoxelShellFromTinyMesh(mesh.bounds.min, mesh.bounds.max, part);
                lock (_locker)
                {
                    threadsQueued--;
                    Monitor.Pulse(_locker);
                } 
                return;
            }

            /*Vector3[] vertsVoxelSpace = new Vector3[vertices.Length];
            for (int i = 0; i < vertsVoxelSpace.Length; i++)
            {
                vertsVoxelSpace[i] = transform.MultiplyPoint3x4(mesh.vertices[i]);
            }*/

            for (int a = 0; a < mesh.triangles.Length; a += 3)
            {
                Vector3 vert1, vert2, vert3;

                vert1 = mesh.vertices[mesh.triangles[a]];
                vert2 = mesh.vertices[mesh.triangles[a + 1]];
                vert3 = mesh.vertices[mesh.triangles[a + 2]];

                CalculateVoxelShellForTriangle(vert1, vert2, vert3, part);
            }

            lock (_locker)
            {
                threadsQueued--;
                Monitor.Pulse(_locker);
            }
        }

        private void CalculateVoxelShellFromTinyMesh(Vector3 minMesh, Vector3 maxMesh, Part part)
        {
            int lowerI, lowerJ, lowerK;
            int upperI, upperJ, upperK;

            Vector3 min, max;
            min = (minMesh - lowerRightCorner) * invElementSize;
            max = (maxMesh - lowerRightCorner) * invElementSize;

            lowerI = (int)Math.Floor(min.x);
            lowerJ = (int)Math.Floor(min.y);
            lowerK = (int)Math.Floor(min.z);

            upperI = (int)Math.Ceiling(max.x);
            upperJ = (int)Math.Ceiling(max.y);
            upperK = (int)Math.Ceiling(max.z);

            lowerI = Math.Max(lowerI, 0);
            lowerJ = Math.Max(lowerJ, 0);
            lowerK = Math.Max(lowerK, 0);

            upperI = Math.Min(upperI, xCellLength - 1);
            upperJ = Math.Min(upperJ, yCellLength - 1);
            upperK = Math.Min(upperK, zCellLength - 1);

            for (int i = lowerI; i <= upperI; i++)
                for (int j = lowerJ; j <= upperJ; j++)
                    for (int k = lowerK; k <= upperK; k++)
                    {
                        SetVoxelSection(i, j, k, part);
                    }
        }

        private void CalculateVoxelShellForTriangle(Vector3 vert1, Vector3 vert2, Vector3 vert3, Part part)
        {
            //Vector4 plane = CalculateEquationOfPlane(vert1, vert2, vert3);
            Vector4 indexPlane = CalculateEquationOfPlaneInIndices(vert1, vert2, vert3);

            float x, y, z;
            x = Math.Abs(indexPlane.x);
            y = Math.Abs(indexPlane.y);
            z = Math.Abs(indexPlane.z);

            //Vector4 indexPlane = TransformPlaneToIndices(plane);

            if (x > y && x > z)
                VoxelShellTrianglePerpX(indexPlane, vert1, vert2, vert3, part);
            else if(y > x && y > z)
                VoxelShellTrianglePerpY(indexPlane, vert1, vert2, vert3, part);
            else
                VoxelShellTrianglePerpZ(indexPlane, vert1, vert2, vert3, part);
        }

        private void VoxelShellTrianglePerpX(Vector4 indexPlane, Vector3 vert1, Vector3 vert2, Vector3 vert3, Part part)
        {
            Vector2 vert1Proj, vert2Proj, vert3Proj;
            vert1Proj = new Vector2(vert1.y - lowerRightCorner.y, vert1.z - lowerRightCorner.z) * invElementSize;
            vert2Proj = new Vector2(vert2.y - lowerRightCorner.y, vert2.z - lowerRightCorner.z) * invElementSize;
            vert3Proj = new Vector2(vert3.y - lowerRightCorner.y, vert3.z - lowerRightCorner.z) * invElementSize;

            Vector2 p1p2, p1p3;
            p1p2 = vert2Proj - vert1Proj;
            p1p3 = vert3Proj - vert1Proj;

            float dot12_12, dot12_13, dot13_13;
            dot12_12 = p1p2.x * p1p2.x + p1p2.y * p1p2.y;
            dot12_13 = p1p2.x * p1p3.x + p1p2.y * p1p3.y;
            dot13_13 = p1p3.x * p1p3.x + p1p3.y * p1p3.y;

/*            dot12_12 = Vector2.Dot(p1p2, p1p2);
            dot12_13 = Vector2.Dot(p1p2, p1p3);
            dot13_13 = Vector2.Dot(p1p3, p1p3);*/

            float invDenom = 1 / (dot12_12 * dot13_13 - dot12_13 * dot12_13);

            int lowJ, highJ, lowK, highK;
            lowJ = (int)Math.Min(vert1Proj.x, Math.Min(vert2Proj.x, vert3Proj.x));
            highJ = (int)Math.Ceiling(Math.Max(vert1Proj.x, Math.Max(vert2Proj.x, vert3Proj.x)));
            lowK = (int)Math.Min(vert1Proj.y, Math.Min(vert2Proj.y, vert3Proj.y));
            highK = (int)Math.Ceiling(Math.Max(vert1Proj.y, Math.Max(vert2Proj.y, vert3Proj.y)));

            if (lowJ < 0)
                lowJ = 0;
            if (lowK < 0)
                lowK = 0;
            if (highJ >= yCellLength)
                highJ = yCellLength - 1;
            if (highK >= zCellLength)
                highK = zCellLength - 1;

            /*lowJ = Math.Max(lowJ, 0);
            lowK = Math.Max(lowK, 0);
            highJ = Math.Min(highJ, yCellLength - 1);
            highK = Math.Min(highK, zCellLength - 1);*/

            for (int j = lowJ; j <= highJ; j++)
                for (int k = lowK; k <= highK; k++)
                {
                    Vector2 pt = new Vector2(j, k);
                    Vector2 p1TestPt = pt - vert1Proj;
                    float dot12_test, dot13_test;
                    dot12_test = p1p2.x * p1TestPt.x + p1p2.y * p1TestPt.y;
                    dot13_test = p1p3.x * p1TestPt.x + p1p3.y * p1TestPt.y;

                    /*dot12_test = Vector2.Dot(p1p2, p1TestPt);
                    dot13_test = Vector2.Dot(p1p3, p1TestPt);*/

                    float u, v;
                    u = (dot13_13 * dot12_test - dot12_13 * dot13_test) * invDenom;
                    v = (dot12_12 * dot13_test - dot12_13 * dot12_test) * invDenom;

                    if (u >= 0 && v >= 0 && u + v <= 1)
                    {
                        int i = (int)Math.Round(-(indexPlane.y * j + indexPlane.z * k + indexPlane.w) / indexPlane.x);
                        if (i < 0 || i >= xCellLength)
                            continue;

                        SetVoxelSection(i, j, k, part);
                        continue;
                    }
                    Vector2 p2TestPt = pt - vert2Proj;
                    Vector2 p3TestPt = pt - vert3Proj;
                    if (p1TestPt.magnitude < RC || p2TestPt.magnitude < RC || p3TestPt.magnitude < RC)
                    {
                        int i = (int)Math.Round(-(indexPlane.y * j + indexPlane.z * k + indexPlane.w) / indexPlane.x);
                        if (i < 0 || i >= xCellLength)
                            continue;

                        SetVoxelSection(i, j, k, part);
                        continue;
                    }

                    if ((u >= 0 && u <= 1 && DistancePerp(p1p2, p1TestPt) < RC) ||
                        (v >= 0 && v <= 1 && DistancePerp(p1p3, p1TestPt) < RC) ||
                        (u >= 0 && v >= 0 && DistancePerp(vert3Proj - vert2Proj, p2TestPt) < RC))
                    {
                        int i = (int)Math.Round(-(indexPlane.y * j + indexPlane.z * k + indexPlane.w) / indexPlane.x);
                        if (i < 0 || i >= xCellLength)
                            continue;

                        SetVoxelSection(i, j, k, part);

                    }
                }
        }

        private void VoxelShellTrianglePerpY(Vector4 indexPlane, Vector3 vert1, Vector3 vert2, Vector3 vert3, Part part)
        {
            Vector2 vert1Proj, vert2Proj, vert3Proj;
            vert1Proj = new Vector2(vert1.x - lowerRightCorner.x, vert1.z - lowerRightCorner.z) * invElementSize;
            vert2Proj = new Vector2(vert2.x - lowerRightCorner.x, vert2.z - lowerRightCorner.z) * invElementSize;
            vert3Proj = new Vector2(vert3.x - lowerRightCorner.x, vert3.z - lowerRightCorner.z) * invElementSize;

            
            Vector2 p1p2, p1p3;
            p1p2 = vert2Proj - vert1Proj;
            p1p3 = vert3Proj - vert1Proj;

            float dot12_12, dot12_13, dot13_13;
            dot12_12 = p1p2.x * p1p2.x + p1p2.y * p1p2.y;
            dot12_13 = p1p2.x * p1p3.x + p1p2.y * p1p3.y;
            dot13_13 = p1p3.x * p1p3.x + p1p3.y * p1p3.y;

            /*            dot12_12 = Vector2.Dot(p1p2, p1p2);
                        dot12_13 = Vector2.Dot(p1p2, p1p3);
                        dot13_13 = Vector2.Dot(p1p3, p1p3);*/

            float invDenom = 1 / (dot12_12 * dot13_13 - dot12_13 * dot12_13);

            int lowI, highI, lowK, highK;
            lowI = (int)Math.Min(vert1Proj.x, Math.Min(vert2Proj.x, vert3Proj.x));
            highI = (int)Math.Ceiling(Math.Max(vert1Proj.x, Math.Max(vert2Proj.x, vert3Proj.x)));
            lowK = (int)Math.Min(vert1Proj.y, Math.Min(vert2Proj.y, vert3Proj.y));
            highK = (int)Math.Ceiling(Math.Max(vert1Proj.y, Math.Max(vert2Proj.y, vert3Proj.y)));


            if (lowI < 0)
                lowI = 0;
            if (lowK < 0)
                lowK = 0;
            if (highI >= xCellLength)
                highI = xCellLength - 1;
            if (highK >= zCellLength)
                highK = zCellLength - 1;

            /*lowI = Math.Max(lowI, 0);
            lowK = Math.Max(lowK, 0);
            highI = Math.Min(highI, xCellLength - 1);
            highK = Math.Min(highK, zCellLength - 1);*/

            for (int i = lowI; i <= highI; i++)
                for (int k = lowK; k <= highK; k++)
                {
                    Vector2 pt = new Vector2(i, k);
                    Vector2 p1TestPt = pt - vert1Proj;
                    float dot12_test, dot13_test;
                    dot12_test = p1p2.x * p1TestPt.x + p1p2.y * p1TestPt.y;
                    dot13_test = p1p3.x * p1TestPt.x + p1p3.y * p1TestPt.y;

                    /*dot12_test = Vector2.Dot(p1p2, p1TestPt);
                    dot13_test = Vector2.Dot(p1p3, p1TestPt);*/

                    float u, v;
                    u = (dot13_13 * dot12_test - dot12_13 * dot13_test) * invDenom;
                    v = (dot12_12 * dot13_test - dot12_13 * dot12_test) * invDenom;

                    if (u >= 0 && v >= 0 && u + v <= 1)
                    {
                        int j = (int)Math.Round(-(indexPlane.x * i + indexPlane.z * k + indexPlane.w) / indexPlane.y);
                        if (j < 0 || j >= yCellLength)
                            continue;
                        SetVoxelSection(i, j, k, part);
                        continue;
                    }
                    Vector2 p2TestPt = pt - vert2Proj;
                    Vector2 p3TestPt = pt - vert3Proj;
                    if (p1TestPt.magnitude < RC || p2TestPt.magnitude < RC || p3TestPt.magnitude < RC)
                    {
                        int j = (int)Math.Round(-(indexPlane.x * i + indexPlane.z * k + indexPlane.w) / indexPlane.y);
                        if (j < 0 || j >= yCellLength)
                            continue;
                        SetVoxelSection(i, j, k, part);
                        continue;
                    }

                    if ((u >= 0 && u <= 1 && DistancePerp(p1p2, p1TestPt) < RC) ||
                        (v >= 0 && v <= 1 && DistancePerp(p1p3, p1TestPt) < RC) ||
                        (u >= 0 && v >= 0 && DistancePerp(vert3Proj - vert2Proj, p2TestPt) < RC))
                    {
                        int j = (int)Math.Round(-(indexPlane.x * i + indexPlane.z * k + indexPlane.w) / indexPlane.y);
                        if (j < 0 || j >= yCellLength)
                            continue;
                        SetVoxelSection(i, j, k, part);
                    }
                }
        }

        private void VoxelShellTrianglePerpZ(Vector4 indexPlane, Vector3 vert1, Vector3 vert2, Vector3 vert3, Part part)
        {
            Vector2 vert1Proj, vert2Proj, vert3Proj;
            vert1Proj = new Vector2(vert1.x - lowerRightCorner.x, vert1.y - lowerRightCorner.y) * invElementSize;
            vert2Proj = new Vector2(vert2.x - lowerRightCorner.x, vert2.y - lowerRightCorner.y) * invElementSize;
            vert3Proj = new Vector2(vert3.x - lowerRightCorner.x, vert3.y - lowerRightCorner.y) * invElementSize;

            Vector2 p1p2, p1p3;
            p1p2 = vert2Proj - vert1Proj;
            p1p3 = vert3Proj - vert1Proj;

            float dot12_12, dot12_13, dot13_13;
            dot12_12 = p1p2.x * p1p2.x + p1p2.y * p1p2.y;
            dot12_13 = p1p2.x * p1p3.x + p1p2.y * p1p3.y;
            dot13_13 = p1p3.x * p1p3.x + p1p3.y * p1p3.y;

            /*            dot12_12 = Vector2.Dot(p1p2, p1p2);
                        dot12_13 = Vector2.Dot(p1p2, p1p3);
                        dot13_13 = Vector2.Dot(p1p3, p1p3);*/

            float invDenom = 1 / (dot12_12 * dot13_13 - dot12_13 * dot12_13);

            int lowI, highI, lowJ, highJ;
            lowI = (int)Math.Min(vert1Proj.x, Math.Min(vert2Proj.x, vert3Proj.x));
            highI = (int)Math.Ceiling(Math.Max(vert1Proj.x, Math.Max(vert2Proj.x, vert3Proj.x)));
            lowJ = (int)Math.Min(vert1Proj.y, Math.Min(vert2Proj.y, vert3Proj.y));
            highJ = (int)Math.Ceiling(Math.Max(vert1Proj.y, Math.Max(vert2Proj.y, vert3Proj.y)));


            if (lowJ < 0)
                lowJ = 0;
            if (lowI < 0)
                lowI = 0;
            if (highJ >= yCellLength)
                highJ = yCellLength - 1;
            if (highI >= xCellLength)
                highI = xCellLength - 1;

            /*lowJ = Math.Max(lowJ, 0);
            lowI = Math.Max(lowI, 0);
            highJ = Math.Min(highJ, yCellLength - 1);
            highI = Math.Min(highI, xCellLength - 1);*/

            for (int i = lowI; i <= highI; i++)
                for (int j = lowJ; j <= highJ; j++)
                {
                    Vector2 pt = new Vector2(i, j);
                    Vector2 p1TestPt = pt - vert1Proj;
                    float dot12_test, dot13_test;
                    dot12_test = p1p2.x * p1TestPt.x + p1p2.y * p1TestPt.y;
                    dot13_test = p1p3.x * p1TestPt.x + p1p3.y * p1TestPt.y;

                    /*dot12_test = Vector2.Dot(p1p2, p1TestPt);
                    dot13_test = Vector2.Dot(p1p3, p1TestPt);*/

                    float u, v;
                    u = (dot13_13 * dot12_test - dot12_13 * dot13_test) * invDenom;
                    v = (dot12_12 * dot13_test - dot12_13 * dot12_test) * invDenom;

                    if (u >= 0 && v >= 0 && u + v <= 1)
                    {
                        int k = (int)Math.Round(-(indexPlane.x * i + indexPlane.y * j + indexPlane.w) / indexPlane.z);
                        if (k < 0 || k >= zCellLength)
                            continue;

                        SetVoxelSection(i, j, k, part);
                        continue;
                    }
                    Vector2 p2TestPt = pt - vert2Proj;
                    Vector2 p3TestPt = pt - vert3Proj;
                    if (p1TestPt.magnitude < RC || p2TestPt.magnitude < RC || p3TestPt.magnitude < RC)
                    {
                        int k = (int)Math.Round(-(indexPlane.x * i + indexPlane.y * j + indexPlane.w) / indexPlane.z);
                        if (k < 0 || k >= zCellLength)
                            continue;

                        SetVoxelSection(i, j, k, part);
                        continue;
                    }

                    if ((u >= 0 && u <= 1 && DistancePerp(p1p2, p1TestPt) < RC) ||
                        (v >= 0 && v <= 1 && DistancePerp(p1p3, p1TestPt) < RC) ||
                        (u >= 0 && v >= 0 && DistancePerp(vert3Proj - vert2Proj, p2TestPt) < RC))
                    {
                        int k = (int)Math.Round(-(indexPlane.x * i + indexPlane.y * j + indexPlane.w) / indexPlane.z);
                        if (k < 0 || k >= zCellLength)
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

            return Math.Abs(testVec.x * perpVector.x + testVec.y * perpVector.y);
            //return Math.Abs(Vector2.Dot(testVec, perpVector));
        }

        private Vector4 CalculateEquationOfPlaneInIndices(Vector3 pt1, Vector3 pt2, Vector3 pt3)
        {
            Vector3 p1p2 = pt2 - pt1;
            Vector3 p1p3 = pt3 - pt1;

            Vector3 tmp = Vector3.Cross(p1p2, p1p3);//.normalized;

            Vector4 result = new Vector4(tmp.x, tmp.y, tmp.z);

            result.w = result.x * (lowerRightCorner.x - pt1.x) + result.y * (lowerRightCorner.y - pt1.y) + result.z * (lowerRightCorner.z - pt1.z);
            result.w *= invElementSize;
            //result.x = result.x;// *elementSize;
            //result.y = result.y;// *elementSize;
            //result.z = result.z;// *elementSize;

            return result;
        }


        private Vector4 CalculateEquationOfPlane(Vector3 pt1, Vector3 pt2, Vector3 pt3)
        {
            Vector3 p1p2 = pt2 - pt1;
            Vector3 p1p3 = pt3 - pt1;

            Vector3 tmp = Vector3.Cross(p1p2, p1p3);//.normalized;

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

        public unsafe float[] CrossSectionalArea(Vector3 orientation)
        {
            float[] crossSections = new float[yCellLength];
            float areaPerElement = elementSize * elementSize;
            for (int j = 0; j < yCellLength; j++)
            {
                int areaCount = 0;
                for (int i = 0; i < xCellLength; i++)
                    for (int k = 0; k < zCellLength; k++)
                    {
                        if (GetPartAtVoxelPos(i, j, k) != null)
                            areaCount++;
                    }
                //Debug.Log(area);
                crossSections[j] = areaCount * areaPerElement;
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

        private void MultithreadSolidifyVoxel()
        {
            int threadCount = 4;

            SweepPlanePoint[,] sweepPlane = new SweepPlanePoint[xCellLength, zCellLength];
            List<SweepPlanePoint> activePts = new List<SweepPlanePoint>();
            HashSet<SweepPlanePoint>[] inactiveInteriorPtsHashes = new HashSet<SweepPlanePoint>[threadCount];

            for(int i = 0; i < inactiveInteriorPtsHashes.Length; i++)
                inactiveInteriorPtsHashes[i] = new HashSet<SweepPlanePoint>();

            ThreadBarrier barrier = new ThreadBarrier(4);

            ThreadPool.QueueUserWorkItem(MultithreadSolidifyVoxelWorker, new SolidifyData(sweepPlane, activePts, inactiveInteriorPtsHashes, 0, xCellLength / 2, 0, zCellLength / 2, 0, barrier));
            ThreadPool.QueueUserWorkItem(MultithreadSolidifyVoxelWorker, new SolidifyData(sweepPlane, activePts, inactiveInteriorPtsHashes, xCellLength / 2, xCellLength, 0, zCellLength / 2, 1, barrier));
            ThreadPool.QueueUserWorkItem(MultithreadSolidifyVoxelWorker, new SolidifyData(sweepPlane, activePts, inactiveInteriorPtsHashes, 0, xCellLength / 2, zCellLength / 2, zCellLength, 2, barrier));
            ThreadPool.QueueUserWorkItem(MultithreadSolidifyVoxelWorker, new SolidifyData(sweepPlane, activePts, inactiveInteriorPtsHashes, xCellLength / 2, xCellLength, zCellLength / 2, zCellLength, 3, barrier));

            lock (_locker)
                while (!solidDone)
                    Monitor.Wait(_locker);

            sweepPlane = null;
            activePts = null;
            inactiveInteriorPtsHashes = null;
        }

        private void MultithreadSolidifyVoxelWorker(object data)
        {
            SolidifyData castData = (SolidifyData)data;
            try
            {
                MultithreadSolidifyVoxelWorker(castData.sweepPlane, castData.activePts, castData.inactiveInteriorPtsHashes
                    , castData.lowI, castData.highI, castData.lowK, castData.highK, castData.threadInd, castData.barrier);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }


        private unsafe void MultithreadSolidifyVoxelWorker(SweepPlanePoint[,] sweepPlane, List<SweepPlanePoint> activePts, HashSet<SweepPlanePoint>[] inactiveInteriorPtsHashes,
            int lowI, int highI, int lowK, int highK, int threadInd, ThreadBarrier barrier)
        {
            HashSet<SweepPlanePoint> inactiveInteriorPts = inactiveInteriorPtsHashes[threadInd];
            SweepPlanePoint[] neighboringSweepPlanePts = new SweepPlanePoint[4];

            for (int j = 0; j < yCellLength; j++) //Iterate from front of vehicle to back
            {
                for (int i = lowI; i < highI; i++) //Iterate across the cross-section plane to add voxel shell and mark active interior points
                    for (int k = lowK; k < highK; k++)
                    {
                        SweepPlanePoint pt;
                        pt = sweepPlane[i, k];      //locks unnecessary due to reading and writing to different indices in different threads
                        Part p = GetPartAtVoxelPos(i, j, k);

                        if (pt == null && p != null) //If there is a section of voxel there, but no pt, add a new voxel shell pt to the sweep plane
                        {
                            sweepPlane[i, k] = new SweepPlanePoint(p, i, k);      //locks unnecessary due to reading and writing to different indices in different threads
                            continue;
                        }
                        else if (pt != null)
                        {
                            if (p == null) //If there is a pt there, but no part listed, this is an interior pt or a the cross-section is shrinking
                            {
                                if (pt.mark == SweepPlanePoint.MarkingType.VoxelShell) //label it as active so that it can be determined once all the points have been checked
                                {
                                    pt.mark = SweepPlanePoint.MarkingType.Active;
                                    lock(activePts)         //lock needed due to common activePts list
                                        activePts.Add(pt); //And add it to the list of active interior pts
                                }
                            }
                            else
                            { //Make sure the point is labeled as a voxel shell if there is already a part there
                                pt.mark = SweepPlanePoint.MarkingType.VoxelShell;
                                pt.part = p;
                                inactiveInteriorPts.Remove(pt);
                            }
                        }
                    }

                barrier.SignalAndWait();

                if (threadInd == 0)     //Go singlethreaded for this complicated operation; it isn't the meat of the expense, so this is alright
                {
                    for (int i = 0; i < activePts.Count; i++) //Then, iterate through all active points for this section
                    {
                        SweepPlanePoint activeInteriorPt = activePts[i]; //Get active interior pt
                        if (activeInteriorPt.i + 1 < xCellLength) //And all of its 4-neighbors
                            neighboringSweepPlanePts[0] = sweepPlane[activeInteriorPt.i + 1, activeInteriorPt.k];
                        else
                            neighboringSweepPlanePts[0] = null;
                        if (activeInteriorPt.i - 1 > 0)
                            neighboringSweepPlanePts[1] = sweepPlane[activeInteriorPt.i - 1, activeInteriorPt.k];
                        else
                            neighboringSweepPlanePts[1] = null;
                        if (activeInteriorPt.k + 1 < zCellLength)
                            neighboringSweepPlanePts[2] = sweepPlane[activeInteriorPt.i, activeInteriorPt.k + 1];
                        else
                            neighboringSweepPlanePts[2] = null;
                        if (activeInteriorPt.k - 1 > 0)
                            neighboringSweepPlanePts[3] = sweepPlane[activeInteriorPt.i, activeInteriorPt.k - 1];
                        else
                            neighboringSweepPlanePts[3] = null;

                        bool remove = false;
                        foreach (SweepPlanePoint neighbor in neighboringSweepPlanePts)//Check if the active point is surrounded by 4 neighbors
                            if (neighbor == null || neighbor.mark == SweepPlanePoint.MarkingType.Clear) //If any of them are null or marked clear, this active point is larger than the current cross-section
                            {                                                                       //In that case, it should be set to be removed
                                remove = true;
                                break;
                            }
                        if (remove) //If it is set to be removed...
                        {
                            foreach (SweepPlanePoint neighbor in neighboringSweepPlanePts)// (int m = 0; m < neighboringSweepPlanePts.Length; m++) //Go through all the neighboring points
                            {
                                //SweepPlanePoint neighbor = neighboringSweepPlanePts[m];
                                if (neighbor != null && neighbor.mark == SweepPlanePoint.MarkingType.InactiveInterior) //For the ones that exist, and are inactive interior...
                                {
                                    neighbor.mark = SweepPlanePoint.MarkingType.Active; //...mark them active

                                    foreach (HashSet<SweepPlanePoint> inactiveHash in inactiveInteriorPtsHashes)    //Go through all the possible inactive hashes
                                        if (inactiveHash.Remove(neighbor))          // try to remove them from inactiveInterior
                                            break;                                  //And end when you do

                                    activePts.Add(neighbor); //Then, add them to the end of activePts
                                }
                            }
                            sweepPlane[activeInteriorPt.i, activeInteriorPt.k].mark = SweepPlanePoint.MarkingType.Clear; //Then, set this point to be marked clear in the sweepPlane
                            //SetVoxelSection(activeInteriorPt.i, j, activeInteriorPt.k, null); //Set the point on the voxel to null
                            //activePts[i] = null; //And clear it out for this guy
                        }
                        else
                        { //If it's surrounded by other points, it's inactive; add it to that list
                            activeInteriorPt.mark = SweepPlanePoint.MarkingType.InactiveInterior;
                            if(activeInteriorPt.i >= xCellLength / 2)
                            {
                                if (activeInteriorPt.k >= zCellLength / 2)      //Upper right quadrant is thread 3
                                    inactiveInteriorPtsHashes[3].Add(activeInteriorPt);
                                else                                            //Lower right quadrant is thread 1
                                    inactiveInteriorPtsHashes[1].Add(activeInteriorPt); 
                            }
                            else
                            {
                                if (activeInteriorPt.k >= zCellLength / 2)      //Upper left quadrant is thread 2
                                    inactiveInteriorPtsHashes[2].Add(activeInteriorPt);
                                else                                            //Lower left quadrant is thread 0 (this thread)
                                    inactiveInteriorPtsHashes[0].Add(activeInteriorPt); 
                            }
                            //inactiveInteriorPts.Add(activeInteriorPt);
                        }
                    }
                    activePts.Clear(); //Clear activePts every iteration
                }

                barrier.SignalAndWait();

                foreach (SweepPlanePoint inactivePt in inactiveInteriorPts) //Any remaining inactive interior pts are guaranteed to be on the inside of the vehicle
                {
                    SetVoxelSectionNoLock(inactivePt.i, j, inactivePt.k, inactivePt.part); //Get each and update the voxel accordingly
                }

                barrier.SignalAndWait();
            }
            //Cleanup
            inactiveInteriorPts = null;
            neighboringSweepPlanePts = null;

            //If we're here, everyone has finished; set conditions to end this stuff
            solidDone = true;
            lock (_locker)
                Monitor.PulseAll(_locker);
        }
        
        private unsafe void SolidifyVoxel()
        {
            SweepPlanePoint[,] sweepPlane = new SweepPlanePoint[xCellLength, zCellLength];
            List<SweepPlanePoint> activePts = new List<SweepPlanePoint>();
            HashSet<SweepPlanePoint> inactiveInteriorPts = new HashSet<SweepPlanePoint>();
            SweepPlanePoint[] neighboringSweepPlanePts = new SweepPlanePoint[4];

            for (int j = 0; j < yCellLength; j++) //Iterate from front of vehicle to back
            {
                for (int i = 0; i < xCellLength; i++) //Iterate across the cross-section plane to add voxel shell and mark active interior points
                    for (int k = 0; k < zCellLength; k++)
                    {
                        SweepPlanePoint pt = sweepPlane[i, k];
                        Part p = GetPartAtVoxelPos(i, j, k);

                        if (pt == null && p != null) //If there is a section of voxel there, but no pt, add a new voxel shell pt to the sweep plane
                        {
                            sweepPlane[i, k] = new SweepPlanePoint(p, i, k);
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
                            }
                        }
                    }
                for (int i = 0; i < activePts.Count; i++) //Then, iterate through all active points for this section
                {
                    SweepPlanePoint activeInteriorPt = activePts[i]; //Get active interior pt
                    if (activeInteriorPt.i + 1 < xCellLength) //And all of its 4-neighbors
                        neighboringSweepPlanePts[0] = sweepPlane[activeInteriorPt.i + 1, activeInteriorPt.k];
                    else
                        neighboringSweepPlanePts[0] = null;
                    if (activeInteriorPt.i - 1 > 0)
                        neighboringSweepPlanePts[1] = sweepPlane[activeInteriorPt.i - 1, activeInteriorPt.k];
                    else
                        neighboringSweepPlanePts[1] = null;
                    if (activeInteriorPt.k + 1 < zCellLength)
                        neighboringSweepPlanePts[2] = sweepPlane[activeInteriorPt.i, activeInteriorPt.k + 1];
                    else
                        neighboringSweepPlanePts[2] = null;
                    if (activeInteriorPt.k - 1 > 0)
                        neighboringSweepPlanePts[3] = sweepPlane[activeInteriorPt.i, activeInteriorPt.k - 1];
                    else
                        neighboringSweepPlanePts[3] = null;

                    bool remove = false;
                    foreach(SweepPlanePoint neighbor in neighboringSweepPlanePts)//Check if the active point is surrounded by 4 neighbors
                        if (neighbor == null || neighbor.mark == SweepPlanePoint.MarkingType.Clear) //If any of them are null or marked clear, this active point is larger than the current cross-section
                        {                                                                       //In that case, it should be set to be removed
                            remove = true;
                            break;
                        }
                    if (remove) //If it is set to be removed...
                    {
                        foreach(SweepPlanePoint neighbor in neighboringSweepPlanePts)// (int m = 0; m < neighboringSweepPlanePts.Length; m++) //Go through all the neighboring points
                        {
                            //SweepPlanePoint neighbor = neighboringSweepPlanePts[m];
                            if (neighbor != null && neighbor.mark == SweepPlanePoint.MarkingType.InactiveInterior) //For the ones that exist, and are inactive interior...
                            {
                                neighbor.mark = SweepPlanePoint.MarkingType.Active; //...mark them active
                                inactiveInteriorPts.Remove(neighbor); //remove them from inactiveInterior
                                activePts.Add(neighbor); //And add them to the end of activePts
                            }
                        }
                        sweepPlane[activeInteriorPt.i, activeInteriorPt.k].mark = SweepPlanePoint.MarkingType.Clear; //Then, set this point to be marked clear in the sweepPlane
                        //SetVoxelSection(activeInteriorPt.i, j, activeInteriorPt.k, null); //Set the point on the voxel to null
                        //activePts[i] = null; //And clear it out for this guy
                    }
                    else
                    { //If it's surrounded by other points, it's inactive; add it to that list
                        activeInteriorPt.mark = SweepPlanePoint.MarkingType.InactiveInterior;
                        inactiveInteriorPts.Add(activeInteriorPt);
                    }
                }
                activePts.Clear(); //Clear activePts every iteration

                foreach(SweepPlanePoint inactivePt in inactiveInteriorPts) //Any remaining inactive interior pts are guaranteed to be on the inside of the vehicle
                {

                    SetVoxelSectionNoLock(inactivePt.i, j, inactivePt.k, inactivePt.part); //Get each and update the voxel accordingly
                }
            }
            //Cleanup
            sweepPlane = null;
            activePts = null;
            inactiveInteriorPts = null;
            neighboringSweepPlanePts = null;
        }

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
                InactiveInterior,
                Clear
            }
        }

        private class SolidifyData
        {
            public SweepPlanePoint[,] sweepPlane;
            public List<SweepPlanePoint> activePts;
            public HashSet<SweepPlanePoint>[] inactiveInteriorPtsHashes;
            public int lowI, highI, lowK, highK;
            public int threadInd;
            public ThreadBarrier barrier;

            public SolidifyData(SweepPlanePoint[,] sweepPlane, List<SweepPlanePoint> activePts, HashSet<SweepPlanePoint>[] inactiveInteriorPtsHashes
                , int lowI, int highI, int lowK, int highK, int threadInd, ThreadBarrier barrier)
            {
                this.sweepPlane = sweepPlane;
                this.activePts = activePts;
                this.inactiveInteriorPtsHashes = inactiveInteriorPtsHashes;
                this.lowI = lowI;
                this.lowK = lowK;
                this.highI = highI;
                this.highK = highK;
                this.threadInd = threadInd;
                this.barrier = barrier;
            }
        }

        private class WorkData
        {
            public Part part;
            public GeometryMesh mesh;
            //public Mesh mesh;
            //public Matrix4x4 transform;

            public WorkData(Part part, GeometryMesh mesh)
            {
                this.part = part;
                this.mesh = mesh;
                //this.transform = transform;
            }
        }
    }
}
