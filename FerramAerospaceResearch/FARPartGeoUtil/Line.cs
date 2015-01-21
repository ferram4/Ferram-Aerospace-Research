using System;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeoUtil
{
    class Line : IEquatable<Line>
    {
        public Vector3 point1;
        public Vector3 point2;

        public Line(Vector3 point1, Vector3 point2)
        {
            if (point1.y < point2.y)
            {
                this.point2 = point2;
                this.point1 = point1;
            }
            else
            {
                this.point1 = point2;
                this.point2 = point1;
            }
        }

        public void TransformLine(Matrix4x4 transformationMatrix)
        {
            point1 = transformationMatrix.MultiplyVector(point1);
            point2 = transformationMatrix.MultiplyVector(point2);
            if(point1.y > point2.y)
            {
                Vector3 tmp = point1;
                point1 = point2;
                point2 = tmp;
            }
        }

        public Vector3 GetPoint(float yVal)
        {
            Vector3 point = new Vector3();
            point.y = yVal;

            float tmp = (point1.y - point2.y);
            tmp = 1 / tmp;

            point.x = (point1.x - point2.x) * tmp * (yVal - point2.y) + point2.x;
            point.z = (point1.z - point2.z) * tmp * (yVal - point2.y) + point2.z;

            return point;
        }

        public bool Equals(Line other)
        {
            if (other.point1 == this.point1 && other.point2 == this.point2)
                return true;

            return false;
        }

        public override int GetHashCode()
        {
            return point1.GetHashCode() * point2.GetHashCode();
        }
    }
}
