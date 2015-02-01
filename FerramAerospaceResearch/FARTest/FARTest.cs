using System;
using System.Collections.Generic;
using UnityEngine;
using KSP;
using FerramAerospaceResearch.FARPartGeometry;

namespace FerramAerospaceResearch.FARTest
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    class FARTest : MonoBehaviour
    {
        Rect windowPos;
        void OnGUI()
        {
            windowPos = GUILayout.Window(this.GetHashCode(), windowPos, TestGUI, "FARTest");
        }

        void TestGUI(int id)
        {
            List<Part> parts = EditorActionGroups.Instance.GetSelectedParts();
            if(parts.Count > 0)
            {
                if(GUILayout.Button("CrossSection"))
                    CreateAndDumpCrossSection(parts[0]);
            }
        }

        void CreateAndDumpCrossSection(Part p)
        {
            CrossSectionCurve xCurve, yCurve, zCurve;
            CrossSectionCurveGenerator.GetCrossSectionalAreaCurves(p, out xCurve, out yCurve, out zCurve);

            ConfigNode node = new ConfigNode("Cross Section Dump");
            node.AddNode(xCurve.Save("xCurve"));
            node.AddNode(yCurve.Save("yCurve"));
            node.AddNode(zCurve.Save("zCurve"));

            string savePath = KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/FerramAerospaceResearch/CrossSectionTest.cfg";
            node.Save(savePath);
        }
    }
}
