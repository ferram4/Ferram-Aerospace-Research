using System;
using System.Collections.Generic;
using ferram4;
using UnityEngine;

namespace FerramAerospaceResearch.FAREditorGUI.Simulation
{
    class StabilityDerivCalculator
    {
        InstantConditionSim _instantCondition;

        public StabilityDerivCalculator(InstantConditionSim instantConditionSim)
        {
            _instantCondition = instantConditionSim;
        }

        public StabilityDerivOutput CalculateStabilityDerivs(double u0, double q, double machNumber, double alpha, double beta, double phi, int flapSetting, bool spoilers, CelestialBody body, double alt)
        {
            StabilityDerivOutput stabDerivOutput = new StabilityDerivOutput();
            stabDerivOutput.nominalVelocity = u0;
            stabDerivOutput.altitude = alt;
            stabDerivOutput.body = body;

            Vector3d CoM = Vector3d.zero;
            double mass = 0;

            double MAC = 0;
            double b = 0;
            double area = 0;

            double Ix = 0;
            double Iy = 0;
            double Iz = 0;
            double Ixy = 0;
            double Iyz = 0;
            double Ixz = 0;

            InstantConditionSimInput input = new InstantConditionSimInput(alpha, beta, phi, 0, 0, 0, machNumber, 0, flapSetting, spoilers);
            InstantConditionSimOutput nominalOutput;
            InstantConditionSimOutput pertOutput = new InstantConditionSimOutput();

            _instantCondition.GetClCdCmSteady(Vector3d.zero, input, out nominalOutput, true);

            for (int i = 0; i < FARAeroUtil.CurEditorParts.Count; i++)
            {
                Part p = FARAeroUtil.CurEditorParts[i];

                if (FARAeroUtil.IsNonphysical(p))
                    continue;
                double partMass = p.mass;
                if (p.Resources.Count > 0)
                    partMass += p.GetResourceMass();

                partMass += p.GetModuleMass(p.mass);
                CoM += partMass * (Vector3d)p.transform.TransformPoint(p.CoMOffset);
                mass += partMass;
                FARWingAerodynamicModel w = p.GetComponent<FARWingAerodynamicModel>();
                if (w != null)
                {
                    area += w.S;
                    MAC += w.GetMAC() * w.S;
                    b += w.Getb_2() * w.S;
                    if (w is FARControllableSurface)
                    {
                        (w as FARControllableSurface).SetControlStateEditor(CoM, p.transform.up, 0, 0, 0, input.flaps, input.spoilers);
                    }
                }
            }
            if (area == 0)
            {
                area = 1;
                MAC = 1;
                b = 1;
            }
            MAC /= area;
            b /= area;
            CoM /= mass;
            mass *= 1000;

            stabDerivOutput.b = b;
            stabDerivOutput.MAC = MAC;
            stabDerivOutput.area = area;

            for (int i = 0; i < FARAeroUtil.CurEditorParts.Count; i++)
            {
                Part p = FARAeroUtil.CurEditorParts[i];

                if (p == null || FARAeroUtil.IsNonphysical(p))
                    continue;
                //This section handles the parallel axis theorem
                Vector3 relPos = p.transform.TransformPoint(p.CoMOffset) - CoM;
                double x2, y2, z2, x, y, z;
                x2 = relPos.z * relPos.z;
                y2 = relPos.x * relPos.x;
                z2 = relPos.y * relPos.y;
                x = relPos.z;
                y = relPos.x;
                z = relPos.y;

                double partMass = p.mass;
                if (p.Resources.Count > 0)
                    partMass += p.GetResourceMass();

                partMass += p.GetModuleMass(p.mass);

                Ix += (y2 + z2) * partMass;
                Iy += (x2 + z2) * partMass;
                Iz += (x2 + y2) * partMass;

                Ixy += -x * y * partMass;
                Iyz += -z * y * partMass;
                Ixz += -x * z * partMass;

                //And this handles the part's own moment of inertia
                Vector3 principalInertia = p.Rigidbody.inertiaTensor;
                Quaternion prncInertRot = p.Rigidbody.inertiaTensorRotation;

                //The rows of the direction cosine matrix for a quaternion
                Vector3 Row1 = new Vector3(prncInertRot.x * prncInertRot.x - prncInertRot.y * prncInertRot.y - prncInertRot.z * prncInertRot.z + prncInertRot.w * prncInertRot.w,
                    2 * (prncInertRot.x * prncInertRot.y + prncInertRot.z * prncInertRot.w),
                    2 * (prncInertRot.x * prncInertRot.z - prncInertRot.y * prncInertRot.w));

                Vector3 Row2 = new Vector3(2 * (prncInertRot.x * prncInertRot.y - prncInertRot.z * prncInertRot.w),
                    -prncInertRot.x * prncInertRot.x + prncInertRot.y * prncInertRot.y - prncInertRot.z * prncInertRot.z + prncInertRot.w * prncInertRot.w,
                    2 * (prncInertRot.y * prncInertRot.z + prncInertRot.x * prncInertRot.w));

                Vector3 Row3 = new Vector3(2 * (prncInertRot.x * prncInertRot.z + prncInertRot.y * prncInertRot.w),
                    2 * (prncInertRot.y * prncInertRot.z - prncInertRot.x * prncInertRot.w),
                    -prncInertRot.x * prncInertRot.x - prncInertRot.y * prncInertRot.y + prncInertRot.z * prncInertRot.z + prncInertRot.w * prncInertRot.w);


                //And converting the principal moments of inertia into the coordinate system used by the system
                Ix += principalInertia.x * Row1.x * Row1.x + principalInertia.y * Row1.y * Row1.y + principalInertia.z * Row1.z * Row1.z;
                Iy += principalInertia.x * Row2.x * Row2.x + principalInertia.y * Row2.y * Row2.y + principalInertia.z * Row2.z * Row2.z;
                Iz += principalInertia.x * Row3.x * Row3.x + principalInertia.y * Row3.y * Row3.y + principalInertia.z * Row3.z * Row3.z;

                Ixy += principalInertia.x * Row1.x * Row2.x + principalInertia.y * Row1.y * Row2.y + principalInertia.z * Row1.z * Row2.z;
                Ixz += principalInertia.x * Row1.x * Row3.x + principalInertia.y * Row1.y * Row3.y + principalInertia.z * Row1.z * Row3.z;
                Iyz += principalInertia.x * Row2.x * Row3.x + principalInertia.y * Row2.y * Row3.y + principalInertia.z * Row2.z * Row3.z;
            }
            Ix *= 1000;
            Iy *= 1000;
            Iz *= 1000;

            stabDerivOutput.stabDerivs[0] = Ix;
            stabDerivOutput.stabDerivs[1] = Iy;
            stabDerivOutput.stabDerivs[2] = Iz;

            stabDerivOutput.stabDerivs[24] = Ixy;
            stabDerivOutput.stabDerivs[25] = Iyz;
            stabDerivOutput.stabDerivs[26] = Ixz;


            double effectiveG = _instantCondition.CalculateAccelerationDueToGravity(body, alt);     //This is the effect of gravity
            effectiveG -= u0 * u0 / (alt + body.Radius);                          //This is the effective reduction of gravity due to high velocity
            double neededCl = mass * effectiveG / (q * area);

            //Longitudinal Mess
            _instantCondition.SetState(machNumber, neededCl, CoM, 0, input.flaps, input.spoilers);

            alpha = FARMathUtil.BrentsMethod(_instantCondition.FunctionIterateForAlpha, -5d, 25d, 0.01, 30);

            nominalOutput = _instantCondition.iterationOutput;
            //alpha_str = (alpha * Mathf.PI / 180).ToString();

            input.alpha = (alpha + 0.1);

            _instantCondition.GetClCdCmSteady(CoM, input, out pertOutput, true, true);

            stabDerivOutput.stableCl = neededCl;
            stabDerivOutput.stableCd = nominalOutput.Cd;
            stabDerivOutput.stableAoA = alpha;
            stabDerivOutput.stableAoAState = "";
            if (Math.Abs((nominalOutput.Cl - neededCl) / neededCl) > 0.1)
                stabDerivOutput.stableAoAState = ((nominalOutput.Cl > neededCl) ? "<" : ">");

            //Debug.Log("Cl needed: " + neededCl + ", AoA: " + alpha + ", Cl: " + nominalOutput.Cl + ", Cd: " + stable_Cd);

            pertOutput.Cl = (pertOutput.Cl - nominalOutput.Cl) / 0.1 * FARMathUtil.rad2deg;                   //vert vel derivs
            pertOutput.Cd = (pertOutput.Cd - nominalOutput.Cd) / 0.1 * FARMathUtil.rad2deg;
            pertOutput.Cm = (pertOutput.Cm - nominalOutput.Cm) / 0.1 * FARMathUtil.rad2deg;

            pertOutput.Cl += nominalOutput.Cd;
            pertOutput.Cd -= nominalOutput.Cl;

            pertOutput.Cl *= -q * area / (mass * u0);
            pertOutput.Cd *= -q * area / (mass * u0);
            pertOutput.Cm *= q * area * MAC / (Iy * u0);

            stabDerivOutput.stabDerivs[3] = pertOutput.Cl;
            stabDerivOutput.stabDerivs[4] = pertOutput.Cd;
            stabDerivOutput.stabDerivs[5] = pertOutput.Cm;

            input.alpha = alpha * FARMathUtil.deg2rad;
            input.machNumber = machNumber + 0.01;

            _instantCondition.GetClCdCmSteady(CoM, input, out pertOutput, true, false);

            pertOutput.Cl = (pertOutput.Cl - nominalOutput.Cl) / 0.01 * machNumber;                   //fwd vel derivs
            pertOutput.Cd = (pertOutput.Cd - nominalOutput.Cd) / 0.01 * machNumber;
            pertOutput.Cm = (pertOutput.Cm - nominalOutput.Cm) / 0.01 * machNumber;

            pertOutput.Cl += 2 * nominalOutput.Cl;
            pertOutput.Cd += 2 * nominalOutput.Cd;

            pertOutput.Cl *= -q * area / (mass * u0);
            pertOutput.Cd *= -q * area / (mass * u0);
            pertOutput.Cm *= q * area * MAC / (u0 * Iy);

            stabDerivOutput.stabDerivs[6] = pertOutput.Cl;
            stabDerivOutput.stabDerivs[7] = pertOutput.Cd;
            stabDerivOutput.stabDerivs[8] = pertOutput.Cm;

            input.machNumber = machNumber;

            _instantCondition.GetClCdCmSteady(CoM, input, out pertOutput, true, true);

            input.alphaDot = -0.05;

            _instantCondition.GetClCdCmSteady(CoM, input, out pertOutput, true, false);
           
            pertOutput.Cl = (pertOutput.Cl - nominalOutput.Cl) / 0.05;                   //pitch rate derivs
            pertOutput.Cd = (pertOutput.Cd - nominalOutput.Cd) / 0.05;
            pertOutput.Cm = (pertOutput.Cm - nominalOutput.Cm) / 0.05;

            pertOutput.Cl *= q * area * MAC / (2 * u0 * mass);
            pertOutput.Cd *= q * area * MAC / (2 * u0 * mass);
            pertOutput.Cm *= q * area * MAC * MAC / (2 * u0 * Iy);

            stabDerivOutput.stabDerivs[9] = pertOutput.Cl;
            stabDerivOutput.stabDerivs[10] = pertOutput.Cd;
            stabDerivOutput.stabDerivs[11] = pertOutput.Cm;

            input.alphaDot = 0;
            input.pitchValue = 0.1;

            _instantCondition.GetClCdCmSteady(CoM, input, out pertOutput, true, false);
            
            pertOutput.Cl = (pertOutput.Cl - nominalOutput.Cl) / 0.1;                   //elevator derivs
            pertOutput.Cd = (pertOutput.Cd - nominalOutput.Cd) / 0.1;
            pertOutput.Cm = (pertOutput.Cm - nominalOutput.Cm) / 0.1;

            pertOutput.Cl *= q * area / mass;
            pertOutput.Cd *= q * area / mass;
            pertOutput.Cm *= q * area * MAC / Iy;

            stabDerivOutput.stabDerivs[12] = pertOutput.Cl;
            stabDerivOutput.stabDerivs[13] = pertOutput.Cd;
            stabDerivOutput.stabDerivs[14] = pertOutput.Cm;

            //Lateral Mess

            input.pitchValue = 0;
            input.beta = (beta + 0.1);

            _instantCondition.GetClCdCmSteady(CoM, input, out pertOutput, true, false);
            pertOutput.Cy = (pertOutput.Cy - nominalOutput.Cy) / 0.1 * FARMathUtil.rad2deg;                   //sideslip angle derivs
            pertOutput.Cn = (pertOutput.Cn - nominalOutput.Cn) / 0.1 * FARMathUtil.rad2deg;
            pertOutput.C_roll = (pertOutput.C_roll - nominalOutput.C_roll) / 0.1 * FARMathUtil.rad2deg;

            pertOutput.Cy *= q * area / mass;
            pertOutput.Cn *= q * area * b / Iz;
            pertOutput.C_roll *= q * area * b / Ix;

            stabDerivOutput.stabDerivs[15] = pertOutput.Cy;
            stabDerivOutput.stabDerivs[17] = pertOutput.Cn;
            stabDerivOutput.stabDerivs[16] = pertOutput.C_roll;

            input.beta = beta;

            _instantCondition.GetClCdCmSteady(CoM, input, out pertOutput, true, true);

            input.phiDot = 0.05;

            _instantCondition.GetClCdCmSteady(CoM, input, out pertOutput, true, false);
           
            pertOutput.Cy = (pertOutput.Cy - nominalOutput.Cy) / 0.05;                   //roll rate derivs
            pertOutput.Cn = (pertOutput.Cn - nominalOutput.Cn) / 0.05;
            pertOutput.C_roll = (pertOutput.C_roll - nominalOutput.C_roll) / 0.05;

            pertOutput.Cy *= q * area * b / (2 * mass * u0);
            pertOutput.Cn *= q * area * b * b / (2 * Iz * u0);
            pertOutput.C_roll *= q * area * b * b / (2 * Ix * u0);

            stabDerivOutput.stabDerivs[18] = pertOutput.Cy;
            stabDerivOutput.stabDerivs[20] = pertOutput.Cn;
            stabDerivOutput.stabDerivs[19] = pertOutput.C_roll;


            input.phiDot = 0;

            _instantCondition.GetClCdCmSteady(CoM, input, out pertOutput, true, true);

            input.betaDot = 0.05;

            _instantCondition.GetClCdCmSteady(CoM, input, out pertOutput, true, false); pertOutput.Cy = (pertOutput.Cy - nominalOutput.Cy) / 0.05f;                   //yaw rate derivs
            pertOutput.Cn = (pertOutput.Cn - nominalOutput.Cn) / 0.05f;
            pertOutput.C_roll = (pertOutput.C_roll - nominalOutput.C_roll) / 0.05f;

            pertOutput.Cy *= q * area * b / (2 * mass * u0);
            pertOutput.Cn *= q * area * b * b / (2 * Iz * u0);
            pertOutput.C_roll *= q * area * b * b / (2 * Ix * u0);

            stabDerivOutput.stabDerivs[21] = pertOutput.Cy;
            stabDerivOutput.stabDerivs[23] = pertOutput.Cn;
            stabDerivOutput.stabDerivs[22] = pertOutput.C_roll;

            return stabDerivOutput;
        }

    }
}
