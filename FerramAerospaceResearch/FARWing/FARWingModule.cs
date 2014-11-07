using System;
using System.Collections.Generic;
using KSP;
using UnityEngine;
using ferram4;

namespace ferram4.FARWing
{
    public class FARWingModule : PartModule
    {
        //An ordered list of the points that make up this planform, defined in part-local space
        protected List<Vector3d> wingPlanformPoints = null;

        private LineRenderer line = null;

        public void Start()
        {
            if(wingPlanformPoints == null)
            {
                wingPlanformPoints = new List<Vector3d>();
                FARWingGeometryCalculator wingGeoCalc = new FARWingGeometryCalculator(part);
                wingPlanformPoints = wingGeoCalc.CalculateWingPlanformPoints();
            }

            GameObject obj = new GameObject("Line");

            // Then create renderer itself...
            line = obj.AddComponent<LineRenderer>();
            line.transform.parent = transform; // ...child to our part...
            line.useWorldSpace = false; // ...and moving along with it (rather 
            // than staying in fixed world coordinates)
            line.transform.localPosition = Vector3.zero;
            line.transform.localEulerAngles = Vector3.zero;

            // Make it render a red to yellow triangle, 1 meter wide and 2 meters long
            line.material = new Material(Shader.Find("Particles/Additive"));
            line.SetColors(Color.cyan, Color.cyan);
            line.SetWidth(0.1f, 0.1f);
            line.SetVertexCount(wingPlanformPoints.Count);

            string s = "";
            for (int i = 0; i < wingPlanformPoints.Count; i++)
            {
                s += "Point " + i + ": " + wingPlanformPoints[i] + "\n\r";
                line.SetPosition(i, wingPlanformPoints[i]);
            }
            line.SetPosition(0, wingPlanformPoints[0]);
            Debug.Log(s);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if(node.HasNode("wingPlanformPoints"))
            {
                ConfigNode planformPoints = node.GetNode("wingPlanformPoints");
                wingPlanformPoints = new List<Vector3d>();

                string[] pointStrings = planformPoints.GetValues("point");

                for (int i = 0; i < pointStrings.Length; i++)
                {
                    string ithPointString = pointStrings[i];
                    string[] xyValues = ithPointString.Split(new char[] {' ', ',', ';'});

                    Vector3d ithPoint = new Vector3d(double.Parse(xyValues[0]), double.Parse(xyValues[1]), 0);

                    wingPlanformPoints.Add(ithPoint);
                }
            }
        }
    }
}
