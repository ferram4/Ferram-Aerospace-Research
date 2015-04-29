using System;
using System.Collections.Generic;
using UnityEngine;
using FerramAerospaceResearch.RealChuteLite;

namespace FerramAerospaceResearch.FARAeroComponents
{
    class TmpFlightIntegratorOverride : FlightIntegrator
    {
        protected override void UpdateAerodynamics(Part part)
        {
            double extraArea = 0;
            if (part.Modules.Contains("ModuleAeroSurface"))     //FIXME Proper model for airbrakes
                base.UpdateAerodynamics(part);

            
            //base.UpdateAerodynamics(part);
            part.radiativeArea = CalculateAreaRadiative(part) + extraArea;
            part.exposedArea = CalculateAreaExposed(part) + extraArea;
        }

        protected override double CalculateAreaRadiative(Part part)
        {
            FARAeroPartModule a = part.GetComponent<FARAeroPartModule>();
            double dragCubeExposed = base.CalculateAreaExposed(part);
            if (a == null)
                return base.CalculateAreaRadiative(part);
            else
            {
                return a.ProjectedAreas.totalArea;
            }
        }

        protected override double CalculateAreaExposed(Part part)
        {
            FARAeroPartModule a = part.GetComponent<FARAeroPartModule>();
            double dragCubeExposed = base.CalculateAreaExposed(part);
            if (a == null)
                return dragCubeExposed;
            else
            {
                return a.ProjectedAreas.totalArea;// / part.DragCubes.GetCubeAreaDir(a.partLocalVel);
            }
        }
    }
}
