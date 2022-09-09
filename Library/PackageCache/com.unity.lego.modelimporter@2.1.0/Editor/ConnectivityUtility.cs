// Copyright (C) LEGO System A/S - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited

using UnityEngine;
using System.Collections.Generic;

namespace LEGOModelImporter
{

    public class ConnectivityUtility
    {
        static HashSet<Connection.ConnectionType> currentlySupportedConnectionTypes = new HashSet<Connection.ConnectionType>() {
        Connection.ConnectionType.knob,
        Connection.ConnectionType.hollowKnob,
        Connection.ConnectionType.knobFitInPegHole,
        Connection.ConnectionType.hollowKnobFitInPegHole,
        Connection.ConnectionType.tube,
        Connection.ConnectionType.antiKnob,
        Connection.ConnectionType.squareAntiKnob,
        Connection.ConnectionType.secondaryPin,
        //Connection.ConnectionType.bottomTube
    };

        public static bool IsConnectionTypeSupported(Connection.ConnectionType connectionType)
        {
            return currentlySupportedConnectionTypes.Contains(connectionType);
        }
    }

}