using UnityEngine;

namespace Unity.LEGO.Behaviours.Actions
{
    public class LookAtAction : MovementAction
    {
        public enum LookAt
        {
            Player,
            Transform
        }

        public enum Rotate
        {
            Horizontally,
            Vertically,
            Freely
        }

        [SerializeField, Tooltip("Look at the player.\nor\nLook at a transform.")]
        LookAt m_LookAt = LookAt.Player;

        [SerializeField, Tooltip("The transform to look at.")]
        Transform m_TransformModeTransform = null;

        [SerializeField, Tooltip("The angular speed in degrees per second.")]
        int m_Speed = 180;

        [SerializeField, Tooltip("Rotate horizontally only.\nor\nRotate vertically only.\nor\nRotate freely.")]
        Rotate m_Rotate = Rotate.Horizontally;

        enum State
        {
            looking,
            waitingToLook
        }

        State m_State;

        Transform m_PlayerTransform;
        float m_RotationAngle;
        Vector3 m_rotationAxis;
        float m_VerticalRotatedAngle;
        float m_HorizontalRotatedAngle;

        public float GetVerticalRotatedAngle()
        {
            return m_VerticalRotatedAngle;
        }

        public float GetHorizontalRotatedAngle()
        {
            return m_HorizontalRotatedAngle;
        }

        protected override void Reset()
        {
            base.Reset();

            m_Time = 1.0f;
            m_IconPath = "Assets/LEGO/Gizmos/LEGO Behaviour Icons/Look At Action.png";
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            m_Speed = Mathf.Max(1, m_Speed);
            m_Time = Mathf.Max(0.1f, m_Time);
        }

        protected override void Start()
        {
            base.Start();

            m_PlayerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        }

        void FixedUpdate()
        {
            if (m_Active)
            {
                // Update time.
                m_CurrentTime += Time.fixedDeltaTime;

                // Look.
                if (m_State == State.looking)
                {
                    // Compute the rotation to look at the target.
                    ComputeRotation(out m_RotationAngle, out m_rotationAxis);

                    // Handle collision.
                    if (IsColliding())
                    {
                        m_CurrentTime = 0.0f;
                        m_VerticalRotatedAngle = 0.0f;
                        m_HorizontalRotatedAngle = 0.0f;
                        m_State = State.waitingToLook;
                    }
                    else
                    {
                        // Play audio.
                        if (m_PlayAudio && m_RotationAngle > 0.1f)
                        {
                            PlayAudio();
                            m_PlayAudio = false;
                        }

                        // Rotate bricks.
                        foreach (var brick in m_ScopedBricks)
                        {
                            brick.transform.RotateAround(transform.position + transform.TransformVector(m_BrickPivotOffset), m_rotationAxis, m_RotationAngle);
                        }

                        // Find rotated angles. Used by custom editor to draw scene UI.
                        var rotation = Quaternion.AngleAxis(m_RotationAngle, m_rotationAxis);
                        switch (m_Rotate)
                        {
                            case Rotate.Horizontally:
                                {
                                    UpdateHorizontalRotatedAngle(rotation);
                                    break;
                                }
                            case Rotate.Vertically:
                                {
                                    UpdateVerticalRotatedAngle(rotation);
                                    break;
                                }
                            case Rotate.Freely:
                                {
                                    UpdateHorizontalRotatedAngle(rotation);
                                    UpdateVerticalRotatedAngle(rotation);
                                    break;
                                }
                        }

                        // Check if we are done rotating.
                        if (m_CurrentTime >= m_Time)
                        {
                            m_CurrentTime = 0.0f;
                            m_VerticalRotatedAngle = 0.0f;
                            m_HorizontalRotatedAngle = 0.0f;
                            m_State = State.waitingToLook;
                        }
                    }
                }

                // Waiting to look.
                if (m_State == State.waitingToLook)
                {
                    if (m_CurrentTime >= m_Pause)
                    {
                        m_CurrentTime = 0.0f;
                        m_State = State.looking;
                        m_PlayAudio = true;
                        m_Active = m_Repeat;
                    }
                }
            }
        }

        void UpdateHorizontalRotatedAngle(Quaternion rotation)
        {
            // Project both before and after unto XZ plane.
            Vector3 before = Vector3.right;
            var after = rotation * before;
            after.y = 0.0f;
            m_HorizontalRotatedAngle += Vector3.SignedAngle(before, after, Vector3.up);
        }

        void UpdateVerticalRotatedAngle(Quaternion rotation)
        {
            // Project both before and after unto plane defined by current right and world up.
            var planeNormal = Vector3.Cross(transform.right, Vector3.up);
            Vector3 before = Vector3.ProjectOnPlane(Vector3.up, planeNormal);
            var after = Vector3.ProjectOnPlane(rotation * before, planeNormal);
            m_VerticalRotatedAngle += Vector3.SignedAngle(before, after, planeNormal);
        }

        void ComputeRotation(out float rotationAngle, out Vector3 rotationAxis)
        {
            Vector3 targetedPosition;

            if (m_LookAt == LookAt.Player)
            {
                targetedPosition = m_PlayerTransform.position + Vector3.up * 2.0f; // Offset by half player height.
            }
            else
            {
                if (m_TransformModeTransform)
                {
                    targetedPosition = m_TransformModeTransform.position;
                }
                else
                {
                    targetedPosition = transform.position + transform.TransformVector(m_BrickPivotOffset) + transform.right;
                }
            }

            var desiredTargetDirection = targetedPosition - (transform.position + transform.TransformVector(m_BrickPivotOffset));
            var currentDirection = transform.right;
            Quaternion rotationDelta = Quaternion.identity;

            switch (m_Rotate)
            {
                case Rotate.Horizontally:
                    {
                        rotationDelta = ComputeHorizontalRotationDelta(currentDirection, desiredTargetDirection);
                        break;
                    }
                case Rotate.Vertically:
                    {
                        rotationDelta = ComputeVerticalRotationDelta(currentDirection, desiredTargetDirection);
                        break;
                    }
                case Rotate.Freely:
                    {
                        rotationDelta = ComputeHorizontalRotationDelta(currentDirection, desiredTargetDirection);
                        rotationDelta *= ComputeVerticalRotationDelta(currentDirection, desiredTargetDirection);
                        break;
                    }
            }

            rotationDelta.ToAngleAxis(out rotationAngle, out rotationAxis);
            // Normalize rotation angle.
            rotationAngle = Mathf.Min(m_Speed * Time.fixedDeltaTime, rotationAngle);
            // Prevent overshoot.
            rotationAngle = Mathf.Min(m_Speed * Mathf.Max(0.0f, m_Time - m_CurrentTime + Time.fixedDeltaTime), rotationAngle);
        }

        Quaternion ComputeHorizontalRotationDelta(Vector3 currentDirection, Vector3 desiredTargetDirection)
        {
            // Project directions onto X-Z plane.
            desiredTargetDirection.y = 0.0f;
            currentDirection.y = 0.0f;

            var desiredRotationDelta = Quaternion.FromToRotation(currentDirection, desiredTargetDirection);
            var rotation = Quaternion.RotateTowards(transform.rotation, transform.rotation * desiredRotationDelta, m_Speed * Time.fixedDeltaTime);
            return Quaternion.Inverse(transform.rotation) * rotation;
        }

        Quaternion ComputeVerticalRotationDelta(Vector3 currentDirection, Vector3 desiredTargetDirection)
        {
            // Rotate desiredTargetDirection into plane defined by current right and world up.
            var currentDirectionInPlane = currentDirection;
            currentDirectionInPlane.y = 0.0f;
            var desiredTargetDirectionInPlane = desiredTargetDirection;
            desiredTargetDirectionInPlane.y = 0.0f;
            var angleDiffInPlane = Vector3.SignedAngle(desiredTargetDirectionInPlane, currentDirectionInPlane, Vector3.up);
            desiredTargetDirection = Quaternion.Euler(0.0f, angleDiffInPlane, 0.0f) * desiredTargetDirection;

            var desiredRotationDelta = Quaternion.FromToRotation(currentDirection, desiredTargetDirection);
            var rotation = Quaternion.RotateTowards(transform.rotation, transform.rotation * desiredRotationDelta, m_Speed * Time.fixedDeltaTime);
            return Quaternion.Inverse(transform.rotation) * rotation;
        }

        protected override bool IsColliding()
        {
            if (base.IsColliding())
            {
                foreach (var activeColliderPair in m_ActiveColliderPairs)
                {
                    if (Physics.ComputePenetration(activeColliderPair.Item1, activeColliderPair.Item1.transform.position, activeColliderPair.Item1.transform.rotation,
                        activeColliderPair.Item2, activeColliderPair.Item2.transform.position, activeColliderPair.Item2.transform.rotation,
                        out Vector3 direction, out _))
                    {
                        // Attempt to find a point to represent the collision. This is an approximation.
                        Vector3 point;
                        var center = GetBrickCenter();
                        var colliderType = activeColliderPair.Item2.GetType();
                        if (colliderType == typeof(BoxCollider) || colliderType == typeof(SphereCollider) || colliderType == typeof(CapsuleCollider) || (colliderType == typeof(MeshCollider) && ((MeshCollider)activeColliderPair.Item2).convex))
                        {
                            point = activeColliderPair.Item2.ClosestPoint(center);
                        }
                        else
                        {
                            point = activeColliderPair.Item2.ClosestPointOnBounds(center);
                        }
                        point = activeColliderPair.Item1.ClosestPoint(point);

                        // Compute linear velocity of point as a result of the current rotation. This is again an approximation.
                        var rotatedPoint = Quaternion.AngleAxis(m_RotationAngle, m_rotationAxis) * (point - center) + center;
                        var velocity = rotatedPoint - point;
                        if (Vector3.Dot(direction, velocity) < -0.0001f)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();

            if (m_LookAt == LookAt.Transform && !m_TransformModeTransform)
            {
                var gizmoBounds = GetGizmoBounds();

                Gizmos.DrawIcon(gizmoBounds.center + Vector3.up, "Assets/LEGO/Gizmos/LEGO Behaviour Icons/Warning.png");
            }
        }
    }
}
