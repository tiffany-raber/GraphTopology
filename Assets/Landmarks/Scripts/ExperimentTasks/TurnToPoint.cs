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
    public bool restrictMovement;
    private GameObject currentOrigin;
    private GameObject currentHeading;
    private GameObject currentTarget;
    private float onset;
    private float responseMovementOnset;
    private bool hasMoved;
    private float timeAtResponse;
    public KeyCode submitButton = KeyCode.UpArrow;
    public bool useBodyNotCameraForRotation;
    private Transform rotationSource;
    private Vector3 lastFrame;
    private Vector3 initialPos;
    private float initialGlobalY;
    private float initialLocalY;
    private float totalRotation;
    private float netClockwiseRotation;
    private Vector3 finalPos;
    private float finalGlobalY;
    private float finalLocalY;
    private float correctLocalY;
    private bool responded;
    private float signedError;
    private float absoluteError;
    private bool firstUpdate;
    [Tooltip("Use {0} in place of the target name")]// Use {0} for origin, {1} for heading, {2} for target")]
    [TextArea] private string prompt = "{0}";
    public bool promptTarget;
    private int targetLayer;

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

        if (restrictMovement) avatar.GetComponentInChildren<CharacterController>().enabled = false;

        // Initialize variables that change from trial-to-trial
        firstUpdate = false;
        if (useBodyNotCameraForRotation) rotationSource = avatar.GetComponent<LM_PlayerController>().collisionObject.transform;
        else rotationSource = avatar.GetComponent<LM_PlayerController>().cam.transform;
        totalRotation = 0;
        netClockwiseRotation = 0;
        onset = -1f;
        responded = false;
        responseMovementOnset = Single.NaN;
        if (listOfOrigins != null) currentOrigin = listOfOrigins.currentObject();
        else currentOrigin = avatar.GetComponent<LM_PlayerController>().cam.gameObject;
        currentHeading = listOfHeadings.currentObject();
        currentTarget = listOfTargets.currentObject();

        // Calculate geometry
        initialPos = rotationSource.position;
        initialLocalY = Experiment.CalculateAngleThreePoints(currentHeading.transform.position,
                                                             currentOrigin.transform.position,
                                                             currentOrigin.transform.position + currentOrigin.transform.forward);
        initialGlobalY = rotationSource.eulerAngles.y;
        correctLocalY = Experiment.CalculateAngleThreePoints(currentHeading.transform.position,
                                                             currentOrigin.transform.position,
                                                             currentTarget.transform.position);
        if (promptTarget) 
        {
            hud.SecondsToShow = 99999;
            prompt = currentTarget.name;
        }
        else hud.SecondsToShow = 0;
        hud.setMessage(prompt);

        // but Turn on the current object
        currentTarget.SetActive(true);

        targetLayer = currentHeading.layer;
		Experiment.MoveToLayer(currentHeading.transform, hud.hudLayer);
        hud.showOnlyHUD();
    }


    public override bool updateTask()
    {
        if (!firstUpdate)
        {
            onset = Time.time;
            lastFrame = rotationSource.eulerAngles;
            firstUpdate = true;
        }
        else
        {
            // Track the player
            if (lastFrame.y != rotationSource.eulerAngles.y)
            {
                var deltaY = Mathf.DeltaAngle(rotationSource.eulerAngles.y, lastFrame.y);
                netClockwiseRotation += deltaY;
                totalRotation += Mathf.Abs(deltaY);
                if (totalRotation > 0 && !hasMoved)
                {
                    hasMoved = true;
                    responseMovementOnset = Time.time;
                }

            }
            lastFrame = rotationSource.eulerAngles;

            // Handle Recording the response (and ending for a response-dependent duration)
            if (Input.GetKeyDown(submitButton) && !responded)
            {
                RecordResponse();
                responded = true;
                if (interval == 0) return true; // end with response unless duration/interval is specified
            }

            // Handle the ending for a fixed duration
            if (interval > 0 && Time.time - onset > interval / 1000)
            {
                if (!responded)
                {
                    RecordResponse();
                    responded = false;
                }
                return true;
            }
        }

        // If noting trigger the end of this task, keep going
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
        absoluteError = Mathf.DeltaAngle(finalLocalY, correctLocalY);
        // FIXME FIXME - calculate signed error
        var underEstimated = Mathf.Abs(finalLocalY) < Mathf.Abs(correctLocalY);
        signedError = underEstimated ? -1 * absoluteError : absoluteError;

        // LOG CRITITCAL TRIAL DATA
        taskLog.AddData(transform.name + "_trial_type", "todo_string_stayOrSwitch");
        taskLog.AddData(transform.name + "_stayCount", "todo_integer_trialsSinceLastSwitch");
        taskLog.AddData(transform.name + "_responded", responded.ToString());
        taskLog.AddData(transform.name + "_overUnder", underEstimated ? "under" : "over");
        taskLog.AddData(transform.name + "_signedError", signedError.ToString());
        taskLog.AddData(transform.name + "_absError", absoluteError.ToString());
        // LOG EVENT TIMING (in an fMRI friendly-ish style)
        taskLog.AddData(transform.name + "_event_onset_s", onset.ToString());
        taskLog.AddData(transform.name + "_event_duration_s", ((interval != 0) ? (interval / 1000).ToString() : (timeAtResponse - onset).ToString())); // fancy if-else
        taskLog.AddData(transform.name + "_preMvmt_duration_s", (responseMovementOnset - onset).ToString());
        taskLog.AddData(transform.name + "_mvmt_onset_s", responseMovementOnset.ToString());
        taskLog.AddData(transform.name + "_mvmt_duration_s", (timeAtResponse - responseMovementOnset).ToString());
        taskLog.AddData(transform.name + "_response_onset_s", timeAtResponse.ToString());
        taskLog.AddData(transform.name + "_response_duration_s", "0");
        taskLog.AddData(transform.name + "_responseLatency_s", (timeAtResponse - onset).ToString());
        // LOG POSITION AND ROTATION DATA
        taskLog.AddData(transform.name + "_initialPosX", initialPos.x.ToString());
        taskLog.AddData(transform.name + "_initialPosZ", initialPos.z.ToString());
        taskLog.AddData(transform.name + "_initialY_allo", initialGlobalY.ToString());
        taskLog.AddData(transform.name + "_initialY_ego", initialLocalY.ToString());
        taskLog.AddData(transform.name + "_correctY_ego", correctLocalY.ToString());
        taskLog.AddData(transform.name + "_totalRotation", totalRotation.ToString());
        taskLog.AddData(transform.name + "_netCWrotation", netClockwiseRotation.ToString());
        taskLog.AddData(transform.name + "_finalPosX", finalPos.x.ToString());
        taskLog.AddData(transform.name + "_finalPosZ", finalPos.z.ToString());
        taskLog.AddData(transform.name + "_finalY_allo", finalGlobalY.ToString());
        taskLog.AddData(transform.name + "_finalY_ego", finalLocalY.ToString());

        // Clean up
        if (canIncrementLists)
        {
            if (listOfOrigins != null) listOfOrigins.incrementCurrent();
            listOfHeadings.incrementCurrent();
            listOfTargets.incrementCurrent();
        }

        if (restrictMovement) manager.RestrictMovement(false);

        Experiment.MoveToLayer(currentHeading.transform, targetLayer);
        hud.setMessage("");
        hud.SecondsToShow = hud.GeneralDuration;
    }

    private void RecordResponse()
    {
        timeAtResponse = Time.time;
        finalPos = rotationSource.position;
        finalGlobalY = rotationSource.eulerAngles.y;
        finalLocalY = Experiment.CalculateAngleThreePoints(currentHeading.transform.position,
                                                            currentOrigin.transform.position,
                                                            currentOrigin.transform.position + currentOrigin.transform.forward);
    }
}