// Copyright (C) LEGO System A/S - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited

using System.Collections.Generic;
using UnityEngine;

namespace LEGOModelImporter
{

    [SelectionBase]
    public class Brick : MonoBehaviour
    {
        public int designID;
        public string uuid;
        public List<Part> parts = new List<Part>();
        public Bounds totalBounds = new Bounds();

        public HashSet<Brick> GetConnectedBricks(bool recursive = true)
        {
            var connectedBricks = new HashSet<Brick>();
            GetConnectedBricks(this, connectedBricks, recursive);
            return connectedBricks;
        }

        public bool IsLegacy()
        {
            foreach(var part in parts)
            {
                if(part.legacy)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Disconnect all fields and their connections on this brick
        /// </summary>
        public void DisconnectAll()
        {
            foreach (var part in parts)
            {
                foreach (var connectionField in part.connectivity.connectionFields)
                {
                    connectionField.DisconnectAll();
                }
            }
        }

        private void GetConnectedBricks(Brick brick, HashSet<Brick> result, bool recursive)
        {
            foreach (var part in brick.parts)
            {
                if (!part.legacy && part.connectivity)
                {
                    foreach (var connectionField in part.connectivity.connectionFields)
                    {
                        var connected = connectionField.GetConnectedConnections();
                        foreach (var connection in connected)
                        {
                            var connectedBrick = connection.GetConnection().field.connectivity.part.brick;
                            if (!result.Contains(connectedBrick))
                            {
                                result.Add(connectedBrick);
                                if (recursive)
                                {
                                    GetConnectedBricks(connectedBrick, result, recursive);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}