using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static System.Runtime.CompilerServices.RuntimeHelpers;

public class TurnToPoint : ExperimentTask
{
    [Header("Task-specific Properties")]
    [Tooltip("Leave blank to use the player/avatar")]
    public ObjectList listOfOrigins;
    public ObjectList listOfHeadings;
    public ObjectList listOfTargets;
    private GameObject currentOrigin;
    private GameObject currentHeading;
    private GameObject currentTarget;
    private float onset;
    private float responseMovementOnset;
    private float responseLatency;
    public KeyCode submitButton = KeyCode.UpArrow;
    public bool useBodyNotCameraForRotation;
    private Transform rotationSource;
    private float lastFrameY;
    private float initialGlobalY; 
    private float initialLocalY;
    private float totalRotation; 
    private float netClockwiseRotation;
    private bool hasMoved; // has the player started adjusting the facing direction yet?
    private float finalGlobalY; 
    private float finalLocalY;
    private float correctLocalY;
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

        // Initialize variables that change from trial-to-trial
        if (useBodyNotCameraForRotation) rotationSource = avatar.GetComponent<LM_PlayerController>().collisionObject.transform;
        else rotationSource = avatar.GetComponent<LM_PlayerController>().cam.transform;
        lastFrameY = Single.NaN;
        totalRotation = 0;
        netClockwiseRotation = 0;
        onset = -1f;
        responded = false;
        hasMoved =false;
        if (listOfOrigins != null) currentOrigin = listOfOrigins.currentObject();
        else currentOrigin = avatar.GetComponent<LM_PlayerController>().cam.gameObject;
        currentHeading = listOfHeadings.currentObject();
        currentTarget = listOfTargets.currentObject();

        // Calculate geometry
        initialLocalY = Experiment.CalculateAngleThreePoints(currentHeading.transform.position,
                                                             currentOrigin.transform.position, 
                                                             currentOrigin.transform.position + currentOrigin.transform.forward);
        initialGlobalY = rotationSource.eulerAngles.y;
        correctLocalY = Experiment.CalculateAngleThreePoints(currentHeading.transform.position, 
                                                             currentOrigin.transform.position, 
                                                             currentTarget.transform.position);

        Debug.Log(initialLocalY);
        Debug.Log(correctLocalY);
        hud.showOnlyHUD();
    }


    public override bool updateTask()
    {
        // keep time
        if (onset < 0) onset = Time.time;
        responseLatency = Time.time - onset;

        // Keep track of rotations as we go
        if (lastFrameY == Single.NaN) lastFrameY = rotationSource.eulerAngles.y;
        var deltaY = Mathf.DeltaAngle(rotationSource.eulerAngles.y, lastFrameY);
        totalRotation += deltaY;
        if (rotationSource.eulerAngles.y < lastFrameY) netClockwiseRotation -= deltaY;
        else netClockwiseRotation += deltaY;
        lastFrameY = rotationSource.eulerAngles.y;
       
        if (totalRotation > 0 && !hasMoved)
        {
            responseMovementOnset = Time.time;
            hasMoved = true;
        }

        // Handle the response input 
        if (Input.GetKeyDown(submitButton) && !responded)
        {
            finalGlobalY = rotationSource.eulerAngles.y;
            finalLocalY = Experiment.CalculateAngleThreePoints( currentHeading.transform.position, 
                                                                currentOrigin.transform.position, 
                                                                currentOrigin.transform.position + currentOrigin.transform.forward);
            responded = true;
            if (interval == 0) return true; // end with response unless duration/interval is specified
        }

        // If we're using a fixed duration just end when times up (regardless of response status
        if (interval > 0 && Time.time - onset > 0)
        {
            finalGlobalY = rotationSource.eulerAngles.y; // still record if time ran out
            finalLocalY = Experiment.CalculateAngleThreePoints( currentHeading.transform.position, 
                                                                currentOrigin.transform.position, 
                                                                currentOrigin.transform.position + currentOrigin.transform.forward);
            responded = false;
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
        Debug.Log(finalLocalY);
        absoluteError = Mathf.DeltaAngle(finalLocalY, correctLocalY);
        Debug.Log("Absolute error " + absoluteError);
        // FIXME FIXME - calculate signed error
        var underEstimated = Mathf.Abs(finalLocalY) < Mathf.Abs(correctLocalY);
        signedError = underEstimated?-1*absoluteError:absoluteError;
        
        // Logging
        taskLog.AddData(transform.name + "event_onset_s", onset.ToString());
        taskLog.AddData(transform.name + "mvmt_onset_s", responseMovementOnset.ToString());
        if (interval != 0) taskLog.AddData(transform.name + "_duration_s", (interval / 1000).ToString());
        else taskLog.AddData(transform.name + "_duration_s", "todo_FIXME"); // fixme event_duration and respDuration
        taskLog.AddData(transform.name + "_responseLatency", responseLatency.ToString());
        taskLog.AddData(transform.name + "_initialY_allo", initialGlobalY.ToString());
        taskLog.AddData(transform.name + "_initialY_ego", initialLocalY.ToString());
        taskLog.AddData(transform.name + "_totalRotation", totalRotation.ToString());
        taskLog.AddData(transform.name + "_netCWrotation", netClockwiseRotation.ToString());
        taskLog.AddData(transform.name + "_finalY_allo", finalGlobalY.ToString());
        taskLog.AddData(transform.name + "_finalY_ego", finalLocalY.ToString());
        taskLog.AddData(transform.name + "_correctY_ego", correctLocalY.ToString());
        taskLog.AddData(transform.name + "_responded", responded.ToString());
        taskLog.AddData(transform.name + "_overUnder", underEstimated?"under":"over");
        taskLog.AddData(transform.name + "_signedError", signedError.ToString());
        taskLog.AddData(transform.name + "_absError", absoluteError.ToString());
        taskLog.AddData(transform.name + "_trialType", "todo_string_stayswitch");
        taskLog.AddData(transform.name + "_stayCount", "todo_integer_trialsSinceLastSwitch");

        // Clean up
        if (canIncrementLists)
        {
            if (listOfOrigins != null) listOfOrigins.incrementCurrent();
            listOfHeadings.incrementCurrent();
            listOfTargets.incrementCurrent();
        }
    }

}