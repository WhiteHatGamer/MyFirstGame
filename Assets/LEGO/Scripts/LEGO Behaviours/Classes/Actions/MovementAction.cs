using LEGOMaterials;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.LEGO.Behaviours.Actions
{
    public abstract class MovementAction : RepeatableAction
    {
        [SerializeField, Tooltip("The time in seconds to complete each movement.")]
        protected float m_Time = 2.0f;

        [SerializeField, Tooltip("Stop when colliding with objects.")]
        protected bool m_Collide = true;

        protected float m_CurrentTime;
        protected bool m_PlayAudio = true;

        protected HashSet<(Collider, Collider)> m_ActiveColliderPairs = new HashSet<(Collider, Collider)>();

        HashSet<(Collider, Collider)> m_ColliderPairsToRemove = new HashSet<(Collider, Collider)>();

        protected override void Reset()
        {
            base.Reset();

            m_FlashColour = MouldingColour.GetColour(MouldingColour.Id.BrightBlue) * 2.0f;
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            m_Time = Mathf.Max(0.0f, m_Time);
        }

        protected override void Start()
        {
            base.Start();

            if (IsPlacedOnBrick())
            {

                // Add MovementCollider to all brick colliders.
                foreach (var brick in m_ScopedBricks)
                {
                    foreach (var part in brick.parts)
                    {
                        foreach (var collider in part.colliders)
                        {
                            var movementCollider = LEGOBehaviourCollider.Add<MovementCollider>(collider, m_ScopedBricks);
                            movementCollider.OnColliderActivated += MovementColliderActivated;
                            movementCollider.OnColliderDeactivated += MovementColliderDeactivated;
                        }
                    }
                }
            }
        }

        protected void MovementColliderActivated((Collider, Collider) colliderPair)
        {
            m_ActiveColliderPairs.Add(colliderPair);
        }

        protected void MovementColliderDeactivated((Collider, Collider) colliderPair)
        {
            m_ActiveColliderPairs.Remove(colliderPair);
        }

        protected virtual bool IsColliding()
        {
            m_ColliderPairsToRemove.Clear();

            foreach(var activeColliderPair in m_ActiveColliderPairs)
            {
                if (!activeColliderPair.Item2)
                {
                    m_ColliderPairsToRemove.Add(activeColliderPair);
                }
            }

            foreach(var colliderPairToRemove in m_ColliderPairsToRemove)
            {
                m_ActiveColliderPairs.Remove(colliderPairToRemove);
            }

            return m_Collide && m_ActiveColliderPairs.Count > 0;
        }
    }
}
