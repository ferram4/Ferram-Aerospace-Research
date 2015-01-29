using System;
using System.Collections.Generic;
using UnityEngine;
using FerramAerospaceResearch.FARModule;

namespace FerramAerospaceResearch.FARPartGeoUtil
{
    class VoxelSection
    {
        protected Part[, ,] voxelPoints = null;

        float size;
        Vector3 upperCorner, lowerCorner;

        int xLength, yLength, zLength;

        public VoxelSection(float size, int xLength, int yLength, int zLength)
        {
            this.size = size;
            this.xLength = xLength;
            this.yLength = yLength;
            this.zLength = zLength;
        }

        public void RebuildScaling(float size, Vector3 lowerCorner, Vector3 upperCorner)
        {
            this.size = size;
            this.lowerCorner = lowerCorner;
            this.upperCorner = upperCorner;
        }

        public void RebuildVoxel(List<FARGeometryModule> geoModules, Matrix4x4 vesselTransform)
        {
            voxelPoints = new Part[xLength, yLength, zLength];

            for(int i = 0; i < geoModules.Count; i++)
            {
                FARGeometryModule geoModule = geoModules[i];

                List<Mesh> meshList = geoModule.geometryMeshes;

                for(int j = 0; j < meshList.Count; j++)
                {
                    Matrix4x4 matrix = vesselTransform * geoModule.meshToWorldMatrixList[i];
                    UpdateFromMesh(geoModule.part, meshList[i], matrix);
                }
            }
        }

        private void UpdateFromMesh(Part part, Mesh mesh, Matrix4x4 transform)
        {
            Vector3[] vertsVoxelSpace = new Vector3[mesh.vertices.Length];
            Bounds meshBounds = new Bounds();
            for(int i = 0; i < vertsVoxelSpace.Length; i++)
            {
                Vector3 vert = transform.MultiplyPoint(mesh.vertices[i]);
                meshBounds.Encapsulate(vert);
                vertsVoxelSpace[i] = vert;
            }

            if(meshBounds.size.x < size && meshBounds.size.y < size && meshBounds.size.z < size)
            {
                CalculateVoxelShellFromTinyMesh(ref meshBounds, ref part);
                return;
            }

            float rc = (float)Math.Sqrt(3) * 0.5f * size;
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
                        voxelPoints[i, j, k] = part;
                    }
        }

        private void CalculateVoxelShellForTriangle(ref Vector3 vert1, ref Vector3 vert2, ref Vector3 vert3,
            ref Bounds triBounds, ref float rc, ref float rcSqr, ref Part part)
        {
            Vector4 plane = CalculateEquationOfPlane(ref vert1, ref vert2, ref vert3);

            //thickness for calculating a 26 neighborhood around the plane
            float t26 = ThicknessForVoxel(ref plane);

            Vector3 lowerBound = (triBounds.min - new Vector3(rc, rc, rc)) / size;
            Vector3 upperBound = (triBounds.max + new Vector3(rc, rc, rc)) / size;

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
                        Vector3 pt = lowerCorner + new Vector3(i, j, k) * size;
                        bool voxelPt = CheckAndSetForPlane(ref pt, ref t26, ref plane);

                        voxelPt |= CheckAndSetForVert(ref pt, ref rcSqr, ref vert1);
                        voxelPt |= CheckAndSetForVert(ref pt, ref rcSqr, ref vert2);
                        voxelPt |= CheckAndSetForVert(ref pt, ref rcSqr, ref vert3);

                        voxelPt |= CheckAndSetForEdge(ref pt, ref rcSqr, ref vert1, ref vert2);
                        voxelPt |= CheckAndSetForEdge(ref pt, ref rcSqr, ref vert2, ref vert3);
                        voxelPt |= CheckAndSetForEdge(ref pt, ref rcSqr, ref vert3, ref vert1);

                        if(voxelPt)
                            voxelPoints[i, j, k] = part; 
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

            return Vector3.Dot(new Vector3(Math.Abs(plane.x), Math.Abs(plane.y), Math.Abs(plane.z)), new Vector3(tmp, tmp, tmp)) * size * 0.5f * tmp;
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
