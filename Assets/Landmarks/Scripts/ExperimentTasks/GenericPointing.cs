/*

*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GenericPointing : ExperimentTask
{
    [Header("Task-specific Properties")]
    [TextArea] public string prompt_text = "Point to the {0}.";
    public List<ObjectList> prompt_elements = new List<ObjectList>();
    private string prompt;

    public override void startTask()
    {
        TASK_START();

        // LEAVE BLANK
    }


    public override void TASK_START()
    {
        if (!manager) Start();
        base.startTask();

        if (skip)
        {
            log.log("INFO    skip task    " + name, 1);
            return;
        }

        // Configure the prompt for the HUD
        foreach (var element in prompt_elements)
        {
            try
            {
                prompt = string.Format(prompt_text, element.currentObject().name);
                taskLog.AddData(transform.name + "_target", element.currentObject().name);
                element.incrementCurrent();
                Debug.Log("INCREMENTED!");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Possible mismatch between prompt elements specified and elements provided");
            }
            
        }
        
        Debug.Log(prompt_text);
        Debug.Log(prompt);
        hud.setMessage(prompt);
        hud.showOnlyHUD();
    }


    public override bool updateTask()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            return true;
        }
        else return false;

        // WRITE TASK UPDATE CODE HERE
    }


    public override void endTask()
    {
        TASK_END();

        // LEAVE BLANK
    }


    public override void TASK_END()
    {
        base.endTask();

        // WRITE TASK EXIT CODE HERE
        Debug.Log("Done!");

    }

}