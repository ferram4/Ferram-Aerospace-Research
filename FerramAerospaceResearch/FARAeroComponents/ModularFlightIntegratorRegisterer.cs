using System;
using System.Collections.Generic;
using ModularFI;
using UnityEngine;

namespace FerramAerospaceResearch.FARAeroComponents
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class ModularFlightIntegratorRegisterer : MonoBehaviour
    {
        void Start()
        {
            Debug.Log("FAR Modular Flight Integrator function registration started");
            ModularFlightIntegrator.RegisterUpdateAerodynamicsOverride(UpdateAerodynamics);
            Debug.Log("FAR Modular Flight Integrator function registration complete");
            GameObject.Destroy(this);
        }

        void UpdateAerodynamics(ModularFlightIntegrator fi, Part part)
        {
            if (part.Modules.Contains("ModuleAeroSurface") || part.Modules.Contains("KerbalEVA"))     //FIXME Proper model for airbrakes
            {
                fi.BaseFIUpdateAerodynamics(part);
                return;
            }
            else if (!part.DragCubes.None)
            {
                Rigidbody rb = part.Rigidbody;
                if (rb)
                    part.DragCubes.SetDrag(-(rb.velocity + Krakensbane.GetFrameVelocityV3f()).normalized, (float)part.machNumber);
            }

            FARAeroPartModule aeroModule = part.GetComponent<FARAeroPartModule>();

            part.radiativeArea = CalculateAreaRadiative(fi, part, aeroModule);
            part.exposedArea = CalculateAreaExposed(fi, part, aeroModule);
        }

        double CalculateAreaRadiative(ModularFlightIntegrator fi, Part part, FARAeroPartModule aeroModule)
        {
            double dragCubeExposed = fi.BaseFICalculateAreaExposed(part);
            if ((object)aeroModule == null)
                return fi.BaseFICalculateAreaRadiative(part);
            else
            {
                return aeroModule.ProjectedAreas.totalArea;
            }
        }

        double CalculateAreaExposed(ModularFlightIntegrator fi, Part part, FARAeroPartModule aeroModule)
        {
            double dragCubeExposed = fi.BaseFICalculateAreaExposed(part);
            if (aeroModule == null)
                return dragCubeExposed;
            else
            {
                double cubeRadiative = fi.BaseFICalculateAreaRadiative(part);
                if (cubeRadiative > 0)
                    return aeroModule.ProjectedAreas.totalArea * dragCubeExposed / cubeRadiative;
                else
                    return aeroModule.ProjectedAreas.totalArea;
            }
        }
    }
}
