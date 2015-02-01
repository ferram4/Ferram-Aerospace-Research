using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry
{
    class VehicleVoxel
    {
        float elementSize;
        VoxelSection[, ,] voxelSections;
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
            elementSize = (float)Math.Pow(elementVol, 1 / 3);

            float tmp = 0.125f / elementSize;

            int xLength, yLength, zLength;
            xLength = (int)Math.Ceiling(vesselBounds.size.x * tmp);
            yLength = (int)Math.Ceiling(vesselBounds.size.y * tmp);
            zLength = (int)Math.Ceiling(vesselBounds.size.z * tmp);

            lowerRightCorner = vesselBounds.min;

            voxelSections = new VoxelSection[xLength, yLength, zLength];

            for(int i = 0; i < geoModules.Count; i++)
            {
                GeometryPartModule m = geoModules[i];
                for (int j = 0; j < m.geometryMeshes.Count; j++)
                    UpdateFromMesh(m.part, m.geometryMeshes[j], m.meshToVesselMatrixList[j]);
            }
        }

        private void SetVoxelSection(int i, int j, int k, Part part)
        {
            int iSec, jSec, kSec;
            //Find the voxel section that this point points to
            iSec = i / 8;
            jSec = j / 8;
            kSec = k / 8;

            VoxelSection section = voxelSections[iSec, jSec, kSec];
            if (section == null)
                section = new VoxelSection(elementSize, 8, 8, 8);

            section.SetVoxelPoint(i - iSec * 8, j - jSec * 8, k - kSec * 8, part);
        }

        private void UpdateFromMesh(Part part, Mesh mesh, Matrix4x4 transform)
        {
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
            float rcSqr = rc * rc;

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
                     ref triBounds, ref rc, ref rcSqr, ref part);
            }
        }

        private void CalculateVoxelShellFromTinyMesh(ref Bounds meshBounds, ref Part part)
        {
            int lowerI, lowerJ, lowerK;
            int upperI, upperJ, upperK;

            lowerI = (int)Math.Floor(meshBounds.min.x);
            lowerJ = (int)Math.Floor(meshBounds.min.y);
            lowerK = (int)Math.Floor(meshBounds.min.z);

            upperI = (int)Math.Ceiling(meshBounds.max.x);
            upperJ = (int)Math.Ceiling(meshBounds.max.y);
            upperK = (int)Math.Ceiling(meshBounds.max.z);

            for (int i = lowerI; i <= upperI; i++)
                for (int j = lowerJ; j <= upperJ; j++)
                    for (int k = lowerK; k <= upperK; j++)
                    {
                        SetVoxelSection(i, j, k, part);
                    }
        }

        private void CalculateVoxelShellForTriangle(ref Vector3 vert1, ref Vector3 vert2, ref Vector3 vert3,
            ref Bounds triBounds, ref float rc, ref float rcSqr, ref Part part)
        {
            Vector4 plane = CalculateEquationOfPlane(ref vert1, ref vert2, ref vert3);

            //thickness for calculating a 26 neighborhood around the plane
            float t26 = ThicknessForVoxel(ref plane);

            Vector3 lowerBound = (triBounds.min - new Vector3(rc, rc, rc)) / elementSize;
            Vector3 upperBound = (triBounds.max + new Vector3(rc, rc, rc)) / elementSize;

            int lowerI, lowerJ, lowerK;
            int upperI, upperJ, upperK;

            lowerI = (int)Math.Floor(lowerBound.x);
            lowerJ = (int)Math.Floor(lowerBound.y);
            lowerK = (int)Math.Floor(lowerBound.z);

            upperI = (int)Math.Ceiling(upperBound.x);
            upperJ = (int)Math.Ceiling(upperBound.y);
            upperK = (int)Math.Ceiling(upperBound.z);

            for (int i = lowerI; i <= upperI; i++)
                for (int j = lowerJ; j <= upperJ; j++)
                    for (int k = lowerK; k <= upperK; j++)
                    {
                        Vector3 pt = lowerRightCorner + new Vector3(i, j, k) * elementSize;

                        if (CheckAndSetForPlane(ref pt, ref t26, ref plane))
                        {
                            SetVoxelSection(i, j, k, part);
                            continue;
                        }

                        if (CheckAndSetForVert(ref pt, ref rcSqr, ref vert1))
                        {
                            SetVoxelSection(i, j, k, part);
                            continue;
                        }
                        if (CheckAndSetForVert(ref pt, ref rcSqr, ref vert2))
                        {
                            SetVoxelSection(i, j, k, part);
                            continue;
                        }
                        if (CheckAndSetForVert(ref pt, ref rcSqr, ref vert3))
                        { 
                            SetVoxelSection(i, j, k, part);
                            continue;
                        }

                        if (CheckAndSetForEdge(ref pt, ref rcSqr, ref vert1, ref vert2))
                        {
                            SetVoxelSection(i, j, k, part);
                            continue;
                        }
                        if (CheckAndSetForEdge(ref pt, ref rcSqr, ref vert2, ref vert3))
                        {
                            SetVoxelSection(i, j, k, part);
                            continue;
                        }
                        if (CheckAndSetForEdge(ref pt, ref rcSqr, ref vert3, ref vert1))
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

            Vector4 result = Vector3.Cross(p1p2, p1p3).normalized;

            result.w = -(pt1.x * result.x + pt1.y * result.y + pt1.z * result.z);

            return result;
        }

        private float ThicknessForVoxel(ref Vector4 plane)
        {
            float tmp = 1 / (float)Math.Sqrt(3);

            return Vector3.Dot(new Vector3(Math.Abs(plane.x), Math.Abs(plane.y), Math.Abs(plane.z)), new Vector3(tmp, tmp, tmp)) * elementSize * 0.5f * tmp;
        }

        private bool CheckAndSetForPlane(ref Vector3 pt, ref float t, ref Vector4 plane)
        {
            float result = plane.x * pt.x + plane.y + pt.y + plane.z + pt.z + plane.w;
            result = Math.Abs(result);

            return result <= t;
        }

        private bool CheckAndSetForVert(ref Vector3 pt, ref float rcSqr, ref Vector3 vert)
        {
            return (pt - vert).sqrMagnitude > rcSqr;
        }

        private bool CheckAndSetForEdge(ref Vector3 pt, ref float rcSqr, ref Vector3 vert1, ref Vector3 vert2)
        {
            Vector3 edge = vert2 - vert1;
            float result = Vector3.Exclude(edge, pt - vert1).sqrMagnitude;

            return result <= rcSqr;
        }
    }
}
