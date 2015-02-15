using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;

namespace FerramAerospaceResearch.FARAeroComponents
{
    //Used to apply forces to vessel based on data from FARVesselAero
    public class FARAeroPartModule : PartModule
    {
        float dragPerDynPres = 0;
        float newDragPerDynPres = 0;
        public bool updateDrag = false;

        void Start()
        {
            part.maximum_drag = 0;
            part.minimum_drag = 0;
            part.angularDrag = 0;
        }

        void FixedUpdate()
        {
            if (FlightGlobals.ready && part && part.Rigidbody)
            {
                Vector3 velocity = part.Rigidbody.velocity + Krakensbane.GetFrameVelocityV3f();
                Vector3 velNorm = velocity.normalized;
                float dynPres = velocity.sqrMagnitude * 0.0005f * (float)vessel.atmDensity;     //In kPa

                if (updateDrag)
                {
                    updateDrag = false;
                    dragPerDynPres = newDragPerDynPres;
                    newDragPerDynPres = 0;
                }
                if(!float.IsNaN(dragPerDynPres))
                    part.Rigidbody.AddForce(velNorm * dynPres * dragPerDynPres * part.mass);
            }
        }

        public void IncrementNewDragPerDynPres(float dragIncrement)
        {
            newDragPerDynPres += dragIncrement;
        }
    }
}
