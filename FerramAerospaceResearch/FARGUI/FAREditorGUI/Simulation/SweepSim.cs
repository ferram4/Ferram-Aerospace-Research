using System;
using System.Collections.Generic;
using UnityEngine;
using ferram4;

namespace FerramAerospaceResearch.FARGUI.FAREditorGUI.Simulation
{
    class SweepSim
    {
        InstantConditionSim _instantCondition;

        public SweepSim(InstantConditionSim instantConditionSim)
        {
            _instantCondition = instantConditionSim;
        }

        public GraphData MachNumberSweep(double aoAdegrees, double pitch, double lowerBound, double upperBound, int numPoints, int flapSetting, bool spoilers, CelestialBody body)
        {
            FARAeroUtil.UpdateCurrentActiveBody(body);

            double mass = 0;
            Vector3d CoM = Vector3d.zero;
            FARAeroUtil.ResetEditorParts();
            List<Part> partsList = EditorLogic.SortedShipList;
            for (int i = 0; i < partsList.Count; i++)
            {
                Part p = partsList[i];

                if (FARAeroUtil.IsNonphysical(p))
                    continue;

                double partMass = p.mass;
                if (p.Resources.Count > 0)
                    partMass += p.GetResourceMass();

                partMass += p.GetModuleMass(p.mass);
                CoM += partMass * (Vector3d)p.transform.TransformPoint(p.CoMOffset);
                mass += partMass;
            }
            CoM /= mass;


            double[] ClValues = new double[(int)numPoints];
            double[] CdValues = new double[(int)numPoints];
            double[] CmValues = new double[(int)numPoints];
            double[] LDValues = new double[(int)numPoints];
            double[] AlphaValues = new double[(int)numPoints];

            InstantConditionSimInput input = new InstantConditionSimInput(aoAdegrees, 0, 0, 0, 0, 0, 0, pitch, flapSetting, spoilers);
            
            for (int i = 0; i < numPoints; i++)
            {
                input.machNumber = i / (double)numPoints * (upperBound - lowerBound) + lowerBound;

                if (input.machNumber == 0)
                    input.machNumber = 0.001;

                InstantConditionSimOutput output;

                _instantCondition.GetClCdCmSteady(CoM, input, out output, i == 0);
                AlphaValues[i] = input.machNumber;
                ClValues[i] = output.Cl;
                CdValues[i] = output.Cd;
                CmValues[i] = output.Cm;
                LDValues[i] = output.Cl * 0.1 / output.Cd;
            }

            GraphData data = new GraphData();
            data.xValues = AlphaValues;
            data.AddData(ClValues, EditorColors.GetColor(0), "Cl", true);
            data.AddData(CdValues, EditorColors.GetColor(1), "Cd", true);
            data.AddData(CmValues, EditorColors.GetColor(2), "Cm", true);
            data.AddData(LDValues, EditorColors.GetColor(3), "L/D", true);

            return data;
        }

        public GraphData AngleOfAttackSweep(double machNumber, double pitch, double lowerBound, double upperBound, int numPoints, int flapSetting, bool spoilers, CelestialBody body)
        {
            if (machNumber == 0)
                machNumber = 0.001;

            InstantConditionSimInput input = new InstantConditionSimInput(0, 0, 0, 0, 0, 0, machNumber, pitch, flapSetting, spoilers);

            FARAeroUtil.UpdateCurrentActiveBody(body);

            double mass = 0;
            Vector3d CoM = Vector3d.zero;
            FARAeroUtil.ResetEditorParts();

            List<Part> partsList = EditorLogic.SortedShipList;
            for (int i = 0; i < partsList.Count; i++)
            {
                Part p = partsList[i];
                if (FARAeroUtil.IsNonphysical(p))
                    continue;

                double partMass = p.mass;
                if (p.Resources.Count > 0)
                    partMass += p.GetResourceMass();

                partMass += p.GetModuleMass(p.mass);
                CoM += partMass * (Vector3d)p.transform.TransformPoint(p.CoMOffset);
                mass += partMass;
            }
            CoM /= mass;

            double[] ClValues = new double[(int)numPoints];
            double[] CdValues = new double[(int)numPoints];
            double[] CmValues = new double[(int)numPoints];
            double[] LDValues = new double[(int)numPoints];
            double[] AlphaValues = new double[(int)numPoints];
            double[] ClValues2 = new double[(int)numPoints];
            double[] CdValues2 = new double[(int)numPoints];
            double[] CmValues2 = new double[(int)numPoints];
            double[] LDValues2 = new double[(int)numPoints];

            for (int i = 0; i < 2 * numPoints; i++)
            {
                double angle = 0;
                if (i < numPoints)
                    angle = i / (double)numPoints * (upperBound - lowerBound) + lowerBound;
                else
                    angle = (i - (double)numPoints + 1) / (double)numPoints * (lowerBound - upperBound) + upperBound;

                input.alpha = angle;

                InstantConditionSimOutput output;

                _instantCondition.GetClCdCmSteady(CoM, input, out output, i == 0);

                //                MonoBehaviour.print("Cl: " + Cl + " Cd: " + Cd);
                if (i < numPoints)
                {
                    AlphaValues[i] = angle;
                    ClValues[i] = output.Cl;
                    CdValues[i] = output.Cd;
                    CmValues[i] = output.Cm;
                    LDValues[i] = output.Cl * 0.1 / output.Cd;
                }
                else
                {
                    ClValues2[numPoints * 2 - 1 - i] = output.Cl;
                    CdValues2[numPoints * 2 - 1 - i] = output.Cd;
                    CmValues2[numPoints * 2 - 1 - i] = output.Cm;
                    LDValues2[numPoints * 2 - 1 - i] = output.Cl * 0.1 / output.Cd;
                }
            }

            GraphData data = new GraphData();
            data.xValues = AlphaValues;
            data.AddData(ClValues2, EditorColors.GetColor(0) * 0.5f, "Cl2", false);
            data.AddData(ClValues, EditorColors.GetColor(0), "Cl", true);

            data.AddData(CdValues2, EditorColors.GetColor(1) * 0.5f, "Cd2", false);
            data.AddData(CdValues, EditorColors.GetColor(1), "Cd", true);

            data.AddData(CmValues2, EditorColors.GetColor(2) * 0.5f, "Cm2", false);
            data.AddData(CmValues, EditorColors.GetColor(2), "Cm", true);

            data.AddData(LDValues2, EditorColors.GetColor(3) * 0.5f, "L/D2", false);
            data.AddData(LDValues, EditorColors.GetColor(3), "L/D", true);


            return data;
        }

    }
}
