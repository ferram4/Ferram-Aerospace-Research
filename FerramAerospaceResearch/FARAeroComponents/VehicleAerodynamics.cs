/*
Ferram Aerospace Research v0.15.5.4 "Hoerner"
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
using System.Threading;
using UnityEngine;
using FerramAerospaceResearch.FARThreading;
using FerramAerospaceResearch.FARPartGeometry;
using FerramAerospaceResearch.FARPartGeometry.GeometryModification;

namespace FerramAerospaceResearch.FARAeroComponents
{
    class VehicleAerodynamics
    {
        static double[] indexSqrt = new double[1];
        static object _commonLocker = new object();

        VehicleVoxel _voxel = null;
        VoxelCrossSection[] _vehicleCrossSection = new VoxelCrossSection[1];
        double[] _ductedAreaAdjustment = new double[1];

        int _voxelCount;

        double _length = 0;
        public double Length
        {
            get { return _length; }
        }

        double _maxCrossSectionArea = 0;
        public double MaxCrossSectionArea
        {
            get { return _maxCrossSectionArea; }
        }

        bool _calculationCompleted = false;
        public bool CalculationCompleted
        {
            get { return _calculationCompleted; }
        }

        double _sonicDragArea;
        public double SonicDragArea
        {
            get { return _sonicDragArea; }
        }

        double _criticalMach;
        public double CriticalMach
        {
            get { return _criticalMach; }
        }

        Matrix4x4 _worldToLocalMatrix, _localToWorldMatrix;

        Vector3d _voxelLowerRightCorner;
        double _voxelElementSize;
        double _sectionThickness;
        public double SectionThickness
        {
            get { return _sectionThickness; }
        }

        Vector3 _vehicleMainAxis;
        List<Part> _vehiclePartList;

        List<GeometryPartModule> _currentGeoModules;
        Dictionary<Part, PartTransformInfo> _partWorldToLocalMatrixDict = new Dictionary<Part, PartTransformInfo>();
        Dictionary<FARAeroPartModule, FARAeroPartModule.ProjectedArea> _moduleAndAreasDict = new Dictionary<FARAeroPartModule, FARAeroPartModule.ProjectedArea>();

        List<FARAeroPartModule> _currentAeroModules = new List<FARAeroPartModule>();
        List<FARAeroPartModule> _newAeroModules = new List<FARAeroPartModule>();

        List<FARAeroPartModule> _currentUnusedAeroModules = new List<FARAeroPartModule>();
        List<FARAeroPartModule> _newUnusedAeroModules = new List<FARAeroPartModule>();

        List<FARAeroSection> _currentAeroSections = new List<FARAeroSection>();
        List<FARAeroSection> _newAeroSections = new List<FARAeroSection>();

        List<ferram4.FARWingAerodynamicModel> _legacyWingModels = new List<ferram4.FARWingAerodynamicModel>();

        List<ICrossSectionAdjuster> activeAdjusters = new List<ICrossSectionAdjuster>();
        //Dictionary<Part, double> adjusterAreaPerVoxelDict = new Dictionary<Part, double>();
        //Dictionary<Part, ICrossSectionAdjuster> adjusterPartDict = new Dictionary<Part, ICrossSectionAdjuster>();

        List<FARAeroPartModule> includedModules = new List<FARAeroPartModule>();
        List<float> weighting = new List<float>();
        static Stack<FARAeroSection> currentlyUnusedSections;

        int validSectionCount;
        int firstSection;

        bool visualizing = false;
        bool voxelizing = false;

        public VehicleAerodynamics()
        {
            if(currentlyUnusedSections == null)
                currentlyUnusedSections = new Stack<FARAeroSection>();
        }

        public void ForceCleanup()
        {
            if (_voxel != null)
            {
                _voxel.CleanupVoxel();
                _voxel = null;
            }
            _vehicleCrossSection = null;
            _ductedAreaAdjustment = null;

            _currentAeroModules = null;
            _newAeroModules = null;

            _currentUnusedAeroModules = null;
            _newUnusedAeroModules = null;

            _currentAeroSections = null;
            _newAeroSections = null;

            _legacyWingModels = null;

            _vehiclePartList = null;

            activeAdjusters = null;
        }

        private double[] GenerateIndexSqrtLookup(int numStations)
        {
            double[] indexSqrt = new double[numStations];
            for (int i = 0; i < numStations; i++)
                indexSqrt[i] = Math.Sqrt(i);

            return indexSqrt;
        }

        #region UpdateAeroData
        //Used by other classes to update their aeroModule and aeroSection lists
        //When these functions fire, all the data that was once restricted to the voxelization thread is passed over to the main unity thread

        public void GetNewAeroData(out List<FARAeroPartModule> aeroModules, out List<FARAeroPartModule> unusedAeroModules, out List<FARAeroSection> aeroSections, out List<ferram4.FARWingAerodynamicModel> legacyWingModel)
        {
            _calculationCompleted = false;
            List<FARAeroPartModule> tmpAeroModules = _currentAeroModules;
            aeroModules = _currentAeroModules = _newAeroModules;
            _newAeroModules = tmpAeroModules;

            List<FARAeroSection> tmpAeroSections = _currentAeroSections;
            aeroSections = _currentAeroSections = _newAeroSections;
            _newAeroSections = tmpAeroSections;

            tmpAeroModules = _currentUnusedAeroModules;
            unusedAeroModules = _currentUnusedAeroModules = _newUnusedAeroModules;
            _newUnusedAeroModules = tmpAeroModules;


            legacyWingModel = LEGACY_UpdateWingAerodynamicModels();
        }

        public void GetNewAeroData(out List<FARAeroPartModule> aeroModules, out List<FARAeroSection> aeroSections)
        {
            _calculationCompleted = false;
            List<FARAeroPartModule> tmpAeroModules = _currentAeroModules;
            aeroModules = _currentAeroModules = _newAeroModules;
            _newAeroModules = tmpAeroModules;

            List<FARAeroSection> tmpAeroSections = _currentAeroSections;
            aeroSections = _currentAeroSections = _newAeroSections;
            _newAeroSections = tmpAeroSections;

            tmpAeroModules = _currentUnusedAeroModules;
            _currentUnusedAeroModules = _newUnusedAeroModules;
            _newUnusedAeroModules = tmpAeroModules;

            LEGACY_UpdateWingAerodynamicModels();
        }

        private List<ferram4.FARWingAerodynamicModel> LEGACY_UpdateWingAerodynamicModels()
        {
            _legacyWingModels.Clear();
            for (int i = 0; i < _currentAeroModules.Count; i++)
            {
                Part p = _currentAeroModules[i].part;
                if (!p)
                    continue;
                if (p.Modules.Contains("FARWingAerodynamicModel"))
                {
                    ferram4.FARWingAerodynamicModel w = (ferram4.FARWingAerodynamicModel)p.Modules["FARWingAerodynamicModel"];
                    if ((object)w != null)
                    {
                        w.isShielded = false;
                        w.NUFAR_ClearExposedAreaFactor();
                        _legacyWingModels.Add(w);
                    }
                }
                else if (p.Modules.Contains("FARControllableSurface"))
                {
                    ferram4.FARWingAerodynamicModel w = (ferram4.FARWingAerodynamicModel)p.Modules["FARControllableSurface"];
                    if ((object)w != null)
                    {
                        w.isShielded = false;
                        w.NUFAR_ClearExposedAreaFactor();
                        _legacyWingModels.Add(w);
                    }
                }
            }

            /*for (int i = 0; i < _currentAeroSections.Count; i++)
            {
                FARAeroSection sect = _currentAeroSections[i];
                sect.LEGACY_SetLiftForFARWingAerodynamicModel();
            }*/

            for (int i = 0; i < _legacyWingModels.Count; i++)
            {
                ferram4.FARWingAerodynamicModel w = _legacyWingModels[i];
                w.NUFAR_CalculateExposedAreaFactor();
            } 

            for (int i = 0; i < _legacyWingModels.Count; i++)
            {
                ferram4.FARWingAerodynamicModel w = _legacyWingModels[i];
                w.NUFAR_SetExposedAreaFactor();
            }
            for (int i = 0; i < _legacyWingModels.Count; i++)
            {
                ferram4.FARWingAerodynamicModel w = _legacyWingModels[i];
                w.NUFAR_UpdateShieldingStateFromAreaFactor();
            } 
            return _legacyWingModels;
        }
        #endregion

        #region GetFunctions

        //returns various data for use in displaying outside this class

        public Matrix4x4 VoxelAxisToLocalCoordMatrix()
        {
            return Matrix4x4.TRS(Vector3.zero, Quaternion.FromToRotation(_vehicleMainAxis, Vector3.up), Vector3.one);
        }

        public double FirstSectionXOffset()
        {
            double offset = Vector3d.Dot(_vehicleMainAxis, _voxelLowerRightCorner);
            offset += firstSection * _sectionThickness;

            return offset;
        }

        public double[] GetPressureCoeffs()
        {
            double[] pressureCoeffs = new double[validSectionCount];
            return GetPressureCoeffs(pressureCoeffs);
        }

        public double[] GetPressureCoeffs(double[] pressureCoeffs)
        {
            for (int i = firstSection; i < validSectionCount + firstSection; i++)
                pressureCoeffs[i - firstSection] = _vehicleCrossSection[i].cpSonicForward;

            return pressureCoeffs;
        }


        public double[] GetCrossSectionAreas()
        {
            double[] areas = new double[validSectionCount];
            return GetCrossSectionAreas(areas);
        }

        public double[] GetCrossSectionAreas(double[] areas)
        {
            for (int i = firstSection; i < validSectionCount + firstSection; i++)
                areas[i - firstSection] = _vehicleCrossSection[i].area;

            return areas;
        }

        public double[] GetCrossSection2ndAreaDerivs()
        {
            double[] areaDerivs = new double[validSectionCount];
            return GetCrossSection2ndAreaDerivs(areaDerivs);
        }

        public double[] GetCrossSection2ndAreaDerivs(double[] areaDerivs)
        {
            for (int i = firstSection; i < validSectionCount + firstSection; i++)
                areaDerivs[i - firstSection] = _vehicleCrossSection[i].secondAreaDeriv;

            return areaDerivs;
        }

        #endregion

        #region VoxelDebug
        //Handling for display of debug voxels

        private void ClearDebugVoxel()
        {
            _voxel.ClearVisualVoxels();
            visualizing = false;
        }

        private void DisplayDebugVoxels(Matrix4x4 localToWorldMatrix)
        {
            _voxel.VisualizeVoxel(localToWorldMatrix);
            visualizing = true;
        }

        public void DebugVisualizeVoxels(Matrix4x4 localToWorldMatrix)
        {
            if (visualizing)
                ClearDebugVoxel();
            else
                DisplayDebugVoxels(localToWorldMatrix);
        }

        #endregion

        #region Voxelization

        //This function will attempt to voxelize the vessel, as long as it isn't being voxelized currently all data that is on the Unity thread should be processed here before being passed to the other threads
        public bool TryVoxelUpdate(Matrix4x4 worldToLocalMatrix, Matrix4x4 localToWorldMatrix, int voxelCount, List<Part> vehiclePartList, List<GeometryPartModule> currentGeoModules, bool updateGeometryPartModules = true)
        {
            bool returnVal = false;
            if (voxelizing)             //set to true when this function ends; only continue to voxelizing if the voxelization thread has not been queued
            {                                               //this should catch conditions where this function is called again before the voxelization thread starts
                returnVal = false;
            }
            else if (Monitor.TryEnter(this, 0))         //only continue if the voxelizing thread has not locked this object
            {
                try
                {
                    //Bunch of voxel setup data
                    _voxelCount = voxelCount;

                    this._worldToLocalMatrix = worldToLocalMatrix;
                    this._localToWorldMatrix = localToWorldMatrix;
                    this._vehiclePartList = vehiclePartList;
                    this._currentGeoModules = currentGeoModules;

                    _partWorldToLocalMatrixDict.Clear();

                    for (int i = 0; i < _currentGeoModules.Count; i++)
                    {
                        GeometryPartModule g = _currentGeoModules[i];
                        _partWorldToLocalMatrixDict.Add(g.part, new PartTransformInfo(g.part.partTransform));
                        if (updateGeometryPartModules)
                            g.UpdateTransformMatrixList(_worldToLocalMatrix);
                    }

                    this._vehicleMainAxis = CalculateVehicleMainAxis();
                    //If the voxel still exists, cleanup everything so we can continue;
                    visualizing = false;

                    if (_voxel != null)
                    {
                        _voxel.CleanupVoxel();
                    }

                    //set flag so that this function can't run again before voxelizing completes and queue voxelizing thread
                    voxelizing = true;
                    VoxelizationThreadpool.Instance.QueueVoxelization(CreateVoxel);
                    returnVal = true;
                }
                finally
                {
                    Monitor.Exit(this);
                }
            }

            return returnVal;
        }

        //And this actually creates the voxel and then begins the aero properties determination
        private void CreateVoxel()
        {
            lock (this)     //lock this object to prevent race with main thread
            {
                try
                {
                    //Actually voxelize it
                    _voxel = new VehicleVoxel(_vehiclePartList, _currentGeoModules, _voxelCount);
                    if (_vehicleCrossSection.Length < _voxel.MaxArrayLength)
                        _vehicleCrossSection = _voxel.EmptyCrossSectionArray;

                    _voxelLowerRightCorner = _voxel.LocalLowerRightCorner;
                    _voxelElementSize = _voxel.ElementSize;

                    CalculateVesselAeroProperties();
                    _calculationCompleted = true;

                    //                    voxelizing = false;
                }

                catch (Exception e)
                {
                    ThreadSafeDebugLogger.Instance.RegisterException(e);
                }
                finally
                {
                    //Always, when we finish up, if we're in flight, cleanup the voxel
                    if (HighLogic.LoadedSceneIsFlight && _voxel != null)
                    {
                        _voxel.CleanupVoxel();
                        _voxel = null;
                    }
                    //And unset the flag so that the main thread can queue it again
                    voxelizing = false;
                }
            }
        }

        #endregion


        private Vector3 CalculateVehicleMainAxis()
        {
            Vector3 axis = Vector3.zero;
            //Vector3 notAxis = Vector3.zero;
            HashSet<Part> hitParts = new HashSet<Part>();

            bool hasPartsForAxis = false;

            for (int i = 0; i < _vehiclePartList.Count; i++)
            {
                Part p = _vehiclePartList[i];

                if (p == null || hitParts.Contains(p))
                    continue;

                GeometryPartModule geoModule = null;

                if (p.Modules.Contains("GeometryPartModule"))
                    geoModule = (GeometryPartModule)p.Modules["GeometryPartModule"]; // Could be left null if a launch clamp

                hitParts.Add(p);

                Vector3 tmpCandVector = Vector3.zero;
                Vector3 candVector = Vector3.zero;

                if (p.Modules.Contains("ModuleResourceIntake"))      //intakes are probably pointing in the direction we're gonna be going in
                {
                    ModuleResourceIntake intake = (ModuleResourceIntake)p.Modules["ModuleResourceIntake"];
                    Transform intakeTrans = p.FindModelTransform(intake.intakeTransformName);
                    if ((object)intakeTrans != null)
                        candVector = intakeTrans.TransformDirection(Vector3.forward);
                }
                else if (geoModule == null || geoModule.IgnoreForMainAxis || p.Modules.Contains("FARWingAerodynamicModel") || p.Modules.Contains("FARControllableSurface"))      //aggregate wings for later calc...
                {
                    continue;
                }
                else
                {
                    if (p.srfAttachNode != null && p.srfAttachNode.attachedPart != null)// && p.attachRules.allowSrfAttach)
                    {
                        tmpCandVector = p.srfAttachNode.orientation;
                        tmpCandVector = new Vector3(0, Math.Abs(tmpCandVector.x) + Math.Abs(tmpCandVector.z), Math.Abs(tmpCandVector.y));

                        if (p.srfAttachNode.position.sqrMagnitude == 0 && tmpCandVector == Vector3.forward)
                            tmpCandVector = Vector3.up;

                        if (tmpCandVector.z > tmpCandVector.x && tmpCandVector.z > tmpCandVector.y)
                            tmpCandVector = Vector3.forward;
                        else if (tmpCandVector.y > tmpCandVector.x && tmpCandVector.y > tmpCandVector.z)
                            tmpCandVector = Vector3.up;
                        else
                            tmpCandVector = Vector3.right;
                    }
                    else
                    {
                        tmpCandVector = Vector3.up;
                    }
                    candVector = p.partTransform.TransformDirection(tmpCandVector);
                }

                for (int j = 0; j < p.symmetryCounterparts.Count; j++)
                {
                    Part q = p.symmetryCounterparts[j];

                    if (q == null || hitParts.Contains(q))
                        continue;

                    hitParts.Add(q);

                    if (q.Modules.Contains("ModuleResourceIntake"))      //intakes are probably pointing in the direction we're gonna be going in
                    {
                        ModuleResourceIntake intake = (ModuleResourceIntake)q.Modules["ModuleResourceIntake"];
                        Transform intakeTrans = q.FindModelTransform(intake.intakeTransformName);
                        if ((object)intakeTrans != null)
                            candVector += intakeTrans.TransformDirection(Vector3.forward);
                    }
                    else
                        candVector += q.partTransform.TransformDirection(tmpCandVector);
                }
                hasPartsForAxis = true;     //set that we will get a valid axis out of this operation

                candVector = _worldToLocalMatrix.MultiplyVector(candVector);
                candVector.x = Math.Abs(candVector.x);
                candVector.y = Math.Abs(candVector.y);
                candVector.z = Math.Abs(candVector.z);

                Vector3 size = geoModule.overallMeshBounds.size;

                axis += candVector * size.x * size.y * size.z;// *(1 + p.symmetryCounterparts.Count);    //scale part influence by approximate size
            }

            if (hasPartsForAxis)
            {
                float dotProdX, dotProdY, dotProdZ;

                dotProdX = Math.Abs(Vector3.Dot(axis, Vector3.right));
                dotProdY = Math.Abs(Vector3.Dot(axis, Vector3.up));
                dotProdZ = Math.Abs(Vector3.Dot(axis, Vector3.forward));

                if (dotProdY > 2 * dotProdX && dotProdY > 2 * dotProdZ)
                    return Vector3.up;

                if (dotProdX > 2 * dotProdY && dotProdX > 2 * dotProdZ)
                    return Vector3.right;

                if (dotProdZ > 2 * dotProdX && dotProdZ > 2 * dotProdY)
                    return Vector3.forward;

                //Otherwise, now we need to use axis, since it's obviously not close to anything else

                return axis.normalized;
            }
            else
                return Vector3.up;      //welp, no parts that we can rely on for determining the axis; fall back to up
        }

        //Smooths out area and area 2nd deriv distributions to deal with noise in the representation
        unsafe void GaussianSmoothCrossSections(VoxelCrossSection[] vehicleCrossSection, double stdDevCutoff, double lengthPercentFactor, double sectionThickness, double length, int frontIndex, int backIndex, int areaSmoothingIterations, int derivSmoothingIterations)
        {
            double stdDev = length * lengthPercentFactor;
            int numVals = (int)Math.Ceiling(stdDevCutoff * stdDev / sectionThickness);

            if (numVals <= 1)
                return;

            double* gaussianFactors = stackalloc double[numVals];
            double* prevUncorrectedVals = stackalloc double[numVals];
            double* futureUncorrectedVals = stackalloc double[numVals - 1];


/*            double[] gaussianFactors = new double[numVals];
            double[] prevUncorrectedVals = new double[numVals];
            double[] futureUncorrectedVals = new double[numVals - 1];*/

            double invVariance = 1 / (stdDev * stdDev);

            //calculate Gaussian factors for each of the points that will be hit
            for (int i = 0; i < numVals; i++)
            {
                double factor = (i * sectionThickness);
                factor *= factor;
                gaussianFactors[i] = Math.Exp(-0.5 * factor * invVariance);
            }

            //then sum them up...
            double sum = 0;
            for (int i = 0; i < numVals; i++)
                if (i == 0)
                    sum += gaussianFactors[i];
                else
                    sum += 2 * gaussianFactors[i];

            double invSum = 1 / sum;    //and then use that to normalize the factors

            for (int i = 0; i < numVals; i++)
            {
                gaussianFactors[i] *= invSum;
            }

            //first smooth the area itself.  This has a greater effect on the 2nd deriv due to the effect of noise on derivatives
            for (int j = 0; j < areaSmoothingIterations; j++)
            {
                for (int i = 0; i < numVals; i++)
                    prevUncorrectedVals[i] = vehicleCrossSection[frontIndex].area;     //set all the vals to 0 to prevent screwups between iterations

                for (int i = frontIndex; i <= backIndex; i++)       //area smoothing pass
                {
                    for (int k = numVals - 1; k > 0; k--)
                    {
                        prevUncorrectedVals[k] = prevUncorrectedVals[k - 1];        //shift prev vals down
                    }
                    double curValue = vehicleCrossSection[i].area;
                    prevUncorrectedVals[0] = curValue;       //and set the central value


                    for (int k = 0; k < numVals - 1; k++)          //update future vals
                    {
                        if (i + k < backIndex)
                            futureUncorrectedVals[k] = vehicleCrossSection[i + k + 1].area;
                        else
                            futureUncorrectedVals[k] = vehicleCrossSection[backIndex].area;
                    }
                    curValue = 0;       //zero for coming calculations...

                    double borderScaling = 1;      //factor to correct for the 0s lurking at the borders of the curve...

                    for (int k = 0; k < numVals; k++)
                    {
                        double val = prevUncorrectedVals[k];
                        double gaussianFactor = gaussianFactors[k];

                        curValue += gaussianFactor * val;        //central and previous values;
                        if (val == 0)
                            borderScaling -= gaussianFactor;
                    }
                    for (int k = 0; k < numVals - 1; k++)
                    {
                        double val = futureUncorrectedVals[k];
                        double gaussianFactor = gaussianFactors[k + 1];

                        curValue += gaussianFactor * val;      //future values
                        if (val == 0)
                            borderScaling -= gaussianFactor;
                    }
                    if (borderScaling > 0)
                        curValue /= borderScaling;      //and now all of the 0s beyond the edge have been removed

                    vehicleCrossSection[i].area = curValue;
                }
            }

            CalculateCrossSectionSecondDerivs(vehicleCrossSection, numVals, frontIndex, backIndex, sectionThickness);

            //and now smooth the derivs
            for (int j = 0; j < derivSmoothingIterations; j++)
            {
                for (int i = 0; i < numVals; i++)
                    prevUncorrectedVals[i] = vehicleCrossSection[frontIndex].secondAreaDeriv;     //set all the vals to 0 to prevent screwups between iterations

                for (int i = frontIndex; i <= backIndex; i++)       //deriv smoothing pass
                {
                    for (int k = numVals - 1; k > 0; k--)
                    {
                        prevUncorrectedVals[k] = prevUncorrectedVals[k - 1];        //shift prev vals down
                    }
                    double curValue = vehicleCrossSection[i].secondAreaDeriv;
                    prevUncorrectedVals[0] = curValue;       //and set the central value


                    for (int k = 0; k < numVals - 1; k++)          //update future vals
                    {
                        if (i + k < backIndex)
                            futureUncorrectedVals[k] = vehicleCrossSection[i + k + 1].secondAreaDeriv;
                        else
                            futureUncorrectedVals[k] = vehicleCrossSection[backIndex].secondAreaDeriv;
                    }
                    curValue = 0;       //zero for coming calculations...

                    double borderScaling = 1;      //factor to correct for the 0s lurking at the borders of the curve...

                    for (int k = 0; k < numVals; k++)
                    {
                        double val = prevUncorrectedVals[k];
                        double gaussianFactor = gaussianFactors[k];

                        curValue += gaussianFactor * val;        //central and previous values;
                        if (val == 0)
                            borderScaling -= gaussianFactor;
                    }
                    for (int k = 0; k < numVals - 1; k++)
                    {
                        double val = futureUncorrectedVals[k];
                        double gaussianFactor = gaussianFactors[k + 1];

                        curValue += gaussianFactor * val;      //future values
                        if (val == 0)
                            borderScaling -= gaussianFactor;
                    }
                    if (borderScaling > 0)
                        curValue /= borderScaling;      //and now all of the 0s beyond the edge have been removed

                    vehicleCrossSection[i].secondAreaDeriv = curValue;
                }
            }
        }

        //Based on http://www.holoborodko.com/pavel/downloads/NoiseRobustSecondDerivative.pdf
        unsafe void CalculateCrossSectionSecondDerivs(VoxelCrossSection[] vehicleCrossSection, int oneSidedFilterLength, int frontIndex, int backIndex, double sectionThickness)
        {
            int M, N;

            if (oneSidedFilterLength < 2)
            {
                oneSidedFilterLength = 2;
                ThreadSafeDebugLogger.Instance.RegisterMessage("Needed to adjust filter length up");
            }
            else if (oneSidedFilterLength > 40)
            {
                oneSidedFilterLength = 40;
                ThreadSafeDebugLogger.Instance.RegisterMessage("Reducing filter length to prevent overflow");
            }

            M = oneSidedFilterLength;
            N = M * 2 + 1;
            long* sK = stackalloc long[M + 1];
            //double* areas = stackalloc double[N + 2];

            for (int i = 0; i <= M; i++)
            {
                sK[i] = CalculateSk(i, M, N);
            }
            double denom = Math.Pow(2, N - 3);
            denom *= sectionThickness * sectionThickness;
            denom = 1 / denom;

            int lowIndex, highIndex;
            lowIndex = Math.Max(frontIndex - 1, 0);
            highIndex = Math.Min(backIndex + 1, vehicleCrossSection.Length - 1);

            for (int i = lowIndex + M; i <= highIndex - M; i++)
            {

                double secondDeriv = 0;
                if(i >= frontIndex && i <= backIndex)
                    secondDeriv = sK[0] * vehicleCrossSection[i].area;

                for (int k = 1; k <= M; k++)
                {
                    double forwardArea, backwardArea;

                    if (i + k <= backIndex)
                        backwardArea = vehicleCrossSection[i + k].area;
                    else
                        backwardArea = 0;// (vehicleCrossSection[backIndex].area - vehicleCrossSection[backIndex - 1].area) * (i + k - backIndex) + vehicleCrossSection[backIndex].area;

                    if (i - k >= frontIndex)
                        forwardArea = vehicleCrossSection[i - k].area;
                    else
                        forwardArea = 0;// (vehicleCrossSection[frontIndex].area - vehicleCrossSection[frontIndex + 1].area) * (i - k - frontIndex) + vehicleCrossSection[frontIndex].area; ;// vehicleCrossSection[frontIndex].area;

                    secondDeriv += sK[k] * (forwardArea + backwardArea);
                }

                vehicleCrossSection[i].secondAreaDeriv = secondDeriv * denom;
                //ThreadSafeDebugLogger.Instance.RegisterMessage(vehicleCrossSection[i].secondAreaDeriv.ToString());
            }
            //forward difference
            for (int i = frontIndex; i < lowIndex + M; i++)
            {
                double secondDeriv = 0;

                secondDeriv += vehicleCrossSection[i].area;
                if(i + 2 <= backIndex)
                    secondDeriv += vehicleCrossSection[i + 2].area;
                if (i + 1 <= backIndex)
                    secondDeriv -= 2 * vehicleCrossSection[i + 1].area;

                secondDeriv /= sectionThickness * sectionThickness;

                vehicleCrossSection[i].secondAreaDeriv = secondDeriv;
            }
            //backward difference
            for (int i = highIndex - M + 1; i <= backIndex; i++)
            {
                double secondDeriv = 0;

                secondDeriv += vehicleCrossSection[i].area;
                if (i - 2 >= frontIndex)
                    secondDeriv += vehicleCrossSection[i - 2].area;
                if (i - 1 >= frontIndex)
                    secondDeriv -= 2 * vehicleCrossSection[i - 1].area;

                secondDeriv /= sectionThickness * sectionThickness;

                vehicleCrossSection[i].secondAreaDeriv = secondDeriv;
            }
        }

        long CalculateSk(long k, int M, int N)
        {
            if (k > M)
                return 0;
            if (k == M)
                return 1;
            long val = (2 * N - 10) * CalculateSk(k + 1, M, N);
            val -= (N + 2 * k + 3) * CalculateSk(k + 2, M, N);
            val /= (N - 2 * k - 1);

            return val;
        }

        unsafe void AdjustCrossSectionForAirDucting(VoxelCrossSection[] vehicleCrossSection, List<GeometryPartModule> geometryModules, int front, int back, ref double maxCrossSectionArea)
        {

            //double* areaAdjustment = stackalloc double[vehicleCrossSection.Length];

            for (int i = 0; i < geometryModules.Count; i++)
            {
                GeometryPartModule g = geometryModules[i];
                g.GetICrossSectionAdjusters(activeAdjusters, _worldToLocalMatrix, _vehicleMainAxis);
            }

            double intakeArea = 0;
            double engineExitArea = 0;

            for (int i = 0; i < activeAdjusters.Count; i++)    //get all forward facing engines / intakes
            {
                ICrossSectionAdjuster adjuster = activeAdjusters[i];
                if (adjuster is AirbreathingEngineCrossSectonAdjuster)
                    engineExitArea += Math.Abs(adjuster.AreaRemovedFromCrossSection());
                if (adjuster is IntakeCrossSectionAdjuster)
                    intakeArea += Math.Abs(adjuster.AreaRemovedFromCrossSection());
                if(adjuster is IntegratedIntakeEngineCrossSectionAdjuster)
                {
                    engineExitArea += Math.Abs(adjuster.AreaRemovedFromCrossSection());
                    intakeArea += Math.Abs(adjuster.AreaRemovedFromCrossSection());
                }
            }

            //ThreadSafeDebugLogger.Instance.RegisterMessage(intakeArea + " " + engineExitArea);
            if (intakeArea != 0 && engineExitArea != 0)        //if they exist, go through the calculations
            {
                if (_ductedAreaAdjustment.Length != vehicleCrossSection.Length)
                    _ductedAreaAdjustment = new double[vehicleCrossSection.Length];


                int frontMostIndex = -1, backMostIndex = -1;

                //sweep through entire vehicle
                for (int i = 0; i < _ductedAreaAdjustment.Length; i++)
                {
                    double ductedArea = 0;      //area based on the voxel size
                    //double actualArea = 0;      //area based on intake and engine data
                    double voxelCountScale = _voxelElementSize * _voxelElementSize;
                    //and all the intakes / engines
                    if (i >= front && i <= back)
                    {
                        for (int j = 0; j < activeAdjusters.Count; j++)
                        {
                            ICrossSectionAdjuster adjuster = activeAdjusters[j];

                            if (adjuster.IntegratedCrossSectionIncreaseDecrease())
                                continue;

                            if (adjuster.AreaRemovedFromCrossSection() == 0)
                                continue;

                            VoxelCrossSection.SideAreaValues val;
                            Part p = adjuster.GetPart();

                            //see if you can find that in this section
                            if (vehicleCrossSection[i].partSideAreaValues.TryGetValue(p, out val))
                            {
                                if (adjuster.AreaRemovedFromCrossSection() > 0)
                                {
                                    //actualArea += adjuster.AreaRemovedFromCrossSection();
                                    ductedArea += Math.Max(0, val.crossSectionalAreaCount * voxelCountScale + adjuster.AreaThreshold());
                                }
                                else
                                {
                                    //actualArea -= adjuster.AreaRemovedFromCrossSection();
                                    ductedArea -= Math.Max(0, val.crossSectionalAreaCount * voxelCountScale + adjuster.AreaThreshold());
                                }
                            }
                        }

                        ductedArea *= 0.75;

                        //if (Math.Abs(actualArea) < Math.Abs(ductedArea))
                        //    ductedArea = actualArea;

                        if (ductedArea != 0)
                            if (frontMostIndex < 0)
                                frontMostIndex = i;
                            else
                                backMostIndex = i;
                    }

                    _ductedAreaAdjustment[i] = ductedArea;
                }

                double tmpArea = _ductedAreaAdjustment[0];

                for (int i = 1; i < _ductedAreaAdjustment.Length; i++)
                {
                    double areaAdjustment = _ductedAreaAdjustment[i];
                    double prevAreaAdjustment = tmpArea;

                    tmpArea = areaAdjustment;       //store for next iteration

                    if (areaAdjustment > 0 && prevAreaAdjustment > 0)
                    {
                        double areaChange = areaAdjustment - prevAreaAdjustment;
                        if (areaChange > 0)
                            _ductedAreaAdjustment[i] = areaChange;     //this transforms this into a change in area, but only for increases (intakes)
                        else
                        {
                            tmpArea = prevAreaAdjustment;
                            _ductedAreaAdjustment[i] = 0;
                        }
                    }
                        
                }

                tmpArea = _ductedAreaAdjustment[_ductedAreaAdjustment.Length - 1];

                for (int i = _ductedAreaAdjustment.Length - 1; i >= 0; i--)
                {
                    double areaAdjustment = _ductedAreaAdjustment[i];
                    double prevAreaAdjustment = tmpArea;

                    tmpArea = areaAdjustment;       //store for next iteration

                    if (areaAdjustment < 0 && prevAreaAdjustment < 0)
                    {
                        double areaChange = areaAdjustment - prevAreaAdjustment;
                        if (areaChange < 0)
                            _ductedAreaAdjustment[i] = areaChange;     //this transforms this into a change in area, but only for decreases (engines)
                        else
                        {
                            tmpArea = prevAreaAdjustment;
                            _ductedAreaAdjustment[i] = 0;
                        }
                    }
                } 
                
                for (int i = _ductedAreaAdjustment.Length - 1; i >= 0; i--)
                {
                    double areaAdjustment = 0;
                    for (int j = 0; j <= i; j++)
                        areaAdjustment += _ductedAreaAdjustment[j];

                    _ductedAreaAdjustment[i] = areaAdjustment;
                    //ThreadSafeDebugLogger.Instance.RegisterMessage(areaAdjustment.ToString());
                }

                for (int i = 0; i < vehicleCrossSection.Length; i++)
                {
                    double ductedArea = 0;      //area based on the voxel size
                    double actualArea = 0;      //area based on intake and engine data

                    //and all the intakes / engines
                    for (int j = 0; j < activeAdjusters.Count; j++)
                    {
                        ICrossSectionAdjuster adjuster = activeAdjusters[j];

                        if (!adjuster.IntegratedCrossSectionIncreaseDecrease())
                            continue;

                        VoxelCrossSection.SideAreaValues val;
                        Part p = adjuster.GetPart();

                        //see if you can find that in this section
                        if (vehicleCrossSection[i].partSideAreaValues.TryGetValue(p, out val))
                        {
                            ductedArea += val.crossSectionalAreaCount;
                            actualArea += adjuster.AreaRemovedFromCrossSection();
                        }
                        //ThreadSafeDebugLogger.Instance.RegisterMessage(ductedArea.ToString());
                    }

                    ductedArea *= _voxelElementSize * _voxelElementSize * 0.75;

                    if (Math.Abs(actualArea) < Math.Abs(ductedArea))
                        ductedArea = actualArea;

                    if (ductedArea != 0)
                        if (i < frontMostIndex)
                            frontMostIndex = i;
                        else if (i > backMostIndex)
                            backMostIndex = i;

                    _ductedAreaAdjustment[i] += ductedArea;
                }

                int index = _ductedAreaAdjustment.Length - 1;
                double endVoxelArea = _ductedAreaAdjustment[index];

                double currentArea = endVoxelArea;

                while(currentArea > 0)
                {
                    currentArea -= endVoxelArea;
                    _ductedAreaAdjustment[index] = currentArea;

                    --index;

                    if (index < 0)
                        break;

                    currentArea = _ductedAreaAdjustment[index];
                }

                maxCrossSectionArea = 0;
                //put upper limit on area lost
                for (int i = 0; i < vehicleCrossSection.Length; i++)
                {
                    double areaUnchanged = vehicleCrossSection[i].area;
                    double areaChanged = -_ductedAreaAdjustment[i];
                    if (areaChanged > 0)
                        areaChanged = 0;
                    areaChanged += areaUnchanged;

                    double tmpTotalArea = Math.Max(0.15 * areaUnchanged, areaChanged);
                    if (tmpTotalArea > maxCrossSectionArea)
                        maxCrossSectionArea = tmpTotalArea;

                    vehicleCrossSection[i].area = tmpTotalArea;

                }
                
            }
            activeAdjusters.Clear();
        }

        #region Aerodynamics Calculations

        private void CalculateVesselAeroProperties()
        {
            int front, back, numSections;

            _voxel.CrossSectionData(_vehicleCrossSection, _vehicleMainAxis, out front, out back, out _sectionThickness, out _maxCrossSectionArea);

            numSections = back - front;
            _length = _sectionThickness * numSections;

            double voxelVolume = _voxel.Volume;

            double filledVolume = 0;
            for (int i = front; i <= back; i++)
                filledVolume += _vehicleCrossSection[i].area;

            filledVolume *= _sectionThickness;      //total volume taken up by the filled voxel

            double gridFillednessFactor = filledVolume / voxelVolume;     //determines how fine the grid is compared to the vehicle.  Accounts for loss in precision and added smoothing because of unused sections of voxel volume

            gridFillednessFactor *= 25;     //used to handle relatively empty, but still alright, planes
            double stdDevCutoff = 3;
            stdDevCutoff *= gridFillednessFactor;
            if (stdDevCutoff < 0.5)
                stdDevCutoff = 0.5;
            if (stdDevCutoff > 3)
                stdDevCutoff = 3;

            double invMaxRadFactor = 1f / Math.Sqrt(_maxCrossSectionArea / Math.PI);

            double finenessRatio = _sectionThickness * numSections * 0.5 * invMaxRadFactor;       //vehicle length / max diameter, as calculated from sect thickness * num sections / (2 * max radius) 

            int extraLowFinessRatioDerivSmoothingPasses = (int)Math.Round((5f - finenessRatio) * 0.5f) * FARSettingsScenarioModule.Settings.numDerivSmoothingPasses;
            if (extraLowFinessRatioDerivSmoothingPasses < 0)
                extraLowFinessRatioDerivSmoothingPasses = 0;

            int extraAreaSmoothingPasses = (int)Math.Round((gridFillednessFactor / 25.0 - 0.5) * 4.0);
            if (extraAreaSmoothingPasses < 0)
                extraAreaSmoothingPasses = 0;


            ThreadSafeDebugLogger.Instance.RegisterMessage("Std dev for smoothing: " + stdDevCutoff + " voxel total vol: " + voxelVolume + " filled vol: " + filledVolume);

            AdjustCrossSectionForAirDucting(_vehicleCrossSection, _currentGeoModules, front, back, ref _maxCrossSectionArea);

            GaussianSmoothCrossSections(_vehicleCrossSection, stdDevCutoff, FARSettingsScenarioModule.Settings.gaussianVehicleLengthFractionForSmoothing, _sectionThickness, _length, front, back, FARSettingsScenarioModule.Settings.numAreaSmoothingPasses + extraAreaSmoothingPasses, FARSettingsScenarioModule.Settings.numDerivSmoothingPasses + extraLowFinessRatioDerivSmoothingPasses);

            CalculateSonicPressure(_vehicleCrossSection, front, back, _sectionThickness, _maxCrossSectionArea);

            validSectionCount = numSections;
            firstSection = front;

            //recalc these with adjusted cross-sections
            invMaxRadFactor = 1f / Math.Sqrt(_maxCrossSectionArea / Math.PI);

            finenessRatio = _sectionThickness * numSections * 0.5 * invMaxRadFactor;       //vehicle length / max diameter, as calculated from sect thickness * num sections / (2 * max radius) 

            //skin friction and pressure drag for a body, taken from 1978 USAF Stability And Control DATCOM, Section 4.2.3.1, Paragraph A
            double viscousDragFactor = 0;
            viscousDragFactor = 60 / (finenessRatio * finenessRatio * finenessRatio) + 0.0025 * finenessRatio;     //pressure drag for a subsonic / transonic body due to skin friction
            viscousDragFactor++;

            viscousDragFactor /= (double)numSections;   //fraction of viscous drag applied to each section

            double criticalMachNumber = CalculateCriticalMachNumber(finenessRatio);

            _criticalMach = criticalMachNumber * CriticalMachFactorForUnsmoothCrossSection(_vehicleCrossSection, finenessRatio, _sectionThickness);

            float lowFinenessRatioFactor = 1f;
            lowFinenessRatioFactor += 1f/(1 + 0.5f * (float)finenessRatio);
            float lowFinenessRatioBlendFactor = lowFinenessRatioFactor--;

            _moduleAndAreasDict.Clear();
            //_newAeroSections = new List<FARAeroSection>();

            HashSet<FARAeroPartModule> tmpAeroModules = new HashSet<FARAeroPartModule>();
            _sonicDragArea = 0;

            if (_newAeroSections.Capacity < numSections + 1)
                _newAeroSections.Capacity = numSections + 1;

            int aeroSectionIndex = 0;
            FARAeroSection prevSection = null;

            for (int i = 0; i <= numSections; i++)  //index in the cross sections
            {
                int index = i + front;      //index along the actual body

                double prevArea, curArea, nextArea;

                curArea = _vehicleCrossSection[index].area;
                if (i == 0)
                    prevArea = 0;
                else
                    prevArea = _vehicleCrossSection[index - 1].area;
                if (i == numSections)
                    nextArea = 0;
                else
                    nextArea = _vehicleCrossSection[index + 1].area;

                FARAeroSection currentSection = null;

                if (aeroSectionIndex < _newAeroSections.Count)
                    currentSection = _newAeroSections[aeroSectionIndex];
                else
                {
                    lock (_commonLocker)
                        if (currentlyUnusedSections.Count > 0)
                            currentSection = currentlyUnusedSections.Pop();
                }

                if (currentSection == null)
                    currentSection = new FARAeroSection();

                FARFloatCurve xForcePressureAoA0 = currentSection.xForcePressureAoA0;
                FARFloatCurve xForcePressureAoA180 = currentSection.xForcePressureAoA180;
                FARFloatCurve xForceSkinFriction = currentSection.xForceSkinFriction;

                //Potential and Viscous lift calcs
                float potentialFlowNormalForce;
                if(i == 0)
                    potentialFlowNormalForce = (float)(nextArea - curArea);
                else if(i == numSections)
                    potentialFlowNormalForce = (float)(curArea - prevArea);
                else
                    potentialFlowNormalForce = (float)(nextArea - prevArea) * 0.5f;      //calcualted from area change

                float areaChangeMax = (float)Math.Min(Math.Min(nextArea, prevArea) * 0.1, _length * 0.01);

                float sonicBaseDrag = 0.21f;

                sonicBaseDrag *= potentialFlowNormalForce;    //area base drag acts over

                if (potentialFlowNormalForce > areaChangeMax)
                    potentialFlowNormalForce = areaChangeMax;
                else if (potentialFlowNormalForce < -areaChangeMax)
                    potentialFlowNormalForce = -areaChangeMax;
                else
                    if(areaChangeMax != 0)
                        sonicBaseDrag *= Math.Abs(potentialFlowNormalForce / areaChangeMax);      //some scaling for small changes in cross-section

                double flatnessRatio = _vehicleCrossSection[index].flatnessRatio;
                if (flatnessRatio >= 1)
                    sonicBaseDrag /= (float)(flatnessRatio * flatnessRatio);
                else
                    sonicBaseDrag *= (float)(flatnessRatio * flatnessRatio);


                //float sonicWaveDrag = (float)CalculateTransonicWaveDrag(i, index, numSections, front, _sectionThickness, Math.Min(_maxCrossSectionArea * 2, curArea * 16));//Math.Min(maxCrossSectionArea * 0.1, curArea * 0.25));
                //sonicWaveDrag *= (float)FARSettingsScenarioModule.Settings.fractionTransonicDrag;     //this is just to account for the higher drag being felt due to the inherent blockiness of the model being used and noise introduced by the limited control over shape and through voxelization
                float hypersonicDragForward = (float)CalculateHypersonicDrag(prevArea, curArea, _sectionThickness);     //negative forces
                float hypersonicDragBackward = (float)CalculateHypersonicDrag(nextArea, curArea, _sectionThickness);

                float hypersonicDragForwardFrac = 0, hypersonicDragBackwardFrac = 0;

                if(curArea - prevArea != 0)
                    hypersonicDragForwardFrac = Math.Abs(hypersonicDragForward * 0.5f / (float)(curArea - prevArea));
                if(curArea - nextArea != 0)
                    hypersonicDragBackwardFrac = Math.Abs(hypersonicDragBackward * 0.5f / (float)(curArea - nextArea));

                hypersonicDragForwardFrac *= hypersonicDragForwardFrac;     //^2
                hypersonicDragForwardFrac *= hypersonicDragForwardFrac;     //^4
                //hypersonicDragForwardFrac *= hypersonicDragForwardFrac;     //^8
                //hypersonicDragForwardFrac *= hypersonicDragForwardFrac;     //^16
                //hypersonicDragForwardFrac *= hypersonicDragForwardFrac;     //^32

                hypersonicDragBackwardFrac *= hypersonicDragBackwardFrac;     //^2
                hypersonicDragBackwardFrac *= hypersonicDragBackwardFrac;     //^4
                //hypersonicDragBackwardFrac *= hypersonicDragBackwardFrac;     //^8
                //hypersonicDragBackwardFrac *= hypersonicDragBackwardFrac;     //^16
                //hypersonicDragBackwardFrac *= hypersonicDragBackwardFrac;     //^32

                if (flatnessRatio >= 1)
                {
                    hypersonicDragForwardFrac /= (float)(flatnessRatio * flatnessRatio);
                    hypersonicDragBackwardFrac /= (float)(flatnessRatio * flatnessRatio);
                }
                else
                {
                    hypersonicDragForwardFrac *= (float)(flatnessRatio * flatnessRatio);
                    hypersonicDragBackwardFrac *= (float)(flatnessRatio * flatnessRatio);
                }

                /*if (hypersonicDragForwardFrac > 1)
                    hypersonicDragForwardFrac = 1;
                if (hypersonicDragBackwardFrac > 1)
                    hypersonicDragBackwardFrac = 1;*/

                float hypersonicMomentForward = (float)CalculateHypersonicMoment(prevArea, curArea, _sectionThickness);
                float hypersonicMomentBackward = (float)CalculateHypersonicMoment(nextArea, curArea, _sectionThickness);


                xForcePressureAoA0.SetPoint(5, new Vector3d(35, hypersonicDragForward, 0));
                xForcePressureAoA180.SetPoint(5, new Vector3d(35, -hypersonicDragBackward, 0));

                float sonicAoA0Drag, sonicAoA180Drag;

                double cPSonicForward, cPSonicBackward;

                cPSonicForward = _vehicleCrossSection[index].cpSonicForward;
                if (index > front)
                {
                    cPSonicForward += _vehicleCrossSection[index - 1].cpSonicForward;
                    cPSonicForward *= 0.5;
                }

                cPSonicBackward = _vehicleCrossSection[index].cpSonicBackward;
                if (index < back)
                {
                    cPSonicBackward += _vehicleCrossSection[index + 1].cpSonicBackward;
                    cPSonicBackward *= 0.5;
                }

                if (sonicBaseDrag > 0)      //occurs with increase in area; force applied at 180 AoA
                {
                    xForcePressureAoA0.SetPoint(0, new Vector3d(_criticalMach, (0.325f * hypersonicDragForward * hypersonicDragForwardFrac) * lowFinenessRatioFactor, 0));    //hypersonic drag used as a proxy for effects due to flow separation
                    xForcePressureAoA180.SetPoint(0, new Vector3d(_criticalMach, (sonicBaseDrag * 0.2f - (0.325f * hypersonicDragBackward * hypersonicDragBackwardFrac)) * lowFinenessRatioFactor, 0));


                    hypersonicDragBackwardFrac += 1f;       //avg fracs with 1 to get intermediate frac
                    hypersonicDragBackwardFrac *= 0.5f;

                    hypersonicDragForwardFrac += 1f;
                    hypersonicDragForwardFrac *= 0.5f;

                    sonicAoA0Drag = -(float)(cPSonicForward * (curArea - prevArea)) + 0.3f * hypersonicDragForward * hypersonicDragForwardFrac;
                    sonicAoA0Drag *= (1 - lowFinenessRatioBlendFactor);      //at high finenessRatios, use the entire above section for sonic drag
                    sonicAoA0Drag += hypersonicDragForward * hypersonicDragForwardFrac * lowFinenessRatioBlendFactor * 1.4f;     //at very low finenessRatios, use a boosted version of the hypersonic drag

                    sonicAoA180Drag = (float)(cPSonicBackward * (curArea - nextArea)) + sonicBaseDrag - 0.3f * hypersonicDragBackward * hypersonicDragBackwardFrac;
                    sonicAoA180Drag *= (1 - lowFinenessRatioBlendFactor);      //at high finenessRatios, use the entire above section for sonic drag
                    sonicAoA180Drag += (-hypersonicDragBackward * hypersonicDragBackwardFrac * 1.4f + sonicBaseDrag) * lowFinenessRatioBlendFactor;     //at very low finenessRatios, use a boosted version of the hypersonic drag
                    //if(i == 0)
                    //    sonicAoA180Drag += (float)(cPSonicBackward * (curArea)) + sonicBaseDrag - hypersonicDragBackward * 0.3f * hypersonicDragBackwardFrac;
                }
                else if (sonicBaseDrag < 0)
                {
                    xForcePressureAoA0.SetPoint(0, new Vector3d(_criticalMach, (sonicBaseDrag * 0.2f + (0.325f * hypersonicDragForward * hypersonicDragForwardFrac)) * lowFinenessRatioFactor, 0));
                    xForcePressureAoA180.SetPoint(0, new Vector3d(_criticalMach, -(0.325f * hypersonicDragBackward * hypersonicDragBackwardFrac) * lowFinenessRatioFactor, 0));

                    hypersonicDragBackwardFrac += 1f;       //avg fracs with 1 to get intermediate frac
                    hypersonicDragBackwardFrac *= 0.5f;

                    hypersonicDragForwardFrac += 1f;
                    hypersonicDragForwardFrac *= 0.5f;

                    sonicAoA0Drag = -(float)(cPSonicForward * (curArea - prevArea)) + sonicBaseDrag + 0.3f * hypersonicDragForward * hypersonicDragForwardFrac;
                    sonicAoA0Drag *= (1 - lowFinenessRatioBlendFactor);      //at high finenessRatios, use the entire above section for sonic drag
                    sonicAoA0Drag += (hypersonicDragForward * hypersonicDragForwardFrac * 1.4f + sonicBaseDrag) * lowFinenessRatioBlendFactor;     //at very low finenessRatios, use a boosted version of the hypersonic drag

                    sonicAoA180Drag = (float)(cPSonicBackward * (curArea - nextArea)) - 0.3f * hypersonicDragBackward * hypersonicDragBackwardFrac;
                    sonicAoA180Drag *= (1 - lowFinenessRatioBlendFactor);      //at high finenessRatios, use the entire above section for sonic drag
                    sonicAoA180Drag += (-hypersonicDragBackward * hypersonicDragBackwardFrac * 1.4f) * lowFinenessRatioBlendFactor;     //at very low finenessRatios, use a boosted version of the hypersonic drag

                    //if (i == numSections)
                    //    sonicAoA0Drag += -(float)(cPSonicForward * (-curArea)) + sonicBaseDrag + hypersonicDragForward * 0.3f * hypersonicDragForwardFrac;
                }
                else
                {
                    xForcePressureAoA0.SetPoint(0, new Vector3d(_criticalMach, (0.325f * hypersonicDragForward * hypersonicDragForwardFrac) * lowFinenessRatioFactor, 0));
                    xForcePressureAoA180.SetPoint(0, new Vector3d(_criticalMach, -(0.325f * hypersonicDragBackward * hypersonicDragBackwardFrac) * lowFinenessRatioFactor, 0));

                    hypersonicDragBackwardFrac += 1f;       //avg fracs with 1 to get intermediate frac
                    hypersonicDragBackwardFrac *= 0.5f;

                    hypersonicDragForwardFrac += 1f;
                    hypersonicDragForwardFrac *= 0.5f;

                    sonicAoA0Drag = -(float)(cPSonicForward * (curArea - prevArea)) + 0.3f * hypersonicDragForward * hypersonicDragForwardFrac;
                    sonicAoA0Drag *= (1 - lowFinenessRatioBlendFactor);      //at high finenessRatios, use the entire above section for sonic drag
                    sonicAoA0Drag += hypersonicDragForward * hypersonicDragForwardFrac * lowFinenessRatioBlendFactor * 1.4f;     //at very low finenessRatios, use a boosted version of the hypersonic drag

                    sonicAoA180Drag = (float)(cPSonicBackward * (curArea - nextArea)) - 0.3f * hypersonicDragBackward * hypersonicDragBackwardFrac;
                    sonicAoA180Drag *= (1 - lowFinenessRatioBlendFactor);      //at high finenessRatios, use the entire above section for sonic drag
                    sonicAoA180Drag += (-hypersonicDragBackward * hypersonicDragBackwardFrac * 1.4f) * lowFinenessRatioBlendFactor;     //at very low finenessRatios, use a boosted version of the hypersonic drag
                }
                float diffSonicHyperAoA0 = Math.Abs(sonicAoA0Drag) - Math.Abs(hypersonicDragForward);
                float diffSonicHyperAoA180 = Math.Abs(sonicAoA180Drag) - Math.Abs(hypersonicDragBackward);


                xForcePressureAoA0.SetPoint(1, new Vector3d(1f, sonicAoA0Drag, 0));
                xForcePressureAoA180.SetPoint(1, new Vector3d(1f, sonicAoA180Drag, 0));

                xForcePressureAoA0.SetPoint(2, new Vector3d(2f, sonicAoA0Drag * 0.5773503f + (1 - 0.5773503f) * hypersonicDragForward, -0.2735292 * diffSonicHyperAoA0));            //need to recalc slope here
                xForcePressureAoA180.SetPoint(2, new Vector3d(2f, sonicAoA180Drag * 0.5773503f - (1 - 0.5773503f) * hypersonicDragBackward, -0.2735292 * diffSonicHyperAoA180));

                xForcePressureAoA0.SetPoint(3, new Vector3d(5f, sonicAoA0Drag * 0.2041242f + (1 - 0.2041242f) * hypersonicDragForward, -0.04252587f * diffSonicHyperAoA0));
                xForcePressureAoA180.SetPoint(3, new Vector3d(5f, sonicAoA180Drag * 0.2041242f - (1 - 0.2041242f) * hypersonicDragBackward, -0.04252587f * diffSonicHyperAoA180));

                xForcePressureAoA0.SetPoint(4, new Vector3d(10f, sonicAoA0Drag * 0.1005038f + (1 - 0.1005038f) * hypersonicDragForward, -0.0101519f * diffSonicHyperAoA0));
                xForcePressureAoA180.SetPoint(4, new Vector3d(10f, sonicAoA180Drag * 0.1005038f - (1 - 0.1005038f) * hypersonicDragBackward, -0.0101519f * diffSonicHyperAoA180));

                Vector3 xRefVector;
                if (index == front || index == back)
                    xRefVector = _vehicleMainAxis;
                else
                {
                    xRefVector = (Vector3)(_vehicleCrossSection[index - 1].centroid - _vehicleCrossSection[index + 1].centroid);
                    Vector3 offMainAxisVec = Vector3.ProjectOnPlane(xRefVector, _vehicleMainAxis);
                    float tanAoA = offMainAxisVec.magnitude / (2f * (float)_sectionThickness);
                    if (tanAoA > 0.17632698070846497347109038686862f)
                    {
                        offMainAxisVec.Normalize();
                        offMainAxisVec *= 0.17632698070846497347109038686862f;      //max acceptable is 10 degrees
                        xRefVector = _vehicleMainAxis + offMainAxisVec;
                    }
                    xRefVector.Normalize();
                }


                Vector3 nRefVector = Matrix4x4.TRS(Vector3.zero, Quaternion.FromToRotation(_vehicleMainAxis, xRefVector), Vector3.one).MultiplyVector(_vehicleCrossSection[index].flatNormalVector);
                
                Vector3 centroid = _localToWorldMatrix.MultiplyPoint3x4(_vehicleCrossSection[index].centroid);
                xRefVector = _localToWorldMatrix.MultiplyVector(xRefVector);
                nRefVector = _localToWorldMatrix.MultiplyVector(nRefVector);

                Dictionary<Part, VoxelCrossSection.SideAreaValues> includedPartsAndAreas = _vehicleCrossSection[index].partSideAreaValues;

                float weightingFactor = 0;

                double surfaceArea = 0;
                foreach (KeyValuePair<Part, VoxelCrossSection.SideAreaValues> pair in includedPartsAndAreas)
                {
                    VoxelCrossSection.SideAreaValues areas = pair.Value;
                    surfaceArea += areas.iN + areas.iP + areas.jN + areas.jP + areas.kN + areas.kP;

                    Part key = pair.Key;
                    if (key == null)
                        continue;

                    if (!key.Modules.Contains("FARAeroPartModule"))
                        continue;

                    FARAeroPartModule m = (FARAeroPartModule)key.Modules["FARAeroPartModule"];
                    if ((object)m != null)
                        includedModules.Add(m);

                    if (_moduleAndAreasDict.ContainsKey(m))
                        _moduleAndAreasDict[m] += areas;
                    else
                        _moduleAndAreasDict[m] = areas;

                    weightingFactor += (float)pair.Value.exposedAreaCount;
                    weighting.Add((float)pair.Value.exposedAreaCount);
                }

                weightingFactor = 1 / weightingFactor;
                for (int j = 0; j < weighting.Count; j++)
                {
                    weighting[j] *= weightingFactor;
                }

                float viscCrossflowDrag = (float)(Math.Sqrt(curArea / Math.PI) * _sectionThickness * 2d);

                xForceSkinFriction.SetPoint(0, new Vector3d(0, (surfaceArea * viscousDragFactor), 0));   //subsonic incomp visc drag
                xForceSkinFriction.SetPoint(1, new Vector3d(1, (surfaceArea * viscousDragFactor), 0));   //transonic visc drag
                xForceSkinFriction.SetPoint(2, new Vector3d(2, (float)surfaceArea, 0));                     //above Mach 1.4, visc is purely surface drag, no pressure-related components simulated

                currentSection.UpdateAeroSection(potentialFlowNormalForce, viscCrossflowDrag
                    ,viscCrossflowDrag / (float)(_sectionThickness), (float)flatnessRatio, hypersonicMomentForward, hypersonicMomentBackward,
                    centroid, xRefVector, nRefVector, _localToWorldMatrix, _vehicleMainAxis, includedModules, weighting, _partWorldToLocalMatrixDict);


                if (prevSection != null && prevSection.CanMerge(currentSection))
                {
                    prevSection.MergeAeroSection(currentSection);
                    currentSection.ClearAeroSection();
                }
                else
                {
                    if (aeroSectionIndex < _newAeroSections.Count)
                        _newAeroSections[aeroSectionIndex] = currentSection;
                    else
                        _newAeroSections.Add(currentSection);

                    prevSection = currentSection;
                    ++aeroSectionIndex;
                }

                for (int j = 0; j < includedModules.Count; j++)
                {
                    FARAeroPartModule a = includedModules[j];
                    tmpAeroModules.Add(a);
                }

                includedModules.Clear();
                weighting.Clear();

            }
            if (_newAeroSections.Count > aeroSectionIndex + 1)        //deal with sections that are unneeded now
            {
                lock (_commonLocker)
                    for (int i = _newAeroSections.Count - 1; i > aeroSectionIndex; --i)
                    {
                        FARAeroSection unusedSection = _newAeroSections[i];
                        _newAeroSections.RemoveAt(i);

                        unusedSection.ClearAeroSection();
                        if (currentlyUnusedSections.Count < 64)
                            currentlyUnusedSections.Push(unusedSection);        //if there aren't that many extra ones stored, add them to the stack to be reused
                        else
                        {
                            unusedSection = null;
                        }
                    }
            }
            foreach (KeyValuePair<FARAeroPartModule, FARAeroPartModule.ProjectedArea> pair in _moduleAndAreasDict)
            {
                pair.Key.SetProjectedArea(pair.Value, _localToWorldMatrix);
            }

            //_newAeroModules = tmpAeroModules.ToList();        //this method creates lots of garbage
            int aeroIndex = 0;
            if (_newAeroModules.Capacity < tmpAeroModules.Count)
                _newAeroModules.Capacity = tmpAeroModules.Count;


            foreach(FARAeroPartModule module in tmpAeroModules)
            {
                if (aeroIndex < _newAeroModules.Count)
                    _newAeroModules[aeroIndex] = module;
                else
                    _newAeroModules.Add(module);
                ++aeroIndex;
            }
            //at this point, aeroIndex is what the count of _newAeroModules _should_ be, but due to the possibility of the previous state having more modules, this is not guaranteed
            for (int i = _newAeroModules.Count - 1; i >= aeroIndex; --i)
            {
                _newAeroModules.RemoveAt(i);        //steadily remove the modules from the end that shouldn't be there
            }

            _newUnusedAeroModules.Clear();

            for (int i = 0; i < _currentGeoModules.Count; i++)
            {
                if (!_currentGeoModules[i])
                    continue;

                FARAeroPartModule aeroModule = _currentGeoModules[i].GetComponent<FARAeroPartModule>();
                if (aeroModule != null && !tmpAeroModules.Contains(aeroModule))
                    _newUnusedAeroModules.Add(aeroModule);
            }
            //UpdateSonicDragArea();
        }

        public void UpdateSonicDragArea()
        {
            ferram4.FARCenterQuery center = new ferram4.FARCenterQuery();

            Vector3 worldMainAxis = _localToWorldMatrix.MultiplyVector(_vehicleMainAxis);
            worldMainAxis.Normalize();

            for (int i = 0; i < _newAeroSections.Count; i++)
            {
                FARAeroSection a = _newAeroSections[i];
                a.PredictionCalculateAeroForces(2f, 1f, 50000f, 0.005f, worldMainAxis, center);
            }

            _sonicDragArea = Vector3.Dot(center.force, worldMainAxis) * -1000;
        }

        private double CalculateHypersonicMoment(double lowArea, double highArea, double sectionThickness)
        {
            if(lowArea >= highArea)
                return 0;

            double r1, r2;
            r1 = Math.Sqrt(lowArea / Math.PI);
            r2 = Math.Sqrt(highArea / Math.PI);

            double moment = r2 * r2 + r1 * r1 + sectionThickness * sectionThickness * 0.5;
            moment *= 2 * Math.PI;

            double radDiffSq = (r2 - r1);
            radDiffSq *= radDiffSq;

            moment *= radDiffSq;
            moment /= sectionThickness * sectionThickness + radDiffSq;

            return -moment * sectionThickness;
        }

        #region AreaRulingCalculations

        private void CalculateSonicPressure(VoxelCrossSection[] vehicleCrossSection, int front, int back, double sectionThickness, double maxCrossSection)
        {
            lock (_commonLocker)
                if (vehicleCrossSection.Length > indexSqrt.Length + 1)
                    indexSqrt = GenerateIndexSqrtLookup(vehicleCrossSection.Length + 2);

            double machTest = 1.2;
            double beta = Math.Sqrt(machTest * machTest - 1);

            //double noseAreaSlope = (vehicleCrossSection[front].area) / sectionThickness;

            for (int i = front; i <= back; i++)
            {
                double cP = CalculateCpLinearForward(vehicleCrossSection, i, front, beta, sectionThickness, double.PositiveInfinity);
                //cP += CalculateCpNoseDiscont(i - front, noseAreaSlope, sectionThickness);

                cP *= -0.5;
                cP = AdjustVelForFinitePressure(cP);
                cP *= -2;
                if (cP < 0)
                    cP = AdjustCpForNonlinearEffects(cP, beta, machTest);

                vehicleCrossSection[i].cpSonicForward = cP;
            }

            //noseAreaSlope = (vehicleCrossSection[back].area) / sectionThickness;

            for (int i = back; i >= front; i--)
            {
                double cP = CalculateCpLinearBackward(vehicleCrossSection, i, back, beta, sectionThickness, double.PositiveInfinity);
                //cP += CalculateCpNoseDiscont(back - i, noseAreaSlope, sectionThickness);

                cP *= -0.5;
                cP = AdjustVelForFinitePressure(cP);
                cP *= -2;
                if (cP < 0)
                    cP = AdjustCpForNonlinearEffects(cP, beta, machTest);

                vehicleCrossSection[i].cpSonicBackward = cP;
            }
        }

        //Taken from Appendix A of NASA TR R-213
        private double CalculateCpLinearForward(VoxelCrossSection[] vehicleCrossSection, int index, int front, double beta, double sectionThickness, double maxCrossSection)
        {
            double cP = 0;

            double cutoff = maxCrossSection * 2;//Math.Min(maxCrossSection * 0.1, vehicleCrossSection[index].area * 0.25);

            double tmp1, tmp2;
            tmp1 = indexSqrt[index - (front - 2)];
            for (int i = front - 1; i <= index; i++)
            {
                double tmp;
                //tmp2 = Math.Sqrt(tmp);
                tmp2 = indexSqrt[index - i];
                tmp = tmp1 - tmp2;
                tmp1 = tmp2;

                if (i >= 0)
                    tmp *= MathClampAbs(vehicleCrossSection[i].secondAreaDeriv, cutoff);
                else
                    tmp *= 0;

                cP += tmp;
            }

            double avgArea = vehicleCrossSection[index].area;
            /*if(index > 0)
            {
                avgArea += vehicleCrossSection[index - 1].area;
            }
            avgArea *= 0.5;*/

            cP *= Math.Sqrt(0.5 * sectionThickness / (beta * Math.Sqrt(Math.PI * avgArea)));
            
            return cP;
        }

        private double CalculateCpLinearBackward(VoxelCrossSection[] vehicleCrossSection, int index, int back, double beta, double sectionThickness, double maxCrossSection)
        {
            double cP = 0;

            double cutoff = maxCrossSection * 2;//Math.Min(maxCrossSection * 0.1, vehicleCrossSection[index].area * 0.25);

            double tmp1, tmp2;
            tmp1 = indexSqrt[back + 2 - index];
            for (int i = back + 1; i >= index; i--)
            {
                double tmp;
                //tmp2 = Math.Sqrt(tmp);
                tmp2 = indexSqrt[i - index];
                tmp = tmp1 - tmp2;
                tmp1 = tmp2;

                if (i < vehicleCrossSection.Length)
                    tmp *= MathClampAbs(vehicleCrossSection[i].secondAreaDeriv, cutoff);
                else
                    tmp *= 0; 
                
                cP += tmp;
            }

            double avgArea = vehicleCrossSection[index].area;
            /*if (index < vehicleCrossSection.Length - 1)
            {
                avgArea += vehicleCrossSection[index + 1].area;
            }
            avgArea *= 0.5;*/

            cP *= Math.Sqrt(0.5 * sectionThickness / (beta * Math.Sqrt(Math.PI * avgArea)));

            return cP;
        }

        private double CalculateCpNoseDiscont(int index, double noseAreaSlope, double sectionThickness)
        {
            double cP_noseDiscont = index * sectionThickness * Math.PI;
            cP_noseDiscont = (noseAreaSlope) / cP_noseDiscont;

            return cP_noseDiscont;
        }

        private double AdjustVelForFinitePressure(double vel)
        {
            if (vel > 0)
                return vel;

            double newVel = 1.0 - vel;
            newVel *= newVel;
            newVel = 2.0 * newVel / (1.0 + newVel);

            newVel = 1.0 - newVel;

            return newVel;
        }

        private double AdjustCpForNonlinearEffects(double cP, double beta, double freestreamMach)
        {
            double nuFreestream = PrandtlMeyerExpansionAngle(freestreamMach, freestreamMach);
            double deflectionAngle = 0.5 * cP * beta;

            return cPPrandtlMeyerExpansion(freestreamMach, nuFreestream, deflectionAngle);
        }

        double cPPrandtlMeyerExpansion(double machNumber, double nuFreestream, double deflectionAngle)
        {
            double nu = nuFreestream - deflectionAngle;

            if (nu > 180 / (130.45 * Math.PI))
                nu = 180 / (130.45 * Math.PI);

            double effectiveFactor = nu / 130.45 * 180 / Math.PI;
            if (effectiveFactor < 0)
                effectiveFactor = 0;
            double exp = -0.42 * Math.Sqrt(effectiveFactor);
            exp += 0.313 * effectiveFactor;
            exp += 0.56;

            double effectiveMach = 1 - Math.Pow(effectiveFactor, exp);
            effectiveMach = 1 / effectiveMach;

            double cP = StagPresRatio(effectiveMach, nu) / StagPresRatio(machNumber, nuFreestream);
            cP--;
            cP /= 0.7 * machNumber * machNumber;
            return cP;
        }

        double StagPresRatio(double machNumber, double nu)
        {
            double ratio = nu + Math.Acos(1 / machNumber);
            ratio *= 2.0 / Math.Sqrt(6.0);
            ratio = 1 + Math.Cos(ratio);
            ratio /= 2.4;
            ratio = Math.Pow(ratio, 3.5);

            return ratio;
        }

        double PrandtlMeyerExpansionAngle(double localMach, double freestreamMach)
        {
            double nu;
            if (localMach < 1.0)
            {
                double freestreamNu = PrandtlMeyerExpansionAngle(freestreamMach, freestreamMach);
                nu = 1.0 - localMach;
                nu *= nu;
                nu *= (freestreamNu - Math.PI * 0.5);
            }
            else
            {
                nu = localMach * localMach - 1.0;
                nu /= 6.0;
                nu = Math.Sqrt(nu);
                nu = Math.Atan(nu) * Math.Sqrt(6.0);
                nu -= Math.Acos(1 / localMach);
            }
            return nu;
        }

        #endregion

        private double CalculateHypersonicDrag(double lowArea, double highArea, double sectionThickness)
        {
            if (lowArea >= highArea)
                return 0;

            double r1, r2;
            r1 = Math.Sqrt(lowArea / Math.PI);
            r2 = Math.Sqrt(highArea / Math.PI);

            double radDiff = r2 - r1;
            double radDiffSq = radDiff * radDiff;

            double drag = sectionThickness * sectionThickness + radDiffSq;
            drag = 2d * Math.PI / drag;
            drag *= radDiff * radDiffSq * (r1 + r2);

            return -drag;        //force is negative 
        }

        private double CalcAllTransonicWaveDrag(VoxelCrossSection[] sections, int front, int numSections, double sectionThickness)
        {
            double drag = 0;
            double Lj;

            for(int j = 0; j < numSections; j++)
            {
                double accumulator = 0;

                Lj = (j + 0.5) * Math.Log(j + 0.5);
                if (j > 0)
                    Lj -= (j - 0.5) * Math.Log(j - 0.5);

                for(int i = j; i < numSections; i++)
                {
                    accumulator += sections[front + i].secondAreaDeriv * sections[front + i - j].secondAreaDeriv;
                }

                drag += accumulator * Lj;
            }

            drag *= -sectionThickness * sectionThickness / Math.PI;
            return drag;
        }

        private double CalculateTransonicWaveDrag(int i, int index, int numSections, int front, double sectionThickness, double cutoff)
        {
            double currentSectAreaCrossSection = MathClampAbs(_vehicleCrossSection[index].secondAreaDeriv, cutoff);

            if (currentSectAreaCrossSection == 0)       //quick escape for 0 cross-section section drag
                return 0;

            double lj2ndTerm = 0;
            double drag = 0;
            int limDoubleDrag = Math.Min(i, numSections - i);
            double sectionThicknessSq = sectionThickness * sectionThickness;

            double lj3rdTerm = Math.Log(sectionThickness) - 1;

            lj2ndTerm = 0.5 * Math.Log(0.5);
            drag = currentSectAreaCrossSection * (lj2ndTerm + lj3rdTerm);


            for (int j = 1; j <= limDoubleDrag; j++)      //section of influence from ahead and behind
            {
                double thisLj = (j + 0.5) * Math.Log(j + 0.5);
                double tmp = thisLj;
                thisLj -= lj2ndTerm;
                lj2ndTerm = tmp;

                tmp = MathClampAbs(_vehicleCrossSection[index + j].secondAreaDeriv, cutoff);
                tmp += MathClampAbs(_vehicleCrossSection[index - j].secondAreaDeriv, cutoff);

                thisLj += lj3rdTerm;

                drag += tmp * thisLj;
            }
            if (i < numSections - i)
            {
                for (int j = 2 * i + 1; j < numSections; j++)
                {
                    double thisLj = (j - i + 0.5) * Math.Log(j - i + 0.5);
                    double tmp = thisLj;
                    thisLj -= lj2ndTerm;
                    lj2ndTerm = tmp;

                    tmp = MathClampAbs(_vehicleCrossSection[j + front].secondAreaDeriv, cutoff);

                    thisLj += lj3rdTerm;

                    drag += tmp * thisLj;
                }
            }
            else if (i > numSections - i)
            {
                for (int j = numSections - 2 * i - 2; j >= 0; j--)
                {
                    double thisLj = (i - j + 0.5) * Math.Log(i - j + 0.5);
                    double tmp = thisLj;
                    thisLj -= lj2ndTerm;
                    lj2ndTerm = tmp;

                    tmp = MathClampAbs(_vehicleCrossSection[j + front].secondAreaDeriv, cutoff);

                    thisLj += lj3rdTerm;

                    drag += tmp * thisLj;
                }
            }

            drag *= sectionThicknessSq;
            drag /= 2 * Math.PI;
            drag *= currentSectAreaCrossSection;
            return drag;
        }

        private double CalculateCriticalMachNumber(double finenessRatio)
        {
            if (finenessRatio > 10)
                return 0.925;
            if (finenessRatio < 1.5)
                return 0.285;
            if (finenessRatio > 4)
            {
                if (finenessRatio > 6)
                    return 0.00625 * finenessRatio + 0.8625;

                return 0.025 * finenessRatio + 0.75;
            }
            else if (finenessRatio < 3)
                return 0.33 * finenessRatio - 0.21;

            return 0.07 * finenessRatio + 0.57;
        }

        private double CriticalMachFactorForUnsmoothCrossSection(VoxelCrossSection[] crossSections, double finenessRatio, double sectionThickness)
        {
            double maxAbsRateOfChange = 0;
            double maxSecondDeriv = 0;
            double prevArea = 0;
            double invSectionThickness = 1/ sectionThickness;

            for(int i = 0; i < crossSections.Length; i++)
            {
                double currentArea = crossSections[i].area;
                double absRateOfChange = Math.Abs(currentArea - prevArea) * invSectionThickness;
                if (absRateOfChange > maxAbsRateOfChange)
                    maxAbsRateOfChange = absRateOfChange;
                prevArea = currentArea;
                maxSecondDeriv = Math.Max(maxSecondDeriv, Math.Abs(crossSections[i].secondAreaDeriv));
            }

            //double normalizedRateOfChange = maxAbsRateOfChange / _maxCrossSectionArea;

            double maxCritMachAdjustmentFactor = 2 * _maxCrossSectionArea + 5 * (0.5 * maxAbsRateOfChange + 0.3 * maxSecondDeriv);
            maxCritMachAdjustmentFactor = 0.5 + (_maxCrossSectionArea - 0.5 * (0.5 * maxAbsRateOfChange + 0.3 * maxSecondDeriv)) / maxCritMachAdjustmentFactor;     //will vary based on x = maxAbsRateOfChange / _maxCrossSectionArea from 1 @ x = 0 to 0.5 as x -> infinity

            double critAdjustmentFactor = 4 + finenessRatio;
            critAdjustmentFactor = 6 * (1 - maxCritMachAdjustmentFactor) / critAdjustmentFactor;
            critAdjustmentFactor += maxCritMachAdjustmentFactor;

            if (critAdjustmentFactor > 1)
                critAdjustmentFactor = 1;

            return critAdjustmentFactor;
        }
        #endregion

        double MathClampAbs(double value, double abs)
        {
            if (value < -abs)
                return -abs;
            if (value > abs)
                return abs;
            return value;
        }

        struct CrossSectionAdjustData
        {
            public double activeAreaRemoved;
            public int lastIndex;
            public int counter;

            public CrossSectionAdjustData(double activeAreaRemoved, int lastIndex)
            {
                this.activeAreaRemoved = activeAreaRemoved;
                this.lastIndex = lastIndex;
                counter = 0;
            }
        }
    }
}
