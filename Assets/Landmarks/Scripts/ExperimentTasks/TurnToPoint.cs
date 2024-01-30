using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnToPoint : ExperimentTask
{
    [Header("Task-specific Properties")]
    [Min(0)] public float timeAllotted_s;
    private float onset;
    private float responseLatency;

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

        // WRITE TASK STARTUP CODE HERE
        onset = -1f;
    }


    public override bool updateTask()
    {
        if (onset < 0) onset = Time.time;
        responseLatency = Time.time - onset;

        if (Input.GetKeyDown(KeyCode.Return))
        {
            return true;
        }

        return false;
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
        taskLog.AddData(transform.name + "_onset_s", onset.ToString());
        taskLog.AddData(transform.name + "_duration_s", timeAllotted_s.ToString());
        taskLog.AddData(transform.name + "_responseLatency", responseLatency.ToString());

    }

}