using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class ColorFastObject : MonoBehaviour
{
    public Color originalColor;
    

    // Start is called before the first frame update
    void Start()
    {
       
    }

    // Update is called once per frame
    void Update()
    {
        if (!GetComponent<Renderer>().material.color.Equals(originalColor))
        {
            GetComponent<Renderer>().material.color = originalColor;
            Debug.LogWarning("The color of " + name + "could not be changed because it has a ColorFastObject component attached alongside its renderer.");
        }
    }
}
