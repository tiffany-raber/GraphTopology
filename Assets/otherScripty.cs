using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class otherScripty : MonoBehaviour
{
    public float rotSpeed = 60;
    private Vector3 orientation;
    private bool responded;

    public Transform origin;
    // Start is called before the first frame update
    void Start()
    {
        if (origin == null) origin = transform;
        orientation = transform.eulerAngles;
    }

    // Update is called once per frame
    void Update()
    {
        // rotate the response estimate
        if (Input.GetKey(KeyCode.Alpha1)) transform.RotateAround(origin.position, Vector3.forward, rotSpeed * Time.deltaTime);
        else if (Input.GetKey(KeyCode.Alpha2)) transform.RotateAround(origin.position, Vector3.back, rotSpeed * Time.deltaTime);

        // Keep the image upright
        transform.eulerAngles = orientation;

        // Register the response only if it hasn't been provided already
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (!responded)
            {
                responded = true;
                Debug.Log("Response submitted");
            }
            else Debug.Log("You cannot submit more than one response");
        }
        
    }
}
