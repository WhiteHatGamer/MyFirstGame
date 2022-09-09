// Copyright (C) LEGO System A/S - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited

using LEGOMaterials;

namespace LEGOModelImporter
{

    public class Knob : CommonPart
    {
        public Connection connection;

        public override bool IsVisible()
        {
            var connectedTo = connection.GetConnection();
            if (!connectedTo)
            {
                return true;
            }
            else
            {
                var notCovering = (connection.flags & Connection.flagsCoveringKnob) == 0 || (connectedTo.flags & Connection.flagsCoveringKnob) == 0;
                notCovering |= MouldingColour.IsAnyTransparent(part.materialIDs) || MouldingColour.IsAnyTransparent(connectedTo.field.connectivity.part.materialIDs);

                return notCovering;
            }
        }
    }
}

