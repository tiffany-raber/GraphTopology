using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityStandardAssets.Characters.FirstPerson;

public class TurnToPoint : ExperimentTask
{
    [Header("Task-specific Properties")]
    public bool topDown;
    [Tooltip("Leave blank to use the player/avatar")]
    public ObjectList listOfOrigins;
    public ObjectList listOfHeadings;
    public ObjectList listOfTargets;
    public bool restrictMovement;
    [Tooltip("When can a response be submitted (in seconds)")] 
    public float minResponseLatency;
    public KeyCode submitButton = KeyCode.UpArrow;

    // Store targetObjects used to define this trial
    private GameObject currentOrigin;
    private GameObject currentHeading;
    private GameObject currentTarget;
    private GameObject currentReference;

    // Track trial data
    private bool firstUpdate;
    private float onset;
    private float responseMovementOnset;
    private bool hasMoved;
    private int targetLayer;
    private float timeAtResponse;
    public bool terminateOnResponse;
    
    public bool useBodyNotCameraForRotation;
    private Transform PointingSource;
    private Vector3 initialPos;
    private Vector3 lastFrame;
    private float lastTime;
    private float totalRotation;
    private float netClockwiseRotation;
    private Vector3 finalPos;

    // Measured variables
    private bool responded;
    private float signedErrorCW;
    private float absoluteError;
    [Tooltip("Use {0} in place of the target name")]// Use {0} for origin, {1} for heading, {2} for target")]
    [TextArea] private string prompt = "{0}";
    
    [Header("Properties Specific To Top-Down")]
    public TopDownPointingInterface topDownInterfacePrefab;
    private TopDownPointingInterface topDownInterface;
    // Options
    public bool keepPromptStable = true;
    private GameObject hi; // heading icon copy we'll use for the UI and then destroy after the trial
    private GameObject ri; // reference icon copy we'll use for hte UI and then destroy
    public KeyCode left = KeyCode.LeftArrow;
    public KeyCode right = KeyCode.RightArrow;
    [Tooltip("degrees per second")] public float rotSpeed = 60f; // 60 deg/s = 10 rev/min

    [Header("Properties for stay-switch")]
    public BalancedBoolList getTopDownListFrom;
    public List<bool> topDownTrialList = new List<bool>();
    public Vector3 localOffsetFacing;
    private int topDownTrialIndex;
    [Min(0)] public int dummyTrials = 0;
    private bool lastTopDown;
    private int formatRepeatCount;

    // Trig Check
    [HideInInspector]
    public float startRotNorthCW;
    [HideInInspector] public float endRotNorthCW;
    [HideInInspector] public float targetRotNorthCW;
    [HideInInspector] public float referenceRotNorthCW;
    [HideInInspector] public float headingRotNorthCW;


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
        else for (int i = 0; i < parentTask.repeat*taskLog.GetComponent<TaskList>().repeat; i++) topDownTrialList.Add(topDown);

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

        // Set up the trial properties and configure
        if (parentTask.repeatCount <= dummyTrials) 
        {
            // TODO: handle if they provide an originlist
            currentOrigin = avatar.GetComponent<LM_PlayerController>().cam.gameObject;
            currentHeading = listOfTargets.objects[UnityEngine.Random.Range(0, listOfTargets.objects.Count)];
            do 
            {
                currentTarget = listOfTargets.objects[UnityEngine.Random.Range(0, listOfTargets.objects.Count)];
            } while (currentTarget.name == currentHeading.name);

        }
        else
        {
            if (listOfOrigins != null) currentOrigin = listOfOrigins.currentObject();
            else currentOrigin = avatar.GetComponent<LM_PlayerController>().cam.gameObject;
            currentHeading = listOfHeadings.currentObject();
            currentTarget = listOfTargets.currentObject();
        }
       
        // Put the player in front of the store
        if (restrictMovement) manager.RestrictMovement(true, topDown);
        avatar.GetComponentInChildren<CharacterController>().enabled = false;
        if (avatar.GetComponentInChildren<FirstPersonController>()) avatar.GetComponentInChildren<FirstPersonController>().enabled = false;
        avatar.transform.position = listOfOrigins != null ? currentOrigin.transform.position + currentOrigin.transform.TransformDirection(localOffsetFacing) : 
                                                            currentHeading.transform.position+ currentHeading.transform.TransformDirection(localOffsetFacing);
        avatar.transform.LookAt(currentHeading.transform);
        avatar.transform.eulerAngles = Vector3.Scale(avatar.transform.eulerAngles, Vector3.up);
        avatar.GetComponentInChildren<CharacterController>().enabled = true;
        if (avatar.GetComponentInChildren<FirstPersonController>()) avatar.GetComponentInChildren<FirstPersonController>().enabled = true;

        // Calculate geometry now that the player is positioned
        targetRotNorthCW = Experiment.MeasureClockwiseGlobalAngle(currentOrigin, currentTarget);
        headingRotNorthCW = Experiment.MeasureClockwiseGlobalAngle(currentOrigin, currentHeading);
        startRotNorthCW = avatar.transform.eulerAngles.y;

        // Select and position a reference object to constrain the correct repsonse (mostly for top-down)
        do
        {
            currentReference = listOfTargets.objects[UnityEngine.Random.Range(0, listOfTargets.objects.Count)];
            referenceRotNorthCW = Experiment.MeasureClockwiseGlobalAngle(currentOrigin, currentReference);    
        }
        while (currentReference.name == currentOrigin.name ||
                currentReference.name == currentHeading.name ||
                currentReference.name == currentTarget.name
                );

        // Set the prompt requested
        hud.SecondsToShow = 99999;
        prompt = currentTarget.name;

        // Set up the trial format
        if (topDown)
        {
            // Spawn a top-down interface in front of the player
            topDownInterface = Instantiate(topDownInterfacePrefab, avatar.GetComponentInChildren<LM_SnapPoint>().transform);
            topDownInterface.transform.position = new Vector3(
                avatar.GetComponentInChildren<LM_SnapPoint>().transform.position.x,
                manager.playerCamera.transform.position.y,
                avatar.GetComponentInChildren<LM_SnapPoint>().transform.position.z
                );
            // topDownInterface.transform.LookAt(manager.playerCamera.transform);
            topDownInterface.transform.localRotation = Quaternion.identity;
            
            PointingSource = topDownInterface.topDownObject.transform;
            // Hide the HUD and put the prompt on the Top-Down UI
            hud.setMessage(""); hud.SecondsToShow = 0;
            topDownInterface.targetIcon.GetComponentInChildren<TextMeshProUGUI>().text = prompt;
            if (restrictMovement) manager.RestrictMovement(true, true);

            // Adjust to top-down if necessary (ironically requiring us to make it egocentric)
            // top-downAnswer   = targetRotNorthCW  - startRotNorthCW  (if < 0, add 360);   // Allo to ego conversion for top-down
            targetRotNorthCW -= startRotNorthCW;
            if (targetRotNorthCW < 0) targetRotNorthCW += 360; // wrap 0-360

            referenceRotNorthCW -= startRotNorthCW;
            if (referenceRotNorthCW < 0) referenceRotNorthCW += 360;

            var fppRotNorthCW = startRotNorthCW;
            startRotNorthCW = PointingSource.transform.localEulerAngles.y; // should be ego-zero; set after using allo startRot to calculate target

            // Arrange the top-down interface objects
            // Heading
            topDownInterface.headingIcon.transform.parent.localEulerAngles = new Vector3(
                topDownInterface.headingIcon.transform.parent.localEulerAngles.x, 
                startRotNorthCW, 
                topDownInterface.headingIcon.transform.parent.localEulerAngles.z
                );
            // Reference
            topDownInterface.referenceIcon.transform.parent.localEulerAngles = new Vector3(
                topDownInterface.referenceIcon.transform.parent.localEulerAngles.x, 
                referenceRotNorthCW, 
                topDownInterface.referenceIcon.transform.parent.localEulerAngles.z);

            Debug.Log("Standing in front of and facing the " + currentHeading.name + ", turn to face the " + currentTarget.name);
            Debug.Log(startRotNorthCW + "\t" + referenceRotNorthCW + "\t" + targetRotNorthCW);

            // copy the target and set it's transform to be the same as the targetIcon
            hi = Instantiate(currentHeading, topDownInterface.headingIcon.transform.parent);
            hi.transform.localPosition = topDownInterface.headingIcon.transform.localPosition;
            hi.transform.parent = hi.transform.parent.parent;
            hi.transform.localEulerAngles = currentHeading.transform.eulerAngles - Vector3.up * fppRotNorthCW;
            hi.transform.parent = topDownInterface.headingIcon.transform.parent;
            hi.transform.localScale = topDownInterface.headingIcon.transform.localScale;
            foreach (var child in hi.GetComponentsInChildren<Transform>()) Experiment.MoveToLayer(child, hud.hudLayer);
            hi.name = "temporaryTargetIcon";
            topDownInterface.headingIcon.SetActive(false);

            // Copy the reference and set it's transform to be the same as the referenceIcon
            ri = Instantiate(currentReference, topDownInterface.referenceIcon.transform.parent);
            ri.transform.localPosition = topDownInterface.referenceIcon.transform.localPosition;
            ri.transform.parent = ri.transform.parent.parent;
            Debug.Log(currentReference.transform.eulerAngles);
            ri.transform.localEulerAngles = currentReference.transform.eulerAngles - Vector3.up * fppRotNorthCW;
            ri.transform.parent = topDownInterface.referenceIcon.transform.parent;
            ri.transform.localScale = topDownInterface.referenceIcon.transform.localScale;
            foreach (var child in ri.GetComponentsInChildren<Transform>()) Experiment.MoveToLayer(child, hud.hudLayer);
            ri.name = "temporaryReferenceIcon";
            topDownInterface.referenceIcon.SetActive(false);
            

            // Set up the rendering
            hud.showOnlyHUD(false);
            if (manager.userInterface == UserInterface.KeyboardSingleAxis || manager.userInterface == UserInterface.KeyboardMouse)
            {
                manager.playerCamera.orthographic = true;
            }
        }
        else // i.e., if first-person trials
        {
            if (useBodyNotCameraForRotation) PointingSource = avatar.GetComponent<LM_PlayerController>().collisionObject.transform;
            else PointingSource = avatar.GetComponent<LM_PlayerController>().cam.transform;
            hud.setMessage(prompt);
            if (restrictMovement) manager.RestrictMovement(true, false);
            currentTarget.SetActive(true);
            targetLayer = currentHeading.layer;
            Experiment.MoveToLayer(currentHeading.transform, hud.hudLayer);
            Experiment.MoveToLayer(currentReference.transform, hud.hudLayer);
            hud.showOnlyHUD();
        }

        // Debug.Log(   "Facing vector: " + startRotNorthCW + "°\t" + "Target vector: " + targetRotNorthCW + "°");

        initialPos = currentOrigin.transform.position;
    }


    public override bool updateTask()
    {
        if (!firstUpdate)
        {
            onset = Time.time;
            lastFrame = !topDown ? PointingSource.eulerAngles : PointingSource.localEulerAngles;
            lastTime = Time.time;
            firstUpdate = true;
        }
        else
        {
            // Take input for the top-down interface, if active
            if (topDown && !responded)
            {
                var netInput = (float)(System.Convert.ToDouble(Input.GetKey(right)) - System.Convert.ToDouble(Input.GetKey(left)));
                topDownInterface.topDownObject.transform.Rotate(0f, netInput * rotSpeed * Time.deltaTime, 0f);
                if (keepPromptStable) topDownInterface.targetIcon.transform.localEulerAngles =
                                        new Vector3 (0f, -1 * topDownInterface.topDownObject.transform.localEulerAngles.y, 0f);
            }

            // Regardless of the perspective/format, record changes in the response orientation
            if (lastFrame.y != PointingSource.eulerAngles.y && !responded)
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
            if (Input.GetKeyDown(submitButton) && !responded && Time.time - onset >= minResponseLatency)
            {
                RecordResponse(true);
                manager.RestrictMovement(true, true);
                if (interval == 0 || terminateOnResponse) return true; // end with response unless duration/interval is specified
            }

            // Handle the ending for a fixed duration, whether they responded or not
            if (interval > 0 && Time.time - onset > interval / 1000)
            {
                if (!responded) RecordResponse(false);
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
        signedErrorCW = -1 * Mathf.DeltaAngle(endRotNorthCW, targetRotNorthCW); // idk why they do this ccw as positive, but whatever
        Debug.LogWarning(   "Ended at " + endRotNorthCW + "°\n" + 
                            "Angular error calculated to be " + signedErrorCW + "°");
        absoluteError = Mathf.Abs(signedErrorCW);
        
        // LOG CRITITCAL TRIAL DATA
        taskLog.AddData(transform.name + "_dummyTrial", (parentTask.repeatCount <= dummyTrials).ToString());
        taskLog.AddData(transform.name + "_trial_type_topDown", topDown.ToString());
        taskLog.AddData(transform.name + "_trial_type_switch", topDown == lastTopDown ? bool.FalseString : bool.TrueString);
        taskLog.AddData(transform.name + "_stayCount", formatRepeatCount.ToString());
        taskLog.AddData(transform.name + "_origin", currentOrigin.name);
        taskLog.AddData(transform.name + "_heading",currentHeading.name);
        taskLog.AddData(transform.name + "_target", currentTarget.name);
        taskLog.AddData(transform.name + "_responded", responded.ToString());
        taskLog.AddData(transform.name + "_targetResponseAngle", targetRotNorthCW.ToString());
        taskLog.AddData(transform.name + "_observedResponseAngle", endRotNorthCW.ToString());
        taskLog.AddData(transform.name + "_signedErrorCW_deg", signedErrorCW.ToString());
        taskLog.AddData(transform.name + "_absError", absoluteError.ToString());
        taskLog.AddData(transform.name + "_responseLatency_s", (timeAtResponse - onset).ToString());
        // ADDITIONAL POSITION AND ROTATION DATA
        taskLog.AddData(transform.name + "_initialPosX", initialPos.x.ToString());
        taskLog.AddData(transform.name + "_initialPosZ", initialPos.z.ToString());
        taskLog.AddData(transform.name + "_totalRotation", totalRotation.ToString());
        taskLog.AddData(transform.name + "_netCWrotation", netClockwiseRotation.ToString());
        taskLog.AddData(transform.name + "_finalPosX", finalPos.x.ToString());
        taskLog.AddData(transform.name + "_finalPosZ", finalPos.z.ToString());
        // LOG EVENT TIMING (in an fMRI friendly-ish style)
        taskLog.AddData(transform.name + "_event-trial_onset_s", onset.ToString());
        taskLog.AddData(transform.name + "_event-trial_duration_s", interval != 0 ? (interval / 1000).ToString() : 
                                                                                    (timeAtResponse - onset).ToString());
        taskLog.AddData(transform.name + "_event-preMvmt_onset_s", onset.ToString());
        taskLog.AddData(transform.name + "_event-preMvmt_duration_s", hasMoved ? (responseMovementOnset - onset).ToString() : 
                                                                                 (timeAtResponse - onset).ToString());
        taskLog.AddData(transform.name + "_event-mvmt_onset_s", hasMoved ? responseMovementOnset.ToString() : 
                                                                           timeAtResponse.ToString());
        taskLog.AddData(transform.name + "_event-mvmt_duration_s", hasMoved ? (timeAtResponse - responseMovementOnset).ToString() : "0");
        taskLog.AddData(transform.name + "_event-response_onset_s", timeAtResponse.ToString());
        taskLog.AddData(transform.name + "_event-response_duration_s", "0");

        // Clean up
        if (canIncrementLists && parentTask.repeatCount > dummyTrials)
        {
            if (listOfOrigins != null) listOfOrigins.incrementCurrent();
            listOfHeadings.incrementCurrent();
            listOfTargets.incrementCurrent();
            if (topDown) PointingSource.transform.localRotation = Quaternion.Euler(Vector3.zero);
        }

        if (restrictMovement) manager.RestrictMovement(false, false);

        // update the format
        lastTopDown = topDown;
        topDownTrialIndex++;

        hud.setMessage("");
        hud.SecondsToShow = hud.GeneralDuration;
        hud.hudPanel.GetComponent<Image>().enabled = true;
    }

    private void RecordResponse(bool responseProvided)
    {
        timeAtResponse = Time.time;
        finalPos = PointingSource.position;

        endRotNorthCW = topDown ?   -1 * PointingSource.transform.localEulerAngles.z : // It seems rectTransforms (i.e. gui objects) rotate differently?
                                    PointingSource.transform.eulerAngles.y; // just the y rotation of the player
        if (endRotNorthCW < 0) endRotNorthCW += 360; // wrap 0-360

        if (!topDown) 
        {
            Experiment.MoveToLayer(currentHeading.transform, targetLayer);
            Experiment.MoveToLayer(currentReference.transform, targetLayer);
        }
        else 
        {
            //responseAngle_actual = PointingSource.localEulerAngles.z;
            // while (responseAngle_actual > 180) responseAngle_actual -= 180;
            // while (responseAngle_actual < -180) responseAngle_actual +=180;
            Destroy(hi);
            Destroy(ri);
            Destroy(topDownInterface.gameObject);
            if (manager.userInterface == UserInterface.KeyboardSingleAxis || manager.userInterface == UserInterface.KeyboardMouse)
            {
                manager.playerCamera.orthographic = false;
            }
        }

        responded = responseProvided;

        Experiment.MoveToLayer(currentHeading.transform, targetLayer);
        if (interval > 0) 
        {
            hud.setMessage("+");
            hud.hudPanel.GetComponent<Image>().enabled = false;
            hud.SecondsToShow = interval;
            hud.showOnlyHUD(false);
        }
    }
}