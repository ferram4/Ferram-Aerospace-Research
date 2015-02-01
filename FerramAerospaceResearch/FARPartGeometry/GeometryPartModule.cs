using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FerramAerospaceResearch.FARPartGeometry
{
    public class GeometryPartModule : PartModule
    {
        public Transform partTransform;
        public Rigidbody partRigidBody;

        public List<Mesh> geometryMeshes;
        private List<Transform> meshTransforms;
        public List<Matrix4x4> meshToVesselMatrixList = new List<Matrix4x4>();
        public Bounds overallMeshBounds;

        void Start()
        {
            partTransform = part.transform;
            partRigidBody = part.Rigidbody;
            meshTransforms = PartModelTransformList(this.part);
            geometryMeshes = CreateMeshListFromTransforms(meshTransforms);
            if (this.vessel)
                UpdateTransformMatrixList(vessel.vesselTransform.worldToLocalMatrix);
            else
                UpdateTransformMatrixList(EditorLogic.RootPart.partTransform.worldToLocalMatrix);
        }

        public void UpdateTransformMatrixList(Matrix4x4 worldToVesselMatrix)
        {
            meshToVesselMatrixList.Clear();
            for (int i = 0; i < meshTransforms.Count; i++)
                meshToVesselMatrixList.Add(worldToVesselMatrix * meshTransforms[i].localToWorldMatrix);
            overallMeshBounds = part.GetPartOverallMeshBoundsInBasis(worldToVesselMatrix);
        }

        private static List<Mesh> CreateMeshListFromTransforms(List<Transform> meshTransforms)
        {
            List<Mesh> meshList = new List<Mesh>();
            foreach (Transform t in meshTransforms)
            {
                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (mf == null)
                    continue;
                Mesh m = mf.sharedMesh;

                if (m == null)
                    continue;

                meshList.Add(m);
            }
            return meshList;
        }

        private static List<Transform> PartModelTransformList(Part p)
        {
            List<Transform> returnList = new List<Transform>();

            List<Transform> propellersToIgnore = IgnoreModelTransformList(p);

            returnList.AddRange(p.FindModelComponents<Transform>());

            if (p.Modules.Contains("ModuleJettison"))
            {
                ModuleJettison[] jettisons = p.GetComponents<ModuleJettison>();
                foreach (ModuleJettison j in jettisons)
                {
                    if (j.isJettisoned || j.jettisonTransform == null)
                        continue;

                    returnList.Add(j.jettisonTransform);
                }
            }

            foreach (Transform t in propellersToIgnore)
                returnList.Remove(t);

            return returnList;
        }

        private static List<Transform> IgnoreModelTransformList(Part p)
        {
            PartModule module;
            string transformString;
            List<Transform> Transform = new List<Transform>();

            if (p.Modules.Contains("FSplanePropellerSpinner"))
            {
                module = p.Modules["FSplanePropellerSpinner"];
                transformString = (string)module.GetType().GetField("propellerName").GetValue(module);
                if (transformString != "")
                {
                    Transform.AddRange(p.FindModelComponents<Transform>(transformString));
                }
                transformString = (string)module.GetType().GetField("rotorDiscName").GetValue(module);
                if (transformString != "")
                {
                    Transform.AddRange(p.FindModelComponents<Transform>(transformString));
                }

                transformString = (string)module.GetType().GetField("blade1").GetValue(module);
                if (transformString != "")
                {
                    Transform.AddRange(p.FindModelComponents<Transform>(transformString));
                }

                transformString = (string)module.GetType().GetField("blade2").GetValue(module);
                if (transformString != "")
                {
                    Transform.AddRange(p.FindModelComponents<Transform>(transformString));
                }

                transformString = (string)module.GetType().GetField("blade3").GetValue(module);
                if (transformString != "")
                {
                    Transform.AddRange(p.FindModelComponents<Transform>(transformString));
                }

                transformString = (string)module.GetType().GetField("blade4").GetValue(module);
                if (transformString != "")
                {
                    Transform.AddRange(p.FindModelComponents<Transform>(transformString));
                }
                transformString = (string)module.GetType().GetField("blade5").GetValue(module);
                if (transformString != "")
                {
                    Transform.AddRange(p.FindModelComponents<Transform>(transformString));
                }
            }
            if (p.Modules.Contains("FScopterThrottle"))
            {
                module = p.Modules["FScopterThrottle"];
                transformString = (string)module.GetType().GetField("rotorparent").GetValue(module);
                if (transformString != "")
                {
                    Transform.AddRange(p.FindModelComponents<Transform>(transformString));
                }
            }
            if (p.Modules.Contains("ModuleParachute"))
            {
                module = p.Modules["ModuleParachute"];
                transformString = (string)module.GetType().GetField("canopyName").GetValue(module);
                if (transformString != "")
                {
                    Transform.AddRange(p.FindModelComponents<Transform>(transformString));
                }
            }
            if (p.Modules.Contains("RealChuteModule"))
            {
                module = p.Modules["RealChuteModule"];
                transformString = (string)module.GetType().GetField("parachuteName").GetValue(module);
                if (transformString != "")
                {
                    Transform.AddRange(p.FindModelComponents<Transform>(transformString));
                }
                transformString = (string)module.GetType().GetField("secParachuteName").GetValue(module);
                if (transformString != "")
                {
                    Transform.AddRange(p.FindModelComponents<Transform>(transformString));
                }
            }
            foreach (Transform t in p.FindModelComponents<Transform>())
            {
                if (Transform.Contains(t))
                    continue;

                string tag = t.tag.ToLowerInvariant();
                if (tag == "ladder" || tag == "airlock")
                    Transform.Add(t);
            }

            return Transform;
        }
    }
}
