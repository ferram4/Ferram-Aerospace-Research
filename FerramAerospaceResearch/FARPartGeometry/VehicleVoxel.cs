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
        VoxelSection[, ,] voxelSections;
        int xLength, yLength, zLength;
        int itemsQueued = 0;
        object _locker = new object();
        Vector3 lowerRightCorner;

        public VehicleVoxel(List<Part> partList, int elementCount)
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

            float tmp = 0.125f / elementSize;

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
                    WorkData data = new WorkData(m.part, m.geometryMeshes[j], m.meshToVesselMatrixList[j]);
                    //UpdateFromMesh(data);
                    ThreadPool.QueueUserWorkItem(UpdateFromMesh, data);
                    itemsQueued++;
                }
            }
            while (itemsQueued > 0)
            {
                Thread.Sleep(50);
            }
            //SolidifyVoxel();
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
                    section = new VoxelSection(elementSize, 8, 8, 8, lowerRightCorner + new Vector3(iSec, jSec, kSec) * elementSize * 8);
                    voxelSections[iSec, jSec, kSec] = section;
                }
            }

            //Debug.Log(i.ToString() + ", " + j.ToString() + ", " + k.ToString() + ", " + part.partInfo.title);

            section.SetVoxelPoint(i % 8, j % 8, k % 8, part);
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

        private void UpdateFromMesh(object stuff)
        {
            WorkData data = (WorkData)stuff;
            Part part = data.part;
            Mesh mesh = data.mesh;
            Matrix4x4 transform = data.transform;

            Vector3[] vertsVoxelSpace = new Vector3[mesh.vertices.Length];
            Bounds meshBounds = new Bounds();
            for (int i = 0; i < vertsVoxelSpace.Length; i++)
            {
                Vector3 vert = transform.MultiplyPoint(mesh.vertices[i]);
                meshBounds.Encapsulate(vert);
                vertsVoxelSpace[i] = vert;
            }

            if (meshBounds.size.x < elementSize && meshBounds.size.y < elementSize && meshBounds.size.z < elementSize)
            {
                CalculateVoxelShellFromTinyMesh(ref meshBounds, ref part);
                return;
            }

            float rc = (float)Math.Sqrt(3) * 0.5f * elementSize;

            for (int a = 0; a < mesh.triangles.Length; a += 3)
            {
                Vector3 vert1, vert2, vert3;

                vert1 = vertsVoxelSpace[mesh.triangles[a]];
                vert2 = vertsVoxelSpace[mesh.triangles[a + 1]];
                vert3 = vertsVoxelSpace[mesh.triangles[a + 2]];

                Bounds triBounds = new Bounds();

                triBounds.Encapsulate(vert1);
                triBounds.Encapsulate(vert2);
                triBounds.Encapsulate(vert3);

                CalculateVoxelShellForTriangle(ref vert1, ref vert2, ref vert3,
                     ref triBounds, ref rc, ref part);
            }

            lock (_locker)
            {
                itemsQueued -= 1;
            }

        }

        private void CalculateVoxelShellFromTinyMesh(ref Bounds meshBounds, ref Part part)
        {
            int lowerI, lowerJ, lowerK;
            int upperI, upperJ, upperK;

            Vector3 min, max;
            min = (meshBounds.min - lowerRightCorner) / elementSize;
            max = (meshBounds.max - lowerRightCorner) / elementSize;

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

        private void CalculateVoxelShellForTriangle(ref Vector3 vert1, ref Vector3 vert2, ref Vector3 vert3,
            ref Bounds triBounds, ref float rc, ref Part part)
        {
            Vector4 plane = CalculateEquationOfPlane(ref vert1, ref vert2, ref vert3);

            //thickness for calculating a 26 neighborhood around the plane
            float t26 = ThicknessForVoxel(ref plane);

            Vector3 lowerBound = (triBounds.min - lowerRightCorner - new Vector3(rc, rc, rc)) / elementSize;
            Vector3 upperBound = (triBounds.max - lowerRightCorner + new Vector3(rc, rc, rc)) / elementSize;

            int lowerI, lowerJ, lowerK;
            int upperI, upperJ, upperK;

            lowerI = (int)Math.Floor(lowerBound.x);
            lowerJ = (int)Math.Floor(lowerBound.y);
            lowerK = (int)Math.Floor(lowerBound.z);

            upperI = (int)Math.Ceiling(upperBound.x);
            upperJ = (int)Math.Ceiling(upperBound.y);
            upperK = (int)Math.Ceiling(upperBound.z);

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
                        Vector3 pt = lowerRightCorner + new Vector3(i, j, k) * elementSize;

                        /*if (CheckAndSetForPlane(ref pt, ref t26, ref plane))
                        {
                            SetVoxelSection(i, j, k, part);
                            continue;
                        }*/

                        if (CheckAndSetForVert(ref pt, ref rc, ref vert1))
                        {
                            SetVoxelSection(i, j, k, part);
                            continue;
                        }
                        if (CheckAndSetForVert(ref pt, ref rc, ref vert2))
                        {
                            SetVoxelSection(i, j, k, part);
                            continue;
                        }
                        if (CheckAndSetForVert(ref pt, ref rc, ref vert3))
                        { 
                            SetVoxelSection(i, j, k, part);
                            continue;
                        }

                        if (CheckAndSetForEdge(ref pt, ref rc, ref vert1, ref vert2))
                        {
                            SetVoxelSection(i, j, k, part);
                            continue;
                        }
                        if (CheckAndSetForEdge(ref pt, ref rc, ref vert2, ref vert3))
                        {
                            SetVoxelSection(i, j, k, part);
                            continue;
                        }
                        if (CheckAndSetForEdge(ref pt, ref rc, ref vert3, ref vert1))
                        {
                            SetVoxelSection(i, j, k, part);
                            continue;
                        }
                    }
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

        private float ThicknessForVoxel(ref Vector4 plane)
        {
            float tmp = 1 / (float)Math.Sqrt(3);

            return Vector3.Dot(new Vector3(plane.x, plane.y, plane.z), new Vector3(Math.Sign(plane.x) * tmp, Math.Sign(plane.y) * tmp, Math.Sign(plane.z) * tmp)) * elementSize * 0.5f * (float)Math.Sqrt(3);
        }

        private bool CheckAndSetForPlane(ref Vector3 pt, ref float t, ref Vector4 plane)
        {
            float result = plane.x * pt.x + plane.y + pt.y + plane.z + pt.z + plane.w;
            result = Math.Abs(result);

            return result <= t;
        }

        private bool CheckAndSetForVert(ref Vector3 pt, ref float rc, ref Vector3 vert)
        {
            return (pt - vert).magnitude <= rc;
        }

        private bool CheckAndSetForEdge(ref Vector3 pt, ref float rc, ref Vector3 vert1, ref Vector3 vert2)
        {
            Vector3 edge = vert2 - vert1;
            float result = Vector3.Exclude(edge, pt - vert1).magnitude;

            return result <= rc;
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
            List<SweepPlanePoint> activeInteriorPts = new List<SweepPlanePoint>();
            List<SweepPlanePoint> inactiveInteriorPts = new List<SweepPlanePoint>();
            SweepPlanePoint[] neighboringSweepPlanePts = new SweepPlanePoint[4];

            for (int j = 0; j < yLength * 8; j++)      //Iterate from front of vehicle to back
            {
                for (int i = 0; i < xLength * 8; i++)    //Iterate across the cross-section plane to add voxel shell and mark active interior points
                    for (int k = 0; k < zLength * 8; k++)
                    {
                        Part p = GetPartAtVoxelPos(i, j, k);
                        SweepPlanePoint pt = sweepPlane[i, k];

                        if (pt == null && p != null)        //If there is a section of voxel there, but no pt, add a new voxel shell pt to the sweep plane
                        {
                            pt = new SweepPlanePoint(p, i, k);
                            continue;
                        }
                        else if (pt != null)
                        {
                            if (p == null)                  //If there is a pt there, but no part listed, this is an interior pt; label it as an active interior
                                if (pt.mark == SweepPlanePoint.MarkingType.VoxelShell)
                                {
                                    pt.mark = SweepPlanePoint.MarkingType.ActiveInterior;
                                    activeInteriorPts.Add(pt);      //And add it to the list of active interior pts
                                }
                        }
                    }

                for(int i = 0; i < activeInteriorPts.Count; i++)
                {
                    SweepPlanePoint activeInteriorPt = activeInteriorPts[i];    //Get active interior pt
                    neighboringSweepPlanePts[0] = sweepPlane[activeInteriorPt.i + 1, activeInteriorPt.k];
                    neighboringSweepPlanePts[1] = sweepPlane[activeInteriorPt.i - 1, activeInteriorPt.k];
                    neighboringSweepPlanePts[2] = sweepPlane[activeInteriorPt.i, activeInteriorPt.k + 1];
                    neighboringSweepPlanePts[3] = sweepPlane[activeInteriorPt.i, activeInteriorPt.k - 1];

                    bool remove = false;
                    for (int k = 0; k < neighboringSweepPlanePts.Length; k++)
                        if (neighboringSweepPlanePts[k] == null)
                        {
                            remove = true;
                            break;
                        }
                    if(remove)
                    {
                        for (int k = 0; k < neighboringSweepPlanePts.Length; k++)
                        {
                            SweepPlanePoint neighbor = neighboringSweepPlanePts[k];
                            if (neighbor != null && neighbor.mark == SweepPlanePoint.MarkingType.VoxelShell)
                            {
                                neighbor.mark = SweepPlanePoint.MarkingType.ActiveInterior;
                                inactiveInteriorPts.Remove(neighbor);
                                activeInteriorPts.Add(neighbor);
                            }
                        }
                        sweepPlane[activeInteriorPt.i, activeInteriorPt.k] = null;
                        SetVoxelSection(activeInteriorPt.i, j, activeInteriorPt.k, null);
                    }
                    else
                    {
                        inactiveInteriorPts.Add(activeInteriorPt);
                    }
                    activeInteriorPts.Remove(activeInteriorPt);
                    i--;
                }

                for(int i = 0; i < inactiveInteriorPts.Count; i++)
                {
                    SweepPlanePoint inactivePt = inactiveInteriorPts[i];
                    SetVoxelSection(inactivePt.i, j, inactivePt.k, inactivePt.part);
                }
            }
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
                ActiveInterior,
                InactiveInterior
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
