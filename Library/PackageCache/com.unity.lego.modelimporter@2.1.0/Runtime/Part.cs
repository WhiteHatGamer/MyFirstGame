// Copyright (C) LEGO System A/S - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited

using System.Collections.Generic;
using UnityEngine;

namespace LEGOModelImporter
{

    public class Part : MonoBehaviour
    {
        public int designID;
        public bool legacy;
        public Connectivity connectivity;
        public List<int> materialIDs = new List<int>(); 
        public List<Collider> colliders = new List<Collider>();
        public Brick brick;
        public List<Knob> knobs = new List<Knob>();
        public List<Tube> tubes = new List<Tube>();

        static readonly float collisionEpsilon = 0.02f;

        /// <summary>
        /// Check if the part collides with any other part in the scene
        /// </summary>
        /// <param name="part">The part that we want to check collision for</param>
        /// <returns></returns>
        public static bool IsColliding(Part part, Collider[] colliders, HashSet<Brick> ignoredBricks = null)
        {
            foreach (var collider in part.colliders)
            {
                // FIXME Is there a more elegant way to handle this?
                var colliderType = collider.GetType();
                var physicsScene = collider.gameObject.scene.GetPhysicsScene();
                var hitColliders = 0;
                if (colliderType == typeof(BoxCollider))
                {
                    var boxCollider = (BoxCollider)collider;
                    hitColliders = physicsScene.OverlapBox(collider.transform.TransformPoint(boxCollider.center), (boxCollider.size / 2.0f) - Vector3.one * collisionEpsilon, colliders, collider.transform.rotation, ~LayerMask.GetMask(Connection.connectivityFeatureLayerName), QueryTriggerInteraction.Ignore);
                }
                else if (colliderType == typeof(SphereCollider))
                {
                    var sphereCollider = (SphereCollider)collider;
                    hitColliders = physicsScene.OverlapSphere(collider.transform.TransformPoint(sphereCollider.center), (sphereCollider.radius) - collisionEpsilon, colliders, ~LayerMask.GetMask(Connection.connectivityFeatureLayerName), QueryTriggerInteraction.Ignore);
                }

                if (hitColliders > 0)
                {
                    for(var i = 0; i < hitColliders; i++)
                    {
                        var overlap = colliders[i];
                        // FIXME Possibly need to make this more efficient. Perhaps each collider has a PartCollider component, which can be used to reference the part.
                        var overlapPart = overlap.GetComponentInParent<Part>();
                        if (overlapPart != null)
                        {                            
                            if(part == overlapPart)
                            {
                                continue;
                            }                            

                            if(ignoredBricks != null)
                            {                                
                                if(ignoredBricks.Contains(overlapPart.brick))
                                {
                                    continue;
                                }
                            }
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }

}