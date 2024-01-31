using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static System.Runtime.CompilerServices.RuntimeHelpers;

public class TurnToPoint : ExperimentTask
{
    [Header("Task-specific Properties")]
    private float onset;
    private float responseMovementOnset;
    private float responseLatency;
    public KeyCode  ccwButton = KeyCode.LeftArrow;
    public KeyCode cwButton = KeyCode.RightArrow;
    public KeyCode submitButton = KeyCode.UpArrow;
    public int rotationSpeedMultiplier = 1;
    public bool useBodyNotCameraForRotation;
    private Transform rotationSource;
    private float lastFrameY;
    private float initialY; 
    private float totalClockwiseRotation; 
    private float finalY; 
    private bool hasMoved; // has the player started adjusting the facing direction yet?
    private bool responded;
    private float signedError;
    private float absoluteError;

    

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
        if (useBodyNotCameraForRotation) rotationSource = avatar.GetComponent<LM_PlayerController>().collisionObject.transform;
        else rotationSource = avatar.GetComponent<LM_PlayerController>().cam.transform;
        initialY = rotationSource.eulerAngles.y;
        totalClockwiseRotation = 0;

        onset = -1f;
    }


    public override bool updateTask()
    {
        // keep time
        if (onset < 0) onset = Time.time;
        responseLatency = Time.time - onset;

        // Keep track of rotations as we go
        totalClockwiseRotation += rotationSource.eulerAngles.y - lastFrameY;
        lastFrameY = rotationSource.eulerAngles.y;
        if (totalClockwiseRotation > 0 && !hasMoved)
        {
            responseMovementOnset = Time.time;
            hasMoved = true;
        }

        // Handle the response input 
        if (Input.GetKeyDown(submitButton) && !responded)
        {
            finalY = rotationSource.eulerAngles.y;
            responded = true;
            if (interval == 0) return true; // end with response unless duration/interval is specified
        }
        // If we're using a fixed duration just end when times up (regardless of response status
        if (interval > 0 && Time.time - onset > 0)
        {
            finalY = rotationSource.eulerAngles.y; // still record if time ran out
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

        // Calculations

        // Logging
        taskLog.AddData(transform.name + "event_onset_s", onset.ToString());
        taskLog.AddData(transform.name + "mvmt_onset_s", responseMovementOnset.ToString());
        if (interval != 0) taskLog.AddData(transform.name + "_duration_s", (interval / 1000).ToString());
        else taskLog.AddData(transform.name + "_duration_s", responseLatency.ToString());
        taskLog.AddData(transform.name + "_responseLatency", responseLatency.ToString());
        taskLog.AddData(transform.name + "_initialY", initialY.ToString());
        taskLog.AddData(transform.name + "_finalY", finalY.ToString());
        taskLog.AddData(transform.name + "_responded", responded.ToString());
        taskLog.AddData(transform.name + "_signedError", signedError.ToString());
        taskLog.AddData(transform.name + "_absError", absoluteError.ToString());

        // Clean up
    }

}