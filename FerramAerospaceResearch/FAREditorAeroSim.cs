using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using KSP;

namespace ferram4
{
    class FAREditorAeroSim
    {
        public double CalculateAccelerationDueToGravity(CelestialBody body, double alt)
        {
            double radius = body.Radius + alt;
            double mu = body.gravParameter;

            double accel = radius * radius;
            accel = mu / accel;
            return accel;
        }
        
        public void GetClCdCmSteady(Vector3d CoM, double alpha, double beta, double phi, double alphaDot, double betaDot, double phiDot, double M, double pitch, out double Cl, out double Cd, out double Cm, out double Cy, out double Cn, out double C_roll, bool clear, bool reset_stall = false, int flap_setting = 0, bool spoilersDeployed = false, bool vehicleFueled = true)
        {
            Cl = 0;
            Cd = 0;
            Cm = 0;
            Cy = 0;
            Cn = 0;
            C_roll = 0;
            double area = 0;
            double MAC = 0;
            double b_2 = 0;

            alpha *= FARMathUtil.deg2rad;
            beta *= FARMathUtil.deg2rad;
            phi *= FARMathUtil.deg2rad;

            Vector3d forward = Vector3.forward;
            Vector3d up = Vector3.up;
            Vector3d right = Vector3.right;

            if (EditorLogic.fetch.editorType == EditorLogic.EditorMode.VAB)
            {
                forward = Vector3.up;
                up = -Vector3.forward;
            }

            Vector3d AngVel = (phiDot - Math.Sin(alpha) * betaDot) * forward + (Math.Cos(phi) * alphaDot + Math.Cos(alpha) * Math.Sin(phi) * betaDot) * right + (Math.Sin(phi) * alphaDot - Math.Cos(alpha) * Math.Cos(phi) * betaDot) * up;


            Vector3d velocity = forward * Math.Cos(alpha) * Math.Cos(beta) + right * (Math.Sin(phi) * Math.Sin(alpha) * Math.Cos(beta) - Math.Cos(phi) * Math.Sin(beta)) - up * (Math.Cos(phi) * Math.Sin(alpha) * Math.Cos(beta) - Math.Cos(phi) * Math.Sin(beta));

            velocity.Normalize();

            Vector3d liftVector = -forward * Math.Sin(alpha) + right * Math.Sin(phi) * Math.Cos(alpha) - up * Math.Cos(phi) * Math.Cos(alpha);

            Vector3d sideways = Vector3.Cross(velocity, liftVector);

            for (int i = 0; i < FARAeroUtil.CurEditorWings.Count; i++ )
            {
                FARWingAerodynamicModel w = FARAeroUtil.CurEditorWings[i];
                if (w.isShielded)
                    continue;

                if (clear)
                    w.EditorClClear(reset_stall);

                Vector3d relPos = w.GetAerodynamicCenter() - CoM;

                Vector3d vel = velocity + Vector3d.Cross(AngVel, relPos);

                if (w is FARControllableSurface)
                    (w as FARControllableSurface).SetControlStateEditor(CoM, vel, (float)pitch, 0, 0, flap_setting, spoilersDeployed);

                w.ComputeClCdEditor(vel, M);

                double tmpCl = w.GetCl() * w.S;
                Cl += tmpCl * -Vector3d.Dot(w.GetLiftDirection(), liftVector);
                Cy += tmpCl * -Vector3d.Dot(w.GetLiftDirection(), sideways);
                double tmpCd = w.GetCd() * w.S;
                Cd += tmpCd;
                Cm += tmpCl * Vector3d.Dot((relPos), velocity) * -Vector3d.Dot(w.GetLiftDirection(), liftVector) + tmpCd * -Vector3d.Dot((relPos), liftVector);
                Cn += tmpCd * Vector3d.Dot((relPos), sideways) + tmpCl * Vector3d.Dot((relPos), velocity) * -Vector3d.Dot(w.GetLiftDirection(), sideways);
                C_roll += tmpCl * Vector3d.Dot((relPos), sideways) * -Vector3d.Dot(w.GetLiftDirection(), liftVector);
                area += w.S;
                MAC += w.GetMAC() * w.S;
                b_2 += w.Getb_2() * w.S;
            }
            for (int i = 0; i < FARAeroUtil.CurEditorOtherDrag.Count; i++)
            {
                FARBasicDragModel d = FARAeroUtil.CurEditorOtherDrag[i];
                if (d.isShielded)
                    continue;

                Vector3d relPos = d.part.transform.position - CoM;

                Vector3d vel = velocity + Vector3d.Cross(AngVel, relPos);

                double tmpCd = d.GetDragEditor(vel, M);
                Cd += tmpCd;
                double tmpCl = d.GetLiftEditor();
                Cl += tmpCl * -Vector3d.Dot(d.GetLiftDirection(), liftVector);
                Cy += tmpCl * -Vector3d.Dot(d.GetLiftDirection(), sideways);
                relPos = d.GetCoDEditor() - CoM;
                Cm += d.GetMomentEditor() + tmpCl * Vector3d.Dot((relPos), velocity) * -Vector3d.Dot(d.GetLiftDirection(), liftVector) + tmpCd * -Vector3d.Dot((relPos), liftVector);
                Cn += tmpCd * Vector3d.Dot((relPos), sideways) + tmpCl * Vector3d.Dot((relPos), velocity) * -Vector3d.Dot(d.GetLiftDirection(), sideways);
                C_roll += tmpCl * Vector3d.Dot((relPos), sideways) * -Vector3d.Dot(d.GetLiftDirection(), liftVector);

            }
            for (int i = 0; i < FARAeroUtil.CurEditorParts.Count; i++)
            {
                Part p = FARAeroUtil.CurEditorParts[i];
                if (FARAeroUtil.IsNonphysical(p))
                    continue;

                Vector3 part_pos = p.transform.TransformPoint(p.CoMOffset) - CoM;
                double partMass = p.mass;
                if (vehicleFueled && p.Resources.Count > 0)
                    partMass += p.GetResourceMass();

                double stock_drag = partMass * p.maximum_drag * FlightGlobals.DragMultiplier * 1000;
                Cd += stock_drag;
                Cm += stock_drag * -Vector3d.Dot(part_pos, liftVector);
                Cn += stock_drag * Vector3d.Dot(part_pos, sideways);
            }
            if (area == 0)
            {
                area = 1;
                b_2 = 1;
                MAC = 1;
            }

            double recipArea = 1 / area;

            MAC *= recipArea;
            b_2 *= recipArea;
            Cl *= recipArea;
            Cd *= recipArea;
            Cm *= recipArea / MAC;
            Cy *= recipArea;
            Cn *= recipArea / b_2;
            C_roll *= recipArea / b_2;
        }
    }
}
