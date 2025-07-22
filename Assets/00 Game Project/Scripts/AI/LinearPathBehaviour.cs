using UnityEngine;

public class LinearPathBehaviour : MonoBehaviour
{
    [SerializeField]
    private bool reversePath;

    private Color gizmosColor = Color.red;
    private Color gizmosSelectedColor = Color.yellow;

    public Transform GetFirstPoint()
    {
        if (reversePath)
        {
            return transform.GetChild(transform.childCount - 1);
        }

        return transform.GetChild(0);
    }

    public Transform GetNextPoint(Transform currentPoint)
    {
        if (currentPoint == null)
        {
            return transform.GetChild(0);
        }

        int nextIndex = currentPoint.GetSiblingIndex();
        if (reversePath)
        {
            if (nextIndex > 0)
                return transform.GetChild(nextIndex - 1);
            else
                return transform.GetChild(transform.childCount - 1); // Ga naar het laatste punt als het begin is bereikt
        }
        else
        {
            if (nextIndex < transform.childCount - 1)
                return transform.GetChild(nextIndex + 1);
            else
                return transform.GetChild(0); // Ga terug naar het eerste punt als het einde is bereikt
        }
    }

    public Transform GetClosestPoint(Vector3 currentPosition)
    {
        Transform closestPoint = null;
        float minDistance = float.MaxValue; // Start met een maximale afstand

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform point = transform.GetChild(i);
            float distance = Vector3.Distance(currentPosition, point.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestPoint = point;
            }
        }

        return closestPoint; // Geef het dichtstbijzijnde punt terug
    }

    private void OnDrawGizmos()
    {
        if(transform.childCount < 2)
        {
            return;
        }

        if (reversePath)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawCube(transform.GetChild(transform.childCount - 1).position, new(.2f, .2f, .2f));

            for (int i = 0; i < transform.childCount - 1; ++i)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(transform.GetChild(i).position, .1f);

                Gizmos.color = gizmosColor;
                Gizmos.DrawLine(transform.GetChild(i).position, transform.GetChild(i + 1).position);
            }
        }
        else
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawCube(transform.GetChild(0).position, new(.2f, .2f, .2f));

            for (int i = 1; i < transform.childCount; ++i)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(transform.GetChild(i).position, .1f);

                Gizmos.color = gizmosColor;
                Gizmos.DrawLine(transform.GetChild(i).position, transform.GetChild(i - 1).position);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (transform.childCount < 2)
        {
            return;
        }

        if (reversePath)
        {
            for (int i = 0; i < transform.childCount - 1; ++i)
            {
                Gizmos.color = gizmosSelectedColor;
                Gizmos.DrawLine(transform.GetChild(i).position, transform.GetChild(i + 1).position);
            }
        }
        else
        {
            for (int i = 1; i < transform.childCount; ++i)
            {
                Gizmos.color = gizmosSelectedColor;
                Gizmos.DrawLine(transform.GetChild(i).position, transform.GetChild(i - 1).position);
            }
        }
    }
}
