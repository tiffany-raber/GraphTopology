using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static System.Runtime.CompilerServices.RuntimeHelpers;
using TMPro;
using System.Linq;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.Events;

public class TurnToPoint : ExperimentTask
{
    [Header("Task-specific Properties")]
    public bool topDown;
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
    private Transform PointingSource;
    private Vector3 lastFrame;
    private float lastTime;
    private Vector3 initialPos;
    private float initialLocalY;
    private float totalRotation;
    private float netClockwiseRotation;
    private Vector3 finalPos;
    private float responseAngle_actual;
    private float responseAngle_correct;
    private bool responded;
    private float signedError;
    private float absoluteError;
    private bool firstUpdate;
    [Tooltip("Use {0} in place of the target name")]// Use {0} for origin, {1} for heading, {2} for target")]
    [TextArea] private string prompt = "{0}";
    public bool promptTarget;
    private int targetLayer;
    
    [Header("Properties Specific To Top-Down")]
    public GameObject topDownInterface;
    public GameObject originObject;
    public GameObject topDownObject;
    public GameObject headingIcon;
    public TextMeshProUGUI targetprompt;
    public KeyCode left = KeyCode.LeftArrow;
    public KeyCode right = KeyCode.RightArrow;
    [Tooltip("degrees per second")] public float rotSpeed = 60f; // 60 deg/s = 10 rev/min
    private GameObject ti; // target icon copy we'll use for the UI and then destroy after the trial
    [Header("Properties for stay-switch")]
    public bool staySwitch;
    public BalancedBoolList getTopDownListFrom;
    public List<bool> topDownTrialList = new List<bool>();
    private int topDownTrialIndex;
    public bool dummyTrial = true;
    private bool lastTopDown;
    private int formatRepeatCount;

    public override void startTask()
    {
        TASK_START();

        // LEAVE BLANK
    }


    public override void TASK_START()
    {
        if (!manager) Start();
        base.startTask();

        // Skip if prompted
        if (skip)
        {
            log.log("INFO    skip task    " + name, 1);
            return;
        }

        if (getTopDownListFrom != null) topDownTrialList = getTopDownListFrom.outTrials;
        else 
        {
            for (int i = 0; i < parentTask.repeat*taskLog.GetComponent<TaskList>().repeat; i++) 
            {
                topDownTrialList.Add(topDown);
            }
        }

        // Initialize variables that change from trial-to-trial
        topDown = topDownTrialList[topDownTrialIndex];
        firstUpdate = false;
        totalRotation = 0f;
        netClockwiseRotation = 0f;
        onset = -1f;
        responded = false;
        hasMoved = false;
        responseMovementOnset = 0f;
        if (topDownTrialIndex > 0) {
            if (topDown != lastTopDown) formatRepeatCount = 0;
            else formatRepeatCount++;
        }

        topDownInterface.SetActive(topDown);

        // Do we want an intial dummy trial?
        if (dummyTrial) 
        {
            currentOrigin = avatar.GetComponent<LM_PlayerController>().cam.gameObject;
            currentHeading = GameObject.Instantiate(new GameObject(), currentOrigin.transform.position + Vector3.forward, Quaternion.Euler(Vector3.zero));
            currentTarget = listOfTargets.objects[UnityEngine.Random.Range(0, listOfTargets.objects.Count)];
        }
        else
        {
            if (listOfOrigins != null) currentOrigin = listOfOrigins.currentObject();
            else currentOrigin = avatar.GetComponent<LM_PlayerController>().cam.gameObject;
            currentHeading = listOfHeadings.currentObject();
            currentTarget = listOfTargets.currentObject();
        }
       

        // Set the prompt requested
        if (promptTarget)
        {
            hud.SecondsToShow = 99999;
            prompt = currentTarget.name;
        }
        else hud.SecondsToShow = 0;

        // Set up the trial format
        if (topDown)
        {
            PointingSource = topDownObject.transform;
            // Hide the HUD and put the prompt on the Top-Down UI
            hud.setMessage(""); hud.SecondsToShow = 0;
            topDownInterface.transform.position = new Vector3(
                avatar.GetComponentInChildren<LM_SnapPoint>().transform.position.x,
                manager.playerCamera.transform.position.y,
                avatar.GetComponentInChildren<LM_SnapPoint>().transform.position.z);
            topDownInterface.transform.rotation = avatar.GetComponentInChildren<LM_SnapPoint>().transform.rotation;
            topDownInterface.transform.gameObject.SetActive(true);
            targetprompt.text = prompt;
            if (restrictMovement) manager.RestrictMovement(true, true);
            // copy the target and set it's transform to be the same as the targetIcon
            ti = new GameObject();
            ti = GameObject.Instantiate<GameObject>(currentHeading, headingIcon.transform.position, headingIcon.transform.rotation, headingIcon.transform.parent);
            foreach (var child in ti.GetComponentsInChildren<Transform>()) child.gameObject.layer = headingIcon.layer;
            ti.name = "temporaryTargetIcon";
            ti.transform.localScale = headingIcon.transform.localScale;
            headingIcon.SetActive(false);
            hud.showOnlyHUD(false);
            topDownObject.transform.localEulerAngles = Vector3.zero;
            if (manager.userInterface == UserInterface.KeyboardSingleAxis || manager.userInterface == UserInterface.KeyboardMouse)
            {
                manager.playerCamera.orthographic = true;
            }
        }
        else
        {
            if (useBodyNotCameraForRotation) PointingSource = avatar.GetComponent<LM_PlayerController>().collisionObject.transform;
            else PointingSource = avatar.GetComponent<LM_PlayerController>().cam.transform;
            hud.setMessage(prompt);
            if (restrictMovement) manager.RestrictMovement(true, false);
            currentTarget.SetActive(true);
            targetLayer = currentHeading.layer;
            Experiment.MoveToLayer(currentHeading.transform, hud.hudLayer);
            hud.showOnlyHUD();
        }



        // Calculate geometry
        initialPos = PointingSource.position;
        initialLocalY = Experiment.CalculateAngleThreePoints(currentHeading.transform.position,
                                                             currentOrigin.transform.position,
                                                             currentOrigin.transform.position + currentOrigin.transform.forward);
        responseAngle_correct = Experiment.CalculateAngleThreePoints(currentHeading.transform.position,
                                                             currentOrigin.transform.position,
                                                             currentTarget.transform.position);

        
        
    }


    public override bool updateTask()
    {
        if (topDown)
        {
                var rot = (float)(Convert.ToDouble(Input.GetKey(left)) - Convert.ToDouble(Input.GetKey(right)));
                var targetRot = topDownObject.transform.localRotation;
                targetRot *= Quaternion.Euler(0f, 0f, rot * rotSpeed * Time.deltaTime);
                topDownObject.transform.localRotation = targetRot;
                // FIXME FIXME targetprompt.transform.parent.localEulerAngles = new Vector3(0f, 0f, -1 * topDownObject.transform.localRotation.z);
        }

        if (!firstUpdate)
        {
            onset = Time.time;
            lastFrame = PointingSource.eulerAngles;
            lastTime = Time.time;
            firstUpdate = true;
        }
        else
        {
            // Track the player
            if (lastFrame.y != PointingSource.eulerAngles.y)
            {
                var deltaY = !topDown ? Mathf.DeltaAngle(PointingSource.eulerAngles.y, lastFrame.y) : Mathf.DeltaAngle(PointingSource.localEulerAngles.z, lastFrame.z);
                var deltaT = Time.time - lastTime;
                netClockwiseRotation += deltaY;
                totalRotation += Mathf.Abs(deltaY);
                if (totalRotation > 0 && !hasMoved)
                {
                    hasMoved = true;
                    responseMovementOnset = Time.time;
                }
            }
            lastFrame = !topDown ? PointingSource.eulerAngles : PointingSource.localEulerAngles;

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
        absoluteError = Mathf.Abs(Mathf.DeltaAngle(responseAngle_actual, responseAngle_correct));
        // FIXME FIXME - calculate signed error
        var underEstimated = Mathf.Abs(responseAngle_actual) < Mathf.Abs(responseAngle_correct);
        signedError = underEstimated ? -1 * absoluteError : absoluteError;

        // LOG CRITITCAL TRIAL DATA
        taskLog.AddData(transform.name + "_dummyTrial", dummyTrial.ToString());
        taskLog.AddData(transform.name + "_trial_type_topDown", topDown.ToString());
        taskLog.AddData(transform.name + "_trial_type_switch", topDown == lastTopDown ? bool.FalseString : bool.TrueString);
        taskLog.AddData(transform.name + "_stayCount", formatRepeatCount.ToString());
        taskLog.AddData(transform.name + "_origin", currentOrigin.name);
        taskLog.AddData(transform.name + "_heading",currentHeading.name);
        taskLog.AddData(transform.name + "_target", currentTarget.name);
        taskLog.AddData(transform.name + "_responded", responded.ToString());
        taskLog.AddData(transform.name + "_responseAngle_correct", responseAngle_correct.ToString());
        taskLog.AddData(transform.name + "_responseAngle_actual", responseAngle_actual.ToString());
        taskLog.AddData(transform.name + "_overUnder", underEstimated ? "under" : "over");
        taskLog.AddData(transform.name + "_signedError", signedError.ToString());
        taskLog.AddData(transform.name + "_absError", absoluteError.ToString());
        // Additional POSITION AND ROTATION DATA
        taskLog.AddData(transform.name + "_initialPosX", initialPos.x.ToString());
        taskLog.AddData(transform.name + "_initialPosZ", initialPos.z.ToString());
        taskLog.AddData(transform.name + "_initialY_ego", initialLocalY.ToString());
        taskLog.AddData(transform.name + "_totalRotation", totalRotation.ToString());
        taskLog.AddData(transform.name + "_netCWrotation", netClockwiseRotation.ToString());
        taskLog.AddData(transform.name + "_finalPosX", finalPos.x.ToString());
        taskLog.AddData(transform.name + "_finalPosZ", finalPos.z.ToString());
        // LOG EVENT TIMING (in an fMRI friendly-ish style)
        taskLog.AddData(transform.name + "_event_onset_s", onset.ToString());
        taskLog.AddData(transform.name + "_event_duration_s", ((interval != 0) ? (interval / 1000).ToString() : (timeAtResponse - onset).ToString())); // fancy if-else
        taskLog.AddData(transform.name + "_preMvmt_duration_s", (responseMovementOnset - onset).ToString());
        taskLog.AddData(transform.name + "_mvmt_onset_s", responseMovementOnset.ToString());
        taskLog.AddData(transform.name + "_mvmt_duration_s", (timeAtResponse - responseMovementOnset).ToString());
        taskLog.AddData(transform.name + "_response_onset_s", timeAtResponse.ToString());
        taskLog.AddData(transform.name + "_response_duration_s", "0");
        taskLog.AddData(transform.name + "_responseLatency_s", (timeAtResponse - onset).ToString());

        // Clean up
        if (topDown)
        {
            GameObject.Destroy(ti);
            topDownInterface.transform.gameObject.SetActive(false);
            if (manager.userInterface == UserInterface.KeyboardSingleAxis || manager.userInterface == UserInterface.KeyboardMouse)
            {
                manager.playerCamera.orthographic = false;
            }
        }

        if (canIncrementLists && !dummyTrial)
        {
            if (listOfOrigins != null) listOfOrigins.incrementCurrent();
            listOfHeadings.incrementCurrent();
            listOfTargets.incrementCurrent();
        }

        if (restrictMovement) manager.RestrictMovement(false, false);

        if (dummyTrial) dummyTrial = !dummyTrial; // the next one shouldn't be a dummy trial

        // update the format
        lastTopDown = topDown;
        topDownTrialIndex++;

        Experiment.MoveToLayer(currentHeading.transform, targetLayer);
        hud.setMessage("");
        hud.SecondsToShow = hud.GeneralDuration;
    }

    private void RecordResponse()
    {
        timeAtResponse = Time.time;
        finalPos = PointingSource.position;

        if (!topDown) responseAngle_actual = Experiment.CalculateAngleThreePoints(currentHeading.transform.position,
                                                                         currentOrigin.transform.position,
                                                                         currentOrigin.transform.position + currentOrigin.transform.forward);
        else responseAngle_actual = PointingSource.localEulerAngles.z;
        
    }


}