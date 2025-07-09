using UnityEngine;

public class MovementTracker
{
    private Transform trackedObject;
    private Vector3 lastPosition;
    private bool hasMoved;

    public MovementTracker(Transform objectToTrack)
    {
        trackedObject = objectToTrack;
        lastPosition = trackedObject.position;
        hasMoved = false;
    }

    // call on update (or fixed update)
    public bool CheckIfMoved()
    {
        if (trackedObject == null)
        {
            Debug.LogWarning("Tracked object is null!");
            return false;
        }

        // compare Positions
        Vector3 currentPosition = trackedObject.position;

        if (currentPosition != lastPosition)
        {
            hasMoved = true;
            lastPosition = currentPosition;
        }
        else
        {
            hasMoved = false;
        }

        return hasMoved;
    }
}
