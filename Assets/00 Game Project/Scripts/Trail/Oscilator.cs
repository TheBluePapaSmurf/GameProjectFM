using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Oscilator : MonoBehaviour
{
    Vector3 startingPos;
    [SerializeField] Vector3 movementVector;
    [SerializeField] [Range(0,1)] float movementFactor;
    [SerializeField] float period = 2f;
    [SerializeField] float YRotation = 20f;

    [SerializeField] float XRotation, ZRotation;
    [SerializeField] float MinRotationRange = -1f, MaxRotationRange = 1f;

    void Start()
    {
        startingPos = transform.position;

        RandomRotation();
    }

    private void RandomRotation()
    {
        XRotation = Random.Range(MinRotationRange, MaxRotationRange);
        ZRotation = Random.Range(MinRotationRange, MaxRotationRange);
        this.transform.position = new Vector2(XRotation, ZRotation);
    }

    // Update is called once per frame
    void Update()
    {
        if (period <= Mathf.Epsilon) { return; }

        float cycles = Time.fixedTime / period; // Continually growing over time

        const float tau = Mathf.PI * 2;
        float rawSinWave = Mathf.Sin(cycles * tau); // going from -1 to 1

        movementFactor = (rawSinWave + 1f) / 2f; // Recalculate to go from 0 to 1

        Vector3 offset = movementVector * movementFactor;
        transform.position = startingPos + offset;

        transform.Rotate(XRotation, YRotation * Time.fixedDeltaTime, ZRotation, Space.Self);
    }
}
