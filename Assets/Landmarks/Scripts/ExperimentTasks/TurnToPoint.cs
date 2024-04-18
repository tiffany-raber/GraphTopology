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
    // private GameObject currentReference;

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
    private float lastRot;
    private float lastTime;
    private float totalRotation;
    private float netClockwiseRotation;
    private Vector3 finalPos;

    // Measured variables
    private bool responded;
    private bool responseRecorded;
    // private float signedErrorCW; // TODO - remove when possible
    // private float absoluteError; // TODO - remove when possible
    [Tooltip("Use {0} in place of the target name")]// Use {0} for origin, {1} for heading, {2} for target")]
    [TextArea] private string prompt = "{0}";
    public Vector3 offsetPrompt;
    
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
    [Range(0, 180)] public float hideCurrentHeadingAngle;
    public bool colormatchTarget;

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
    //[HideInInspector] public float referenceRotNorthCW;
    [HideInInspector] public float headingRotNorthCW;

    [HideInInspector] public float correctTurnCw;
    [HideInInspector] public float observedTurnCw;
    [HideInInspector] public float errorTurnCw;
    [HideInInspector] public float errorTurnAbs;


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
        responseRecorded = false;
        hasMoved = false;
        responseMovementOnset = 0f;
        if (topDownTrialIndex > 0) {
            if (topDown != lastTopDown) formatRepeatCount = 0;
            else formatRepeatCount++;
        }

        // Specify and select the landmarks used for geometry (origin, heading, target)
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
        // do
        // {
        //     currentReference = listOfTargets.objects[UnityEngine.Random.Range(0, listOfTargets.objects.Count)];
        //     referenceRotNorthCW = Experiment.MeasureClockwiseGlobalAngle(currentOrigin, currentReference);    
        // }
        // while (currentReference.name == currentOrigin.name ||
        //         currentReference.name == currentHeading.name ||
        //         currentReference.name == currentTarget.name
        //         );

        // Set up the trial format
        if (topDown)
        {
            // Spawn a top-down interface in front of the player
            topDownInterface = Instantiate(topDownInterfacePrefab, avatar.GetComponentInChildren<LM_SnapPoint>().transform);
            // Set the color on the origin
            if (currentHeading.GetComponentInChildren<LM_TargetStore>() != null && currentHeading.GetComponentInChildren<LM_TargetStore>().exteriorElements.Length > 0)
            {
                topDownInterface.originObject.GetComponentInChildren<RawImage>().color = 
                    currentHeading.GetComponentInChildren<LM_TargetStore>().exteriorElements[0].GetComponent<Renderer>().material.color;
            }
            if (colormatchTarget && currentTarget.GetComponentInChildren<LM_TargetStore>() != null && currentTarget.GetComponentInChildren<LM_TargetStore>().exteriorElements.Length > 0)
            {
                topDownInterface.targetIcon.GetComponent<Renderer>().material.color = 
                    currentTarget.GetComponentInChildren<LM_TargetStore>().exteriorElements[0].GetComponent<Renderer>().material.color;
            }

            topDownInterface.transform.position = new Vector3(
                avatar.GetComponentInChildren<LM_SnapPoint>().transform.position.x,
                manager.playerCamera.transform.position.y,
                avatar.GetComponentInChildren<LM_SnapPoint>().transform.position.z
                );
            // topDownInterface.transform.LookAt(manager.playerCamera.transform);
            topDownInterface.transform.localRotation = Quaternion.identity;
            
            PointingSource = topDownInterface.topDownObject.transform;

            // topDownInterface.targetIcon.GetComponentInChildren<TextMeshProUGUI>().text = prompt;
            if (restrictMovement) manager.RestrictMovement(true, true);

            // Adjust to top-down if necessary (ironically requiring us to make it egocentric)
            // top-downAnswer   = targetRotNorthCW  - startRotNorthCW  (if < 0, add 360);   // Allo to ego conversion for top-down
            targetRotNorthCW -= startRotNorthCW;
            if (targetRotNorthCW < 0) targetRotNorthCW += 360; // wrap 0-360

            // referenceRotNorthCW -= startRotNorthCW;
            // if (referenceRotNorthCW < 0) referenceRotNorthCW += 360;

            var fppRotNorthCW = startRotNorthCW;
            startRotNorthCW = PointingSource.transform.localEulerAngles.y; // should be ego-zero; set after using allo startRot to calculate target

            // Arrange the top-down interface objects
            // Heading
            topDownInterface.headingIcon.transform.parent.localEulerAngles = new Vector3(
                topDownInterface.headingIcon.transform.parent.localEulerAngles.x, 
                startRotNorthCW, 
                topDownInterface.headingIcon.transform.parent.localEulerAngles.z
                );
            // // Reference
            // topDownInterface.referenceIcon.transform.parent.localEulerAngles = new Vector3(
            //     topDownInterface.referenceIcon.transform.parent.localEulerAngles.x, 
            //     referenceRotNorthCW, 
            //     topDownInterface.referenceIcon.transform.parent.localEulerAngles.z);

            // Debug.Log("Standing in front of and facing the " + currentHeading.name + ", turn to face the " + currentTarget.name);
            // Debug.Log(startRotNorthCW + "\t" + referenceRotNorthCW + "\t" + targetRotNorthCW);

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
            // ri = Instantiate(currentReference, topDownInterface.referenceIcon.transform.parent);
            // ri.transform.localPosition = topDownInterface.referenceIcon.transform.localPosition;
            // ri.transform.parent = ri.transform.parent.parent;
            // Debug.Log(currentReference.transform.eulerAngles);
            // ri.transform.localEulerAngles = currentReference.transform.eulerAngles - Vector3.up * fppRotNorthCW;
            // ri.transform.parent = topDownInterface.referenceIcon.transform.parent;
            // ri.transform.localScale = topDownInterface.referenceIcon.transform.localScale;
            // foreach (var child in ri.GetComponentsInChildren<Transform>()) Experiment.MoveToLayer(child, hud.hudLayer);
            // ri.name = "temporaryReferenceIcon";
            // topDownInterface.referenceIcon.SetActive(false);
            

            // Set up the rendering
            // if (manager.userInterface == UserInterface.KeyboardSingleAxis || manager.userInterface == UserInterface.KeyboardMouse) manager.playerCamera.orthographic = true;
        }
        else // i.e., if first-person trials
        {
            if (useBodyNotCameraForRotation) PointingSource = avatar.GetComponent<LM_PlayerController>().collisionObject.transform;
            else PointingSource = avatar.GetComponent<LM_PlayerController>().cam.transform;
            if (restrictMovement) manager.RestrictMovement(true, false);
            currentTarget.SetActive(true);
            targetLayer = currentHeading.layer;
            Experiment.MoveToLayer(currentHeading.transform, hud.hudLayer);
            // Experiment.MoveToLayer(currentReference.transform, hud.hudLayer);
        }

        // Set the prompt requested
        hud.SecondsToShow = 99999;
        prompt = currentTarget.name;
        hud.setMessage(prompt);
        if (offsetPrompt != Vector3.zero) hud.hudPanel.transform.position += offsetPrompt;
        hud.showOnlyHUD(!topDown);

        // Debug.Log(   "Facing vector: " + startRotNorthCW + "°\t" + "Target vector: " + targetRotNorthCW + "°");

        correctTurnCw = Experiment.CalculateCwAngleThreePoints(
            currentOrigin.transform.position, 
            currentHeading.transform.position, 
            currentTarget.transform.position);

        initialPos = PointingSource.position;
    }


    public override bool updateTask()
    {
        if (!firstUpdate)
        {
            onset = Time.time;
            lastRot = topDown ? PointingSource.localEulerAngles.y : PointingSource.eulerAngles.y;
            lastTime = Time.time;
            firstUpdate = true;
        }
        else
        {
            // ui offset won't update on frame when response is recorded (causing jittery HUD)
            // The (hacky) solution here is to just wait another frame by checking at the top of updateTask()
            if (responseRecorded) return true;

            // Take input for the top-down interface, if active
            if (topDown && !responded)
            {
                var netInput = (float)(System.Convert.ToDouble(Input.GetKey(right)) - System.Convert.ToDouble(Input.GetKey(left)));
                topDownInterface.topDownObject.transform.Rotate(0f, netInput * rotSpeed * Time.deltaTime, 0f);
                if (keepPromptStable) topDownInterface.targetIcon.transform.localEulerAngles =
                                        new Vector3 (0f, -1 * topDownInterface.topDownObject.transform.localEulerAngles.y, 0f);
                
                if (hideCurrentHeadingAngle != 0) 
                {
                    var wrappedNetCW = netClockwiseRotation;
                    while (wrappedNetCW > 180) wrappedNetCW -= 360;
                    while (wrappedNetCW <= -180) wrappedNetCW += 360;
                    hi.SetActive(Mathf.Abs(wrappedNetCW) < hideCurrentHeadingAngle);
                }
            }

            // Regardless of the perspective/format, record changes in the response orientation
            var thisRot = topDown ? PointingSource.localEulerAngles.y : PointingSource.eulerAngles.y;
            var deltaY = Mathf.DeltaAngle(thisRot, lastRot);
            var deltaT = Time.time - lastTime;
            netClockwiseRotation -= deltaY;
            totalRotation += Mathf.Abs(deltaY);
            if (totalRotation > 0 && !hasMoved)
            {
                hasMoved = true;
                responseMovementOnset = Time.time;
            }
            lastRot = thisRot;

            // Handle Recording the response (and ending for a response-dependent duration)
            if (Input.GetKeyDown(submitButton) && !responded && Time.time - onset >= minResponseLatency)
            {
                RecordResponse(true);
                if (interval == 0 || terminateOnResponse) responseRecorded = true; // end with response unless duration/interval is specified
            }

            // Handle the ending for a fixed duration, whether they responded or not
            if (interval > 0 && Time.time - onset > interval / 1000)
            {
                if (!responded) RecordResponse(false);
                responseRecorded = true;
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

        // TODO Remove once confirmed obsolete
        // // FIXME BEGIN
        // // Calculations 
        // signedErrorCW = -1 * Mathf.DeltaAngle(endRotNorthCW, targetRotNorthCW); // idk why they do this ccw as positive, but whatever
        // Debug.LogWarning(   "Ended at " + endRotNorthCW + "°\n" + 
        //                     "Angular error calculated to be " + signedErrorCW + "°");
        // absoluteError = Mathf.Abs(signedErrorCW);
        // //FIXME END
        
        // TRIAL TYPE INFORMATION
        taskLog.AddData(transform.name + "_dummyTrial", (parentTask.repeatCount <= dummyTrials).ToString());
        taskLog.AddData(transform.name + "_trial_type_topDown", topDown.ToString());
        taskLog.AddData(transform.name + "_trial_type_switch", topDown == lastTopDown ? bool.FalseString : bool.TrueString);
        taskLog.AddData(transform.name + "_stayCount", formatRepeatCount.ToString());

        // CRITICAL TRIAL PARAMETERS AND MEASUREMENTS
        taskLog.AddData(transform.name + "_origin", currentOrigin.name);
        taskLog.AddData(transform.name + "_originPosX", currentOrigin.transform.position.x.ToString());
        taskLog.AddData(transform.name + "_originPosZ", currentOrigin.transform.position.z.ToString());
        taskLog.AddData(transform.name + "_originRotY", currentOrigin.transform.eulerAngles.y.ToString());
        taskLog.AddData(transform.name + "_heading",currentHeading.name);
        taskLog.AddData(transform.name + "_headingPosX", currentHeading.transform.position.x.ToString());
        taskLog.AddData(transform.name + "_headingPosZ", currentHeading.transform.position.z.ToString());
        taskLog.AddData(transform.name + "_headingRotY", currentHeading.transform.eulerAngles.y.ToString());
        taskLog.AddData(transform.name + "_target", currentTarget.name);
        taskLog.AddData(transform.name + "_targetPosX", currentTarget.transform.position.x.ToString());
        taskLog.AddData(transform.name + "_targetPosZ", currentTarget.transform.position.z.ToString());
        taskLog.AddData(transform.name + "_targetRotY", currentTarget.transform.eulerAngles.y.ToString());
        taskLog.AddData(transform.name + "_initialPosX", initialPos.x.ToString());
        taskLog.AddData(transform.name + "_initialPosZ", initialPos.z.ToString());
        // taskLog.AddData(transform.name + "_initialRotY", .ToString()); // FIXME get rotation
        taskLog.AddData(transform.name + "_finalPosX", finalPos.x.ToString());
        taskLog.AddData(transform.name + "_finalPosZ", finalPos.z.ToString());
        // taskLog.AddData(transform.name + "_finalRotY", .ToString()); // FIXME get rotation

        // BEHAVIORAL MEASUREMENTS AND METRICS
        taskLog.AddData(transform.name + "_responded", responded.ToString());
        taskLog.AddData(transform.name + "_correctTurn_cwDeg", correctTurnCw.ToString());
        taskLog.AddData(transform.name + "_observedTurn_cwDeg", observedTurnCw.ToString());
        taskLog.AddData(transform.name + "_signedError_cwDeg", errorTurnCw.ToString());
        taskLog.AddData(transform.name + "_absoluteError_Deg", errorTurnAbs.ToString());
        // taskLog.AddData(transform.name + "_targetResponseAngle", targetRotNorthCW.ToString()); //fixme // TODO - remove when possible
        // taskLog.AddData(transform.name + "_observedResponseAngle", endRotNorthCW.ToString()); //fixme // TODO - remove when possible
        // taskLog.AddData(transform.name + "_signedErrorCW_deg", signedErrorCW.ToString()); //fixme // TODO - remove when possible
        // taskLog.AddData(transform.name + "_absError", absoluteError.ToString()); //fixme // TODO - remove when possible
        taskLog.AddData(transform.name + "_responseLatency_s", (timeAtResponse - onset).ToString());

        // ADDITIONAL POSITION AND ROTATION DATA
        taskLog.AddData(transform.name + "_totalRotation", totalRotation.ToString());
        taskLog.AddData(transform.name + "_netCWrotation", netClockwiseRotation.ToString());

        // EVENT TIMING (in an fMRI friendly-ish style)
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
        }

        if (restrictMovement) manager.RestrictMovement(false, false);

        // update the format
        lastTopDown = topDown;
        topDownTrialIndex++;
        
        if (offsetPrompt != Vector3.zero) hud.hudPanel.transform.position -= offsetPrompt;
    }

    private void RecordResponse(bool responseProvided)
    {   
        hud.SecondsToShow = 0;
        hud.setMessage("");

        // HIDE VISUAL INFO SO WE CAN DO SOME CALCULATIONS IN PRIVATE
        if (!topDown) 
        {
            Experiment.MoveToLayer(currentHeading.transform, targetLayer);
            // Experiment.MoveToLayer(currentReference.transform, targetLayer);
        }
        else 
        {
            //responseAngle_actual = PointingSource.localEulerAngles.z;
            // while (responseAngle_actual > 180) responseAngle_actual -= 180;
            // while (responseAngle_actual < -180) responseAngle_actual +=180;
            Destroy(hi);
            Destroy(ri);
            Destroy(topDownInterface.gameObject);
            // if (manager.userInterface == UserInterface.KeyboardSingleAxis || manager.userInterface == UserInterface.KeyboardMouse) manager.playerCamera.orthographic = false;
        }
        Experiment.MoveToLayer(currentHeading.transform, targetLayer);
        // if (interval > 0) 
        // {
        //     hud.setMessage("+");
        //     hud.hudPanel.GetComponent<Image>().enabled = false;
        //     hud.SecondsToShow = interval;
        //     hud.showOnlyHUD(false);
        // }

        // RECORD DATA
        timeAtResponse = Time.time;
        responded = responseProvided;
        // observedTurnCw = topDown ? 
        //     netClockwiseRotation :
        //     Experiment.CalculateCwAngleThreePoints( currentOrigin.transform.position, currentHeading.transform.position, avatar.GetComponentInChildren<LM_SnapPoint>().transform.position);
        observedTurnCw = netClockwiseRotation;
        if (topDown) avatar.transform.localEulerAngles += new Vector3(0f, netClockwiseRotation, 0f);
        errorTurnCw = Experiment.CalculateCwAngleThreePoints(currentOrigin.transform.position, currentTarget.transform.position, avatar.GetComponentInChildren<LM_SnapPoint>().transform.position);
        errorTurnAbs = Mathf.Abs(errorTurnCw);
        finalPos = PointingSource.position;

        endRotNorthCW = topDown ?   -1 * PointingSource.transform.localEulerAngles.z : // It seems rectTransforms (i.e. gui objects) rotate differently?
                                    PointingSource.transform.eulerAngles.y; // just the y rotation of the player
        if (endRotNorthCW < 0) endRotNorthCW += 360; // wrap 0-360
    }
}