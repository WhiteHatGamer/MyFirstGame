using LEGOModelImporter;
using UnityEngine;
using Unity.LEGO.Game;
using Unity.LEGO.Minifig;

namespace Unity.LEGO.Behaviours
{
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        public bool Deadly { get; private set; } = true;

        Rigidbody m_RigidBody;
        CapsuleCollider m_Collider;
        ParticleSystem m_ParticleSystem;
        bool m_Launched;

        public void Init(float velocity, bool useGravity, float time)
        {
            m_RigidBody.velocity = transform.forward * velocity;

            m_RigidBody.useGravity = useGravity;

            Destroy(gameObject, time);
        }

        void Awake()
        {
            m_Collider = GetComponent<CapsuleCollider>();

            // Should we set these or assume they are set correctly on projectile?
            // Disable the collider. We will enable it again once the projectile is clear of any initial colliders.
            // This ensures that the projectile will not collide with the Shoot Action that fires it.
            // Also, enabling the collider will ensure that OnTriggerEnter is fired even if the projectile is spawned completely inside a Trigger collider.
            m_Collider.enabled = false;

            m_RigidBody = GetComponent<Rigidbody>();

            // Should we set these or assume they are set correctly on projectile?
            m_RigidBody.isKinematic = false;
            m_RigidBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Should we set these or assume they are set correctly on projectile?
            m_ParticleSystem = GetComponent<ParticleSystem>();
            m_ParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void Update()
        {
            // Check if the collider can be enabled.
            if (!m_Collider.enabled)
            {
                // Assumes that the capsule collider is aligned with local forward axis in projectile.
                var c0 = transform.TransformPoint(m_Collider.center - Vector3.forward * m_Collider.height * 0.5f);
                var c1 = transform.TransformPoint(m_Collider.center + Vector3.forward * m_Collider.height * 0.5f);
                var colliders = Physics.OverlapCapsule(c0, c1, m_Collider.radius);
                var collisions = false;
                foreach (var collider in colliders)
                {
                    // Do not collide with self, connectivity features, the player or colliders from other LEGOBehaviourColliders.
                    if (collider != m_Collider && collider.gameObject.layer != LayerMask.NameToLayer(Connection.connectivityFeatureLayerName) && !collider.gameObject.CompareTag("Player") && !collider.gameObject.GetComponent<LEGOBehaviourCollider>())
                    {
                        collisions = true;
                        break;
                    }
                }

                if (!collisions)
                {
                    m_Collider.enabled = true;
                }
            }

            // Play launch particle effect when projectile is no longer colliding with anything.
            if (!m_Launched && m_Collider.enabled)
            {
                m_ParticleSystem.Play();
                m_Launched = true;
            }

            if (Deadly)
            {
                transform.rotation = Quaternion.LookRotation(m_RigidBody.velocity);
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            // Check if the player was hit.
            var minifigController = collision.collider.GetComponent<MinifigController>();
            if (Deadly && minifigController)
            {
                minifigController.Explode();

                GameOverEvent evt = Events.GameOverEvent;
                evt.Win = false;
                EventManager.Broadcast(evt);
            }

            // Turn on gravity and make non-deadly.
            m_RigidBody.useGravity = true;

            Deadly = false;
        }
    }
}
