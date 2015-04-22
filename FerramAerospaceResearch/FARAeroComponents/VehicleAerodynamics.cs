using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using FerramAerospaceResearch.FARPartGeometry;

namespace FerramAerospaceResearch.FARAeroComponents
{
    class VehicleAerodynamics
    {
        VehicleVoxel _voxel = null;
        VoxelCrossSection[] _vehicleCrossSection = null;
        int _voxelCount;

        double length = 0;
        public double Length
        {
            get { return length; }
        }

        double maxCrossSectionArea = 0;
        public double MaxCrossSectionArea
        {
            get { return maxCrossSectionArea; }
        }

        bool calculationCompleted = false;
        public bool CalculationCompleted
        {
            get { return calculationCompleted; }
        }

        Matrix4x4 _worldToLocalMatrix, _localToWorldMatrix;
        Vector3 _vehicleMainAxis;
        List<Part> _vehiclePartList;

        List<FARAeroPartModule> _currentAeroModules;
        List<FARAeroPartModule> _newAeroModules;

        List<FARAeroSection> _currentAeroSections;
        List<FARAeroSection> _newAeroSections;

        int validSectionCount;
        int firstSection;

        bool visualizing = false;

        public void GetNewAeroData(out List<FARAeroPartModule> aeroModules, out List<FARAeroSection> aeroSections)
        {
            calculationCompleted = false;
            aeroModules = _currentAeroModules = _newAeroModules;
            _newAeroModules = null;

            aeroSections = _currentAeroSections = _newAeroSections;
            _newAeroSections = null;
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

        public void VoxelUpdate(Matrix4x4 worldToLocalMatrix, Matrix4x4 localToWorldMatrix, int voxelCount, List<Part> vehiclePartList)
        {
            _voxelCount = voxelCount;

            this._worldToLocalMatrix = worldToLocalMatrix;
            this._localToWorldMatrix = localToWorldMatrix;
            this._vehiclePartList = vehiclePartList;
            this._vehicleMainAxis = CalculateVehicleMainAxis();

            if(_voxel != null)
                ClearDebugVoxel();

            visualizing = false;

            ThreadPool.QueueUserWorkItem(CreateVoxel);
        }

        private void CreateVoxel(object nullObj)
        {
            try
            {
                lock (this)
                {
                    UpdateGeometryPartModules();

                    VehicleVoxel newvoxel = new VehicleVoxel(_vehiclePartList, _voxelCount, true, true);

                    _vehicleCrossSection = newvoxel.EmptyCrossSectionArray;

                    _voxel = newvoxel;

                    CalculateVesselAeroProperties();
                    calculationCompleted = true;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void UpdateGeometryPartModules()
        {
            for (int i = 0; i < _vehiclePartList.Count; i++)
            {
                Part p = _vehiclePartList[i];
                if ((object)p == null)
                    continue;
                GeometryPartModule geoModule = p.GetComponent<GeometryPartModule>();
                if ((object)geoModule != null)
                    geoModule.UpdateTransformMatrixList(_worldToLocalMatrix);
            }
        }

        private Vector3 CalculateVehicleMainAxis()
        {
            Vector3 axis = Vector3.zero;
            for (int i = 0; i < _vehiclePartList.Count; i++)      //get axis by averaging all parts up vectors
            {
                Part p = _vehiclePartList[i];
                GeometryPartModule m = p.GetComponent<GeometryPartModule>();
                if (m != null)
                {
                    Bounds b = m.overallMeshBounds;
                    axis += p.transform.up * b.size.x * b.size.y * b.size.z;    //scale part influence by approximate size
                }
            }
            axis.Normalize();   //normalize axis for later calcs
            float dotProd;

            dotProd = Math.Abs(Vector3.Dot(axis, _localToWorldMatrix.MultiplyVector(Vector3.up)));
            if (dotProd >= 0.99)        //if axis and _vessel.up are nearly aligned, just use _vessel.up
                return Vector3.up;

            dotProd = Math.Abs(Vector3.Dot(axis, _localToWorldMatrix.MultiplyVector(Vector3.forward)));

            if (dotProd >= 0.99)        //Same for forward...
                return Vector3.forward;

            dotProd = Math.Abs(Vector3.Dot(axis, _localToWorldMatrix.MultiplyVector(Vector3.right)));

            if (dotProd >= 0.99)        //and right...
                return Vector3.right;

            //Otherwise, now we need to use axis, since it's obviously not close to anything else

            axis = _worldToLocalMatrix.MultiplyVector(axis);

            return axis;
        }

        private void CalculateVesselAeroProperties()
        {
            int front, back, numSections;
            double sectionThickness;

            _voxel.CrossSectionData(_vehicleCrossSection, _vehicleMainAxis, out front, out back, out sectionThickness, out maxCrossSectionArea);

            numSections = back - front;
            validSectionCount = numSections;
            firstSection = front;
            double invMaxRadFactor = 1f / Math.Sqrt(maxCrossSectionArea / Math.PI);

            double finenessRatio = sectionThickness * numSections * 0.5 * invMaxRadFactor;       //vehicle length / max diameter, as calculated from sect thickness * num sections / (2 * max radius) 

            //skin friction and pressure drag for a body, taken from 1978 USAF Stability And Control DATCOM, Section 4.2.3.1, Paragraph A
            double viscousDragFactor = 0;
            viscousDragFactor = 60 / (finenessRatio * finenessRatio * finenessRatio) + 0.0025 * finenessRatio;     //pressure drag for a subsonic / transonic body due to skin friction
            viscousDragFactor++;

            viscousDragFactor /= (double)numSections;   //fraction of viscous drag applied to each section

            double criticalMachNumber = CalculateCriticalMachNumber(finenessRatio);

            double transonicWaveDragFactor = -sectionThickness * sectionThickness / (2 * Math.PI);

            length = sectionThickness * numSections;

            _newAeroSections = new List<FARAeroSection>();
            HashSet<FARAeroPartModule> tmpAeroModules = new HashSet<FARAeroPartModule>();

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

                FloatCurve xForcePressureAoA0 = new FloatCurve();
                FloatCurve xForcePressureAoA180 = new FloatCurve();

                FloatCurve xForceSkinFriction = new FloatCurve();

                //Potential and Viscous lift calcs
                float potentialFlowNormalForce;
                if(i == 0)
                    potentialFlowNormalForce = (float)(nextArea - curArea);
                else if(i == numSections)
                    potentialFlowNormalForce = (float)(curArea - prevArea);
                else
                    potentialFlowNormalForce = (float)(nextArea - prevArea);      //calcualted from area change

                float areaChangeMax = (float)Math.Min(nextArea, prevArea) * 0.1f;

                float sonicBaseDrag = 0.21f;

                sonicBaseDrag *= potentialFlowNormalForce;    //area base drag acts over

                if (potentialFlowNormalForce > areaChangeMax)
                    potentialFlowNormalForce = areaChangeMax;
                else if (potentialFlowNormalForce < -areaChangeMax)
                    potentialFlowNormalForce = -areaChangeMax;
                else
                    sonicBaseDrag *= Math.Abs(potentialFlowNormalForce / areaChangeMax);      //some scaling for small changes in cross-section

                double flatnessRatio = _vehicleCrossSection[index].flatnessRatio;
                if (flatnessRatio >= 1)
                    sonicBaseDrag /= (float)flatnessRatio;
                else
                    sonicBaseDrag *= (float)flatnessRatio;

                sonicBaseDrag = Math.Abs(sonicBaseDrag);

                float viscCrossflowDrag = (float)(Math.Sqrt(curArea / Math.PI) * sectionThickness * 2d);

                double surfaceArea = curArea * Math.PI;
                if (surfaceArea < 0)
                    surfaceArea = 0;
                surfaceArea = 2d * Math.Sqrt(surfaceArea); //section circumference
                surfaceArea *= sectionThickness;    //section surface area for viscous effects

                xForceSkinFriction.Add(0f, (float)(surfaceArea * viscousDragFactor), 0, 0);   //subsonic incomp visc drag
                xForceSkinFriction.Add(1f, (float)(surfaceArea * viscousDragFactor), 0, 0);   //transonic visc drag
                xForceSkinFriction.Add(2f, (float)surfaceArea, 0, 0);                     //above Mach 1.4, visc is purely surface drag, no pressure-related components simulated

                float sonicWaveDrag = (float)CalculateTransonicWaveDrag(i, index, numSections, front, sectionThickness, Math.Min(maxCrossSectionArea * 2, curArea * 16));//Math.Min(maxCrossSectionArea * 0.1, curArea * 0.25));
                sonicWaveDrag *= 0.325f;     //this is just to account for the higher drag being felt due to the inherent blockiness of the model being used and noise introduced by the limited control over shape and through voxelization
                float hypersonicDragForward = (float)CalculateHypersonicDrag(prevArea, curArea, sectionThickness);
                float hypersonicDragBackward = (float)CalculateHypersonicDrag(nextArea, curArea, sectionThickness);

                if (hypersonicDragForward > 0)
                    hypersonicDragForward = 0;
                if (hypersonicDragBackward > 0)
                    hypersonicDragBackward = 0;

                float hypersonicMomentForward = (float)CalculateHypersonicMoment(prevArea, curArea, sectionThickness);
                float hypersonicMomentBackward = (float)CalculateHypersonicMoment(nextArea, curArea, sectionThickness);

                if (hypersonicMomentForward > 0)
                    hypersonicMomentForward = 0;
                if (hypersonicMomentBackward > 0)
                    hypersonicMomentBackward = 0;

                xForcePressureAoA0.Add(35f, hypersonicDragForward, 0f, 0f);
                xForcePressureAoA180.Add(35f, -hypersonicDragBackward, 0f, 0f);

                float sonicAoA0Drag, sonicAoA180Drag;
                if (sonicBaseDrag > 0)      //occurs with increase in area; force applied at 180 AoA
                {
                    xForcePressureAoA0.Add((float)criticalMachNumber, hypersonicDragForward * 0.4f, 0f, 0f);    //hypersonic drag used as a proxy for effects due to flow separation
                    xForcePressureAoA180.Add((float)criticalMachNumber, (sonicBaseDrag * 0.25f - hypersonicDragBackward * 0.4f), 0f, 0f);

                    sonicAoA0Drag = sonicWaveDrag + hypersonicDragForward * 0.1f;
                    sonicAoA180Drag = -sonicWaveDrag - hypersonicDragBackward * 0.1f + sonicBaseDrag;
                    //xForcePressureAoA0.Add(1f, sonicWaveDrag + hypersonicDragForward * 0.1f, 0f, 0f);     //positive is force forward; negative is force backward
                    //xForcePressureAoA180.Add(1f, -sonicWaveDrag - hypersonicDragBackward * 0.1f + sonicBaseDrag, 0f, 0f);
                }
                else if (sonicBaseDrag < 0)
                {
                    xForcePressureAoA0.Add((float)criticalMachNumber, (-sonicBaseDrag * 0.25f + hypersonicDragForward * 0.4f), 0f, 0f);
                    xForcePressureAoA180.Add((float)criticalMachNumber, -hypersonicDragBackward * 0.4f, 0f, 0f);

                    sonicAoA0Drag = sonicWaveDrag + hypersonicDragForward * 0.1f - sonicBaseDrag;
                    sonicAoA180Drag = -sonicWaveDrag - hypersonicDragBackward * 0.1f;

                    //xForcePressureAoA0.Add(1f, sonicWaveDrag + hypersonicDragForward * 0.1f - sonicBaseDrag, 0f, 0f);     //positive is force forward; negative is force backward
                    //xForcePressureAoA180.Add(1f, -sonicWaveDrag - hypersonicDragBackward * 0.1f, 0f, 0f);
                }
                else
                {
                    xForcePressureAoA0.Add((float)criticalMachNumber, hypersonicDragForward * 0.4f, 0f, 0f);
                    xForcePressureAoA180.Add((float)criticalMachNumber, -hypersonicDragBackward * 0.4f, 0f, 0f);

                    sonicAoA0Drag = sonicWaveDrag + hypersonicDragForward * 0.1f;
                    sonicAoA180Drag = -sonicWaveDrag - hypersonicDragBackward * 0.1f;

                    //xForcePressureAoA0.Add(1f, sonicWaveDrag + hypersonicDragForward * 0.1f, 0f, 0f);     //positive is force forward; negative is force backward
                    //xForcePressureAoA180.Add(1f, -sonicWaveDrag - hypersonicDragBackward * 0.1f, 0f, 0f);
                }

                float diffSonicHyperAoA0 = Math.Abs(sonicAoA0Drag) - Math.Abs(hypersonicDragForward);
                float diffSonicHyperAoA180 = Math.Abs(sonicAoA180Drag) - Math.Abs(hypersonicDragBackward);


                xForcePressureAoA0.Add(1f, sonicAoA0Drag, 0, 0);
                xForcePressureAoA180.Add(1f, sonicAoA180Drag, 0, 0);

                xForcePressureAoA0.Add(2f, sonicAoA0Drag * 0.5773503f + (1 - 0.5773503f) * hypersonicDragForward);
                xForcePressureAoA180.Add(2f, sonicAoA180Drag * 0.5773503f - (1 - 0.5773503f) * hypersonicDragBackward);

                xForcePressureAoA0.Add(5f, sonicAoA0Drag * 0.2041242f + (1 - 0.2041242f) * hypersonicDragForward, -0.04252587f * diffSonicHyperAoA0, -0.04252587f * diffSonicHyperAoA0);
                xForcePressureAoA180.Add(5f, sonicAoA180Drag * 0.2041242f - (1 - 0.2041242f) * hypersonicDragBackward, -0.04252587f * diffSonicHyperAoA180, -0.04252587f * diffSonicHyperAoA180);

                xForcePressureAoA0.Add(10f, sonicAoA0Drag * 0.1005038f + (1 - 0.1005038f) * hypersonicDragForward, -0.0101519f * diffSonicHyperAoA0, -0.0101519f * diffSonicHyperAoA0);
                xForcePressureAoA180.Add(10f, sonicAoA180Drag * 0.1005038f - (1 - 0.1005038f) * hypersonicDragBackward, -0.0101519f * diffSonicHyperAoA180, -0.0101519f * diffSonicHyperAoA180);

                Vector3 xRefVector;
                if (index == front || index == back)
                    xRefVector = _vehicleMainAxis;
                else
                {
                    xRefVector = (Vector3)(_vehicleCrossSection[index - 1].centroid - _vehicleCrossSection[index + 1].centroid).normalized;
                    Vector3 offMainAxisVec = Vector3.Exclude(_vehicleMainAxis, xRefVector);
                    float tanAoA = offMainAxisVec.magnitude / (2f * (float)sectionThickness);
                    if (tanAoA > 0.17632698070846497347109038686862f)
                    {
                        offMainAxisVec.Normalize();
                        offMainAxisVec *= 0.17632698070846497347109038686862f;
                        xRefVector = _vehicleMainAxis + offMainAxisVec;
                        xRefVector.Normalize();
                    }
                }

                Vector3 nRefVector = Matrix4x4.TRS(Vector3.zero, Quaternion.FromToRotation(_vehicleMainAxis, xRefVector), Vector3.one).MultiplyVector(_vehicleCrossSection[index].flatNormalVector);

                Vector3 centroid = _localToWorldMatrix.MultiplyPoint3x4(_vehicleCrossSection[index].centroid);
                xRefVector = _localToWorldMatrix.MultiplyVector(xRefVector);
                nRefVector = _localToWorldMatrix.MultiplyVector(nRefVector);

                Dictionary<Part, VoxelCrossSection.SideAreaValues> includedPartsAndAreas = _vehicleCrossSection[index].partSideAreaValues;
                List<FARAeroPartModule> includedModules = new List<FARAeroPartModule>();
                List<float> weighting = new List<float>();

                float weightingFactor = 0;

                foreach (KeyValuePair<Part, VoxelCrossSection.SideAreaValues> pair in includedPartsAndAreas)
                {
                    FARAeroPartModule m = pair.Key.GetComponent<FARAeroPartModule>();
                    if (m != null)
                        includedModules.Add(m);
                    weightingFactor += (float)pair.Value.count;
                    weighting.Add((float)pair.Value.count);
                }
                weightingFactor = 1 / weightingFactor;
                for (int j = 0; j < includedModules.Count; j++)
                {
                    weighting[j] *= weightingFactor;
                }
                FARAeroSection section = new FARAeroSection(xForcePressureAoA0, xForcePressureAoA180, xForceSkinFriction, potentialFlowNormalForce, viscCrossflowDrag
                    , (float)flatnessRatio, hypersonicMomentForward, hypersonicMomentBackward,
                    centroid, xRefVector, nRefVector, includedModules, includedPartsAndAreas, weighting);

                _newAeroSections.Add(section);
                tmpAeroModules.UnionWith(includedModules);
            }
            _newAeroModules = tmpAeroModules.ToList();
            if(HighLogic.LoadedSceneIsFlight)
                _voxel = null;
        }

        private double CalculateHypersonicMoment(double lowArea, double highArea, double sectionThickness)
        {
            if(lowArea >= highArea)
                return 0;

            double r1, r2;
            r1 = Math.Sqrt(lowArea / Math.PI);
            r2 = Math.Sqrt(highArea / Math.PI);

            double moment = r2 * r2 + r1 * r1 + sectionThickness * sectionThickness * 0.5;
            moment *= 4 * Math.PI * sectionThickness;

            double radDiffSq = (r2 - r1);
            radDiffSq *= radDiffSq;

            moment *= radDiffSq;
            moment /= sectionThickness * sectionThickness + radDiffSq;

            return -moment;
        }

        private double CalculateHypersonicDrag(double lowArea, double highArea, double sectionThickness)
        {
            double drag = highArea + lowArea - 2 * Math.Sqrt(highArea * lowArea);
            drag = drag / (drag + sectionThickness * sectionThickness * Math.PI);       //calculate sin^2
            drag *= drag * drag;
            if (drag < 0)
                return 0;
            drag = Math.Sqrt(drag);
            drag *= (lowArea - highArea) * 2;
            return drag;        //force is negative 
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

            for (int j = 0; j <= limDoubleDrag; j++)      //section of influence from ahead and behind
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
                return 0.975;
            if (finenessRatio < 1.5)
                return 0.335;
            if (finenessRatio > 4)
            {
                if (finenessRatio > 6)
                    return 0.00625 * finenessRatio + 0.9125;

                return 0.025 * finenessRatio + 0.8;
            }
            else if (finenessRatio < 3)
                return 0.33 * finenessRatio - 0.16;

            return 0.07 * finenessRatio + 0.62;
        }

        double MathClampAbs(double value, double abs)
        {
            if (value < -abs)
                return -abs;
            if (value > abs)
                return abs;
            return value;
        }
    }
}
