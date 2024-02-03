using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class scriptyscript : MonoBehaviour
{
    public bool randomHeading;
    // Start is called before the first frame update
    void Start()
    {
        if (randomHeading)
        {
            // random start orientation
            var tmp = transform.eulerAngles;
            int newz = Random.Range(0, 359);
            tmp.z = newz;
            transform.eulerAngles = tmp;
        }

    }

    // Update is called once per frame
    void Update()
    {
        //transform.Rotate(new Vector3(0, 0, 10 * Time.deltaTime));    
    }
}
