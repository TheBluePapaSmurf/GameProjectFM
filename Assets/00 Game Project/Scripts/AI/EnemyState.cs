using UnityEngine;

namespace GameProjectFM.AI.Core
{
    public enum PatrolType
    {
        Loop,
        PingPong,
        Random,
        StayInArea
    }

    public enum EnemyState
    {
        Patrolling,
        Moving,
        Waiting,
        LookingAround,
        Chasing,
        Returning,
        InvestigatingTrail,
        MovingToTrail
    }

    [System.Serializable]
    public class TrailInfo
    {
        public Vector3Int position;
        public float distance;
    }
}
