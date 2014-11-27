using System;
using System.Collections.Generic;

namespace FerramAerospaceResearch.FARWing
{
    class FARWingVortexPanel
    {
        const double QUARTER_PI = 0.07957747154594766788444188168626;
        const double FOUR_PI = 12.566370614359172953850573533118;

        double halfWidth;
        double quarterHeight;

        Vector3d localPosition;
        Vector3d flowFieldPosition;

        Vector3d flowFieldNormalVector;
        Vector3d flowFieldPerpVector;

        double strength;
        double normalVel;

        
        public void UpdateStrength(double beta)
        {
            double strengthChange = EstimateStrengthChange(beta) - strength;
            strength += strength * 0.1;     //damping factor to prevent divergent behavior
        }

        private double EstimateStrengthChange(double beta)
        {
            Vector3d inducedInfluence = InducedInfluence(flowFieldPosition - new Vector3d(1, 0, 0) * quarterHeight, beta);
            double normalInfluence = Vector3d.Dot(inducedInfluence, flowFieldNormalVector);

            double newStrength = normalVel * FOUR_PI / normalInfluence;

            return newStrength;
        }
        
        public Vector3d InducedVelocity(Vector3d position, double beta)
        {
            Vector3d inducedVel = InducedInfluence(position, beta);

            inducedVel *= strength;
            inducedVel *= QUARTER_PI;        //0.25 * pi;

            return inducedVel;
        }

        private Vector3d InducedInfluence(Vector3d position, double beta)
        {
            Vector3d r1, r2;    //corners of horseshoe vortex

            Vector3d forward = new Vector3d(1, 0, 0);


            r1 = flowFieldPosition + quarterHeight * forward;
            r2 = r1;

            r1 -= halfWidth * flowFieldPerpVector;      //set the start points of the horseshoe vertices
            r2 += halfWidth * flowFieldPerpVector;

            Vector3d r0 = r2 - r1;      //a vector from one corner of the horseshoe vertex to the other

            r1 = position - r1;
            r2 = position - r2;

            r0.y *= beta;       //account for coordinate transformation due to Mach number effects
            r0.z *= beta;

            r1.y *= beta;
            r1.z *= beta;

            r2.y *= beta;
            r2.z *= beta;

            double r1SqMag, r2SqMag;
            r1SqMag = r1.sqrMagnitude;
            r2SqMag = r2.sqrMagnitude;

            Vector3d semiInfinite1, semiInfinite2;

            if (r1SqMag < 0 && r2SqMag < 0)     //both points are outside the Mach cone; there is no influence, return 0
                return Vector3d.zero;

            double r1Mag, r2Mag;
            r1Mag = Math.Sqrt(r1SqMag);
            r2Mag = Math.Sqrt(r2SqMag);

            if (r1SqMag > 0)     //A SqrMag > 0 indicates being inside of this vortex's Mach cone; evaluate 
            {
                semiInfinite1 = new Vector3d(0, r1.z, -r1.y);

                if (semiInfinite1 != Vector3d.zero)
                {
                    semiInfinite1 /= semiInfinite1.sqrMagnitude;

                    semiInfinite1 *= 1 + r1.x / r1Mag;
                }
            }
            else
                semiInfinite1 = Vector3d.zero;

            if (r2SqMag > 0)
            {
                semiInfinite2 = -new Vector3d(0, r2.z, -r2.y);

                if (semiInfinite2 != Vector3d.zero)
                {
                    semiInfinite2 /= semiInfinite2.sqrMagnitude;

                    semiInfinite2 *= 1 + r2.x / r2Mag;
                }
            }
            else
                semiInfinite2 = Vector3d.zero;

            Vector3d topOfVortex = Vector3d.Cross(r1, r2);
            if (topOfVortex != Vector3d.zero)
            {
                topOfVortex /= topOfVortex.sqrMagnitude;
                topOfVortex *= (Vector3d.Dot(r0, r1) / r1Mag + Vector3d.Dot(r0, r2) / r2Mag);
            }

            return topOfVortex + semiInfinite1 + semiInfinite2;
        }
    }
}
