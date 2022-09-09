// Copyright (C) LEGO System A/S - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using System.Linq;
using System.Text.RegularExpressions;
using LEGOMaterials;


namespace LEGOModelImporter
{

    public class ModelImporter
    {
        //private static readonly string decorationMaterialsPath = "Assets/LEGOIntegrationHub/Internal/LXFML/Resources/Materials";
        //public static Material decoCutoutMaterial = Resources.Load<Material>("LXFMLMaterials/transpcutoutMinifigure");

        /// <summary>
        /// Translate the bricks in a given LXFML-group to interactable objects
        /// </summary>
        /// <param name="group">The LXFML-group</param>
        /// <param name="index">Index of the group</param>
        /// <param name="parent">The imported asset</param>
        /// <param name="absoluteFilePath">Path of the imported asset</param>
        /// <param name="relativeFilePath">Path of the imported asset</param>
        /// <param name="resultBricks">Dictionary containing the simple bricks</param>
        /// <param name="isSubGroup">Whether it is a subgroup or not</param>
        /// <param name="lightmapped">Whether it is lightmapped or not</param>
        /// <param name="missingGroups">List of groups containing missing elements</param>
        public static GameObject InstantiateModelGroup(LXFMLDoc.BrickGroup group, int index, GameObject parent, string absoluteFilePath, string relativeFilePath, ref Dictionary<int, Brick> resultBricks, bool isSubGroup, GroupType groupType, bool randomizeRotation, bool lightmapped, bool preferLegacy, int lod)
        {
            ModelGroup groupComp;

            GameObject groupParent;

            if (isSubGroup)
            {
                groupParent = new GameObject("SubGroup " + index + " - " + group.name);
            }
            else
            {
                groupParent = new GameObject(group.name + " - " + groupType);
            }

            // FIXME Handle subgroups properly.
            //Recursively check subgroups
            if (group.children != null)
            {
                foreach (var subGroup in group.children)
                {
                    foreach (var part in group.brickRefs)
                    {
                        //Apparently supergroups contain elements from subgroups. Duplicates are removed from supergroups.
                        if (subGroup.brickRefs.Contains(part))
                        {
                            group.brickRefs[Array.IndexOf(group.brickRefs, part)] = -1;
                        }
                    }
                    InstantiateModelGroup(subGroup, Array.IndexOf(group.children, subGroup), groupParent, absoluteFilePath, relativeFilePath, ref resultBricks, true, groupType, randomizeRotation, lightmapped, preferLegacy, lod);
                }
            }

            var isStatic = (groupType == GroupType.Static || groupType == GroupType.Environment);
            lightmapped &= isStatic;

            SetStaticAndGIParams(groupParent, isStatic, lightmapped);

            groupParent.transform.parent = parent.transform;
            groupParent.transform.SetSiblingIndex(index);
            if (!isSubGroup)
            {

                groupComp = groupParent.AddComponent<ModelGroup>();
                groupComp.absoluteFilePath = absoluteFilePath;
                groupComp.relativeFilePath = relativeFilePath;

                groupComp.type = groupType;
                groupComp.reimportType = groupType;
                groupComp.randomizeRotation = randomizeRotation;
                groupComp.reimportRandomizeRotation = randomizeRotation;
                groupComp.lightmapped = lightmapped;
                groupComp.reimportLightmapped = lightmapped;
                groupComp.preferLegacy = preferLegacy;
                groupComp.reimportPreferLegacy = preferLegacy;
                groupComp.lod = lod;
                groupComp.reimportLod = lod;

                groupComp.groupName = group.name;
                groupComp.number = index;
                groupComp.parentName = parent.name;

                // Set default optimizations based on group type.
                if (groupType == GroupType.Environment)
                {
                    groupComp.optimizations = ModelGroup.DefaultEnvironmentOptimizations;
                }
                else if (groupType == GroupType.Static)
                {
                    groupComp.optimizations = ModelGroup.DefaultStaticOptimizations;
                }

            }

            if (groupType == GroupType.Ignore)
            {
                return groupParent;
            }

            var groupBricks = new List<Brick>();
            foreach (int id in group.brickRefs)
            {
                if (id == -1)
                {
                    continue;
                }
                if (resultBricks.ContainsKey(id))
                {
                    groupBricks.Add(resultBricks[id]);
                    resultBricks[id].transform.SetParent(groupParent.transform);
                }
            }

            if (groupType == GroupType.Static || groupType == GroupType.Dynamic)
            {
                DetectConnectivity(groupType, groupBricks);
            }

            return groupParent;
        }


        private static void DetectConnectivity(GroupType groupType, ICollection<Brick> bricks)
        {
            Physics.SyncTransforms();

            HashSet<Connection> connectionsToRemove = new HashSet<Connection>();
            HashSet<CommonPart> commonPartsToDestroy = new HashSet<CommonPart>();

            foreach (var brick in bricks)
            {
                var modelGroup = brick.GetComponentInParent<ModelGroup>();
                var fields = brick.GetComponentsInChildren<ConnectionField>();

                foreach(var field in fields)
                {
                    var query = field.QueryConnections();
                    foreach(var (connection, otherConnection) in query)
                    {
                        if(connection.HasConnection() || otherConnection.HasConnection())
                        {
                            continue;
                        }

                        if (modelGroup.type == GroupType.Static && modelGroup != otherConnection.GetComponentInParent<ModelGroup>())
                        {
                            continue;
                        }
                        
                        if (Connection.ConnectionValid(connection, otherConnection))
                        {
                            connection.Connect(otherConnection);

                            // FIXME Eventually, do not perform any removal on import. Instead we can do it when processing OR when building/pressing play.
                            if (groupType == GroupType.Static)
                            {
                                connectionsToRemove.Add(connection);
                                connectionsToRemove.Add(otherConnection);

                                if (connection.knob && !connection.knob.IsVisible())
                                {
                                    commonPartsToDestroy.Add(connection.knob);
                                }

                                if (otherConnection.knob && !otherConnection.knob.IsVisible())
                                {
                                    commonPartsToDestroy.Add(otherConnection.knob);
                                }


                                foreach(var tube in connection.tubes)
                                {
                                    if (!tube.IsVisible())
                                    {
                                        commonPartsToDestroy.Add(tube);
                                    }
                                }

                                foreach (var tube in otherConnection.tubes)
                                {
                                    if (!tube.IsVisible())
                                    {
                                        commonPartsToDestroy.Add(tube);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Remove common parts.
            foreach (var commonPart in commonPartsToDestroy)
            {
                Object.DestroyImmediate(commonPart.gameObject);
            }

            // Clean up connections.
            foreach (var connection in connectionsToRemove)
            {
                connection.field.connections.Remove(connection);

                // If no connections are left, remove the field.
                if (connection.field.connections.Count == 0)
                {
                    connection.field.connectivity.connectionFields.Remove(connection.field);

                    // If no fields are left, remove the Connectivity parent.
                    if (connection.field.connectivity.connectionFields.Count == 0)
                    {
                        Object.DestroyImmediate(connection.field.transform.parent.gameObject);
                    } else
                    {
                        Object.DestroyImmediate(connection.field.gameObject);
                    }
                } else
                {
                    Object.DestroyImmediate(connection.gameObject);
                }
            }
        }

        private static LXFMLDoc.BrickGroup FindGroup(LXFMLDoc lxfml, LXFMLDoc.Brick brick)
        {
            foreach (var group in lxfml.groups)
            {
                if (group.brickRefs.Contains(brick.refId))
                {
                    return group;
                }
            }

            return null;
        }

        /// <summary>
        /// Instantiate game objects for each brick in an LXFML-file
        /// </summary>
        /// <param name="lxfml">The LXFML-file</param>
        /// <param name="groupType">The type of the group</param>
        /// <param name="randomizeRotation">Slightly rotate rotation of part</param>
        /// <param name="lightmapped">Instantiate meshes with or without lightmap UVs</param>
        /// <param name="preferLegacy">Choose legacy meshes if available</param>
        /// <param name="lod">Instantiate meshes of a certain LOD</param>
        /// <param name="resultBricks">Dictionary that contains brick component, using refID as key</param>
        /// <param name="groupNumber">If non-negative, only instantiate bricks from the specified group number</param>
        public static void InstantiateModelBricks(LXFMLDoc lxfml, Dictionary<int, GroupType> groupType, Dictionary<int, bool> randomizeRotation, Dictionary<int, bool> lightmapped, Dictionary<int, bool> preferLegacy, Dictionary<int, int> lod, ref Dictionary<int, Brick> resultBricks, int groupNumber = -1)
        {
            for (var i = 0; i < lxfml.bricks.Count; ++i)
            {
                if (i % 200 == 0)
                {
                    EditorUtility.DisplayProgressBar("Importing", "Creating bricks.", ((float)i / lxfml.bricks.Count) * 0.7f);
                }

                var brick = lxfml.bricks[i];

                var group = FindGroup(lxfml, brick);

                // Discard bricks from other groups if group number is specified.
                if (groupNumber >= 0 && group != null && group.number != groupNumber)
                {
                    continue;
                }

                if (group != null && groupType[group.number] == GroupType.Ignore)
                {
                    continue;
                }

                // Determine whether or not to be static and to generate light map UVs.
                var isStatic = (group != null ? groupType[group.number] == GroupType.Static || groupType[group.number] == GroupType.Environment : true);
                var brickLightmapped = (group != null ? lightmapped[group.number] && isStatic : false);
                var brickLod =(group != null ? lod[group.number] : 0);

                var brickGO = new GameObject(brick.designId, typeof(Brick));
                var brickComp = brickGO.GetComponent<Brick>();
                Undo.RegisterCreatedObjectUndo(brickGO, "Brick");

                foreach (var part in brick.parts)
                {
                    GameObject partToInstantiate = null;

                    var partExistenceResult = PartUtility.UnpackPart(part.partDesignId, brickLightmapped, group != null ? preferLegacy[group.number] : false, brickLod);

                    if (partExistenceResult.existence != PartUtility.PartExistence.None)
                    {
                        // FIXME Make a note of changed design ids.
                        partToInstantiate = PartUtility.LoadPart(partExistenceResult.designID, brickLightmapped, partExistenceResult.existence == PartUtility.PartExistence.Legacy, brickLod);
                    }

                    if (partToInstantiate == null)
                    {
                        Debug.LogError("Missing part FBX -> " + partExistenceResult.designID);
                        continue;
                    }
                    var partGO = Object.Instantiate(partToInstantiate);
                    partGO.name = partToInstantiate.name;

                    // Assign legacy, material IDs and set up references.
                    var partComp = partGO.AddComponent<Part>();
                    partComp.designID = Convert.ToInt32(part.partDesignId);
                    partComp.legacy = partExistenceResult.existence == PartUtility.PartExistence.Legacy;
                    foreach(var material in part.materials)
                    {
                        partComp.materialIDs.Add(material.colorId);
                    }
                    partComp.brick = brickComp;
                    brickComp.parts.Add(partComp);


                    if (partExistenceResult.existence == PartUtility.PartExistence.New)
                    {
                        // FIXME Handle normal mapped model.
                        InstantiateKnobsAndTubes(partComp, brickLightmapped, brickLod);
                    }

                    // Create collider and connectivity information.
                    if (group != null ? groupType[group.number] == GroupType.Static || groupType[group.number] == GroupType.Dynamic : false)
                    {
                        GameObject collidersToInstantiate = null;

                        var collidersAvailable = PartUtility.UnpackCollidersForPart(partExistenceResult.designID);
                        if (collidersAvailable)
                        {
                            collidersToInstantiate = PartUtility.LoadCollidersPrefab(partExistenceResult.designID);
                        }

                        if (collidersToInstantiate == null && partExistenceResult.existence != PartUtility.PartExistence.Legacy)
                        {
                            Debug.LogError("Missing part collider information -> " + partExistenceResult.designID);
                        }

                        if (collidersToInstantiate)
                        {
                            var collidersGO = Object.Instantiate(collidersToInstantiate);
                            collidersGO.name = "Colliders";
                            collidersGO.transform.SetParent(partGO.transform, false);
                            var colliders = collidersGO.GetComponentsInChildren<Collider>();
                            partComp.colliders.AddRange(colliders);
                        }

                        GameObject connectivityToInstantiate = null;

                        var connectivityAvailable = PartUtility.UnpackConnectivityForPart(partExistenceResult.designID);
                        if (connectivityAvailable)
                        {
                            connectivityToInstantiate = PartUtility.LoadConnectivityPrefab(partExistenceResult.designID);
                        }

                        if (connectivityToInstantiate == null && partExistenceResult.existence != PartUtility.PartExistence.Legacy)
                        {
                            Debug.LogError("Missing part connectivity information -> " + partExistenceResult.designID);
                        }

                        if (connectivityToInstantiate)
                        {
                            var connectivityGO = Object.Instantiate(connectivityToInstantiate);
                            connectivityGO.name = "Connectivity";
                            connectivityGO.transform.SetParent(partGO.transform, false);
                            var connectivity = connectivityGO.GetComponent<Connectivity>();
                            partComp.connectivity = connectivity;
                            brickComp.totalBounds.Encapsulate(connectivity.extents);
                            connectivity.part = partComp;

                            foreach (var field in connectivity.connectionFields)
                            {
                                foreach (var connection in field.connections)
                                {
                                    MatchConnectionWithKnob(connection, partComp.knobs);
                                    MatchConnectionWithTubes(connection, partComp.tubes);
                                }
                            }
                        }
                    }

                    SetMaterials(partComp, part.materials, partExistenceResult.existence == PartUtility.PartExistence.Legacy);
                    SetDecorations(partComp, part.decorations, partExistenceResult.existence == PartUtility.PartExistence.Legacy);

                    SetStaticAndGIParams(partGO, isStatic, brickLightmapped, true);

                    // Set Position & Rotation
                    SetPositionRotation(partGO, part);

                    if (group != null ? randomizeRotation[group.number] : false)
                    {
                        // FIXME Ruins connectivity detection for dynamic groups.
                        RandomizeRotation(partComp, group != null ? groupType[group.number] == GroupType.Dynamic : false);
                    }

                    // If first part, place brick at same position.
                    if (brickGO.transform.childCount == 0)
                    {
                        brickGO.transform.position = partGO.transform.position;
                        brickGO.transform.rotation = partGO.transform.rotation;
                        brickGO.transform.localScale = Vector3.one;

                    }
                    partGO.transform.SetParent(brickGO.transform, true);
                }

                // If all parts were missing, discard brick.
                if (brickGO.transform.childCount == 0)
                {
                    Undo.DestroyObjectImmediate(brickGO);
                    continue;
                }

                SetStaticAndGIParams(brickGO, isStatic, brickLightmapped);

                // Assign uuid
                brickComp.designID = Convert.ToInt32(brick.designId);
                brickComp.uuid = brick.uuid;

                // Add LEGOAsset component.
                brickGO.AddComponent<LEGOAsset>();

                resultBricks[brick.refId] = brickComp;
            }
        }

        private static void SetPositionRotation(GameObject partGO, LXFMLDoc.Brick.Part part)
        {
            foreach (var bone in part.bones)
            {

                partGO.transform.localPosition = bone.position;
                partGO.transform.localRotation = bone.rotation;
                break; // no support for flex
            }
        }

        private static void RandomizeRotation(Part part, bool moveCollidersAndConnectivity)
        {
            var partRenderers = part.GetComponentsInChildren<MeshRenderer>(true);
            if (partRenderers.Length == 0)
            {
                return;
            }

            // Get the part bounds.
            var partBounds = partRenderers[0].bounds;
            foreach (var partRenderer in partRenderers)
            {
                partBounds.Encapsulate(partRenderer.bounds);
            }

            // Randomly rotate part. Scale the rotation down by the square of the part's size.
            Vector3 size = partBounds.size;
            Vector3 noise = (UnityEngine.Random.insideUnitSphere * 1.5f) / Mathf.Max(1.0f, size.sqrMagnitude);
            var partRotation = Quaternion.Euler(noise);

            part.transform.localRotation *= partRotation;

            if (!moveCollidersAndConnectivity)
            {
                // Rotate colliders and connectivity by inverse rotation to make them stay in place.
                var colliders = part.transform.Find("Colliders");
                if (colliders)
                {
                    colliders.localRotation *= Quaternion.Inverse(partRotation);
                }
                var connectivity = part.transform.Find("Connectivity");
                if (connectivity)
                {
                    connectivity.localRotation *= Quaternion.Inverse(partRotation);
                }
            }
        }

        private static void MatchConnectionWithKnob(Connection connection, List<Knob> knobs)
        {
            var POS_EPSILON = 0.01f;
            var ROT_EPSILON = 0.01f;
            foreach (var knob in knobs)
            {
                if (Vector3.Distance(connection.transform.position, knob.transform.position) < POS_EPSILON && 1.0f - Vector3.Dot(connection.transform.up, knob.transform.up) < ROT_EPSILON)
                {
                    connection.knob = knob;
                    knob.connection = connection;
                    return;
                }
            }
        }

        private static void MatchConnectionWithTubes(Connection connection, List<Tube> tubes)
        {
            // FIXME Temporary fix to tube removal while we work on connections that are related/non-rejecting but not connected.
            if (connection.IsRelevantForTube())
            {
                var DIST_EPSILON = 0.01f * 0.01f;
                var ROT_EPSILON = 0.01f;
                foreach (var tube in tubes)
                {
                    var bounds = tube.GetComponent<MeshFilter>().sharedMesh.bounds;
                    var extents = bounds.extents;
                    extents.x += 0.4f;
                    extents.z += 0.4f;
                    bounds.extents = extents;
                    var localConnectionPosition = tube.transform.InverseTransformPoint(connection.transform.position);

                    if (bounds.SqrDistance(localConnectionPosition) < DIST_EPSILON && 1.0f - Vector3.Dot(connection.transform.up, tube.transform.up) < ROT_EPSILON)
                    {
                        connection.tubes.Add(tube);
                        tube.connections.Add(connection);
                    }
                    if (connection.tubes.Count == 4)
                    {
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Instantiate all bricks and groups in an LXFML document
        /// </summary>
        /// <param name="lxfml">The LXFML document</param>
        /// <param name="nameOfObject">Path of the LXFML document</param>
        public static GameObject InstantiateModel(LXFMLDoc lxfml, string filePath, Model.Pivot pivot, Dictionary<int, GroupType> groupType, Dictionary<int, bool> randomizeRotation, Dictionary<int, bool> lightmapped, Dictionary<int, bool> preferLegacy, Dictionary<int, int> lod)
        {
            //Create "root" LXFML gameobject
            GameObject parent = new GameObject(Path.GetFileNameWithoutExtension(filePath));
            Undo.RegisterCreatedObjectUndo(parent, "Model");
            parent.transform.position = Vector3.zero;

            var model = parent.AddComponent<Model>();
            model.absoluteFilePath = filePath;
            model.relativeFilePath = PathUtils.GetRelativePath(Directory.GetCurrentDirectory(), filePath);
            model.pivot = pivot;

            EditorUtility.DisplayProgressBar("Importing", "Creating bricks.", 0.0f);

            var resultBricks = new Dictionary<int, Brick>(lxfml.bricks.Count);
            InstantiateModelBricks(lxfml, groupType, randomizeRotation, lightmapped, preferLegacy, lod, ref resultBricks);

            EditorUtility.DisplayProgressBar("Importing", "Creating groups.", 0.8f);

            if (resultBricks.Count > 0)
            {
                var groups = lxfml.groups;

                for (int i = 0; i < groups.Length; i++)
                {
                    var number = groups[i].number;
                    GameObject groupParent = InstantiateModelGroup(groups[i], i, parent, filePath, model.relativeFilePath, ref resultBricks, false, groupType[number], randomizeRotation[number], lightmapped[number], preferLegacy[number], lod[number]);
                    model.groups.Add(groupParent.GetComponent<ModelGroup>());
                }
            }

            // Change the pivot.
            if (pivot != Model.Pivot.Original)
            {
                EditorUtility.DisplayProgressBar("Importing", "Computing bounds.", 0.9f);

                var bounds = ComputeBounds(parent.transform);
                var newPivot = bounds.center;
                switch (pivot)
                {
                    case Model.Pivot.BottomCenter:
                        {
                            newPivot += Vector3.down * bounds.extents.y;
                            break;
                        }
                }
                foreach (Transform child in parent.transform)
                {
                    child.position -= newPivot;
                }
                parent.transform.position += newPivot;
            }

            EditorUtility.ClearProgressBar();

            return parent;
        }

        public static void ReimportModel(LXFMLDoc lxfml, Model model)
        {
            // FIXME Next version will include option to match groups up manually.

            // Check if groups have been deleted.
            for(var i = model.groups.Count - 1; i >= 0; i--)
            {
                if (!model.groups[i])
                {
                    model.groups.RemoveAt(i);
                }
            }

            var groupsMatch = true;
            for (var i = model.groups.Count - 1; i >= 0; i--)
            {
                var group = model.groups[i];
                if (group.number >= lxfml.groups.Length || lxfml.groups[group.number].name != group.groupName)
                {
                    Debug.LogWarning("Group " + group.number + " " + group.groupName + " does not match up with file.");
                    groupsMatch = false;
//                    Debug.LogWarning("Group " + group.number + " " + group.groupName + " does not match up with file. Deleting!");
//                    Undo.DestroyObjectImmediate(group.gameObject);
//                    model.groups.RemoveAt(i);
                }
            }

            if (!groupsMatch)
            {
                EditorUtility.DisplayDialog("Reimport failed", "Model groups do not match up with groups in file. Check log for details", "Ok");
                return;
            }

            // We know the groups match, so update and reimport each group.
            foreach (var group in model.groups)
            {
                group.absoluteFilePath = model.absoluteFilePath;
                group.relativeFilePath = model.relativeFilePath;

                ReimportModelGroup(lxfml, group, true);
            }
        }

        public static void ReimportModelGroup(LXFMLDoc lxfml, ModelGroup group, bool partOfEntireModelReimport = false)
        {
            // FIXME Next version will include option to match groups up manually.

            if (!partOfEntireModelReimport)
            {
                if (group.number >= lxfml.groups.Length || lxfml.groups[group.number].name != group.groupName)
                {
                    EditorUtility.DisplayDialog("Reimport failed", $"Model group {group.number} {group.groupName }does not match up with group in file", "Ok");
                    return;
                }
            }

            // We know that the group can be found, so reimport it.

            if (group.processed)
            {
                // Remove all processed meshes.
                var renderers = group.GetComponentsInChildren<MeshRenderer>();
                foreach(var renderer in renderers)
                {
                    // FIXME Destroy the mesh? Prevents undo..
                    var filter = renderer.GetComponent<MeshFilter>();
                    //Undo.DestroyObjectImmediate(filter.sharedMesh);

                    if (renderer.GetComponent<ModelGroup>() == null)
                    {
                        // Destroy submesh game objects entirely.
                        Undo.DestroyObjectImmediate(renderer.gameObject);
                    } else
                    {
                        // Destroy mesh related components on group game object.
                        Object.DestroyImmediate(filter);
                        Object.DestroyImmediate(renderer);
                    }
                }
            }

            // FIXME Check if bricks are referenced.
            // FIXME Check if bricks have custom components attached.

            // Remove group bricks.
            var existingBricks = group.GetComponentsInChildren<Brick>();
            foreach (var brick in existingBricks)
            {
                Undo.DestroyObjectImmediate(brick.gameObject);
            }

            // Update group component.
            group.processed = false;
            group.type = group.reimportType;
            group.randomizeRotation = group.reimportRandomizeRotation;
            group.lightmapped = group.reimportLightmapped;
            group.preferLegacy = group.reimportPreferLegacy;
            group.lod = group.reimportLod;
            group.name = group.groupName + " - " + group.type;
 
            var isStatic = (group.type == GroupType.Static || group.type == GroupType.Environment);
            var groupLightMapped = group.lightmapped && isStatic;

            SetStaticAndGIParams(group.gameObject, isStatic, groupLightMapped);

            // Move group to origo to ensure that bricks are instantiated in the correct positions.
            var originalGroupLocalPosition = group.transform.localPosition;
            var originalGroupLocalRotation = group.transform.localRotation;
            var originalGroupLocalScale = group.transform.localScale;
            var originalGroupParent = group.transform.parent;
            var originalGroupSiblingIndex = group.transform.GetSiblingIndex();
            group.transform.parent = null;
            group.transform.localPosition = Vector3.zero;
            group.transform.localRotation = Quaternion.identity;
            group.transform.localScale = Vector3.one;

            // Create dictionaries with just this group.
            var groupType = new Dictionary<int, GroupType> { { group.number, group.type } };
            var randomizeRotation = new Dictionary<int, bool> { { group.number, group.randomizeRotation } };
            var lightmapped = new Dictionary<int, bool> { { group.number, group.lightmapped } };
            var preferLegacy = new Dictionary<int, bool> { { group.number, group.preferLegacy } };
            var lod = new Dictionary<int, int> { { group.number, group.lod } };

            // Instantiate group bricks.
            var resultBricks = new Dictionary<int, Brick>(lxfml.bricks.Count);
            InstantiateModelBricks(lxfml, groupType, randomizeRotation, lightmapped, preferLegacy, lod, ref resultBricks, group.number);;

            // Assign bricks to group.
            if (group.type != GroupType.Ignore)
            {
                foreach (var brick in resultBricks.Values)
                {
                    brick.transform.parent = group.transform;
                }
            }

            // Move group back to original location.
            group.transform.parent = originalGroupParent;
            group.transform.SetSiblingIndex(originalGroupSiblingIndex);
            group.transform.localPosition = originalGroupLocalPosition;
            group.transform.localRotation = originalGroupLocalRotation;
            group.transform.localScale = originalGroupLocalScale;

            /*if (group.processed)
            {
                // Process the group again.
                // FIXME Is this even a good idea?
                if (group.type == GroupType.Environment || group.type == GroupType.Static)
                {
                    Vector2Int vertCount = Vector2Int.zero;
                    Vector2Int triCount = Vector2Int.zero;
                    Vector2Int meshCount = Vector2Int.zero;
                    Vector2Int colliderCount = Vector2Int.zero;
                    ModelProcessor.ProcessModelGroup(group, ref vertCount, ref triCount, ref meshCount, ref colliderCount);

                    Debug.Log($"Process result (before/after):\nVerts {vertCount.x}/{vertCount.y}, tris {triCount.x}/{triCount.y}, meshes {meshCount.x}/{meshCount.y}, colliders {colliderCount.x}/{colliderCount.y}");
                }
            }*/

            if (group.type == GroupType.Static || group.type == GroupType.Dynamic)
            {
                DetectConnectivity(group.type, resultBricks.Values);
            }

            EditorUtility.ClearProgressBar();
        }

        private static Bounds ComputeBounds(Transform root)
        {
            var meshRenderers = root.GetComponentsInChildren<MeshRenderer>();
            if (meshRenderers.Length > 0)
            {
                var bounds = meshRenderers[0].bounds;
                foreach (var renderer in meshRenderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                return bounds;
            }
            return new Bounds(root.position, Vector3.zero);
        }

        /// <summary>
        /// Applying materials to imported objects.
        /// Ignores shader id of material.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="materials"></param>
        /// <param name="isLegacy"></param>
        public static void SetMaterials(Part part, LXFMLDoc.Brick.Part.Material[] materials, bool isLegacy)
        {
            if (materials.Length > 0)
            {
                if (isLegacy)
                {
                    var mr = part.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = GetMaterial(materials[0].colorId);
                }
                else
                {
                    if (part.transform.childCount > 0)
                    {
                        var colourChangeSurfaces = part.transform.Find("ColourChangeSurfaces");

                        // Assign materials to shell, knobs, tubes and colour change surfaces
                        for (var i = 0; i < materials.Length; ++i)
                        {
                            if (i == 0)
                            {
                                // Shell.
                                var shell = part.transform.Find("Shell");
                                if (shell)
                                {
                                    var mr = shell.GetComponent<MeshRenderer>();
                                    mr.sharedMaterial = GetMaterial(materials[i].colorId);
                                }
                                else
                                {
                                    Debug.LogError("Missing shell submesh on item " + part.name);
                                }

                                // Knobs.
                                foreach (var knob in part.knobs)
                                {
                                    var mr = knob.GetComponent<MeshRenderer>();
                                    mr.sharedMaterial = GetMaterial(materials[i].colorId);
                                }

                                // Tubes.
                                foreach (var tube in part.tubes)
                                {
                                    var mr = tube.GetComponent<MeshRenderer>();
                                    mr.sharedMaterial = GetMaterial(materials[i].colorId);
                                }
                            }
                            else
                            {
                                // Colour change surfaces.
                                if (colourChangeSurfaces)
                                {
                                    var surface = colourChangeSurfaces.GetChild(i - 1);
                                    if (surface)
                                    {
                                        var mr = surface.GetComponent<MeshRenderer>();
                                        mr.sharedMaterial = GetMaterial(materials[i].colorId);
                                    }
                                    else
                                    {
                                        Debug.LogError("Missing colour change surface " + (i - 1) + " on item " + part.name);
                                    }
                                }
                                else
                                {
                                    Debug.LogError("Missing colour change surface group on multi material item " + part.name);
                                }
                            }
                        }

                        // Check if all colour change surfaces have been assigned a material.
                        if (colourChangeSurfaces)
                        {
                            if (materials.Length - 1 < colourChangeSurfaces.childCount)
                            {
                                Debug.LogError("Missing material for colour change surface(s) on item " + part.name);

                                for (var i = materials.Length - 1; i < colourChangeSurfaces.childCount; ++i)
                                {
                                    var surface = colourChangeSurfaces.GetChild(i);
                                    if (surface)
                                    {
                                        var mr = surface.GetComponent<MeshRenderer>();
                                        mr.sharedMaterial = GetMaterial(materials[materials.Length - 1].colorId);
                                    }
                                    else
                                    {
                                        Debug.LogError("Missing colour change surface " + i + " on item " + part.name);
                                    }
                                }
                            }
                        }
                    }
                }

            }
        }

        private static Material GetMaterial(int colourId)
        {
            var materialExistence = MaterialUtility.CheckIfMaterialExists(colourId);

            if (materialExistence == MaterialUtility.MaterialExistence.Legacy)
            {
                Debug.LogWarning("Legacy material " + colourId);
            } else if(materialExistence == MaterialUtility.MaterialExistence.None)
            {
                Debug.LogError("Missing material " + colourId);
            }

            if (materialExistence != MaterialUtility.MaterialExistence.None)
            {
                return MaterialUtility.LoadMaterial(colourId, materialExistence == MaterialUtility.MaterialExistence.Legacy);
            }

            return null;
        }

        private static void SetStaticAndGIParams(GameObject go, bool isStatic, bool lightmapped, bool recursive = false)
        {
            if (isStatic)
            {
                go.isStatic = true;

                var mr = go.GetComponent<MeshRenderer>();
                if (mr)
                {
                    if (lightmapped)
                    {
                        mr.receiveGI = ReceiveGI.Lightmaps;
                    }
                    else
                    {
                        mr.receiveGI = ReceiveGI.LightProbes;
                    }
                }

                if (recursive)
                {
                    foreach (Transform child in go.transform)
                    {
                        SetStaticAndGIParams(child.gameObject, isStatic, lightmapped, recursive);
                    }
                }
            }
        }

        private static void InstantiateKnobsAndTubes(Part part, bool lightmapped, int lod)
        {
            var knobs = part.transform.Find("Knobs_loc");
            if (knobs)
            {
                InstantiateCommonParts<Knob>(part, part.knobs, knobs, lightmapped, lod);
                knobs.name = "Knobs";
            }

            var tubes = part.transform.Find("Tubes_loc");
            if (tubes)
            {
                InstantiateCommonParts<Tube>(part, part.tubes, tubes, lightmapped, lod);
                tubes.name = "Tubes";
            }
        }

        private static void InstantiateCommonParts<T>(Part part, List<T> partsList, Transform parent, bool lightmapped, int lod) where T : CommonPart
        {
            int count = parent.childCount;
            // Instantiate common parts using locators.
            for (int i = 0; i < count; i++)
            {
                var commonPartLocation = parent.GetChild(i);
                var name = Regex.Split(commonPartLocation.name, "(_[0-9]+ 1)");

                GameObject commonPartToInstantiate = null;

                var commonPartAvailable = PartUtility.UnpackCommonPart(name[0], lightmapped);
                if (commonPartAvailable)
                {
                    commonPartToInstantiate = PartUtility.LoadCommonPart(name[0], lightmapped, lod);
                }

                if (commonPartToInstantiate == null)
                {
                    Debug.LogError("Missing Common Part -> " + name[0]);
                    continue;
                }

                var commonPartGO = Object.Instantiate(commonPartToInstantiate);
                commonPartGO.name = commonPartToInstantiate.name;

                var commonPartComponent = commonPartGO.AddComponent<T>();
                commonPartComponent.part = part;

                // Set position and rotation.
                commonPartGO.transform.position = commonPartLocation.position;
                commonPartGO.transform.rotation = commonPartLocation.rotation;

                commonPartGO.transform.SetParent(parent, true);

                partsList.Add(commonPartComponent);
            }
            // Remove locators.
            for (int i = 0; i < count; i++)
            {
                Object.DestroyImmediate(parent.GetChild(0).gameObject);
            }
        }

        /// <summary>
        /// For setting decorations on imported objects. Not modified.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="decorations"></param>
        /// <param name="isLegacy"></param>
        public static void SetDecorations(Part part, LXFMLDoc.Brick.Part.Decoration[] decorations, bool isLegacy)
        {
            if (isLegacy)
            {
            }
            else
            {
                // Disable decoration surfaces.
                var decorationSurfaces = part.transform.Find("DecorationSurfaces");
                if (decorationSurfaces)
                {
                    decorationSurfaces.gameObject.SetActive(false);
                }
            }
            /*
            for (var i = 0; i < obj.transform.childCount; ++i)
            {
                var t = obj.transform.GetChild(i);

                if (t.gameObject.name.StartsWith("Decoration_"))
                {
                    if (decorations != null && i < decorations.Length && decorations[i] != 0)
                    {
                        if (!mats.ContainsKey(decorations[i]))
                        {
                            var t2d = Util.LoadObjectFromResources<Texture2D>("Decorations/" + decorations[i]);
                            if (t2d != null)
                            {
                                // Generate new material for our prefabs
                                t2d.wrapMode = TextureWrapMode.Clamp;
                                t2d.anisoLevel = 4;
                                var newDecoMat = new Material(decoCutoutMaterial);
                                newDecoMat.SetTexture("_MainTex", t2d);
                                AssetDatabase.CreateAsset(newDecoMat,
                                    decorationMaterialsPath + "/" + decorations[i] + ".mat");
                                mats.Add(decorations[i], newDecoMat);
                                t.gameObject.GetComponent<Renderer>().sharedMaterial = mats[decorations[i]];
                            }
                            else
                            {
                                Debug.Log("Missing decoration -> " + decorations[i]);
                            }
                        }
                        else
                        {
                            t.gameObject.GetComponent<Renderer>().sharedMaterial = mats[decorations[i]];
                        }
                    }
                    else
                    {
                        Object.DestroyImmediate(t.gameObject);
                    }
                }
            }
            */
        }
    }
}