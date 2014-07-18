/*
Ferram Aerospace Research v0.14.0.1
Copyright 2014, Michael Ferrara, aka Ferram4

    This file is part of Ferram Aerospace Research.

    Ferram Aerospace Research is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Kerbal Joint Reinforcement is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Ferram Aerospace Research.  If not, see <http://www.gnu.org/licenses/>.

    Serious thanks:		a.g., for tons of bugfixes and code-refactorings
            			Taverius, for correcting a ton of incorrect values
            			sarbian, for refactoring code for working with MechJeb, and the Module Manager 1.5 updates
            			ialdabaoth (who is awesome), who originally created Module Manager
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

namespace ferram4
{
    public static class FARGeoUtil
    {
        private static Vector3 maxBounds = Vector3.zero;
        private static Vector3 minBounds = Vector3.zero;
        private static Vector2 lowerAxis1 = Vector2.zero;
        private static Vector2 lowerAxis2 = Vector2.zero;
        private static Vector2 upperAxis1 = Vector2.zero;
        private static Vector2 upperAxis2 = Vector2.zero;

        public static Transform[] ChooseNearbyModelTransforms(Part p, List<AttachNode> attachNodeGroup, Vector2 bounds, Transform[] PartModelTransforms, Transform[] AllVesselModelTransforms)
        {
            Vector3 nodeUpVector = Vector3.zero;
            Vector3 nodeCenter = Vector3.zero;
            float avgNodeDistFromCenter = 0;

            foreach (AttachNode attach in attachNodeGroup)
            {
                nodeUpVector += attach.orientation;
                nodeCenter += attach.position;
            }

            nodeUpVector.Normalize();
            nodeCenter /= attachNodeGroup.Count;

            foreach (AttachNode attach in attachNodeGroup)
            {
                avgNodeDistFromCenter += (attach.position - nodeCenter).magnitude;
            }
            avgNodeDistFromCenter /= attachNodeGroup.Count;

            Quaternion partToNode = Quaternion.FromToRotation(Vector3.up, nodeUpVector);        //This is the angle between part up and the node

            Quaternion rot = Quaternion.Inverse(partToNode);

            Matrix4x4 partToNodeMatrix = Matrix4x4.TRS(Vector3.zero, rot, Vector3.one);
            Matrix4x4 upMatrix = partToNodeMatrix * p.transform.worldToLocalMatrix;

            nodeCenter = partToNodeMatrix.MultiplyPoint(nodeCenter);

            Vector3 worldNodeVector = nodeUpVector;

            if (Vector3.Dot(nodeUpVector, nodeCenter) < 0)  //Make sure the direction is correct
                worldNodeVector = -worldNodeVector;

            worldNodeVector = (p.transform.localToWorldMatrix * Matrix4x4.TRS(Vector3.zero, partToNode, Vector3.one)).MultiplyVector(worldNodeVector);

            List<Transform> FinalModelTransforms = new List<Transform>();
            
            foreach(Transform t in AllVesselModelTransforms)
            {
                if (PartModelTransforms.Contains(t) || Vector3.Dot(t.position, worldNodeVector) < -0.2f)    //Can't be part of this part, nor can it be really far behind us
                    continue;

                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (mf == null)
                    continue;
                Mesh m = mf.mesh;

                if (m == null)
                    continue;

                Matrix4x4 orientMatrix = upMatrix * t.localToWorldMatrix;

                Vector3 locMaxBounds = orientMatrix.MultiplyPoint(m.bounds.max) - nodeCenter;
                Vector3 locMinBounds = orientMatrix.MultiplyPoint(m.bounds.min) - nodeCenter;

                if (locMaxBounds.x < locMinBounds.x)
                {
                    float tmp = locMinBounds.x;
                    locMinBounds.x = locMaxBounds.x;
                    locMaxBounds.x = tmp;
                }
                if (locMaxBounds.y < locMinBounds.y)
                {
                    float tmp = locMinBounds.y;
                    locMinBounds.y = locMaxBounds.y;
                    locMaxBounds.y = tmp;
                }
                if (locMaxBounds.z < locMinBounds.z)
                {
                    float tmp = locMinBounds.z;
                    locMinBounds.z = locMaxBounds.z;
                    locMaxBounds.z = tmp;
                }

                /*foreach (Vector3 vertex in m.vertices)
                {
                    Vector3 v = orientMatrix.MultiplyPoint(vertex) - nodeCenter;

                    locMaxBounds.x = Mathf.Max(locMaxBounds.x, v.x);
                    locMinBounds.x = Mathf.Min(locMinBounds.x, v.x);
                    locMaxBounds.y = Mathf.Max(locMaxBounds.y, v.y);
                    locMinBounds.y = Mathf.Min(locMinBounds.y, v.y);
                    locMaxBounds.z = Mathf.Max(locMaxBounds.z, v.z);
                    locMinBounds.z = Mathf.Min(locMinBounds.z, v.z);
                }*/

                if (locMaxBounds.x > -avgNodeDistFromCenter && locMaxBounds.y > -0.5f && locMaxBounds.z > -avgNodeDistFromCenter && locMinBounds.x < avgNodeDistFromCenter && locMinBounds.y < 0.5f && locMinBounds.z < avgNodeDistFromCenter)
                    FinalModelTransforms.Add(t);

            }

            return FinalModelTransforms.ToArray();
        }
        
        //This takes a part, a tolerance, an offset, a matrix describing its orientation and a list of model transforms and sets the part's taper data
        public static Vector2 NodeBoundaries(Part p, List<AttachNode> attachNodeGroup, Vector3 worldOffset, double toleranceForSizing, Transform[] ModelTransforms)
        {
            Vector3 nodeUpVector = Vector3.zero;
            Vector3 nodeCenter = Vector3.zero;

            foreach (AttachNode attach in attachNodeGroup)
            {
                nodeUpVector += attach.orientation;
                nodeCenter += attach.position;
            }

            nodeUpVector.Normalize();
            nodeCenter /= attachNodeGroup.Count;

            Quaternion partToNode = Quaternion.FromToRotation(Vector3.up, nodeUpVector);        //This is the angle between part up and the node

            Quaternion rot = Quaternion.Inverse(partToNode);

            Matrix4x4 partToNodeMatrix = Matrix4x4.TRS(Vector3.zero, rot, Vector3.one);
            Matrix4x4 upMatrix = partToNodeMatrix * p.transform.worldToLocalMatrix;

            nodeCenter = partToNodeMatrix.MultiplyPoint(nodeCenter);

            Vector2 bounds = new Vector2();
            
            foreach (Transform t in ModelTransforms)         //Get the max boundaries of the part
            {
                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (mf == null)
                    continue;
                Mesh m = mf.mesh;

                if (m == null)
                    continue;

                Matrix4x4 orientMatrix = upMatrix * t.localToWorldMatrix;

                foreach (Vector3 vertex in m.vertices)
                {
                    Vector3 v = orientMatrix.MultiplyPoint(vertex) - nodeCenter;

                    if (Mathf.Abs(v.y) <= toleranceForSizing)              //0.15f meter leeway to check for points near end of object
                    {
                        bounds.x = Mathf.Max(bounds.x, Mathf.Abs(v.x));
                        bounds.y = Mathf.Max(bounds.y, Mathf.Abs(v.z));
                    }
                }
            }

            return bounds;
        }

        public static Vector2 NodeBoundaries(Part p, AttachNode attachNode, Vector3 worldOffset, double toleranceForSizing, Transform[] ModelTransforms)
        {
            Vector3 nodeUpVector = attachNode.orientation;
            Vector3 nodeCenter = attachNode.position;

            Quaternion partToNode = Quaternion.FromToRotation(Vector3.up, nodeUpVector);        //This is the angle between part up and the node

            Quaternion rot = Quaternion.Inverse(partToNode);

            Matrix4x4 partToNodeMatrix = Matrix4x4.TRS(Vector3.zero, rot, Vector3.one);
            Matrix4x4 upMatrix = partToNodeMatrix * p.transform.worldToLocalMatrix;

            nodeCenter = partToNodeMatrix.MultiplyPoint(nodeCenter);

            Vector2 bounds = new Vector2();

            foreach (Transform t in ModelTransforms)         //Get the max boundaries of the part
            {
                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (mf == null)
                    continue;
                Mesh m = mf.mesh;

                if (m == null)
                    continue;

                Matrix4x4 orientMatrix = upMatrix * t.localToWorldMatrix;

                foreach (Vector3 vertex in m.vertices)
                {
                    Vector3 v = orientMatrix.MultiplyPoint(vertex) - nodeCenter;

                    if (Mathf.Abs(v.y) <= toleranceForSizing)              //0.15f meter leeway to check for points near end of object
                    {
                        bounds.x = Mathf.Max(bounds.x, Mathf.Abs(v.x));
                        bounds.y = Mathf.Max(bounds.y, Mathf.Abs(v.z));
                    }
                }
            }

            return bounds;
        }

        public static Vector2 NodeBoundaries(Part p, Vector3 position, Vector3 orientation, Vector3 worldOffset, double toleranceForSizing, Transform[] ModelTransforms)
        {
            Quaternion partToNode = Quaternion.FromToRotation(Vector3.up, orientation);        //This is the angle between part up and the node

            Quaternion rot = Quaternion.Inverse(partToNode);

            Matrix4x4 partToNodeMatrix = Matrix4x4.TRS(Vector3.zero, rot, Vector3.one);
            Matrix4x4 upMatrix = partToNodeMatrix * p.transform.worldToLocalMatrix;

            position = partToNodeMatrix.MultiplyPoint(position);

            Vector2 bounds = new Vector2();

            foreach (Transform t in ModelTransforms)         //Get the max boundaries of the part
            {
                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (mf == null)
                    continue;
                Mesh m = mf.mesh;

                if (m == null)
                    continue;

                Matrix4x4 orientMatrix = upMatrix * t.localToWorldMatrix;

                foreach (Vector3 vertex in m.vertices)
                {
                    Vector3 v = orientMatrix.MultiplyPoint(vertex) - position;

                    if (Mathf.Abs(v.y) <= toleranceForSizing)              //0.15f meter leeway to check for points near end of object
                    {
                        bounds.x = Mathf.Max(bounds.x, Mathf.Abs(v.x));
                        bounds.y = Mathf.Max(bounds.y, Mathf.Abs(v.z));
                    }
                }
            }

            return bounds;
        }

        public static String UpVectorFromDir(Vector3 direction)
        {
            float tmp = Mathf.Abs(direction.y);
            String upVectorName = "up";
            if (Mathf.Abs(direction.x) > tmp)
            {
                tmp = Mathf.Abs(direction.x);
                upVectorName = "right";
            }
            if (Mathf.Abs(direction.z) > tmp)
                upVectorName = "forward";
            return upVectorName;
        }

        public static Vector3 GuessUpVector(Part part)
        {
            // For intakes, use the intake vector
            if (part.Modules.Contains("ModuleResourceIntake"))
            {
                ModuleResourceIntake i = part.Modules["ModuleResourceIntake"] as ModuleResourceIntake;
                Transform intakeTrans = part.FindModelTransform(i.intakeTransformName);
                return part.transform.InverseTransformDirection(intakeTrans.forward);
            }
            // If surface attachable, and node normal is up, check stack nodes or use forward
            else if (part.srfAttachNode != null &&
                     part.attachRules.srfAttach &&
                     Mathf.Abs(part.srfAttachNode.orientation.normalized.y) > 0.9f)
            {
                // When the node normal is exactly Vector3.up, the editor orients forward along the craft axis
                Vector3 dir = Vector3.forward;
                String dirname = null;

                foreach (AttachNode node in part.attachNodes)
                {
                    // Doesn't seem to ever happen, but anyway
                    if (node.nodeType == AttachNode.NodeType.Surface)
                        continue;

                    if(node.id.ToLowerInvariant() == "strut")
                        continue;

                    // If all node orientations agree, use that axis
                    String name = UpVectorFromDir(node.orientation);

                    if (dirname == null)
                    {
                        dirname = name;
                        dir = node.orientation;
                    }
                    // Conflicting node directions - bail out
                    else if (dirname != name)
                        return Vector3.up;
                }

                Debug.Log(part.partInfo.title + ": Choosing " + (dirname == null ? "heuristic forward" : dirname) + " axis for FAR drag model.");
                return dir;
            }
            else
            {
                return Vector3.up;
            }
        }

        public static Quaternion GuessUpRotation(Part p)
        {
            Vector3 real_up = GuessUpVector(p).normalized;

            // Transformation from canonical orientation to the real local coordinates
            if (Mathf.Abs(real_up.y) < 0.9f)
                return Quaternion.FromToRotation(Vector3.up, real_up);
            else
                return Quaternion.identity;
        }

        public static Matrix4x4 WorldToPartUpMatrix(Part p)
        {
            // Transformation from local coords to canonical orientation
            Quaternion rot = Quaternion.Inverse(GuessUpRotation(p));

            return Matrix4x4.TRS(Vector3.zero, rot, Vector3.one) * p.transform.worldToLocalMatrix;
        }

        public static Transform[] VesselModelTransforms(Vessel v)
        {
            Transform[] allVesselTransforms = new Transform[0];

            foreach (Part p in v.Parts)
            {
                allVesselTransforms = allVesselTransforms.Concat(PartModelTransformArray(p)).ToArray();
            }

            return allVesselTransforms;
        }

        public static Transform[] VesselModelTransforms(List<Part> sortedShipList)
        {
            List<Transform> allVesselTransforms = new List<Transform>();

            foreach (Part p in sortedShipList)
            {
                allVesselTransforms.AddRange(PartModelTransformList(p));
            }

            return allVesselTransforms.ToArray();
        }

        //This takes a part and then finds all valid model transforms
        public static Transform[] PartModelTransformArray(Part p)
        {
            return PartModelTransformList(p).ToArray();
        }

        public static List<Transform> PartModelTransformList(Part p)
        {
            List<Transform> returnList = new List<Transform>();

            //Very hacky, but is necessary for root parts with broken transforms
            if (p.partTransform == null)
            {
                bool root = p == p.vessel.rootPart;
                Debug.Log("This one is busted: " + p.partInfo.title + " root? " + root);
                if (root)
                    p.partTransform = p.vessel.vesselTransform;
            } 
            
            Transform[] propellersToIgnore = IgnoreModelTransformArray(p);

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

            //foreach (Transform t in returnList)
            //    Debug.Log(t.name);
            Debug.Log("Part: " + p.partInfo.title + " Transforms: " + returnList.Count);
            return returnList;
        }

        private static Transform[] IgnoreModelTransformArray(Part p)
        {
            return IgnoreModelTransformList(p).ToArray();
        }

            
        private static List<Transform> IgnoreModelTransformList(Part p)
        {
            PartModule module;
            string transformString;
            List<Transform> Transform = new List<Transform>();
            //Transform[] tmp1, tmp2;
            //tmp1 = tmp2 = new Transform[0];

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

        //This takes a part, an offset, a matrix describing its orientation and a list of model transforms and sets the part's bounds
        public static Vector2 PartLengthBounds(Part p, Vector3 worldOffset, Matrix4x4 partUpMatrix, Transform[] ModelTransforms)
        {
            Vector2 bounds = new Vector2();
            foreach (Transform t in ModelTransforms)         //Get the max boundaries of the part
            {
                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (mf == null)
                    continue;
                Mesh m = mf.mesh;

                if (m == null)
                    continue;

                Matrix4x4 matrix = partUpMatrix * t.localToWorldMatrix;


                foreach (Vector3 vertex in m.vertices)
                {
                    Vector3 v = matrix.MultiplyPoint(vertex);

                    bounds.y = Mathf.Max(bounds.y, v.y);
                    bounds.y = Mathf.Min(bounds.y, v.y);
                }
            }

            return bounds;
        }

        //This takes a part, an offset, a matrix describing its orientation and a list of model transforms and sets the part's bounds
        private static void PartMaxBoundaries(Part p, Vector3 worldOffset, Matrix4x4 partUpMatrix, Transform[] ModelTransforms)
        {
            foreach (Transform t in ModelTransforms)         //Get the max boundaries of the part
            {
                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (mf == null)
                    continue;
                Mesh m = mf.mesh;

                if (m == null)
                    continue;

                Matrix4x4 matrix = partUpMatrix * t.localToWorldMatrix;


                foreach (Vector3 vertex in m.vertices)
                {
                    Vector3 v = matrix.MultiplyPoint(vertex);

                    maxBounds.x = Mathf.Max(maxBounds.x, v.x);
                    minBounds.x = Mathf.Min(minBounds.x, v.x);
                    maxBounds.y = Mathf.Max(maxBounds.y, v.y);
                    minBounds.y = Mathf.Min(minBounds.y, v.y);
                    maxBounds.z = Mathf.Max(maxBounds.z, v.z);
                    minBounds.z = Mathf.Min(minBounds.z, v.z);
                }
            }
        }

        //This takes a part, a tolerance, an offset, a matrix describing its orientation and a list of model transforms and sets the part's taper data
        private static void PartTaperBoundaries(Part p, double toleranceForTaper, Vector3 worldOffset, Matrix4x4 partUpMatrix, Transform[] ModelTransforms)
        {
            foreach (Transform t in ModelTransforms)         //Get the max boundaries of the part
            {
                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (mf == null)
                    continue;
                Mesh m = mf.mesh;

                if (m == null)
                    continue;

                Matrix4x4 matrix = partUpMatrix * t.localToWorldMatrix;


                foreach (Vector3 vertex in m.vertices)
                {
                    Vector3 v = matrix.MultiplyPoint(vertex);

                    if (Mathf.Abs(v.y - maxBounds.y) <= toleranceForTaper)              //0.15f meter leeway to check for points near end of object
                    {
                        upperAxis1.x = Mathf.Max(upperAxis1.x, v.x);
                        upperAxis1.y = Mathf.Max(upperAxis1.y, v.z);
                        upperAxis2.x = Mathf.Min(upperAxis2.x, v.x);
                        upperAxis2.y = Mathf.Min(upperAxis2.y, v.z);
                    }
                    if (Mathf.Abs(v.y - minBounds.y) <= toleranceForTaper)
                    {
                        lowerAxis1.x = Mathf.Max(lowerAxis1.x, v.x);
                        lowerAxis1.y = Mathf.Max(lowerAxis1.y, v.z);
                        lowerAxis2.x = Mathf.Min(lowerAxis2.x, v.x);
                        lowerAxis2.y = Mathf.Min(lowerAxis2.y, v.z);
                    }
                }
            }
        }
        public static BodyGeometryForDrag CalcBodyGeometryFromMesh(Part p)
        {
            BodyGeometryForDrag partGeometry = new BodyGeometryForDrag();

            maxBounds = new Vector3(-100, -100, -100);
            minBounds = new Vector3(100, 100, 100);

            int symmetryCounterpartNum = 1;
            //Vector3 centerOfSymCounterparts = p.transform.position;
            Matrix4x4 base_matrix = WorldToPartUpMatrix(p);

            Transform[] ModelTransforms = PartModelTransformArray(p);

            if (p.parent != null && p.GetComponent<FARPayloadFairingModule>() != null)
            {
                foreach (Part q in p.symmetryCounterparts)
                    if (q != p)
                    {
                        //centerOfSymCounterparts += q.transform.position;
                        symmetryCounterpartNum++;
                    }

                //centerOfSymCounterparts /= symmetryCounterpartNum;

                foreach (Part q in p.symmetryCounterparts)
                    if (q != p)
                    {
                        //Vector3 offset = q.transform.position - centerOfSymCounterparts;
                        PartMaxBoundaries(q, Vector3.zero, WorldToPartUpMatrix(q), ModelTransforms);
                    }
            }
            PartMaxBoundaries(p, Vector3.zero, base_matrix, ModelTransforms);

            upperAxis1 = new Vector2(-100, -100);                 //1s are the "positive" values
            lowerAxis1 = new Vector2(-100, -100);
            upperAxis2 = new Vector2(100, 100);               //2 are the "negative" values
            lowerAxis2 = new Vector2(100, 100);

            Vector3d size = maxBounds - minBounds;

            double toleranceForTaper = Mathf.Abs((float)size.y) / 5;
            toleranceForTaper = Math.Max(toleranceForTaper, 0.25);

            if (p.parent != null && p.GetComponent<FARPayloadFairingModule>() != null)
            {
                foreach (Part q in p.symmetryCounterparts)
                    if (q != p)
                    {
                        //Vector3 offset = q.transform.position - centerOfSymCounterparts;
                        PartTaperBoundaries(q, toleranceForTaper, Vector3.zero, WorldToPartUpMatrix(q), ModelTransforms);
                    }
            }
            PartTaperBoundaries(p, toleranceForTaper, Vector3.zero, base_matrix, ModelTransforms);

            Vector3d upperDiameters = (Vector3)(upperAxis1 - upperAxis2);
            Vector3d lowerDiameters = (Vector3)(lowerAxis1 - lowerAxis2);

            //            if (p.Modules.Contains("ModuleJettison"))
            //                upperDiameters = lowerDiameters = size;
            //size *= p.scaleFactor;
            Vector3d centroid = Vector3d.zero;

            double lowerR = lowerDiameters.magnitude * 0.5;
            double upperR = upperDiameters.magnitude * 0.5;
            centroid.y = 4 * (lowerR * lowerR + lowerR * upperR + upperR * upperR);
            centroid.y = size.y * (lowerR * lowerR + 2 * lowerR * upperR + 3 * upperR * upperR) / centroid.y;
            centroid.y += minBounds.y;
            centroid.x = (maxBounds.x + minBounds.x);
            centroid.z = (maxBounds.z + minBounds.z);

            partGeometry.originToCentroid = centroid;

            partGeometry.crossSectionalArea = (size.x * size.z) / 4;                    //avg radius
            partGeometry.crossSectionalArea *= Math.PI;
            partGeometry.crossSectionalArea /= (symmetryCounterpartNum);

            partGeometry.area = (Math.Abs(upperDiameters.x + upperDiameters.y) + Math.Abs(lowerDiameters.x + lowerDiameters.y)) / 4 * Math.PI * Math.Abs(size.y);              //surface area, not counting cross-sectional area
            partGeometry.area /= (symmetryCounterpartNum);


            partGeometry.finenessRatio = Math.Abs(size.x + size.z) / 2;
            partGeometry.finenessRatio = Math.Abs(size.y) / partGeometry.finenessRatio;

            partGeometry.majorMinorAxisRatio = Math.Abs(size.x / size.z);

            partGeometry.taperRatio = Math.Abs(upperDiameters.x + upperDiameters.y) / Math.Abs(lowerDiameters.x + lowerDiameters.y);

            partGeometry.taperCrossSectionArea = Math.Abs(Math.Pow((upperDiameters.x + upperDiameters.y) / 4, 2) - Math.Pow((lowerDiameters.x + lowerDiameters.y) / 4, 2)) * Math.PI;

            //This is the cross-sectional area of the tapered section


            //Debug.Log(p.partInfo.title + ": Geometry model created; Size: " + size + ", LD " + lowerDiameters + ", UD " + upperDiameters + "\n\rSurface area: " + partGeometry.area + "\n\rFineness Ratio: " + partGeometry.finenessRatio + "\n\rTaperRatio: " + partGeometry.taperRatio + "\n\rCross Sectional Area: " + partGeometry.crossSectionalArea + "\n\rCross Sectional Tapered Area: " + partGeometry.taperCrossSectionArea + "\n\rMajor-minor axis ratio: " + partGeometry.majorMinorAxisRatio + "\n\rCentroid: " + partGeometry.originToCentroid);
            return partGeometry;
        }

        public struct BodyGeometryForDrag
        {
            public double area;
            public double finenessRatio;
            public double taperRatio;
            public double crossSectionalArea;
            public double taperCrossSectionArea;
            public double majorMinorAxisRatio;
            public Vector3d originToCentroid;
        }
    }
}
