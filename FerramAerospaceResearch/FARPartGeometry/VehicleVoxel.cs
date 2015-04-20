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
using System.Threading;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry
{
    public class VehicleVoxel
    {
        double elementSize;
        double invElementSize;

        VoxelChunk[, ,] voxelChunks;
        int xLength, yLength, zLength;
        int xCellLength, yCellLength, zCellLength;
        int threadsQueued = 0;
        object _locker = new object();

        Vector3d lowerRightCorner;
        const double RC = 0.5f;

        public VoxelCrossSection[] EmptyCrossSectionArray
        {
            get
            {
                VoxelCrossSection[] array = new VoxelCrossSection[MaxArrayLength];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i].partSideAreaValues = new Dictionary<Part, VoxelCrossSection.SideAreaValues>();
                }
                return array;
            }
        }

        public int MaxArrayLength
        {
            get { return yCellLength + xCellLength + zCellLength; }
        }

        public VehicleVoxel(List<Part> partList, int elementCount, bool multiThreaded, bool solidify)
        {
            Vector3d min = new Vector3d(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
            Vector3d max = new Vector3d(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity);

            List<GeometryPartModule> geoModules = new List<GeometryPartModule>();
            for(int i = 0; i < partList.Count; i++)
            {
                Part p = partList[i];
                GeometryPartModule m = p.GetComponent<GeometryPartModule>();
                if ((object)m != null)
                {
                    Vector3d minBounds = m.overallMeshBounds.min;
                    Vector3d maxBounds = m.overallMeshBounds.max;

                    min.x = Math.Min(min.x, minBounds.x);
                    min.y = Math.Min(min.y, minBounds.y);
                    min.z = Math.Min(min.z, minBounds.z);

                    max.x = Math.Max(max.x, maxBounds.x);
                    max.y = Math.Max(max.y, maxBounds.y);
                    max.z = Math.Max(max.z, maxBounds.z);

                    geoModules.Add(m);
                }
            }

            Vector3d size = max - min;

            double voxelVolume = size.x * size.y * size.z;

            if (double.IsInfinity(voxelVolume))
                return;

            double elementVol = voxelVolume / elementCount;
            elementSize = Math.Pow(elementVol, 1d / 3d);
            invElementSize = 1 / elementSize;

            double tmp = 0.125 * invElementSize;

            xLength = (int)Math.Ceiling(size.x * tmp) + 1;
            yLength = (int)Math.Ceiling(size.y * tmp) + 1;
            zLength = (int)Math.Ceiling(size.z * tmp) + 1;

            xCellLength = xLength * 8;
            yCellLength = yLength * 8;
            zCellLength = zLength * 8;

            //Debug.Log(elementSize);
            //Debug.Log(xLength + " " + yLength + " " + zLength);
            //Debug.Log(size);

            Vector3d extents = new Vector3d(); //this will be the distance from the center to the edges of the voxel object
            extents.x = xLength * 4 * elementSize;
            extents.y = yLength * 4 * elementSize;
            extents.z = zLength * 4 * elementSize;

            Vector3d center = (max + min) * 0.5f;    //Center of the vessel

            lowerRightCorner = center - extents;    //This places the center of the voxel at the center of the vehicle to achieve maximum symmetry

            voxelChunks = new VoxelChunk[xLength, yLength, zLength];

            threadsQueued = 0;

            for (int i = 0; i < geoModules.Count; i++)
                threadsQueued += geoModules[i].meshDataList.Count;      //Doing this out here allows us to get rid of the lock, which should reduce sync costs for many meshes

            for(int i = 0; i < geoModules.Count; i++)
            {
                GeometryPartModule m = geoModules[i];
                for (int j = 0; j < m.meshDataList.Count; j++)
                {
                    if (multiThreaded)
                    {
                        VoxelShellParams data = new VoxelShellParams(m.part, m.meshDataList[j]);
                        ThreadPool.QueueUserWorkItem(UpdateFromMesh, data);
                    }
                    else
                        UpdateFromMesh(m.meshDataList[j], m.part);
                }
                
            }
            if(multiThreaded)
                lock (_locker)
                    while (threadsQueued > 0)
                        Monitor.Wait(_locker);
                
            threadsQueued = 2;

            if (multiThreaded)
            {
                VoxelSolidParams data = new VoxelSolidParams(0, (yLength / 2) * 8, true);
                ThreadPool.QueueUserWorkItem(SolidifyVoxel, data);
                data = new VoxelSolidParams((yLength / 2) * 8, yCellLength, false);
                ThreadPool.QueueUserWorkItem(SolidifyVoxel, data);
            }
            else
                SolidifyVoxel(0, yCellLength, false);

            if (multiThreaded)
                lock (_locker)
                    while (threadsQueued > 0)
                        Monitor.Wait(_locker);
        }

        ~VehicleVoxel()
        {
            ClearVisualVoxels();
        }

        public unsafe void CrossSectionData(VoxelCrossSection[] crossSections, Vector3 orientationVector, out int frontIndex, out int backIndex, out double sectionThickness, out double maxCrossSectionArea)
        {
            //TODO: Look into setting better limits for iterating over sweep plane to improve off-axis performance

            double wInc;
            Vector4d plane = CalculateEquationOfSweepPlane(orientationVector, out wInc);

            double x, y, z;
            x = Math.Abs(plane.x);
            y = Math.Abs(plane.y);
            z = Math.Abs(plane.z);

            double elementArea = elementSize * elementSize;

            bool frontIndexFound = false;
            frontIndex = 0;
            backIndex = crossSections.Length - 1;

            sectionThickness = elementSize;

            Matrix4x4 sectionNormalToVesselCoords = Matrix4x4.TRS(Vector3.zero, Quaternion.FromToRotation(new Vector3(0, 0, 1), orientationVector), Vector3.one);
            Matrix4x4 vesselToSectionNormal = sectionNormalToVesselCoords.inverse;

            //Code has multiple optimizations to take advantage of the limited rnage of values that are included.  They are listed below
            //(int)Math.Ceiling(x) -> (int)(x + 1)      for x > 0
            //(int)Math.Round(x) -> (int)(x + 0.5f)     for x > 0
            //(int)Math.Floor(x) -> (int)(x)            for x > 0

            //Check y first, since it is most likely to be the flow direction
            if (y >= z && y >= x)
            {
                int sectionCount = yCellLength + (int)(xCellLength * x / y + 1) + (int)(zCellLength * z / y + 1);
                sectionCount = Math.Min(sectionCount, crossSections.Length);
                double angleSizeIncreaseFactor = Math.Sqrt((x + y + z) / y);
                elementArea *= angleSizeIncreaseFactor;       //account for different angles effects on voxel cube's projected area

                double invMag = 1 / Math.Sqrt(x * x + y * y + z * z);

                sectionThickness *= y * invMag;

                double xNorm, yNorm, zNorm;
                xNorm = plane.x * invMag;
                yNorm = plane.y * invMag;
                zNorm = plane.z * invMag;

                double yAbsNorm = Math.Abs(yNorm);

                int xSectArrayLength = (int)(xCellLength * yAbsNorm + yCellLength * Math.Abs(xNorm) + 1);
                //Stack allocation of array allows removal of garbage collection issues

                //bool* sectionArray = stackalloc bool[xSectArrayLength * (int)(zCellLength * yAbsNorm + yCellLength * Math.Abs(zNorm) + 1)];

                //bool[,] sectionRepresentation = new bool[(int)Math.Ceiling(xCellLength * Math.Abs(yNorm) + yCellLength * Math.Abs(xNorm)), (int)Math.Ceiling(zCellLength * Math.Abs(yNorm) + yCellLength * Math.Abs(zNorm))];

                double invYPlane = 1 / plane.y;

                plane.x *= invYPlane;
                plane.z *= invYPlane;
                plane.w *= invYPlane;

                wInc *= invYPlane;

                plane.w -= 0.5;     //shift the plane to auto-account for midpoint rounding, allowing it to be simple casts to int

                for (int m = 0; m < sectionCount; m++)
                {
                    int areaCount = 0;
                    //Vector3d centroid = Vector3d.zero;
                    int centx, centy, centz;
                    centx = centy = centz = 0;

                    //int unshadowedAreaCount = 0;
                    //Vector3d unshadowedCentroid = Vector3d.zero;
                    //int unshadowedCentx, unshadowedCenty, unshadowedCentz;
                    //unshadowedCentx = unshadowedCenty = unshadowedCentz = 0;

                    double i_xx = 0, i_xy = 0, i_yy = 0;
                    Dictionary<Part, VoxelCrossSection.SideAreaValues> partSideAreas = crossSections[m].partSideAreaValues;
                    partSideAreas.Clear();

                    for (int iOverall = 0; iOverall < xCellLength; iOverall += 8)     //Overall ones iterate over the actual voxel indices (to make use of the equation of the plane) but are used to get chunk indices
                        for (int kOverall = 0; kOverall < zCellLength; kOverall += 8)
                        {

                            int jSect1, jSect2, jSect3;

                            if (plane.x * plane.z > 0.0)        //Determine high and low points on this quad of the plane
                            {
                                jSect1 = (int)(-(plane.x * iOverall + plane.z * kOverall + plane.w));             //End points of the plane
                                jSect3 = (int)(-(plane.x * (iOverall + 7) + plane.z * (kOverall + 7) + plane.w));
                            }
                            else
                            {
                                jSect1 = (int)(-(plane.x * (iOverall + 7) + plane.z * kOverall + plane.w));
                                jSect3 = (int)(-(plane.x * iOverall + plane.z * (kOverall + 7) + plane.w));
                            }
                            jSect2 = (int)((jSect1 + jSect3) * 0.5);     //Central point

                            int jMin = Math.Min(jSect1, jSect3);
                            int jMax = Math.Max(jSect1, jSect3) + 1;

                            jSect1 = jMin >> 3;
                            jSect2 = jSect2 >> 3;
                            jSect3 = (jMax - 1) >> 3;

                            if (jSect1 >= yLength)  //if the smallest sect is above the limit, they all are
                                continue;

                            if (jSect3 < 0)         //if the largest sect is below the limit, they all are
                                continue;


                            if (jSect1 == jSect3)        //If chunk indices are identical, only get that one and it's very simple; only need to check 1 and 3, because if any are different, it must be those
                            {
                                if (jSect1 < 0)
                                    continue;

                                VoxelChunk sect = voxelChunks[iOverall >> 3, jSect1, kOverall >> 3];

                                if (sect == null)
                                    continue;

                                for (int i = iOverall; i < iOverall + 8; i++)            //Finally, iterate over the chunk
                                {
                                    double tmp = plane.x * i + plane.w;
                                    for (int k = kOverall; k < kOverall + 8; k++)
                                    {
                                        int j = (int)(-(plane.z * k + tmp));

                                        if (j < jMin || j > jMax)
                                            continue;

                                        int index = i + 8 * j + 64 * k;

                                        Part p = sect.GetVoxelPartGlobalIndex(index);

                                        if ((object)p != null)
                                        {
                                            areaCount++;
                                            centx += i;
                                            centy += j;
                                            centz += k;

                                            Vector3 location = vesselToSectionNormal.MultiplyVector(new Vector3(i, j, k));
                                            i_xx += location.x * location.x;
                                            i_xy += location.x * location.y;
                                            i_yy += location.y * location.y;

                                            DetermineIfPartGetsForcesAndAreas(partSideAreas, p, i, j, k);
                                        }
                                    }
                                }
                            }
                            else                       //Two or three different indices requires separate handling
                            {
                                VoxelChunk sect1 = null, sect2 = null, sect3 = null;

                                int iSect = iOverall >> 3;
                                int kSect = kOverall >> 3;
                                bool validSects = false;
                                if (!(jSect1 < 0))      //this block ensures that there are sections here to check
                                {
                                    sect1 = voxelChunks[iSect, jSect1, kSect];
                                    if (sect1 != null)
                                        validSects = true;
                                }
                                if (!(jSect2 < 0 || jSect2 >= yLength))
                                {
                                    sect2 = voxelChunks[iSect, jSect2, kSect];
                                    if (sect2 != null && sect2 != sect1)
                                        validSects |= true;
                                }
                                if (!(jSect3 >= yLength))
                                {
                                    sect3 = voxelChunks[iSect, jSect3, kSect];
                                    if (sect3 != null && (sect3 != sect2 && sect3 != sect1))
                                        validSects |= true;
                                }

                                if (!validSects)
                                    continue;

                                for (int i = iOverall; i < iOverall + 8; i++)
                                {
                                    double tmp = plane.x * i + plane.w;
                                    for (int k = kOverall; k < kOverall + 8; k++)
                                    {
                                        int j = (int)(-(plane.z * k + tmp));

                                        if (j < jMin || j > jMax)
                                            continue;

                                        int index = i + 8 * j + 64 * k;

                                        Part p = null;
                                        int jSect = j >> 3;
                                        if (jSect == jSect1 && sect1 != null)
                                            p = sect1.GetVoxelPartGlobalIndex(index);
                                        else if (jSect == jSect2 && sect2 != null)
                                            p = sect2.GetVoxelPartGlobalIndex(index);
                                        else if (jSect == jSect3 && sect3 != null)
                                            p = sect3.GetVoxelPartGlobalIndex(index);

                                        if ((object)p != null)
                                        {
                                            areaCount++;
                                            centx += i;
                                            centy += j;
                                            centz += k;

                                            Vector3 location = vesselToSectionNormal.MultiplyVector(new Vector3(i, j, k));
                                            i_xx += location.x * location.x;
                                            i_xy += location.x * location.y;
                                            i_yy += location.y * location.y;

                                            DetermineIfPartGetsForcesAndAreas(partSideAreas, p, i, j, k);
                                        }
                                    }
                                }
                            }
                        }
                    Vector3d centroid = new Vector3d(centx, centy, centz);
                    //Vector3d unshadowedCentroid = new Vector3d(unshadowedCentx, unshadowedCenty, unshadowedCentz);
                    if (areaCount > 0)
                    {
                        if (frontIndexFound)
                            backIndex = m;
                        else
                        {
                            frontIndexFound = true;
                            frontIndex = m;
                        }
                        centroid /= areaCount;
                        /*if (unshadowedAreaCount != 0)
                            unshadowedCentroid /= unshadowedAreaCount;
                        else
                            unshadowedCentroid = centroid;*/
                    }
                    Vector3 localCentroid = vesselToSectionNormal.MultiplyVector(centroid);
                    i_xx -= areaCount * localCentroid.x * localCentroid.x;
                    i_xy -= areaCount * localCentroid.x * localCentroid.y;
                    i_yy -= areaCount * localCentroid.y * localCentroid.y;

                    double tanPrinAngle = TanPrincipalAxisAngle(i_xx, i_yy, i_xy);
                    Vector3 axis1 = new Vector3(1, 0, 0), axis2 = new Vector3(0, 0, 1);
                    double flatnessRatio = 1;

                    if (tanPrinAngle != 0)
                    {
                        axis1 = new Vector3(1, 0, (float)tanPrinAngle);
                        axis1.Normalize();
                        axis2 = new Vector3(axis1.z, 0, -axis1.x);

                        flatnessRatio = i_xy * axis2.z / axis2.x + i_xx;
                        flatnessRatio = (i_xy * tanPrinAngle + i_xx) / flatnessRatio;
                        flatnessRatio = Math.Sqrt(Math.Sqrt(flatnessRatio));
                    }
                    if (double.IsNaN(flatnessRatio))
                        flatnessRatio = 1;

                    Vector3 principalAxis;
                    if (flatnessRatio > 1)
                        principalAxis = axis1;
                    else
                    {
                        flatnessRatio = 1 / flatnessRatio;
                        principalAxis = axis2;
                    }
                    if (flatnessRatio > 10)
                        flatnessRatio = 10;

                    principalAxis = sectionNormalToVesselCoords.MultiplyVector(principalAxis);

                    crossSections[m].centroid = centroid * elementSize + lowerRightCorner;

                    crossSections[m].area = areaCount * elementArea;
                    crossSections[m].flatnessRatio = flatnessRatio;
                    crossSections[m].flatNormalVector = principalAxis;

                    plane.w += wInc;
                }
            }
            else if (x > y && x > z)
            {
                int sectionCount = xCellLength + (int)(yCellLength * y / x + 1) + (int)(zCellLength * z / x + 1);
                sectionCount = Math.Min(sectionCount, crossSections.Length);
                double angleSizeIncreaseFactor = Math.Sqrt((x + y + z) / x);
                elementArea *= angleSizeIncreaseFactor;       //account for different angles effects on voxel cube's projected area

                double invMag = 1 / Math.Sqrt(x * x + y * y + z * z);

                sectionThickness *= x * invMag;

                double xNorm, yNorm, zNorm;
                xNorm = plane.x * invMag;
                yNorm = plane.y * invMag;
                zNorm = plane.z * invMag;

                double xAbsNorm = Math.Abs(xNorm);

                double i_xx = 0, i_xy = 0, i_yy = 0;

                int ySectArrayLength = (int)(xCellLength * Math.Abs(yNorm) + yCellLength * xAbsNorm + 1);
                //Stack allocation of array allows removal of garbage collection issues
                //bool* sectionArray = stackalloc bool[ySectArrayLength * (int)(zCellLength * xAbsNorm + xCellLength * Math.Abs(zNorm) + 1)];

                //bool[,] sectionRepresentation = new bool[(int)Math.Ceiling(xCellLength * Math.Abs(yNorm) + yCellLength * Math.Abs(xNorm)), (int)Math.Ceiling(zCellLength * Math.Abs(xNorm) + xCellLength * Math.Abs(zNorm))];

                double invXPlane = 1 / plane.x;

                plane.y *= invXPlane;
                plane.z *= invXPlane;
                plane.w *= invXPlane;

                wInc *= invXPlane;

                for (int m = 0; m < sectionCount; m++)
                {
                    int areaCount = 0;
                    //Vector3d centroid = Vector3d.zero;
                    int centx, centy, centz;
                    centx = centy = centz = 0;

                    //int unshadowedAreaCount = 0;
                    //Vector3d unshadowedCentroid = Vector3d.zero;
                    //int unshadowedCentx, unshadowedCenty, unshadowedCentz;
                    //unshadowedCentx = unshadowedCenty = unshadowedCentz = 0;

                    Dictionary<Part, VoxelCrossSection.SideAreaValues> partSideAreas = crossSections[m].partSideAreaValues;
                    partSideAreas.Clear();

                    for (int jOverall = 0; jOverall < yCellLength; jOverall += 8)
                        for (int kOverall = 0; kOverall < zCellLength; kOverall += 8)
                        {
                            int iSect1, iSect2, iSect3;

                            if (plane.y * plane.z > 0)
                            {
                                iSect1 = (int)(-(plane.y * jOverall + plane.z * kOverall + plane.w) + 0.5);
                                iSect3 = (int)(-(plane.y * (jOverall + 7) + plane.z * (kOverall + 7) + plane.w) + 0.5);
                            }
                            else
                            {
                                iSect1 = (int)(-(plane.y * (jOverall + 7) + plane.z * kOverall + plane.w) + 0.5);
                                iSect3 = (int)(-(plane.y * jOverall + plane.z * (kOverall + 7) + plane.w) + 0.5);
                            }
                            iSect2 = (int)((iSect1 + iSect3) * 0.5);

                            int iMin = Math.Min(iSect1, iSect3);
                            int iMax = Math.Max(iSect1, iSect3) + 1;

                            iSect1 = iMin >> 3;
                            iSect2 = iSect2 >> 3;
                            iSect3 = (iMax - 1) >> 3;

                            if (iSect1 >= xLength)  //if the smallest sect is above the limit, they all are
                                continue;

                            if (iSect3 < 0)         //if the largest sect is below the limit, they all are
                                continue;

                            if (iSect1 == iSect3)
                            {
                                if (iSect1 < 0)
                                    continue;

                                VoxelChunk sect = voxelChunks[iSect1, jOverall >> 3, kOverall >> 3];

                                if (sect == null)
                                    continue;

                                for (int j = jOverall; j < jOverall + 8; j++)
                                {
                                    double tmp = plane.y * j + plane.w;
                                    for (int k = kOverall; k < kOverall + 8; k++)
                                    {
                                        int i = (int)(-(plane.z * k + tmp) + 0.5);

                                        if (i < iMin || i > iMax)
                                            continue;

                                        int index = i + 8 * j + 64 * k;

                                        Part p = sect.GetVoxelPartGlobalIndex(index);

                                        if ((object)p != null)
                                        {
                                            areaCount++;
                                            centx += i;
                                            centy += j;
                                            centz += k;

                                            Vector3 location = vesselToSectionNormal.MultiplyVector(new Vector3(i, j, k));
                                            i_xx += location.x * location.x;
                                            i_xy += location.x * location.y;
                                            i_yy += location.y * location.y;

                                            DetermineIfPartGetsForcesAndAreas(partSideAreas, p, i, j, k);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                VoxelChunk sect1 = null, sect2 = null, sect3 = null;

                                int jSect = jOverall >> 3;
                                int kSect = kOverall >> 3;

                                bool validSects = false;
                                if (!(iSect1 < 0))
                                {
                                    sect1 = voxelChunks[iSect1, jSect, kSect];
                                    if (sect1 != null)
                                        validSects = true;
                                }
                                if (!(iSect2 < 0 || iSect2 >= xLength))
                                {
                                    sect2 = voxelChunks[iSect2, jSect, kSect];
                                    if (sect2 != null && sect2 != sect1)
                                        validSects |= true;
                                }

                                if (!(iSect3 >= xLength))
                                {
                                    sect3 = voxelChunks[iSect3, jSect, kSect];
                                    if (sect3 != null && (sect3 != sect2 && sect3 != sect1))
                                        validSects |= true;
                                }

                                if (!validSects)
                                    continue;

                                for (int j = jOverall; j < jOverall + 8; j++)
                                {
                                    double tmp = plane.y * j + plane.w;
                                    for (int k = kOverall; k < kOverall + 8; k++)
                                    {
                                        int i = (int)(-(plane.z * k + tmp) + 0.5);

                                        if (i < iMin || i > iMax)
                                            continue;

                                        int index = i + 8 * j + 64 * k;

                                        Part p = null;
                                        int iSect = i >> 3;
                                        if (iSect == iSect1 && sect1 != null)
                                            p = sect1.GetVoxelPartGlobalIndex(index);
                                        else if (iSect == iSect2 && sect2 != null)
                                            p = sect2.GetVoxelPartGlobalIndex(index);
                                        else if (iSect == iSect3 && sect3 != null)
                                            p = sect3.GetVoxelPartGlobalIndex(index);

                                        if ((object)p != null)
                                        {
                                            areaCount++;
                                            centx += i;
                                            centy += j;
                                            centz += k;

                                            Vector3 location = vesselToSectionNormal.MultiplyVector(new Vector3(i, j, k));
                                            i_xx += location.x * location.x;
                                            i_xy += location.x * location.y;
                                            i_yy += location.y * location.y;

                                            DetermineIfPartGetsForcesAndAreas(partSideAreas, p, i, j, k);
                                        }
                                    }
                                }
                            }
                        }
                    Vector3d centroid = new Vector3d(centx, centy, centz);
                    //Vector3d unshadowedCentroid = new Vector3d(unshadowedCentx, unshadowedCenty, unshadowedCentz);
                    if (areaCount > 0)
                    {
                        if (frontIndexFound)
                            backIndex = m;
                        else
                        {
                            frontIndexFound = true;
                            frontIndex = m;
                        }
                        centroid /= areaCount;
                        /*if (unshadowedAreaCount != 0)
                            unshadowedCentroid /= unshadowedAreaCount;
                        else
                            unshadowedCentroid = centroid;*/
                    }
                    Vector3 localCentroid = vesselToSectionNormal.MultiplyVector(centroid);
                    i_xx -= areaCount * localCentroid.x * localCentroid.x;
                    i_xy -= areaCount * localCentroid.x * localCentroid.y;
                    i_yy -= areaCount * localCentroid.y * localCentroid.y;

                    double tanPrinAngle = TanPrincipalAxisAngle(i_xx, i_yy, i_xy);
                    Vector3 axis1 = new Vector3(1, 0, 0), axis2 = new Vector3(0, 0, 1);
                    double flatnessRatio = 1;

                    if (tanPrinAngle != 0)
                    {
                        axis1 = new Vector3(1, 0, (float)tanPrinAngle);
                        axis1.Normalize();
                        axis2 = new Vector3(axis1.z, 0, -axis1.x);

                        flatnessRatio = i_xy * axis2.z / axis2.x + i_xx;
                        flatnessRatio = (i_xy * tanPrinAngle + i_xx) / flatnessRatio;
                        flatnessRatio = Math.Sqrt(Math.Sqrt(flatnessRatio));
                    }
                    if (double.IsNaN(flatnessRatio))
                        flatnessRatio = 1;

                    Vector3 principalAxis;
                    if (flatnessRatio > 1)
                        principalAxis = axis1;
                    else
                    {
                        flatnessRatio = 1 / flatnessRatio;
                        principalAxis = axis2;
                    }
                    if (flatnessRatio > 10)
                        flatnessRatio = 10;

                    principalAxis = sectionNormalToVesselCoords.MultiplyVector(principalAxis);

                    crossSections[m].centroid = centroid * elementSize + lowerRightCorner;
                    //crossSections[m].additonalUnshadowedCentroid = unshadowedCentroid * elementSize + lowerRightCorner;

                    crossSections[m].area = areaCount * elementArea;
                    crossSections[m].flatnessRatio = flatnessRatio;
                    crossSections[m].flatNormalVector = principalAxis;
                    //crossSections[m].additionalUnshadowedArea = unshadowedAreaCount * elementArea;

                    plane.w += wInc;
                }
            }
            else
            {
                int sectionCount = zCellLength + (int)(xCellLength * x / z + 1) + (int)(yCellLength * y / z + 1);
                sectionCount = Math.Min(sectionCount, crossSections.Length);
                double angleSizeIncreaseFactor = Math.Sqrt((x + y + z) / z);       //account for different angles effects on voxel cube's projected area
                elementArea *= angleSizeIncreaseFactor;       //account for different angles effects on voxel cube's projected area

                double invMag = 1 / Math.Sqrt(x * x + y * y + z * z);

                sectionThickness *= z * invMag;

                double xNorm, yNorm, zNorm;
                xNorm = plane.x * invMag;
                yNorm = plane.y * invMag;
                zNorm = plane.z * invMag;

                double zAbsNorm = Math.Abs(zNorm);

                double i_xx = 0, i_xy = 0, i_yy = 0;

                int xSectArrayLength = (int)(xCellLength * zAbsNorm + zCellLength * Math.Abs(xNorm) + 1);
                //Stack allocation of array allows removal of garbage collection issues
                //bool* sectionArray = stackalloc bool[xSectArrayLength * (int)(yCellLength * zAbsNorm + zCellLength * Math.Abs(yNorm) + 1)];

                //bool[,] sectionRepresentation = new bool[(int)Math.Ceiling(xCellLength * Math.Abs(zNorm) + zCellLength * Math.Abs(xNorm)), (int)Math.Ceiling(yCellLength * Math.Abs(zNorm) + zCellLength * Math.Abs(yNorm))];

                double invZPlane = 1 / plane.z;

                plane.y *= invZPlane;
                plane.x *= invZPlane;
                plane.w *= invZPlane;

                wInc *= invZPlane;

                for (int m = 0; m < sectionCount; m++)
                {
                    int areaCount = 0;
                    //Vector3d centroid = Vector3d.zero;
                    int centx, centy, centz;
                    centx = centy = centz = 0;

                    //int unshadowedAreaCount = 0;
                    //Vector3d unshadowedCentroid = Vector3d.zero;
                    //int unshadowedCentx, unshadowedCenty, unshadowedCentz;
                    //unshadowedCentx = unshadowedCenty = unshadowedCentz = 0;

                    Dictionary<Part, VoxelCrossSection.SideAreaValues> partSideAreas = crossSections[m].partSideAreaValues;
                    partSideAreas.Clear();

                    for (int iOverall = 0; iOverall < xCellLength; iOverall += 8)     //Overall ones iterate over the actual voxel indices (to make use of the equation of the plane) but are used to get chunk indices
                        for (int jOverall = 0; jOverall < yCellLength; jOverall += 8)
                        {
                            int kSect1, kSect2, kSect3;

                            if (plane.x * plane.y > 0)        //Determine high and low points on this quad of the plane
                            {
                                kSect1 = (int)(-(plane.x * iOverall + plane.y * jOverall + plane.w) + 0.5);
                                kSect3 = (int)(-(plane.x * (iOverall + 7) + plane.y * (jOverall + 7) + plane.w) + 0.5);
                            }
                            else
                            {
                                kSect1 = (int)(-(plane.x * (iOverall + 7) + plane.y * jOverall + plane.w) + 0.5);
                                kSect3 = (int)(-(plane.x * iOverall + plane.y * (jOverall + 7) + plane.w) + 0.5);
                            }
                            kSect2 = (int)((kSect1 + kSect3) * 0.5);

                            int kMin = Math.Min(kSect1, kSect3);
                            int kMax = Math.Max(kSect1, kSect3) + 1;

                            kSect1 = kMin >> 3;
                            kSect2 = kSect2 >> 3;
                            kSect3 = (kMax - 1) >> 3;

                            if (kSect1 >= zLength)  //if the smallest sect is above the limit, they all are
                                continue;

                            if (kSect3 < 0)         //if the largest sect is below the limit, they all are
                                continue;


                            if (kSect1 == kSect3)        //If chunk indices are identical, only get that one and it's very simple
                            {
                                if (kSect1 < 0)
                                    continue;

                                VoxelChunk sect = voxelChunks[iOverall >> 3, jOverall >> 3, kSect1];

                                if (sect == null)
                                    continue;


                                for (int i = iOverall; i < iOverall + 8; i++)            //Finally, iterate over the chunk
                                {
                                    double tmp = plane.x * i + plane.w;
                                    for (int j = jOverall; j < jOverall + 8; j++)
                                    {
                                        int k = (int)(-(plane.y * j + tmp) + 0.5);


                                        if (k < kMin || k > kMax)
                                            continue;

                                        int index = i + 8 * j + 64 * k;

                                        Part p = sect.GetVoxelPartGlobalIndex(index);

                                        if ((object)p != null)
                                        {
                                            areaCount++;
                                            centx += i;
                                            centy += j;
                                            centz += k;

                                            Vector3 location = vesselToSectionNormal.MultiplyVector(new Vector3(i, j, k));
                                            i_xx += location.x * location.x;
                                            i_xy += location.x * location.y;
                                            i_yy += location.y * location.y;

                                            DetermineIfPartGetsForcesAndAreas(partSideAreas, p, i, j, k);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                VoxelChunk sect1 = null, sect2 = null, sect3 = null;

                                int iSect = iOverall >> 3;
                                int jSect = jOverall >> 3;

                                bool validSects = false;
                                if (!(kSect1 < 0))         //If indices are different, this section of the plane crosses two chunks
                                {
                                    sect1 = voxelChunks[iSect, jSect, kSect1];
                                    if (sect1 != null)
                                        validSects = true;
                                }
                                if (!(kSect2 < 0 || kSect2 >= zLength))
                                {
                                    sect2 = voxelChunks[iSect, jSect, kSect2];
                                    if (sect2 != null && sect2 != sect1)
                                        validSects |= true;
                                }
                                if (!(kSect3 >= zLength))
                                {
                                    sect3 = voxelChunks[iSect, jSect, kSect3];
                                    if (sect3 != null && (sect3 != sect2 && sect3 != sect1))
                                        validSects |= true;
                                }

                                if (!validSects)
                                    continue;

                                for (int i = iOverall; i < iOverall + 8; i++)
                                {
                                    double tmp = plane.x * i + plane.w;
                                    for (int j = jOverall; j < jOverall + 8; j++)
                                    {
                                        int k = (int)(-(plane.y * j + tmp) + 0.5);

                                        if (k < kMin || k > kMax)
                                            continue;

                                        int index = i + 8 * j + 64 * k;

                                        Part p = null;
                                        int kSect = k >> 3;
                                        if (kSect == kSect1 && sect1 != null)
                                            p = sect1.GetVoxelPartGlobalIndex(index);
                                        else if (kSect == kSect2 && sect2 != null)
                                            p = sect2.GetVoxelPartGlobalIndex(index);
                                        else if (kSect == kSect3 && sect3 != null)
                                            p = sect3.GetVoxelPartGlobalIndex(index);

                                        if ((object)p != null)
                                        {
                                            areaCount++;
                                            centx += i;
                                            centy += j;
                                            centz += k;

                                            Vector3 location = vesselToSectionNormal.MultiplyVector(new Vector3(i, j, k));
                                            i_xx += location.x * location.x;
                                            i_xy += location.x * location.y;
                                            i_yy += location.y * location.y;

                                            DetermineIfPartGetsForcesAndAreas(partSideAreas, p, i, j, k);
                                        }
                                    }
                                }
                            }
                        }

                    Vector3d centroid = new Vector3d(centx, centy, centz);
                    //Vector3d unshadowedCentroid = new Vector3d(unshadowedCentx, unshadowedCenty, unshadowedCentz);
                    if (areaCount > 0)
                    {
                        if (frontIndexFound)
                            backIndex = m;
                        else
                        {
                            frontIndexFound = true;
                            frontIndex = m;
                        }
                        centroid /= areaCount;
                        /*if (unshadowedAreaCount != 0)
                            unshadowedCentroid /= unshadowedAreaCount;
                        else
                            unshadowedCentroid = centroid;*/
                    }
                    Vector3 localCentroid = vesselToSectionNormal.MultiplyVector(centroid);
                    i_xx -= areaCount * localCentroid.x * localCentroid.x;
                    i_xy -= areaCount * localCentroid.x * localCentroid.y;
                    i_yy -= areaCount * localCentroid.y * localCentroid.y;

                    double tanPrinAngle = TanPrincipalAxisAngle(i_xx, i_yy, i_xy);
                    Vector3 axis1 = new Vector3(1, 0, 0), axis2 = new Vector3(0, 0, 1);
                    double flatnessRatio = 1;

                    if (tanPrinAngle != 0)
                    {
                        axis1 = new Vector3(1, 0, (float)tanPrinAngle);
                        axis1.Normalize();
                        axis2 = new Vector3(axis1.z, 0, -axis1.x);

                        flatnessRatio = i_xy * axis2.z / axis2.x + i_xx;
                        flatnessRatio = (i_xy * tanPrinAngle + i_xx) / flatnessRatio;
                        flatnessRatio = Math.Sqrt(Math.Sqrt(flatnessRatio));
                    }
                    if (double.IsNaN(flatnessRatio))
                        flatnessRatio = 1;

                    Vector3 principalAxis;
                    if (flatnessRatio > 1)
                        principalAxis = axis1;
                    else
                    {
                        flatnessRatio = 1 / flatnessRatio;
                        principalAxis = axis2;
                    }
                    if (flatnessRatio > 10)
                        flatnessRatio = 10;

                    principalAxis = sectionNormalToVesselCoords.MultiplyVector(principalAxis);

                    crossSections[m].centroid = centroid * elementSize + lowerRightCorner;
                    //crossSections[m].additonalUnshadowedCentroid = unshadowedCentroid * elementSize + lowerRightCorner;

                    crossSections[m].area = areaCount * elementArea;
                    crossSections[m].flatnessRatio = flatnessRatio;
                    crossSections[m].flatNormalVector = principalAxis;
                    //crossSections[m].additionalUnshadowedArea = unshadowedAreaCount * elementArea;

                    plane.w += wInc;
                }
            }

            double denom = sectionThickness;
            denom *= denom;
            //denom *= 12;
            //denom *= Math.PI;
            denom = 1 / denom;
            maxCrossSectionArea = 0;

            for (int i = frontIndex; i <= backIndex; i++)
            {
                double areaM1, area0, areaP1;

                if (i - 1 < frontIndex)
                    areaM1 = 0;
                else
                    areaM1 = crossSections[i - 1].area;

                area0 = crossSections[i].area;

                if (i + 1 > backIndex)
                    areaP1 = 0;
                else
                    areaP1 = crossSections[i + 1].area;

                double areaSecondDeriv = (areaM1 + areaP1) - 2 * area0;
                areaSecondDeriv *= denom;

                /*double prevArea, curArea, nextArea, areaSecondDeriv;

                if (i == frontIndex)
                {
                    prevArea = 0;
                    curArea = crossSections[i].area;
                    nextArea = crossSections[i + 1].area;
                }
                else if (i == backIndex)
                {
                    prevArea = crossSections[i - 1].area;
                    curArea = crossSections[i].area;
                    nextArea = 0;
                }
                else
                {
                    prevArea = crossSections[i - 1].area;
                    curArea = crossSections[i].area;
                    nextArea = crossSections[i + 1].area;
                }

                areaSecondDeriv = nextArea + 2 * curArea + prevArea;
                areaSecondDeriv -= 2 * Math.Sqrt(nextArea * curArea);
                areaSecondDeriv -= 2 * Math.Sqrt(prevArea * curArea);
                areaSecondDeriv *= denom;*/

                crossSections[i].areaDeriv2ToNextSection = areaSecondDeriv;

                if (crossSections[i].area > maxCrossSectionArea)
                    maxCrossSectionArea = crossSections[i].area;
            }

            double gaussianVal2, gaussianVal1, gaussianVal0;
            gaussianVal2 = Math.Exp(-4 / 2);
            gaussianVal1 = Math.Exp(-1 / 2);
            gaussianVal0 = Math.Exp(0 / 2);

            double sum = gaussianVal0 + 2 * gaussianVal1 + 2 * gaussianVal2;

            gaussianVal2 /= sum;
            gaussianVal1 /= sum;
            gaussianVal0 /= sum;

            double unSmoothedLastDeriv = 0;
            double unSmoothedLastDeriv2 = 0;

            for (int i = frontIndex; i <= backIndex; i++)       //second area derivative smoothing pass
            {
                double prevDeriv2, prevDeriv, curDeriv, nextDeriv, nextDeriv2;
                prevDeriv = unSmoothedLastDeriv;
                prevDeriv2 = unSmoothedLastDeriv2;

                unSmoothedLastDeriv2 = unSmoothedLastDeriv;

                curDeriv = crossSections[i].areaDeriv2ToNextSection;     //this is used to make sure we don't end up using smoothed derivs for the calculations
                unSmoothedLastDeriv = curDeriv;

                if (i <= backIndex)
                {
                    nextDeriv = crossSections[i + 1].areaDeriv2ToNextSection;
                    if (i + 1 <= backIndex)
                        nextDeriv2 = crossSections[i + 2].areaDeriv2ToNextSection;
                    else
                        nextDeriv2 = 0;
                }
                else
                {
                    nextDeriv = 0;
                    nextDeriv2 = 0;
                }

                curDeriv *= gaussianVal0;
                curDeriv += gaussianVal1 * prevDeriv;
                curDeriv += gaussianVal1 * nextDeriv;
                curDeriv += gaussianVal2 * prevDeriv2;
                curDeriv += gaussianVal2 * nextDeriv2;

                crossSections[i].areaDeriv2ToNextSection = curDeriv;
            }

        }

        private unsafe void DetermineIfPartGetsForcesAndAreas(Dictionary<Part, VoxelCrossSection.SideAreaValues> partSideAreas, Part p, int i, int j, int k)
        {
            VoxelCrossSection.SideAreaValues areas;
            bool partGetsForces = true;
            if (!partSideAreas.TryGetValue(p, out areas))
            {
                areas = new VoxelCrossSection.SideAreaValues();
                partGetsForces = false;
            }
            if (i + 1 >= xCellLength || (object)GetPartAtVoxelPos(i + 1, j, k) == null)
            {
                areas.iP += elementSize * elementSize;
                partGetsForces = true;
            }
            if (i - 1 < 0 || (object)GetPartAtVoxelPos(i - 1, j, k) == null)
            {
                areas.iN += elementSize * elementSize;
                partGetsForces = true;
            }
            if (j + 1 >= yCellLength || (object)GetPartAtVoxelPos(i, j + 1, k) == null)
            {
                areas.jP += elementSize * elementSize;
                partGetsForces = true;
            }
            if (j - 1 < 0 || (object)GetPartAtVoxelPos(i, j - 1, k) == null)
            {
                areas.jN += elementSize * elementSize;
                partGetsForces = true;
            }
            if (k + 1 >= zCellLength || (object)GetPartAtVoxelPos(i, j, k + 1) == null)
            {
                areas.kP += elementSize * elementSize;
                partGetsForces = true;
            }
            if (k - 1 < 0 || (object)GetPartAtVoxelPos(i, j, k - 1) == null)
            {
                areas.kN += elementSize * elementSize;
                partGetsForces = true;
            }
            if (partGetsForces)
            {
                areas.count++;
                partSideAreas[p] = areas;
            }
        }

        private double TanPrincipalAxisAngle(double Ixx, double Iyy, double Ixy)
        {
            if (Ixx == Iyy)
                return 0;

            double tan2Angle = 2d * Ixy / (Ixx - Iyy);
            double tanAngle = 1 + tan2Angle * tan2Angle;
            tanAngle = Math.Sqrt(tanAngle);
            tanAngle++;
            tanAngle = tan2Angle / tanAngle;

            return tanAngle;
            //Vector3d principalAxis = new Vector3d(1, tanAngle, 0);
            //principalAxis.Normalize();

            //return principalAxis;
        }

        public void ClearVisualVoxels()
        {
            for (int i = 0; i < xLength; i++)
                for (int j = 0; j < yLength; j++)
                    for (int k = 0; k < zLength; k++)
                    {
                        VoxelChunk section = voxelChunks[i, j, k];
                        if (section != null)
                        {
                            section.ClearVisualVoxels();
                        }
                    }
        }

        public void VisualizeVoxel(Matrix4x4 vesselLocalToWorldMatrix)
        {
            for (int i = 0; i < xLength; i++)
                for (int j = 0; j < yLength; j++)
                    for (int k = 0; k < zLength; k++)
                    {
                        VoxelChunk section = voxelChunks[i, j, k];
                        if (section != null)
                        {
                            section.VisualizeVoxels(vesselLocalToWorldMatrix);
                        }
                    }
        }

        //Use when guaranteed that you will not attempt to write to the same section simultaneously
        private unsafe void SetVoxelSectionNoLock(int i, int j, int k, Part part)
        {
            int iSec, jSec, kSec;
            //Find the voxel section that this point points to

            iSec = i >> 3;
            jSec = j >> 3;
            kSec = k >> 3;

            VoxelChunk section;

            section = voxelChunks[iSec, jSec, kSec];
            if (section == null)
            {
                section = new VoxelChunk(elementSize, lowerRightCorner + new Vector3d(iSec, jSec, kSec) * elementSize * 8, iSec * 8, jSec * 8, kSec * 8);
                voxelChunks[iSec, jSec, kSec] = section;
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

            VoxelChunk section;

            lock (voxelChunks)
            {
                section = voxelChunks[iSec, jSec, kSec];
                if (section == null)
                {
                    section = new VoxelChunk(elementSize, lowerRightCorner + new Vector3d(iSec, jSec, kSec) * elementSize * 8, iSec * 8, jSec * 8, kSec * 8);
                    voxelChunks[iSec, jSec, kSec] = section;
                }
            }

            //Debug.Log(i.ToString() + ", " + j.ToString() + ", " + k.ToString() + ", " + part.partInfo.title);

            section.SetVoxelPointGlobalIndex(i, j, k, part);
        }

        private unsafe VoxelChunk GetVoxelChunk(int i, int j, int k)
        {
            int iSec, jSec, kSec;
            //Find the voxel section that this point points to
            iSec = i >> 3;
            jSec = j >> 3;
            kSec = k >> 3;

            VoxelChunk section;
            //lock (voxelSections)
            //{
                section = voxelChunks[iSec, jSec, kSec];
            //}
            return section;
        }

        private unsafe bool VoxelPointExistsAtPos(int i, int j, int k)
        {
            int iSec, jSec, kSec;
            //Find the voxel section that this point points to

            iSec = i >> 3;
            jSec = j >> 3;
            kSec = k >> 3;

            VoxelChunk section;
            //lock (voxelSections)      //No locks are needed because reading and writing are not done in different threads simultaneously
            //{
            section = voxelChunks[iSec, jSec, kSec];
            //}
            if (section == null)
                return false;

            return section.VoxelPointExistsGlobalIndex(i, j, k);
        }
        
        private unsafe Part GetPartAtVoxelPos(int i, int j, int k)
        {
            int iSec, jSec, kSec;
            //Find the voxel section that this point points to

            iSec = i >> 3;
            jSec = j >> 3;
            kSec = k >> 3;

            VoxelChunk section;
            //lock (voxelSections)      //No locks are needed because reading and writing are not done in different threads simultaneously
            //{
                section = voxelChunks[iSec, jSec, kSec];
            //}
            if (section == null)
                return null;

            return section.GetVoxelPartGlobalIndex(i, j, k);
        }

        private unsafe Part GetPartAtVoxelPos(int i, int j, int k, ref VoxelChunk section)
        {
            return section.GetVoxelPartGlobalIndex(i, j, k);
        }

        private void UpdateFromMesh(object stuff)
        {
            try
            {
                VoxelShellParams data = (VoxelShellParams)stuff;
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
            if (mesh.bounds.size.x < elementSize && mesh.bounds.size.y < elementSize && mesh.bounds.size.z < elementSize)
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

            lowerI = (int)(min.x);
            lowerJ = (int)(min.y);
            lowerK = (int)(min.z);

            upperI = (int)(max.x + 1);
            upperJ = (int)(max.y + 1);
            upperK = (int)(max.z + 1);

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

            double x, y, z;
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

        private void VoxelShellTrianglePerpX(Vector4d indexPlane, Vector3d vert1, Vector3d vert2, Vector3d vert3, Part part)
        {
            Vector2d vert1Proj, vert2Proj, vert3Proj;
            vert1Proj = new Vector2d(vert1.y - lowerRightCorner.y, vert1.z - lowerRightCorner.z) * invElementSize;
            vert2Proj = new Vector2d(vert2.y - lowerRightCorner.y, vert2.z - lowerRightCorner.z) * invElementSize;
            vert3Proj = new Vector2d(vert3.y - lowerRightCorner.y, vert3.z - lowerRightCorner.z) * invElementSize;

            Vector2d p1p2, p1p3;
            p1p2 = vert2Proj - vert1Proj;
            p1p3 = vert3Proj - vert1Proj;

            double dot12_12, dot12_13, dot13_13;
            dot12_12 = p1p2.x * p1p2.x + p1p2.y * p1p2.y;
            dot12_13 = p1p2.x * p1p3.x + p1p2.y * p1p3.y;
            dot13_13 = p1p3.x * p1p3.x + p1p3.y * p1p3.y;

/*            dot12_12 = Vector2.Dot(p1p2, p1p2);
            dot12_13 = Vector2.Dot(p1p2, p1p3);
            dot13_13 = Vector2.Dot(p1p3, p1p3);*/

            double invDenom = 1 / (dot12_12 * dot13_13 - dot12_13 * dot12_13);

            int lowJ, highJ, lowK, highK;
            lowJ = (int)(Math.Min(vert1Proj.x, Math.Min(vert2Proj.x, vert3Proj.x)) - RC);
            highJ = (int)(Math.Ceiling(Math.Max(vert1Proj.x, Math.Max(vert2Proj.x, vert3Proj.x)) + RC));
            lowK = (int)(Math.Min(vert1Proj.y, Math.Min(vert2Proj.y, vert3Proj.y)) - RC);
            highK = (int)(Math.Ceiling(Math.Max(vert1Proj.y, Math.Max(vert2Proj.y, vert3Proj.y)) + RC));

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
                    Vector2d pt = new Vector2d(j, k);
                    Vector2d p1TestPt = pt - vert1Proj;
                    double dot12_test, dot13_test;
                    dot12_test = p1p2.x * p1TestPt.x + p1p2.y * p1TestPt.y;
                    dot13_test = p1p3.x * p1TestPt.x + p1p3.y * p1TestPt.y;

                    /*dot12_test = Vector2.Dot(p1p2, p1TestPt);
                    dot13_test = Vector2.Dot(p1p3, p1TestPt);*/

                    double u, v;
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
                    Vector2d p2TestPt = pt - vert2Proj;
                    Vector2d p3TestPt = pt - vert3Proj;
                    if (p1TestPt.magnitude <= RC || p2TestPt.magnitude <= RC || p3TestPt.magnitude <= RC)
                    {
                        int i = (int)Math.Round(-(indexPlane.y * j + indexPlane.z * k + indexPlane.w) / indexPlane.x);
                        if (i < 0 || i >= xCellLength)
                            continue;

                        SetVoxelSection(i, j, k, part);
                        continue;
                    }

                    if (IsWithinDistanceFromSide(p1p2, p1TestPt) ||
                        IsWithinDistanceFromSide(p1p3, p1TestPt) ||
                        IsWithinDistanceFromSide(vert3Proj - vert2Proj, p2TestPt))
                    {
                        int i = (int)Math.Round(-(indexPlane.y * j + indexPlane.z * k + indexPlane.w) / indexPlane.x);
                        if (i < 0 || i >= xCellLength)
                            continue;

                        SetVoxelSection(i, j, k, part);

                    }
                }
        }

        private void VoxelShellTrianglePerpY(Vector4d indexPlane, Vector3d vert1, Vector3d vert2, Vector3d vert3, Part part)
        {
            Vector2d vert1Proj, vert2Proj, vert3Proj;
            vert1Proj = new Vector2d(vert1.x - lowerRightCorner.x, vert1.z - lowerRightCorner.z) * invElementSize;
            vert2Proj = new Vector2d(vert2.x - lowerRightCorner.x, vert2.z - lowerRightCorner.z) * invElementSize;
            vert3Proj = new Vector2d(vert3.x - lowerRightCorner.x, vert3.z - lowerRightCorner.z) * invElementSize;

            
            Vector2d p1p2, p1p3;
            p1p2 = vert2Proj - vert1Proj;
            p1p3 = vert3Proj - vert1Proj;

            double dot12_12, dot12_13, dot13_13;
            dot12_12 = p1p2.x * p1p2.x + p1p2.y * p1p2.y;
            dot12_13 = p1p2.x * p1p3.x + p1p2.y * p1p3.y;
            dot13_13 = p1p3.x * p1p3.x + p1p3.y * p1p3.y;

            /*            dot12_12 = Vector2.Dot(p1p2, p1p2);
                        dot12_13 = Vector2.Dot(p1p2, p1p3);
                        dot13_13 = Vector2.Dot(p1p3, p1p3);*/

            double invDenom = 1 / (dot12_12 * dot13_13 - dot12_13 * dot12_13);

            int lowI, highI, lowK, highK;
            lowI = (int)(Math.Min(vert1Proj.x, Math.Min(vert2Proj.x, vert3Proj.x)) - RC);
            highI = (int)(Math.Ceiling(Math.Max(vert1Proj.x, Math.Max(vert2Proj.x, vert3Proj.x)) + RC));
            lowK = (int)(Math.Min(vert1Proj.y, Math.Min(vert2Proj.y, vert3Proj.y)) - RC);
            highK = (int)(Math.Ceiling(Math.Max(vert1Proj.y, Math.Max(vert2Proj.y, vert3Proj.y)) + RC));


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
                    Vector2d pt = new Vector2d(i, k);
                    Vector2d p1TestPt = pt - vert1Proj;
                    double dot12_test, dot13_test;
                    dot12_test = p1p2.x * p1TestPt.x + p1p2.y * p1TestPt.y;
                    dot13_test = p1p3.x * p1TestPt.x + p1p3.y * p1TestPt.y;

                    /*dot12_test = Vector2.Dot(p1p2, p1TestPt);
                    dot13_test = Vector2.Dot(p1p3, p1TestPt);*/

                    double u, v;
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
                    Vector2d p2TestPt = pt - vert2Proj;
                    Vector2d p3TestPt = pt - vert3Proj;
                    if (p1TestPt.magnitude <= RC || p2TestPt.magnitude <= RC || p3TestPt.magnitude <= RC)
                    {
                        int j = (int)Math.Round(-(indexPlane.x * i + indexPlane.z * k + indexPlane.w) / indexPlane.y);
                        if (j < 0 || j >= yCellLength)
                            continue;
                        SetVoxelSection(i, j, k, part);
                        continue;
                    }

                    if (IsWithinDistanceFromSide(p1p2, p1TestPt) ||
                        IsWithinDistanceFromSide(p1p3, p1TestPt) ||
                        IsWithinDistanceFromSide(vert3Proj - vert2Proj, p2TestPt))
                    {
                        int j = (int)Math.Round(-(indexPlane.x * i + indexPlane.z * k + indexPlane.w) / indexPlane.y);
                        if (j < 0 || j >= yCellLength)
                            continue;
                        SetVoxelSection(i, j, k, part);
                    }
                }
        }

        private void VoxelShellTrianglePerpZ(Vector4d indexPlane, Vector3d vert1, Vector3d vert2, Vector3d vert3, Part part)
        {
            Vector2d vert1Proj, vert2Proj, vert3Proj;
            vert1Proj = new Vector2d(vert1.x - lowerRightCorner.x, vert1.y - lowerRightCorner.y) * invElementSize;
            vert2Proj = new Vector2d(vert2.x - lowerRightCorner.x, vert2.y - lowerRightCorner.y) * invElementSize;
            vert3Proj = new Vector2d(vert3.x - lowerRightCorner.x, vert3.y - lowerRightCorner.y) * invElementSize;

            Vector2d p1p2, p1p3;
            p1p2 = vert2Proj - vert1Proj;
            p1p3 = vert3Proj - vert1Proj;

            double dot12_12, dot12_13, dot13_13;
            dot12_12 = p1p2.x * p1p2.x + p1p2.y * p1p2.y;
            dot12_13 = p1p2.x * p1p3.x + p1p2.y * p1p3.y;
            dot13_13 = p1p3.x * p1p3.x + p1p3.y * p1p3.y;

            /*            dot12_12 = Vector2.Dot(p1p2, p1p2);
                        dot12_13 = Vector2.Dot(p1p2, p1p3);
                        dot13_13 = Vector2.Dot(p1p3, p1p3);*/

            double invDenom = 1 / (dot12_12 * dot13_13 - dot12_13 * dot12_13);

            int lowI, highI, lowJ, highJ;
            lowI = (int)(Math.Min(vert1Proj.x, Math.Min(vert2Proj.x, vert3Proj.x)) - RC);
            highI = (int)(Math.Ceiling(Math.Max(vert1Proj.x, Math.Max(vert2Proj.x, vert3Proj.x)) + RC));
            lowJ = (int)(Math.Min(vert1Proj.y, Math.Min(vert2Proj.y, vert3Proj.y)) - RC);
            highJ = (int)(Math.Ceiling(Math.Max(vert1Proj.y, Math.Max(vert2Proj.y, vert3Proj.y)) + RC));


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
                    Vector2d pt = new Vector2d(i, j);
                    Vector2d p1TestPt = pt - vert1Proj;
                    double dot12_test, dot13_test;
                    dot12_test = p1p2.x * p1TestPt.x + p1p2.y * p1TestPt.y;
                    dot13_test = p1p3.x * p1TestPt.x + p1p3.y * p1TestPt.y;

                    /*dot12_test = Vector2.Dot(p1p2, p1TestPt);
                    dot13_test = Vector2.Dot(p1p3, p1TestPt);*/

                    double u, v;
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
                    Vector2d p2TestPt = pt - vert2Proj;
                    Vector2d p3TestPt = pt - vert3Proj;
                    if (p1TestPt.magnitude <= RC || p2TestPt.magnitude <= RC || p3TestPt.magnitude <= RC)
                    {
                        int k = (int)Math.Round(-(indexPlane.x * i + indexPlane.y * j + indexPlane.w) / indexPlane.z);
                        if (k < 0 || k >= zCellLength)
                            continue;

                        SetVoxelSection(i, j, k, part);
                        continue;
                    }

                    if (IsWithinDistanceFromSide(p1p2, p1TestPt)||
                        IsWithinDistanceFromSide(p1p3, p1TestPt)||
                        IsWithinDistanceFromSide(vert3Proj - vert2Proj, p2TestPt))
                    {
                        int k = (int)Math.Round(-(indexPlane.x * i + indexPlane.y * j + indexPlane.w) / indexPlane.z);
                        if (k < 0 || k >= zCellLength)
                            continue;

                        SetVoxelSection(i, j, k, part);
                    }
                }
        }

        private bool IsWithinDistanceFromSide(Vector2d sideVector, Vector2d testVec)
        {
            Vector2d perpVector = new Vector2d(sideVector.y, -sideVector.x);        //vector perpendicular to the sideVector
            perpVector.Normalize();

            double dist =  testVec.x * perpVector.x + testVec.y * perpVector.y;   //length perp from this

            if(Math.Abs(dist) > RC)
                return false;

            testVec -= perpVector * dist;   //this projects testVec onto sideVector

            double sideDot = testVec.x * sideVector.x + testVec.y * sideVector.y;

            if (sideDot >= 0 && sideDot <= sideVector.sqrMagnitude)
                return true;

            return false;
        }

        private Vector4d CalculateEquationOfSweepPlane(Vector3d normalVector, out double wInc)
        {
            Vector4d result = new Vector4d(normalVector.x, normalVector.y, normalVector.z);

            if(result.x > 0)
                result.w -= result.x * xCellLength;
            if (result.y > 0)
                result.w -= result.y * yCellLength;
            if (result.z > 0)
                result.w -= result.z * zCellLength;

            double x, y, z;
            x = Math.Abs(result.x);
            y = Math.Abs(result.y);
            z = Math.Abs(result.z);

            if (y >= x && y >= z)
                wInc = y;
            else if (x > y && x > z)
                wInc = x;
            else
                wInc = z;

            return result;
        }

        private Vector4d CalculateEquationOfPlaneInIndices(Vector3d pt1, Vector3d pt2, Vector3d pt3)
        {
            Vector3d p1p2 = pt2 - pt1;
            Vector3d p1p3 = pt3 - pt1;

            Vector3d tmp = Vector3d.Cross(p1p2, p1p3);

            Vector4d result = new Vector4d(tmp.x, tmp.y, tmp.z);

            result.w = result.x * (lowerRightCorner.x - pt1.x) + result.y * (lowerRightCorner.y - pt1.y) + result.z * (lowerRightCorner.z - pt1.z);
            result.w *= invElementSize;

            return result;
        }


        private Vector4d CalculateEquationOfPlane(Vector3d pt1, Vector3d pt2, Vector3d pt3)
        {
            Vector3d p1p2 = pt2 - pt1;
            Vector3d p1p3 = pt3 - pt1;

            Vector3d tmp = Vector3d.Cross(p1p2, p1p3);

            Vector4d result = new Vector4d(tmp.x, tmp.y, tmp.z);

            result.w = -(pt1.x * result.x + pt1.y * result.y + pt1.z * result.z);

            return result;
        }

        private Vector4d TransformPlaneToIndices(Vector4d plane)
        {
            Vector4d newPlane = new Vector4d();
            newPlane.x = plane.x * elementSize;
            newPlane.y = plane.y * elementSize;
            newPlane.z = plane.z * elementSize;
            newPlane.w = plane.w + plane.x * lowerRightCorner.x + plane.y * lowerRightCorner.y + plane.z * lowerRightCorner.z;

            return newPlane;
        }

        private unsafe void SolidifyVoxel(object uncastData)
        {
            VoxelSolidParams parameters = (VoxelSolidParams)uncastData;
            try
            {
                SolidifyVoxel(parameters.lowJ, parameters.highJ, parameters.increasingJ);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        
        private unsafe void SolidifyVoxel(int lowJ, int highJ, bool increasingJ)
        {
            SweepPlanePoint[,] sweepPlane = new SweepPlanePoint[xCellLength, zCellLength];
            List<SweepPlanePoint> activePts = new List<SweepPlanePoint>();
            HashSet<SweepPlanePoint> inactiveInteriorPts = new HashSet<SweepPlanePoint>();
            SweepPlanePoint[] neighboringSweepPlanePts = new SweepPlanePoint[4];

            if(increasingJ)
                for (int j = lowJ; j < highJ; j++) //Iterate from back of vehicle to front
                {
                    SolidifyLoop(j, sweepPlane, activePts, inactiveInteriorPts, neighboringSweepPlanePts);
                }
            else
                for (int j = highJ - 1; j >= lowJ; j--) //Iterate from front of vehicle to back
                {
                    SolidifyLoop(j, sweepPlane, activePts, inactiveInteriorPts, neighboringSweepPlanePts);
                }

            //Cleanup
            sweepPlane = null;
            activePts = null;
            inactiveInteriorPts = null;
            neighboringSweepPlanePts = null;

            lock (_locker)
            {
                threadsQueued--;
                Monitor.Pulse(_locker);
            }
        }

        private unsafe void SolidifyLoop(int j, SweepPlanePoint[,] sweepPlane, List<SweepPlanePoint> activePts, HashSet<SweepPlanePoint> inactiveInteriorPts, SweepPlanePoint[] neighboringSweepPlanePts)
        {
            for (int i = 0; i < xCellLength; i++) //Iterate across the cross-section plane to add voxel shell and mark active interior points
                for (int k = 0; k < zCellLength; k++)
                {
                    SweepPlanePoint pt = sweepPlane[i, k];
                    Part p = GetPartAtVoxelPos(i, j, k);

                    if (pt == null) //If there is a section of voxel there, but no pt, add a new voxel shell pt to the sweep plane
                    {
                        if ((object)p != null)
                        {
                            sweepPlane[i, k] = new SweepPlanePoint(p, i, k);
                            continue;
                        }
                    }
                    else 
                    {
                        if ((object)p == null) //If there is a pt there, but no part listed, this is an interior pt or a the cross-section is shrinking
                        {
                            if (pt.mark == SweepPlanePoint.MarkingType.VoxelShell) //label it as active so that it can be determined if it is interior or not once all the points have been updated
                            {
                                activePts.Add(pt); //And add it to the list of active interior pts
                                pt.mark = SweepPlanePoint.MarkingType.Active;
                            }
                        }
                        else
                        { //Make sure the point is labeled as a voxel shell if there is already a part there
                            inactiveInteriorPts.Remove(pt);
                            pt.mark = SweepPlanePoint.MarkingType.VoxelShell;
                            pt.part = p;
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
                foreach (SweepPlanePoint neighbor in neighboringSweepPlanePts)//Check if the active point is surrounded by all 4 neighbors
                    if (neighbor == null || neighbor.mark == SweepPlanePoint.MarkingType.Clear) //If any of them are null or marked clear, this active point is not an interior point
                    {                                                                       //In that case, it should be set to be removed
                        remove = true;
                        break;
                    }
                if (remove) //If it is set to be removed...
                {
                    foreach (SweepPlanePoint neighbor in neighboringSweepPlanePts) //Go through all the neighboring points
                    {
                        //SweepPlanePoint neighbor = neighboringSweepPlanePts[m];
                        if (neighbor != null && neighbor.mark == SweepPlanePoint.MarkingType.InactiveInterior) //For the ones that exist, and are inactive interior...
                        {
                            inactiveInteriorPts.Remove(neighbor); //remove them from inactiveInterior
                            neighbor.mark = SweepPlanePoint.MarkingType.Active; //...mark them active
                            activePts.Add(neighbor); //And add them to the end of activePts
                        }
                    }
                    sweepPlane[activeInteriorPt.i, activeInteriorPt.k].mark = SweepPlanePoint.MarkingType.Clear; //Then, set this point to be marked clear in the sweepPlane
                }
                else
                { //If it's surrounded by other points, it's inactive; add it to that list
                    activeInteriorPt.mark = SweepPlanePoint.MarkingType.InactiveInterior;
                    inactiveInteriorPts.Add(activeInteriorPt);
                }
            }
            activePts.Clear(); //Clear activePts every iteration

            foreach (SweepPlanePoint inactivePt in inactiveInteriorPts) //Any remaining inactive interior pts are guaranteed to be on the inside of the vehicle
            {
                SetVoxelSectionNoLock(inactivePt.i, j, inactivePt.k, inactivePt.part); //Get each and update the voxel accordingly
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
                Active,
                InactiveInterior,
                Clear
            }
        }

        private class VoxelSolidParams
        {
            public int lowJ;
            public int highJ;
            public bool increasingJ;

            public VoxelSolidParams(int lowJ, int highJ, bool increasingJ)
            {
                this.lowJ = lowJ;
                this.highJ = highJ;
                this.increasingJ = increasingJ;
            }
        }
        private class VoxelShellParams
        {
            public Part part;
            public GeometryMesh mesh;

            public VoxelShellParams(Part part, GeometryMesh mesh)
            {
                this.part = part;
                this.mesh = mesh;
            }
        }

        public int planeJ { get; set; }
    }
}
