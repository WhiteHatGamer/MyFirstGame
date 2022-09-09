using LEGOModelImporter;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.LEGO.Behaviours.Triggers
{
    public abstract class SensoryTrigger : Trigger
    {
        public enum Sense
        {
            Player,
            AllBricks,
            SpecificBricks
        }

        [SerializeField, Tooltip("Trigger when sensing the player.\nor\nTrigger when sensing all other bricks.\nor\nTrigger when sensing specific bricks.")]
        protected Sense m_Sense = Sense.Player;

        [SerializeField, Tooltip("The list of bricks to sense.")]
        protected List<Brick> m_SpecificSenseBricks = new List<Brick>();

        protected HashSet<SensoryCollider> m_ActiveColliders = new HashSet<SensoryCollider>();

        void Update()
        {
            if (m_ActiveColliders.Count > 0)
            {
                ConditionMet();
            }
        }

        protected void SetupSensoryCollider(SensoryCollider collider)
        {
            collider.OnSensorActivated += SensoryColliderActivated;
            collider.OnSensorDeactivated += SensoryColliderDeactivated;

            collider.Sense = m_Sense;
            if (m_Sense == Sense.SpecificBricks)
            {
                collider.SpecificBricks = m_SpecificSenseBricks;
            }
        }

        protected void SensoryColliderActivated(SensoryCollider collider)
        {
            m_ActiveColliders.Add(collider);
        }

        protected void SensoryColliderDeactivated(SensoryCollider collider)
        {
            m_ActiveColliders.Remove(collider);
        }
    }
}
