using LEGOModelImporter;
using System;
using System.Collections.Generic;
using Unity.LEGO.Behaviours.Triggers;
using UnityEngine;

namespace Unity.LEGO.Behaviours
{
    public class SensoryCollider : LEGOBehaviourCollider
    {
        public Action<SensoryCollider> OnSensorActivated;
        public Action<SensoryCollider> OnSensorDeactivated;

        public SensoryTrigger.Sense Sense;

        public List<Brick> SpecificBricks;

        HashSet<Collider> m_ActiveTriggers = new HashSet<Collider>();

        void OnTriggerEnter(Collider other)
        {
            if (IsSensed(other))
            {
                if (m_ActiveTriggers.Count == 0)
                {
                    OnSensorActivated?.Invoke(this);
                }
                m_ActiveTriggers.Add(other);
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (IsSensed(other))
            {
                m_ActiveTriggers.Remove(other);
                if (m_ActiveTriggers.Count == 0)
                {
                    OnSensorDeactivated?.Invoke(this);
                }
            }
        }

        bool IsSensed(Collider collider)
        {
            // Do not collide with triggers.
            if (collider.isTrigger)
            {
                return false;
            }

            switch(Sense)
            {
                case SensoryTrigger.Sense.Player:
                    {
                        // If sensing player, check if collider belongs to player.
                        return collider.gameObject.CompareTag("Player");
                    }
                case SensoryTrigger.Sense.AllBricks:
                    {
                        // If sensing all bricks, first check for collision with projectiles.
                        if (collider.gameObject.CompareTag("Projectile"))
                        {
                            return true;
                        }

                        // If sensing all bricks, do not collide with bricks in the ignored set. This is typically the scope of the SensoryTrigger.
                        var brick = collider.GetComponentInParent<Brick>();
                        if (m_IgnoredBricks.Contains(brick))
                        {
                            return false;
                        }

                        // If sensing all bricks, check if colliding with brick.
                        return brick;
                    }
                case SensoryTrigger.Sense.SpecificBricks:
                    {
                        // If sensing specific bricks, just check if colliding with one of those bricks.
                        var brick = collider.GetComponentInParent<Brick>();
                        return SpecificBricks.Contains(brick);
                    }
            }

            return false;
        }

        void OnDestroy()
        {
            OnSensorDeactivated?.Invoke(this);
        }
    }
}
