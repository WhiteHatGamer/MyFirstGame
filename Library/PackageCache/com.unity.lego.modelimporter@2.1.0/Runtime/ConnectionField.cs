// Copyright (C) LEGO System A/S - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited

using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;

namespace LEGOModelImporter
{
    public class ConnectionField : MonoBehaviour
    {
        public enum FieldType
        {
            custom2DField,
            axel
        }

        public Vector2Int gridSize;
        public FieldType fieldType;
        public List<Connection> connections = new List<Connection>();

        /// <summary>
        /// Grid structure for quick access of connections during queries.
        /// Keys are Vector2Int in local space of connection field.
        /// Once we load all connectivity features in, we should be
        /// able to simply use the connections list directly.
        /// This grid is necessary because we currently only have
        /// every second feature loaded.
        /// </summary>
        [SerializeField]
        public Connection[] connectionGrid;

        public Connectivity connectivity;

        /// <summary>
        /// Check if any connections on a field match with any on another field
        /// </summary>
        /// <param name="f1">The first field</param>
        /// <param name="f2">The field to check against</param>
        /// <returns></returns>
        public static bool MatchTypes(ConnectionField f1, ConnectionField f2)
        {
            if(f1 == null || f2 == null)
            {
                return false;
            }

            foreach(var c1 in f1.connections)
            {
                foreach(var c2 in f2.connections)
                {
                    if(Connection.MatchTypes(c1.connectionType, c2.connectionType))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Convert a local 3D position to a 2D/XZ grid position
        /// </summary>
        /// <param name="localPos">The local position to convert</param>
        /// <returns></returns>
        public static Vector2Int ToGridPos(Vector3 localPos)
        {
            return new Vector2Int(Mathf.RoundToInt(localPos.x / BrickBuildingUtility.LU_5) * -1, Mathf.RoundToInt(localPos.z / BrickBuildingUtility.LU_5));
        }

        /// <summary>
        /// Given a world position, check if this field has any connections
        /// </summary>
        /// <param name="worldPos">The world position to check</param>
        /// <returns></returns>
        public Connection GetConnectionAt(Vector3 worldPos)
        {            
            var localPos = transform.InverseTransformPoint(worldPos);            
            return GetConnectionAt(ToGridPos(localPos));
        }

        /// <summary>
        /// Get a connection at the given coordinates, null if there are none
        /// </summary>
        /// <param name="localCoordinates">Local grid coordinates</param>
        /// <returns></returns>
        public Connection GetConnectionAt(Vector2Int localCoordinates)
        {            
            if(localCoordinates.x > gridSize.x + 1 || localCoordinates.y > gridSize.y + 1 ||
                localCoordinates.x < 0 || localCoordinates.y < 0)
            {
                return null;
            }
                        
            var index = localCoordinates.x + (gridSize.x + 1) * localCoordinates.y;
            if(index >= connectionGrid.Length || index < 0)
            {            
                return null;
            }
            return connectionGrid[index];
        }

        private HashSet<Connection> QueryConnection(Vector3 position, Connection.ConnectionType srcType)
        {
            HashSet<Connection> result = new HashSet<Connection>();

            // PhysicsScene
            var physicsScene = gameObject.scene.GetPhysicsScene();
            var hits = physicsScene.OverlapSphere(position, .05f, BrickBuildingUtility.colliderBuffer, LayerMask.GetMask(Connection.connectivityFeatureLayerName), QueryTriggerInteraction.Collide);
            for(var i = 0; i < hits; i++)
            {
                var overlap = BrickBuildingUtility.colliderBuffer[i];
                var connection = overlap.GetComponent<Connection>();
                if(connection == null)
                {
                    continue;
                }

                if(Connection.MatchTypes(srcType, connection.connectionType))
                {
                    result.Add(connection);
                }
            }
            
            return result;
        }

        /// <summary>
        /// Query the possible connections for this field
        /// </summary>
        /// <param name="onlyConnectTo">An optional filter field if you only want to check connections on a specific field</param>
        /// <returns>A list of tuples for the possible connections</returns>
        public HashSet<(Connection, Connection)> QueryConnections(ConnectionField onlyConnectTo = null)
        {
            HashSet<(Connection, Connection)> validConnections = new HashSet<(Connection, Connection)>();
            foreach (var connection in connections)
            {
                var querySuccess = QueryConnection(connection.transform.position, connection.connectionType);
                foreach (var con in querySuccess)
                {
                    if(onlyConnectTo == null || onlyConnectTo == con.field)
                    {
                        validConnections.Add((connection, con));
                    }
                }
            }
            return validConnections;
        }

        /// <summary>
        /// Get a list of connected connections on this field
        /// </summary>
        /// <returns></returns>
        public List<Connection> GetConnectedConnections()
        {
            List<Connection> connected = new List<Connection>();
            foreach (var connection in connections)
            {
                if (connection.HasConnection())
                {
                    connected.Add(connection);
                }
            }
            return connected;
        }

        /// <summary>
        /// Disconnect all connections for this field.
        /// </summary>
        /// <returns>The fields that were disconnected</returns>
        public HashSet<ConnectionField> DisconnectAll()
        {
            // Return the fields that were disconnected if needed by caller.
            HashSet<ConnectionField> result = new HashSet<ConnectionField>();

            List<(Connection, Connection)> toBeDisconnected = new List<(Connection, Connection)>();

            var connected = GetConnectedConnections();
            foreach (var connection in connected)
            {
                var otherConnection = connection.GetConnection();
                toBeDisconnected.Add((connection, otherConnection));
                result.Add(otherConnection.field);
            }
            Disconnect(toBeDisconnected);

            return result;
        }

        /// <summary>
        /// Disconnect all invalid connections for this field.
        /// </summary>
        /// <returns>The fields that were disconnected</returns>
        public HashSet<ConnectionField> DisconnectAllInvalid()
        {
            // Return the fields that were disconnected if needed by caller.
            HashSet<ConnectionField> result = new HashSet<ConnectionField>();

            List<(Connection, Connection)> toBeDisconnected = new List<(Connection, Connection)>();

            var connected = GetConnectedConnections();
            foreach (var connection in connected)
            {
                var otherConnection = connection.GetConnection();
                if (!Connection.ConnectionValid(connection, otherConnection))
                {
                    toBeDisconnected.Add((connection, otherConnection));
                    result.Add(otherConnection.field);
                }
            }
            Disconnect(toBeDisconnected);

            return result;
        }

        /// <summary>
        /// Disconnect from all connections not connected to a list of bricks.
        /// Used to certain cases where you may want to keep connections with a 
        /// selection of bricks.
        /// </summary>
        /// <param name="bricksToKeep">List of bricks to keep connections to</param>
        /// <returns></returns>
        public HashSet<ConnectionField> DisconnectInverse(HashSet<Brick> bricksToKeep)
        {
            // Return the fields that were disconnected if needed by caller.
            HashSet<ConnectionField> result = new HashSet<ConnectionField>();

            List<(Connection, Connection)> toBeDisconnected = new List<(Connection, Connection)>();
            foreach(var connection in connections)
            {
                var connectedTo = connection.GetConnection();
                if(connectedTo != null)
                {
                    if(!bricksToKeep.Contains(connectedTo.field.connectivity.part.brick))
                    {
                        toBeDisconnected.Add((connection, connectedTo));
                        result.Add(connectedTo.field);
                    }
                }
            }

            Disconnect(toBeDisconnected);
            return result;
        }

        private static void Disconnect(List<(Connection, Connection)> toBeDisconnected)
        {
#if UNITY_EDITOR
            HashSet<Object> toRecord = new HashSet<Object>();
            foreach (var (c1, c2) in toBeDisconnected)
            {
                toRecord.Add(c1);
                if (c1.knob)
                {
                    toRecord.Add(c1.knob.gameObject);
                }
                foreach (var tube in c1.tubes)
                {
                    if (tube)
                    {
                        toRecord.Add(tube.gameObject);
                    }
                }
                toRecord.Add(c2);
                if (c2.knob)
                {
                    toRecord.Add(c2.knob.gameObject);
                }
                foreach (var tube in c2.tubes)
                {
                    if (tube)
                    {
                        toRecord.Add(tube.gameObject);
                    }
                }
            }
            Undo.RegisterCompleteObjectUndo(toRecord.ToArray(), "Recording all connections, knobs and tubes before disconnecting.");
#endif
            foreach(var (c1, c2) in toBeDisconnected)
            {
                c1.Connect(null);
            }
        }

        /// <summary>
        /// Create a rotation that aligns the orientation of a transform to another
        /// The rotation will not be applied in this function.
        /// </summary>
        /// <param name="source">The transform we want to align</param>
        /// <param name="destination">The transform we want to align to</param>
        /// <param name="resultRotation">Output parameter for the resulting rotation</param>
        /// <returns></returns>
        public static bool AlignRotation(Transform source, Transform destination, out Quaternion resultRotation)
        {            
            // Find rotation needed to align up vectors
            var alignedRotation = Quaternion.FromToRotation(source.up, destination.up);

            // Compute the angle between the rotations. To check if it is too large
            var angle = Quaternion.Angle(source.rotation, alignedRotation * source.rotation);

            // Ignore if we need to rotate more than 90 degrees (plus a small epsilon to account for randomized rotations) to align up vectors. 
            if (angle > 91.0f)
            {
                resultRotation = Quaternion.identity;
                return false;
            }

            // Cache the old rotation
            var oldRotation = source.rotation;

            // Set the rotation to the aligned rotation
            source.rotation = alignedRotation * source.rotation;

            // Find the rotation needed to align to the destination
            resultRotation = MathUtils.AlignRotation(new Vector3[2]{source.right, source.forward}, destination.localToWorldMatrix);

            // Combine up-alignment with forward/right alignment
            resultRotation = resultRotation * alignedRotation;
            source.rotation = oldRotation;
            return true;
        }

        /// <summary>
        /// Get the relative position and rotation of a possible connection
        /// </summary>
        /// <param name="src">The feature connecting</param>
        /// <param name="dst">The feature being connected to</param>
        /// <param name="pivot">The pivot we rotate around</param>
        /// <param name="offset">out parameter for the relative position</param>
        /// <param name="angle">out parameter for the angle needed for the rotation</param>
        /// <param name="axis">out parameter for the axis needed for the rotation</param>
        public static void GetConnectedTransformation(Connection src, Connection dst, Vector3 pivot, out Vector3 offset, out float angle, out Vector3 axis)
        {
            if(src.field == null || dst.field == null)
            {
                // Unsupported connectivity type
                offset = Vector3.zero;
                angle = 0.0f;
                axis = Vector3.zero;
                return;
            }

            var part = src.field.connectivity.part;
            var brick = part.brick;

            // Find the required rotation for the source to align with the destination
            AlignRotation(src.transform, dst.transform, out Quaternion rot);

            var oldRot = brick.transform.rotation;
            var oldPos = brick.transform.position;

            // We rotate around a pivot, so we need angle and axis
            rot.ToAngleAxis(out angle, out axis);
            brick.transform.RotateAround(pivot, axis, angle);

            // Offset of connections after pivot rotation
            offset = dst.transform.position - src.transform.position;

            brick.transform.rotation = oldRot;
            brick.transform.position = oldPos;
        }

        /// <summary>
        /// Check if any knobs and tubes are visible
        /// </summary>
        /// <returns>True or false depending on any knobs or tubes are visible on this field</returns>
        public bool IsVisible()
        {
            var visible = false;
            foreach(var connection in connections)
            {
                visible = visible || connection.IsVisible();
            }
            return visible;
        }

        /// <summary>
        /// Check whether connecting to connection features is possible.
        /// </summary>
        /// <param name="src">The feature connecting</param>
        /// <param name="dst">The feature being connected to</param>
        /// <param name="pivot">The pivot we rotate around</param>
        /// <param name="ignoredBricks">Set of bricks to ignore when checking collision</param>
        /// <returns>Whether or not the connection is valid</returns>
        public static bool IsConnectionValid(Connection src, Connection dst, Vector3 pivot, HashSet<Brick> ignoredBricks = null)
        {
            // Make sure types match
            if(!Connection.MatchTypes(src.connectionType, dst.connectionType))
            {
                return false;
            }

            // Prevent connecting to itself
            if (src == dst)
            {
                return false;
            }

            if (src.field == null || dst.field == null)
            {
                // Could be due to an unsupported connection type
                return false;
            }            

            var part = src.field.connectivity.part;
            var brick = part.brick;

            var dstField = dst.field;
            var otherPart = dstField.connectivity.part;

            //FIXME: Can parts connect to themselves?
            if (otherPart == part)
            {
                return false;
            }

            // Get the relative position and rotation for this brick of a possible connection
            GetConnectedTransformation(src, dst, pivot, out Vector3 conOffset, out float conAngle, out Vector3 conAxis);

            // Cache position before we check collision
            var oldPosition = brick.transform.position;
            var oldRotation = brick.transform.rotation;

            brick.transform.RotateAround(pivot, conAxis, conAngle);
            brick.transform.position += conOffset;

            // Check if we collide with anything
            var parts = brick.parts;
            foreach (var p in parts)
            {
                if (Part.IsColliding(p, BrickBuildingUtility.colliderBuffer, ignoredBricks))
                {
                    // We collided with something. Make sure to reset position and rotation to original.
                    brick.transform.position = oldPosition;
                    brick.transform.rotation = oldRotation;
                    return false;
                }
            }

            // Reset position and rotation to original
            brick.transform.position = oldPosition;
            brick.transform.rotation = oldRotation;
            return true;
        }

        /// <summary>
        /// Connect two fields through a src and dst connection.
        /// Connects to all fields possible through this connection.
        /// </summary>
        /// <param name="src">The feature connecting</param>
        /// <param name="dst">The feature being connected to</param>
        /// <param name="pivot">The pivot we rotate around</param>
        /// <returns>The fields that were connected to</returns>
        public static HashSet<ConnectionField> Connect(Connection src, Connection dst, Vector3 pivot)
        {
            // Return the fields that were connected if needed by caller.
            HashSet<ConnectionField> result = new HashSet<ConnectionField>();

            //FIXME: Is this even possible if we have non-null connections?
            if (src.field == null || dst.field == null)
            {
                // Unsupported field types
                return result;
            }

            if (!IsConnectionValid(src, dst, pivot))
            {
                // Connection is invalid: Mismatched connection types or collision
                return result;
            }

            var dstField = dst.field;
            var srcField = src.field;

            // We know the connection is valid, so first we remove all old connections.
            // We will detect any connections resulting from this connection later anyway

            foreach (var field in srcField.connectivity.connectionFields)
            {
                field.DisconnectAll();
            }

            List<(Connection, Connection)> toBeConnected = new List<(Connection, Connection)>();

            // The initial connection
            toBeConnected.Add((src, dst));
            result.Add(dstField);

            // Now we look in the fields of the src part for other possible connections
            foreach (var field in srcField.connectivity.connectionFields)
            {
                // Make a new ray query for all nearby connections
                var connections = field.QueryConnections();

                foreach (var connection in connections)
                {
                    if(connection.Item1 == src || connection.Item2 == dst)
                    {
                        continue;
                    }

                    // If there already is a connection, ignore.
                    if (connection.Item1.HasConnection() || connection.Item2.HasConnection())
                    {
                        continue;
                    }

                    // Check for validity of connection
                    if (Connection.ConnectionValid(connection.Item1, connection.Item2))
                    {
                        toBeConnected.Add(connection);
                        result.Add(connection.Item2.field);
                    }
                }
            }

#if UNITY_EDITOR
            HashSet<Object> toRecord = new HashSet<Object>();
            foreach (var (c1, c2) in toBeConnected)
            {
                toRecord.Add(c1);
                if (c1.knob)
                {
                    toRecord.Add(c1.knob.gameObject);
                }
                foreach (var tube in c1.tubes)
                {
                    if (tube)
                    {
                        toRecord.Add(tube.gameObject);
                    }
                }
                toRecord.Add(c2);
                if (c2.knob)
                {
                    toRecord.Add(c2.knob.gameObject);
                }
                foreach (var tube in c2.tubes)
                {
                    if (tube)
                    {
                        toRecord.Add(tube.gameObject);
                    }
                }
            }
            Undo.RegisterCompleteObjectUndo(toRecord.ToArray(), "Recording all connections, knobs and tubes before connecting.");
#endif

            foreach (var (c1, c2) in toBeConnected)
            {
                c1.Connect(c2);
            }

            return result;
        }
    }


}