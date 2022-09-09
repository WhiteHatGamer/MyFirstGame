// Copyright (C) LEGO System A/S - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace LEGOModelImporter
{
    [InitializeOnLoad]
    public class SceneBrickBuilder
    {
        #region Editor Prefs
        private static bool buildWithBricks = false;
        private static readonly float stickySnapDistanceDefault = 20.0f;
        private static float stickySnapDistance = stickySnapDistanceDefault;
        private static int maxTriesPerBrick = BrickBuildingUtility.defaultMaxBricksToConsiderWhenFindingConnections;
        static readonly string brickBuildingPrefsKey = "com.unity.lego.modelimporter.brickBuilding";
        static readonly string stickySnapDistancePrefsKey = "com.unity.lego.modelimporter.stickySnapDistance";
        static readonly string maxTriesPerBrickPrefsKey = "com.unity.lego.modelimporter.maxTriesPerBrick";
        static readonly string selectConnectedPrefsKey = "com.unity.lego.modelimporter.selectConnected";

        private const string brickBuildingShortcut = " &%n"; // Space required to properly add to menu path
        public const string brickBuildingMenuPath = "LEGO Tools/Brick Building";

        private const string expandSelectionShortcut = " &%e"; // Space required to properly add to menu path
        public const string expandSelectionMenuPath = "LEGO Tools/Selection/Expand To Connected Bricks";

        private static bool selectConnectedDefault = true;
        private static bool selectConnected = selectConnectedDefault;
        
        public const string editorSelectConnectedMenuPath = "LEGO Tools/Select Connected Bricks";
        private const string editorSelectConnectedShortcut = " &%k"; // Space required to properly add to menu path

        #endregion
        
        // Used to keep track of which brick is under the mouse between frames
        // Is only reset if the current event is a valid picking event
        private static Brick hitBrick = null;

        private static List<GameObject> queuedSelection = null;
        private static UnityEngine.Object[] lastSelection = new UnityEngine.Object[] { };
        private static GameObject lastActiveObject = null;
        private static Event currentEvent = null;

        private static HashSet<Brick> selectedBricks = new HashSet<Brick>();
        private static Bounds selectionBounds = new Bounds();

        // Focus brick always represents the most recently clicked or dragged brick
        private static Brick focusBrick = null;
        private static List<(Quaternion, Vector3)> rotationOffsets = new List<(Quaternion, Vector3)>();
        private static (Connection, Connection) currentConnection = (null, null);
        private static Vector3 mousePosition = Vector3.zero;
        private static Vector3 pickupOffset = Vector3.zero;
        private static Brick[] bricks = null;
        private static Brick draggedBrick = null;
        private static Vector3 placeOffset = Vector3.zero;
                

        enum SelectionState
        {
            noSelection,
            dragging,
            selected,
            moving
        }

        enum InteractionKey
        {
            none,
            left,
            right,
            up,
            down
        }

        private static InteractionKey rotateOrNudgeDirection = InteractionKey.none;
        private static SelectionState currentSelectionState = SelectionState.noSelection;
        private static RaycastHit currentHitPoint = new RaycastHit();
        private static bool aboutToPlace = false;
        private static float currentMouseDelta = 0.0f;
        private static Ray currentRay = new Ray();

        private static Plane worldPlane = new Plane(Vector3.up, Vector3.zero);

        private static bool undoQueued = false;

        private static bool duplicateQueued = false;
        private static bool dragAndDropQueued = false;
        
        private static bool activeChanged = false;

        private static bool sceneViewCurrentlyInFocus = false;

        private static HashSet<Connection> dirtyConnections = new HashSet<Connection>();

        private enum BrickBuildingState
        {
            off,
            on,
            playMode
        }

        static SceneBrickBuilder()
        {
            buildWithBricks = EditorPrefs.GetBool(brickBuildingPrefsKey, false);
            stickySnapDistance = EditorPrefs.GetFloat(stickySnapDistancePrefsKey, stickySnapDistanceDefault);
            selectConnected = EditorPrefs.GetBool(selectConnectedPrefsKey, selectConnectedDefault);
            EditorApplication.playModeStateChanged -= PlayModeStateChanged;
            EditorApplication.playModeStateChanged += PlayModeStateChanged;

            var state = EditorApplication.isPlayingOrWillChangePlaymode ? BrickBuildingState.playMode : (buildWithBricks ? BrickBuildingState.on : BrickBuildingState.off);
            SetupBrickBuilding(state);
        }

        private static void PlayModeStateChanged(PlayModeStateChange state)
        {
            if(state == PlayModeStateChange.ExitingEditMode)
            {
                SetupBrickBuilding(BrickBuildingState.playMode);
            }
            else if(state == PlayModeStateChange.EnteredEditMode)
            {
                SetupBrickBuilding(buildWithBricks ? BrickBuildingState.on : BrickBuildingState.off);
            }
        }

        public static void SetStickySnapDistance(float value)
        {
            EditorPrefs.SetFloat(stickySnapDistancePrefsKey, value);
            stickySnapDistance = value;
        }

        public static float GetStickySnapDistance()
        {
            return EditorPrefs.GetFloat(stickySnapDistancePrefsKey, stickySnapDistanceDefault);
        }

        public static void SetMaxTriesPerBrick(int value)
        {
            EditorPrefs.SetInt(maxTriesPerBrickPrefsKey, value);
            maxTriesPerBrick = value;
        }

        public static int GetMaxTriesPerBrick()
        {
            return EditorPrefs.GetInt(maxTriesPerBrickPrefsKey, BrickBuildingUtility.defaultMaxBricksToConsiderWhenFindingConnections);
        }

        private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            if(IsDuplicateEvent(Event.current))
            {
                PlaceBrick();
                duplicateQueued = true;
            }
        }

        private static void SetupBrickBuilding(BrickBuildingState newState)
        {
            ApplyConnectivityLayer();

            // Good form to remove all delegates before we add them again
            // to prevent from adding them multiple times by accident.
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
            SceneView.duringSceneGui -= OnSceneGUIDefault;
            SceneView.duringSceneGui -= OnSceneGUIBuilding;
            EditorApplication.update -= EditorUpdate;
            Selection.selectionChanged -= OnSelectionChangedBuilding;
            Selection.selectionChanged -= OnSelectionChangedDefault;
            Undo.undoRedoPerformed -= OnUndo;
            Connection.dirtied -= OnConnectionDirtied;
            rotateOrNudgeDirection = InteractionKey.none;

            switch(newState)
            {
                case BrickBuildingState.on:
                {
                    ConnectivityUIManager.ShowTools(true);
                    EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
                    SceneView.duringSceneGui += OnSceneGUIBuilding;
                    EditorApplication.update += EditorUpdate;
                    Selection.selectionChanged += OnSelectionChangedBuilding;
                    Undo.undoRedoPerformed += OnUndo;
                    Connection.dirtied += OnConnectionDirtied;
                    if (Selection.transforms.Length > 0)
                    {
                        SetFromSelection();
                    }
                }
                break;
                case BrickBuildingState.off:
                {
                    ConnectivityUIManager.ShowTools(true);
                    PlaceBrick();
                    SetFocusBrick(null);
                    EditorApplication.update += EditorUpdate;
                    SceneView.duringSceneGui += OnSceneGUIDefault;
                    Selection.selectionChanged += OnSelectionChangedDefault;
                    Connection.dirtied += OnConnectionDirtied;
                    HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Keyboard));
                }
                break;
                case BrickBuildingState.playMode:
                {
                    ConnectivityUIManager.ShowTools(false);
                    PlaceBrick();
                    SetFocusBrick(null);
                    HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Keyboard));
                }
                break;
            }
        }

        private static void OnConnectionDirtied(Connection dirtyConnection)
        {
            dirtyConnections.Add(dirtyConnection);
        }

        private static void SetFromSelection(bool updateSelection = true)
        {
            selectedBricks.Clear();
            Brick focus = null;
            foreach(var selection in Selection.transforms)
            {
                var brick = selection.GetComponentInParent<Brick>();
                if(brick != null)
                {
                    selectedBricks.Add(brick);
                    if(focus == null)
                    {
                        focus = brick;
                    }
                }
            }

            SetFocusBrick(focus);

            if(updateSelection && selectedBricks.Count == Selection.transforms.Length)
            {
                QueueSelection(selectedBricks);
            }
        } 

        public static bool GetToggleBrickBuildingStatus()
        {
            return EditorPrefs.GetBool(brickBuildingPrefsKey, false);
        }

        [MenuItem(editorSelectConnectedMenuPath + editorSelectConnectedShortcut, priority = 35)]
        public static void ToggleSelectConnectedBricks()
        {
            selectConnected = !selectConnected;
            EditorPrefs.SetBool(selectConnectedPrefsKey, selectConnected);
            SceneView.RepaintAll();
        }

        [MenuItem(editorSelectConnectedMenuPath + editorSelectConnectedShortcut, validate = true)]
        public static bool ValidateSelectConnectedBricks()
        {
            Menu.SetChecked(editorSelectConnectedMenuPath, selectConnected);
            return true;
        }

        public static bool GetSelectConnectedBricks()
        {
            return EditorPrefs.GetBool(selectConnectedPrefsKey, selectConnectedDefault);
        }

        [MenuItem(brickBuildingMenuPath + brickBuildingShortcut, priority = 30)]
        public static void ToggleBrickBuilding()
        {
            buildWithBricks = !buildWithBricks;
            EditorPrefs.SetBool(brickBuildingPrefsKey, buildWithBricks);
            if(!Application.isPlaying)
            {
                SetupBrickBuilding(buildWithBricks ? BrickBuildingState.on : BrickBuildingState.off);
            }
        }

        [MenuItem(brickBuildingMenuPath + brickBuildingShortcut, true)]
        private static bool ValidateBrickBuilding()
        {
            Menu.SetChecked(brickBuildingMenuPath, buildWithBricks);
            return !EditorApplication.isPlaying;
        }

        private static void RemoveInvalidConnections(Brick brick)
        {
            // Remove connections and make sure it is undoable
            foreach (var part in brick.parts)
            {
                if (part.connectivity == null)
                {
                    continue;
                }

                foreach (var field in part.connectivity.connectionFields)
                {
                    field.DisconnectAllInvalid();
                }
            }
        }

        private static void CheckChangedTransforms()
        {
            var selection = Selection.activeTransform;
            if (selection != null)
            {
                // Remove any connections on this transform if it has changed
                if (selection.hasChanged)
                {
                    foreach(var obj in Selection.objects)
                    {
                        var go = obj as GameObject;
                        if(go != null)
                        {
                            var brick = go.GetComponentInParent<Brick>();
                            if (brick != null)
                            {
                                selection.hasChanged = false;
                                RemoveInvalidConnections(brick);
                            }
                        }
                    }
                }
            }
        }

        private static void OnSceneGUIDefault(SceneView sceneView)
        {
            currentEvent = Event.current;
            CheckChangedTransforms();

            if(lastActiveObject != Selection.activeGameObject)
            {
                activeChanged = true;
                OnSelectionChangedDefault();
                activeChanged = false;
            }
            lastActiveObject = Selection.activeGameObject;
        }

        private static List<Transform> PrepareForMove(HashSet<Brick> bricks, bool disconnect = true)
        {
            var transforms = new List<Transform>();

            // Remove all connections when we start to move the brick. Undo is handled within the ConnectionFields
            // Don't remove connections that are connected to bricks in the selection
            foreach (var brick in bricks)
            {
                transforms.Add(brick.transform);

                if(disconnect)
                {
                    foreach(var part in brick.parts)
                    {
                        foreach(var field in part.connectivity.connectionFields)
                        {
                            field.DisconnectInverse(bricks);
                        }
                    }
                }
            }

            Undo.RegisterCompleteObjectUndo(transforms.ToArray(), "Register brick state before selection");
            return transforms;
        }

        private static HashSet<Brick> SelectConnected(Brick[] bricks)
        {
            var connectedBricks = new HashSet<Brick>();
            foreach(var brick in bricks)
            {
                var connected = brick.GetConnectedBricks();   
                connectedBricks.Add(brick);
                connectedBricks.UnionWith(connected);
            }
            return connectedBricks;
        }

        private static void StartMovingBrick(Brick brick)
        {
            currentMouseDelta = 0.0f;
            draggedBrick = null;            

            bricks = StageUtility.GetCurrentStageHandle().FindComponentsOfType<Brick>();

            PrepareForMove(selectedBricks);
            SyncBounds();

            if (currentSelectionState == SelectionState.dragging)
            {
                pickupOffset = currentHitPoint.point - focusBrick.transform.position;
            }
            else if(currentSelectionState == SelectionState.selected)
            {
                pickupOffset = selectionBounds.center - focusBrick.transform.position;
            }
            SetSelectionState(SelectionState.moving);
        }

        private static void EvaluateSelectionState(Event current, Camera camera, SceneView sceneView)
        {
            switch(currentSelectionState)
            {
                case SelectionState.noSelection:
                    {
                        if(current.type == EventType.MouseDown && current.button == 0 && !current.alt)
                        {
                            var brick = BrickUnderRay(current, out currentHitPoint);
                            if(brick != null)
                            {                                
                                draggedBrick = brick;
                                SetSelectionState(SelectionState.dragging);
                            }
                        }
                        currentMouseDelta = 0.0f;
                    }
                    break;
                case SelectionState.dragging:
                    {
                        if(current.type == EventType.MouseUp)
                        {
                            // We select on mouse up and in case we haven't yet started a drag
                            currentMouseDelta = 0.0f;
                            SetSelectionState(SelectionState.selected);

                            if (current.control || current.command || current.shift)
                            {
                                if(selectedBricks.Contains(draggedBrick))
                                {
                                    if(selectConnected)
                                    {
                                        var connected = SelectConnected(new Brick[]{draggedBrick});
                                        foreach(var brick in connected)
                                        {
                                            selectedBricks.Remove(brick);
                                        }
                                    }
                                    else
                                    {
                                        selectedBricks.Remove(draggedBrick);
                                    }

                                    if(draggedBrick == focusBrick)
                                    {
                                        foreach(var brick in selectedBricks)
                                        {
                                            SetFocusBrick(brick);
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    SetFocusBrick(draggedBrick);
                                    selectedBricks.Add(draggedBrick);
                                }
                            }
                            else
                            {
                                selectedBricks = new HashSet<Brick> { draggedBrick };
                                SetFocusBrick(draggedBrick);
                            }

                            if(selectConnected)
                            {
                                selectedBricks.UnionWith(SelectConnected(selectedBricks.ToArray()));
                            }

                            QueueSelection(selectedBricks);
                        }
                        else if (currentMouseDelta > stickySnapDistance && current.type == EventType.MouseDrag)
                        {
                            if (Selection.transforms.Length == 0 || !selectedBricks.Contains(draggedBrick))
                            {
                                if(selectConnected)
                                {
                                    selectedBricks.Clear();
                                    selectedBricks.UnionWith(SelectConnected(new Brick[1]{draggedBrick}));
                                    QueueSelection(selectedBricks);
                                }
                                else
                                {
                                    queuedSelection = new List<GameObject> { draggedBrick.gameObject };
                                    selectedBricks = new HashSet<Brick> { draggedBrick };
                                }
                            }
                            else if(selectConnected)
                            {
                                selectedBricks.UnionWith(SelectConnected(selectedBricks.ToArray()));
                                QueueSelection(selectedBricks);
                            }

                            SetFocusBrick(draggedBrick);
                            StartMovingBrick(draggedBrick);
                        }
                    }
                    break;
                case SelectionState.selected:
                    {
                        currentMouseDelta = 0.0f;
                        if(current.type == EventType.MouseDown && current.button == 0 && !current.alt)
                        {
                            // Cache the hit point here for use in the dragging state
                            var brick = BrickUnderRay(current, out currentHitPoint);
                            if(brick != null)
                            {
                                draggedBrick = brick;
                                SetSelectionState(SelectionState.dragging);
                                currentMouseDelta = 0.0f;
                            }
                        }

                        if(rotateOrNudgeDirection != InteractionKey.none)
                        {
                            NudgeBrick(camera, rotateOrNudgeDirection);
                            rotateOrNudgeDirection = InteractionKey.none;
                        }
                    }
                    break;
                case SelectionState.moving:
                    {                      
                        if(!IsOverSceneView())
                        {
                            return;
                        }                        

                        if (aboutToPlace && current.type == EventType.MouseUp)
                        {
                            PlaceBrick();
                            aboutToPlace = false;

                            // Force a repaint to reflect connection has been made.
                            sceneView.Repaint();
                            return;
                        }

                        if (current.type == EventType.MouseDown)
                        {
                            aboutToPlace = true;
                        }

                        // Check for a small delta to make sure we place even though the user moves the mouse ever so slightly.
                        // In that case, the event will actually be a drag, but it will seem weird to the user since their intention
                        // was to do a place.
                        if (currentMouseDelta > 20.0f && current.type == EventType.MouseDrag)
                        {
                            aboutToPlace = false;
                        }

                        // Cancel a selection
                        if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
                        {
                            // Cannot do Undo.PerformUndo in OnSceneGUI as it causes null ref inside Unity GUI code.
                            undoQueued = true;
                        }

                        if(rotateOrNudgeDirection != InteractionKey.none)
                        {
                            RotateBrick(camera, rotateOrNudgeDirection);
                            rotateOrNudgeDirection = InteractionKey.none;
                        }

                        if (currentMouseDelta > 15.0f)
                        {
                            currentMouseDelta = 0.0f;
                            ComputeNewConnection(camera, currentRay);
                        }
                    }
                    break;
            }
        }

        [Shortcut("LEGO/Rotate/Nudge Brick Left", KeyCode.A, ShortcutModifiers.Alt)]
        private static void RotateOrNudgeLeft()
        {
            rotateOrNudgeDirection = InteractionKey.left;
        }

        [Shortcut("LEGO/Rotate/Nudge Brick Right", KeyCode.D, ShortcutModifiers.Alt)]
        public static void RotateOrNudgeRight()
        {
            rotateOrNudgeDirection = InteractionKey.right;
        }

        [Shortcut("LEGO/Rotate/Nudge Brick Up", KeyCode.W, ShortcutModifiers.Alt)]
        public static void RotateOrNudgeUp()
        {                        
            rotateOrNudgeDirection = InteractionKey.up;
        }

        [Shortcut("LEGO/Rotate/Nudge Brick Down", KeyCode.S, ShortcutModifiers.Alt)]
        public static void RotateOrNudgeDown()
        {
            rotateOrNudgeDirection = InteractionKey.down;
        }

        private static void NudgeBrick(Camera camera, InteractionKey key)
        {
            if(currentSelectionState != SelectionState.selected)
            {
                return;
            }

            if(focusBrick == null)
            {
                return;
            }

            var oldPositions = new List<Vector3>();

            foreach(var brick in selectedBricks)
            {
                oldPositions.Add(brick.transform.position);
            }

            PrepareForMove(selectedBricks, false);

            switch(key)
            {
                case InteractionKey.left:
                {
                    var right = MathUtils.SnapMajorAxis(camera.transform.right, true).normalized;
                    foreach(var brick in selectedBricks)
                    {
                        brick.transform.position -= right * BrickBuildingUtility.LU_10;
                    }
                }
                break;
                case InteractionKey.right:
                {
                    var right = MathUtils.SnapMajorAxis(camera.transform.right, true).normalized;
                    foreach(var brick in selectedBricks)
                    {
                        brick.transform.position += right * BrickBuildingUtility.LU_10;
                    }
                }
                break;
                case InteractionKey.up:
                {
                    var up = MathUtils.SnapMajorAxis(camera.transform.up, true).normalized;
                    var upUnsigned = MathUtils.SnapMajorAxis(camera.transform.up, false).normalized;
                    var increment = BrickBuildingUtility.LU_1 * 4;
                    var angleUpUnsigned = Vector3.Angle(Vector3.up, upUnsigned);
                    if(angleUpUnsigned > Vector3.Angle(Vector3.forward, upUnsigned) ||
                      angleUpUnsigned > Vector3.Angle(Vector3.right, upUnsigned))
                    {
                        increment = BrickBuildingUtility.LU_10;
                    }

                    foreach(var brick in selectedBricks)
                    {
                        brick.transform.position += up * increment;
                    }
                }
                break;
                case InteractionKey.down:
                {
                    var up = MathUtils.SnapMajorAxis(camera.transform.up, true).normalized;
                    var upUnsigned = MathUtils.SnapMajorAxis(camera.transform.up, false).normalized;
                    var increment = BrickBuildingUtility.LU_1 * 4;
                    var angleUpUnsigned = Vector3.Angle(Vector3.up, upUnsigned);
                    if(angleUpUnsigned > Vector3.Angle(Vector3.forward, upUnsigned) ||
                      angleUpUnsigned > Vector3.Angle(Vector3.right, upUnsigned))
                    {
                        increment = BrickBuildingUtility.LU_10;
                    }

                    foreach(var brick in selectedBricks)
                    {
                        brick.transform.position -= up * increment;
                    }
                }
                break;
            }
            
            Physics.SyncTransforms();
            if(CollideAtTransformation(selectedBricks))
            {
                var i = 0;
                foreach(var brick in selectedBricks)
                {
                    brick.transform.position = oldPositions[i++];
                }
            }
            else
            {
                Vector3 axis = Vector3.zero;
                float angle = 0.0f;
                var pivot = focusBrick.transform.position + pickupOffset;
                (Connection, Connection) foundConnection = (null, null);
                Quaternion rot = Quaternion.identity;
                foreach(var brick in selectedBricks)
                {
                    foreach(var part in brick.parts)
                    {
                        foreach(var field in part.connectivity.connectionFields)
                        {
                            field.DisconnectInverse(selectedBricks);

                            if(foundConnection == (null, null))
                            {
                                var connections = field.QueryConnections();
                                if(connections.Count > 0)
                                {
                                    var (c1, c2) = connections.ToList()[0];
                                    ConnectionField.GetConnectedTransformation(c1, c2, pivot, out _, out angle, out axis);
                                    foundConnection = (c1, c2);
                                }
                            }
                        }
                    }
                }

                if(foundConnection != (null, null))
                {
                    foundConnection.Item1.field.connectivity.part.brick.transform.RotateAround(pivot, axis, angle);
                    ConnectWithSelection(foundConnection.Item1, foundConnection.Item2);
                }
            }
        }
        
        private static void RotateBrick(Camera camera, InteractionKey key)
        {
            if(currentSelectionState != SelectionState.moving)
            {
                return;
            }

            if(focusBrick == null)
            {
                return;
            }
            
            SyncBounds();
            var rotationPivot = selectionBounds.min + pickupOffset;
            var localPickupOffset = focusBrick.transform.InverseTransformDirection(pickupOffset);

            bool rotated = false;
            switch (key)
            {
                case InteractionKey.left:
                    {
                        foreach(var selected in selectedBricks)
                        {
                            selected.transform.RotateAround(rotationPivot, Vector3.up, -90.0f);
                        }
                        rotated = true;
                        break;
                    }
                case InteractionKey.right:
                    {
                        foreach (var selected in selectedBricks)
                        {
                            selected.transform.RotateAround(rotationPivot, Vector3.up, 90.0f);
                        }
                        rotated = true;
                        break;
                    }
                case InteractionKey.up:
                    {
                        var right = MathUtils.SnapMajorAxis(camera.transform.right, true);
                        foreach (var selected in selectedBricks)
                        {
                            selected.transform.RotateAround(rotationPivot, right, 90.0f);
                        }
                        rotated = true;
                        break;
                    }
                case InteractionKey.down:
                    {
                        var right = MathUtils.SnapMajorAxis(camera.transform.right, true);
                        foreach (var selected in selectedBricks)
                        {
                            selected.transform.RotateAround(rotationPivot, right, -90.0f);
                        }
                        rotated = true;
                        break;
                    }
            }

            if (rotated)
            {
                SyncBounds();
                pickupOffset = focusBrick.transform.TransformDirection(localPickupOffset);
                ComputeNewConnection(camera, currentRay);
            }
        }

        private static void ConnectWithSelection(Connection src, Connection dst)
        {
            var pivot = focusBrick.transform.position + pickupOffset;
            ConnectionField.Connect(src, dst, pivot);

            foreach(var brick in selectedBricks)
            {
                if(brick == src.field.connectivity.part.brick)
                {
                    continue;
                }

                foreach(var part in brick.parts)
                {
                    foreach(var field in part.connectivity.connectionFields)
                    {
                        var possibleConnections = field.QueryConnections();

                        if(possibleConnections.Count > 0)
                        {
                            var connection = possibleConnections.ToList()[0];
                            ConnectionField.Connect(connection.Item1, connection.Item2, pivot);
                        }
                    }
                }
            }
        }

        private static void PlaceBrick()
        {
            if (focusBrick == null)
            {
                return;
            }

            // If we had selected a brick and a connection should be made, do the connection here.
            if (currentConnection.Item1 != null && currentConnection.Item2 != null)
            {
                // Get rid of place offset. Need to sync transforms afterwards.
                foreach(var brick in selectedBricks)
                {
                    brick.transform.position -= placeOffset;
                }
                placeOffset = Vector3.zero;
                Physics.SyncTransforms();

                ConnectWithSelection(currentConnection.Item1, currentConnection.Item2);
            }

            SetSelectionState(SelectionState.selected);
        }

        private static bool IsPickingEvent(Event current)
        {
            return current.type != EventType.Repaint &&
                current.type != EventType.Layout &&
                current.type != EventType.ExecuteCommand &&
                current.type != EventType.ValidateCommand;
        }


        private static bool IsDuplicateEvent(Event current)
        {
            return current != null 
                    && ((current.commandName == "Duplicate" && selectedBricks.Count > 0) || current.commandName == "Paste") 
                    && current.type == EventType.ExecuteCommand;
        }

        private static bool IsOverSceneView()
        {
            if(SceneView.mouseOverWindow == null)
            {
                return false;
            }
            System.Type windowOver = SceneView.mouseOverWindow.GetType();
            System.Type sceneView = typeof(SceneView);
            return windowOver.Equals(sceneView);
        }

        private static Brick BrickUnderRay(Event current, out RaycastHit hit)
        {
            hit = new RaycastHit();
            
            PhysicsScene physicsScene;
            if(PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                physicsScene = PrefabStageUtility.GetCurrentPrefabStage().scene.GetPhysicsScene();
            }
            else
            {
                physicsScene = PhysicsSceneExtensions.GetPhysicsScene(EditorSceneManager.GetActiveScene());
            }
            
            if (physicsScene.Raycast(currentRay.origin, currentRay.direction, out hit))
            {
                var brick = hit.collider.gameObject.GetComponentInParent<Brick>();
                if (brick != null && !brick.IsLegacy())
                {
                    return brick;
                }
            }
            
            return null;
        }

        private static void UpdateMouse(Vector3 newMousePosition)
        {
            var mouseDelta = Vector3.Distance(newMousePosition, mousePosition);
            currentMouseDelta += mouseDelta;
            mousePosition = newMousePosition;
            currentRay = HandleUtility.GUIPointToWorldRay(mousePosition);
        }

        private static bool AdjustSelectionIfNeeded(GameObject go, object[] lastSelection, List<GameObject> newSelection, out bool isNotBrick)
        {
            var brick = go.GetComponentInParent<Brick>();
            if(brick != null)
            {
                selectedBricks.Add(brick);
                isNotBrick = false;

                if (Array.IndexOf(lastSelection, go) < 0)
                {
                    if (Array.IndexOf(lastSelection, brick) < 0)
                    {
                        newSelection.Add(brick.gameObject);
                        return true;
                    }
                }
            }
            else
            {
                isNotBrick = true;
            }

            newSelection.Add(go);
            return false;
        }

        private static void OnSelectionChangedDefault()
        {
            if(Selection.objects.Length == 0)
            {
                return;
            }

            if((IsOverSceneView() && !sceneViewCurrentlyInFocus) || !IsOverSceneView())
            {
                return;
            }
            
            // Check if control was down the last time we updated the scene
            var controlDown = currentEvent.control || currentEvent.command;
            
            if(selectConnected)
            {
                // Check if anything has changed in selection
                var lastSelectionCount = lastSelection != null ? lastSelection.Length : 0;
                bool changed = Selection.objects.Length != lastSelectionCount;
                var newSelection = new List<GameObject>();
                foreach(var obj in Selection.objects)
                {
                    var go = obj as GameObject;
                    if(go != null)
                    {
                        changed |= AdjustSelectionIfNeeded(go, lastSelection, newSelection, out _);
                    }
                }

                var selection = new List<GameObject>();

                if(changed)
                {
                    // Find all connected bricks and add to selection
                    var connectedBricks = new HashSet<Brick>();
                    var nonBricks = new List<GameObject>();
                    foreach(var obj in Selection.objects)
                    {
                        var go = obj as GameObject;
                        if(go != null)
                        {
                            var brick = go.GetComponentInParent<Brick>();
                            if(brick != null || (controlDown && brick != null && brick.gameObject == go))
                            {
                                var connected = brick.GetConnectedBricks();
                                connectedBricks.UnionWith(connected);
                                connectedBricks.Add(brick);
                            }
                            else
                            {
                                nonBricks.Add(go);
                            }
                        }
                    }
                    
                    // In case we control clicked, the will be an extra in the selection.
                    // This is fine if it was a new brick, but if the brick was already selected
                    // we need to remove it from the selection.
                    if(controlDown && lastSelection.Length + 1 == Selection.objects.Length)
                    {
                        foreach(var obj in Selection.objects)
                        {
                            if(Array.IndexOf(lastSelection, obj) < 0)
                            {
                                var go = obj as GameObject;
                                if(go)
                                {
                                    var brick = go.GetComponentInParent<Brick>();
                                    if(brick && Array.IndexOf(lastSelection, brick.gameObject) >= 0)
                                    {
                                        connectedBricks.Remove(brick);
                                        var connected = brick.GetConnectedBricks();
                                        foreach(var connectedBrick in connected)
                                        {
                                            connectedBricks.Remove(connectedBrick);
                                        }
                                    }
                                }
                            }
                        }
                    }                        
                    
                    foreach(var brick in connectedBricks)
                    {
                        selection.Add(brick.gameObject);
                    }

                    foreach(var go in nonBricks)
                    {
                        selection.Add(go);
                    }
                    queuedSelection = selection;
                }
                else if(activeChanged)
                {
                    if(controlDown && Selection.activeGameObject != null)
                    {
                        Brick activeBrick = Selection.activeGameObject.GetComponent<Brick>();
                        if(!activeBrick)
                        {
                            activeBrick = Selection.activeGameObject.GetComponentInParent<Brick>();
                        }

                        if(activeBrick)
                        {
                            var connected = activeBrick.GetConnectedBricks();
                            foreach(var selected in Selection.objects)
                            {
                                if(selected == activeBrick.gameObject)
                                {
                                    continue;
                                }

                                var go = selected as GameObject;
                                if(go)
                                {
                                    var brick = go.GetComponentInParent<Brick>();
                                    if(connected.Contains(brick))
                                    {
                                        continue;
                                    }
                                }
                                selection.Add(go);
                            }
                            queuedSelection = selection;
                        }
                    }
                }   

                lastActiveObject = Selection.activeGameObject;
                lastSelection = Selection.objects;
            }
        }

        private static void OnSelectionChangedBuilding()
        {
            if((IsOverSceneView() && !sceneViewCurrentlyInFocus) || !IsOverSceneView())
            {
                return;
            }

            selectedBricks.Clear();
            var lastSelectionCount = lastSelection != null ? lastSelection.Length : 0;
            var newSelection = new List<GameObject>();
            bool changed = Selection.objects.Length != lastSelectionCount;
            var containsNonBricks = false;
            foreach(var obj in Selection.objects)
            {
                var go = obj as GameObject;
                if(go != null)
                {
                    changed |= AdjustSelectionIfNeeded(go, lastSelection, newSelection, out bool isNotBrick);
                    containsNonBricks |= isNotBrick;
                }
            }

            if(!changed)
            {
                newSelection = null;
            }

            if(selectedBricks.Count == 0 || containsNonBricks)
            {
                SetFocusBrick(null);
            }

            queuedSelection = newSelection;
            lastSelection = Selection.objects;
            lastActiveObject = Selection.activeGameObject;

            if(dragAndDropQueued)
            {
                dragAndDropQueued = false;
                var toSelect = new HashSet<Brick>();
                foreach(var obj in Selection.objects)
                {
                    var go = obj as GameObject;
                    if(go != null)
                    {
                        var brick = go.GetComponent<Brick>();
                        if(brick != null)
                        {
                            selectedBricks.Add(brick);
                            toSelect.Add(brick);
                            if(focusBrick == null)
                            {
                                SetFocusBrick(brick);
                            }
                        }
                    }
                }                    

                if(focusBrick != null)
                {
                    SetSelectionState(SelectionState.selected);
                    StartMovingBrick(focusBrick);
                    currentMouseDelta = stickySnapDistance;
                }
            }

            if (duplicateQueued)
            {
                duplicateQueued = false;

                if(focusBrick != null)
                {
                    Brick newFocusBrick = null;
                    foreach(var brick in selectedBricks)
                    {
                        if(newFocusBrick == null)
                        {
                            newFocusBrick = brick;
                        }

                        if (brick.transform.position == focusBrick.transform.position)
                        {
                            newFocusBrick = brick;
                        }
                    }

                    SetFocusBrick(newFocusBrick);
                }
                else // In case there was no previous selection, there will be no focus brick to relate to
                {
                    var it = selectedBricks.GetEnumerator();
                    it.MoveNext();
                    SetFocusBrick(it.Current);
                }

                // In case we have a duplicate queued, we want to make sure of a few things:
                // 1. All duplicated bricks now possibly reference a one-way connection to the old 
                //    selection. In this case, ONLY set the reference on this side to null. Using Connect(null), would
                //    result in the original bricks losing their connections. Remember to record prefab changes.

                // 2. Add these pairs of connection and old connection to a list, so that we can check them later
                //    when we want to re-establish connections in the new selection. This really only applies for brick
                //    selections larger than 1.

                var connectionPairs = new List<(Connection, Connection)>();
                
                // In our new selection check all bricks
                foreach(var brick in selectedBricks)
                {
                    foreach (var part in brick.parts)
                    {
                        foreach (var field in part.connectivity.connectionFields)
                        {
                            foreach (var connection in field.connections)
                            {
                                // For each connection, remove the one-way reference to the previous connection 
                                if(connection.HasConnection())
                                {
                                    var connectedTo = connection.connectedTo;
                                    connection.connectedTo = null;
                                    Connection.RegisterPrefabChanges(connection);
                                    connectionPairs.Add((connection, connectedTo));
                                }
                            }
                        }
                    }
                }

                foreach(var (connection, connectedTo) in connectionPairs)
                {
                    var brick = connection.field.connectivity.part.brick;

                    // Now check all other bricks for a connection equivalent to the old one
                    foreach(var otherBrick in selectedBricks)
                    {
                        if(otherBrick == brick)
                        {
                            continue;
                        }

                        if(otherBrick.transform.position != connectedTo.field.connectivity.part.brick.transform.position)
                        {
                            continue;
                        }

                        foreach(var otherPart in otherBrick.parts)
                        {
                            if(otherPart.transform.position != connectedTo.field.connectivity.part.transform.position)
                            {
                                continue;
                            }

                            foreach(var otherField in otherPart.connectivity.connectionFields)
                            {
                                if(otherField.transform.position != connectedTo.field.transform.position)
                                {
                                    continue;
                                }

                                foreach(var otherConnection in otherField.connections)
                                {
                                    if(otherConnection.connectedTo != null)
                                    {
                                        continue;
                                    }

                                    if(connectedTo.transform.position == otherConnection.transform.position)
                                    {
                                        connection.Connect(otherConnection);
                                    }
                                }
                            }
                        }
                    }
                    connection.UpdateKnobsAndTubes();
                }
                

                if(focusBrick != null)
                {
                    StartMovingBrick(focusBrick);
                    currentMouseDelta = stickySnapDistance;
                }
            }
        }

        private static void QueueSelection(HashSet<Brick> selection)
        {
            queuedSelection = new List<GameObject>();
            foreach (var obj in selection)
            {
                queuedSelection.Add(obj.gameObject);
            }
        }

        [MenuItem(expandSelectionMenuPath + expandSelectionShortcut, priority = 40)]
        public static void ExpandSelection()
        {
            var connectedBricks = new HashSet<Brick>();
            if(buildWithBricks)
            {
                foreach(var brick in selectedBricks)
                {
                    var connected = brick.GetConnectedBricks();
                    connectedBricks.UnionWith(connected);
                }
                selectedBricks.UnionWith(connectedBricks);
                QueueSelection(selectedBricks);
            }
            else
            {
                foreach(var obj in Selection.objects)
                {
                    var go = obj as GameObject;
                    if(go != null)
                    {
                        var brick = go.GetComponentInParent<Brick>();
                        if(brick != null)
                        {
                            var connected = brick.GetConnectedBricks();
                            connectedBricks.UnionWith(connected);
                            connectedBricks.Add(brick);
                        }
                    }
                }

                var selection = new List<GameObject>();
                foreach(var brick in connectedBricks)
                {
                    selection.Add(brick.gameObject);
                }

                queuedSelection = selection;
            }           
        }

        [MenuItem(expandSelectionMenuPath + expandSelectionShortcut, true)]
        private static bool ValidateExpandSelection()
        {
            return ExpandSelectionEnabled();
        }

        public static bool ExpandSelectionEnabled()
        {
            if(buildWithBricks)
            {
                return currentSelectionState == SelectionState.selected;
            }
            
            foreach(var obj in Selection.objects)
            {
                var go = obj as GameObject;
                if(go == null)
                {
                    return false;
                }
                
                var brick = go.GetComponentInParent<Brick>();
                if(brick == null)
                {
                    return false;
                }
            }
            return true;
        }

        private static void UpdateRemovedBricks()
        {
            var toRemove = new List<Brick>();
            foreach(var brick in selectedBricks)
            {
                if(brick == null)
                {
                    toRemove.Add(brick);
                }
            }

            foreach(var brick in toRemove)
            {
                if(brick == focusBrick)
                {
                    SetFocusBrick(null);
                }
                selectedBricks.Remove(brick);
            }
        }

        private static void OnSceneGUIBuilding(SceneView sceneView)
        {            
            if(Event.current.type == EventType.DragPerform)
            {
                var objects = DragAndDrop.objectReferences;
                if(objects.Length > 0)
                {
                    PlaceBrick();
                    SetFocusBrick(null);
                    selectedBricks.Clear();
                    dragAndDropQueued = true;
                }
            }

            CheckChangedTransforms();
            UpdateRemovedBricks();

            if (IsPickingEvent(Event.current))
            {
                UpdateMouse(Event.current.mousePosition);
            }

            // Make sure that when we are starting to drag a brick in the scene that we are allowed to.
            // Allowed to means:
            // - The mouse is over the scene view. There is no reason to check for a brick if we are not mousing over the scene view
            // - If hot control is zero, we are sure that we are not interacting with any handle/tool (move tool, rotation tool etc.)
            if(GUIUtility.hotControl != 0)
            {
                // Dragging a gizmo over a brick starts the dragging state
                if(currentSelectionState == SelectionState.dragging)
                {
                    SetSelectionState(SelectionState.noSelection);
                }
                currentMouseDelta = 0.0f;
            }
            else
            {
                if(Event.current.button == 0 && Tool.View != Tools.current && IsPickingEvent(Event.current) && IsOverSceneView())
                {
                    hitBrick = BrickUnderRay(Event.current, out _);
                }

                if (hitBrick != null || (focusBrick != null && currentSelectionState == SelectionState.moving))
                {
                    HandleUtility.AddDefaultControl(0);
                }

                if(currentSelectionState != SelectionState.dragging)
                {
                    if (IsDuplicateEvent(Event.current))
                    {
                        PlaceBrick();
                        duplicateQueued = true;
                    }
                }

                EvaluateSelectionState(Event.current, sceneView.camera, sceneView);
            }
        }

        private static void SetSelectionState(SelectionState newState)
        {
            rotateOrNudgeDirection = InteractionKey.none;
            currentSelectionState = newState;
        }

        private static void ApplyConnectivityLayer()
        {
            var fields = StageUtility.GetCurrentStageHandle().FindComponentsOfType<ConnectionField>();
            foreach(var field in fields)
            {
                if(!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    foreach(var connection in field.connections)
                    {
                        if(connection == null)
                        {
                            continue;
                        }
                        connection.gameObject.layer = LayerMask.NameToLayer(Connection.connectivityFeatureLayerName);
                    }
                }
            }
        }

        private static void SyncBounds()
        {
            selectionBounds = BrickBuildingUtility.ComputeBounds(selectedBricks);
        }

        private static bool CanConnect((Connection, Connection) chosenConnection, RaycastHit collidingHit, out Vector3 pivot, out Vector3 connectedOffset, out float angle, out Vector3 axis)
        {            
            if(chosenConnection != (null, null))
            {
                // Compute the pivot for the rotation
                pivot = chosenConnection.Item1.field.connectivity.part.brick.transform.position + pickupOffset;

                // Get the connected transformation to compute a snapping position
                ConnectionField.GetConnectedTransformation(chosenConnection.Item1, chosenConnection.Item2, pivot, out connectedOffset, out angle, out axis);

                // Check if the chosen connection will be underneath the hitpoint in local space of the hit plane.
                var hitNormal = collidingHit.normal;
                var hitPoint = collidingHit.point;
                var transformation = Matrix4x4.TRS(hitPoint, Quaternion.FromToRotation(Vector3.up, hitNormal), Vector3.one).inverse;
                var localConnection = transformation.MultiplyPoint(chosenConnection.Item2.transform.position);
                
                return localConnection.y >= 0.0f || collidingHit.transform == null;
            }
            pivot = Vector3.zero;
            connectedOffset = Vector3.zero;
            angle = 0.0f;
            axis = Vector3.zero;

            return false;
        }

        private static void ComputeNewConnection(Camera camera, Ray ray)
        {
            if (focusBrick == null)
            {
                return;
            }

            // We only need to cache the position of the focus brick, since every other brick
            // can be repositioned relative to the focus brick (see: rotationOffsets)
            var oldFocusPosition = focusBrick.transform.position;

            // catch-all rotation caching if any collision happens 
            var oldRotations = new List<Quaternion>();

            foreach(var brick in selectedBricks)
            {
                oldRotations.Add(brick.transform.rotation);
            }
            
            var pivot = focusBrick.transform.position + pickupOffset;

            // Align the bricks to intersecting geometry or a fallback plane (worldPlane)
            BrickBuildingUtility.AlignBricks(focusBrick, selectedBricks, selectionBounds, pivot, pickupOffset, ray, worldPlane, out Vector3 offset, out Vector3 alignedOffset, out Quaternion rotation, out RaycastHit collidingHit);

            // We only rotate if we are not in the middle of a connection, since then we already know where we will place the brick(s).
            // This makes it easier to connect when already in a connection.
            if(currentConnection == (null, null))
            {
                // Before we align, cache the pickup offset so we don't get weird placements after rotating.
                // This is especially important for larger selections.
                
                var localOffset = focusBrick.transform.InverseTransformDirection(pickupOffset);
                rotation.ToAngleAxis(out float alignedAngle, out Vector3 alignedAxis);
                foreach(var brick in selectedBricks)
                {
                    brick.transform.RotateAround(pivot, alignedAxis, alignedAngle);
                }

                // Transform pickup offset back to world space for later use
                pickupOffset = focusBrick.transform.TransformDirection(localOffset);
                
                // Cache new rotations for later
                var k = 0;
                foreach(var brick in selectedBricks)
                {
                    rotationOffsets[k] = (brick.transform.rotation, rotationOffsets[k].Item2);
                    k++;
                }
            }

            // Apply the un-aligned offset to place the brick in the world            
            focusBrick.transform.position += offset;
            ResetPositions();
            SyncBounds();

            // Find the best connection after the brick has been moved
            var chosenConnection = BrickBuildingUtility.FindBestConnection(pickupOffset, selectedBricks, ray, camera, bricks, maxTriesPerBrick);
            
            Quaternion rot = Quaternion.identity;

            if (CanConnect(chosenConnection, collidingHit, out pivot, out Vector3 connectedOffset, out float angle, out Vector3 axis))
            {
                // Cache the local pickup offset to recompute it after connection
                var localOffset = focusBrick.transform.InverseTransformDirection(pickupOffset);
                currentConnection = chosenConnection;  

                BrickBuildingUtility.AlignTransformations(selectedBricks, pivot, axis, angle, connectedOffset);

                // Compute place offset:
                // 1. Find all connections at current position.
                // 2. If a connection is to a non-selected brick, get the preconnect offset.
                // 3. If all preconnect offsets are similar, use them as place offset.
                // 4. Move the selected bricks with the place offset.
                Physics.SyncTransforms();

                HashSet<(Connection, Connection)> currentConnections = new HashSet<(Connection, Connection)>();
                foreach (var brick in selectedBricks)
                {
                    foreach (var part in brick.parts)
                    {
                        foreach (var connectionField in part.connectivity.connectionFields)
                        {
                            currentConnections.UnionWith(connectionField.QueryConnections());
                        }
                    }
                }

                var potentialPlaceOffset = Vector3.zero;
                var firstPlaceOffset = true;
                foreach (var connection in currentConnections)
                {
                    if (!selectedBricks.Contains(connection.Item2.field.connectivity.part.brick))
                    {
                        var preconnectOffset = Connection.GetPreconnectOffset(connection.Item2);
                        if (firstPlaceOffset)
                        {
                            potentialPlaceOffset = preconnectOffset;
                            firstPlaceOffset = false;
                        }
                        else
                        {
                            if ((preconnectOffset - potentialPlaceOffset).sqrMagnitude > 0.01f)
                            {
                                potentialPlaceOffset = Vector3.zero;
                                break;
                            }
                        }
                    }
                }

                placeOffset = potentialPlaceOffset;
                foreach (var brick in selectedBricks)
                {
                    brick.transform.position += placeOffset;
                }
                pickupOffset = focusBrick.transform.TransformDirection(localOffset);
            }
            else
            {
                // Revert the offset and apply the aligned offset
                focusBrick.transform.position -= offset;
                focusBrick.transform.position += alignedOffset;
                ResetPositions();

                var localPickupOffset = focusBrick.transform.InverseTransformDirection(pickupOffset);
                // If we collide at this position, simply reset to before we moved the bricks.
                // This could mean back to a possible connection or simply on intersecting geometry.
                if (CollideAtTransformation(selectedBricks))
                {    
                    var k = 0;
                    foreach (var brick in selectedBricks)
                    {
                        brick.transform.rotation = oldRotations[k++];
                    }
                    focusBrick.transform.position = oldFocusPosition;
                    ResetPositions();
                }
                else if(currentConnection != (null, null))
                {
                    // We don't have a collision at the moved position
                    // In case we don't have a collision at this position, we need to reset the selection to their
                    // original rotations from previously. 
                    // Once we reset the original rotations, we need to find a position in the scene to re-align them to
                    // any intersecting geometry they might hit now.
                    // Doing this reset (both rotation and re-aligning), might result in a collision again.
                    // In case we collide again, we have to go back to before we tried to reposition the selection
                    // since it is invariant that the bricks never collide after this function.

                    var i = 0;
                    var previousRotations = new List<Quaternion>();

                    foreach(var brick in selectedBricks)
                    {
                        previousRotations.Add(brick.transform.rotation);
                        brick.transform.rotation = rotationOffsets[i++].Item1;
                    }
                    SyncBounds();

                    // Realignment needs a correct pickupOffset
                    pickupOffset = focusBrick.transform.TransformDirection(localPickupOffset);

                    pivot = focusBrick.transform.position + pickupOffset;
                    BrickBuildingUtility.AlignBricks(focusBrick, selectedBricks, selectionBounds, pivot, pickupOffset, ray, worldPlane, out _, out alignedOffset, out _, out _);

                    focusBrick.transform.position += alignedOffset;
                    ResetPositions();
                    Physics.SyncTransforms();

                    if(CollideAtTransformation(selectedBricks))
                    {
                        focusBrick.transform.position = oldFocusPosition;
                        i = 0;
                        foreach(var brick in selectedBricks)
                        {
                            brick.transform.rotation = previousRotations[i++];
                        }
                        ResetPositions();
                    }
                    else
                    {
                        currentConnection = (null, null);
                    }
                }

                pickupOffset = focusBrick.transform.TransformDirection(localPickupOffset);
            }
            SyncBounds();
        }

        private static bool CollideAtTransformation(HashSet<Brick> bricks)
        {
            foreach (var brick in bricks)
            {
                if (BrickBuildingUtility.IsCollidingAtTransformation(brick, brick.transform.position, brick.transform.rotation, bricks))
                {
                    return true;
                }
            }
            return false;
        }

        private static void ResetPositions()
        {
            if(rotationOffsets.Count == 0)
            {
                return;
            }                      

            var j = 0;            
            foreach (var selected in selectedBricks)
            {
                var (_, localOffset) = rotationOffsets[j++];
                if(selected == focusBrick)
                {
                    continue;
                }
                var offset = focusBrick.transform.TransformVector(localOffset);
                selected.transform.position = focusBrick.transform.position - offset;
            }
        }
        
        private static void EditorUpdate()
        {
            sceneViewCurrentlyInFocus = EditorWindow.focusedWindow == SceneView.lastActiveSceneView;
            if (queuedSelection != null)
            {
                Selection.objects = queuedSelection.ToArray();
                queuedSelection = null;

                if(buildWithBricks)
                {
                    if (currentSelectionState != SelectionState.moving)
                    {
                        if (selectedBricks.Count > 0 && Selection.objects.Length == selectedBricks.Count)
                        {
                            Tools.hidden = true;
                            SetSelectionState(SelectionState.selected);
                        }
                        else
                        {
                            Tools.hidden = false;
                            SetSelectionState(SelectionState.noSelection);
                            selectionBounds = new Bounds();
                        }
                    }
                }
            }

            if (undoQueued)
            {
                undoQueued = false;
                Undo.PerformUndo();
            }

            if (dirtyConnections.Count > 0)
            {
                foreach(var connection in dirtyConnections)
                {
                    if (connection)
                    {
                        if (connection.knob)
                        {
                            Undo.RegisterCompleteObjectUndo(connection.knob.gameObject, "Updating connection");
                        }
                        foreach (var tube in connection.tubes)
                        {
                            if (tube)
                            {
                                Undo.RegisterCompleteObjectUndo(tube.gameObject, "Updating connection");
                            }
                        }
                        connection.UpdateKnobsAndTubes();
                    }
                }

                dirtyConnections.Clear();
            }
        }

        private static void OnUndo()
        {
            switch (currentSelectionState)
            {
                case SelectionState.moving:
                    {
                        SetSelectionState(SelectionState.selected);
                    }
                    break;
            }
            SetFromSelection(false);
        }

        private static void SetFocusBrick(Brick brick)
        {
            focusBrick = brick;
            if (focusBrick != null)
            {
                Tools.hidden = true;
                rotationOffsets.Clear();
                foreach(var selected in selectedBricks)
                {
                    var offset = focusBrick.transform.position - selected.transform.position;
                    var localOffset = focusBrick.transform.InverseTransformVector(offset);
                    rotationOffsets.Add((selected.transform.rotation, localOffset));
                }
            }
            else
            {
                Tools.hidden = false;
                rotationOffsets.Clear();
                SetSelectionState(SelectionState.noSelection);
            }
        }
    }
}
#endif