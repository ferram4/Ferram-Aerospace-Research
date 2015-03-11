/*
Ferram Aerospace Research v0.14.6
Copyright 2014, Michael Ferrara, aka Ferram4

    This file is part of Ferram Aerospace Research.

    Ferram Aerospace Research is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Ferram Aerospace Research is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Ferram Aerospace Research.  If not, see <http://www.gnu.org/licenses/>.

    Serious thanks:		a.g., for tons of bugfixes and code-refactorings
            			Taverius, for correcting a ton of incorrect values
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
            			ialdabaoth (who is awesome), who originally created Module Manager
                        Regex, for adding RPM support
            			Duxwing, for copy editing the readme
 * 
 * Kerbal Engineer Redux created by Cybutek, Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License
 *      Referenced for starting point for fixing the "editor click-through-GUI" bug
 *
 * Part.cfg changes powered by sarbian & ialdabaoth's ModuleManager plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/55219
 *
 * Toolbar integration powered by blizzy78's Toolbar plugin; used with permission
 *	http://forum.kerbalspaceprogram.com/threads/60863
 */
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP;

namespace FerramAerospaceResearch.FARPartGeometry
{
    public class GeometryPartModule : PartModule
    {
        public Transform partTransform;
        public Rigidbody partRigidBody;

        public Bounds overallMeshBounds;

        public List<GeometryMesh> meshDataList;

        void Start()
        {
            RebuildAllMeshData();
            //part.OnEditorAttach += EditorAttach;
        }

        private void RebuildAllMeshData()
        {
            partTransform = part.transform;
            partRigidBody = part.Rigidbody;
            List<Transform> meshTransforms = PartModelTransformList(this.part);
            List<Mesh> geometryMeshes = CreateMeshListFromTransforms(ref meshTransforms);

            meshDataList = new List<GeometryMesh>();

            Matrix4x4 worldToVesselMatrix;
            if (HighLogic.LoadedSceneIsFlight)
                worldToVesselMatrix = vessel.ReferenceTransform.worldToLocalMatrix;
            else
                worldToVesselMatrix = EditorLogic.RootPart.transform.worldToLocalMatrix;

            for (int i = 0; i < meshTransforms.Count; i++)
            {
                Mesh m = geometryMeshes[i];
                GeometryMesh geoMesh = new GeometryMesh(m.vertices, m.triangles, m.bounds, meshTransforms[i], worldToVesselMatrix);
                meshDataList.Add(geoMesh);
            }

            overallMeshBounds = part.GetPartOverallMeshBoundsInBasis(worldToVesselMatrix);
        }

        /*public void EditorAttach()
        {
            UpdateTransformMatrixList(EditorLogic.RootPart.transform.worldToLocalMatrix);
            overallMeshBounds = part.GetPartOverallMeshBoundsInBasis(EditorLogic.RootPart.transform.worldToLocalMatrix);
        }*/

        public void UpdateTransformMatrixList(Matrix4x4 worldToVesselMatrix)
        {
            for (int i = 0; i < meshDataList.Count; i++)
                meshDataList[i].TransformBasis(worldToVesselMatrix);
        }

        private List<Mesh> CreateMeshListFromTransforms(ref List<Transform> meshTransforms)
        {
            List<Mesh> meshList = new List<Mesh>();
            List<Transform> validTransformList = new List<Transform>();

            Bounds rendererBounds = this.part.GetPartOverallMeshBoundsInBasis(part.transform.worldToLocalMatrix);
            Bounds colliderBounds = this.part.GetPartColliderBoundsInBasis(part.transform.worldToLocalMatrix);

            bool cantUseColliders = true;
            if (rendererBounds.size.x * rendererBounds.size.z < colliderBounds.size.x * colliderBounds.size.z * 1.6f && rendererBounds.size.y < colliderBounds.size.y * 1.2f && (rendererBounds.center - colliderBounds.center).magnitude < 0.3f)
            {
                foreach (Transform t in meshTransforms)
                {
                    MeshCollider mc = t.GetComponent<MeshCollider>();

                    if (mc != null)
                    {
                        Mesh m = mc.sharedMesh;

                        if (m == null)
                            continue;

                        meshList.Add(m);
                        validTransformList.Add(t);
                        cantUseColliders = false;
                    }
                    else
                    {
                        BoxCollider bc = t.GetComponent<BoxCollider>();
                        if (bc == null)
                            continue;

                        meshList.Add(CreateBoxMeshFromBoxCollider(bc.size, bc.center));
                        validTransformList.Add(t);
                        cantUseColliders = false;
                    }
                }
            }

            if (cantUseColliders)
            {
                foreach (Transform t in meshTransforms)
                {
                    MeshCollider mc = t.GetComponent<MeshCollider>();

                    if (mc != null)
                    {
                        continue;
                    }
                    else
                    {
                        MeshFilter mf = t.GetComponent<MeshFilter>();
                        if (mf == null)
                            continue;
                        Mesh m = mf.sharedMesh;

                        if (m == null)
                            continue;

                        meshList.Add(m);
                        validTransformList.Add(t);
                    }
                }
            }

            if (part.Modules.Contains("ModuleJettison"))
            {
                ModuleJettison[] jettisons = part.GetComponents<ModuleJettison>();
                foreach (ModuleJettison j in jettisons)
                {
                    if (j.isJettisoned || j.jettisonTransform == null || j.checkBottomNode)
                        continue;

                    Transform t = j.jettisonTransform;
                    MeshFilter mf = t.GetComponent<MeshFilter>();

                    if (mf == null)
                        continue;
                    Mesh m = mf.sharedMesh;

                    if (m == null)
                        continue;

                    meshList.Add(m);
                    validTransformList.Add(t);
                }
            }

            meshTransforms = validTransformList;
            return meshList;
        }

        private static Mesh CreateBoxMeshFromBoxCollider(Vector3 size, Vector3 center)
        {
            List<Vector3> Points = new List<Vector3>();
            List<Vector3> Verts = new List<Vector3>();
            List<int> Tris = new List<int>();

            Vector3 extents = size * 0.5f;

            Points.Add(new Vector3(center.x - extents.x, center.y + extents.y, center.z - extents.z));
            Points.Add(new Vector3(center.x + extents.x, center.y + extents.y, center.z - extents.z));
            Points.Add(new Vector3(center.x + extents.x, center.y - extents.y, center.z - extents.z));
            Points.Add(new Vector3(center.x - extents.x, center.y - extents.y, center.z - extents.z));
            Points.Add(new Vector3(center.x + extents.x, center.y + extents.y, center.z + extents.z));
            Points.Add(new Vector3(center.x - extents.x, center.y + extents.y, center.z + extents.z));
            Points.Add(new Vector3(center.x - extents.x, center.y - extents.y, center.z + extents.z));
            Points.Add(new Vector3(center.x + extents.x, center.y - extents.y, center.z + extents.z));

            Mesh mesh = new Mesh();
            // Front plane
            Verts.Add(Points[0]); Verts.Add(Points[1]); Verts.Add(Points[2]); Verts.Add(Points[3]);
            // Back plane
            Verts.Add(Points[4]); Verts.Add(Points[5]); Verts.Add(Points[6]); Verts.Add(Points[7]);
            // Left plane
            Verts.Add(Points[5]); Verts.Add(Points[0]); Verts.Add(Points[3]); Verts.Add(Points[6]);
            // Right plane
            Verts.Add(Points[1]); Verts.Add(Points[4]); Verts.Add(Points[7]); Verts.Add(Points[2]);
            // Top plane
            Verts.Add(Points[5]); Verts.Add(Points[4]); Verts.Add(Points[1]); Verts.Add(Points[0]);
            // Bottom plane
            Verts.Add(Points[3]); Verts.Add(Points[2]); Verts.Add(Points[7]); Verts.Add(Points[6]);
            // Front Plane
            Tris.Add(0); Tris.Add(1); Tris.Add(2);
            Tris.Add(2); Tris.Add(3); Tris.Add(0);
            // Back Plane
            Tris.Add(4); Tris.Add(5); Tris.Add(6);
            Tris.Add(6); Tris.Add(7); Tris.Add(4);
            // Left Plane
            Tris.Add(8); Tris.Add(9); Tris.Add(10);
            Tris.Add(10); Tris.Add(11); Tris.Add(8);
            // Right Plane
            Tris.Add(12); Tris.Add(13); Tris.Add(14);
            Tris.Add(14); Tris.Add(15); Tris.Add(12);
            // Top Plane
            Tris.Add(16); Tris.Add(17); Tris.Add(18);
            Tris.Add(18); Tris.Add(19); Tris.Add(16);
            // Bottom Plane
            Tris.Add(20); Tris.Add(21); Tris.Add(22);
            Tris.Add(22); Tris.Add(23); Tris.Add(20);

            mesh.vertices = Verts.ToArray();
            mesh.triangles = Tris.ToArray();

            Points = null;
            Verts = null;
            Tris = null;

            mesh.RecalculateBounds();

            mesh.Optimize();

            return mesh;
        }

        private static List<Transform> PartModelTransformList(Part p)
        {
            List<Transform> returnList = new List<Transform>();

            List<Transform> propellersToIgnore = IgnoreModelTransformList(p);

            returnList.AddRange(p.FindModelComponents<Transform>());

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
