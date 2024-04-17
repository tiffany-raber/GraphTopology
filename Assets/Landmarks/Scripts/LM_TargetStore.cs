using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UI;

public class LM_TargetStore : LM_Target
{
    //[Header("Store Identity")]
    //public Sprite storeIcon;
    [Header("Store-Specific Properties")]
    public GameObject[] exteriorElements;
    public Text[] signTextElements;
    public Image[] iconImageElements;
    [Header("Door Properties")]
    public GameObject door;
    public float doorMaxOpenAngle = -115;
    public float doorSpeedMulitplier = 1;
    private bool doorOpen;
    private bool doorInMotion;
    public GameObject collisionIndicator;
    private GameObject m_collisionIndicator;

    //private Color storeColor;


    private void Awake()
    {
        //if (!useExisting)
        //{
        //    // Assign any text elements
        //    foreach (var textitem in signTextElements)
        //    {
        //        textitem.text = gameObject.name;
        //    }
        //    // Assign any icon elements
        //    foreach (var iconitem in iconImageElements)
        //    {
        //        iconitem.sprite = storeIcon;
        //    }

        //    storeColor = color;
        //}
        //else storeColor = exteriorElements[0].GetComponent<Renderer>().material.color;
    }

    // Start is called before the first frame update
    void Start()
    {
        door.transform.localEulerAngles = Vector3.zero;
    }

    // Update is called once per frame
    void Update()
    {

        
    }

    public void ChangeMaterial(Material mat)
    {
        foreach (var elem in exteriorElements)
        {
            elem.GetComponent<Renderer>().material = mat;
        }
    }

    public void ChangeColor(Color col)
    {
        foreach (var elem in exteriorElements)
        {
            elem.GetComponent<Renderer>().material.color = col;
            Debug.Log("Element " + elem.name.ToString() + " changed to color " + col);
        }
    }

    public void SetActiveTarget(bool yes)
    {
        if (yes)
        {
            OpenDoor();
            InstantiateIndicator(true);
            
        }
        else 
        {
            CloseDoor();
            InstantiateIndicator(false);    
        }

    }

    private void InstantiateIndicator(bool yes)
    {
        if (collisionIndicator != null)
        {
            if (yes) 
            {
                m_collisionIndicator = Instantiate(collisionIndicator, GetComponentInChildren<LM_SnapPoint>().transform);
                // Color match the target store's identifying color
                foreach (var mr in m_collisionIndicator.GetComponentsInChildren<Renderer>()) 
                {
                    
                    mr.material.color = exteriorElements[0].GetComponent<Renderer>().material.color;
                }
            }
            else Destroy(m_collisionIndicator);
        }
    }

    public void OpenDoor()
    {
        if (!doorInMotion || doorOpen) StartCoroutine(Open());
    }

    public void CloseDoor()
    {
        if (!doorInMotion || !doorOpen) StartCoroutine(Close());
    }

    IEnumerator Open()
    {
        doorInMotion = true;
        for (float ft = 0; ft > doorMaxOpenAngle; ft--)
        {
            door.transform.localEulerAngles = new Vector3(0f, ft, 0f);
            yield return null;
        }
        door.transform.localEulerAngles = new Vector3(0f, doorMaxOpenAngle, 0f);
        doorInMotion = false;
        doorOpen = true;
    }

    IEnumerator Close()
    {
        doorInMotion = true;
        for (float ft = doorMaxOpenAngle; ft < 0; ft++)
        {
            door.transform.localEulerAngles = new Vector3(0f, ft, 0f);
            yield return null;
        }
        door.transform.localEulerAngles = Vector3.zero;
        doorInMotion = false;
        doorOpen = false;
    }
}
