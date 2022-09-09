// Copyright (C) LEGO System A/S - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited

using System.Collections.Generic;
using UnityEngine;
using LEGOMaterials;
#if UNITY_EDITOR
using UnityEditor;

#endif
namespace LEGOModelImporter
{
#if UNITY_EDITOR
    [ExecuteAlways]
#endif
    public class Connection : MonoBehaviour
    {
        public static readonly string connectivityFeatureLayerName = "ConnectivityFeature";

#if UNITY_EDITOR
        public static event System.Action<Connection> dirtied;
#endif

        public enum ConnectionType
        {
            knob = 0,
            hollowKnob,
            knobFitInPegHole,           // Function similar to knob, plus fits in pegHole
            hollowKnobFitInPegHole,     // Function similar to hollowKnob, plus fits in pegHole
            squareKnob,                 // Fit only in square anti knob

            tube,                       // Connector on bottom of a round 1x1
            tubeWithRib,                // Function similar to tube, but has ribs sticking out to the sides rejecting tubeGrabber
            bottomTube,                 // Function similar to tube, but cannot connect to tubeGap or be occupied
            bottomTubeWithRib,          // Function similar to tubeWithRib, but cannot connect to tubeGap or be occupied

            secondaryPin,
            secondaryPinWithRib,
            secondaryPinWithTinyPinReceptor,
            secondaryPinWithRibAndTinyPinReceptor,

            fixedTube,                  // Tube that can not rotate, can only connect in the 4 90 degree orientations
            fixedTubeWithAntiKnob,      // Function similar to fixedTube, but knobs can also connect

            antiKnob,                   //
                                        //                       antiKnobWithSecondaryPin,   // Has a pin inside that reject knobs but not hollow knobs

            pegHole,                    // anti knob at end of peg holes, only connects to special knobs

            squareAntiKnob,             // Function similar to anti knob, plus squareKnob fit in it

            tubeGap,                    // Receptor located diagonally between 4 knobs
            tubeGrabber,                // Wine glass that connect to tube, rejects tubes "WithRib"

            tinyPin,
            tinyPinReceptor,

            edge,                       // edge of any kind and rib
            edgeGap,

            knobReject,                 // Reject knobs, but not hollow knobs, does not connect to either

            powerFuncLeftTop,
            powerFuncRightTop,
            powerFuncLeftBottom,
            powerFuncRightBottom,

            voidFeature,

            duploKnob,

            duploHollowKnob,
            duploAntiKnob,
            duploTube,
            duploFixedTube,
            duploTubeGap,
            duploAnimalKnob,
            duploAnimalTube,

            secondaryPinReceptor,       // Function similar to hollowKnob, but cannot connect on the outside

            duploFixedAnimalTube,
            duploSecondaryPinWithRib,   // Connect to duploHollowKnob but reject duploAnimalKnob
            duploSecondaryPin,          // Connect to both duploHollowKnob and duploAnimalKnob
            duploKnobReject,            // Reject duploKnob, but not duploHollowKnob nor duploAnimalKnob, does not connect to either of the latter
        };

        /// <summary>
        /// Every ConnectionPoint has some flags.
        /// </summary>
        public enum Flags
        {
            squareFeature = 1 << 0,                        // This feature is square (used for geometry optimization)
            roundFeature = 1 << 1,                         // This feature is round (used for geometry optimization)
            knobWithHole = 1 << 2,                         // Use hollow knob collision volumes ##################################### TODO: remove, using IsTechnicKnob() instead
            knobWithMiniFigHandHole = 1 << 3,              // Use mini-fig hand knob collision volumes
            knobWithSingleCollision = 1 << 4,              // Use single "hole" collision volume off to one side
            singleFeature = 1 << 5,                        // Feature is from a "single" sized field
            receptorDontRemoveKnobCollision = 1 << 6,      // When connecting don't remove the knob collision volume
            knobWithoutCollision = 1 << 7,                 // Knob should never have active collision volumes
        };

        public static readonly Flags flagsCoveringKnob = Flags.squareFeature | Flags.roundFeature;
        public static readonly Flags flagsCoveringTube = Flags.squareFeature;

        public ConnectionType connectionType;
        public int quadrants;
        public Flags flags;
        public ConnectionField field;
        public Knob knob;
        public List<Tube> tubes;

        public Connection connectedTo;

        public bool HasConnection()
        {
            return connectedTo != null;
        }

        public Connection GetConnection()
        {
            return connectedTo;
        }

        public static void RegisterPrefabChanges(Object changedObject)
        {
#if UNITY_EDITOR
            if (PrefabUtility.IsPartOfAnyPrefab(changedObject))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(changedObject);
            }
#endif
        }

        /// <summary>
        /// Connect two features.
        /// Note: Does not move the related parts.
        /// </summary>
        /// <param name="toConnect">The feature to connect to</param>
        /// <param name="updateKnobAndTubes">If true, update visibility of knobs and tubes.</param>
        public void Connect(Connection toConnect, bool updateKnobAndTubes = true)
        {
            if (toConnect == this)
            {
                return;
            }

            if (connectedTo == toConnect)
            {
                return;
            }

            // Disconnect old connection.
            if (connectedTo)
            {
                var previousConnection = connectedTo;
                connectedTo = null;
                previousConnection.Connect(null, updateKnobAndTubes);
                RegisterPrefabChanges(previousConnection);
            }

            connectedTo = toConnect;

            // Connect new connection.
            if (connectedTo)
            {
                connectedTo.Connect(this, updateKnobAndTubes);
                RegisterPrefabChanges(connectedTo);
            }

            // Enable/disable knob.
            if (updateKnobAndTubes)
            {
                UpdateKnobsAndTubes();
            }

            RegisterPrefabChanges(this);
        }

        public void UpdateKnobsAndTubes()
        {
            if (knob)
            {
                knob.UpdateVisibility();

                RegisterPrefabChanges(knob.gameObject);
            }

            foreach (var tube in tubes)
            {
                if (tube)
                {
                    tube.UpdateVisibility();

                    RegisterPrefabChanges(tube.gameObject);
                }
            }
        }

        public bool IsVisible()
        {
            var visible = false;
            if(knob)
            {
                visible = visible || knob.IsVisible();
            }

            foreach(var tube in tubes)
            {
                if(tube)
                {
                    visible = visible || tube.IsVisible();
                }
            }
            return visible;
        }

        public bool IsRelevantForTube()
        {
            // FIXME Temporary fix to tube removal while we work on connections that are related/non-rejecting but not connected.
            return connectionType == ConnectionType.antiKnob || connectionType == ConnectionType.squareAntiKnob;
        }

        /// <summary>
        /// Check whether a connection has been broken
        /// </summary>
        /// <returns>Whether or not a connection is still valid</returns>
        public static bool ConnectionValid(Connection lhs, Connection rhs)
        {
            if (lhs == null || rhs == null)
            {
                return false;
            }

            if (!MatchTypes(lhs.connectionType, rhs.connectionType))
            {
                return false;
            }

            var POS_EPSILON = 0.1f;
            var ROT_EPSILON = 3.0f;
            return Vector3.Distance(lhs.transform.position, rhs.transform.position) < POS_EPSILON && Vector3.Angle(lhs.transform.up, rhs.transform.up) < ROT_EPSILON;
        }

        /// <summary>
        /// Checks whether two connection types are connectable
        /// </summary>
        public static bool MatchTypes(ConnectionType lhs, ConnectionType rhs)
        {
            switch (lhs)
            {
                case ConnectionType.knob:
                    return rhs == ConnectionType.antiKnob || rhs == ConnectionType.tube || rhs == ConnectionType.bottomTube || rhs == ConnectionType.squareAntiKnob;
                case ConnectionType.knobFitInPegHole:
                    return rhs == ConnectionType.antiKnob || rhs == ConnectionType.tube || rhs == ConnectionType.bottomTube || rhs == ConnectionType.squareAntiKnob;
                case ConnectionType.hollowKnob:
                    return rhs == ConnectionType.antiKnob || rhs == ConnectionType.tube || rhs == ConnectionType.bottomTube || rhs == ConnectionType.squareAntiKnob || rhs == ConnectionType.secondaryPin;
                case ConnectionType.hollowKnobFitInPegHole:
                    return rhs == ConnectionType.antiKnob || rhs == ConnectionType.tube || rhs == ConnectionType.bottomTube || rhs == ConnectionType.squareAntiKnob || rhs == ConnectionType.secondaryPin;
                case ConnectionType.antiKnob:
                    return rhs == ConnectionType.knob || rhs == ConnectionType.hollowKnob || rhs == ConnectionType.knobFitInPegHole || rhs == ConnectionType.hollowKnobFitInPegHole;
                case ConnectionType.tube:
                    return rhs == ConnectionType.knob || rhs == ConnectionType.hollowKnob || rhs == ConnectionType.knobFitInPegHole || rhs == ConnectionType.hollowKnobFitInPegHole;
                case ConnectionType.bottomTube:
                    return rhs == ConnectionType.tubeGap || rhs == ConnectionType.knob || rhs == ConnectionType.hollowKnob || rhs == ConnectionType.knobFitInPegHole || rhs == ConnectionType.hollowKnobFitInPegHole;
                case ConnectionType.secondaryPin:
                    return rhs == ConnectionType.hollowKnob || rhs == ConnectionType.hollowKnobFitInPegHole;
                case ConnectionType.squareAntiKnob:
                    return rhs == ConnectionType.knob || rhs == ConnectionType.hollowKnob || rhs == ConnectionType.knobFitInPegHole || rhs == ConnectionType.hollowKnobFitInPegHole;
                default:
                    return false;
            }
        }

        public static Vector3 GetPreconnectOffset(Connection dst)
        {
            switch (dst.connectionType)
            {
                case Connection.ConnectionType.knob:
                case Connection.ConnectionType.hollowKnob:
                case Connection.ConnectionType.knobFitInPegHole:
                case Connection.ConnectionType.hollowKnobFitInPegHole:
                    {
                        return dst.transform.TransformDirection(Vector3.up * 0.1f);
                    }
                case Connection.ConnectionType.tube:
                case Connection.ConnectionType.antiKnob:
                case Connection.ConnectionType.squareAntiKnob:
                    {
                        return dst.transform.TransformDirection(Vector3.down * 0.1f);
                    }
            }

            return Vector3.zero;
        }

        private void OnDestroy()
        {
            if(HasConnection())
            {
#if UNITY_EDITOR
                // FIXME isPlayingOrWillChangePlaymode is false when exiting play mode even though isPlaying is true. This seems like a bug.
                // FIXME If this bug is resolved, replace the test with just !EditorApplication.isPlayingOrWillChangePlaymode.
                if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Undo.RegisterCompleteObjectUndo(connectedTo, "Destroying connection");
                    Undo.RegisterCompleteObjectUndo(this, "Destroying connection");

                    dirtied?.Invoke(connectedTo);
                }
#endif
                Connect(null, false);
            }
        }
    }
}