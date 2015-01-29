using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FerramAerospaceResearch.FARModule
{
    public class FARGeometryModule : PartModule
    {
        public Transform partTransform;
        public Rigidbody partRigidBody;

        public List<Mesh> geometryMeshes;
        private List<Transform> meshTransforms;
        public List<Matrix4x4> meshToWorldMatrixList = new List<Matrix4x4>();

        void Start()
        {
            partTransform = part.transform;
            partRigidBody = part.Rigidbody;

        }

        public void UpdateTransformMatrixList()
        {
            meshToWorldMatrixList.Clear();
            for (int i = 0; i < meshTransforms.Count; i++)
                meshToWorldMatrixList.Add(meshTransforms[i].localToWorldMatrix);
        }

    }
}
