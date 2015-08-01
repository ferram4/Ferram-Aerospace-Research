using System;
using UnityEngine;

namespace FerramAerospaceResearch
{
    //recyclable float curve
    class FARFloatCurve
    {
        struct CubicSection
        {
            public double a, b, c, d;
            public double upperLim, lowerLim;
            public int nextIndex, prevIndex;

            public void BuildSection(Vector3d upperInputs, Vector3d lowerInputs)
            {
                //Creates cubic from x,y,dy/dx data

                double recipXDiff, recipXDiffSq;
                recipXDiff = 1 / (lowerInputs.x - upperInputs.x);
                recipXDiffSq = recipXDiff * recipXDiff;

                a = 2 * (upperInputs.y - lowerInputs.y) * recipXDiff;
                a += upperInputs.z + lowerInputs.z;
                a *= recipXDiffSq;

                b = 3 * (upperInputs.x + lowerInputs.x) * (lowerInputs.y - upperInputs.y) * recipXDiff;
                b -= (lowerInputs.x + 2 * upperInputs.x) * lowerInputs.z;
                b -= (2 * lowerInputs.x + upperInputs.x) * upperInputs.z;

                c = 6 * upperInputs.x * lowerInputs.x * (upperInputs.y - lowerInputs.y) * recipXDiff;
                c += (2 * lowerInputs.x * upperInputs.x + upperInputs.x * upperInputs.x) * lowerInputs.z;
                c += (2 * lowerInputs.x * upperInputs.x + lowerInputs.x * lowerInputs.x) * upperInputs.z;

                d = (3 * lowerInputs.x - upperInputs.x) * upperInputs.x * upperInputs.x * lowerInputs.y;
                d += (lowerInputs.x - 3 * upperInputs.x) * lowerInputs.x * lowerInputs.x * upperInputs.y;
                d *= recipXDiff;

                d -= lowerInputs.x * upperInputs.x * upperInputs.x * lowerInputs.z;
                d -= lowerInputs.x * lowerInputs.x * upperInputs.x * upperInputs.z;
                d *= recipXDiffSq;

                upperLim = upperInputs.x;
                lowerLim = lowerInputs.x;
            }

            public double Evaluate(double x)
            {
                double y = a * x;
                y += b;
                y *= x;
                y += c;
                y *= x;
                y += d;

                return y;
            }

            public double EvalUpperLim()
            {
                return Evaluate(upperLim);
            }

            public double EvalLowerLim()
            {
                return Evaluate(lowerLim);
            }

            public int CheckRange(double x)
            {
                if (x > upperLim)
                    return 1;
                if (x < lowerLim)
                    return -1;

                return 0;
            }
        }

        Vector3d[] controlPoints;
        CubicSection[] sections;
        int centerIndex;

        private FARFloatCurve() { }
        public FARFloatCurve(int numControlPoints)
        {
            controlPoints = new Vector3d[numControlPoints];

            sections = new CubicSection[numControlPoints - 1];

            centerIndex = (sections.Length - 1) / 2;

            SetNextPrevIndices(sections.Length - 1, 0, centerIndex);
        }

        private void SetNextPrevIndices(int upperIndex, int lowerIndex, int curIndex)
        {
            if (curIndex == lowerIndex)
                return;

            int nextIndex, prevIndex;

            nextIndex = (upperIndex + curIndex + 1) / 2;
            prevIndex = (lowerIndex + curIndex - 1) / 2;

            sections[curIndex].nextIndex = nextIndex;
            sections[curIndex].prevIndex = prevIndex;

            SetNextPrevIndices(curIndex - 1, lowerIndex, prevIndex);
            SetNextPrevIndices(upperIndex, curIndex + 1, nextIndex);
        }

        //uses x for x point, y for y point and z for dy/dx
        public void SetPoint(int index, Vector3d controlPoint)
        {
            controlPoints[index] = controlPoint;
        }

        public void BakeCurve()
        {
            for(int i = 0; i < sections.Length; i++)
            {
                sections[i].BuildSection(controlPoints[i], controlPoints[i + 1]);
            }
        }

        public double Evaluate(double x)
        {
            int curIndex = centerIndex;
            while(true)
            {
                int check = sections[curIndex].CheckRange(x);
                if (check > 0)       //above of this cubic's range
                    if (curIndex == sections.Length - 1)
                        return sections[curIndex].EvalUpperLim();   //at upper end of curve, just return max val of last cubic
                    else
                    {
                        curIndex = sections[curIndex].nextIndex;    //otherwise, find next cubic to check and continue
                        continue;
                    }
                else if (check < 0) //below this cubic's range
                    if (curIndex == 0)
                        return sections[curIndex].EvalLowerLim();   //at lower end of curve, return min val of first cubic
                    else
                    {
                        curIndex = sections[curIndex].prevIndex;    //otherwise, find next cubic to check and continue
                        continue;
                    }

                return sections[curIndex].Evaluate(x);          //if we get here, we're in range and should evaluate this cubic
            }
        }
    }
}
