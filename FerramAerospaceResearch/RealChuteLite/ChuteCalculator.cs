using System;
using UnityEngine;

namespace FerramAerospaceResearch.RealChuteLite
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class ChuteCalculator : MonoBehaviour
    {
        private void Start()
        {
            foreach (AvailablePart part in PartLoader.Instance.parts)
            {
                Part prefab = part.partPrefab;
                if (prefab != null && prefab.Modules.Contains("RealChuteFAR"))
                {
                    //Updates the part's GetInfo.
                    RealChuteFAR module = prefab.Modules["RealChuteFAR"] as RealChuteFAR;
                    DragCubeSystem.Instance.LoadDragCubes(prefab);
                    DragCube semi = prefab.DragCubes.Cubes.Find(c => c.Name == "SEMIDEPLOYED"), deployed = prefab.DragCubes.Cubes.Find(c => c.Name == "DEPLOYED");
                    module.preDeployedDiameter = GetApparentDiameter(semi);
                    module.deployedDiameter = GetApparentDiameter(deployed);
                    AvailablePart.ModuleInfo moduleInfo = part.moduleInfos.Find(m => m.moduleName == "RealChute");
                    moduleInfo.info = module.GetInfo();
                }
            }
        }

        //Retreives an "apparent" diameter from a DragCube
        private float GetApparentDiameter(DragCube cube)
        {
            float area = 0;
            for (int i = 0; i < 6; i++)
            {
                area += cube.Area[i] * cube.Drag[i]
                    * PhysicsGlobals.DragCurveValue((Vector3.Dot(Vector3.up, DragCubeList.GetFaceDirection((DragCube.DragFace)i)) + 1f) * 0.5f, 0);
            }
            return (float)(Math.Max(Math.Round(Math.Sqrt((area * PhysicsGlobals.DragCubeMultiplier * PhysicsGlobals.DragMultiplier) / Mathf.PI) * 2d, 1), 0.1));
        }
    }
}
