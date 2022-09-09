using System.Collections.Generic;
using UnityEngine;

namespace Unity.LEGO.Game
{
    public class ObjectiveManager : MonoBehaviour
    {
        List<IObjective> m_Objectives;

        bool m_Won;
        bool m_Lost;

        protected void Awake()
        {
            m_Objectives = new List<IObjective>();

            EventManager.AddListener<ObjectiveAdded>(OnObjectiveAdded);
        }

        void OnObjectiveAdded(ObjectiveAdded evt)
        {
            m_Won = false;

            m_Objectives.Add(evt.Objective);
            evt.Objective.OnProgress += OnProgress;
        }

        public void OnProgress(IObjective _)
        {
            m_Won = true;

            foreach (IObjective objective in m_Objectives)
            {
                m_Won &= (objective.IsCompleted || objective.m_Lose);
                m_Lost |= (objective.IsCompleted && objective.m_Lose);
            }
        }

        void Update()
        {
            if (m_Won || m_Lost)
            {
                Events.GameOverEvent.Win = m_Won || !m_Lost;
                EventManager.Broadcast(Events.GameOverEvent);

                Destroy(this);
            }
        }

        void OnDestroy()
        {
            EventManager.RemoveListener<ObjectiveAdded>(OnObjectiveAdded);
        }
    }
}
