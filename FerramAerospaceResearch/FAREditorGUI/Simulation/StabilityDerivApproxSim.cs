using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerramAerospaceResearch.FAREditorGUI.Simulation
{
    class StabilityDerivLinearSim
    {
        InstantConditionSim _instantCondition;

        public StabilityDerivLinearSim(InstantConditionSim instantConditionSim)
        {
            _instantCondition = instantConditionSim;
        }

        public GraphData RunTransientSimLateral(StabilityDerivOutput vehicleData, double endTime, double initDt, double[] InitCond)
        {
            SimMatrix A = new SimMatrix(4, 4);

            A.PrintToConsole();

            int i = 0;
            int j = 0;
            int num = 0;
            double[] Derivs = new double[27];

            vehicleData.stabDerivs.CopyTo(Derivs, 0);

            Derivs[15] = Derivs[15] / vehicleData.nominalVelocity;
            Derivs[18] = Derivs[18] / vehicleData.nominalVelocity;
            Derivs[21] = Derivs[21] / vehicleData.nominalVelocity - 1;

            double Lb = Derivs[16] / (1 - Derivs[26] * Derivs[26] / (Derivs[0] * Derivs[2]));
            double Nb = Derivs[17] / (1 - Derivs[26] * Derivs[26] / (Derivs[0] * Derivs[2]));

            double Lp = Derivs[19] / (1 - Derivs[26] * Derivs[26] / (Derivs[0] * Derivs[2]));
            double Np = Derivs[20] / (1 - Derivs[26] * Derivs[26] / (Derivs[0] * Derivs[2]));

            double Lr = Derivs[22] / (1 - Derivs[26] * Derivs[26] / (Derivs[0] * Derivs[2]));
            double Nr = Derivs[23] / (1 - Derivs[26] * Derivs[26] / (Derivs[0] * Derivs[2]));

            Derivs[16] = Lb + Derivs[26] / Derivs[0] * Nb;
            Derivs[17] = Nb + Derivs[26] / Derivs[2] * Lb;

            Derivs[19] = Lp + Derivs[26] / Derivs[0] * Np;
            Derivs[20] = Np + Derivs[26] / Derivs[2] * Lp;

            Derivs[22] = Lr + Derivs[26] / Derivs[0] * Nr;
            Derivs[23] = Nr + Derivs[26] / Derivs[2] * Lr;

            for (int k = 0; k < Derivs.Length; k++)
            {
                double f = Derivs[k];
                if (num < 15)
                {
                    num++;              //Avoid Ix, Iy, Iz and long derivs
                    continue;
                }
                else
                    num++;
                Debug.Log(i + "," + j);
                if (i <= 2)
                    A.Add(f, i, j);

                if (j < 2)
                    j++;
                else
                {
                    j = 0;
                    i++;
                }

            }
            A.Add(_instantCondition.CalculateAccelerationDueToGravity(vehicleData.body, vehicleData.altitude) * Math.Cos(vehicleData.stableAoA * Math.PI / 180) / vehicleData.nominalVelocity, 3, 0);
            A.Add(1, 1, 3);


            A.PrintToConsole();                //We should have an array that looks like this:

            /*             i --------------->
             *       j  [ Yb / u0 , Yp / u0 , -(1 - Yr/ u0) ,  g Cos(θ0) / u0 ]
             *       |  [   Lb    ,    Lp   ,      Lr       ,          0          ]
             *       |  [   Nb    ,    Np   ,      Nr       ,          0          ]
             *      \ / [    0    ,    1    ,      0        ,          0          ]
             *       V                              //And one that looks like this:
             *                              
             *          [ Z e ]
             *          [ X e ]
             *          [ M e ]
             *          [  0  ]
             * 
             * 
             */
            RungeKutta4 transSolve = new RungeKutta4(endTime, initDt, A, InitCond);
            transSolve.Solve();

            GraphData lines = new GraphData();
            lines.xValues = transSolve.time;

            double[] yVal = transSolve.GetSolution(0);
            ScaleAndClampValues(yVal, 180 / Math.PI, 50);
            lines.AddData(yVal, EditorColors.GetColor(3), "β", true);

            yVal = transSolve.GetSolution(1);
            ScaleAndClampValues(yVal, 180 / Math.PI, 50);
            lines.AddData(yVal, EditorColors.GetColor(2), "p", true);

            yVal = transSolve.GetSolution(2);
            ScaleAndClampValues(yVal, 180 / Math.PI, 50);
            lines.AddData(yVal, EditorColors.GetColor(1), "r", true);

            yVal = transSolve.GetSolution(3);
            ScaleAndClampValues(yVal, 180 / Math.PI, 50);
            lines.AddData(yVal, EditorColors.GetColor(0), "φ", true);

            /*graph.SetBoundaries(0, endTime, -10, 10);
            graph.SetGridScaleUsingValues(1, 5);
            graph.horizontalLabel = "time";
            graph.verticalLabel = "value";
            graph.Update();*/

            return lines;
        }

        public GraphData RunTransientSimLongitudinal(StabilityDerivOutput vehicleData, double endTime, double initDt, double[] InitCond)
        {
            SimMatrix A = new SimMatrix(4, 4);
            SimMatrix B = new SimMatrix(1, 4);

            A.PrintToConsole();

            int i = 0;
            int j = 0;
            int num = 0;
            double[] Derivs = new double[27];

            for (int k = 0; k < vehicleData.stabDerivs.Length; k++)
            {
                double f = vehicleData.stabDerivs[k];
                if (num < 3 || num >= 15)
                {
                    num++;              //Avoid Ix, Iy, Iz
                    continue;
                }
                else
                    num++;
                MonoBehaviour.print(i + "," + j);
                if (i <= 2)
                    if (num == 10)
                        A.Add(f + vehicleData.nominalVelocity, i, j);
                    else
                        A.Add(f, i, j);
                else
                    B.Add(f, 0, j);
                if (j < 2)
                    j++;
                else
                {
                    j = 0;
                    i++;
                }

            }
            A.Add(-_instantCondition.CalculateAccelerationDueToGravity(vehicleData.body, vehicleData.altitude), 3, 1);
            A.Add(1, 2, 3);


            A.PrintToConsole();                //We should have an array that looks like this:

            /*             i --------------->
             *       j  [ Z w , Z u , Z q ,  0 ]
             *       |  [ X w , X u , X q , -g ]
             *       |  [ M w , M u , M q ,  0 ]
             *      \ / [  0  ,  0  ,  1  ,  0 ]
             *       V                              //And one that looks like this:
             *                              
             *          [ Z e ]
             *          [ X e ]
             *          [ M e ]
             *          [  0  ]
             * 
             * 
             */

            RungeKutta4 transSolve = new RungeKutta4(endTime, initDt, A, InitCond);
            transSolve.Solve();

            GraphData lines = new GraphData();
            lines.xValues = transSolve.time;

            double[] yVal = transSolve.GetSolution(0);
            ScaleAndClampValues(yVal, 1, 50);
            lines.AddData(yVal, EditorColors.GetColor(3), "w", true);

            yVal = transSolve.GetSolution(1);
            ScaleAndClampValues(yVal, 1, 50);
            lines.AddData(yVal, EditorColors.GetColor(2), "u", true);

            yVal = transSolve.GetSolution(2);
            ScaleAndClampValues(yVal, 180 / Math.PI, 50);
            lines.AddData(yVal, EditorColors.GetColor(1), "q", true);

            yVal = transSolve.GetSolution(3);
            ScaleAndClampValues(yVal, 180 / Math.PI, 50);
            lines.AddData(yVal, EditorColors.GetColor(0), "θ", true);

            /*graph.SetBoundaries(0, endTime, -10, 10);
            graph.SetGridScaleUsingValues(1, 5);
            graph.horizontalLabel = "time";
            graph.verticalLabel = "value";
            graph.Update();*/

            return lines;
        }


        private void ScaleAndClampValues(double[] yVal, double scalingFactor, double clampValue)
        {
            for (int k = 0; k < yVal.Length; k++)
            {
                yVal[k] = yVal[k] * scalingFactor;
                if (yVal[k] > clampValue)
                    yVal[k] = clampValue;
                else if (yVal[k] < -clampValue)
                    yVal[k] = -clampValue;
            }
        }
    }
}
