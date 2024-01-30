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

    // fMRI compatible time logging
    private float cueOnset = -1f;
    private float cueDuration;
    private float promptOnset;
    private float promptDuration;
    private float pointOnset;
    private float pointDuration;
    public int secondsToShowEnvironment = 0;
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
        var promptVars = new string[prompt_elements.Count];
        for (int iElement = 0; iElement < prompt_elements.Count; iElement++)
        {
            promptVars[iElement] = prompt_elements[iElement].currentObject().transform.name;
        }
        prompt = string.Format(prompt_text, promptVars);
        
    }


    public override bool updateTask()
    {
        if (cueOnset < 0) cueOnset = Time.time;
        var elapsedTime = Time.time - cueOnset;

        if (secondsToShowEnvironment > 0) hud.showEverything();

        // Once the environment disappears
        if (elapsedTime > secondsToShowEnvironment)
        {
            if (hud.HudState == CullState.showEverything)
            {
                hud.showOnlyHUD();
                promptOnset = Time.time;
                hud.setMessage(prompt);
            }

            if (Input.GetKeyDown(KeyCode.Return)) return true;
            else return false;
        }
        else return false;

    }


    public override void endTask()
    {
        TASK_END();

        // LEAVE BLANK
    }


    public override void TASK_END()
    {
        base.endTask();

        // Logging
        taskLog.AddData(transform.name + "_cue_onset_s", cueOnset.ToString());
        taskLog.AddData(transform.name + "_cue_duration_s", cueDuration.ToString());
        taskLog.AddData(transform.name + "_prompt_onset_s", promptOnset.ToString());
        taskLog.AddData(transform.name + "_prompt_duration_s", promptDuration.ToString());
        taskLog.AddData(transform.name + "_point_onset_s", pointOnset.ToString());
        taskLog.AddData(transform.name + "_point_duration_s", pointDuration.ToString());

        // WRITE TASK EXIT CODE HERE
        hud.setMessage("");
        if (canIncrementLists) foreach (var ol in prompt_elements) ol.incrementCurrent();
        cueOnset = -1f;

        Debug.LogWarning("Trial Completed");
    }

}